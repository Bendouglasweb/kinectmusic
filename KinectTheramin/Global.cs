using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectTheramin
{
    internal static class Global
    {
        internal static readonly ushort NUM_OSCILLATOR_SETS = 2;
        internal static readonly ushort OSCILLATOR_SET_SIZE = 3;

        internal static readonly ushort[] Octaves = { 4, 5, 6 }; // Extra octave included so chords work. Get a better solution later

        internal static DominantHandMode DominantHand;

        internal static bool FancyHandPosition = false;
    }
}
