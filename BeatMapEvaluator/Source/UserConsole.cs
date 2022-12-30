using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BeatMapEvaluator
{
    /// <summary>The user console log class</summary>
    internal class UserConsole {
        //Decoration pipes :)
        const string cPipe = "├─ ";
        const string endPipe = "└─ ";

        private readonly static string logStampFormat = @"hh:mm:ss";
        private static string[] consoleBuffer = new string[7]; //User console buffer

        public delegate void updateStringGUI(string ctx);

        //User console callback
        public static updateStringGUI? onConsoleUpdate = null;
        //Log file update callback
        public static updateStringGUI? onLogUpdate = null;

        /// <summary>
        /// Logs to the log file and the user console.
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void Log(string message) {
            LogHandle(message, "LOG");  //Log to file
            ConsoleHandle(message);     //Log to user console
        }

        /// <summary>
        /// Writes a report file for a given map
        /// </summary>
        /// <param name="layout">The maps evaluation info</param>
        /// <param name="info">The maps json info</param>
        /// <param name="filePath">the output filepath</param>
        public static async Task ExportReport(MapStorageLayout layout, json_MapInfo info, string filePath) {
            //report cached as just "r"
            DiffCriteriaReport r = layout.report;

            //The report file lines buffer
            List<string> buffer = new List<string>();
            //Header formatting
            string _title = "- " + layout.bsr + ": " + info._songName + " -";
            string _diff = "Difficulty: " + layout.mapDiff._difficulty;
            buffer.Add(_title);
            buffer.Add(_diff);

            //await File.WriteAllTextAsync(filePath, _title);
            //await File.AppendAllTextAsync(filePath, _diff);
            
            //If mods required, write as rule-exception
            if (r.modsRequired != null) {
                string _buffer = "Mods: [" + r.modsRequired.Count + "]\n";
                for(int i = 0; i < r.modsRequired.Count; i++) {
                    _buffer += (i==r.modsRequired.Count-1) ? endPipe : cPipe;
                    _buffer += r.modsRequired[i] + "\n";
                }
                buffer.Add(_buffer);
                //await File.AppendAllTextAsync(filePath, _buffer);
            }

            //Write note/wall hot start violations
            if(r.note_HotStarts != null || r.wall_HotStarts != null) {
                int count = 0;
                string _buffer = "";
                if(r.note_HotStarts != null) {
                    count += r.note_HotStarts.Count;
                    for(int i = 0; i < r.note_HotStarts.Count; i++)
                        _buffer += _writeStandardNote(r.note_HotStarts, i);
                }
                if(r.wall_HotStarts != null) { 
                    count += r.wall_HotStarts.Count;
                    for(int i = 0; i < r.wall_HotStarts.Count; i++)
                        _buffer += _writeStandardWall(r.wall_HotStarts, i) + '\n';
                }
                if(count != 0) { 
                    string _b = "HotStarts: [" + count + "]\n" + _buffer;
                    buffer.Add(_b);
                    //await File.AppendAllTextAsync(filePath, _b);
                }
            }
            //Write note/wall cold end violations
            if(r.note_ColdEnds != null || r.wall_ColdEnds != null) {
                int count = 0;
                string _buffer = "";
                if(r.note_ColdEnds != null) {
                    count += r.note_ColdEnds.Count;
                    for(int i = 0; i < r.note_ColdEnds.Count; i++)
                        _buffer += _writeStandardNote(r.note_ColdEnds, i);
                }
                if(r.wall_ColdEnds != null) { 
                    count += r.wall_ColdEnds.Count;
                    for(int i = 0; i < r.wall_ColdEnds.Count; i++)
                        _buffer += _writeStandardWall(r.wall_ColdEnds, i) + '\n';
                }
                if(count != 0) { 
                    string _b = "ColdEnds: [" + count + "]\n" + _buffer;
                    buffer.Add(_b);
                    //await File.AppendAllTextAsync(filePath, _b);
                }
            }
            //Write note/wall.. you get it.
            if(r.note_Intersections != null || r.wall_Intersections != null) { 
                int count = 0;
                string _buffer = "";
                if(r.note_Intersections != null) {
                    count += r.note_Intersections.Count;
                    for(int i = 0; i < r.note_Intersections.Count; i++)
                        _buffer += _writeStandardNote(r.note_Intersections, i);
                }
                if(r.wall_Intersections != null) { 
                    count += r.wall_Intersections.Count;
                    for(int i = 0; i < r.wall_Intersections.Count; i++)
                        _buffer += _writeStandardWall(r.wall_Intersections, i) + '\n';
                }
                if(count != 0) { 
                    string _b = "Intersections: [" + count + "]\n" + _buffer;
                    buffer.Add(_b);
                    //await File.AppendAllTextAsync(filePath, _b);
                }
            }
            if(r.note_failSwings != null) {
                int count = r.note_failSwings.Count;
                string _buffer = "Fail Swigns: [" + count + "]\n";
                for (int i = 0; i < r.note_failSwings.Count; i++) { 
                    _buffer += _writeStandardNote(r.note_failSwings, i);
                }
                if(count != 0) {
                    buffer.Add(_buffer);
                    //await File.AppendAllTextAsync(filePath, _buffer);
                }
            }
            if(r.note_OutOfRange != null || r.wall_OutOfRange != null) {
                int count = 0;
                string _buffer = "";
                if(r.note_OutOfRange != null) {
                    count += r.note_OutOfRange.Count;
                    for(int i = 0; i < r.note_OutOfRange.Count; i++)
                        _buffer += _writeStandardNote(r.note_OutOfRange, i);
                }
                if(r.wall_OutOfRange != null) {
                    count += r.wall_OutOfRange.Count;
                    for(int i = 0; i < r.wall_OutOfRange.Count; i++) {
                        _buffer += _writeStandardWall(r.wall_OutOfRange, i) + '\n';
                    }
                }
                if(count != 0) {
                    string _b = "Outside Range: [" + count + "]\n" + _buffer;
                    buffer.Add(_b);
                    //await File.AppendAllTextAsync(filePath, _b);
                }
            }
            await File.WriteAllLinesAsync(filePath, buffer);
        }

        /// <summary>
        /// Formats a note rule-exception in the logging format.
        /// </summary>
        /// <param name="list">A list of rule breaking notes</param>
        /// <param name="index">The given index to write for</param>
        /// <returns>"[Beat {time}]: left/right hand or bomb"</returns>
        private static string _writeStandardNote(List<json_MapNote> list, int index) {
            //Pipe decoration
            string _buffer = (index == list.Count-1) ? endPipe : cPipe;
            _buffer += "[Beat " + list[index]._time + "]: ";
            switch(list[index]._type) {
                case NoteType.Left: _buffer += "left hand\n"; break;
                case NoteType.Right: _buffer += "right hand\n"; break;
                case NoteType.Bomb: _buffer += "bomb\n"; break;
            }
            return _buffer;
        }

        /// <summary>
        /// Formats a wall rule-exception in the logging format.
        /// </summary>
        /// <param name="list">A list of rule breaking walls</param>
        /// <param name="index">The given index to write for</param>
        /// <returns>"[Beat {time}]: wall"</returns>
        private static string _writeStandardWall(List<json_MapObstacle> list, int index) {
            //Pipe decoration
            string _buffer = (index == list.Count-1) ? endPipe : cPipe;
            _buffer += "[Beat " + list[index]._time + "]: wall";
            return _buffer;
        }

        /// <summary>
        /// Error log, hopefully this dont happen too often.
        /// </summary>
        /// <param name="message">The error message</param>
        public static void LogError(string message) {
            LogHandle(message, "ERROR");    //Log to file
            ConsoleHandle(message);         //Log to user console
        }

        /// <summary>
        /// Logs a message to the users UI console.
        /// </summary>
        /// <param name="message">The message to be logged</param>
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

        /// <summary>
        /// Logs to the log file.
        /// </summary>
        /// <param name="message">The log message</param>
        /// <param name="hndl">the class thats logging's name</param>
        private static Task LogHandle(string message, string hndl) { 
            //Log to file with time stamp
            string timeStamp = DateTime.Now.ToString(logStampFormat);
            string logOut = $"[{timeStamp}|{hndl}]: {message}";
            //Debug to the visual studio debug log
            #if !RELEASE
            System.Diagnostics.Debug.WriteLine(logOut);
            #endif
            //Call LogFile write
            if(onLogUpdate != null)
                onLogUpdate.Invoke(logOut);
            return Task.CompletedTask;
        }
    }
}
