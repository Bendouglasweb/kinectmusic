
#include <SPI.h>
//#include "C:\Users\David\Google Drive\UofL\CECS 596\Capstone Team\FreqMeasure-master\FreqMeasure.h"
#include <FreqMeasure.h>

// TODO
// - Change dp function to char array, for heap fragmentation stuff
// - Change parseCSV to not need number of characters sent to it

// chip select pins in order of oscillators, 100k, 5k, timer 1, 100k, 5k timer 2 etc.


const int freqMeasurePin = 49;                // This is just here as a note

// *** Settings -- Physical
const float onehundredkdiv  = 100000 / 256;		// Step size of 100k Rheostat
const float fivekdiv = 5000 / 256;				    // Step size of 5k Rheostat
const int wiperMax = 255;                     // Max SPI input to Rheostats
const int wiperMin = 0;                       // Min SPI input to Rheostats
const long maxResistance = 105000;             // Max possible resistance
const int numOsc = 6;                         // Number of oscillators controlled by the Arduino
const int numRheo = numOsc * 2;               // Number of rheostats controlled by the Arduino
const long baudRate = 9600;                    // Serial baud rate

// *** Settings -- Software
int mode = 0;								            // Mode (e.g., play/tuning) variable, and initial mode
int numFreqSample = 75;						      // Number of frequency samples to take

int debug = 0;    							        // Serial debugging. 1 = on
int quickanddirty = 0;						      // Temp frequency fix

// *** Variables used in Serial Comm
char endMarker = '\n';                  // What symbol is considered the end of a request
const int maxCSVVals = 128;              // Max number of CSV values that we're going to hold
const int sizeOfMaxCSVPair = 9;         // Max size of a single CSV value pair
const int maxSerChars = (maxCSVVals/2) * sizeOfMaxCSVPair + 1;           // Max num of characters we can process in a single Serial request

// *** Sequencer Variables
int BPM;                                // Beats Per Minute
int tst;                                // Time signature Top
long seq1vals[maxCSVVals];               // Sequencer 1 Values;

char receivedChars[maxSerChars];        // Array to store received data

long csvValues[maxCSVVals];                     // Used as storage for CSV parser

int OutValues[12] = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0};  // Output wiper values. 
  // { 100k Rheo 1 Val, 5k Rheo 1 Val, 100k Rheo 2 Val, 5k Rheo 2 Val, .. 100k Rheo 5 Val, 5k Rheo 5 Val, }

// Other
long memi[16];                           // Used as reserved memory to transfer data between things. "Memory, integers"
char m = '\0';                          // Used in main loop

const int SlavePins [numRheo] = {23, 25, 27, 29, 31, 33, 35, 37, 39, 41, 43, 45};                 
  // 23:  100k Rheo 0
  // 25:  5k Rheo 0
  // 27:  100k Rheo 1
  // 29:  5k Rheo 1
  // 31:  100k Rheo 2
  // 33:  5k Rheo 2
  // 35:  100k Rheo 3
  // 37:  5k Rheo 3
  // 39:  100k Rheo 4
  // 41:  5k Rheo 4
  // 43:  100k Rheo 5
  // 45:  5k Rheo 5

  const int OscTransistorPins[numOsc] = {22,24,26,28,30,32};
  // 22: Oscillator 0 Transistor Pin (With 22k Pullup)
  // 24: Oscillator 1 Transistor Pin (With 22k Pullup)
  // 26: Oscillator 2 Transistor Pin (With 22k Pullup)
  // 28: Oscillator 3 Transistor Pin (With 22k Pullup)
  // 30: Oscillator 4 Transistor Pin (With 22k Pullup)
  // 32: Oscillator 5 Transistor Pin (With 22k Pullup)



// Debugging functions. Handles Serial output of debugging information
void dp(char* input) {
  if (debug == 1) {
    Serial.print(input);
  }
}

// Integer debug print
void dpi(long input) {
  if (debug == 1) {
    Serial.print(input);
  }
}

// char debug print
void dpc(char input) {
  if (debug == 1) {
    Serial.print(input);
  }
}

