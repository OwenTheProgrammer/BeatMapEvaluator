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
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Xps.Serialization;

namespace BeatMapEvaluator
{
    public struct DiffCriteriaReport {
        public int[] swingsPerSecond;
        //R1:d
        public List<string> modsRequired;
        //R1:f
        public List<json_MapNote> note_HotStarts;
        public List<json_MapObstacle> wall_HotStarts;
        //R1:g
        public List<json_MapNote> note_ColdEnds;
        public List<json_MapObstacle> wall_ColdEnds;
        //R3:a
        public List<json_MapNote> note_Intersections;
        public List<json_MapObstacle> wall_Intersections;
        public List<json_MapNote> note_OutOfRange;
        public List<json_MapObstacle> wall_OutOfRange;
        //R4:cd
        public List<json_MapObstacle> wall_Lengths;
        //R3:e, R5:a
        public List<json_MapNote> note_failSwings;

        //bad, good, error
        public static string[] diffColors = {"#BE1F46","#9CED9C","#713E93"};

        public int[] errors;
        public int GetReportColor() {
            errors = new int[8];
            for(int i = 0; i < 8; i++)
                errors[i] = -1;

            if(modsRequired != null)
                errors[0] = modsRequired.Count;
            if (note_HotStarts != null && wall_HotStarts != null)
                errors[1] = note_HotStarts.Count + wall_HotStarts.Count;
            if(note_ColdEnds != null && wall_ColdEnds != null)
                errors[2] = note_ColdEnds.Count + wall_ColdEnds.Count;
            if(note_Intersections != null && wall_Intersections != null)
                errors[3] = note_Intersections.Count + wall_Intersections.Count;
            if(wall_Lengths != null)
                errors[4] = wall_Lengths.Count;
            if(note_failSwings != null)
                errors[5] = note_failSwings.Count;
            if(note_OutOfRange != null)
                errors[6] = note_OutOfRange.Count;
            if(wall_OutOfRange != null)
                errors[7] = wall_OutOfRange.Count;

            if(errors.Contains(-1)) return 2; //Error
            if(errors.All(e => e == 0)) return 1; //Good
            return 0; //Bad
        }
    }

    public class MapStorageLayout {
        public DiffCriteriaReport report;
        public int reportColorIndex;
        public json_beatMapDifficulty mapDiff;
        public json_DiffFileV2 diffFile;

        public Dictionary<float, List<json_MapNote>>? noteCache;
        public Dictionary<float, List<json_MapObstacle>>? obstacleCache;
        public float[]? noteKeys;
        public float[]? obstacleKeys;
        public string bsr;

        public int actualNoteCount;

        public float audioLength; //Audio length in seconds
        public float bpm, beatsPerSecond, njs, nps;
        public float noteOffset;
        public float jumpDistance;
        public float reactionTime;

        public MapStorageLayout(json_MapInfo info, json_DiffFileV2 diff, int diffIndex) {
            string songPath = Path.Combine(info.mapContextDir, info._songFilename);
            reportColorIndex = -1;
            mapDiff = info.standardBeatmap._diffMaps[diffIndex];
            audioLength = AudioLib.GetAudioLength(songPath);
            bpm = info._bpm;
            beatsPerSecond = 60f / bpm;
            njs = mapDiff._njs;
            nps = 0;
            noteOffset = mapDiff._noteOffset;
            jumpDistance = Utils.CalculateJD(bpm, njs, noteOffset);
            reactionTime = Utils.CalculateRT(jumpDistance, njs);
            diffFile = diff;
            bsr = info.mapBSR;
        }

