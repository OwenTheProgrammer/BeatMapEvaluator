using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json; // :)
using BeatMapEvaluator.Model;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace BeatMapEvaluator
{
    internal class FileInterface {
        private static readonly string beatSaverAPI = "https://beatsaver.com/api/download/key/";
        private static readonly HttpClient webClient = new HttpClient();

        public static Task UnzipMap(string bsr, string targetDir) {
            string zipFileName = bsr + ".zip";
            string zipPath = Path.Combine(targetDir, zipFileName);
            string extractDir = Path.Combine(targetDir, bsr);
            UserConsole.Log("Unzipping map..");

            //Extract zip and delete it.
            try {
                ZipFile.ExtractToDirectory(zipPath, extractDir);
                File.Delete(zipPath);
            } catch(Exception err) {
                UserConsole.Log($"Error: {err.Message}");
                throw;
            }
            UserConsole.Log("Map unzipped.");
            return Task.CompletedTask;
        }
        public static async Task DownloadBSR(string bsr, string targetDir) {
            string zipFileName = bsr + ".zip";
            string zipPath = Path.Combine(targetDir, zipFileName);
            Uri urlHandle = new Uri(beatSaverAPI + bsr);
            UserConsole.Log($"Fetching map {bsr}..");
            try {
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
            await UnzipMap(bsr, targetDir);
        }
        public static async Task<json_MapInfo?> ParseInfoFile(string mapDirectory, string bsr) {
            string infoFilePath = Path.Combine(mapDirectory, "Info.dat");
            if(!File.Exists(infoFilePath)) {
                UserConsole.LogError($"[{bsr}] Error: Failed to find Info.dat file.");
                return null;
            }

            UserConsole.Log($"[{bsr}]: Reading \'Info.dat\' ..");
            string infoFileData = File.ReadAllText(infoFilePath);

            var loadTask = Task.Run(() => JsonConvert.DeserializeObject<json_MapInfo>(infoFileData));
            json_MapInfo infoFile = await loadTask;
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
            if(infoFile.beatmapSets == null || 
               infoFile.standardBeatmap == null) {
                UserConsole.Log($"[{bsr}]: {infoFile._songName} has no maps.");
                return infoFile;
            }

            infoFile.mapDifficulties = Utils.GetMapDifficulties(infoFile.standardBeatmap._diffMaps);

            UserConsole.Log($"[{bsr}]: Parsed \'Info.dat\'.");
            return infoFile;
        }
        public static async Task<MapStorageLayout?> InterpretMapFile(json_MapInfo info, int diffIndex) {
            string mapFileName = info.standardBeatmap._diffMaps[diffIndex]._beatmapFilename;
            string fileData = File.ReadAllText(Path.Combine(info.mapContextDir, mapFileName));
            if(fileData == null) {
                UserConsole.LogError($"[{info.mapBSR}]: Error no map file data.");
                return null;
            }

            var rawRead = Task.Run(()=>JsonConvert.DeserializeObject<json_DiffFileV2>(fileData));
            json_DiffFileV2? diff = await rawRead;


            // In case it still fills null for some reason
            if(diff == null || diff._notes == null || diff._obstacles == null) {
                UserConsole.LogError($"[{info.mapBSR}]: Failed to parse file JSON.");
                return null;
            }
            if(diff._version.StartsWith("3.")) {
                UserConsole.LogError($"[{info.mapBSR}]: Version 3 not supported.");
                return null;
            }
            diff.noteCount = diff._notes.Length;
            diff.obstacleCount = diff._obstacles.Length;

            UserConsole.Log($"[{info.mapBSR}]: Loading map data to table..");
            MapStorageLayout MapData = new MapStorageLayout(info, diff, diffIndex);
            return MapData;
        }

        public static MapQueueModel CreateMapListItem(json_MapInfo info, int color) {
            MapQueueModel DisplayItem = new MapQueueModel();
            string ImagePath = "";

            DisplayItem.diffsAvailable = info.mapDifficulties;
            DisplayItem.mapID = info.mapBSR;
            if(info._coverImageFilename != null) {
                ImagePath = Path.Combine(info.mapContextDir, info._coverImageFilename);
                try {
                    //Build the image file
                    DisplayItem.MapProfile.BeginInit();
                    DisplayItem.MapProfile.CacheOption = BitmapCacheOption.OnLoad;
                    DisplayItem.MapProfile.UriSource = new Uri(ImagePath);
                    DisplayItem.MapProfile.EndInit();
                } catch {
                    UserConsole.LogError($"Failed to load: \"{info._coverImageFilename}\"");
                    DisplayItem.MapProfile = new BitmapImage();
                }
            } else {
                UserConsole.LogError($"Failed to find profile for map {info._songName}");
            }

            var conv = new BrushConverter();
            string diffHex = DiffCriteriaReport.diffColors[color];

            DisplayItem.MapSongName = info._songName ?? "<NO SONG>";
            DisplayItem.MapSongSubName = info._songSubName ?? "";
            DisplayItem.MapAuthors = info._levelAuthorName ?? "<NO AUTHOR>";
            DisplayItem.EvalColor = (Brush)conv.ConvertFromString(diffHex);
            return DisplayItem;
        }

        //Delete directory / recursive
        public static void DeleteDir_Full(string dirPath) {
            DirectoryInfo info = new DirectoryInfo(dirPath);
            try {
                foreach(FileInfo file in info.GetFiles())
                    file.Delete();
                foreach(DirectoryInfo dir in info.GetDirectories())
                    dir.Delete(true);
                Directory.Delete(dirPath);
            } catch(Exception err) {
                UserConsole.LogError($"Error: {err.Message}");
                throw;
            }
            UserConsole.Log("Removed temp directory.");
        }
    }
}
