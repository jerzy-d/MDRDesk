using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using ClrMDRIndex;
using Application = System.Windows.Application;
using Binding = System.Windows.Data.Binding;
using Clipboard = System.Windows.Clipboard;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ListBox = System.Windows.Controls.ListBox;
using ListView = System.Windows.Controls.ListView;
using MessageBox = System.Windows.MessageBox;
using Panel = System.Windows.Controls.Panel;
using TabControl = System.Windows.Controls.TabControl;
using TextBox = System.Windows.Controls.TextBox;
using TreeView = System.Windows.Controls.TreeView;
using SW = System.Windows;
using SWC = System.Windows.Controls;


namespace MDRDesk
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private DispatcherTimer _dispatcherTimer; // to update some stuff periodically

        /// <summary>
        /// List of recent files.
        /// </summary>
		private RecentFileList RecentDumpList;
		private RecentFileList RecentIndexList;
		private RecentFileList RecentAdhocList;

		public static string BaseTitle;
		public static Map CurrentMap; // TODO JRD to be removed
		public static DumpIndex CurrentIndex;
		public static ClrtDump CurrentAdhocDump;
		public static DumpIndex CurrentAdhocIndex;
		private static Version _myVersion;

		#region Ctors/Initialization

		public MainWindow()
		{

			InitializeComponent();
			string error;
			if (!Init(out error))
			{
				Dispatcher.CurrentDispatcher.InvokeAsync(() => MessageBoxShowError(error));
			}
		}

		private bool Init(out string error)
		{
			error = null;
			try
			{
				_myVersion = Assembly.GetExecutingAssembly().GetName().Version;
#if DEBUG
				MainWindow.BaseTitle = "MDR Desk (debug) " + _myVersion + (Environment.Is64BitProcess ? "  [64-bit]" : "  [32-bit]");
#else
				MainWindow.BaseTitle = "MDR Desk  " + _myVersion + (Environment.Is64BitProcess ? "  [64-bit]" : "  [32-bit]");
#endif
				this.Title = MainWindow.BaseTitle;
				int addrSize = IntPtr.Size;
				this.AddHandler(CloseableTabItem.CloseTabEvent, new RoutedEventHandler(this.CloseTab));

				var result = Setup.GetConfigSettings(out error);
				if (!result) return false;
				RecentDumpList = new RecentFileList(RecentDumpsMenuItem, (int)Setup.RecentFiles.MaxCount);
				RecentDumpList.Add(Setup.RecentDumpList);
				RecentIndexList = new RecentFileList(RecentIndexMenuItem, (int)Setup.RecentFiles.MaxCount);
				RecentIndexList.Add(Setup.RecentIndexList);
				RecentAdhocList = new RecentFileList(RecentAdhocMenuItem, (int)Setup.RecentFiles.MaxCount);
				RecentAdhocList.Add(Setup.RecentAdhocList);
				SetupDispatcherTimer();

				switch (Setup.TypesDisplayMode)
				{
					case "namespaces":
						TypeDisplayNamespaceClass.IsChecked = true;
						break;
					case "types":
						TypeDisplayClass.IsChecked = true;
						break;
					case "fulltypenames":
						TypeDisplayNamespace.IsChecked = true;
						break;
					default:
						TypeDisplayNamespace.IsChecked = true;
						break;

				}
				//myresourcedictionary = new ResourceDictionary();
				//myresourcedictionary.Source = new Uri("/MDRDesk;component/Resources/MDRDeskResourceDct.xaml",
				//UriKind.RelativeOrAbsolute);
				return result;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}

		private void SetupDispatcherTimer()
		{
			_dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
			_dispatcherTimer.Tick += new EventHandler(DispatcherTimerTick);
			_dispatcherTimer.Interval = new TimeSpan(0, 0, 5);
			_dispatcherTimer.Start();

		}

		private void DispatcherTimerTick(object sender, EventArgs e)
		{
			var proc = Process.GetCurrentProcess();
			long wrkSet = proc.WorkingSet64;
			long peakWrkSet = proc.PeakWorkingSet64;

			ProcessMemory.Content = "Memory: " + Utils.FormatBytes(wrkSet) + " / " + Utils.FormatBytes(peakWrkSet);
		}

		#endregion Ctors/Initialization

		#region Menu

		#region File

		private void FileOpenReportFileClicked(object sender, RoutedEventArgs e)
		{
			string path = SelectFile(string.Format("*.{0}", Constants.TextFileExt),
			string.Format("Dump Map Files|*.{0}|All Files|*.*", Constants.TextFileExt));
			if (path == null) return;
			string error, title;
			var result = ReportFile.ReadReportFile(path, out title, out error);
			if (result == null || error != null)
			{
				MessageBox.Show(path + Environment.NewLine + error, "Action Failed", MessageBoxButton.OK,
					MessageBoxImage.Exclamation);
				return;
			}
			DisplayListViewBottomGrid(result, Constants.AdhocQuerySymbol, "ReportFile", (string.IsNullOrWhiteSpace(title) ? "Report" : title), null, path);
		}

		private void ExitClicked(object sender, RoutedEventArgs e)
		{
			Application.Current.Shutdown();
		}

		public void MainWindow_Closing(object sender, CancelEventArgs e)
		{
			string error;
			if (RecentDumpList != null) Setup.ResetRecentFileList(RecentDumpList.GetPaths(), Setup.RecentFiles.Dump);
			if (RecentIndexList != null) Setup.ResetRecentFileList(RecentIndexList.GetPaths(), Setup.RecentFiles.Map);
			if (RecentAdhocList != null) Setup.ResetRecentFileList(RecentAdhocList.GetPaths(), Setup.RecentFiles.Adhoc);
			Setup.SaveConfigSettings(out error);
			var task = Task.Factory.StartNew(() =>
			{
				CurrentMap?.Dispose();
				CurrentAdhocDump?.Dispose();
				CurrentAdhocIndex?.Dispose();
			});
			task.Wait();
		}

		private void CloseTab(object source, RoutedEventArgs args)
		{
			var tabItem = args.Source as TabItem;
			if (tabItem == null) return;
			var tabControl = tabItem.Parent as TabControl;
			if (tabControl != null)
				tabControl.Items.Remove(tabItem);
		}

		#endregion File

		#region Dump

		private void CrashDumpRequiredDacClicked(object sender, RoutedEventArgs e)
		{
			var path = SelectCrashDumpFile();
			if (path == null) return;

			string error;
			var dacFile = ClrtDump.GetRequiredDac(path, out error);
			if (dacFile == null)
			{
				SW.MessageBox.Show(error, "Get Required Dac Failed", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			SW.Clipboard.SetText(dacFile);
			RecentDumpList.Add(path);
			ShowInformation("Required Dac File", dacFile, "Dac file name (shown above) is copied to Clipboard.", string.Empty);
		}

		private void AddDacFileClicked(object sender, RoutedEventArgs e)
		{

		}

		private void TryOpenCrashDumpClicked(object sender, RoutedEventArgs e)
		{

		}

		#endregion Dump

		#region Index

		private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
		{
			RadioButton button = sender as RadioButton;
			string buttonName = button.Name;
			if (!IsIndexAvailable())
			{
				switch (buttonName)
				{
					case "TypeDisplayNamespaceClass":
						Setup.SetTypesDisplayMode("namespaces");
						break;
					case "TypeDisplayClass":
						Setup.SetTypesDisplayMode("types");
						break;
					case "TypeDisplayNamespace":
						Setup.SetTypesDisplayMode("fulltypenames");
						break;
				}
				return;
			}

			bool openNewTab = true;
			List<string> lst = new List<string>(3);
			foreach (TabItem tabItem in MainTab.Items)
			{
				if (tabItem.Content is Grid)
				{
					var grid = tabItem.Content as Grid;
					if (grid.Name.StartsWith(GridReversedNameTypeView))
					{
						if (buttonName == "TypeDisplayClass")
						{
							openNewTab = false;
							break;
						}
					}
					if (grid.Name.StartsWith(GridNameNamespaceTypeView))
					{
						if (buttonName == "TypeDisplayNamespaceClass")
						{
							openNewTab = false;
							break;
						}
					}
					if (grid.Name.StartsWith(GridKeyNameTypeView))
					{
						if (buttonName == "TypeDisplayNamespace")
						{
							openNewTab = false;
							break;
						}
					}
				}
			}
			if (openNewTab)
			{
				switch (buttonName)
				{
					case "TypeDisplayNamespaceClass":
						Setup.SetTypesDisplayMode("namespaces");
						var namespaces = CurrentIndex.GetNamespaceDisplay();
						DisplayNamespaceGrid(namespaces);
						break;
					case "TypeDisplayClass":
						Setup.SetTypesDisplayMode("types");
						DisplayTypesGrid(true);
						break;
					case "TypeDisplayNamespace":
						Setup.SetTypesDisplayMode("fulltypenames");
						DisplayTypesGrid(false);
						break;
				}
			}
		}

		private async void CreateDumpIndexClicked(object sender, RoutedEventArgs e)
		{
			var path = SelectCrashDumpFile();
			if (path == null) return;
			var dmpFileName = Path.GetFileNameWithoutExtension(path);
			var progressHandler = new Progress<string>(MainStatusShowMessage);
			var progress = progressHandler as IProgress<string>;
			SetStartTaskMainWindowState("Indexing: " + dmpFileName + ", please wait.");

			var result = await Task.Run(() =>
			{
				string error;
				string indexPath;
				var indexer = new DumpIndexer(path);
				var ok = indexer.CreateDumpIndex(_myVersion, progress, DumpIndexer.IndexingArguments.All, out indexPath, out error);
				return new Tuple<bool, string, string>(ok, error, indexPath);
			});

			Utils.ForceGcWithCompaction();
			SetEndTaskMainWindowState(result.Item1
				? "Indexing: " + dmpFileName + " done."
				: "Indexing: " + dmpFileName + " failed.");

			if (!result.Item1)
			{
				ShowError(result.Item2);
				return;
			}
			Dispatcher.CurrentDispatcher.InvokeAsync(() => DoOpenDumpIndex(0, result.Item3));
		}

		private async void OpenDumpIndexClicked(object sender, RoutedEventArgs e)
		{
			string path = null;
			if (sender != null && sender is MenuItem)
			{
				var menuItem = sender as MenuItem;
				if (menuItem.HasHeader && menuItem.Header is string && (menuItem.Header as string) == "Recent Indices")
				{
					path = e.OriginalSource as string;
				}
			}
			if (path == null) path = GetFolderPath(Setup.MapFolder);

			if (path == null) return;

			DoOpenDumpIndex(0, path);
		}

		private async void DoOpenDumpIndex(int runtimeIndex, string path)
		{
			if (path == null) return;
			var progressHandler = new Progress<string>(MainStatusShowMessage);
			var progress = progressHandler as IProgress<string>;
			SetStartTaskMainWindowState("Opening index: " + Utils.GetPathLastFolder(path) + ", please wait...");

			var result = await Task.Run(() =>
			{
				string error;
				DumpIndex index = DumpIndex.OpenIndexInstanceReferences(_myVersion, path, runtimeIndex,  out error, progress);
				KeyValuePair<string, KeyValuePair<string, int>[]>[] namespaces = null;

				if (error == null && index != null)
				{
					namespaces = index.GetNamespaceDisplay();
				}
				return new Tuple<string, DumpIndex, KeyValuePair<string, KeyValuePair<string, int>[]>[]>(error, index, namespaces);
			});

			SetEndTaskMainWindowState("Index: '" + DumpFileMoniker.GetMapName(path) + (result.Item1 == null ? "' is open." : "' open failed."));

			if (result.Item1 != null)
			{
				ShowError(result.Item1);
				return;
			}
			if (CurrentIndex != null)
			{
				CurrentIndex.Dispose();
				CurrentIndex = null;
				Utils.ForceGcWithCompaction();
			}
			CurrentIndex = result.Item2;

			var genInfo = CurrentIndex.GetGenerationTotals();
			DisplayGeneralInfoGrid(CurrentIndex.DumpInfo, genInfo);
			if ((TypeDisplayNamespaceClass.IsChecked ?? (bool)TypeDisplayNamespaceClass.IsChecked) && result.Item3 != null)
			{
				DisplayNamespaceGrid(result.Item3);
			}
			else
			{
				if ((TypeDisplayClass.IsChecked ?? (bool)TypeDisplayNamespaceClass.IsChecked))
					DisplayTypesGrid(true);
				else
					DisplayTypesGrid(false);
			}

			this.Title = BaseTitle + Constants.BlackDiamondPadded + CurrentIndex.DumpFileName;
			RecentIndexList.Add(path);
		}

		private void IndexShowModuleInfosClicked(object sender, RoutedEventArgs e)
		{
			if (!IsIndexAvailable("Show Loaded Modules Infos")) return;
			var path = CurrentIndex.GetFilePath(-1, Constants.TxtTargetModulesPostfix);

			string error, title;
			var result = ReportFile.ReadReportFile(path, out title, out error);
			if (result == null || error != null)
			{
				MessageBox.Show(path + Environment.NewLine + error, "Action Failed", MessageBoxButton.OK,
					MessageBoxImage.Exclamation);
				return;
			}
			DisplayListViewBottomGrid(result, Constants.BlackDiamond, "Modules", (string.IsNullOrWhiteSpace(title) ? "Modules" : title), null, path);
		}

		private async void IndexShowFinalizerQueueClicked(object sender, RoutedEventArgs e)
		{
			if (!IsIndexAvailable("Show Finalizer Queue")) return;

			SetStartTaskMainWindowState("Getting finalizer queue info, please wait...");

			var finalizerInfo = await Task.Run(() => CurrentMap.GetDisplayableFinalizationQueue(true));

			SetEndTaskMainWindowState("Getting finalizer queue info done.");

			var grid = this.TryFindResource("ListViewBottomGrid") as Grid;
			Debug.Assert(grid != null);
			grid.Name = "FinalizerQueue__" + Utils.GetNewID();
			grid.Tag = new DumpFileMoniker(CurrentMap.DumpPath);
			var listView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, "TopListView");
			GridView gridView = (GridView)listView.View;

			gridView.Columns.Add(new GridViewColumn
			{
				Header = "Address",
				DisplayMemberBinding = new Binding("PFirst"),
				Width = 200,
			});
			gridView.Columns.Add(new GridViewColumn
			{
				Header = "Not Rooted",
				DisplayMemberBinding = new Binding("PSecond"),
				Width = 100,
			});
			gridView.Columns.Add(new GridViewColumn
			{
				Header = "Type",
				DisplayMemberBinding = new Binding("PThird"),
				Width = 500,
			});

			listView.ItemsSource = finalizerInfo.Item1;
			var bottomGrid = (Panel)LogicalTreeHelper.FindLogicalNode(grid, "BottomGrid");

			var btmLstView = new ListView
			{
				VerticalAlignment = SW.VerticalAlignment.Stretch,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};
			GridView btmGridView = new GridView();
			btmGridView.Columns.Add(new GridViewColumn
			{
				Header = "Count",
				DisplayMemberBinding = new Binding("Key"),
				Width = 200,
			});
			btmGridView.Columns.Add(new GridViewColumn
			{
				Header = "Type",
				DisplayMemberBinding = new Binding("Value"),
				Width = 500,
			});
			btmLstView.ItemsSource = finalizerInfo.Item2;
			btmLstView.View = btmGridView;

			bottomGrid.Children.Add(btmLstView);

			var tab = new CloseableTabItem() { Header = Constants.BlackDiamond + " Finalization Queue", Content = grid, Name = "FinalizationQueueView" };
			MainTab.Items.Add(tab);
			MainTab.SelectedItem = tab;
			MainTab.UpdateLayout();
		}

		private void IndexShowRootsClicked(object sender, RoutedEventArgs e)
		{
			if (!IsIndexAvailable("Show Roots")) return;

		}

		private async void IndexShowWeakReferencesClicked(object sender, RoutedEventArgs e)
		{
			if (!IsIndexAvailable("Show Weak References")) return;

			SetStartTaskMainWindowState("Getting WeakReference information, please wait...");
			var result = await Task.Run(() =>
			{
				string error;
				var info = CurrentMap.GetWeakReferenceInfo(out error);
				return error == null ? info : new ListingInfo(error);
			});

			SetEndTaskMainWindowState(result.Error == null
				? "Getting WeakReference information done."
				: "Getting WeakReference information failed.");

			if (result.Error != null)
			{
				MessageBox.Show(result.Error, "Getting WeakReference Information Failed", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			DisplayListViewBottomGrid(result, Constants.BlackDiamond, ReportNameWeakReferenceInfo, ReportTitleWeakReferenceInfo);
		}


		private async void IndexGetSizeInformationClicked(object sender, RoutedEventArgs e)
		{
			if (!IsIndexAvailable("Get Size Information")) return;

			SetStartTaskMainWindowState("Getting type size information: , please wait...");
			var result = await Task.Run(() =>
			{
				string error;
				var info = CurrentMap.GetAllTypesSizesInfo(out error);
				return error == null ? info : new ListingInfo(error);
			});

			SetEndTaskMainWindowState(result.Error == null
				? "Getting type size information done."
				: "Getting sizes info failed.");

			if (result.Error != null)
			{
				MessageBox.Show(result.Error, "Getting Sizes Info Failed", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			DisplayListViewBottomGrid(result, Constants.BlackDiamond, ReportNameSizeInfo, ReportTitleSizeInfo);
		}

		private long _minStringUsage = 1;
		private async void IndexStringUsageClicked(object sender, RoutedEventArgs e)
		{
			if (!IsIndexAvailable("Get String Usage")) return;

			bool addGenerationInfo = false;
			MenuItem menuItem = sender as MenuItem;
			Debug.Assert(menuItem != null);
			if ((menuItem.Header as string).Contains("Generations")) addGenerationInfo = true;

			GetUserEnteredNumber("Minimum String Reference Count", "Enter number, hex format not allowed:", out _minStringUsage);

			if (CurrentMap.AreStringDataFilesAvailable())
				SetStartTaskMainWindowState("Getting string usage. Please wait...");
			else
				SetStartTaskMainWindowState("Getting string usage, string cache has to be created, it will take a while. Please wait...");


			var taskResult = await Task.Run(() =>
			{
				string error;
				return CurrentMap.GetStringStats((int)_minStringUsage, out error, addGenerationInfo);
			});

			SetEndTaskMainWindowState(taskResult.Error == null
				? "Collecting strings of: " + CurrentMap.DumpBaseName + " done."
				: "Collecting strings of: " + CurrentMap.DumpBaseName + " failed.");

			if (taskResult.Error != null)
			{
				MainStatusShowMessage("Getting Strings: " + CurrentMap.DumpBaseName + ", failed.");
				MessageBox.Show(taskResult.Error, "Getting Strings Failed", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			SWC.MenuItem[] menuItems = new SWC.MenuItem[]
			{
				new SWC.MenuItem { Header = "Copy List Row" },
				new SWC.MenuItem { Header = "GC Generation Distribution" },
				new SWC.MenuItem { Header = "Get References" },
				new SWC.MenuItem { Header = "Get References of String Prefix" },
				new SWC.MenuItem { Header = "Get Size of Strings with Prefix" }
			};
			DisplayListViewBottomGrid(taskResult, Constants.BlackDiamond, ReportNameStringUsage, ReportTitleStringUsage, menuItems);
		}

		private void InstReferenceClicked(object sender, RoutedEventArgs e)
		{
			if (!IsIndexAvailable("Get Instance Information")) return;
			var menuItem = sender as MenuItem;
			Debug.Assert(menuItem != null);

			// first get the address
			//
			ulong addr;
			if (!GetUserEnteredAddress("Instance Address", "Enter instance address, if not hex format prefix with n/N.", out addr))
				return;

			switch (menuItem.Header.ToString().ToUpper())
			{
				case "IMMEDIATE PARENT REFERENCE":
					Dispatcher.CurrentDispatcher.InvokeAsync(() => ExecuteReferenceQuery("Getting instance references", addr, null, 1));
					break;
				case "N PARENTS REFERENCE":
					long refLevel;
					if (!GetUserEnteredNumber("Reference Query Level", "Enter number, hex format not allowed:", out refLevel)) return;
					Dispatcher.CurrentDispatcher.InvokeAsync(() => ExecuteReferenceQuery("Getting instance references", addr, null, (int)refLevel));
					break;
				case "ALL PARENTS REFERENCE":
					Dispatcher.CurrentDispatcher.InvokeAsync(() => ExecuteReferenceQuery("Getting instance references", addr, null, Int32.MaxValue));
					break;
				case "GENERATION HISTOGRAM":
					var grid = GetCurrentTabGrid();
					Dispatcher.CurrentDispatcher.InvokeAsync(() => ExecuteGenerationQuery("Get instance generation", new ulong[] { addr }, grid));
					break;
				case "INSTANCE HIERARCHY WALK":
					Dispatcher.CurrentDispatcher.InvokeAsync(() => ExecuteInstanceHierarchyQuery("Get instance hierarchy " + Utils.AddressStringHeader(addr), addr, Constants.InvalidIndex));
					break;
			}
		}

		private async void IndexCompareSizeInformationClicked(object sender, RoutedEventArgs e)
		{
			if (!IsIndexAvailable("Compare Size Information")) return;
			var path = SelectCrashDumpFile();
			if (path == null) return;
			SetStartTaskMainWindowState("Getting type sizes to compare. Please wait...");

			var taskResult = await Task.Run(() =>
			{
				return CurrentMap.CompareTypesWithOther(path);
			});

			SetEndTaskMainWindowState(taskResult.Error == null
				? "Collecting sizes of: " + CurrentMap.DumpBaseName + " done."
				: "Collecting sizes of: " + CurrentMap.DumpBaseName + " failed.");

			DisplayListViewBottomGrid(taskResult, Constants.BlackDiamond, ReportNameSizeDiffs, ReportTitleSizeDiffs);
		}

		private async void IndexCompareStringInformationClicked(object sender, RoutedEventArgs e)
		{
			if (!IsIndexAvailable("Compare String Instances Information")) return;
			var path = SelectCrashDumpFile();
			if (path == null) return;
			SetStartTaskMainWindowState("Getting string instances to compare. Please wait...");

			var taskResult = await Task.Run(() =>
			{
				return CurrentMap.CompareStringsWithOther(path);
			});

			SetEndTaskMainWindowState(taskResult.Error == null
				? "Collecting strings of: " + CurrentMap.DumpBaseName + " done."
				: "Collecting strings of: " + CurrentMap.DumpBaseName + " failed.");

			if (taskResult.Error != null)
			{
				ShowError(taskResult.Error);
				return;
			}

			DisplayListViewBottomGrid(taskResult, Constants.BlackDiamond, ReportNameSizeDiffs, ReportTitleSizeDiffs);
		}

		private void RecentIndicesClicked(object sender, RoutedEventArgs e)
		{
			var menuItem = sender as MenuItem;
			Debug.Assert(menuItem != null);
			if (menuItem.Items != null && menuItem.Items.Count > 0 && menuItem.Items.CurrentItem != null)
			{
				var fileInfo = menuItem.Items.CurrentItem as FileInfo;
				if (fileInfo != null)
				{
					Dispatcher.CurrentDispatcher.InvokeAsync(() => OpenDumpIndexClicked(menuItem, new RoutedEventArgs(null, fileInfo.FilePath)));
				}
			}
		}

		#endregion Index

		#region Context Menu



		#endregion Context Menu

		#region AdhocQueries

		private async void StringUsageClicked(object sender, RoutedEventArgs e)
		{
			string error;
			var dumpFilePath = SelectCrashDumpFile();
			if (dumpFilePath == null) return;
			var progressHandler = new Progress<string>(MainStatusShowMessage);
			var progress = progressHandler as IProgress<string>;

			GetUserEnteredNumber("Minimum String Reference Count", "Enter number, hex format not allowed:", out _minStringUsage);

			SetStartTaskMainWindowState("Getting string usage. Please wait...");

			var taskResult = await Task.Run(() =>
			{
				var dmp = ClrtDump.OpenDump(dumpFilePath, out error);
				if (dmp == null)
				{
					return new ListingInfo(error);
				}
				using (dmp)
				{
					var heap = dmp.Runtime.GetHeap();
					var addresses = ClrtDump.GetTypeAddresses(heap, "System.String", out error);
					if (error != null)
					{
						return new ListingInfo(error);
					}
					var strStats = ClrtDump.GetStringStats(heap, addresses, dumpFilePath, out error);
					return strStats.GetGridData((int)_minStringUsage, out error);
				}
			});

			SetEndTaskMainWindowState("Getting Strings: " + System.IO.Path.GetFileName(dumpFilePath) + (taskResult.Error == null ? ", done." : ", failed."));
			if (taskResult.Error != null)
			{
				MessageBox.Show(taskResult.Error, "Getting Strings Failed", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			SWC.MenuItem[] menuItems = new SWC.MenuItem[]
			{
				new SWC.MenuItem { Header = "Copy List Row" },
				new SWC.MenuItem { Header = "GC Generation Distribution" },
				new SWC.MenuItem { Header = "Get References" },
				new SWC.MenuItem { Header = "Get References of String Prefix" }
			};
			DisplayListViewBottomGrid(taskResult, Constants.BlackDiamond, ReportNameStringUsage, ReportTitleStringUsage, menuItems);
			RecentAdhocList.Add(dumpFilePath);
		}

		private async void AhqTypeCountClicked(object sender, RoutedEventArgs e)
		{
			string error;
			var dumpFilePath = SelectCrashDumpFile();
			if (dumpFilePath == null) return;
			var progressHandler = new Progress<string>(MainStatusShowMessage);
			var progress = progressHandler as IProgress<string>;
			string typeName;
			if (!GetDlgString("Get Type Count", "EnterTypeName", " ", out typeName)) return;
			SetStartTaskMainWindowState("Getting type count. Please wait...");
			var taskResult = await Task.Run(() =>
			{
				var dmp = ClrtDump.OpenDump(dumpFilePath, out error);
				if (dmp == null)
				{
					return new KeyValuePair<string, int>(error, -1);
				}
				using (dmp)
				{
					var heap = dmp.Runtime.GetHeap();
					var count = ClrtDump.GetTypeCount(heap, typeName, out error);
					return new KeyValuePair<string, int>(error, count);
				}
			});

			string msg;
			if (taskResult.Key != null)
			{
				msg = "Getting type count failed, " + typeName;
			}
			else
			{
				msg = "Type count: " + taskResult.Value + ", " + typeName;
			}

			SetEndTaskMainWindowState(msg);
			if (taskResult.Key != null)
				ShowError(taskResult.Key);

		}

		private void AhqCollectionContentArray(object sender, RoutedEventArgs e)
		{
			string error;
			var dumpFilePath = SelectCrashDumpFile();
			if (dumpFilePath == null) return;
			ulong addr;
			if (!GetUserEnteredAddress("Enter an array address.", out addr))
				return;
			var progressHandler = new Progress<string>(MainStatusShowMessage);
			var progress = progressHandler as IProgress<string>;

		}

		private async void AhqCreateInstanceRefsClicked(object sender, RoutedEventArgs e)
		{
			var dumpFilePath = SelectCrashDumpFile();
			if (dumpFilePath == null) return;

			var progressHandler = new Progress<string>(MainStatusShowMessage);
			var progress = progressHandler as IProgress<string>;
			var dmpFileName = Path.GetFileName(dumpFilePath);
			SetStartTaskMainWindowState("Indexing: " + dmpFileName + ", please wait.");

			var result = await Task.Run(() =>
			{
				string error;
				string indexPath;
				var indexer = new DumpIndexer(dumpFilePath);
				//var ok = indexer.Index(_myVersion, progress, out indexPath, out error);
				var ok = indexer.CreateDumpIndex(_myVersion, progress, DumpIndexer.IndexingArguments.JustInstanceRefs, out indexPath, out error);
				return new Tuple<bool, string, string>(ok, error, indexPath);
			});

			Utils.ForceGcWithCompaction();
			SetEndTaskMainWindowState(result.Item1
				? "Indexing: " + dmpFileName + " done."
				: "Indexing: " + dmpFileName + " failed.");

			if (!result.Item1)
			{
				ShowError(result.Item2);
				return;
			}

		}

		private async void AhqOpenInstanceRefsClicked(object sender, RoutedEventArgs e)
		{
			var dumpFilePath = SelectCrashDumpFile();
			if (dumpFilePath == null) return;

			var progressHandler = new Progress<string>(MainStatusShowMessage);
			var progress = progressHandler as IProgress<string>;
			var dmpFileName = Path.GetFileName(dumpFilePath);
			SetStartTaskMainWindowState("Indexing: " + dmpFileName + ", please wait.");

			var result = await Task.Run(() =>
			{
				string error;
				var index = DumpIndex.OpenIndexInstanceReferences(_myVersion, dumpFilePath, 0, out error, progress);
				if (index != null)
				{
					if (CurrentAdhocIndex != null)
					{
						CurrentAdhocIndex.Dispose();
					}
					CurrentAdhocIndex = index;
				}
				return new Tuple<bool, string>(index!=null, error);
			});

			Utils.ForceGcWithCompaction();
			SetEndTaskMainWindowState(result.Item1
				? "Indexing: " + dmpFileName + " done."
				: "Indexing: " + dmpFileName + " failed.");

			if (!result.Item1)
			{
				ShowError(result.Item2);
				return;
			}

		}


		/// <summary>
		/// Handle the context menu of ListViewBottomGrid 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void ListViewBottomGridClick(object sender, RoutedEventArgs e)
		{
			try
			{
				Debug.Assert(sender is SWC.MenuItem);
				SWC.MenuItem menuItem = sender as SWC.MenuItem;
				var lstView = menuItem.Tag as SWC.ListView;
				Debug.Assert(lstView != null);
				var reportTitle = GetReportTitle(lstView);
				var ndx = lstView.SelectedIndex;
				if (ndx < 0)
				{
					// TODO JRD
					return;
				}

				var entries = lstView.ItemsSource as listing<string>[];
				string row = null;
				string error = null;
				string headerUpper = menuItem.Header.ToString().ToUpper();
				switch (headerUpper)
				{
					case "COPY LIST ROW":
						row = GetListingRow(entries[ndx]);
						Clipboard.SetText(row);
						MessageBox.Show(row, "Copied to clipboard.", MessageBoxButton.OK, MessageBoxImage.Information);
						break;
					case "GC GENERATION DISTRIBUTION":
						var str = entries[ndx].GetItem(entries[ndx].Count - 1);
						str = ReportFile.RecoverReportLineString(str);
						var grid = GetCurrentTabGrid();
						Dispatcher.CurrentDispatcher.InvokeAsync(() => ExecuteGenerationQuery("Getting generation distribution", reportTitle, str, grid));
						break;
					case "GET REFERENCES OF STRING PREFIX":
					case "GET REFERENCES":
						var selStr = entries[ndx].GetLastItem();
						if (headerUpper == "GET REFERENCES OF STRING PREFIX")
						{
							string prefix;
							if (GetDlgString("Type String Prefix", "", selStr, out prefix))
							{
								selStr = prefix + Constants.FancyKleeneStar;
							}
						}
						switch (reportTitle)
						{
							case ReportTitleStringUsage:
								SetStartTaskMainWindowState("Searching for instances with string field: " + selStr + ", please wait.");

								var task = Task<ListingInfo>.Factory.StartNew(() => CurrentMap.GetTypesWithSpecificStringFieldListing(selStr));
								task.Wait();
								SetEndTaskMainWindowState(task.Result.Error == null
									? "Searching for instances with string field done."
									: "Searching for instances with string field failed.");
								if (task.Result.Error != null)
								{
									if (task.Result.Error == ListingInfo.EmptyList)
									{
										Dispatcher.CurrentDispatcher.InvokeAsync(() =>
										ShowInformation("Empty List", "Search for string references failed", "References not found for string: " + selStr, null))
										;
										return;
									}
									Dispatcher.CurrentDispatcher.InvokeAsync(() => ShowError(task.Result.Error));
									return;
								}
								SWC.MenuItem[] menuItems = new SWC.MenuItem[]
								{
								new SWC.MenuItem { Header = "Copy List Row" },
								new SWC.MenuItem { Header = "GC Generation Distribution" },
								new SWC.MenuItem { Header = "Get References" },
								};
								DisplayListViewBottomGrid(task.Result, Constants.BlackDiamond, ReportNameTypesWithString, ReportTitleSTypesWithString, menuItems);
								break;
							case ReportTitleSTypesWithString:
								selStr = entries[ndx].Second;
								var addr = Convert.ToUInt64(selStr, 16);

								var report = CurrentMap.GetFieldReferencesReport(addr);
								if (report.Error != null)
								{
									Dispatcher.CurrentDispatcher.InvokeAsync(() => ShowError(report.Error));
									//MessageBox.Show(report.Error, "Action Aborted", MessageBoxButton.OK, MessageBoxImage.Exclamation);
									return;
								}
								DisplayListViewBottomGrid(report, Constants.BlackDiamond, ReportNameInstRef, ReportTitleInstRef);
								break;
						}
						break;
					case "GET SIZE OF STRINGS WITH PREFIX":
						var sel = entries[ndx].GetLastItem();
						string pref;
						if (GetDlgString("Type String Prefix", "", "", out pref))
						{
							sel = pref + Constants.FancyKleeneStar;
							var len = CurrentMap.GetSizeOfStringsWithPrefix(sel, out error);
							if (error != null && len < 0)
							{
								// error TODO JRD
							}
							MainStatusShowMessage("Size of strings with prefix: " + sel + ": " + Utils.LargeNumberString(len));
						}
						break;
				}
			}
			catch (Exception ex)
			{
				var error = Utils.GetExceptionErrorString(ex);
				MessageBox.Show(error, "ListViewBottomGridClick", MessageBoxButton.OK, MessageBoxImage.Error);
			}

		}

		private static string GetListingRow(listing<string> lst)
		{
			StringBuilder sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
			int cnt = lst.Count;
			int lastNdx = cnt - 1;
			for (int i = 0; i < cnt; ++i)
			{
				sb.Append(lst.GetItem(i));
				if (i < lastNdx)
					sb.Append(Constants.ReportSeparator);
			}
			return StringBuilderCache.GetStringAndRelease(sb);
		}

		private void ListViewBottomGridHeaderClick(object sender, RoutedEventArgs e)
		{
			GridViewColumnHeader column = e.OriginalSource as GridViewColumnHeader;
			if (column == null) return;
			if (column.Role == GridViewColumnHeaderRole.Padding) return;

			var mapListView = sender as ListView;
			if (mapListView == null) return;

			if (mapListView.Tag != null && mapListView.Tag is Tuple<ListingInfo, string>)
			{
				var aryToSort = mapListView.ItemsSource as listing<string>[];
				string header = column.Column.Header.ToString();
				var info = mapListView.Tag as Tuple<ListingInfo, string>;
				int foundNdx = -1;
				for (int i = 0, icnt = info.Item1.ColInfos.Length; i < icnt; ++i)
				{
					if (info.Item1.ColInfos[i].Name == header)
					{
						foundNdx = i;
						break;
					}
				}
				if (foundNdx >= 0)
				{
					info.Item1.ColInfos[foundNdx].ReverseOrder();
					ReportFile.SortListingStringArray(info.Item1.ColInfos[foundNdx], aryToSort);
					mapListView.ItemsSource = aryToSort;
					ICollectionView dataView = CollectionViewSource.GetDefaultView(mapListView.ItemsSource);
					dataView.Refresh();
					return;
				}
				return;
			}

			var entries = mapListView.ItemsSource as StringStatsDispEntry[];
			if (entries != null)
			{
				StringStatsDispEntry.Sort(entries, column.Column.Header.ToString());
				mapListView.ItemsSource = entries;
				ICollectionView dataView = CollectionViewSource.GetDefaultView(mapListView.ItemsSource);
				dataView.Refresh();
				return;
			}
			var tuples = mapListView.ItemsSource as sextuple<int, ulong, ulong, ulong, ulong, string>[];
			if (tuples != null)
			{
				string header = column.Column.Header.ToString();
				bool sorted = false;
				string sortedBy = mapListView.Tag == null ? string.Empty : mapListView.Tag.ToString();
				switch (header)
				{
					case "Count":
						if (sortedBy != Constants.ByCount)
						{
							Array.Sort(tuples, (a, b) => a.First < b.First ? 1 : (a.First > b.First ? -1 : 0));
							sortedBy = Constants.ByCount;
							sorted = true;
						}
						break;
					case "Total Size":
						if (sortedBy != Constants.ByTotalSize)
						{
							sortedBy = Constants.ByTotalSize;
							Array.Sort(tuples, (a, b) => a.Second < b.Second ? 1 : (a.Second > b.Second ? -1 : 0));
							sorted = true;
						}
						break;
					case "Max Size":
						if (sortedBy != Constants.ByMaxSize)
						{
							Array.Sort(tuples, (a, b) => a.Third < b.Third ? 1 : (a.Third > b.Third ? -1 : 0));
							sortedBy = Constants.ByMaxSize;
							sorted = true;
						}
						break;
					case "Avg Size":
						if (sortedBy != Constants.ByAvgSize)
						{
							Array.Sort(tuples, (a, b) => a.Fifth < b.Fifth ? 1 : (a.Fifth > b.Fifth ? -1 : 0));
							sortedBy = Constants.ByAvgSize;
							sorted = true;
						}
						break;
					case "Type":
						if (sortedBy != Constants.ByTypeName)
						{
							Array.Sort(tuples, (a, b) => string.Compare(a.Sixth, b.Sixth, StringComparison.Ordinal));
							sortedBy = Constants.ByTypeName;
							sorted = true;
						}
						break;

				}
				if (sorted)
				{
					mapListView.Tag = sortedBy;
					mapListView.ItemsSource = tuples;
					ICollectionView dataView = CollectionViewSource.GetDefaultView(mapListView.ItemsSource);
					dataView.Refresh();
				}
				return;
			}
		}

		#endregion AdhocQueries

		#region File Reports

		private async void FileReportClicked(object sender, RoutedEventArgs e)
		{
			Grid grid = GetReportGrid(true);
			if (grid == null) return;
			string gridName = Utils.GetNameWithoutId(grid.Name);
			switch (gridName)
			{
				case "IndexSizesInfo":
					var info = grid.Tag as Tuple<DumpFileMoniker, ulong>;
					Debug.Assert(info != null);
					var listView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, "TopListView");
					Debug.Assert(listView != null);
					var dataAry = listView.ItemsSource as sextuple<int, ulong, ulong, ulong, ulong, string>[];
					Debug.Assert(dataAry != null);
					var sortBy = listView.Tag as string;
					Debug.Assert(sortBy != null);
					var reportPath = info.Item1.OutputFolder + System.IO.Path.DirectorySeparatorChar + info.Item1.DumpFileName +
									 ".TYPESIZES." + sortBy + ".txt";
					break;
				case "IndexStringUsage":
					var dmpInf = grid.Tag as DumpFileMoniker;
					Debug.Assert(dmpInf != null);
					var lstView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, "TopListView");
					Debug.Assert(lstView != null);
					var datAry = lstView.ItemsSource as StringStatsDispEntry[];
					Debug.Assert(datAry != null);
					var repPath = dmpInf.OutputFolder + System.IO.Path.DirectorySeparatorChar + "ALLSTRINGUSAGE.txt";
					string error;
					StringStatsDispEntry.WriteShortReport(datAry, repPath, "String usage in: " + CurrentMap.DumpBaseName, datAry.Length,
						new string[] { "Count" }, null, out error);
					break;
			}

			var listingView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, "TopListView");
			GridView gridView = (GridView)listingView.View;
			listing<string>[] data = listingView.ItemsSource as listing<string>[];
			var colSortInfo = listingView.Tag as Tuple<ListingInfo, string>;
			var bottomGrid = (Panel)LogicalTreeHelper.FindLogicalNode(grid, "BottomGrid");
			var textBox = bottomGrid.Children[0] as TextBox;
			var descrLines = GetTextBoxReportNotes(textBox);
			var gridInfo = grid.Tag as Tuple<string, DumpFileMoniker>;
			Debug.Assert(gridInfo != null);
			var outpath = DumpFileMoniker.GetOutputPathWithDumpPrefix(gridInfo.Item2, Utils.RemoveWhites(gridInfo.Item1) + ".txt");

			SetStartTaskMainWindowState("Writing report. Please wait...");
			var taskResult = await Task.Run(() =>
			{
				string error;
				var ok = ReportFile.WriteReport(outpath, gridName, gridInfo.Item1, colSortInfo.Item1.ColInfos, descrLines, data, out error);
				return new Tuple<string, string>(error, outpath);
			});

			SetEndTaskMainWindowState(taskResult.Item1 == null
				? "Report written: " + taskResult.Item2
				: "Report failed: " + taskResult.Item2);
		}

		private static string[] GetTextBoxReportNotes(TextBox tb)
		{
			List<string> lines = new List<string>(tb.LineCount);
			for (int i = 0, icnt = tb.LineCount; i < icnt; ++i)
			{
				var line = tb.GetLineText(i);
				if (line.StartsWith("#### "))
					line = line.Substring("#### ".Length);
				if (string.IsNullOrWhiteSpace(line)) continue;
				lines.Add(line.TrimEnd());
			}
			return lines.ToArray();

		}

		private void FileReportShortClicked(object sender, RoutedEventArgs e)
		{
			Grid grid = GetReportGrid(false);
			if (grid == null) return;
			string error;
			DumpFileMoniker filePathInfo;
			ListView listView;
			string repPath;
			string gridName = Utils.GetNameWithoutId(grid.Name);
			switch (gridName)
			{
				case "IndexSizesInfo":
					var info = grid.Tag as Tuple<DumpFileMoniker, ulong>;
					Debug.Assert(info != null);
					listView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, "TopListView");
					Debug.Assert(listView != null);
					var dataAry = listView.ItemsSource as sextuple<int, ulong, ulong, ulong, ulong, string>[];
					Debug.Assert(dataAry != null);
					var sortBy = listView.Tag as string;
					Debug.Assert(sortBy != null);
					var reportPath = info.Item1.OutputFolder + System.IO.Path.DirectorySeparatorChar + info.Item1.DumpFileName +
									 ".TYPESIZES.TOP64." + sortBy + ".txt";
					StreamWriter sw = null;
					try
					{
						sw = new StreamWriter(reportPath);
						sw.WriteLine("Grand total of all instances base sizes: " + Utils.SizeString(info.Item2) + ", (" + Utils.FormatBytes((long)info.Item2) + ")");
						sw.WriteLine("Columns:  'Count', 'Total Size', 'Max Size', 'Min Size', 'Avg Size', 'Type'    ...all sizes in bytes.");
						sw.WriteLine();
						for (int i = 0; i < 64; ++i)
						{
							var tuple = dataAry[i];
							sw.WriteLine(
								string.Format("{0,14:#,###,###}  ", tuple.First)
								+ string.Format("{0,14:#,###,###}  ", tuple.Second)
								+ string.Format("{0,14:#,###,###}  ", tuple.Third)
								+ string.Format("{0,14:#,###,###}  ", tuple.Forth)
								+ string.Format("{0,14:#,###,###}  ", tuple.Fifth)
								+ tuple.Sixth
								);
						}

					}
					catch (Exception ex)
					{
						error = Utils.GetExceptionErrorString(ex);
					}
					finally
					{
						sw?.Close();
					}
					break;
				case "IndexStringUsage":
					filePathInfo = grid.Tag as DumpFileMoniker;
					Debug.Assert(filePathInfo != null);
					listView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, "TopListView");
					Debug.Assert(listView != null);
					var datAry = listView.ItemsSource as StringStatsDispEntry[];
					Debug.Assert(datAry != null);
					repPath = filePathInfo.OutputFolder + System.IO.Path.DirectorySeparatorChar + "STRINGUSAGE.txt";
					StringStatsDispEntry.WriteShortReport(datAry, repPath, "String usage in: " + CurrentMap.DumpBaseName, 100,
						new string[] { "Count", "TotalSize" }, null, out error);
					break;
				case "ReportFile":
					var pathInfo = grid.Tag as Tuple<string, DumpFileMoniker>;
					Debug.Assert(pathInfo != null);
					listView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, "TopListView");
					Debug.Assert(listView != null);
					var listingInfo = listView.Tag as Tuple<ListingInfo, string>;
					Debug.Assert(listingInfo != null);
					var outPath = pathInfo.Item2.GetPathAppendingToFileName(".ShortReport.txt");
					ListingInfo.DumpListing(outPath, listingInfo.Item1, pathInfo.Item1, out error, 100);
					break;
			}
		}

		private Grid GetReportGrid(bool detailedReport)
		{
			var grid = GetCurrentTabGrid();
			if (grid == null)
			{
				MainStatusShowMessage("Cannot write report, no data grid is available.");
			}
			return grid;
		}


		#endregion File Reports

		#region Settings
		private void SettingsClicked(object sender, RoutedEventArgs e)
		{

		}

		#endregion Settings

		#region Force GC
		private void ForceGCClicked(object sender, RoutedEventArgs e)
		{
			long totMem = GC.GetTotalMemory(false);
			SetStartTaskMainWindowState("Forcing GC collection, total memory: " + Utils.SizeString(totMem) + ", please wait.");
			Utils.ForceGcWithCompaction();
			long totMemAfter = GC.GetTotalMemory(true);
			SetEndTaskMainWindowState("GC collection done, total memory: " + Utils.SizeString(totMemAfter) + ", before/after diff: " + Utils.SizeString(totMem - totMemAfter));
		}

		#endregion Force GC

		#endregion Menu

		#region File/Folder Selection

		private string SelectCrashDumpFile()
		{
			return SelectFile(string.Format("*.{0}", Constants.CrashDumpFileExt),
							string.Format("Dump Map Files|*.{0}|All Files|*.*", Constants.CrashDumpFileExt));
		}

		private string[] SelectCrashDumpFiles()
		{
			return SelectFiles(string.Format("*.{0}", Constants.CrashDumpFileExt),
							string.Format("Dump Map Files|*.{0}|All Files|*.*", Constants.CrashDumpFileExt));
		}

		private string SelectAssemblyFile()
		{
			return SelectFile(string.Format("*.{0}", "dll"),
							string.Format("Dll files (*.dll)|*.dll|Exe files (*.exe)|*.exe"), null);
		}

		private string SelectFile(string defExtension, string filter, string initialDir = null)
		{
			try
			{
				var dlg = new Microsoft.Win32.OpenFileDialog { DefaultExt = defExtension, Filter = filter, Multiselect = false };
				dlg.Multiselect = false;
				if (initialDir != null)
				{
					string path = System.IO.Path.GetFullPath(initialDir);
					if (Directory.Exists(path))
					{
						dlg.InitialDirectory = path;
					}
				}
				bool? result = dlg.ShowDialog();
				return result == true ? dlg.FileName : null;
			}
			catch (Exception ex)
			{
				ShowError(Utils.GetExceptionErrorString(ex));
				return null;
			}
		}

		private string[] SelectFiles(string defExtension, string filter, string initialDir = null)
		{
			try
			{
				var dlg = new Microsoft.Win32.OpenFileDialog { DefaultExt = defExtension, Filter = filter, Multiselect = false };
				dlg.Multiselect = true;
				if (initialDir != null)
				{
					string path = System.IO.Path.GetFullPath(initialDir);
					if (Directory.Exists(path))
					{
						dlg.InitialDirectory = path;
					}
				}
				bool? result = dlg.ShowDialog();
				if (result != true) return null;
				return dlg.FileNames;
			}
			catch (Exception ex)
			{
				ShowError(Utils.GetExceptionErrorString(ex));
				return null;
			}
		}

		private string GetFolderPath(string initialFolder)
		{
			string path = null;
			var dialog = new System.Windows.Forms.FolderBrowserDialog();
			using (dialog)
			{
				if (initialFolder != null) dialog.SelectedPath = initialFolder;
				System.Windows.Forms.DialogResult result = dialog.ShowDialog();
				if (result != System.Windows.Forms.DialogResult.OK) return null;
				path = dialog.SelectedPath;
			}
			return path;
		}

		#endregion File/Folder Selection

		#region GUI Helpers

		private static Stopwatch _taskDurationStopwatch = new Stopwatch();
		private void SetStartTaskMainWindowState(string message)
		{
			_taskDurationStopwatch.Restart();
			MainStatusShowMessage(message);
			MainToolbarTray.IsEnabled = false;
			Mouse.OverrideCursor = Cursors.Wait;
			MainStatusProgressBar.Visibility = Visibility.Visible;
		}

		private void SetEndTaskMainWindowState(string message)
		{
			MainStatusShowMessage(message + Utils.StopAndGetDurationString(_taskDurationStopwatch));
			MainToolbarTray.IsEnabled = true;
			Mouse.OverrideCursor = null;
			MainStatusProgressBar.Visibility = Visibility.Hidden;
		}

		private void MainStatusShowMessage(string msg)
		{
			MainStatusLabel.Content = msg;
		}

		private void DisplayTreeView(TreeViewItem root, string title)
		{
			var grid = this.TryFindResource("TreeViewGrid") as Grid;
			Debug.Assert(grid != null);
			var tv = grid.Children[0] as TreeView;
			Debug.Assert(tv != null);
			tv.Items.Add(root);
			root.ExpandSubtree();
			var tab = new CloseableTabItem() { Header = title, Content = grid };
			MainTab.Items.Add(tab);
			MainTab.SelectedItem = tab;
		}

		private Grid GetCurrentTabGrid()
		{
			return (MainTab.SelectedItem as CloseableTabItem)?.Content as Grid;
		}

		#endregion GUI Helpers

		#region Dialogs

		private bool GetDlgString(string title, string descr, string defValue, out string str)
		{
			str = null;
			InputStringDlg dlg = new InputStringDlg(descr, defValue ?? " ") { Title = title, Owner = Window.GetWindow(this) };
			if (dlg.DialogResult != null && (!dlg.DialogResult.Value || string.IsNullOrWhiteSpace(dlg.Answer))) return false;
			var dlgResult = dlg.ShowDialog();
			str = dlg.Answer.Trim();
			dlg.Close();
			dlg = null;
			return true;
		}

		private bool GetUserEnteredAddress(string title, out ulong addr)
		{
			addr = Constants.InvalidAddress;
			string str;
			if (!GetDlgString(title, "Enter address:", " ", out str)) return false;
			str = str.Trim();
			if (str.StartsWith("0x") || str.StartsWith("0X")) str = str.Substring(2);
			if (!ulong.TryParse(str, System.Globalization.NumberStyles.AllowHexSpecifier, null, out addr))
			{
				MessageBox.Show("Not valid hex number string: " + str + ".", "INVALID INPUT", MessageBoxButton.OK,
					MessageBoxImage.Error);
				return false;
			}
			return true;
		}

		private bool GetUserEnteredInteger(string title, out int value)
		{
			value = Int32.MinValue;
			string str;
			if (!GetDlgString(title, "Enter number:", " ", out str)) return false;
			str = str.Trim();

			if (!Int32.TryParse(str, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value))
			{
				MessageBox.Show("Not valid number string: " + str + ".", "INVALID INPUT", MessageBoxButton.OK,
					MessageBoxImage.Error);
				return false;
			}
			return true;
		}

		#endregion Dialogs

		private void RecentDumpSelectionClicked(object sender, RoutedEventArgs e)
		{
			int a = 0;
		}







		private void CopyAddressSelectionClicked(object sender, RoutedEventArgs e)
		{
			var lbAddresses = GetTypeAddressesListBox(sender);

			if (lbAddresses == null)
			{
				ShowInformation("Copy Address Command", "ACTION FAILED", "Grid's ListBox control not found.", null);
				return;
			}

			var sb = StringBuilderCache.Acquire(512);
			int cnt = 0;
			foreach (var item in lbAddresses.SelectedItems)
			{
				if (++cnt > 10000) break;
				sb.AppendLine(Utils.AddressString((ulong)item));
			}
			if (cnt < 1)
			{
				MainStatusShowMessage("No address is copied to Clipboard. Is an address selected?");
				return;
			}

			string str = StringBuilderCache.GetStringAndRelease(sb);
			if (cnt == 1)
				str = str.Trim();
			Clipboard.SetText(str);
			if (cnt == 1)
			{
				MainStatusShowMessage("Address: " + str + " is copied to Clipboard.");
				return;
			}
			MainStatusShowMessage(cnt + " addresses are copied to Clipboard.");
		}

		private void CopyAddressAllClicked(object sender, RoutedEventArgs e)
		{
			var lbAddresses = GetTypeAddressesListBox(sender);
			if (lbAddresses == null)
			{
				ShowInformation("Copy Address Command", "ACTION FAILED", "Grid's ListBox control not found.", null);
				return;
			}

			var addresses = lbAddresses.ItemsSource as ulong[];

			int maxLen = Math.Min(10000, addresses.Length);
			if (maxLen < 1)
			{
				MainStatusShowMessage("No address is copied to Clipboard. Address list is ermpty.");
				return;
			}

			var sb = StringBuilderCache.Acquire(maxLen * 18);
			for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
			{
				sb.AppendLine(Utils.AddressString(addresses[i]));
			}
			string str = StringBuilderCache.GetStringAndRelease(sb);

			if (maxLen == 1)
				str = str.Trim();
			Clipboard.SetText(str);
			if (maxLen == 1)
			{
				MainStatusShowMessage("Address: " + str + " is copied to Clipboard.");
				return;
			}
			MainStatusShowMessage(maxLen + " addresses are copied to Clipboard.");
		}

		private ListBox GetTypeAddressesListBox(object sender)
		{
			var grid = GetCurrentTabGrid();
			string gridName = Utils.GetNameWithoutId(grid.Name);
			string listName = null;
			switch (gridName)
			{
				case GridNameTypeView:
				case GridReversedNameTypeView:
					listName = "lbTypeNameAddresses";
					break;
				case GridNameNamespaceTypeView:
					listName = "lbTypeNamespaceAddresses";
					break;
			}
			if (listName == null) return null;
			var lbAddresses = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, listName);
			Debug.Assert(lbAddresses != null);
			return lbAddresses;
		}

		private ListBox GetTypeNameListBox(object sender)
		{
			var grid = GetCurrentTabGrid();
			string gridName = Utils.GetNameWithoutId(grid.Name);
			string listName = null;
			switch (gridName)
			{
				case GridNameTypeView:
				case GridReversedNameTypeView:
					listName = "lbTypeViewTypeNames";
					break;
				case GridNameNamespaceTypeView:
					listName = "lbNamespaceViewTypeNames";
					break;
			}
			if (listName == null) return null;
			var lbTypeNames = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, listName);
			Debug.Assert(lbTypeNames != null);
			return lbTypeNames;
		}




        private async void GenerateSizeDetailsReport(object sender, RoutedEventArgs e) // TODO JRD -- display as ListView (listing)
		{
			var lbTypeNames = GetTypeNameListBox(sender);
			var selectedItem = lbTypeNames.SelectedItems[0];
			if (selectedItem == null)
			{
				return; // TODO JRD -- display message
			}

			string typeName = null;
			int typeId = Constants.InvalidIndex;
			if (selectedItem is KeyValuePair<string, int>) // namespace display
			{
				typeId = ((KeyValuePair<string, int>)selectedItem).Value;
				typeName = ((KeyValuePair<string, int>)selectedItem).Key;
			}

			var baseTypeName = Utils.BaseTypeName(typeName);

			SetStartTaskMainWindowState("Getting size details for: '" + baseTypeName + "', please wait...");

			var result = await Task.Run(() =>
			{
				string error;
				var info = CurrentMap.GetTypeSizeDetails(typeId, out error);
				if (info == null)
					return new Tuple<string, string, string>(error, null, null);

				var rep = Reports.DumpTypeSizeDetails(CurrentMap.ReportPath, typeName, info.Item1, info.Item2, info.Item3, info.Item4, info.Item5,
					out error);

				return new Tuple<string, string, string>(null, rep.Item1, rep.Item2);
			});

			SetEndTaskMainWindowState("Getting size details for: '" + baseTypeName + "', done");

		}

		private async void GetTypeValuesReportClicked(object sender, RoutedEventArgs e)
		{

			var lbTypeNames = GetTypeNameListBox(sender);
			var selectedItem = lbTypeNames.SelectedItems[0];
			if (selectedItem == null)
			{
				return; // TODO JRD -- display message
			}

			string typeName = null;
			int typeId = Constants.InvalidIndex;
			if (selectedItem is KeyValuePair<string, int>) // namespace display
			{
				typeId = ((KeyValuePair<string, int>)selectedItem).Value;
				typeName = ((KeyValuePair<string, int>)selectedItem).Key;
			}
			var baseTypeName = Utils.BaseTypeName(typeName);

			SetStartTaskMainWindowState("Getting type details for: '" + baseTypeName + "', please wait...");

			var result = await Task.Run(() =>
			{
				string error;
				ClrtDisplayableType dispType = CurrentMap.GetTypeDisplayableRecord(typeId, out error);
				if (dispType == null)
					return new Tuple<string, ClrtDisplayableType>(error, null);
				return new Tuple<string, ClrtDisplayableType>(null, dispType);
			});

			if (result.Item1 != null)
			{
				// TODO JRD
				return;
			}

			DisplayTypeValueSetupGrid(result.Item2);
			SetEndTaskMainWindowState("Getting type details for: '" + baseTypeName + "', done");
		}

		private async void TypeValueReportTreeViewDoubleClicked(object sender, MouseButtonEventArgs e)
		{
			TreeView tv = sender as TreeView;
			var selItem = tv.SelectedItem as TreeViewItem;
			Debug.Assert(selItem != null);
			var dispType = selItem.Tag as ClrtDisplayableType;
			Debug.Assert(dispType != null);

			string msg;
			if (!dispType.CanGetFields(out msg))
			{
				MainStatusShowMessage("Action failed for: '" + dispType.FieldName + "'. " + msg);
				return;
			}

			if (dispType.FieldIndex == Constants.InvalidIndex) // this root type, fields are already displayed
			{
				return;
			}

			var parent = selItem.Parent as TreeViewItem;
			Debug.Assert(parent != null);
			var parentDispType = parent.Tag as ClrtDisplayableType;
			Debug.Assert(parentDispType != null);

			SetStartTaskMainWindowState("Getting type details for field: '" + dispType.FieldName + "', please wait...");

			var result = await Task.Run(() =>
			{
				string error;
				ClrtDisplayableType fldDispType = CurrentMap.GetTypeDisplayableRecord(parentDispType, dispType, out error);
				if (fldDispType == null)
					return new Tuple<string, ClrtDisplayableType>(error, null);
				return new Tuple<string, ClrtDisplayableType>(null, fldDispType);
			});

			if (result.Item1 != null)
			{
				if (Utils.IsInformation(result.Item1))
				{
					SetEndTaskMainWindowState("Action failed for: '" + dispType.FieldName + "'. " + result.Item1);
					return;
				}

				// TODO JRD -- display msg box with error

				SetEndTaskMainWindowState("Getting type details for field: '" + dispType.FieldName + "', failed");

				return;
			}

			var fields = result.Item2.Fields;
			selItem.Items.Clear();
			for (int i = 0, icnt = fields.Length; i < icnt; ++i)
			{
				var fld = fields[i];
				var node = GetTypeValueSetupTreeViewItem(fld);
				selItem.Items.Add(node);
			}
			selItem.ExpandSubtree();

			SetEndTaskMainWindowState("Getting type details for field: '" + dispType.FieldName + "', done");


		}


		private void LbGetInstValueClicked(object sender, RoutedEventArgs e)
		{
			var lbAddresses = GetTypeAddressesListBox(sender);
			if (lbAddresses.SelectedItems.Count < 1)
			{
				MessageBox.Show("No address is selected!", "Action Aborted", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			var addr = (ulong)lbAddresses.SelectedItems[0];


		}

		private void LbGetInstHierarchyClicked(object sender, RoutedEventArgs e)
		{
			var lbAddresses = GetTypeAddressesListBox(sender);
			if (lbAddresses.SelectedItems.Count < 1)
			{
				MessageBox.Show("No address is selected!", "Action Aborted", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			var addr = (ulong)lbAddresses.SelectedItems[0];
			Dispatcher.CurrentDispatcher.InvokeAsync(() => ExecuteInstanceHierarchyQuery("Get instance hierarchy " + Utils.AddressStringHeader(addr), addr, Constants.InvalidIndex));

		}


		/// <summary>
		/// TODO JRD -- thsi is Alex instance walker
		/// </summary>
		/// <param name="rootNode"></param>
		private void DisplayInstanceReferenceTree(InstanceTypeNode rootNode)
		{
			var grid = this.TryFindResource("TreeViewGrid") as Grid;
			Debug.Assert(grid != null);
			var treeView = (TreeView)LogicalTreeHelper.FindLogicalNode(grid, @"treeView");
			var viewRoot = new TreeViewItem() { Header = rootNode };
			Queue<TreeViewItem> que = new Queue<TreeViewItem>(64);
			que.Enqueue(viewRoot);
			while (que.Count > 0)
			{
				var item = que.Dequeue();
				var instNode = item.Header as InstanceTypeNode;
				var instances = instNode.TypeNodes;
				for (int i = 0, icnt = instances.Length; i < icnt; ++i)
				{
					var vnode = new TreeViewItem() { Header = instances[i] };
					item.Items.Add(vnode);
					que.Enqueue(vnode);
				}
			}

			treeView.Items.Add(viewRoot);
			viewRoot.ExpandSubtree();
			var tab = new CloseableTabItem() { Header = "Parents of: " + Utils.AddressString(rootNode.Address), Content = grid };
			MainTab.Items.Add(tab);
			MainTab.SelectedItem = tab;
		}

		private bool IsIndexAvailable(string caption = null)
		{
			if (CurrentIndex == null)
			{
				if (caption == null) return false;
				ShowInformation(caption, "No Index Available", "There's no opened index" + Environment.NewLine + "Please open one. Dump indices folders have extension: '.map'.", null);
				return false;
			}
			return true;
		}




		private void GetDlgString2(string title, string descr, string defValue, out string str)
		{
			str = null;
			InputStringDlg dlg = new InputStringDlg(descr, defValue ?? " ") { Title = title, Owner = Window.GetWindow(this) };
			var dlgResult = dlg.ShowDialog();
		}


		private List<ulong> _lbTypeAddressesLastselections = new List<ulong>();
		private int lbTypeAddressesLastselectedIndex = 0;
		private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ListBox lb = sender as ListBox;
			Debug.Assert(lb != null);
			if (lb.SelectedItems.Count > 0)
			{
				lbTypeAddressesLastselectedIndex = lb.SelectedIndex;
				_lbTypeAddressesLastselections.Clear();
				foreach (var sel in lb.SelectedItems)
				{
					_lbTypeAddressesLastselections.Add((ulong)sel);
				}
				return;
			}

		}

		private void GetTypeStringUsage(object sender, RoutedEventArgs e)
		{
			ListBox listBox = GetTypeNameListBox(sender);
			string typeName = null;
			int typeId = Constants.InvalidIndex;
			if (listBox != null && listBox.SelectedItems.Count > 0)
			{
				string error;
				var sel = listBox.SelectedItems[0];
				typeName = GetTypeNameFromSelection(sel, out typeId);

				return;
			}
		}


	}
}