        public async Task ProcessDiffRegistery() {
            report = new DiffCriteriaReport();
            report.modsRequired = await Eval_ModsRequired();

            UserConsole.Log($"[{bsr}]: Caching map data..");
            Task[] Loaders = new Task[] {
                Load_NotesToCache(diffFile),
                Load_ObstaclesToCache(diffFile),
                Eval_OutOfRangeNotes(),
                Eval_OutOfRangeWalls()
            };
            nps = Calc_NotesPerSecond();
            await Task.WhenAll(Loaders);

            report.note_OutOfRange = ((Task<List<json_MapNote>>)Loaders[2]).Result;
            report.wall_OutOfRange = ((Task<List<json_MapObstacle>>)Loaders[3]).Result;

            UserConsole.Log($"[{bsr}]: Evaluating map..");

            //... This is probably the worst moment of my life writing this out.
            try {report.swingsPerSecond = await Calc_SwingsPerSecond();} 
            catch {UserConsole.LogError($"[{bsr} > Swings/Sec]: Failed to evaluate.");}

            try {report.note_HotStarts = await Eval_NoteHotStart(1.33f);} 
            catch {UserConsole.LogError($"[{bsr} > HotStartNotes]: Failed to evaluate.");}

            try {report.wall_HotStarts = await Eval_WallHotStart(1.33f);} 
            catch {UserConsole.LogError($"[{bsr} > HotStartWalls]: Failed to evaluate.");}

            try {report.note_ColdEnds = await Eval_NoteColdEnd(2.0f);} 
            catch {UserConsole.LogError($"[{bsr} > ColdEndNotes]: Failed to evaluate.");}

            try {report.wall_ColdEnds = await Eval_WallColdEnd(2.0f);} 
            catch {UserConsole.LogError($"[{bsr} > ColdEndWalls]: Failed to evaluate.");}

            try {report.note_Intersections = await Eval_NoteIntersections();} 
            catch {UserConsole.LogError($"[{bsr} > NoteIntersections]: Failed to evaluate.");}

            try {report.wall_Intersections = await Eval_WallIntersections();} 
            catch {UserConsole.LogError($"[{bsr} > WallIntersections]: Failed to evaluate.");}

            try {report.wall_Lengths = await Eval_WallDurations();} 
            catch {UserConsole.LogError($"[{bsr} > WallLength]: Failed to evaluate.");}

            try {report.note_failSwings = await Eval_FailSwings();} 
            catch {UserConsole.LogError($"[{bsr} > FailSwings]: Failed to evaluate.");}
            
            reportColorIndex = report.GetReportColor();
            UserConsole.Log($"[{bsr}]: Finished");
        }

