using KinectTheramin.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace KinectTheramin
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SensorManager   sm;
        private Theramin        theramin;
        private Tuner           tnr;
        private ArduinoManager  am;

        private HandPositionLogger hpl;

        private const ushort UIScaler = 400;

        #region DrawingMembers
        /// <summary>
        /// Drawing group for hand position rendering
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = (float) (UIScaler * Theramin.halfXWidth * 2);

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = (float) (UIScaler * (Theramin.YTop-Theramin.YBot));

        /// <summary>
        /// Radius used when drawing hand position circles
        /// </summary>
        private const double baseHandRadius = 8;

        /// <summary>
        /// Brush used for drawing command hand
        /// </summary>
        private readonly Brush commandHandBrush = new SolidColorBrush(Colors.Magenta);

        /// <summary>
        /// Brush used for drawing play hand
        /// </summary>
        private readonly Brush playHandBrush = new SolidColorBrush(Colors.LimeGreen);
        
        /// <summary>
        /// Pen used for drawing the borders of each note/command region
        /// </summary>        
        private readonly Pen regionBorderPen = new Pen(Brushes.Gray, 1);
        #endregion DrawingMembers

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!DB.InitializeDB())
            {
                MessageBox.Show(
                    "Database not successfully initialized",
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                string result = DB.ExecuteScalar("SELECT Resistance FROM TUNINGS WHERE Oscillator=1 AND Frequency=329.63");
                if (result != "7687")
                {
                    MessageBox.Show(
                        "Invalid value returned from the database: " + result,
                        "Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            Image.Source = this.imageSource;

            sm = new SensorManager();
            sm.HandPositionUpdated += sm_handPositionUpdate;

            theramin = new Theramin();
            sm.HandPositionUpdated += theramin.sm_handPositionUpdate;
            theramin.KeyUpdate += theramin_KeyUpdate;
            theramin.NoteUpdate += theramin_NoteUpdate;

            tnr = new Tuner();
            theramin.NoteUpdate += tnr.th_FrequencyUpdate;

            am = new ArduinoManager();
            tnr.ResistanceUpdate += am.t_ResistanceUpdate;
            tnr.AttachArduinoManager(am);

            // Disabling hand position logging for now. Can re-enable if needed.
            //hpl = new HandPositionLogger();
            //sm.HandPositionUpdated += hpl.handPosUpdate;

            if (!tnr.SpotCheckTuning())
            {
                if (MessageBox.Show("Synthesizer is not properly tuned. Run a full tune now?", "Tuning spot check found a problem", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    runFullTune();
                }
            }

            sm.Initialize();
        }

        void theramin_KeyUpdate(object sender, KeyUpdateEventArgs e)
        {
            this.keyNameTb.Text = DB.ExecuteScalar("SELECT Name FROM SCALES WHERE ID = " + e.keyDBKey.ToString());
        }

        private async void runFullTune()
        {
            fullTuneBtn.IsEnabled = false;
            playModeBtn.IsEnabled = false;
            tuningPrgbar.Foreground = new SolidColorBrush(Colors.Green);

            IProgress<TuneProgressUpdateEventArgs> progress = new Progress<TuneProgressUpdateEventArgs>(this.tnr_TuneProgressUpdate);
            if (!await Task.Run(() => tnr.FullTune(progress))) //If tuning was unsuccessful
            {
                MessageBox.Show(
                    "Unable to acquire a satisfactory tuning. Arduino may be disconnected.",
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            playModeBtn.IsEnabled = true;
            fullTuneBtn.IsEnabled = true;
        }

        private void theramin_NoteUpdate(object sender, FrequencyUpdateEventArgs e)
        {
            this.NoteTb.Text = e.Notes[0].name; //Fancy version: String.Format("{0}{1}",e.Notes[0].name,String.IsNullOrEmpty(e.Notes[1].name) ? null : "(3)");
        }

        private void tnr_ResistanceUpdate(object sender, ResistanceUpdateEventArgs e)
        {
        }

        //TODO: Optimize so that we only redraw new stuff (hand position) on top of existing image of black bg, command/play regions and labels
        private void sm_handPositionUpdate(object sender, HandPositionUpdateEventArgs e)
        {
            int playSide = Global.DominantHand == DominantHandMode.Right ? -1 : 1;
            int cmdSide = -playSide;
            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                for (int row = 1; row < Theramin.numPlayRows; row++)
                {
                    dc.DrawLine(regionBorderPen, ConvertPositionToPoint(Theramin.halfXWidth*playSide, Theramin.playRowHeight*row), ConvertPositionToPoint(Theramin.halfXGap*playSide, Theramin.playRowHeight*row));
                }

                for (int col = 1; col <= Theramin.numPlayCols; col++)
                {
                    dc.DrawLine(regionBorderPen, ConvertPositionToPoint(Theramin.halfXWidth*playSide - (Theramin.playColWidth*col)*playSide, Theramin.YTop - Theramin.YBot), ConvertPositionToPoint(Theramin.halfXWidth*playSide - (Theramin.playColWidth * col)*playSide, 0));
                }

                for (int crow = 1; crow < Theramin.numCmdRows; crow++)
                {
                    dc.DrawLine(regionBorderPen, ConvertPositionToPoint(Theramin.halfXWidth * cmdSide, Theramin.cmdRowHeight * crow), ConvertPositionToPoint(Theramin.halfXGap * cmdSide, Theramin.cmdRowHeight * crow));
                }

                for (int ccol = 1; ccol <= Theramin.numCmdCols; ccol++)
                {
                    dc.DrawLine(regionBorderPen, ConvertPositionToPoint(Theramin.halfXWidth * cmdSide - (Theramin.cmdColWidth * ccol) * cmdSide, Theramin.YTop - Theramin.YBot), ConvertPositionToPoint(Theramin.halfXWidth * cmdSide - (Theramin.cmdColWidth * ccol) * cmdSide, 0));
                }

                // TODO: Add labels to command regions

                // Draws both hands as circles
                dc.DrawEllipse(playHandBrush, null, ConvertPositionToPoint(e.PlayHandX, e.PlayHandY), baseHandRadius - e.PlayHandZ, baseHandRadius - e.PlayHandZ);
                dc.DrawEllipse(commandHandBrush, null, ConvertPositionToPoint(e.CommandHandX,e.CommandHandY), baseHandRadius-e.CommandHandZ, baseHandRadius-e.CommandHandZ);

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        private Point ConvertPositionToPoint(double x_pos, double y_pos)
        {
            return new Point(((x_pos+Theramin.halfXWidth)/(2*Theramin.halfXWidth))*RenderWidth , ((Theramin.YTop - y_pos)/(Theramin.YTop-Theramin.YBot))*RenderHeight); 
        }

        private int HandPosToArrayIndex(double handPos)
        {
            return (int)Math.Floor(handPos);
        }

        private void fullTuneBtn_Click(object sender, RoutedEventArgs e)
        {
            runFullTune();
        }

        void tnr_TuneProgressUpdate(TuneProgressUpdateEventArgs e)
        {
            if (e.successfulSoFar)
            {
                tuningPrgbar.Maximum = e.totalValues;
                tuningPrgbar.Value = e.valuesDone;
            }
            else
            {
                if (tuningPrgbar.Value == 0)
                {
                    tuningPrgbar.Maximum = 1;
                    tuningPrgbar.Value = 1;
                }
                tuningPrgbar.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        private void playModeBtn_Click(object sender, RoutedEventArgs e)
        {
            //verify we're not in the middle of a tune
            //Ideally we won't need this button. But rather the theramin will default to play mode after a quick spot check tune.
            am.Mode = SynthMode.NORMAL;
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sm != null)
            {
                if ((RadioButton)sender == this.leftHandBtn)
                {
                    Global.DominantHand = DominantHandMode.Left;
                }
                else if ((RadioButton)sender == this.rightHandBtn)
                {
                    Global.DominantHand = DominantHandMode.Right;
                }
            }
        }

        private void seqBtn_Click(object sender, RoutedEventArgs e)
        {
            am.Send(false,"s:1");
        }

        private void testHandPosBtn_Click(object sender, RoutedEventArgs e)
        {
            sm.TestHandPosition();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                uint bpm = Convert.ToUInt32(this.bpmTb.Text);
                am.Send(false, String.Format("b:{0}", bpm));
            }
            catch(FormatException fe)
            {
                Log.Write("MainWindow", "User tried to set invalid BPM: " + this.bpmTb.Text);
            }
            catch(Exception ex)
            {
                Log.WriteException("MainWindow", ex);
            }
        }

        private void fancyHandPosChkBox_Checked(object sender, RoutedEventArgs e)
        {
            if (fancyHandPosChkBox.IsChecked.HasValue)
            {
                Global.FancyHandPosition = fancyHandPosChkBox.IsChecked.Value;
            }
        }

    }
}
