using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectTheramin
{
    public class FrequencyUpdateEventArgs : EventArgs
    {
        internal Note[] Notes = new Note[Global.OSCILLATOR_SET_SIZE];
        internal ArduinoCommand Command;
    }
}
