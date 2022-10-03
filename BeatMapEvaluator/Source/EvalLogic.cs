using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading.Tasks;

namespace BeatMapEvaluator
{
    public struct MapModList {
        public bool ModsNeeded;
        public string[]? ModNames;
    }

    internal class EvalLogic {
        public static Task<MapModList[]> mapHasRequirements(json_beatMapDifficulty[] diffs) {
            MapModList[] ModCheck = new MapModList[diffs.Length];
            for(int i = 0; i < diffs.Length; i++) {
                JObject? customData = (JObject?)diffs[i]._customData;
                if(customData != null) {
                    var t = customData.SelectToken("_requirements");
                    if(t != null) {
                        ModCheck[i].ModsNeeded = true;
                        ModCheck[i].ModNames = t.ToObject<string[]>();
                    }
                }
            }
            return Task.FromResult(ModCheck);
        }

    }
}
