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

namespace BeatMapEvaluator
{
    public partial class MainWindow : Window
    {
        private readonly char _ps = Path.DirectorySeparatorChar;
        private string appTemp;

        public MainWindow() {
            InitializeComponent();

            appTemp = Path.Combine(Directory.GetCurrentDirectory(), "temp") + _ps;
            UserConsole.onConsoleUpdate = new UserConsole.updateStringGUI(updateUserLog);

            if(Directory.Exists(appTemp))
                FileInterface.deleteDirFull(appTemp);

            UserConsole.Log($"tempDir: \"{appTemp}\"");
            Directory.CreateDirectory(appTemp);
        }

        //At the moment the user can press the button as many
        //times as they want... das bad
        private async void evaluateCode_OnClick(object sender, RoutedEventArgs e) {
            await FileInterface.downloadBSR("1e6ff", appTemp);
            await FileInterface.parseInfoFile(Path.Combine(appTemp, "1e6ff\\"));
        }

        private void updateUserLog(string ctx) => ConsoleText.Text = ctx;
        private void onAppQuit(object sender, System.ComponentModel.CancelEventArgs e) {
            UserConsole.Log("Clearing temporary directory..");
            FileInterface.deleteDirFull(appTemp);
        }
    }
}
