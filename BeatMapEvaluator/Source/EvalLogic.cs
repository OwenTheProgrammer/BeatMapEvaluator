using LiveCharts.Maps;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Printing;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Xps.Serialization;

namespace BeatMapEvaluator
{
    public struct DiffCriteriaReport {
        public List<string> modsRequired;
        public int[] swingsPerSecond;
        public List<json_MapNote> note_HotStarts;
        public List<json_MapNote> note_ColdEnds;
        public List<json_MapObstacle> wall_HotStarts;
        public List<json_MapObstacle> wall_ColdEnds;
    }

    public class MapStorageLayout {
        public DiffCriteriaReport report;
        public json_beatMapDifficulty mapDiff;
        public json_DiffFileV2 diffFile;

        public Dictionary<float, List<json_MapNote>>? noteCache;
        public Dictionary<float, List<json_MapObstacle>>? obstacleCache;
        public float[]? noteKeys;
        public float[]? obstacleKeys;

        public int actualNoteCount;

        public float AudioLength;
        public float bpm, bps, njs, nps;
        public float noteOffset;
        public float jumpDistance;
        public float reactionTime;
        public float MaxWallLength;

        public MapStorageLayout(json_MapInfo info, json_DiffFileV2 diff, int diffIndex) {
            string songPath = Path.Combine(info.mapContextDir, info._songFilename);
            mapDiff = info.standardBeatmap._diffMaps[diffIndex];
            AudioLength = AudioLib.GetAudioLength(songPath);
            bpm = info._bpm;
            bps = 60f / bpm;
            njs = mapDiff._njs;
            nps = 0;
            noteOffset = mapDiff._noteOffset;
            jumpDistance = Utils.CalculateJD(bpm, njs, noteOffset);
            reactionTime = Utils.CalculateRT(jumpDistance, njs);
            diffFile = diff;
        }

        public async Task ProcessDiffRegistery() {
            report = new DiffCriteriaReport();
            report.modsRequired = await Eval_ModsRequired();

            Task[] Loaders = new Task[] {
                Load_NotesToCache(diffFile),
                Load_ObstaclesToCache(diffFile)
            };
            //Task noteLoader = Load_NotesToCache(diffFile);
            //Task wallLoader = Load_ObstaclesToCache(diffFile);

            nps = Calc_NotesPerSecond();
            report.swingsPerSecond = await Calc_SwingsPerSecond();
            UserConsole.Log("Finished");
        }

        public Task Load_NotesToCache(json_DiffFileV2 diff) { 
            noteCache = new Dictionary<float, List<json_MapNote>>();
            noteCache.EnsureCapacity(diff.noteCount);
            actualNoteCount = 0;
            foreach (var note in diff._notes) {
                note.cellIndex = 4 * note._lineLayer + note._lineIndex;
                note.actualTime = note._time / bps;

                if(note._type == NoteType.Left || 
                    note._type == NoteType.Right)
                    actualNoteCount++;

                if(!noteCache.ContainsKey(note._time))
                    noteCache.Add(note._time, new List<json_MapNote>());
                noteCache[note._time].Add(note);
            }
            noteKeys = noteCache.Keys.ToArray();
            return Task.CompletedTask;
        }
        public Task Load_ObstaclesToCache(json_DiffFileV2 diff) { 
            obstacleCache = new Dictionary<float, List<json_MapObstacle>>();
            obstacleCache.EnsureCapacity(diff.obstacleCount);

            foreach(var obstacle in diff._obstacles) {
                obstacle.isInteractive = (obstacle._lineIndex==1||
                                          obstacle._lineIndex==2);
                obstacle.actualTime = obstacle._time / bps;
                if(obstacle._duration > MaxWallLength)
                    MaxWallLength = obstacle._duration;
                //If the walls length is less than 13.8ms
                obstacle.isShortWall = (obstacle.actualTime < (1.0 / 72.0));

                if(!obstacleCache.ContainsKey(obstacle._time))
                    obstacleCache.Add(obstacle._time, new List<json_MapObstacle>());
                obstacleCache[obstacle._time].Add(obstacle);
            }
            obstacleKeys = obstacleCache.Keys.ToArray();
            return Task.CompletedTask;
        }

