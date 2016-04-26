using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectTheramin
{
    public class HandPositionUpdateEventArgs : EventArgs
    {
        public float PlayHandX { get; set; }
        public float PlayHandY { get; set; }
        public float PlayHandZ { get; set; }
        public bool PlayHandFound { get; set; }

        public float CommandHandX { get; set; }
        public float CommandHandY { get; set; }
        public float CommandHandZ { get; set; }
        public bool CommandHandFound { get; set; }
    }
}
