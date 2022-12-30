using System;
using System.IO;
using System.IO.Compression;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json; //https://tenor.com/bD1IM.gif
using BeatMapEvaluator.Model;

namespace BeatMapEvaluator
{
    /// <summary>All File/Web interface logic</summary>
    internal class FileInterface {
        //The beatsaver API base link
        private static readonly string beatSaverAPI = "https://beatsaver.com/api/download/key/";
        //A standard HttpClient instance
        private static readonly HttpClient webClient = new HttpClient();

        /// <summary>
        /// Unfold the binary origami that is the zip archive into its natural habitat
        /// </summary>
        /// <remarks>
        /// <c>targetDir is the root folder that the zip will make a folder inside.
        /// <example>
        /// extracts here: "targetDir/bsr/"
        /// </example>
        /// </c>
        /// </remarks>
        /// <param name="bsr">The BSR code</param>
        /// <param name="targetDir">The output folder</param>
        /// <returns></returns>
        public static Task UnzipMap(string bsr, string targetDir) {
            string zipFileName = bsr + ".zip";
            string zipPath = Path.Combine(targetDir, zipFileName);
            string extractDir = Path.Combine(targetDir, bsr);
            UserConsole.Log("Unzipping map..");

            //Extract zip and delete it.
            try {
                ZipFile.ExtractToDirectory(zipPath, extractDir);
                File.Delete(zipPath);
            } catch (Exception err) {
                UserConsole.Log($"Error: {err.Message}");
                throw;
            }
            UserConsole.Log("Map unzipped.");
            return Task.CompletedTask;
        }

        //I heard this is a good spot
        private static void TakeOutToDinner() { return; }

        /// <summary>
        /// Contacts the authorities for them to hand deliver your BSR zip file :bcmok2:
        /// </summary>
        /// <param name="bsr">The BSR code</param>
        /// <param name="targetDir">The download directory</param>
        /// <returns>Nofin</returns>
        public static async Task DownloadBSR(string bsr, string targetDir) {
            string zipFileName = bsr + ".zip";
            string zipPath = Path.Combine(targetDir, zipFileName);
            Uri urlHandle = new Uri(beatSaverAPI + bsr);
            UserConsole.Log($"Fetching map {bsr}..");
            try {
                //Download the urlHandle async
                var res = await webClient.GetAsync(urlHandle);
                res.EnsureSuccessStatusCode();
                using(var stream = new FileStream(zipPath, FileMode.CreateNew)) {
                    await res.Content.CopyToAsync(stream);
                    await stream.FlushAsync();
                    stream.Close();
                }
            } catch(Exception err) {
                UserConsole.LogError($"Error: {err.Message}");
                throw;
            }
            //Unzip the zip file, damn. At least you took it out to dinner first!
            TakeOutToDinner();
            await UnzipMap(bsr, targetDir);
        }

        /// <summary>
        /// Reads a downloaded BSR map folder and returns a <see cref="json_MapInfo">MapInfo Object</see>
        /// </summary>
        /// <param name="mapDirectory">The parent folder of the BSR folder</param>
        /// <param name="bsr">The name of the folder (most likely the bsr)</param>
        /// <returns></returns>
        public static async Task<json_MapInfo?> ParseInfoFile(string mapDirectory, string bsr) {
            //Create a path string to "Info.dat"
            string infoFilePath = Path.Combine(mapDirectory, "Info.dat");
            if (!File.Exists(infoFilePath)) {
                //Test older lower-case just incase
                infoFilePath = Path.Combine(mapDirectory, "info.dat");
                if(!File.Exists(infoFilePath)) { 
                    UserConsole.LogError($"[{bsr}] Error: Failed to find Info.dat file.");
                    return null;
                }
            }

            UserConsole.Log($"[{bsr}]: Reading \'Info.dat\' ..");
            string infoFileData = File.ReadAllText(infoFilePath);

            //Read the filedata as json
            json_MapInfo infoFile = JsonConvert.DeserializeObject<json_MapInfo>(infoFileData);
            //var loadTask = Task.Run(() => JsonConvert.DeserializeObject<json_MapInfo>(infoFileData));
            //json_MapInfo infoFile = await loadTask;
            infoFile.mapBSR = bsr;
            infoFile.songFilePath = Path.Combine(mapDirectory, infoFile._songFilename);
            infoFile.mapContextDir = mapDirectory;

            //Find the standard beatmap
            if(infoFile.beatmapSets != null) {
                foreach(var set in infoFile.beatmapSets) { 
                    if(set._mapType.Equals("Standard")) {
                        infoFile.standardBeatmap = set;
                    }
                }
            }
            //If no beatmapSets or couldnt find the "standard" beatmap
            if(infoFile.beatmapSets == null || 
               infoFile.standardBeatmap == null) {
                UserConsole.Log($"[{bsr}]: {infoFile._songName} has no bitches.");
                return infoFile;
            }

            infoFile.mapDifficulties = Utils.GetMapDifficulties(infoFile.standardBeatmap._diffMaps);

            UserConsole.Log($"[{bsr}]: Parsed \'Info.dat\'.");
            return infoFile;
        }

