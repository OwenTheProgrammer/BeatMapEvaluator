using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using WMPLib;

namespace BeatMapEvaluator
{
    internal class Utils {

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
    }
}
