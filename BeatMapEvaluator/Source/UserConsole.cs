using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Windows.Media.Animation;

namespace BeatMapEvaluator
{
    internal class UserConsole {
        const string cPipe = "├─ ";
        const string endPipe = "└─ ";

        private readonly static string logStampFormat = @"hh:mm:ss";
        private static string[] consoleBuffer = new string[7];

        public delegate void updateStringGUI(string ctx);

        public static updateStringGUI? onConsoleUpdate = null;
        public static updateStringGUI? onLogUpdate = null;

        public static void Log(string message) {
            LogHandle(message, "LOG");
            ConsoleHandle(message);
        }

        public static async Task ExportReport(MapStorageLayout layout, json_MapInfo info, string filePath) {

            DiffCriteriaReport r = layout.report;
            string _title = "- " + layout.bsr + ": " + info._songName + " -\n";
            string _diff = "Difficulty: " + layout.mapDiff._difficulty + "\n\n";

            await File.WriteAllTextAsync(filePath, _title);
            await File.AppendAllTextAsync(filePath, _diff);
            if(r.modsRequired != null) {
                string _buffer = "Mods: [" + r.modsRequired.Count + "]\n";
                for(int i = 0; i < r.modsRequired.Count; i++) {
                    _buffer += (i==r.modsRequired.Count-1) ? endPipe : cPipe;
                    _buffer += r.modsRequired[i] + "\n";
                }
                await File.AppendAllTextAsync(filePath, _buffer);
            }
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
                    await File.AppendAllTextAsync(filePath, _b);
                }
            }
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
                    await File.AppendAllTextAsync(filePath, _b);
                }
            }
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
                    await File.AppendAllTextAsync(filePath, _b);
                }
            }
            if(r.wall_Lengths != null) {
                int count = r.wall_Lengths.Count;
                string _buffer = "Wall Lengths: [" + count + "]\n";
                for (int i = 0; i < r.wall_Lengths.Count; i++) { 
                    _buffer += _writeStandardWall(r.wall_Lengths, i);
                    if(r.wall_Lengths[i]._duration < 0) _buffer += "neg-time\n";
                    else if(r.wall_Lengths[i]._width < 0) _buffer += "neg-width\n";
                    else _buffer += "zero width\n";
                }
                if(count != 0) { 
                    await File.AppendAllTextAsync(filePath, _buffer);
                }
            }
            if(r.note_failSwings != null) {
                int count = r.note_failSwings.Count;
                string _buffer = "Fail Swigns: [" + count + "]\n";
                for (int i = 0; i < r.note_failSwings.Count; i++) { 
                    _buffer += _writeStandardNote(r.note_failSwings, i);
                }
                if(count != 0) { 
                    await File.AppendAllTextAsync(filePath, _buffer);
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
                    string _b = "Out of Bounds: [" + count + "]\n" + _buffer;
                    await File.AppendAllTextAsync(filePath, _b);
                }
            }

        }

        private static string _writeStandardNote(List<json_MapNote> list, int index) {
            string _buffer = (index == list.Count-1) ? endPipe : cPipe;
            _buffer += "[Beat " + list[index]._time + "]: ";
            switch(list[index]._type) {
                case NoteType.Left: _buffer += "left hand\n"; break;
                case NoteType.Right: _buffer += "right hand\n"; break;
                case NoteType.Bomb: _buffer += "bomb\n"; break;
            }
            return _buffer;
        }
        private static string _writeStandardWall(List<json_MapObstacle> list, int index) {
            string _buffer = (index == list.Count-1) ? endPipe : cPipe;
            _buffer += "[Beat " + list[index]._time + "]: wall";
            return _buffer;
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
