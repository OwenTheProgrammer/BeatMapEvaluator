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

namespace BeatMapEvaluator
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<MapQueueModel> MapQueue;
        private Dictionary<string, (json_MapInfo, MapStorageLayout[])> evalStorage;

        private readonly char _ps = Path.DirectorySeparatorChar;
        private string appTemp;

        public MainWindow() {
            InitializeComponent();
            MapQueue = new ObservableCollection<MapQueueModel>();
            evalStorage = new Dictionary<string, (json_MapInfo, MapStorageLayout[])>();
            QueueList.ItemsSource = MapQueue;
            UpdateDiffButtons(MapDiffs.NONE);

            appTemp = Path.Combine(Directory.GetCurrentDirectory(), "temp") + _ps;
            UserConsole.onConsoleUpdate = new UserConsole.updateStringGUI(updateUserLog);

            if(Directory.Exists(appTemp))
                FileInterface.DeleteDir_Full(appTemp);

            UserConsole.Log($"tempDir: \"{appTemp}\"");
            Directory.CreateDirectory(appTemp);
        }

        //At the moment the user can press the button as many
        //times as they want... das bad
        private async void evaluateCode_OnClick(object sender, RoutedEventArgs e) {
            string bsr = bsrBox.Text;
            if(bsr == null || bsr.Equals("")) {
                UserConsole.Log("BSR code null.");
                return;
            }
            if(Directory.Exists(Path.Combine(appTemp, bsr))) {
                UserConsole.Log($"{bsrBox.Text} has already been loaded.");
                return;
            }

            try {
                await FileInterface.DownloadBSR(bsr, appTemp);
                string mapFolder = Path.Combine(appTemp, bsr + '\\');

                json_MapInfo info = await FileInterface.ParseInfoFile(mapFolder);
                info.mapBSR = bsr;

                if(!evalStorage.ContainsKey(info.mapBSR)) {
                    json_beatMapDifficulty[] diffRegistry = info.standardBeatmap._diffMaps;
                    evalStorage.Add(info.mapBSR, (info, new MapStorageLayout[5]));
                    for(int i = 0; i < diffRegistry.Length; i++) {
                        MapStorageLayout layout = await FileInterface.InterpretMapFile(info, i);
                        await layout.ProcessDiffRegistery();
                        int index = layout.mapDiff._difficultyRank / 2;
                        evalStorage[info.mapBSR].Item2[index] = layout;
                    }
                }

                //MapStorageLayout layout = await FileInterface.InterpretMapFile(info, 0);
                //DiffCriteriaReport report = await layout.ProcessDiffRegistery();

                //spsChartGraph.spsData.Clear();
                //spsChartGraph.spsData.AddRange(report.swingsPerSecond);

                UserConsole.Log("Map loaded.");

                MapQueue.Add(FileInterface.CreateMapListItem(info));
            } catch(Exception err) {
                UserConsole.Log($"Error: {err.Message}");
                return;
            }
        }
        private async void diffButton_OnClick(object sender, RoutedEventArgs e) {
            Button button = sender as Button;
            int diffValue = int.Parse(button.Tag as string);
            MapDiffs diff = (MapDiffs)(1<<(diffValue / 2));
            UserConsole.Log($"Selected diff: {diff}");

            string bsr = MapQueue[QueueList.SelectedIndex].mapID;
            UpdateMainPage(bsr, diffValue);
        }

        private void UpdateDiffButtons(MapDiffs avl) {
            //Dont @ me
            diffButton_Easy.Visibility = ShowIfFound(avl.HasFlag(MapDiffs.Easy));
            diffButton_Normal.Visibility = ShowIfFound(avl.HasFlag(MapDiffs.Normal));
            diffButton_Hard.Visibility = ShowIfFound(avl.HasFlag(MapDiffs.Hard));
            diffButton_Expert.Visibility = ShowIfFound(avl.HasFlag(MapDiffs.Expert));
            diffButton_ExpertPlus.Visibility = ShowIfFound(avl.HasFlag(MapDiffs.ExpertPlus));
        }
        private void UpdateMainPage(string bsr, int dValue) {
            json_MapInfo mapInfo = evalStorage[bsr].Item1;
            MapStorageLayout[] layouts = evalStorage[bsr].Item2;

            int sel = dValue / 2;
            string _nps = layouts[sel].nps.ToString("0.00");
            string _jd = layouts[sel].jumpDistance.ToString("0.00");

            spsChartGraph.spsData.Clear();
            spsChartGraph.spsData.AddRange(layouts[sel].report.swingsPerSecond);
            spsChartGraph.DataContext = spsChartGraph;

            evc_Profile.Source = MapQueue[QueueList.SelectedIndex].MapProfile;
            evc_SongName.Content = mapInfo._songName;
            evc_SongDiff.Content = (MapDiffs)(1<<(dValue / 2));
            evc_NPS.Text = $"Notes/Second: {_nps}";
            evc_JD.Text = $"JD: {_jd}";

        }

        private Visibility ShowIfFound(bool Shown) => Shown ? Visibility.Visible : Visibility.Collapsed;
        private void updateUserLog(string ctx) => ConsoleText.Text = ctx;
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
            UpdateDiffButtons(MapQueue[model.SelectedIndex].diffsAvailable);
        }
    }
}