        public float Calc_NotesPerSecond() {
            return (float)actualNoteCount / AudioLength;
        }
        public Task<int[]> Calc_SwingsPerSecond() {
            int cellCount = (int)Math.Ceiling(AudioLength);
            int[] segments = new int[cellCount];

            for(int i = 0; i < noteKeys.Length; i++) {
                float currentKey = noteKeys[i];
                int segIndex = (int)Math.Floor(currentKey * bps);
                foreach(json_MapNote note in noteCache[currentKey]) { 
                    if(note._type == NoteType.Left ||
                       note._type == NoteType.Right) {
                        segments[segIndex]++;
                    }
                }
            }
            return Task.FromResult(segments);
        }
        public Task<List<string>> Eval_ModsRequired() {
            List<string> Mods = new List<string>();
            JObject? customData = (JObject?)mapDiff._customData;
            if(customData != null) {
                var t = customData.SelectToken("_requirements");
                if(t != null)
                    Mods.AddRange(t.ToObject<string[]>());
            }
            return Task.FromResult(Mods);
        }

        public Task<List<json_MapNote>> Eval_NoteHotStart(float limit) {
            List<json_MapNote> offenders = new List<json_MapNote>();
            float beatLimit = bps * limit;

            foreach(var timeStep in noteCache) { 
                if(timeStep.Key < beatLimit)
                    offenders.AddRange(timeStep.Value);
                else break;
            }
            return Task.FromResult(offenders);
        }
        public Task<List<json_MapNote>> Eval_NoteColdEnd(float limit) {
            List<json_MapNote> offenders = new List<json_MapNote>();
            float leadBeats = (AudioLength*bps) - (limit*bps);
            for(int i = noteKeys.Length-1; i > 0; i--) {
                if(noteKeys[i] > leadBeats)
                    offenders.AddRange(noteCache[i]);
                else break;
            }
            return Task.FromResult(offenders);
        }
        public Task<List<json_MapNote>> Eval_NoteIntersections() {
            List<json_MapNote> offenders = new List<json_MapNote>();
            foreach(var noteList in noteCache.Values) {
                bool[] SpaceUsed = new bool[3*4];
                foreach(json_MapNote note in noteList) {
                    if(SpaceUsed[note.cellIndex])
                        offenders.Add(note);
                    SpaceUsed[note.cellIndex] = true;
                }
            }
            return Task.FromResult(offenders);
        }
        public Task<List<json_MapNote>> Eval_NoteWallIntersections() {
            List<json_MapNote> offenders = new List<json_MapNote>();
            foreach(var wallKey in obstacleCache) {
                //Find the first key that is before the cache point
                int noteIndex = 0;
                foreach(float noteKey in noteKeys) {
                    if(noteKey >= wallKey.Key)
                        break;
                    noteIndex++;
                }
                foreach(json_MapObstacle wall in wallKey.Value) {
                    int xLength = wall._lineIndex + wall._width;
                    float end = wall._time + wall._duration;
                    float key = noteKeys[noteIndex];
                    for(int i = noteIndex; key < end; i++) {
                        foreach(var note in noteCache[key]) {
                            bool isFullWall = wall._type == ObstacleType.FullWall;
                            bool inLeftBound = note._lineIndex >= wall._lineIndex;
                            bool inRightBound = note._lineIndex <= xLength;
                            if(inLeftBound && inRightBound) {
                                if(!isFullWall && note._lineLayer == 0)
                                    continue;
                                offenders.Add(note);
                            }
                        }
                        key = noteKeys[noteIndex];
                    }
                }
            }
            return Task.FromResult(offenders);
        }

        public Task<List<json_MapObstacle>> Eval_WallHotStart(float limit) {
            List<json_MapObstacle> offenders = new List<json_MapObstacle>();
            float beatLimit = bps * limit;

            foreach(var timeStep in obstacleCache) { 
                if(timeStep.Key < beatLimit)
                    offenders.AddRange(timeStep.Value.Where(w=>w.isInteractive));
                else break;
            }
            return Task.FromResult(offenders);
        }
        public Task<List<json_MapObstacle>> Eval_WallColdEnd(float limit) { 
            List<json_MapObstacle> offenders = new List<json_MapObstacle>();
            float leadBeats = (AudioLength*bps) - (MaxWallLength*bps) - (limit*bps);
            for(int i = obstacleKeys.Length-1; i > 0; i--) {
                if(obstacleKeys[i] > leadBeats)
                    offenders.AddRange(obstacleCache[i]);
                else break;
            }
            return Task.FromResult(offenders);
        }
        public Task<List<json_MapObstacle>> Eval_WallDurations() {
            List<json_MapObstacle> offenders = new List<json_MapObstacle>();

            foreach(var wallList in obstacleCache) { 
                foreach(json_MapObstacle wall in wallList.Value) {
                    bool zeroWidth = Utils.Approx(wall._duration, 0f, 0.001f);
                    bool negDuration = wall._duration < 0f;
                    bool negWidth = wall._width < 0f;

                    if(negWidth || negDuration || zeroWidth)
                        offenders.Add(wall);
                }
            }
            return Task.FromResult(offenders);
        }
    }
}
