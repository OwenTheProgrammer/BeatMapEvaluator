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

namespace BeatMapEvaluator
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<MapQueueModel> MapQueue;

        private readonly char _ps = Path.DirectorySeparatorChar;
        private string appTemp;

        public MainWindow() {
            InitializeComponent();
            MapQueue = new ObservableCollection<MapQueueModel>();
            QueueList.ItemsSource = MapQueue;

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
            try {
                await FileInterface.DownloadBSR("1e6ff", appTemp);
                json_MapInfo info = await FileInterface.ParseInfoFile(Path.Combine(appTemp, "1e6ff\\"));
                MapQueue.Add(FileInterface.CreateMapListItem(info));
            } catch(Exception err) {
                UserConsole.Log($"Error: {err.Message}");
                return;
            }

            //var Array = await EvalLogic.mapHasRequirements(info._difficultyBeatmapSets[0]._difficultyBeatmaps);
        }

        private void updateUserLog(string ctx) => ConsoleText.Text = ctx;
        private void onAppQuit(object sender, System.ComponentModel.CancelEventArgs e) {
            MapQueue.Clear();
            UserConsole.Log("Clearing temporary directory..");
            FileInterface.DeleteDir_Full(appTemp);
        }
    }
}
