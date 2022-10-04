﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;

namespace BeatMapEvaluator.Model
{
    public class MapQueueModel {
        public BitmapImage MapProfile { get; set; }
        public string MapSongName { get; set; }
        public string MapSongSubName { get; set; }
        public string MapAuthors { get; set; }

        public MapQueueModel() {
            MapProfile = new BitmapImage();
            MapSongName = "Loading";
            MapSongSubName = "";
            MapAuthors = "";
        }
    }
}