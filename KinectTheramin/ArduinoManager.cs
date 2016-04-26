using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Windows;
using System.IO;
using Microsoft.Kinect.Toolkit;

namespace KinectTheramin
{
    public class ArduinoManager
    {
        private readonly string SERIAL_PORT_NAME = "COM7";
        private readonly int SERIAL_BAUD_RATE = 9600;

        private readonly string ArduinoCmdSeqDone = "s:d";

        private SerialPort ArduinoPort;

        private SynthMode currentMode = SynthMode.TUNING;

        private readonly ContextEventWrapper<ArduinoUpdateEventArgs> arduinoUpdateEventWrapper
            = new ContextEventWrapper<ArduinoUpdateEventArgs>();

        internal event EventHandler<ArduinoUpdateEventArgs> ArduinoUpdate
        {
            add { this.arduinoUpdateEventWrapper.AddHandler(value); }
            remove { this.arduinoUpdateEventWrapper.RemoveHandler(value); }
        }

        internal bool ArduinoAvailable
        {
            get { return ArduinoPort.IsOpen; }
        }

        public SynthMode Mode
        {
            get
            {
                return currentMode;
            }
            set
            {
                if (value != currentMode)
                {
                    string modeChangeRequest = String.Format("m:{0}", Convert.ToInt32(value));
                    string reply = this.Send(true, modeChangeRequest);
                    if (String.Compare(reply, modeChangeRequest) != 0)
                    {
                        MessageBox.Show(
                            String.Format("Communication Error while changing mode. Arduino replied: {0} Expected: {1}", reply, modeChangeRequest),
                            "Serial Communication Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                    else
                    {
                        currentMode = value;
                    }
                }
            }
        }

        public ArduinoManager()
        {
            ArduinoPort = new SerialPort(SERIAL_PORT_NAME, SERIAL_BAUD_RATE);
            try
            {
                ArduinoPort.Open();
                //ArduinoPort.ReadTimeout = 3000;
                ArduinoPort.DataReceived += ArduinoPort_DataReceived;
            }
            catch(IOException ioe)
            {
                Log.WriteException("ArduinoManager", ioe);
                MessageBox.Show(
                    "Unable to open port at "+SERIAL_PORT_NAME,
                    "Serial Communication Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            Mode = SynthMode.TUNING;
            /*if (!t.SpotCheckTuning())
            {
                MessageBox.Show(
                    "Unable to acquire a satisfactory tuning. Arduino may be disconnected.",
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }*/
        }

        void ArduinoPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (Tuner.SendingSequenceNotes && Mode == SynthMode.NORMAL)
            {
                try
                {
                    string message = ArduinoPort.ReadLine();
                    if (message == ArduinoCmdSeqDone)
                    {
                        ArduinoUpdateEventArgs auea = new ArduinoUpdateEventArgs() { message = ArduinoMessage.SequenceDone };
                        this.arduinoUpdateEventWrapper.Invoke(this, auea);
                    }
                    // Handle error codes here
                }
                catch (Exception ex)
                {
                    Log.WriteException("ArduinoManager", ex);
                }
            }
        }

        internal string Send(bool requestReply, string message)
        {
            if (ArduinoPort.IsOpen)
            {
                try
                {
                    ArduinoPort.WriteLine(message);
                    Console.WriteLine(message);
                    if (requestReply)
                    {
                        string reply = ArduinoPort.ReadLine();
                        Console.WriteLine("AR: " + reply);
                        return reply;
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (TimeoutException te)
                {
                    Log.WriteException("ArduinoManager", te);
                    return null;
                }
                catch (Exception e)
                {
                    Log.WriteException("ArduinoManager", e);
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        internal void t_ResistanceUpdate(object sender, ResistanceUpdateEventArgs e)
        {
            if (Mode == SynthMode.NORMAL)
            {
                if (!String.IsNullOrWhiteSpace(e.Command))
                {
                    this.Send(false, e.Command);
                }

                this.Send(false, String.Format("{0},{1},{2}",
                    e.Resistances[0],
                    e.Resistances[1],
                    e.Resistances[2]
                    ));
            }
        }

        private readonly char errorCodeSymbol = 'e';

        private enum ErrorCode
        {
            NoError = 0,
            TimeOut = 1
        }

        private ErrorCode ValidateResponse(string arduinoResponse)
        {
            if (arduinoResponse[0] == errorCodeSymbol)
            {
                return ErrorCode.TimeOut;
            }
            else
            {
                return ErrorCode.NoError;
            }
        }

        ~ArduinoManager()
        {
            ArduinoPort.Close();
        }
    }
}
