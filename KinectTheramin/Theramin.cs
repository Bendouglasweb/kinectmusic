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
    public class Theramin
    {
        /// <summary>
        /// Base frequency. Middle C, or A4
        /// </summary>
        private const uint f0 = 440;

        /// <summary>
        /// Constant that will be raised to the nth power,
        /// where n is the number of half steps away from the base frequency
        /// </summary>
        private const double alpha = 1.059463094359;

        /// <summary>
        /// Number of meters between each half step
        /// </summary>
        //private const float halfStepLength = 0.03f;

        internal const ushort noteColumns = 7;
        internal const ushort noteRows = 4;
        internal const ushort cmdColumns = 3;
        internal const ushort cmdRows = 3;

        //internal const double cmdPlaySpacing = 0.2; //space between command and play regions
        //internal const double maxHorizontalDistance = 0.8;
        //internal const double maxVerticalDistance = 0.4;
        //internal const double verticalOffset = 0.0; // distance between absolute vertical position 0 to the bottom of command/play regions

        internal const double YTop = 0.4;
        internal const double YBot = 0.0;
        internal const double halfXWidth = 0.7;
        internal const double halfXGap = 0.05;
        internal const ushort numPlayCols = 7;
        internal const ushort numPlayRows = 4;
        internal const ushort numCmdCols = 3;
        internal const ushort numCmdRows = 3;

        internal const double playRowHeight = (YTop - YBot) / numPlayRows;
        internal const double playColWidth = ((halfXWidth) - (halfXGap)) / numPlayCols;
        internal const double cmdRowHeight = (YTop - YBot) / numCmdRows;
        internal const double cmdColWidth = ((halfXWidth) - (halfXGap)) / numCmdCols;

        private ushort currentlySelectedScaleDBKey = 0;
        private ushort CurrentlySelectedScaleDBKey
        {
            get { return currentlySelectedScaleDBKey; }
            set
            {
                if (value != currentlySelectedScaleDBKey)
                {
                    currentlySelectedScaleDBKey = value;
                    updateNotes();
                }
            }
        }
        private Note[] currentScaleOctaveNotes = new Note[7*Global.Octaves.Length];

        private NoteSelector playHandTracker;
        private NoteSelector cmdHandTracker;

        private readonly ContextEventWrapper<FrequencyUpdateEventArgs> noteUpdateEventWrapper =
            new ContextEventWrapper<FrequencyUpdateEventArgs>(ContextSynchronizationMethod.Post);

        public event EventHandler<FrequencyUpdateEventArgs> NoteUpdate
        {
            // ContextEventWrapper<> is already thread safe so no locking
            add { this.noteUpdateEventWrapper.AddHandler(value); }

            remove { this.noteUpdateEventWrapper.RemoveHandler(value); }
        }

        private readonly ContextEventWrapper<KeyUpdateEventArgs> keyUpdateEventWrapper =
            new ContextEventWrapper<KeyUpdateEventArgs>(ContextSynchronizationMethod.Post);

        internal event EventHandler<KeyUpdateEventArgs> KeyUpdate
        {
            // ContextEventWrapper<> is already thread safe so no locking
            add { this.keyUpdateEventWrapper.AddHandler(value); }

            remove { this.keyUpdateEventWrapper.RemoveHandler(value); }
        }

        public Theramin()
        {
            playHandTracker = new NoteSelector(6, 0.5, 0.5);
            cmdHandTracker = new NoteSelector(6, 0.5, 0.5);
            updateNotes();
        }

        internal async void sm_handPositionUpdate(object sender, HandPositionUpdateEventArgs e)
        {
            if (e.PlayHandFound || e.CommandHandFound)
            {
                await Task.Run(() =>
                {
                    FrequencyUpdateEventArgs fuea = new FrequencyUpdateEventArgs();
                    if (e.PlayHandFound)
                    {
                        double x = 0;
                        double y = 0;
                        if (Global.FancyHandPosition)
                        {
                            double[] result = playHandTracker.pushNote(e.PlayHandX, e.PlayHandY);
                            x = result[0];
                            y = result[1];
                        }
                        else
                        {
                            x = e.PlayHandX;
                            y = e.PlayHandY;
                        }
                        fuea.Notes = ConvertPlayHandPosToNote(x, y);
                    }
                    if (e.CommandHandFound)
                    {
                        double x = 0;
                        double y = 0;
                        if (Global.FancyHandPosition)
                        {
                            double[] result = cmdHandTracker.pushNote(e.CommandHandX, e.CommandHandY);
                            x = result[0];
                            y = result[1];
                        }
                        else
                        {
                            x = e.CommandHandX;
                            y = e.CommandHandY;
                        }
                        fuea.Command = ConvertCommandHandPosToCommand(x, y, e.CommandHandZ);
                    }
                    this.noteUpdateEventWrapper.Invoke(this, fuea);
                });
            }
        }

        //private double ConvertPosToFrequency(double x_pos)
        //{
        //    int halfSteps = Convert.ToInt32(x_pos / halfStepLength) + 20;

        //    string[] notes = File.ReadAllLines("Note_table");

        //    if (halfSteps > notes.Length - 1)
        //    {
        //        return 0;
        //    }
        //    else if (halfSteps < 0)
        //    {
        //        return Convert.ToDouble(notes[0]);
        //    }
        //    else
        //    {
        //        return Convert.ToDouble(notes[halfSteps]);
        //    }
        //    //return (uint)(f0 * Math.Pow(alpha, (double)halfSteps));
        //}

        private Note[] ConvertPlayHandPosToNote(double playXPos, double playYPos)
        {
            int regionIndex = -1;
            Note[] result = new Note[3] { 
                new Note() { frequency = 0, name = null },
                new Note() { frequency = 0, name = null },
                new Note() { frequency = 0, name = null }};

            int column = (int)Math.Floor(((playXPos * (Global.DominantHand == DominantHandMode.Right ? -1 : 1)) - halfXGap ) / playColWidth);
            if (column >= 0 && column < numPlayCols)
            {
                int row = (int)Math.Floor((playYPos - YBot) / playRowHeight );
                regionIndex = column + numPlayCols*row;

                if (regionIndex >= 0 && regionIndex < (numPlayCols*2))
                {
                    result[0] = currentScaleOctaveNotes[regionIndex];
                    // Regions 0-13 are note (not chord) regions, so just leave second and third notes at 0
                }
                else if (regionIndex >= (numPlayCols*2) && regionIndex < (numPlayRows*numPlayCols))
                {
                    result[0] = currentScaleOctaveNotes[regionIndex - 14];
                    result[1] = currentScaleOctaveNotes[regionIndex - 12];
                    result[2] = currentScaleOctaveNotes[regionIndex - 10];
                }
            }
            return result;
        }

        private ArduinoCommand ConvertCommandHandPosToCommand(double cmdXPos, double cmdYPos, double cmdZPos)
        {
            //TODO: handle wave type changes from cmdZPos
            int regionIndex = -1;
            // Only multiplying by 5 to effectively double region width
            int column = (int)Math.Floor( ((cmdXPos * (Global.DominantHand == DominantHandMode.Right ? 1 : -1)) - halfXGap) / cmdColWidth );
            if (column >= 0 && column < numCmdCols)
            {
                int row = (int)Math.Floor( (cmdYPos - YBot) / cmdRowHeight );
                regionIndex = column + numCmdCols*row;
                switch(regionIndex)
                {
                    case 0:
                        CurrentlySelectedScaleDBKey = 0;
                        return ArduinoCommand.None;
                    case 1:
                        CurrentlySelectedScaleDBKey = 1;
                        return ArduinoCommand.None;
                    case 2:
                        CurrentlySelectedScaleDBKey = 11;
                        return ArduinoCommand.None;
                    case 3:
                        CurrentlySelectedScaleDBKey = 2;
                        return ArduinoCommand.None;
                    case 4:
                        CurrentlySelectedScaleDBKey = 3;
                        return ArduinoCommand.None;
                    case 5:
                        CurrentlySelectedScaleDBKey = 7;
                        return ArduinoCommand.None;
                    case 6: return ArduinoCommand.Sequence8;
                    case 7: return ArduinoCommand.Sequence16;
                    case 8: return ArduinoCommand.SequenceStop;
                    default: return ArduinoCommand.None;
                }
            }
            else
            {
                return ArduinoCommand.None;
            }
        }

        private void updateNotes()
        {
            List<Note> frequencies = DB.GetOctaveScaleNotes(CurrentlySelectedScaleDBKey, Global.Octaves);
            this.currentScaleOctaveNotes = frequencies.ToArray();

            KeyUpdateEventArgs kuea = new KeyUpdateEventArgs() { keyDBKey = this.currentlySelectedScaleDBKey };
            this.keyUpdateEventWrapper.Invoke(this, kuea);
        }
    }
}
