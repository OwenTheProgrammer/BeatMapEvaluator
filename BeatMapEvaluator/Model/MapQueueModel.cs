using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BeatMapEvaluator.Model
{
    public class MapQueueModel {
        public MapDiffs diffsAvailable { get; set; }
        public string? mapID { get; set; }

        public BitmapImage? MapProfile { get; set; }
        public string MapSongName { get; set; }
        public string MapSongSubName { get; set; }
        public string MapAuthors { get; set; }
        public Brush EvalColor { get; set; }

        public MapQueueModel() {
            MapProfile = null;
            MapSongName = "Loading";
            MapSongSubName = "";
            MapAuthors = "";
            EvalColor = Brushes.Gray;
        }
    }
}