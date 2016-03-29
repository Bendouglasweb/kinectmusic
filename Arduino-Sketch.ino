
#include <SPI.h>
#include <FreqMeasure.h>

// TODO
// - Change dp function to char array, for heap fragmentation stuff

// chip select pins in order of oscillators, 100k, 5k, timer 1, 100k, 5k timer 2 etc.
const int SlavePins [12] = {23, 25, 27, 29, 31, 33, 35, 37, 39, 41, 43, 45};                 
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

// *** Settings -- Physical
const float onehundredkdiv  = 100000 / 256;		// Step size of 100k Rheostat
const float fivekdiv = 5000 / 256;				    // Step size of 5k Rheostat
const int wiperMax = 255;                     // Max SPI input to Rheostats
const int wiperMin = 0;                       // Min SPI input to Rheostats
const int maxResistance = 105000;             // Max possible resistance
const int numOsc = 6;                         // Number of oscillators controlled by the Arduino
const int numRheo = numOsc * 2;               // Number of rheostats controlled by the Arduino
const int baudRate = 9600;                    // Serial baud rate

// *** Settings -- Software
int mode = 0;								            // Mode (e.g., play/tuning) variable, and initial mode
int numFreqSample = 75;						      // Number of frequency samples to take

int debug = 0;    							        // Serial debugging. 1 = on
int quickanddirty = 1;						      // Temp frequency fix

// *** Variables used in Serial Comm
char endMarker = '\n';                  // What symbol is considered the end of a request
const byte maxSerChars = 2305;            // Max num of characters we can process in a single Serial request
char receivedChars[maxSerChars];        // Array to store received data

int ResValues [6] = {0, 0, 0, 0, 0, 0}; // Used as storage for CSV parser
  //  { Oscillator 0 Resistance, Oscillator 1 Resistance .. Oscillator 5 Resistance  }

int OutValues[12] = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0};  // Output wiper values. 
  // { 100k Rheo 1 Val, 5k Rheo 1 Val, 100k Rheo 2 Val, 5k Rheo 2 Val, .. 100k Rheo 5 Val, 5k Rheo 5 Val, }

// Other
int memi[16];                           // Used as reserved memory to transfer data between things. "Memory, integers"
char m = '\0';                          // Used in main loop



// Debugging functions. Handles Serial output of debugging information
void dp(char* input) {
  if (debug == 1) {
    Serial.print(input);
  }
}

// Integer debug print
void dpi(int input) {
  if (debug == 1) {
    Serial.print(input);
  }
}

// Function reads in next available serial command
int readSerial() {
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
  return numChars;
}

// Parses serial input to CSV values
void parseCSV(int numChars) {

  int t = 0;                          // Counter for number array
  int o = 0;                          // Counter for parsing function. Determines which CSV we're on
  int val = 0;                        // Used in parsing
  char number[7];                     // Array used for parsing CSV inputs

  for (int k = 0; k < numChars; k++) {
      if (receivedChars[k] == ',') {
        number[t] = '\0';
        val = atoi(number);
        dp("Val was:");
        dpi(val);     

        ResValues[o] = val;
        t = 0;
        o = o + 1;
      }
      else {
        number[t] = receivedChars[k];
        t = t + 1;
      }
    }

    // Add last CSV to array
    number[t] = '\0';
    val = atoi(number);    
    ResValues[o] = val;
    dp("Val was:");
    dpi(val);

}

// Writes out the current values to the Rheostats
void writeAllRheostats() {
  for (int k = 0; k < numRheo; k++) {      
    digitalWrite(SlavePins[k], LOW);                // set chip select low
    SPI.transfer(0);                                // send command byte
    SPI.transfer(OutValues[k]);                     // send resistance value
    digitalWrite(SlavePins[k], HIGH);               // set chip select high

    dp("Writing: ");
    dpi(OutValues[k]);
    dp(", to ");
    dpi(k);
  }
}

// "Resistance to Wipers". Converts a resistance value to two wiper values.
void rtows(int res) {
  // w1 -> 100k
  // w2 -> 5k
  // Stores values into memi for reading back
  // memi[0:w1,
  //      1:w2,
  //      2:w3,
  //      3->15:unused]
  float w1 = 0;                         // Wiper 1 Val           
  float w2 = 0;                         // Wiper 2 Val
  float w3 = 0;                         // Total resistance of rheo

  // Input resistance bounds checking
  if (res > maxResistance) { res = maxResistance; } else if (res < 0) { res = 0; }

  w1 = floor( res / onehundredkdiv );                           // Get largest portion with 100k
  w2 = floor( ( res - ( w1 * onehundredkdiv ) ) / fivekdiv);    // Get remainder with 5k
  
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
}

// Arduino Setup Loop
void setup() {
  for (int i = 0; i < 8; i++) {				  // loop to set all chip select pins as ouputs  
    pinMode(SlavePins[i], OUTPUT);
  }
  SPI.begin();								          // begin SPI protocol
  SPI.setBitOrder(MSBFIRST);				    // set SPI bit order
  Serial.begin(baudRate);						    // begin Serial communication to PC
}

// Play function. Call when you want to be in play mode. Will exit with mode request
char Play() {
  dp("In playing mode\n");  
  while (true) {                        // Loop until mode change breaks out
    
    int numChars;                       // Number of Characters recieved from Serial comm

	  // Wait until we have serial data. No point of continuing without it
    while ( Serial.available() == 0 ) ;

    numChars = readSerial();            // Get serial input 

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
      
    parseCSV(numChars);                 // Parse recieved Serial into CSV    
    

    // Calculate all rheostat values  
    for (int j = 0; j < numOsc; j++) {
      rtows(ResValues[j]);
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
    
    numChars = readSerial();            // Get serial input
    
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
    
    parseCSV(numChars);    

    // Check to see if given valid oscillator
    if (ResValues[0] < 0 or ResValues[0] > (numOsc - 1)) {   // Invalid Osc
      Serial.print("e:0\n");
    }
    else {                                                   // Valid Osc      
      
      for (int j = 0; j < numRheo; j++) {      // Set all oscillators to off
        OutValues[j] = 0;
      }

      // Set single oscillator on
      rtows(ResValues[1]);
      OutValues[ResValues[0]*2] = memi[0];
      OutValues[ResValues[0]*2 + 1] = memi[1];
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
  
  
  if (m == '0') {
    mode = 0;
  }
  else if (m == '1') {
    mode = 1;
  }
  else {
    Serial.print("e:2\n");
  }  
}