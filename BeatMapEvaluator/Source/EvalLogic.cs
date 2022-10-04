using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
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

    public class MapStorageLayout {
        public json_beatMapDifficulty mapDiff;
        public Dictionary<float, List<json_MapNote>> noteCache;
        public Dictionary<float, List<json_MapObstacle>> obstacleCache;
        public float[] noteKeys;
        public float[] obstacleKeys;

        public float AudioLength;
        public float bpm, njs, noteOffset;
        public float jumpDistance => Utils.CalculateJD(bpm, njs, noteOffset);
        public float reactionTime => Utils.CalculateRT(jumpDistance, njs);

        public Task<(bool, float)> Eval_HotStart(float limit) {
            float bps = (60.0f / bpm);
            float beatLimit = bps * limit;
            float first = noteKeys[0];

            bool pass = (first > beatLimit);
            float elemTime = first / bps;

            return Task.FromResult((pass, elemTime));
        }
    }

    internal class EvalLogic {
        public static Task<MapModList> MapRequirementsCheck(json_beatMapDifficulty diff) {
            MapModList ModCheck = new MapModList();
            JObject? customData = (JObject?)diff._customData;
            if(customData != null) {
                var t = customData.SelectToken("_requirements");
                if(t != null) {
                    ModCheck.ModsNeeded = true;
                    ModCheck.ModNames = t.ToObject<string[]>();
                }
            }
            return Task.FromResult(ModCheck);
        }

        //Parallel loaders 
        public static Task< Dictionary<float, List<json_MapNote>> > LoadNotesToTable(json_DiffFileV2 diff) {
            Dictionary<float, List<json_MapNote>> table = new Dictionary<float, List<json_MapNote>>();
            table.EnsureCapacity(diff.noteCount);
            foreach(var note in diff._notes) {
                note.cellIndex = 4 * note._lineLayer + note._lineIndex;
                if(!table.ContainsKey(note._time))
                    table.Add(note._time, new List<json_MapNote>());
                table[note._time].Add(note);
            }
            return Task.FromResult(table);
        }
        public static Task< Dictionary<float, List<json_MapObstacle>> > LoadObstaclesToTable(json_DiffFileV2 diff) {
            Dictionary<float, List<json_MapObstacle>> table = new Dictionary<float, List<json_MapObstacle>>();
            table.EnsureCapacity(diff.obstacleCount);
            foreach(var obstacle in diff._obstacles) {
                if(!table.ContainsKey(obstacle._time))
                    table.Add(obstacle._time, new List<json_MapObstacle>());
                table[obstacle._time].Add(obstacle);
            }
            return Task.FromResult(table);
        }
    }
}
