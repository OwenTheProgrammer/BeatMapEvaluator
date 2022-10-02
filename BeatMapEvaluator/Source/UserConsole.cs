using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatMapEvaluator
{
    internal class UserConsole {
        private readonly static string logStampFormat = @"hh:mm:ss";
        private static string[] consoleBuffer = new string[4];

        public delegate void updateStringGUI(string ctx);
        public static updateStringGUI? onConsoleUpdate = null;

        public static void Log(string message) {
            //Log to file with time stamp
            string timeStamp = DateTime.Now.ToString(logStampFormat);
            System.Diagnostics.Debug.WriteLine($"[{timeStamp}|LOG]: {message}");

            //Shift all logs in buffer up and add incoming
            for(int i = 0; i < consoleBuffer.Length-1; i++)
                consoleBuffer[i] = consoleBuffer[i+1];
            consoleBuffer[consoleBuffer.Length-1] = message;

            //Build the log string from array
            string logBuffer = "Console log:\n";
            foreach(string line in consoleBuffer)
                logBuffer += line + '\n';
            
            //Call GUI update
            if(onConsoleUpdate != null)
                onConsoleUpdate.Invoke(logBuffer);
        }
    }
}
