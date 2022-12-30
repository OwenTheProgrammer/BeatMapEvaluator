using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BeatMapEvaluator.Model
{
    /// <summary>A "map queue" element model AKA QueuedMap.xaml</summary>
    public class MapQueueModel {
        //All difficulties available
        public MapDiffs diffsAvailable { get; set; }
        //the BSR
        public string? mapID { get; set; }

        //The profile image
        public BitmapImage? MapProfile { get; set; }
        public string MapSongName { get; set; }
        public string MapSongSubName { get; set; }
        public string MapAuthors { get; set; }
        //The resulting evaluation colour
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