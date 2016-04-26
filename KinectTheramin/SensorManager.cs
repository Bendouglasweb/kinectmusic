using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;

namespace KinectTheramin
{
    internal class SensorManager
    {
        private bool sensorValid = false;
        private KinectSensor sensor;

        private readonly ContextEventWrapper<HandPositionUpdateEventArgs> handPosUpdateContextWrapper =
            new ContextEventWrapper<HandPositionUpdateEventArgs>(ContextSynchronizationMethod.Post);

        private KinectSensor Sensor
        {
            get
            {
                return sensor;
            }
            set
            {
                if (value != null && value.Status == KinectStatus.Connected)
                {
                    this.sensorValid = true;
                }
                else
                {
                    this.sensorValid = false;
                }
                sensor = value;
            }
        }

        private KinectSensorChooser sensorChooser;

        internal SensorManager()
        {
            sensorChooser = new KinectSensorChooser();
            sensorChooser.Start();

            sensorChooser.KinectChanged += sensorChooser_KinectChanged;

            Sensor = sensorChooser.Kinect;

            Initialize();
        }

        /// <summary>
        /// Event triggered when a new hand position update is available
        /// </summary>
        //internal event EventHandler<HandPositionUpdateEventArgs> handPositionUpdate
        //{
        //    // ContextEventWrapper<> is already thread safe so no locking
        //    add { this.handPosUpdateContextWrapper.AddHandler(value); }

        //    remove { this.handPosUpdateContextWrapper.RemoveHandler(value); }
        //}

        internal event EventHandler<HandPositionUpdateEventArgs> HandPositionUpdated;

        protected virtual void OnHandPositionUpdated(HandPositionUpdateEventArgs e)
        {
            var handler = HandPositionUpdated;
            if(handler != null)
            {
                handler(this, e);
            }
        }

        private void sensorChooser_KinectChanged(object sender, KinectChangedEventArgs e)
        {
            this.Sensor = e.NewSensor;
        }

        internal void Initialize()
        {
            if (sensorValid)
            {
                //Instantiating this to be able to adjust later
                TransformSmoothParameters tsparams = new TransformSmoothParameters();
                Sensor.SkeletonStream.Enable(tsparams);

                Sensor.SkeletonFrameReady += sensor_SkeletonFrameReady;
            }
        }

        private void sensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            //Load all skeletons from frame
            Skeleton[] skeletons = new Skeleton[0];
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            //Filter down to only tracked skeletons
            List<Skeleton> trackedSkeletons = new List<Skeleton>();
            foreach (Skeleton sk in skeletons)
            {
                if (sk.TrackingState == SkeletonTrackingState.Tracked)
                {
                    trackedSkeletons.Add(sk);
                }
            }

            HandPositionUpdateEventArgs hp = new HandPositionUpdateEventArgs();
            if (trackedSkeletons.Count == 1)
            {
                Skeleton sk = trackedSkeletons[0];
                Joint commandHand = sk.Joints[Global.DominantHand == DominantHandMode.Right ? JointType.HandRight : JointType.HandLeft];
                Joint playHand = sk.Joints[Global.DominantHand == DominantHandMode.Right ? JointType.HandLeft : JointType.HandRight];

                if (playHand != null && playHand.TrackingState == JointTrackingState.Tracked)
                {
                    hp.PlayHandFound = true;
                    hp.PlayHandX = playHand.Position.X;
                    hp.PlayHandY = playHand.Position.Y;
                    hp.PlayHandZ = playHand.Position.Z;
                }
                if (commandHand != null && commandHand.TrackingState == JointTrackingState.Tracked)
                {
                    hp.CommandHandFound = true;
                    hp.CommandHandX = commandHand.Position.X;
                    hp.CommandHandY = commandHand.Position.Y;
                    hp.CommandHandZ = commandHand.Position.Z;
                }
            }

            OnHandPositionUpdated(hp);
            //handPosUpdateContextWrapper.Invoke(this, hp);
        }

        internal void TestHandPosition()
        {
            HandPositionUpdateEventArgs hp = new HandPositionUpdateEventArgs();

            hp.PlayHandFound = true;
            hp.PlayHandX = -0.35f;
            hp.PlayHandY = 0.25f;
            hp.PlayHandZ = 0f;
            hp.CommandHandFound = true;
            hp.CommandHandX = 0.25f;
            hp.CommandHandY = 0.15f;
            hp.CommandHandZ = 0f;

            OnHandPositionUpdated(hp);
            //handPosUpdateContextWrapper.Invoke(this, hp);
        }
        ~SensorManager()
        {
            sensorChooser.Stop();
        }
    }
}
