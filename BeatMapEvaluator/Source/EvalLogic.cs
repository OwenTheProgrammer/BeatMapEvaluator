using LiveCharts.Maps;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Printing;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Xps.Serialization;

namespace BeatMapEvaluator
{
    public struct DiffCriteriaReport {
        public int[]? swingsPerSecond;
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
        //R4:cd
        public List<json_MapNote> note_OutOfRange;
        public List<json_MapObstacle> wall_OutOfRange;
        //R3:e, R5:a
        public List<json_MapNote> note_failSwings;

        //error, failed, passed
        public static readonly string[] diffColors = {"#713E93","#BE1F46","#9CED9C"};

        public int[] errors;
        public ReportStatus GetReportStatus() {
            errors = new int[7];
            for(int i = 0; i < 7; i++)
                errors[i] = -1;

            if(modsRequired != null)
                errors[0] = modsRequired.Count;
            if (note_HotStarts != null && wall_HotStarts != null)
                errors[1] = note_HotStarts.Count + wall_HotStarts.Count;
            if(note_ColdEnds != null && wall_ColdEnds != null)
                errors[2] = note_ColdEnds.Count + wall_ColdEnds.Count;
            if(note_Intersections != null && wall_Intersections != null)
                errors[3] = note_Intersections.Count + wall_Intersections.Count;
            if(note_failSwings != null)
                errors[4] = note_failSwings.Count;
            if(note_OutOfRange != null)
                errors[5] = note_OutOfRange.Count;
            if(wall_OutOfRange != null)
                errors[6] = wall_OutOfRange.Count;

            if(errors.Contains(-1)) 
                return ReportStatus.Error;
            if(errors.All(e => e == 0))
                return ReportStatus.Passed;
            return ReportStatus.Failed;
        }

        public void ClearCache() {
            if(note_HotStarts != null) note_HotStarts.Clear();
            if(wall_HotStarts != null) wall_HotStarts.Clear();
            if(note_ColdEnds != null) note_ColdEnds.Clear();
            if(wall_ColdEnds != null) wall_ColdEnds.Clear();
            if(note_Intersections != null) note_Intersections.Clear();
            if(wall_Intersections != null) wall_Intersections.Clear();
            if(note_OutOfRange != null) note_OutOfRange.Clear();
            if(wall_OutOfRange != null) wall_OutOfRange.Clear();
            if(note_failSwings != null) note_failSwings.Clear();
        }
    }

    public class MapStorageLayout {
        public DiffCriteriaReport report;
        public json_beatMapDifficulty? mapDiff;
        public json_DiffFileV2 diffFile;

        public Dictionary<float, List<json_MapNote>?>? noteCache;
        public Dictionary<float, List<json_MapObstacle>?>? wallCache;
        public int[]? SwingSegments;

        public ReportStatus reportStatus;
        public string? bsr;

        public int actualNoteCount;

        public float audioLength; //Audio length in seconds
        public float bpm, beatsPerSecond, njs;
        public float notesPerSecond;
        public float noteOffset;
        public float jumpDistance;
        public float reactionTime;

        public MapStorageLayout(json_MapInfo info, json_DiffFileV2 diff, int diffIndex) {
            noteCache = new Dictionary<float, List<json_MapNote>?>();
            wallCache = new Dictionary<float, List<json_MapObstacle>?>();
            
            string songPath = Path.Combine(info.mapContextDir, info._songFilename);
            reportStatus = ReportStatus.None;
            mapDiff = info.standardBeatmap._diffMaps[diffIndex];
            audioLength = AudioLib.GetAudioLength(songPath);
            bpm = info._bpm;
            beatsPerSecond = 60f / bpm;
            njs = mapDiff._njs;
            notesPerSecond = 0;
            noteOffset = mapDiff._noteOffset;
            jumpDistance = Utils.CalculateJD(bpm, njs, noteOffset);
            reactionTime = Utils.CalculateRT(jumpDistance, njs);
            diffFile = diff;
            bsr = info.mapBSR;
        }

