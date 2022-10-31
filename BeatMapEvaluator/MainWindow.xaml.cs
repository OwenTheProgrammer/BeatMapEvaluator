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

namespace BeatMapEvaluator
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<MapQueueModel> MapQueue;
        private Dictionary<string, (json_MapInfo, MapStorageLayout[])> evalStorage;

        private readonly char _ps = Path.DirectorySeparatorChar;
        private string appTemp;

        private string appLogsFolder;
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
            appLogsFolder = Path.Combine(cd, "logs") + _ps;
            reportFolder = Path.Combine(cd, "reports") + _ps;
            UserConsole.onConsoleUpdate = new UserConsole.updateStringGUI(updateUserLog);
            UserConsole.onLogUpdate = new UserConsole.updateStringGUI(WriteToLogFile);

            if(!Directory.Exists(appLogsFolder))
                Directory.CreateDirectory(appLogsFolder);

            if(!Directory.Exists(reportFolder))
                Directory.CreateDirectory(reportFolder);

            logFile = "log_" + DateTime.Now.ToString("hh_mm_ss") + ".txt";
            logPath = Path.Combine(appLogsFolder, logFile);
            
            if(Directory.Exists(appTemp))
                FileInterface.DeleteDir_Full(appTemp);

            UserConsole.Log($"tempDir: \"{appTemp}\"");
            UserConsole.Log($"logFile: \"{logFile}\"");
            Directory.CreateDirectory(appTemp);
        }

        private async Task evaluateMap(string mapFolder, string bsr) {
            json_MapInfo info = await FileInterface.ParseInfoFile(mapFolder, bsr);

            int status = -1;
            if(!evalStorage.ContainsKey(info.mapBSR) && info.mapDifficulties != MapDiffs.NONE) {
                json_beatMapDifficulty[] diffRegistry = info.standardBeatmap._diffMaps;
                evalStorage.Add(info.mapBSR, (info, new MapStorageLayout[5]));

                for(int i = 0; i < diffRegistry.Length; i++) {
                    MapStorageLayout layout = await FileInterface.InterpretMapFile(info, i);
                    if(layout == null || layout.audioLength == -1.0f)
                        break;
                    try { 
                    await layout.ProcessDiffRegistery();
                    } catch(Exception err) {
                        UserConsole.LogError($"[{bsr}] Error: {err.Message}");
                    }
                    int index = layout.mapDiff._difficultyRank / 2;
                    evalStorage[info.mapBSR].Item2[index] = layout;

                    if(layout.reportStatus != 1) {
                        string reportPath = Path.Combine(reportFolder, info.mapBSR + ".txt");
                        await UserConsole.ExportReport(layout, info, reportPath);
                    }

                    if(status == -1) {
                        if(layout.reportStatus == 2) status = 2;
                        if(layout.reportStatus == 0) status = 0;
                    }
                }
            }
            if(status == -1) status = 1;
            UserConsole.Log($"[{bsr}]: Map loaded.");
            MapQueue.Add(FileInterface.CreateMapListItem(info, status));
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
            var dialog = new System.Windows.Forms.FolderBrowserDialog {
                Description = "Select folder containing maps",
                UseDescriptionForTitle = true,
                SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + _ps,
                ShowNewFolderButton = true
            };
            if(dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                string folderPath = dialog.SelectedPath + _ps;
                string[] maps = Directory.GetDirectories(folderPath);
                Work_Numerator = 0;
                Work_Denominator = maps.Length;
                evalStorage.EnsureCapacity(evalStorage.Count + Work_Denominator);

                //Evaluate all folders inside selected folder
                foreach(var dir in maps) {
                    string mapFolder = dir + _ps;
                    int cut = dir.LastIndexOf(_ps) + 1;
                    string name = dir.Substring(cut, dir.Length - cut);
                    int end = name.IndexOf(' ');
                    if(end == -1) {
                        name = name.Substring(0, Math.Min(name.Length, 8));
                    } else { 
                        name = name.Substring(0, end);
                    }
                    name += (end == -1) ? ".." : "";
                    await evaluateMap(mapFolder, name);
                }

                Work_Numerator = 0;
                Work_Denominator = 0;
                folderPerc.Content = "";
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
                    string hex = DiffCriteriaReport.diffColors[layout[i].reportStatus];
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

            string _nps = layouts[sel].nps.ToString("0.00");
            string _jd = layouts[sel].jumpDistance.ToString("0.00");
            string _rt = layouts[sel].reactionTime.ToString("000.0");
            string _off = layouts[sel].noteOffset.ToString("0.00");
            string _njs = layouts[sel].njs.ToString("00.00");

            string _modsReq = "Mods: ";
            for(int i = 0; i < report.modsRequired.Count; i++) {
                _modsReq += report.modsRequired[i];
                if(i != report.modsRequired.Count-1)
                    _modsReq += ", ";
            }
            if(report.modsRequired.Count == 0)
                _modsReq += "None :)";

            int[] errorTable = report.errors;

            int _hotStartCount = errorTable[1];
            int _coldEndCount = errorTable[2];
            int _interCount = errorTable[3];
            int _wallWidth = errorTable[4];
            int _failSwings = errorTable[5];
            int _oorNote = errorTable[6];
            int _oorWall = errorTable[7];

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
            evc_WallWidth.Text = $"Wall Widths: {_wallWidth}";
            evc_FailSwings.Text = $"Fail Swings: {_failSwings}";
            evc_OOR.Text = $"Out-Of-Range: Notes:{_oorNote}, Walls:{_oorWall}";

            evc_Mods.Foreground = EvalColor(report.modsRequired.Count == 0);
            evc_HotStart.Foreground = EvalColor(_hotStartCount == 0);
            evc_ColdEnd.Foreground = EvalColor(_coldEndCount == 0);
            evc_Intersections.Foreground = EvalColor(_interCount == 0);
            evc_WallWidth.Foreground = EvalColor(_wallWidth == 0);
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
