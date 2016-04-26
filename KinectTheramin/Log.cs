using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectTheramin
{
    internal static class Log
    {
        private static readonly string  logFilePath = "log.txt";
        private static object           lockObj = new object(); //lock object to prevent collisions writing to log file

        private static string TimeStamp
        {
            get { return DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:fff"); }
        }
        internal static void Initiate()
        {
            //Checks if log file exists. If not, creates it.
            if (!File.Exists(logFilePath))
            {
                File.Create(logFilePath).Dispose();
            }
        }

        internal static void Write(string source, string message)
        {
            string line = String.Format("[{0}] ({1}) {2}", TimeStamp, source, message);
            Console.WriteLine("LOG:" + line);
            lock (lockObj)
            {
                using (StreamWriter logWriter = File.AppendText(logFilePath))
                {
                    logWriter.WriteLine(line);
                }
            }
        }

        internal static void WriteException(string source, Exception e)
        {
            string message = String.Format("{0}-{1}\n\tStack Trace:\n{2}", e.Source, e.Message, e.StackTrace);
            Log.Write(source, message);
            Console.WriteLine(message);
        }
    }
}