        public Task Load_NotesToCache(json_DiffFileV2 diff) { 
            noteCache = new Dictionary<float, List<json_MapNote>>();
            noteCache.EnsureCapacity(diff.noteCount);
            actualNoteCount = 0;
            foreach (var note in diff._notes) {
                note.cellIndex = 4 * note._lineLayer + note._lineIndex;
                note.actualTime = note._time / beatsPerSecond;

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
                obstacle.actualTime = obstacle._time / beatsPerSecond;
                obstacle.endTime = obstacle._time + obstacle._duration;
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
            return (float)actualNoteCount / audioLength;
        }
        public Task<int[]> Calc_SwingsPerSecond() {
            int cellCount = (int)Math.Ceiling(audioLength);
            int[] segments = new int[cellCount];

            for(int i = 0; i < noteKeys.Length; i++) {
                float currentKey = noteKeys[i];
                int segIndex = (int)Math.Floor(currentKey * beatsPerSecond);
                foreach(json_MapNote note in noteCache[currentKey]) { 
                    if(note._type == NoteType.Left ||
                       note._type == NoteType.Right) {
                        segments[segIndex]++;
                    }
                }
            }
            return Task.FromResult(segments);
        }
        
        //R1:d
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
        //R1:f
        public Task<List<json_MapNote>> Eval_NoteHotStart(float limit) {
            List<json_MapNote> offenders = new List<json_MapNote>();
            float beatLimit = beatsPerSecond * limit;

            foreach(var timeStep in noteCache) { 
                if(timeStep.Key < beatLimit)
                    offenders.AddRange(timeStep.Value);
                else break;
            }
            return Task.FromResult(offenders);
        }
        public Task<List<json_MapObstacle>> Eval_WallHotStart(float limit) {
            List<json_MapObstacle> offenders = new List<json_MapObstacle>();
            float beatLimit = beatsPerSecond * limit;

            foreach(var timeStep in obstacleCache) { 
                if(timeStep.Key < beatLimit)
                    offenders.AddRange(timeStep.Value.Where(w=>w.isInteractive));
                else break;
            }
            return Task.FromResult(offenders);
        }
        //R1:g
        public Task<List<json_MapNote>> Eval_NoteColdEnd(float limit) {
            List<json_MapNote> offenders = new List<json_MapNote>();
            float kernel = (audioLength - limit) / beatsPerSecond;
            for(int i = noteKeys.Length-1; i > 0; i--) {
                float key = noteKeys[i];
                if(key >= kernel)
                    offenders.AddRange(noteCache[i]);
                else break;
            }
            return Task.FromResult(offenders);
        }
        public Task<List<json_MapObstacle>> Eval_WallColdEnd(float limit) {
            List<json_MapObstacle> offenders = new List<json_MapObstacle>();
            float kernel = audioLength - limit;

            foreach(var timeStep in obstacleCache) {
                if(timeStep.Key >= kernel)
                    offenders.AddRange(timeStep.Value.Where(w=>w.isInteractive));
            }
            return Task.FromResult(offenders);
        }
        //R3:a
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
        public Task<List<json_MapObstacle>> Eval_WallIntersections() {
            List<json_MapObstacle> offenders = new List<json_MapObstacle>();

            int currentMin = 0;
            foreach(var (time, list) in obstacleCache) {
                for(; currentMin < noteKeys.Length; currentMin++) {
                    float t = noteKeys[currentMin];
                    if(t >= time) break;
                }
                //no over-run
                currentMin = Math.Min(currentMin, noteKeys.Length-1);

                foreach(var wall in list) {
                    int wx = wall._lineIndex;
                    int wSpan = wx + wall._width;
                    bool wFull = wall._type == ObstacleType.FullWall;

                    while(currentMin < noteKeys.Length && noteKeys[currentMin] < wall.endTime) {
                        float t = noteKeys[currentMin];
                        foreach(var note in noteCache[t]) {
                            int nx = note._lineIndex;
                            int ny = note._lineLayer;
                            bool inWidth = (nx >= wx) && (nx < wSpan);
                            if(inWidth && (!wFull && ny != 0)) {
                                offenders.Add(wall);
                            }
                        }
                        currentMin++;
                    }
                }
            }
            return Task.FromResult(offenders);
        }

        public Task<List<json_MapNote>> Eval_OutOfRangeNotes() {
            List<json_MapNote> offenders = new List<json_MapNote>();
            foreach(var noteList in noteCache.Values) {
                List<json_MapNote> removal = new List<json_MapNote>();
                foreach(var note in noteList) {
                    bool xOOR = note._lineIndex < 0 || note._lineIndex > 3;
                    bool yOOR = note._lineLayer < 0 || note._lineLayer > 2;
                    if(xOOR || yOOR) {
                        offenders.Add(note);
                        removal.Add(note);
                    }
                }
                removal.ForEach(r => noteList.Remove(r));
            }
            return Task.FromResult(offenders);
        }
        public Task<List<json_MapObstacle>> Eval_OutOfRangeWalls() {
            List<json_MapObstacle> offenders = new List<json_MapObstacle>();
            foreach(var wallList in obstacleCache.Values) {
                List<json_MapObstacle> removal = new List<json_MapObstacle>();
                foreach(var wall in wallList) {
                    int wallOffset = wall._lineIndex + (wall._width-1);
                    if(wall._lineIndex < 0 || wallOffset > 3) {
                        offenders.Add(wall);
                        removal.Add(wall);
                    }
                }
                removal.ForEach(r => wallList.Remove(r));
            }
            return Task.FromResult(offenders);
        }
        //R4:cd
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
        //R3:e, R5:a
        public Task<List<json_MapNote>> Eval_FailSwings() {
            List<json_MapNote> offenders = new List<json_MapNote>();
            foreach(var noteList in noteCache.Values) { 
                foreach(var note in noteList) { 
                    //Checking for all notes
                    if(note._type != NoteType.Bomb) {
                        var next = Utils.GetAdjacentNote(noteList, note, note._cutDirection);
                        if(next != null && next._type != note._type) {
                            offenders.Add(next);
                        }
                    }
                }
            }
            return Task.FromResult(offenders);
        }
    }
}
