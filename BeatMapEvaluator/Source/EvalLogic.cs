using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace BeatMapEvaluator
{
	/// <summary>Basic evaluation info holder struct</summary>
	public struct DiffCriteriaReport {
		//Swings per second for both hands
		public int[]? LeftHandSwings, RightHandSwings;
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

		//Error counts
		public int[] errors;
		/// <summary>
		/// Fills out <see cref="errors"/> with error counts per error check
		/// </summary>
		/// <returns>Daaa status enum</returns>
		public ReportStatus GetReportStatus() {
			//Preset all errors to -1 (Error failed)
			errors = new int[7];
			for(int i = 0; i < 7; i++)
				errors[i] = -1;

			//Move the list requirements to the errors array
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

			//Any of the errors are -1
			if(errors.Contains(-1)) 
				return ReportStatus.Error;
			//All are zero mean good
			if(errors.All(e => e == 0))
				return ReportStatus.Passed;
			// :/
			return ReportStatus.Failed;
		}

		/// <summary>Clears anything that has memory allocated from the struct</summary>
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

		//??? C# moment lol
		/// <summary>The maps notes in a cache</summary>
		public Dictionary<float, List<json_MapNote>?>? noteCache;
		/// <summary>The maps walls in a cache</summary>
		public Dictionary<float, List<json_MapObstacle>?>? wallCache;
		/// <summary>The swings per second L/R arrays</summary>
		public int[]? LeftHandSwings, RightHandSwings;

		/// <summary>This maps evaluated status</summary>
		public ReportStatus reportStatus;
		/// <summary>The map BSR code</summary>
		public string? bsr;

		/// <summary>Total note count (excluding bombs)</summary>
		public int actualNoteCount;

		//Audio Length in seconds
		public float audioLength;
		/// <summary>Seconds -> Beats if multiplied</summary>
		public float beatsPerSecond;

		public float bpm, njs;
		public float notesPerSecond;
		public float noteOffset;
		public float jumpDistance;
		public float reactionTime;

		//mmhm mmhm yep mmhm we constructing the class instance
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

		/// <summary>Process the evaluation logic on this MapStorageLayout.</summary>
		public async Task ProcessDiffRegistery() {
			report = new DiffCriteriaReport();
			//report.modsRequired = await Eval_ModsRequired();

			//Preload the mods required, notes and walls
			UserConsole.Log($"[{bsr}]: Caching map data..");
			Task[] Loaders = new Task[] {
				Eval_ModsRequired(),
				Load_NotesToCache(diffFile),
				Load_ObstaclesToCache(diffFile),
			};
			//Wait for meee
			await Task.WhenAll(Loaders);
			//Push the mod requirements early
			report.modsRequired = ((Task<List<string>>)Loaders[0]).Result;
			//Remove all notes outside the 3x4 range
			Task[] Cullers = new Task[] {
				Eval_OutOfRangeNotes(),
				Eval_OutOfRangeWalls()
			};
			//Wait for the tasks to finish
			await Task.WhenAll(Cullers);
			//Load the Swings per second for each hand
			report.LeftHandSwings = LeftHandSwings;
			report.RightHandSwings = RightHandSwings;

			report.note_OutOfRange = ((Task<List<json_MapNote>>)Cullers[0]).Result;
			report.wall_OutOfRange = ((Task<List<json_MapObstacle>>)Cullers[1]).Result;

			UserConsole.Log($"[{bsr}]: Evaluating map..");
			
			//I really. Really. Hate this. but C# loves cockblocking the thread if something goes wrong
			//and I dont actively know of any (try catch multiple) block that continues 

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

		/// <summary>Clears the stored memory from this MapStorageLayout</summary>
		public void ClearDiff(bool callGC=true) {
			report.ClearCache();
			noteCache.Clear();
			wallCache.Clear();
			noteCache = null;
			wallCache = null;
			diffFile._notes = null;
			diffFile._walls = null;
			LeftHandSwings = null;
			RightHandSwings = null;
			mapDiff = null;
			if(callGC)
				GC.Collect(); //LMAO I FUCKING HATE C# WHYY
		}

		/// <summary>
		/// Loads all notes from <see cref="json_DiffFileV2"/> to <see cref="noteCache"/>.
		/// </summary>
		/// <param name="diff">The diffFile intermediate</param>
		public Task Load_NotesToCache(json_DiffFileV2 diff) {
			//Roud up how many seconds there are in the audio for swings/second
			int cellCount = (int)Math.Ceiling(audioLength);
			
			//lefty gang btw
			LeftHandSwings = new int[cellCount];
			RightHandSwings = new int[cellCount];

			int noteCount = 0;
			foreach(var note in diff._notes) {
				note.cellIndex = 4 * note.yPos + note.xPos;
				note.realTime = note._time * beatsPerSecond;
				if(note._type != NoteType.Bomb) {
					//get curent cell index
					int index = (int)Math.Floor(note.realTime);

					//Add swing to current swings/second cell
					if(note._type == NoteType.Left) {
						LeftHandSwings[index]++;
					} else {
						RightHandSwings[index]++;
					}

					noteCount++;
				}

				if(!noteCache.ContainsKey(note._time)) {
					var push = new List<json_MapNote>(){note};
					noteCache.Add(note._time, push);
				} else {
					noteCache[note._time].Add(note);
				}
			}
			//Calculate the overall notesPerSecond
			notesPerSecond = noteCount / audioLength;
			return Task.CompletedTask;
		}
		/// <summary>
		/// Loads all walls from <see cref="json_DiffFileV2"/> to <see cref="wallCache"/>.
		/// </summary>
		/// <param name="diff">The diffFile intermediate</param>
		public Task Load_ObstaclesToCache(json_DiffFileV2 diff) {
			//Smallest acceptable time for walls
			const float shortWallEpsilon = 1.0f / 72.0f;
			foreach(var wall in diff._walls) {
				wall.isInteractive = wall.xPos == 1 || wall.xPos == 2;
				wall.realTime = wall._time * beatsPerSecond;
				wall.endTime = wall._time + wall._duration;
				wall.isShort = wall.realTime < shortWallEpsilon;
				
				//No wall here? add it
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
		/// <summary>
		/// Esoteric implementation of looking for json "_requirements"
		/// and checking if anything is in it
		/// </summary>
		/// <returns>List of mods in requirements</returns>
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
		/// <summary>
		/// Finds notes that sit before the starting blank period. AKA "Hot start"
		/// </summary>
		/// <param name="limit">hot start time in seconds</param>
		/// <returns>List of offenders</returns>
		public Task<List<json_MapNote>> Eval_NoteHotStart(float limit) {
			List<json_MapNote> offenders = new List<json_MapNote>();
			//Get the limit beat
			float beatLimit = limit * beatsPerSecond;
			foreach(var (time, list) in noteCache) { 
				if(time < beatLimit)
					offenders.AddRange(list);
				//If the limit has passed just skip
				else break;
			}
			return Task.FromResult(offenders);
		}
		/// <summary>
		/// Finds Walls that sit before the starting blank period. AKA "Hot start"
		/// </summary>
		/// <param name="limit">hot start time in seconds</param>
		/// <returns>List of offenders</returns>
		public Task<List<json_MapObstacle>> Eval_WallHotStart(float limit) {
			List<json_MapObstacle> offenders = new List<json_MapObstacle>();
			//Get the limit beat
			float beatLimit = limit * beatsPerSecond;

			foreach(var (time, list) in wallCache) { 
				if(time < beatLimit) { 
					foreach(var wall in list) {
						//Only add to offender list if its interactive
						if(wall.isInteractive)
							offenders.Add(wall);
					}
				}
				//If the limit has passed just skip
				else break;
			}
			return Task.FromResult(offenders);
		}
		//R1:g
		/// <summary>
		/// Finds notes that exist past the given timeout period at the end, AKA "Cold Ends"
		/// </summary>
		/// <param name="limit">cold end time in seconds</param>
		/// <returns>List of offenders</returns>
		public Task<List<json_MapNote>> Eval_NoteColdEnd(float limit) {
			List<json_MapNote> offenders = new List<json_MapNote>();
			//Get the ending beat
			float kernel = (audioLength - limit) / beatsPerSecond;
			//Reverse the order of the noteCache with a stack
			Stack<float> time_rev = new Stack<float>(noteCache.Keys);
			foreach(var time in time_rev) {
				if(time >= kernel)
					offenders.AddRange(noteCache[time]);
				//If the limit has passed just skip
				else break;
			}
			return Task.FromResult(offenders);
		}
		/// <summary>
		/// Finds Walls that exist past the given timeout period at the end, AKA "Cold Ends"
		/// </summary>
		/// <param name="limit">cold end time in seconds</param>
		/// <returns>List of offenders</returns>
		public Task<List<json_MapObstacle>> Eval_WallColdEnd(float limit) {
			List<json_MapObstacle> offenders = new List<json_MapObstacle>();
			//Get the ending beat
			float kernel = (audioLength - limit) / beatsPerSecond;
			//Reverse the order of the wallCache with a stack
			Stack<float> time_rev = new Stack<float>(wallCache.Keys);

			//Add only interactive walls
			foreach(var time in time_rev) { 
				if(time >= kernel) { 
					foreach(var wall in wallCache[time]) {
						if(wall.isInteractive) {
							offenders.Add(wall);
						}
					}
				}
				//If the limit has passed just skip
				else break;       
			}
			return Task.FromResult(offenders);
		}
		//R3:a
		/// <summary>
		/// Finds notes that share the same cell on the same time step
		/// </summary>
		/// <returns>A list of offenders</returns>
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
		/// <summary>
		/// Finds walls that have notes or other walls inside of them
		/// </summary>
		/// <returns>A list of offenders</returns>
		public Task<List<json_MapObstacle>> Eval_WallIntersections() {
			List<json_MapObstacle> offenders = new List<json_MapObstacle>();

			foreach(var (time, list) in wallCache) { 
				foreach(json_MapObstacle wall in list) {
					//Find all note timesteps that are within the walls time domain
					var look = noteCache.Where(note => (note.Key >= time && note.Key <= wall.endTime));
					
					//Skip if no notes in range of the walls range
                    if(look.Count() == 0)
						continue;

					//Get wall info
					int wx = wall.xPos;
					int wSpan = wx + (wall._width - 1);
					bool isFull = wall._type == ObstacleType.FullWall;
					//Simple note inside wall check for every note thats in range
					foreach(var (noteKey, noteList) in look) {
						foreach(json_MapNote note in noteList) {
							int nx = note.xPos;
							int ny = note.yPos;
							bool inRangeX = (nx >= wx) && (nx <= wSpan);
							bool underWall = (!isFull && ny == 0);
							if(inRangeX && !underWall) {
                                offenders.Add(wall);
                            }
                        }
					}
				}
			}
			return Task.FromResult(offenders);
		}

		//R4:cd
		/// <summary>
		/// Removes all notes that have a position outside of the beat saber grid
		/// </summary>
		/// <returns>A list of offenders</returns>
		public Task<List<json_MapNote>> Eval_OutOfRangeNotes() {
			List<json_MapNote> offenders = new List<json_MapNote>();

			//Tomb array because foreach has to iterate over the array
			List<float> globalTomb = new List<float>();
			foreach(var (time, list) in noteCache) {
				//Local timestep tomb
				List<json_MapNote> tomb = new List<json_MapNote>();
				foreach(var note in list) {
					//Add all notes outside of the 3x4 space to the tomb list
					bool xBound = note.xPos < 0 || note.xPos > 3;
					bool yBound = note.yPos < 0 || note.yPos > 2;
					if(xBound || yBound)
						tomb.Add(note);
				}
				//Add all tomb items to the offenders registry
				offenders.AddRange(tomb);

				//Remove all notes outside of the 3x4 grid for this timestep
				foreach(var target in tomb)
					list.Remove(target);

				//Remove the timestep if there arent anymore blocks
				if(list.Count == 0)
					globalTomb.Add(time);
			}
			//Remove all empty timesteps
			foreach(float target in globalTomb)
				noteCache.Remove(target);
			return Task.FromResult(offenders);
		}
		/// <summary>
		/// Removes all walls that have a position outside of the beat saber grid
		/// </summary>
		/// <returns>A list of offenders</returns>
		public Task<List<json_MapObstacle>> Eval_OutOfRangeWalls() {
			List<json_MapObstacle> offenders = new List<json_MapObstacle>();

			//Tomb array because foreach has to iterate over the array
			List<float> globalTomb = new List<float>();
			foreach(var (time, list) in wallCache) {
				//Local timestep tomb
				List<json_MapObstacle> tomb = new List<json_MapObstacle>();
				foreach(var wall in list) {
					int wx = wall.xPos;
					int wSpan = wx + (wall._width - 1);

					bool ZeroWall = Utils.Approx(wall._duration, 0f, 0.001f);
					bool negativeWidth = wall._width < 0f;
					bool negativeDuration = wall._duration < 0f;

					bool outOfRange = wx < 0 || wx > 3 || wSpan > 3;
					bool invalid = ZeroWall || negativeWidth || negativeDuration;
					if(outOfRange || invalid)
						tomb.Add(wall);
				}
				//Add all tomb items to the offenders registry
				offenders.AddRange(tomb);

				//Remove all walls outside of the 3x4 grid for this timestep
				foreach(var target in tomb)
					list.Remove(target);

				//Remove the timestep if there arent anymore walls
				if(list.Count == 0)
					globalTomb.Add(time);
			}
			//Remove all empty timesteps
			foreach(var target in globalTomb)
				wallCache.Remove(target);

			return Task.FromResult(offenders);
		}
		//R3:e, R5:a
		/// <summary>
		/// Finds patterns that are only made by a prick, 
		/// for instance a bomb on the cut outward side of a block
		/// </summary>
		/// <returns>A list of offenders</returns>
		public Task<List<json_MapNote>> Eval_FailSwings() {
			List<json_MapNote> offenders = new List<json_MapNote>();

			foreach(var (time, list) in noteCache) { 
				foreach(var note in list) { 
					if(note._type != NoteType.Bomb) {
						//Check the cut directions cell
						var next = Utils.GetAdjacentNote(list, note, note._dir);
						//not empty and not the same handedness
						if(next != null && next._type != note._type)
							offenders.Add(next);
					}
				}
			}

			return Task.FromResult(offenders);
		}
	}
}
