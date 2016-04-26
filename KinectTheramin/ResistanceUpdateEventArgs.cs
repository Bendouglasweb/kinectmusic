using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectTheramin
{
    internal class ResistanceUpdateEventArgs : EventArgs
    {
        internal uint[] Resistances = new uint[Global.OSCILLATOR_SET_SIZE];
        internal string Command;
    }
}