        public async Task ProcessDiffRegistery() {
            report = new DiffCriteriaReport();
            //report.modsRequired = await Eval_ModsRequired();

            UserConsole.Log($"[{bsr}]: Caching map data..");
            Task[] Loaders = new Task[] {
                Eval_ModsRequired(),
                Load_NotesToCache(diffFile),
                Load_ObstaclesToCache(diffFile),
            };
            await Task.WhenAll(Loaders);
            report.modsRequired = ((Task<List<string>>)Loaders[0]).Result;
            Task[] Cullers = new Task[] {
                Eval_OutOfRangeNotes(),
                Eval_OutOfRangeWalls()
            };
            await Task.WhenAll(Cullers);
            report.swingsPerSecond = SwingSegments;
            report.note_OutOfRange = ((Task<List<json_MapNote>>)Cullers[0]).Result;
            report.wall_OutOfRange = ((Task<List<json_MapObstacle>>)Cullers[1]).Result;

            UserConsole.Log($"[{bsr}]: Evaluating map..");
            
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

            try {report.note_failSwings = await Eval_FailSwings();} 
            catch {UserConsole.LogError($"[{bsr} > FailSwings]: Failed to evaluate.");}
            
            reportStatus = report.GetReportStatus();
            UserConsole.Log($"[{bsr}]: Finished");
        }

        public void ClearDiff() {
            report.ClearCache();
            noteCache.Clear();
            wallCache.Clear();
            noteCache = null;
            wallCache = null;
            diffFile._notes = null;
            diffFile._walls = null;
            SwingSegments = null;
            mapDiff = null;
            GC.Collect();
        }

        public Task Load_NotesToCache(json_DiffFileV2 diff) {
            int cellCount = (int)Math.Ceiling(audioLength);
            SwingSegments = new int[cellCount];

            int noteCount = 0;
            foreach(var note in diff._notes) {
                note.cellIndex = 4 * note.yPos + note.xPos;
                note.realTime = note._time * beatsPerSecond;
                if(note._type != NoteType.Bomb) {
                    int index = (int)Math.Floor(note.realTime);
                    SwingSegments[index]++;
                    noteCount++;
                }

                if(!noteCache.ContainsKey(note._time)) {
                    var push = new List<json_MapNote>(){note};
                    noteCache.Add(note._time, push);
                } else {
                    noteCache[note._time].Add(note);
                }
            }
            notesPerSecond = noteCount / audioLength;
            return Task.CompletedTask;
        }
        public Task Load_ObstaclesToCache(json_DiffFileV2 diff) {
            const float shortWallEpsilon = 1.0f / 72.0f;
            foreach(var wall in diff._walls) {
                wall.isInteractive = wall.xPos == 1 || wall.xPos == 2;
                wall.realTime = wall._time * beatsPerSecond;
                wall.endTime = wall._time + wall._duration;
                wall.isInteractive = wall.realTime < shortWallEpsilon;
                
                if(!wallCache.ContainsKey(wall._time)) {
                    var push = new List<json_MapObstacle>(){wall};
                    wallCache.Add(wall._time, push);
                } else {
                    wallCache[wall._time].Add(wall);
                }
            }
            return Task.CompletedTask;
        }
        
