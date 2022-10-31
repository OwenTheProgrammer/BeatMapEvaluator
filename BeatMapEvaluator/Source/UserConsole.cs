using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace BeatMapEvaluator
{
    internal class UserConsole {
        private readonly static string logStampFormat = @"hh:mm:ss";
        private static string[] consoleBuffer = new string[7];

        public delegate void updateStringGUI(string ctx);

        public static updateStringGUI? onConsoleUpdate = null;
        public static updateStringGUI? onLogUpdate = null;

        public static void Log(string message) {
            LogHandle(message, "LOG");
            ConsoleHandle(message);
        }

        public static void LogError(string message) {
            LogHandle(message, "ERROR");
            ConsoleHandle(message);
        }

        private static Task ConsoleHandle(string message) { 
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
            return Task.CompletedTask;
        }
        private static Task LogHandle(string message, string hndl) { 
            //Log to file with time stamp
            string timeStamp = DateTime.Now.ToString(logStampFormat);
            string logOut = $"[{timeStamp}|{hndl}]: {message}";
            System.Diagnostics.Debug.WriteLine(logOut);
            //Call LogFile write
            if(onLogUpdate != null)
                onLogUpdate.Invoke(logOut);
            return Task.CompletedTask;
        }
    }
}
