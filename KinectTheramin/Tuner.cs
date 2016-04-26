using KinectTheramin.Database;
using Microsoft.Kinect.Toolkit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectTheramin
{
    internal class Tuner
    {
        //internal static readonly string LookupTableFilePath = "DefaultTunings";
        private readonly ushort SPOTCHECK_SAMPLE_INTERVAL = 5;
        private readonly double FREQ_MARGIN_PERCENT = 0.001; //percentage of desired frequency
        private readonly uint   MAX_TUNE_ATTEMPTS = 100;
        private readonly double RESISTANCE_MARGIN_PERCENT = 0.005;
        private readonly double RESISTANCE_TUNEMATCHING_AGGRESSION = 0.33;

        internal static bool SendingSequenceNotes = false;

        private ArduinoManager ardoMgr;
        private readonly ContextEventWrapper<ResistanceUpdateEventArgs> resistanceUpdateEventWrapper
            = new ContextEventWrapper<ResistanceUpdateEventArgs>();

        internal event EventHandler<ResistanceUpdateEventArgs> ResistanceUpdate
        {
            add { this.resistanceUpdateEventWrapper.AddHandler(value); }
            remove { this.resistanceUpdateEventWrapper.RemoveHandler(value); }
        }

        internal bool Tuned { get; set; }

        internal Tuner()
        {
        }

        internal void AttachArduinoManager(ArduinoManager am)
        {
            this.ardoMgr = am;
            this.ardoMgr.ArduinoUpdate += ardoMgr_ArduinoUpdate;
        }

        private void ardoMgr_ArduinoUpdate(object sender, ArduinoUpdateEventArgs e)
        {
            switch (e.message)
            {
                case ArduinoMessage.SequenceDone:
                    Tuner.SendingSequenceNotes = false;
                    break;
                default: break;
            }
        }

        /// <returns>Whether or not the we're currently tuned satisfactorily</returns>
        internal bool SpotCheckTuning()
        {
            if (ardoMgr.ArduinoAvailable)
            {
                ardoMgr.Mode = SynthMode.TUNING;

                bool tuningIsGood = true;
                List<double> frequencies = DB.GetAllNoteFrequencies(Global.Octaves);
                if (frequencies != null)
                {
                    for (int i = 0; tuningIsGood && i < frequencies.Count; i = i + SPOTCHECK_SAMPLE_INTERVAL)
                    {
                        for (ushort o = 0; tuningIsGood && o < Global.NUM_OSCILLATOR_SETS * Global.OSCILLATOR_SET_SIZE; o++)
                        {
                            double freq = frequencies[i];
                            uint storedRes = DB.GetResistanceFromOscFreq(o, freq);
                            if (storedRes != 0)
                            {
                                uint actualRes = tune(o, freq, true);
                                if (Math.Abs(storedRes - actualRes) > RESISTANCE_MARGIN_PERCENT * storedRes)
                                {
                                    tuningIsGood = false;
                                }
                            }
                            else //note not found, gotta do a tune
                            {
                                tuningIsGood = false;
                            }
                        }
                    }
                }
                else
                {
                    tuningIsGood = false;
                }
                //don't change tuning, just check results. If we're not well-tuned, prompt user if we want to do a full tune or not.
                ardoMgr.Mode = SynthMode.NORMAL;
                Tuned = tuningIsGood; //If our tuning is not good, then set Tuned to false
                return tuningIsGood;
            }
            else
            {
                Log.Write("Tuner", "Arduino not available, cannot spot check tuning.");
                return false;
            }
        }

        /// <returns>Whether or not the tuning successfully completed</returns>
        internal bool FullTune(IProgress<TuneProgressUpdateEventArgs> progress)
        {
            bool successfullyCompleted = false;
            if (ardoMgr.ArduinoAvailable)
            {
                ardoMgr.Mode = SynthMode.TUNING;

                try
                {
                    //foreach null value in db
                    List<double> frequencies = DB.GetAllNoteFrequencies(Global.Octaves);
                    if (frequencies != null)
                    {
                        foreach (double freq in frequencies)
                        {
                            for (ushort osc = 0; osc < Global.NUM_OSCILLATOR_SETS * Global.OSCILLATOR_SET_SIZE; osc++)
                            {
                                uint resistance = tune(osc, freq, false);
                                if (resistance != 0)
                                {
                                    //store reply in tuning database
                                    DB.UpdateTuning(osc, freq, resistance);
                                }
                                else
                                {
                                    Log.Write("Tuner", "Tuner returned 0 resistance for note " + freq.ToString());
                                }
                            }
                            TuneProgressUpdateEventArgs tpuea = new TuneProgressUpdateEventArgs()
                            {
                                valuesDone = frequencies.IndexOf(freq) + 1,
                                totalValues = frequencies.Count,
                                successfulSoFar = true
                            };
                            progress.Report(tpuea);
                        }
                        Tuned = true;
                        successfullyCompleted = true;
                    }
                    else
                    {
                        Tuned = false;
                        successfullyCompleted = false;
                    }
                }
                catch (Exception e)
                {
                    Log.WriteException("Tuner", e);
                    TuneProgressUpdateEventArgs tpuea = new TuneProgressUpdateEventArgs()
                    {
                        valuesDone = 1,
                        totalValues = 1,
                        successfulSoFar = false
                    };
                    progress.Report(tpuea);
                    successfullyCompleted = false;
                }
                ardoMgr.Mode = SynthMode.NORMAL;
            }
            if (!successfullyCompleted)
            {
                TuneProgressUpdateEventArgs tpuea = new TuneProgressUpdateEventArgs()
                {
                    valuesDone = 1,
                    totalValues = 1,
                    successfulSoFar = false
                };
                progress.Report(tpuea);
            }
            return successfullyCompleted;
        }

        internal async void th_FrequencyUpdate(object sender, FrequencyUpdateEventArgs e)
        {
            await Task.Run(() =>
            {
                ResistanceUpdateEventArgs ruea = new ResistanceUpdateEventArgs();
                for (ushort i = 0; i < Global.OSCILLATOR_SET_SIZE; i++)
                {
                    ushort osc = SendingSequenceNotes ? (ushort)(i + Global.OSCILLATOR_SET_SIZE) : i;
                    uint resistance = 0;
                    if (e.Notes[i].frequency != 0)
                    {
                        resistance = DB.GetResistanceFromOscFreq(osc, e.Notes[i].frequency);
                        if (resistance == 0)
                        {
                            Log.Write("Tuner", "Unable to find resistance for desired frequency" + e.Notes[i].frequency.ToString());
                        }
                    }
                    ruea.Resistances[i] = resistance;
                }
                ruea.Command = InterpretCommand(e.Command);
                this.resistanceUpdateEventWrapper.Invoke(this, ruea);
            });
        }

        private string InterpretCommand(ArduinoCommand arduinoCommand)
        {
            switch(arduinoCommand)
            {
                case ArduinoCommand.None: 
                    return null;
                case ArduinoCommand.Sequence8:
                    if (!SendingSequenceNotes)
                    {
                        SendingSequenceNotes = true;
                        return "s:1";
                    }
                    else
                        return null;
                case ArduinoCommand.Sequence16:
                    if (!SendingSequenceNotes)
                    {
                        SendingSequenceNotes = true;
                        return "s:2";
                    }
                    else
                        return null;
                case ArduinoCommand.SequenceStop:
                    if (SendingSequenceNotes)
                    {
                        //Interrupts recording
                        SendingSequenceNotes = false;
                    }
                    return "s:s";
                default: 
                    return null;
            }
        }

        /// <summary>
        /// Finds the resistance needed to achieve a given frequency at given oscillator
        /// </summary>
        /// <param name="osc">Oscillator number</param>
        /// <param name="freq">Desired frequency</param>
        /// <returns>The resistance required to achieve given frequency</returns>
        private uint tune(ushort osc, double freq, bool spotCheck)
        {
            if (ardoMgr.Mode == SynthMode.TUNING)
            {
                bool match = false;
                uint resistance = DB.GetResistanceFromOscFreq(osc,freq);
                if (resistance == 0)
                {
                    resistance = (uint)(1 / ((2.772e-7) * freq)); //backup in case not found in db
                }
                uint attempts = 9;
                do
                {
                    string reply = ardoMgr.Send(true, String.Format("{0},{1}", osc, resistance));
                    //TODO: Handle error codes, frequencies of 0 (frequency too high got filtered out), and maybe garbage characters
                    double diff = Convert.ToDouble(reply) - freq;
                    if (Math.Abs(diff) < FREQ_MARGIN_PERCENT * freq)
                    {
                        match = true;
                    }
                    else
                    {
                        resistance = (uint)(resistance + (diff / freq) * RESISTANCE_TUNEMATCHING_AGGRESSION * resistance);
                    }
                    attempts++;
                } while (!match && !spotCheck && attempts < MAX_TUNE_ATTEMPTS);
                return resistance;
            }
            else
            {
                Log.Write("Tuner","Error tuning: Arduino not in tuning mode.");
                return 0;
            }
        }
    }
}
