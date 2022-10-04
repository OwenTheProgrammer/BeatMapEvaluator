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

namespace BeatMapEvaluator
{
    internal class FileInterface {
        private static readonly string beatSaverAPI = "https://beatsaver.com/api/download/key/";
        private static readonly HttpClient webClient = new HttpClient();

        public static void UnzipMap(string bsr, string targetDir) {
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
                UserConsole.Log($"Error: {err.Message}");
                throw;
            }
            UnzipMap(bsr, targetDir);
        }
        public static Task<json_MapInfo> ParseInfoFile(string mapDirectory) {
            string infoFilePath = Path.Combine(mapDirectory, "Info.dat");
            if(!File.Exists(infoFilePath)) {
                UserConsole.Log("Error: Failed to find Info.dat file.");
                throw new FileNotFoundException();
            }

            UserConsole.Log("Reading \'Info.dat\' ..");
            string infoFileData = File.ReadAllText(infoFilePath);
            var infoFile = JsonConvert.DeserializeObject<json_MapInfo>(infoFileData);
            infoFile.mapContextDir = mapDirectory;

            //Find the standard beatmap (I hate the length)
            if(infoFile.beatmapSets != null) {
                infoFile.standardBeatmap =
                infoFile.beatmapSets.Where(set =>
                        set._mapType.Equals("Standard"))
                        .First();
            } else {
                UserConsole.Log($"{infoFile._songName} has no maps.");
            }
            if(!infoFile.standardBeatmap._mapType.Equals("Standard")) {
                UserConsole.Log($"{infoFile._songName} has no standard map.");
            }

            UserConsole.Log("Parsed \'Info.dat\'.");
            return Task.FromResult(infoFile);
        }
        public static Task<MapStorageLayout> InterpretMapFile(string mapDiffPath) {
            string fileData = File.ReadAllText(mapDiffPath);
            json_DiffFileV2 diff = JsonConvert.DeserializeObject<json_DiffFileV2>(fileData);
            diff.noteCount = diff._notes.Length;
            diff.obstacleCount = diff._obstacles.Length;

            UserConsole.Log("Loading map data to table..");
            //Parallelization
            Task[] LoaderTasks = new Task[] {
                EvalLogic.LoadNotesToTable(diff),
                EvalLogic.LoadObstaclesToTable(diff)
            };
            try {
                Task.WaitAll(LoaderTasks);
            } catch(AggregateException ae) {
                UserConsole.Log("Error: Problem parsing the diff file.");
                throw ae.Flatten();
            }
            MapStorageLayout MapData = new MapStorageLayout();
            MapData.noteCache = ((Task<Dictionary<float, List<json_MapNote>>>)LoaderTasks[0]).Result;
            MapData.obstacleCache = ((Task<Dictionary<float, List<json_MapObstacle>>>)LoaderTasks[1]).Result;
            MapData.noteKeys = new List<float>(MapData.noteCache.Keys).ToArray();
            MapData.obstacleKeys = new List<float>(MapData.obstacleCache.Keys).ToArray();
            return Task.FromResult(MapData);
        }

        public static MapQueueModel CreateMapListItem(json_MapInfo info) {
            MapQueueModel DisplayItem = new MapQueueModel();
            string ImagePath = "";

            if(info._coverImageFilename != null) {
                ImagePath = Path.Combine(info.mapContextDir, info._coverImageFilename);
                DisplayItem.MapProfile.BeginInit();
                DisplayItem.MapProfile.CacheOption = BitmapCacheOption.OnLoad;
                DisplayItem.MapProfile.UriSource = new Uri(ImagePath);
                DisplayItem.MapProfile.EndInit();
            } else {
                UserConsole.Log($"Failed to find profile for map {info._songName}");
            }

            DisplayItem.MapSongName = info._songName ?? "<NO SONG>";
            DisplayItem.MapSongSubName = info._songSubName ?? "";
            DisplayItem.MapAuthors = info._levelAuthorName ?? "<NO AUTHOR>";
            return DisplayItem;
        }


        public static void DeleteDir_Full(string dirPath) {
            DirectoryInfo info = new DirectoryInfo(dirPath);
            try {
                foreach(FileInfo file in info.GetFiles())
                    file.Delete();
                foreach(DirectoryInfo dir in info.GetDirectories())
                    dir.Delete(true);
                Directory.Delete(dirPath);
            } catch(Exception err) {
                UserConsole.Log($"Error: {err.Message}");
                throw;
            }
            UserConsole.Log("Removed temp directory.");
        }
    }
}
