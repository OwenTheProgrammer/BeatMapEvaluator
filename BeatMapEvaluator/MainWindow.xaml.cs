using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.IO;
using BeatMapEvaluator.Model;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using BeatMapEvaluator.Themes;
using System.Security.Policy;
using System.Runtime.Serialization;
using System.Net.WebSockets;
using System.IO;
using System.IO.Compression;

using WinForms = System.Windows.Forms;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Threading;

namespace BeatMapEvaluator
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<MapQueueModel> MapQueue;
        private Dictionary<string, (json_MapInfo, MapStorageLayout[])> evalStorage;

        private readonly char _ps = Path.DirectorySeparatorChar;
        private string appTemp;

        private string logFile;
        private string logPath;

        private string reportFolder;

        private int Work_Numerator; //Folder progress numerator
        private int Work_Denominator;   //Folder progress denominator

        public MainWindow() {
            InitializeComponent();
            MapQueue = new ObservableCollection<MapQueueModel>();
            evalStorage = new Dictionary<string, (json_MapInfo, MapStorageLayout[])>();
            QueueList.ItemsSource = MapQueue;
            UpdateDiffButtons(MapDiffs.NONE, null);

            string cd = Directory.GetCurrentDirectory();
            appTemp = Path.Combine(cd, "temp") + _ps;
            logPath = Path.Combine(cd, "logs") + _ps;
            reportFolder = Path.Combine(cd, "reports") + _ps;
            UserConsole.onConsoleUpdate = new UserConsole.updateStringGUI(updateUserLog);
            UserConsole.onLogUpdate = new UserConsole.updateStringGUI(WriteToLogFile);

            if(!Directory.Exists(logPath))
                Directory.CreateDirectory(logPath);

            if(!Directory.Exists(reportFolder))
                Directory.CreateDirectory(reportFolder);

            logFile = "log_" + DateTime.Now.ToString("hh_mm_ss") + ".txt";
            logPath = Path.Combine(logPath, logFile);
            
            if(Directory.Exists(appTemp))
                FileInterface.DeleteDir_Full(appTemp);

            UserConsole.Log($"tempDir: \"{appTemp}\"");
            UserConsole.Log($"logFile: \"{logFile}\"");
            Directory.CreateDirectory(appTemp);
        }

        private async Task evaluateMap(string mapFolder, string bsr) {
            if(evalStorage.ContainsKey(bsr)) {
                UserConsole.Log($"{bsr} already loaded.");
                return;
            }

            json_MapInfo info = await FileInterface.ParseInfoFile(mapFolder, bsr);
            if(info == null || info.standardBeatmap == null) {
                UserConsole.LogError($"[{bsr}]: \"{mapFolder}Info.dat\" failed to load.");
                return;
            }

            ReportStatus status = ReportStatus.None;
            if(info.mapDifficulties != MapDiffs.NONE) {
                json_beatMapDifficulty[] diffRegistry = info.standardBeatmap._diffMaps;
                if(diffRegistry == null)
                    return;
                if(!evalStorage.ContainsKey(info.mapBSR))
                    evalStorage.Add(info.mapBSR, (info, new MapStorageLayout[5]));

                for(int i = 0; i < diffRegistry.Length; i++) {
                    MapStorageLayout layout = await FileInterface.InterpretMapFile(info, i);
                    if(layout == null || layout.audioLength == -1.0f) {
                        UserConsole.LogError($"[{bsr}] Error loading diff");
                        MapDiffs currentDiff = (MapDiffs)(~(1<<i));
                        info.mapDifficulties &= currentDiff;
                        continue;
                    }
                    try {
                        await layout.ProcessDiffRegistery();
                    } catch(Exception err) {
                        UserConsole.LogError($"[{bsr}] Error: {err.Message}");
                    }
                    int index = layout.mapDiff._difficultyRank / 2;
                    evalStorage[info.mapBSR].Item2[index] = layout;

                    if(layout.reportStatus != ReportStatus.Passed) {
                        string reportPath = Path.Combine(reportFolder, info.mapBSR + ".txt");
                        await UserConsole.ExportReport(layout, info, reportPath);
                    }
                    if(status == ReportStatus.None) { 
                        if(layout.reportStatus == ReportStatus.Error) 
                            status = ReportStatus.Error;
                        if(layout.reportStatus == ReportStatus.Failed)
                            status = ReportStatus.Failed;
                    }
                    layout.ClearDiff();
                }

                info.beatmapSets = null;
                info.standardBeatmap = null;
                evalStorage[info.mapBSR].Item1.beatmapSets = null;
                evalStorage[info.mapBSR].Item1.standardBeatmap = null;
            }
            if(status == ReportStatus.None)
                status = ReportStatus.Passed;
            UserConsole.Log($"[{bsr}]: Map loaded.");
            MapQueue.Add(await FileInterface.CreateMapListItem(info, status));
            Work_Numerator++;
            folderPerc.Content = Work_Numerator.ToString() + " / " + Work_Denominator.ToString();
        }

        //At the moment the user can press the button as many
        //times as they want... das bad

        private async void evaluateCode_OnClick(object sender, RoutedEventArgs e) {
            string bsr = bsrBox.Text;
            if(bsr == null || bsr.Equals("")) {
                UserConsole.LogError("BSR code null.");
                return;
            }
            if(Directory.Exists(Path.Combine(appTemp, bsr))) {
                UserConsole.Log($"{bsrBox.Text} has already been loaded.");
                return;
            }

            try {
                await FileInterface.DownloadBSR(bsr, appTemp);
            } catch {
                UserConsole.LogError($"Failed to download {bsr}");
                return;
            }
            string mapFolder = Path.Combine(appTemp, bsr + _ps);
            await evaluateMap(mapFolder, bsr);
        }
        private async void evaluateFolder_OnClick(object sender, RoutedEventArgs e) {
            //Request folder location
            var dialog = new WinForms.FolderBrowserDialog {
                Description = "Select folder containing maps",
                UseDescriptionForTitle = true,
                SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + _ps,
                ShowNewFolderButton = true
            };
            if(dialog.ShowDialog() == WinForms.DialogResult.OK) {
                string folderPath = dialog.SelectedPath + _ps;
                string[] maps = Directory.GetDirectories(folderPath);
                string[] files = Directory.GetFiles(folderPath);
                for(int i = 0; i < files.Length; i++) {
                    string f = files[i];
                    files[i] = f.Substring(f.LastIndexOf(_ps)+1);
                }
                Work_Numerator = 0;
                Work_Denominator = maps.Length;
                evalStorage.EnsureCapacity(evalStorage.Count + Work_Denominator);

                //If this is a map folder
                if(files.Contains("Info.dat")) {
                    string input = dialog.SelectedPath;
                    int cut = input.LastIndexOf(_ps) + 1;
                    string name = input.Substring(cut, input.Length-cut);
                    string bsr = Utils.ParseBSR(name);
                    await evaluateMap(folderPath, bsr);
                } else {


                    //Evaluate all zip files in folder
                    foreach(var file in files) {
                        if(file.EndsWith(".zip")) {
                            string location = Path.Combine(folderPath, file);
                            await ParseZipFile(location);
                        }
                    }
                    //Evaluate all folders inside selected folder
                    foreach(var dir in maps) {
                        string mapFolder = dir + _ps;
                        string name = Utils.ParseBSR(dir);
                        await evaluateMap(mapFolder, name);
                    }
                }
                Work_Numerator = 0;
                Work_Denominator = 0;
                folderPerc.Content = "";
            }
        }
        private async void evaluateZip_OnClick(object sender, RoutedEventArgs e) { 
            var dialog = new WinForms.OpenFileDialog {
                CheckFileExists = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + _ps,
                Filter = "Archives (*.zip)|*.zip"
            };
            if(dialog.ShowDialog() == WinForms.DialogResult.OK) {
                await ParseZipFile(dialog.FileName);
            }
        }

        private async Task ParseZipFile(string zipPath) {
            string bsr = Utils.ParseBSR(zipPath);
            if(evalStorage.ContainsKey(bsr)) {
                UserConsole.Log($"{bsr} already loaded.");
                return;
            }

            UserConsole.Log($"Loading \"{bsr}\"");
            try {
                string outDir = Path.Combine(appTemp, bsr);
                ZipFile.ExtractToDirectory(zipPath, outDir);
                //string copyPath = Path.Combine(appTemp, bsr + ".zip");
                //File.Copy(zipPath, copyPath);
                //await FileInterface.UnzipMap(bsr, appTemp);
            } catch {
                UserConsole.LogError($"[{bsr}]: Error extracting map.");
                return;
            }
            string mapFolder = Path.Combine(appTemp, bsr + _ps);
            await evaluateMap(mapFolder, bsr);
        }

        private async void QueueDrop_FileInput(object sender, DragEventArgs e) { 
            if(e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach(var drop in files) {
                    if(drop.EndsWith(".zip")) {
                        await ParseZipFile(drop);
                    }
                    string dropAsDir = drop + _ps;
                    if(Directory.Exists(dropAsDir)) {
                        await evaluateMap(dropAsDir, Utils.ParseBSR(drop));
                    }
                }
            }
        }

        private void diffButton_OnClick(object sender, RoutedEventArgs e) {
            Button button = sender as Button;
            int diffValue = int.Parse(button.Tag as string);
            MapDiffs diff = (MapDiffs)(1<<(diffValue / 2));
            //UserConsole.Log($"Selected diff: {diff}");

            string bsr = MapQueue[QueueList.SelectedIndex].mapID;
            UpdateMainPage(bsr, diffValue);
        }

        private void UpdateDiffButtons(MapDiffs avl, MapStorageLayout[]? layout) {
            //Dont @ me
            diffButton_Easy.Visibility = ShowIfFound(avl.HasFlag(MapDiffs.Easy));
            diffButton_Normal.Visibility = ShowIfFound(avl.HasFlag(MapDiffs.Normal));
            diffButton_Hard.Visibility = ShowIfFound(avl.HasFlag(MapDiffs.Hard));
            diffButton_Expert.Visibility = ShowIfFound(avl.HasFlag(MapDiffs.Expert));
            diffButton_ExpertPlus.Visibility = ShowIfFound(avl.HasFlag(MapDiffs.ExpertPlus));

            if(layout != null) {
                var converter = new BrushConverter();
                Brush[] colors = new Brush[5];
                for(int i = 0; i < 5; i++) {
                    if(layout[i] == null) continue;
                    string hex = DiffCriteriaReport.diffColors[(int)layout[i].reportStatus];
                    colors[i] = (Brush)converter.ConvertFromString(hex);
                }
                //Dont even think about it..
                if(diffButton_Easy.IsVisible)
                    diffButton_Easy.BorderBrush = colors[0];
                
                if(diffButton_Normal.IsVisible)
                    diffButton_Normal.BorderBrush = colors[1];
                
                if(diffButton_Hard.IsVisible)
                    diffButton_Hard.BorderBrush = colors[2];
                
                if(diffButton_Expert.IsVisible)
                    diffButton_Expert.BorderBrush = colors[3];
                
                if(diffButton_ExpertPlus.IsVisible)
                    diffButton_ExpertPlus.BorderBrush = colors[4];
            }
        }
        //Just fillin out some UI stuff
        private void UpdateMainPage(string bsr, int dValue) {
            json_MapInfo mapInfo = evalStorage[bsr].Item1;
            MapStorageLayout[] layouts = evalStorage[bsr].Item2;

            int sel = dValue / 2;
            DiffCriteriaReport report = layouts[sel].report;

            string _nps = layouts[sel].notesPerSecond.ToString("0.00");
            string _jd = layouts[sel].jumpDistance.ToString("0.00");
            string _rt = layouts[sel].reactionTime.ToString("000.0");
            string _off = layouts[sel].noteOffset.ToString("0.00");
            string _njs = layouts[sel].njs.ToString("00.00");

            int[] errorTable = report.errors;
            int _modCount = errorTable[0];
            int _hotStartCount = errorTable[1];
            int _coldEndCount = errorTable[2];
            int _interCount = errorTable[3];
            int _failSwings = errorTable[4];
            int _oorNote = errorTable[5];
            int _oorWall = errorTable[6];

            string _modsReq = "Mods: ";

            if(_modCount <= 0) {
                _modsReq += "None";
            } else { 
                for(int i = 0; i < _modCount; i++) {
                    _modsReq += report.modsRequired[i];
                    if(i != _modCount - 1)
                        _modsReq += ", ";
                }  
            }


            spsChartGraph.spsData.Clear();
            spsChartGraph.spsData.AddRange(layouts[sel].report.swingsPerSecond);
            spsChartGraph.DataContext = spsChartGraph;

            evc_Profile.Source = MapQueue[QueueList.SelectedIndex].MapProfile;
            evc_SongName.Content = mapInfo._songName;
            evc_SongDiff.Content = (MapDiffs)(1<<(dValue / 2));
            evc_NPS.Text = $"Notes/Second: {_nps}";
            evc_NJS.Text = $"NJS: {_njs}";
            evc_JD.Text = $"JD: {_jd}";
            evc_RT.Text = $"Reaction Time: {_rt} ms";
            evc_OF.Text = $"Offset: {_off}";
            evc_BPM.Text = $"BPM: {mapInfo._bpm}";
            
            evc_Mods.Text = _modsReq;
            evc_HotStart.Text = $"Hot Starts: {_hotStartCount}";
            evc_ColdEnd.Text = $"Cold Ends: {_coldEndCount}";
            evc_Intersections.Text = $"Intersections: {_interCount}";
            evc_FailSwings.Text = $"Fail Swings: {_failSwings}";
            evc_OOR.Text = $"Out-Of-Range: Notes:{_oorNote}, Walls:{_oorWall}";

            evc_Mods.Foreground = EvalColor(_modCount == 0);
            evc_HotStart.Foreground = EvalColor(_hotStartCount == 0);
            evc_ColdEnd.Foreground = EvalColor(_coldEndCount == 0);
            evc_Intersections.Foreground = EvalColor(_interCount == 0);
            evc_FailSwings.Foreground = EvalColor(_failSwings == 0);
            evc_OOR.Foreground = EvalColor((_oorNote+_oorWall) == 0);

        }

        private SolidColorBrush EvalColor(bool exp) => exp ? Brushes.Green : Brushes.Red;
        private Visibility ShowIfFound(bool Shown) => Shown ? Visibility.Visible : Visibility.Collapsed;
        private void updateUserLog(string ctx) => ConsoleText.Text = ctx;
        private void WriteToLogFile(string ctx) => File.AppendAllText(logPath, ctx + '\n');

        private void onAppQuit(object sender, System.ComponentModel.CancelEventArgs e) {
            MapQueue.Clear();
            UserConsole.Log("Clearing temporary directory..");
            FileInterface.DeleteDir_Full(appTemp);
        }

        private void QueueList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            ListView model = sender as ListView;
            //Selection is changed when the app quits
            if(!model.HasItems)
                return;
            MapQueueModel mdl = MapQueue[model.SelectedIndex];
            UpdateDiffButtons(mdl.diffsAvailable, evalStorage[mdl.mapID].Item2);
            //Set the profile and song name on selection
            evc_Profile.Source = MapQueue[QueueList.SelectedIndex].MapProfile;
            evc_SongName.Content = mdl.MapSongName;
        }
    }
}
