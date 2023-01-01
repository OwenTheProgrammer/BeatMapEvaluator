using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Linq;
using BeatMapEvaluator.Model;
using WinForms = System.Windows.Forms;

namespace BeatMapEvaluator
{
	public partial class MainWindow : Window
	{
		/// <summary>The UI list of queued/evaluated maps</summary>
		public ObservableCollection<MapQueueModel> MapQueue;
		/// <summary>The internal buffer of all queued/evaluated maps</summary>
		private Dictionary<string, (json_MapInfo, MapStorageLayout[])> evalStorage;

        /// <summary>Systems defined path separator "/" "\" etc.</summary>
        private readonly char _ps = Path.DirectorySeparatorChar;

        /// <summary>Path to the current log file</summary>
        private string logFile;

        /// <summary>Dirpath to the temp folder</summary>
        private string appTemp;
        /// <summary>Dirpath to the logs folder</summary>
        private string logPath;
        /// <summary>Dirpath to the reports folder</summary>
        private string reportFolder;

        private int Work_Numerator;     //Folder progress numerator
		private int Work_Denominator;   //Folder progress denominator

		public MainWindow() {
			InitializeComponent();
			MapQueue = new ObservableCollection<MapQueueModel>();
			evalStorage = new Dictionary<string, (json_MapInfo, MapStorageLayout[])>();
			QueueList.ItemsSource = MapQueue;
			UpdateDiffButtons(MapDiffs.NONE, null);

			//Build the dirpath to temp/ logs/ and reports/
			string cd = Directory.GetCurrentDirectory();
			appTemp = Path.Combine(cd, "temp") + _ps;
			logPath = Path.Combine(cd, "logs") + _ps;
			reportFolder = Path.Combine(cd, "reports") + _ps;
			//Bind user console callbacks for UI updates and log file writes
			UserConsole.onConsoleUpdate = new UserConsole.updateStringGUI(updateUserLog);
			UserConsole.onLogUpdate = new UserConsole.updateStringGUI(WriteToLogFile);

			//Create log folder
			if(!Directory.Exists(logPath))
				Directory.CreateDirectory(logPath);

			//Create the report folder
            if (!Directory.Exists(reportFolder))
				Directory.CreateDirectory(reportFolder);

			//Create log file with current time
			logFile = "log_" + DateTime.Now.ToString("hh_mm_ss") + ".txt";
			logPath = Path.Combine(logPath, logFile);
			
			//Delete the old temp/ folder if found
			if(Directory.Exists(appTemp))
				FileInterface.DeleteDir_Full(appTemp);

			UserConsole.Log($"tempDir: \"{appTemp}\"");
			UserConsole.Log($"logFile: \"{logFile}\"");
			Directory.CreateDirectory(appTemp);
		}

