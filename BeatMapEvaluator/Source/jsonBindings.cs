using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatMapEvaluator
{
    #region Info.dat json objects
    //Based off: https://bsmg.wiki/mapping/map-format.html#info-dat
    internal class json_MapInfo {
        public string FileContextPath;
        public string? _version { get; set; }
        
        public string? _songName { get; set; }
        public string? _songSubName { get; set; }
        public string? _songAuthorName { get; set; }

        public string? _levelAuthorName { get; set; }

        public string? _songFilename { get; set; }
        public string? _coverImageFilename { get; set; }
        public float _songTimeOffset { get; set; }

        public json_beatMapSet[]? _difficultyBeatmapSets { get; set; }
    }

    //Based off: https://bsmg.wiki/mapping/map-format.html#difficulty-beatmap-sets
    internal class json_beatMapSet {
        public string? _beatmapCharacteristicName { get; set; }
        public json_beatMapDifficulty[]? _difficultyBeatmaps { get; set; }
    }

    //Based off: https://bsmg.wiki/mapping/map-format.html#difficulty-beatmaps
    internal class json_beatMapDifficulty {
        public string? _difficulty { get; set; }
        public int _difficultyRank { get; set; }
        public string? _beatmapFilename { get; set; }
        public float _noteJumpMovementSpeed { get; set; }
        public float _noteJumpStartBeatOffset { get; set; }
        public dynamic? _customData { get; set; }
    }
    #endregion //Info.dat
}