// Function reads in next available serial command
void readSerial() {
  int numChars = 0;                     // Num Chars recieved. Will be returned
  bool eod = false;                     // End of Data. Only exit once a endMarker is recieved.
  char rc;                              // "Recieved Character". Used for reading in a character

  // Clear receivedChars for new message.
  for (int j = 0; j < maxSerChars; j++) {
        receivedChars[j] = '\0';
      }

  while (Serial.available() > 0 && eod == false) {
    rc = Serial.read();
    if (rc != endMarker) {              // Not at end marker
      receivedChars[numChars] = rc;
      numChars++;
      if (numChars >= maxSerChars) {    // Exceeded max input size
        Serial.println("e:1");

        // If a serial request was too long, i.e. we received too many characters before receiving en endMaker,
        // then loop through the remaining serial buffer to clear it until we reach either the end of the
        // serial data available, or reach an endMarker.
        // This will help prevent the system from getting stuck on bad requests. 
        while (Serial.available() > 0) {
          if (Serial.read() == endMarker) {
            break;
          }
        }        
      }
      while ( Serial.available() == 0 ) ;     // Wait for serial
    }
    else {                              // Reached end marker
        receivedChars[numChars] = '\0'; // terminate the string        
        eod = true;
      }
    }
    // dp("Serial: ");
    // for (int p = 0; p < 64; p++) {
    //   dpc(receivedChars[p]);
    // }
    // dp("\n");
}

// Parses serial input to CSV values
void parseCSV(int op) {

  // op: 1 -> Do clear csvValues
  // op: ? -> Don't clear csvValues

  if (op == 1) {
    for (int j = 0; j < maxCSVVals; j++) {
      csvValues[j] = -1;
    }
  }
  

  int t = 0;                          // Counter for number array
  int o = 0;                          // Counter for parsing function. Determines which CSV we're on
  long val = 0;                        // Used in parsing
  char number[7];                     // Array used for parsing CSV inputs

  for (int k = 0; k < maxSerChars; k++) {
      if (receivedChars[k] == ',') {
        number[t] = '\0';
        // dp("Number:");
        // for (int q = 0; q < 7; q++) {
        //   dpc(number[q]);
        // }
        // dp("\n");
        val = atol(number);
        //dp("Val was:");
        //dpi(val);
        //dp("\n");
        csvValues[o] = val;
        t = 0;
        o = o + 1;
      }
      else if (receivedChars[k] == '\0') {
        // Add last CSV to array
        number[t] = '\0';
        // dp("Number:");
        // for (int q = 0; q < 7; q++) {
        //   dpc(number[q]);
        // }
        // dp("\n");
        val = atol(number);    
        csvValues[o] = val;
        // dp("Val was:");
        // dpi(val);
        // dp("\n");
        break;
      }
      else {
        number[t] = receivedChars[k];
        t = t + 1;
      }
    }

    // for (int l = 0; l < 12; l++) {
    //   dp("csvValues: ");
    //   dpi(csvValues[l]);
    //   dp("\n");
    // }
    // dp("\n");

}

// Writes out the current values to the Rheostats and Transistors
void writeAllRheostats() {

  for (int k = 0; k < numRheo; k++) {      
    digitalWrite(SlavePins[k], LOW);                // set chip select low
    SPI.transfer(0);                                // send command byte
    SPI.transfer(OutValues[k]);                     // send resistance value
    digitalWrite(SlavePins[k], HIGH);               // set chip select high

    // dp("Writing: ");
    // dpi(OutValues[k]);
    // dp(", to ");
    // dpi(k);
    // dp(", on ");
    // dpi(SlavePins[k]);
    // dp("\n");
  }

  for (int l = 0; l < numOsc; l++) {
    if (OutValues[l*2] == 0 and OutValues[l*2+1] == 0) {
      digitalWrite(OscTransistorPins[l], LOW);
    }
    else {
      digitalWrite(OscTransistorPins[l], HIGH);
    }
  }

}

