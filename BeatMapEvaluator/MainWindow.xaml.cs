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
                string songPath = Path.Combine(mapFolder, info._songFilename);

                var diff = info.standardBeatmap._diffMaps[0];

                string diffPath = Path.Combine(mapFolder, diff._beatmapFilename);
                MapStorageLayout layout = await FileInterface.InterpretMapFile(diffPath);
                layout.AudioLength = AudioLib.GetAudioLength(songPath);
                layout.noteOffset = diff._noteOffset;
                layout.bpm = info._bpm;
                layout.njs = diff._njs;

                UserConsole.Log("Map loaded.");

                MapQueue.Add(FileInterface.CreateMapListItem(info));
            } catch(Exception err) {
                UserConsole.Log($"Error: {err.Message}");
                return;
            }
        }

        private void updateUserLog(string ctx) => ConsoleText.Text = ctx;
        private void onAppQuit(object sender, System.ComponentModel.CancelEventArgs e) {
            MapQueue.Clear();
            UserConsole.Log("Clearing temporary directory..");
            FileInterface.DeleteDir_Full(appTemp);
        }

        private void QueueList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            UserConsole.Log("Selected");
        }
    }
}