        //R1:d
        public Task<List<string>> Eval_ModsRequired() {
            List<string> modList = new List<string>();
            JObject? customData = (JObject?)mapDiff._customData;
            if(customData != null) {
                var t = customData.SelectToken("_requirements");
                if(t != null) {
                    var modCell = t.ToObject<string[]>();
                    if(modCell != null)
                        modList.AddRange(modCell);
                }
            }
            return Task.FromResult(modList);
        }
        //R1:f
        public Task<List<json_MapNote>> Eval_NoteHotStart(float limit) {
            List<json_MapNote> offenders = new List<json_MapNote>();
            float beatLimit = limit * beatsPerSecond;
            foreach(var (time, list) in noteCache) { 
                if(time < beatLimit)
                    offenders.AddRange(list);
                else break;
            }
            return Task.FromResult(offenders);
        }
        public Task<List<json_MapObstacle>> Eval_WallHotStart(float limit) {
            List<json_MapObstacle> offenders = new List<json_MapObstacle>();
            float beatLimit = beatsPerSecond * limit;

            foreach(var (time, list) in wallCache) { 
                if(time < beatLimit) { 
                    foreach(var wall in list) { 
                        if(wall.isInteractive)
                            offenders.Add(wall);
                    }
                }
                else break;
            }
            return Task.FromResult(offenders);
        }
        //R1:g
        public Task<List<json_MapNote>> Eval_NoteColdEnd(float limit) {
            List<json_MapNote> offenders = new List<json_MapNote>();
            float kernel = (audioLength - limit) / beatsPerSecond;
            Stack<float> time_rev = new Stack<float>(noteCache.Keys);
            foreach(var time in time_rev) {
                if(time >= kernel)
                    offenders.AddRange(noteCache[time]);
                else break;
            }
            return Task.FromResult(offenders);
        }
        public Task<List<json_MapObstacle>> Eval_WallColdEnd(float limit) {
            List<json_MapObstacle> offenders = new List<json_MapObstacle>();
            float kernel = audioLength - limit;
            Stack<float> time_rev = new Stack<float>(wallCache.Keys);

            foreach(var time in time_rev) { 
                if(time >= kernel) { 
                    foreach(var wall in wallCache[time]) {
                        if(wall.isInteractive) {
                            offenders.Add(wall);
                        }
                    }
                } else break;       
            }
            return Task.FromResult(offenders);
        }
        //R3:a
        public Task<List<json_MapNote>> Eval_NoteIntersections() {
            List<json_MapNote> offenders = new List<json_MapNote>();

            foreach(var (time, list) in noteCache) { 
                bool[] used = new bool[3*4];
                foreach(var note in list) { 
                    if(used[note.cellIndex])
                        offenders.Add(note);
                    used[note.cellIndex] = true;
                }
            }

            return Task.FromResult(offenders);
        }
        public Task<List<json_MapObstacle>> Eval_WallIntersections() {
            List<json_MapObstacle> offenders = new List<json_MapObstacle>();

            int cMin = 0;
            float[] noteKeys = noteCache.Keys.ToArray();
            foreach(var (time, list) in wallCache) {
                for(; cMin < noteKeys.Length; cMin++) { 
                    if(noteKeys[cMin] >= time) break;
                }

                foreach(var wall in list) {
                    int wx = wall.xPos;
                    int wSpan = wx + wall._width;
                    bool isFull = wall._type == ObstacleType.FullWall;

                    while(cMin < noteKeys.Length) {
                        float t = noteKeys[cMin];
                        if(t > wall.endTime) break;

                        foreach(var note in noteCache[t]) {
                            int nx = note.xPos;
                            int ny = note.yPos;
                            bool inside = (nx >= wx) && (nx < wSpan);
                            if(inside && (!isFull && ny != 0)) {
                                offenders.Add(wall);
                            }
                        }
                        cMin++;
                    }
                }
            }
            return Task.FromResult(offenders);
        }

        public Task<List<json_MapNote>> Eval_OutOfRangeNotes() {
            List<json_MapNote> offenders = new List<json_MapNote>();

            List<float> globalTomb = new List<float>();
            foreach(var (time, list) in noteCache) {
                List<json_MapNote> tomb = new List<json_MapNote>();
                foreach(var note in list) {
                    bool xBound = note.xPos < 0 || note.xPos > 3;
                    bool yBound = note.yPos < 0 || note.yPos > 2;
                    if(xBound || yBound)
                        tomb.Add(note);
                }
                offenders.AddRange(tomb);

                foreach(var target in tomb)
                    list.Remove(target);

                if(list.Count == 0)
                    globalTomb.Add(time);
            }
            foreach(float target in globalTomb)
                noteCache.Remove(target);
            return Task.FromResult(offenders);
        }
        //R4:cd
        public Task<List<json_MapObstacle>> Eval_OutOfRangeWalls() {
            List<json_MapObstacle> offenders = new List<json_MapObstacle>();

            List<float> globalTomb = new List<float>();
            foreach(var (time, list) in wallCache) {
                List<json_MapObstacle> tomb = new List<json_MapObstacle>();
                foreach(var wall in list) {
                    int wx = wall.xPos;
                    int wSpan = wx + (wall._width - 1);

                    bool wZero = Utils.Approx(wall._duration, 0f, 0.001f);
                    bool nWidth = wall._width < 0f;
                    bool nDur = wall._duration < 0f;

                    bool outOfRange = wx < 0 || wx > 3 || wSpan > 3;
                    bool invalid = wZero || nWidth || nDur;
                    if(outOfRange || invalid)
                        tomb.Add(wall);
                }
                offenders.AddRange(tomb);

                foreach(var target in tomb)
                    list.Remove(target);
                if(list.Count == 0)
                    globalTomb.Add(time);
            }
            foreach(var target in globalTomb)
                wallCache.Remove(target);

            return Task.FromResult(offenders);
        }
        //R3:e, R5:a
        public Task<List<json_MapNote>> Eval_FailSwings() {
            List<json_MapNote> offenders = new List<json_MapNote>();

            foreach(var (time, list) in noteCache) { 
                foreach(var note in list) { 
                    if(note._type != NoteType.Bomb) {
                        var next = Utils.GetAdjacentNote(list, note, note._dir);
                        if(next != null && next._type != note._type)
                            offenders.Add(next);
                    }
                }
            }

            return Task.FromResult(offenders);
        }
    }
}
