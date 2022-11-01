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

        private readonly static char _ps = Path.DirectorySeparatorChar;
        //Some math wizard shit no one explained
        public static float CalculateJD(float bpm, float njs, float offset) {
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
        public static float CalculateRT(float jd, float njs) { 
            if(njs > 0.002f)
                return (jd / (2.0f * njs) * 1000.0f);
            return 0.0f;
        }

        public static bool Approx(float a, float b, float epsilon) {
            return Math.Abs(a - b) <= epsilon;
        }

        public static MapDiffs GetMapDifficulties(json_beatMapDifficulty[]? Sets) {
            if(Sets == null)
                return MapDiffs.NONE;

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
        public static string ParseBSR(string mapPath) { 
            int cut = mapPath.LastIndexOf(_ps) + 1;
            string name = mapPath.Substring(cut, mapPath.Length-cut);
            int end = name.IndexOf(' ');
            if(end != -1)
                name = name.Substring(0, end);
            return name;
        }

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
            //if testing inside of range
            if(cell != note.cellIndex) { 
                foreach(var test in list) { 
                    if(test.cellIndex == cell)
                        return test;
                }   
            }
            return null;
        }
    }
}
