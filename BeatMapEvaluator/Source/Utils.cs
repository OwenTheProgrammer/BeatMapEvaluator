using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using WMPLib;
using System.IO;

namespace BeatMapEvaluator
{
    public enum ReportStatus {Error=0, Failed=1, Passed=2, None};
    internal class Utils {
        /// <summary>
        /// Systems defined path separator "/" "\" etc.
        /// </summary>
        private readonly static char _ps = Path.DirectorySeparatorChar;

        /// <summary>
        /// Calculates Jump Distance (JD) like you would see on JDFixer.
        /// </summary>
        /// 
        /// <remarks>
        /// <para name="bpm">bpm: 
        /// <a href="https://bsmg.wiki/mapping/map-format.html#beatsperminute">"_beatsPerMinute"</a>
        /// </para>
        /// <para name="njs">njs: 
        /// <a href="https://bsmg.wiki/mapping/map-format.html#notejumpmovementspeed">"_noteJumpMovementSpeed"</a>
        /// </para>
        /// <para name="offset">offset: 
        /// <a href="https://bsmg.wiki/mapping/map-format.html#notejumpstartbeatoffset">"_noteJumpStartBeatOffset"</a>
        /// </para>
        /// </remarks>
        /// 
        /// <param name="bpm">Songs Beats/Minute</param>
        /// <param name="njs">Songs Note Jump Speed</param>
        /// <param name="offset">Songs Note Offset</param>
        /// 
        /// <returns>Jump Distance</returns>
        public static float CalculateJD(float bpm, float njs, float offset) {
            //How was this implemented? https://cdn.discordapp.com/emojis/356735628110594048.webp
            if(njs <= 0.01f) njs = 10.0f;
            float hj = 4.0f;
            float bps = 60f / bpm;
            float leadTime = njs * bps;

            float c = leadTime * hj;
            while(c > 17.999f) {
                hj /= 2.0f;
                c = leadTime * hj;
            }

            hj += offset;
            if(hj < 0.25f)
                hj = 0.25f;

            return leadTime * hj * 2.0f;
        }

        /// <summary>
        /// Calculates the Reation Time in milliseconds.
        /// </summary>
        /// <remarks>
        /// <para name="njs">njs: 
        /// <a href="https://bsmg.wiki/mapping/map-format.html#notejumpmovementspeed">"_noteJumpMovementSpeed"</a>
        /// </para>
        /// </remarks>
        /// 
        /// <param name="jd">Jump Distance</param>
        /// <param name="njs">Note Jump Speed</param>
        /// <returns>the maps reaction time (ms)</returns>
        public static float CalculateRT(float jd, float njs) { 
            if(njs > 0.002f)
                return (jd / (2.0f * njs) * 1000.0f);
            return 0.0f;
        }

        /// <summary>
        /// Checks if float absolute difference ± within small value epsilon.
        /// </summary>
        /// <param name="a">the first number</param>
        /// <param name="b">the second number</param>
        /// <param name="epsilon">the max difference</param>
        /// <returns>
        /// <c>True</c> if numeric difference from <paramref name="a"/> to <paramref name="b"/> is within <paramref name="epsilon"/>, 
        /// <c>False</c> otherwise.
        /// </returns>
        public static bool Approx(float a, float b, float epsilon) {
            return Math.Abs(a - b) <= epsilon;
        }

        /// <summary>
        /// Serializes a maps available difficulties to flag enum <see cref="BeatMapEvaluator.MapDiffs"/>.
        /// </summary>
        /// <param name="Sets">All "Standard" beatmap sets</param>
        /// <returns>All difficulties in the beatmap set</returns>
        public static MapDiffs GetMapDifficulties(json_beatMapDifficulty[]? Sets) {
            //Handle no standard maps
            if(Sets == null)
                return MapDiffs.NONE;

            //Loop through all difficulties and add them to the diffs flag
            //https://bsmg.wiki/mapping/map-format.html#difficultyrank
            MapDiffs diffs = MapDiffs.NONE;
            foreach(json_beatMapDifficulty set in Sets) {
                switch(set._difficultyRank) {
                    case 1: diffs |= MapDiffs.Easy; break;
                    case 3: diffs |= MapDiffs.Normal; break;
                    case 5: diffs |= MapDiffs.Hard; break;
                    case 7: diffs |= MapDiffs.Expert; break;
                    case 9: diffs |= MapDiffs.ExpertPlus; break;
                }
            }
            return diffs;
        }

        /// <summary>
        /// Format BSR from a directory path to a value
        /// </summary>
        /// <remarks>
        /// <example> Example input:
        /// <code>
        /// mapPath = "C:\..\1e6ff (Som..).zip"
        /// returns "1e6ff"
        /// </code>
        /// </example>
        /// </remarks>
        /// <param name="mapPath">Directory path</param>
        /// <returns>BSR code</returns>
        public static string ParseBSR(string mapPath) { 
            int cut = mapPath.LastIndexOf(_ps) + 1;
            string name = mapPath.Substring(cut, mapPath.Length-cut);
            int end = name.IndexOf(' ');
            if(end != -1)
                name = name.Substring(0, end);
            return name;
        }

        /// <summary>
        /// Gets the note directly beside a given note on the same time step.
        /// </summary>
        /// <param name="list">List of notes on a time frame</param>
        /// <param name="note">The current note to look from</param>
        /// <param name="lookDir">Which direction we are testing</param>
        /// <returns>
        /// <c><see cref="BeatMapEvaluator.json_MapNote"/></c> if note found,
        /// <c>null</c> if note outside of range or no note found.
        /// </returns>
        public static json_MapNote? GetAdjacentNote(List<json_MapNote> list, json_MapNote note, NoteCutDirection lookDir) {
            int cell = note.cellIndex;

            bool u = note.yPos != 2;  //Not the top most layer
            bool d = note.yPos != 0;  //Not the bottom most layer
            bool l = note.xPos != 0;  //Not the left most column
            bool r = note.xPos != 3;  //Not the right most column

            switch(lookDir) {
                case NoteCutDirection.Up:   if(u) cell += 4; break;
                case NoteCutDirection.Down: if(d) cell -= 4; break;
                case NoteCutDirection.Left: if(l) cell -= 1; break;
                case NoteCutDirection.Right: if(r) cell += 1; break;
                case NoteCutDirection.UpLeft: if(u&&l) cell += 3; break;
                case NoteCutDirection.UpRight: if(u&&r) cell += 5; break;
                case NoteCutDirection.DownLeft: if(d&&l) cell -= 5; break;
                case NoteCutDirection.DownRight:if(d&&r) cell -= 3; break;
            }
            //if note space exists (switch above made a change)
            if(cell != note.cellIndex) { 
                foreach(var test in list) { 
                    if(test.cellIndex == cell)
                        return test;
                }   
            }
            //return null if nothing found
            return null;
        }
    }
}