		/// <summary>
		/// Master "do the thing" function for evaluating criteria on the map
		/// </summary>
		/// <param name="mapFolder">The containing folder for the map folder</param>
		/// <param name="bsr">The map BSR</param>
		private async Task evaluateMap(string mapFolder, string bsr) {
			//Skip if we evaluated this map
			if(evalStorage.ContainsKey(bsr)) {
				UserConsole.Log($"{bsr} already loaded.");
				return;
			}

			//Download and parse the map
			json_MapInfo info = await FileInterface.ParseInfoFile(mapFolder, bsr);
			//No "info.dat" file or no "standard" diff found
			if(info == null || info.standardBeatmap == null) {
				UserConsole.LogError($"[{bsr}]: \"{mapFolder}Info.dat\" failed to load.");
				return;
			}

			ReportStatus status = ReportStatus.None;
			//If there is a difficulty in the difficulties available
			if(info.mapDifficulties != MapDiffs.NONE) {
				json_beatMapDifficulty[] diffRegistry = info.standardBeatmap._diffMaps;
				if(diffRegistry == null)
					return;
				//Add the current map to the evaluation storage
				if(!evalStorage.ContainsKey(info.mapBSR))
					evalStorage.Add(info.mapBSR, (info, new MapStorageLayout[5])); //5 diffs

				for(int i = 0; i < diffRegistry.Length; i++) {
					MapStorageLayout layout = await FileInterface.InterpretMapFile(info, i);
					//Layout or audio failed to load
					if(layout == null || layout.audioLength == -1.0f) {
						UserConsole.LogError($"[{bsr}] Error loading diff");
						//Mask out from the availability registry (remove the diff)
						MapDiffs currentDiff = (MapDiffs)(~(1<<i));
						info.mapDifficulties &= currentDiff;
						continue;
					}
					//Attempt to evaluate the diff
					try {
						await layout.ProcessDiffRegistery();
					} catch(Exception err) {
						UserConsole.LogError($"[{bsr}] Error: {err.Message}");
					}
					//Store the evaluation into the funny table
					int index = layout.mapDiff._difficultyRank / 2;
					evalStorage[info.mapBSR].Item2[index] = layout;

					//If the eval didnt pass, make a report file
					/*if(layout.reportStatus != ReportStatus.Passed) {
						string reportPath = Path.Combine(reportFolder, info.mapBSR + ".txt");
						await UserConsole.ExportReport(layout, info, reportPath);
					}*/
					if(status == ReportStatus.None) { 
						if(layout.reportStatus == ReportStatus.Error) 
							status = ReportStatus.Error;
						if(layout.reportStatus == ReportStatus.Failed)
							status = ReportStatus.Failed;
					}
					//Release memory
					//layout.ClearDiff();
				}
				//
                string reportPath = Path.Combine(reportFolder, info.mapBSR + ".txt");
                await UserConsole.ExportReport(evalStorage[info.mapBSR].Item2, info, reportPath);

				foreach(var layout in evalStorage[info.mapBSR].Item2) {
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
			//Add the visual stimuli to the screen in the form of a queue item
			MapQueue.Add(await FileInterface.CreateMapListItem(info, status));
			Work_Numerator++;
			folderPerc.Content = Work_Numerator.ToString() + " / " + Work_Denominator.ToString();
		}

		//At the moment the user can press the button as many
		//times as they want... das bad (lol ill do this later whatever)
		private async void evaluateCode_OnClick(object sender, RoutedEventArgs e) {
			string bsr = bsrBox.Text;
			if(bsr == null || bsr.Equals("")) {
				UserConsole.LogError("BSR code null.");
				return;
			}
			//BSR already exists
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
			//Show the windows folder select dialog
			if(dialog.ShowDialog() == WinForms.DialogResult.OK) {
				string folderPath = dialog.SelectedPath + _ps;
				string[] maps = Directory.GetDirectories(folderPath);
				string[] files = Directory.GetFiles(folderPath);
				//Substring to only the file names
				for(int i = 0; i < files.Length; i++) {
					string f = files[i];
					files[i] = f.Substring(f.LastIndexOf(_ps)+1);
				}
				Work_Numerator = 0;
				Work_Denominator = maps.Length;
				evalStorage.EnsureCapacity(evalStorage.Count + Work_Denominator);

				//If this is a map folder
				if(files.Contains("Info.dat")) {
					//Strip the folder to only the folder name (BSR code)
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

        /// <summary>
		/// OnClick binding to <see cref="ParseZipFile(string)"/>.
		/// </summary>
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

        /// <summary>
		/// Framework layer to call <see cref="evaluateMap(string, string)"/> on a zip file.
		/// </summary>
        /// <param name="zipPath">The zip file path</param>
        private async Task ParseZipFile(string zipPath) {
			//Check if the BSR has already been evaluated
			string bsr = Utils.ParseBSR(zipPath);
			if(evalStorage.ContainsKey(bsr)) {
				UserConsole.Log($"{bsr} already loaded.");
				return;
			}

			//Extract the zip to the target dir
			UserConsole.Log($"Loading \"{bsr}\"");
			try {
				string outDir = Path.Combine(appTemp, bsr);
				ZipFile.ExtractToDirectory(zipPath, outDir);
			} catch {
				UserConsole.LogError($"[{bsr}]: Error extracting map.");
				return;
			}
			string mapFolder = Path.Combine(appTemp, bsr + _ps);
			await evaluateMap(mapFolder, bsr);
		}

        /// <summary>
        /// Drag-n-Drop functionality to calling <see cref="evaluateMap(string, string)"/>
        /// </summary>
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

		/// <summary>
		/// Changes the current difficulty for the UI to reference.
		/// </summary>
		private void diffButton_OnClick(object sender, RoutedEventArgs e) {
			Button button = sender as Button;
			int diffValue = int.Parse(button.Tag as string);
			MapDiffs diff = (MapDiffs)(1<<(diffValue / 2));
			//UserConsole.Log($"Selected diff: {diff}");

			string bsr = MapQueue[QueueList.SelectedIndex].mapID;
			UpdateMainPage(bsr, diffValue);
		}

		/// <summary>
		/// A god awful way to update the buttons that are available given the <see cref="MapDiffs"/>
		/// </summary>
		/// <param name="avl">Available difficulties</param>
		/// <param name="layout">Map layout</param>
		private void UpdateDiffButtons(MapDiffs avl, MapStorageLayout[]? layout) {
			//Dont @ me
			diffButton_Easy.Visibility = ShowIfFound(avl.HasFlag(MapDiffs.Easy));
			diffButton_Normal.Visibility = ShowIfFound(avl.HasFlag(MapDiffs.Normal));
			diffButton_Hard.Visibility = ShowIfFound(avl.HasFlag(MapDiffs.Hard));
			diffButton_Expert.Visibility = ShowIfFound(avl.HasFlag(MapDiffs.Expert));
			diffButton_ExpertPlus.Visibility = ShowIfFound(avl.HasFlag(MapDiffs.ExpertPlus));

			if(layout != null) {
				//Constructs all diff colours
				var converter = new BrushConverter();
				Brush[] colors = new Brush[5];
				for(int i = 0; i < 5; i++) {
					if(layout[i] == null) continue;
					string hex = DiffCriteriaReport.diffColors[(int)layout[i].reportStatus];
					colors[i] = (Brush)converter.ConvertFromString(hex);
				}
				//Dont even think about it..
				//Changes the color based on the button.
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
		
		/// <summary>
		/// The main UI update function for when the diff changes.
		/// </summary>
		/// <param name="bsr">BSR key</param>
		/// <param name="dValue">Difficulty index</param>
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


			spsChartGraph.LeftHandSwings.Clear();
			spsChartGraph.RightHandSwings.Clear();
			spsChartGraph.LeftHandSwings.AddRange(report.LeftHandSwings);
			spsChartGraph.RightHandSwings.AddRange(report.RightHandSwings);
			spsChartGraph.DataContext = spsChartGraph;
			//spsChartGraph.spsData.Clear();
			//spsChartGraph.spsData.AddRange(layouts[sel].report.swingsPerSecond);
			//spsChartGraph.DataContext = spsChartGraph;

			//50% 25% 1% of swings averaged
			int[] per = {
				Utils.GetSwingPercentile(report.LeftHandSwings, 0.5f),
				Utils.GetSwingPercentile(report.RightHandSwings, 0.5f),

				Utils.GetSwingPercentile(report.LeftHandSwings, 0.25f),
				Utils.GetSwingPercentile(report.RightHandSwings, 0.25f),
				
				Utils.GetSwingPercentile(report.LeftHandSwings, 0.01f),
				Utils.GetSwingPercentile(report.RightHandSwings, 0.01f)
			};

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
			evc_Percents.Text = $"Swing Highs L/R: 50% [{per[0]}, {per[1]}] " +
								$"| 25% [{per[2]}, {per[3]}] " +
								$"| 1% [{per[4]}, {per[5]}]";

			evc_Mods.Foreground = EvalColor(_modCount == 0);
			evc_HotStart.Foreground = EvalColor(_hotStartCount == 0);
			evc_ColdEnd.Foreground = EvalColor(_coldEndCount == 0);
			evc_Intersections.Foreground = EvalColor(_interCount == 0);
			evc_FailSwings.Foreground = EvalColor(_failSwings == 0);
			evc_OOR.Foreground = EvalColor((_oorNote+_oorWall) == 0);
			evc_Percents.Foreground = new SolidColorBrush(Color.FromRgb(255, 217, 87));
		}

		/// <summary>
		/// Returns a green brush if <paramref name="exp"/> is true,
		/// otherwise returns a red brush
		/// </summary>
		/// <param name="exp">The input expression</param>
		/// <returns>A colour brush</returns>
		private SolidColorBrush EvalColor(bool exp) 
			=> exp ? new SolidColorBrush(Color.FromRgb(17,255,170)) 
					: new SolidColorBrush(Color.FromRgb(255,17,85));

		/// <summary>
		/// Converts a <see cref="bool"/> to Visible state.
		/// </summary>
		/// <param name="Shown">Is this shown?</param>
		/// <returns>Visibility state</returns>
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