// "Resistance to Wipers". Converts a resistance value to two wiper values.
void rtows(long res) {
  // w1 -> 100k
  // w2 -> 5k
  // Stores values into memi for reading back
  // memi[0:w1,
  //      1:w2,
  //      2:w3,
  //      3->15:unused]
  float w1 = 0;                         // Wiper 1 Val           
  float w2 = 0;                         // Wiper 2 Val
  double w3 = 0;                         // Total resistance of rheo

  // dp("rtows recieved:");
  // dpi(res);
  // dp("\n");

  // Input resistance bounds checking
  if (res > maxResistance) { res = maxResistance; } else if (res < 0) { res = 0; }

  w1 = floor( res / onehundredkdiv );                           // Get largest portion with 100k
  w2 = floor( ( res - ( w1 * onehundredkdiv ) ) / fivekdiv);    // Get remainder with 5k
  // dp("w1:");
  // dpi(w1);
  // dp("w2:");
  // dpi(w2);

  
  // w1 bounds check. Probably redundant, but resistant in face of other variable changes.
  if (w1 > wiperMax) { w1 = wiperMax; } else if (w1 < 0) { w1 = 0; } 
  // w2 bounds check. Probably redundant, but resistant in face of other variable changes.
  if (w2 > wiperMax) { w2 = wiperMax; } else if (w2 < 0) { w2 = 0; }

  w3 = ( w1 * onehundredkdiv ) + ( w2 * fivekdiv );             // Calc total resistance
  // Check to see if the total resistance would be more accurate with w2++
  if ( (res - w3) > (fivekdiv / 2)) { w2++; }
  
  // Write values for use elsewhere
  memi[0] = w1;
  memi[1] = w2;
  memi[2] = w3;

  // dp("rtows said memi was: ");
  // dpi(memi[0]);
  // dp(" ");
  // dpi(memi[1]);
  // dp(" ");
  // dpi(memi[2]);
  // dp("\n");
}



// Play function. Call when you want to be in play mode. Will exit with mode request
char Play() {

  dp("In playing mode\n");  
  while (true) {                        // Loop until mode change breaks out
    
    int numChars;                       // Number of Characters recieved from Serial comm

	  // Wait until we have serial data. No point of continuing without it
    while ( Serial.available() == 0 ) ;

    readSerial();            // Get serial input 

    // Mode change recieved
    if (receivedChars[0] == 'm') {
      return receivedChars[2];          // Return mode requested
    }
    else if (receivedChars[0] == 'l') { // Switch debugging
      if (receivedChars[2] == '1') {
        debug = 1;
        dp("Debugging mode on");
      }
      else if (receivedChars[2] == '0') {
        debug = 0;
      }
    }
      
    parseCSV(0);                 // Parse recieved Serial into CSV    
    

    // Calculate all rheostat values  
    for (int j = 0; j < numOsc; j++) {
      rtows(csvValues[j]);
      OutValues[j*2] = memi[0];
      OutValues[j*2 + 1] = memi[1];
    }
    
    writeAllRheostats();

  }
}

// Tune function. Call when you want to be in tuning mode. Will exit with mode request.
char Tune() {
  float frequency;                      // Used for saving/reporting frequency
  double sum = 0;                       // Sum of clock counts thus far
  int count = 0;                        // Used to count freqs measured, exits at numFreqSamples
  int numChars;
  dp("In the tune function\n");
  while (true) {                        // Loop until mode change breaks out.    
    
    while ( Serial.available() == 0 ) ; // Wait for serial
    
    readSerial();            // Get serial input
    
    // Received mode change
    if (receivedChars[0] == 'm') {
      return receivedChars[2];          // Return mode requested
    }
    else if (receivedChars[0] == 'l') { // Switch debugging/logging
      if (receivedChars[2] == '1') {
        debug = 1;
        dp("Debugging mode on");
      }
      else if (receivedChars[2] == '0') {
        debug = 0;
      }
    }
    
    parseCSV(0);    

    // Check to see if given valid oscillator
    if (csvValues[0] < 0 or csvValues[0] > (numOsc - 1)) {   // Invalid Osc
      Serial.print("e:0\n");
    }
    else {                                                   // Valid Osc      
      
      for (int j = 0; j < numRheo; j++) {      // Set all oscillators to off
        OutValues[j] = 0;
      }

      // Set single oscillator on
      rtows(csvValues[1]);
      OutValues[csvValues[0]*2] = memi[0];
      OutValues[csvValues[0]*2 + 1] = memi[1];
      writeAllRheostats();

      sum = 0;                            // Reset sum before freq count
      count = 0;                          // Reset count before freq count

      // Measure frequency
      FreqMeasure.begin();    
      while (count < numFreqSample) {
        if (FreqMeasure.available()) {
          // average several reading together
          sum = sum + FreqMeasure.read();
          count++;
          if (count >= numFreqSample) {
            frequency = FreqMeasure.countToFrequency(sum / count);
            if (quickanddirty == 1) { frequency = frequency * 0.835404; }
            dp("final freq: ");
            Serial.print(frequency);
            Serial.print('\n');          
          }
        }    
      }
      FreqMeasure.end();
    }

    
  }
}

