using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatMapEvaluator
{
    #region Info.dat json objects

    [Flags] public enum MapDiffs {
        NONE = 0,
        Easy = 1<<0, Normal = 1<<1,
        Hard = 1<<2, Expert = 1<<3,
        ExpertPlus = 1<<4
    };

    //Based off: https://bsmg.wiki/mapping/map-format.html#info-dat
    public class json_MapInfo {
        public MapDiffs mapDifficulties;
        public string? mapContextDir;
        public string? songFilePath;

        public string? _version { get; set; }
        
        public string? _songName { get; set; }
        public string? _songSubName { get; set; }
        public string? _songAuthorName { get; set; }

        public string? _levelAuthorName { get; set; }
        [JsonProperty("_beatsPerMinute")]
        public float _bpm { get; set; }

        public string? _songFilename { get; set; }
        public string? _coverImageFilename { get; set; }
        public float _songTimeOffset { get; set; }

        [JsonProperty("_difficultyBeatmapSets")]
        public json_beatMapSet[]? beatmapSets { get; set; }

        public json_beatMapSet? standardBeatmap;
        public string? mapBSR;
    }

    //Based off: https://bsmg.wiki/mapping/map-format.html#difficulty-beatmap-sets
    public class json_beatMapSet {
        [JsonProperty("_beatmapCharacteristicName")]
        public string? _mapType { get; set; }
        [JsonProperty("_difficultyBeatmaps")]
        public json_beatMapDifficulty[]? _diffMaps { get; set; }
    }

    //Based off: https://bsmg.wiki/mapping/map-format.html#difficulty-beatmaps
    public class json_beatMapDifficulty {
        public string? _difficulty { get; set; }
        public int _difficultyRank { get; set; }
        public string? _beatmapFilename { get; set; }
        [JsonProperty("_noteJumpMovementSpeed")]
        public float _njs { get; set; }
        [JsonProperty("_noteJumpStartBeatOffset")]
        public float _noteOffset { get; set; }
        public dynamic? _customData { get; set; }

        public float bpm;
    }
    #endregion //Info.dat

    #region Diff V2 objects

    public enum NoteType {
        Left=0,Right=1,Bomb=3
    };
    public enum NoteCutDirection {
        Up=0, Down=1, Left=2, Right=3,
        UpLeft=4, UpRight=5,
        DownLeft=6, DownRight=7,
        DotNote=8
    };

    //Based off: https://bsmg.wiki/mapping/map-format.html#difficulty-file-v2
    public class json_DiffFileV2 {
        public string? _version { get; set; }
        public json_MapNote[]? _notes { get; set; }
        [JsonProperty("_obstacles")]
        public json_MapObstacle[]? _walls { get; set; }

        public int noteCount;
        public int obstacleCount;
    }

    //Based off: https://bsmg.wiki/mapping/map-format.html#notes-1
    public class json_MapNote {
        public float _time { get; set; }
        [JsonProperty("_lineIndex")]
        public int xPos { get; set; }
        [JsonProperty("_lineLayer")]
        public int yPos { get; set; }
        public NoteType _type { get; set; }
        [JsonProperty("_cutDirection")]
        public NoteCutDirection _dir { get; set; }

        public int cellIndex;   //Index to the grid
        public float realTime;  //Time in seconds
    }

    public enum ObstacleType {
        FullWall=0,CrouchWall=1
    };
    //Based off: https://bsmg.wiki/mapping/map-format.html#obstacles-3
    public class json_MapObstacle {
        public float _time { get; set; }
        [JsonProperty("_lineIndex")]
        public int xPos { get; set; }
        public ObstacleType _type { get; set; }
        public float _duration { get; set; }
        public int _width { get; set; }
        
        public bool isInteractive;
        public bool isShort;        //Less than 13.8ms
        public float realTime;      //The time in seconds
        public float endTime;       //The end point of a wall in beats
    }

    #endregion
}
