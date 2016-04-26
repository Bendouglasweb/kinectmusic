using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectTheramin
{
    internal class HandPositionLogger
    {
        internal static readonly string HandPosLogFilePath = "handPositionLogFile.txt";
        internal static object lockObj = new object();

        internal HandPositionLogger()
        {
            if (!File.Exists(HandPosLogFilePath))
            {
                File.Create(HandPosLogFilePath);
            }
        }

        internal async void handPosUpdate(object sender, HandPositionUpdateEventArgs e)
        {
            if (e.CommandHandFound || e.PlayHandFound)
            {
                await Task.Run(() =>
                {
                    string lineToWrite = String.Format("{0},{1},{2}", e.PlayHandX, e.PlayHandY, e.PlayHandZ);
                    lock (lockObj)
                    {
                        using (StreamWriter logWriter = File.AppendText(HandPosLogFilePath))
                        {
                            logWriter.WriteLine(lineToWrite);
                        }
                    }
                });
            }
        }
    }
}