// Sequencer function. Set's up oscillators as periodic notes, established by input from serial.
void Sequencer() {
  int check;
  int sum;
  int k;
  int count;
  dp("Entered dequencer mode\n");

  while (true) {
    readSerial();
    if (receivedChars[0] == 's') {
      // Settings Mode
      if (receivedChars[2] == '0') {                          
        readSerial();
        parseCSV(0);
        if (csvValues[0] > 200 or csvValues[0] < 20) {
          Serial.print("e:2\n");
        }
        else {
          if (csvValues[1] > 1 or csvValues[1] > 12) {
            Serial.print("e:2\n");
          }
          else {
            BPM = csvValues[0];
            tst = csvValues[1];
            Serial.print("e:0\n");
          }
        }
        BPM = csvValues[0];
      }
      // Enter data for sequencer 1
      else if (receivedChars[2] == '1') {
        readSerial();
        parseCSV(1);

        // Error check all values
        check = 1;
        sum = 0;
        for (int j = 0; j < maxCSVVals; j++) {
          if (csvValues[j] == -1) { // We've reached the end
            if ((j % 2) != 0) {
              Serial.print("e:4\n");
              check = 0;
              break;
            }

            //[151325,4,123132,2,12412,2,-1,-1,-1]
          }
          else {
            if ((j % 2) == 0) {   // Dealing with first element of pair
              if (csvValues[j] > maxResistance) {
                csvValues[j] = maxResistance;
              }
            }
            else {                // Dealing with seciond element of pair
              if (csvValues[j] == 0) {
                Serial.print("e:5\n");
                check = 0;
                break;
              }
              sum = sum + csvValues[j];
            }
          }
        }
        // Check sum of notes
        if ((sum > 64) or ((sum % 4) != 0)) {
          Serial.print("e:3,");
          Serial.print(sum);
          Serial.print("\n");
          check = 0;
        }

        // Checks have passed, go ahead! 
        if (check == 1) {
          k = 0;
          count = 0;
          while (k < maxCSVVals) {
            rtows(csvValues[k]);
            for (int l = 0; l < csvValues[k+1]; l++) {
              seq1vals[count] = memi[0];
              seq1vals[count+1] = memi[1];
              count = count + 2;
            }
            k = k + 2;
          }
          Serial.print("e:0,");
          Serial.print(sum);
          Serial.print("\n");

        }
      }
      else if (receivedChars[2] == '2') {
        
      }
      else if (receivedChars[2] == '9') {
              return;        
      }
      else {
        Serial.print("e:4");
      }

    }
    else {
      Serial.print("e:3");      
    }
    
  }

}

void startSequencers() {
  
}

void seqCallback() {

  
}

// Arduino Setup Loop
void setup() {
  for (int i = 0; i < numRheo; i++) {         // loop to set all chip select pins as ouputs  
    pinMode(SlavePins[i], OUTPUT);
  }
  for (int j = 0; j < numOsc; j++) {
    pinMode(OscTransistorPins[j], OUTPUT);
  }

  pinMode(40, OUTPUT);



  SPI.begin();                          // begin SPI protocol
  SPI.setBitOrder(MSBFIRST);            // set SPI bit order
  Serial.begin(baudRate);               // begin Serial communication to PC

  for (int l = 0; l < numOsc; l++) {
        digitalWrite(OscTransistorPins[l], LOW);
  }
}

// Main loop
void loop() {
  if (mode == 0) {
    Serial.print("m:0\n");
    m = Tune();
  } 
  else if (mode == 1) {
    Serial.print("m:1\n");
    m = Play();
  }
  else if (mode = 2) {
    Serial.print("m:2\n");
    Sequencer();
    m = '1';
  }
  
  
  if (m == '0') {
    mode = 0;
  }
  else if (m == '1') {
    mode = 1;
  }
  else if (m == '2' ) {
    mode == 2;
  }
  else {
    Serial.print("e:2\n");
  }  
}