        /// <summary>
        /// Reads a maps difficulty ".dat" file as a <see cref="MapStorageLayout">MapStorageLayout</see>
        /// </summary>
        /// <param name="info">The map info</param>
        /// <param name="diffIndex">The beatMapSet's index</param>
        /// <returns>A MapStorageLayout object for evaluation capabilites</returns>
        public static async Task<MapStorageLayout?> InterpretMapFile(json_MapInfo info, int diffIndex) {
            string mapFileName = info.standardBeatmap._diffMaps[diffIndex]._beatmapFilename;
            string fileData = File.ReadAllText(Path.Combine(info.mapContextDir, mapFileName));
            if(fileData == null) {
                UserConsole.LogError($"[{info.mapBSR}]: Error no map file data.");
                return null;
            }

            //Read the filedata as json
            //json_DiffFileV2? diff = JsonConvert.DeserializeObject<json_DiffFileV2>(fileData);
            var rawRead = Task.Run(()=>JsonConvert.DeserializeObject<json_DiffFileV2>(fileData));
            json_DiffFileV2? diff = await rawRead;


            //In case it still fills null for some reason
            if (diff == null || diff._notes == null) {
                UserConsole.LogError($"[{info.mapBSR}]: Failed to parse file JSON.");
                return null;
            }
            //In case of unsupported versions
            if(diff._version == null || diff._version.StartsWith("3.")) {
                UserConsole.LogError($"[{info.mapBSR}]: Version 3 not supported.");
                return null;
            }
            diff.noteCount = diff._notes.Length;
            diff.obstacleCount = diff._walls.Length;

            UserConsole.Log($"[{info.mapBSR}]: Loading map data to table..");
            MapStorageLayout MapData = new MapStorageLayout(info, diff, diffIndex);
            return MapData;
        }


        /// <summary>
        /// Builds a MapQueue item.
        /// </summary>
        /// <param name="info">The map info</param>
        /// <param name="sts">The evaluation status</param>
        /// <returns>The MapQueue item</returns>
        public static async Task<MapQueueModel> CreateMapListItem(json_MapInfo info, ReportStatus sts) {
            MapQueueModel DisplayItem = new MapQueueModel();
            string ImagePath = "";

            DisplayItem.diffsAvailable = info.mapDifficulties;
            DisplayItem.mapID = info.mapBSR;
            if(info._coverImageFilename != null) {
                ImagePath = Path.Combine(info.mapContextDir, info._coverImageFilename);
                try {
                    //Build the image file
                    DisplayItem.MapProfile = await BuildImage(ImagePath);
                } catch {
                    UserConsole.LogError($"Failed to load: \"{info._coverImageFilename}\"");
                    DisplayItem.MapProfile = new BitmapImage();
                }
            } else {
                UserConsole.LogError($"Failed to find profile for map {info._songName}");
            }

            //get da evaluation colour
            var conv = new BrushConverter();
            string diffHex = DiffCriteriaReport.diffColors[(int)sts];

            //Build the Queue item with the available info
            DisplayItem.MapSongName = info._songName ?? "<NO SONG>";
            DisplayItem.MapSongSubName = info._songSubName ?? "";
            DisplayItem.MapAuthors = info._levelAuthorName ?? "<NO AUTHOR>";
            DisplayItem.EvalColor = (Brush)conv.ConvertFromString(diffHex);
            return DisplayItem;
        }
        /// <summary>
        /// Loads a map profile image
        /// </summary>
        /// <remarks>
        /// <c>Note: </c>Loading is done with RAM cache so you can close without having memory handles
        /// </remarks>
        /// <param name="path">The profile image path</param>
        /// <returns>Profile BitmapImage</returns>
        private static async Task<BitmapImage> BuildImage(string path) {
            BitmapImage img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.UriSource = new Uri(path);
            img.EndInit();
            return img;
        }


        /// <summary>
        /// Deletes a directory recursively.
        /// </summary>
        /// <param name="dirPath">The directory path.. no way.</param>
        public static void DeleteDir_Full(string dirPath) {
            DirectoryInfo info = new DirectoryInfo(dirPath);
            try {
                //Delete files recursive
                foreach(FileInfo file in info.GetFiles())
                    file.Delete();
                //Delete subfolders
                foreach(DirectoryInfo dir in info.GetDirectories())
                    dir.Delete(true);
                //Delete directory
                Directory.Delete(dirPath);
            } catch(Exception err) {
                UserConsole.LogError($"Error: {err.Message}");
                throw;
            }
            UserConsole.Log("Removed temp directory.");
        }
    }
}
