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
using Clipboard = System.Windows.Clipboard;
using Cursors = System.Windows.Input.Cursors;
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
        private RecentFileList RecentIndexList;

        private RecentFileList RecentAdhocList;

        public static bool WndDbgLoaded { get; private set; }
        public static string BaseTitle;
        public static DumpIndex CurrentIndex;
        public static ClrtDump CurrentAdhocDump;
        private static Version _myVersion;
        private SingleThreadTaskScheduler _adhocSTAScheduler;
        private SingleThreadTaskScheduler _dumpSTAScheduler;
        public SingleThreadTaskScheduler DumpSTAScheduler => _dumpSTAScheduler;

        #region Ctors/Initialization

        public MainWindow()
        {
            InitializeComponent();
            Dispatcher.CurrentDispatcher.InvokeAsync(() => MdrInit());
        }

        private void MdrInit()
        {
            Init();
        }

        private async void Init()
        {
            string error = null;
            try
            {

                var sysdir = Environment.GetFolderPath(Environment.SpecialFolder.System);


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
                if (!result)
                {
                    Dispatcher.CurrentDispatcher.InvokeAsync(() => MessageBoxShowError(error));
                    return;
                }

                RecentIndexList = new RecentFileList(RecentIndexMenuItem, (int)Setup.RecentFiles.MaxCount);
                RecentIndexList.Add(Setup.RecentIndexList);
                RecentAdhocList = new RecentFileList(RecentAdhocMenuItem, (int)Setup.RecentFiles.MaxCount);
                RecentAdhocList.Add(Setup.RecentAdhocList);
                SetupDispatcherTimer();

                switch (Setup.TypesDisplayMode)
                {
                    case "namespaces":
                        TypeDisplayMode.SelectedIndex = 0;
                        break;
                    case "types":
                        TypeDisplayMode.SelectedIndex = 1;
                        break;
                    case "fulltypenames":
                        TypeDisplayMode.SelectedIndex = 2;
                        break;
                    default:
                        TypeDisplayMode.SelectedIndex = 0;
                        break;
                }

                _adhocSTAScheduler = new SingleThreadTaskScheduler();
                _dumpSTAScheduler = new SingleThreadTaskScheduler();

                // load dbgend.dll if required
                if (Setup.HasWndDbgFolder)
                {
                    (WndDbgLoaded, error) = await Task.Factory.StartNew(() =>
                     {
                         string err;
                         var ok = dbgdeng.DbgEng.LoadDebugEngine(Setup.WndDbgFolder, out err);
                         return (ok, err);
                     }, _dumpSTAScheduler);
                    if (!WndDbgLoaded)
                    {
                        error = error + Environment.NewLine + "All dbgeng queries will be disabled.";
                        Dispatcher.CurrentDispatcher.InvokeAsync(() => ShowError(error));
                    }
                }
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                Dispatcher.CurrentDispatcher.InvokeAsync(() => MessageBoxShowError(error));
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

            ProcessMemory.Content = "My Memory: " + Utils.FormatBytes(wrkSet) + " / " + Utils.FormatBytes(peakWrkSet);
        }

        #endregion Ctors/Initialization

        #region Menu

        #region File

        private void FileOpenReportFileClicked(object sender, RoutedEventArgs e)
        {
            string path = GuiUtils.SelectFile(string.Format("*.{0}", Constants.TextFileExt),
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
            DisplayListViewBottomGrid(result, Constants.AdhocQuerySymbol, "ReportFile",
                (string.IsNullOrWhiteSpace(title) ? "Report" : title), null, path);
        }

        private void ExitClicked(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        public void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            string error;
            
            foreach (var kv in _wndDct)
            {
                kv.Value.Close();
            }
            _wndDct.Clear();
            if (RecentIndexList != null) Setup.ResetRecentFileList(RecentIndexList.GetPaths(), Setup.RecentFiles.Map);
            if (RecentAdhocList != null) Setup.ResetRecentFileList(RecentAdhocList.GetPaths(), Setup.RecentFiles.Adhoc);
            Setup.SaveConfigSettings(out error);

            // if ad-hoc dump is opened, close it
            if (_adhocSTAScheduler != null)
            {
                var task = Task.Factory.StartNew(() => CurrentAdhocDump?.Dispose(), _adhocSTAScheduler);
                task.Wait();
            }
            _adhocSTAScheduler?.Dispose();
            _dumpSTAScheduler?.Dispose();
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
            var path = GuiUtils.SelectCrashDumpFile();
            if (path == null) return;

            string error;
            var dacFiles = ClrtDump.GetRequiredDac(path, out error);
            if (dacFiles.Length < 1)
            {
                SW.MessageBox.Show(error, "Get Required Dac Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            SW.Clipboard.SetText(dacFiles[dacFiles.Length - 1]);
            string lst = string.Join("\n", dacFiles);
            if (dacFiles.Length > 1)
                ShowInformation("Required Dac Files", lst, "Last dac file name (shown above) is copied to Clipboard.", string.Empty);
            else
                ShowInformation("Required Dac File", lst, "Dac file name (shown above) is copied to Clipboard.", string.Empty);
        }

        private void AddDacFileClicked(object sender, RoutedEventArgs e)
        {

        }

        private void TryOpenCrashDumpClicked(object sender, RoutedEventArgs e)
        {
            ClrtDump dump = null;
            string error = null;
            string path = string.Empty;
            string[] dacFiles = null;
            try
            {
                path = GuiUtils.SelectCrashDumpFile();
                if (path == null) return;
                dump = TryOpenCrashDump(path, out error, out dacFiles);
                if (dump != null) dacFiles = dump.DacPaths;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
            }
            finally
            {
                dump?.Dispose();
                if (error != null)
                {
                    MainStatusLabel.Content = "Open crash dump failed: " + path;
                    var pos = error.IndexOf(Constants.HeavyGreekCrossPadded, StringComparison.Ordinal);
                    if (pos > 0)
                    {
                        error = "Open Crash Dump Failed" + error.Substring(pos);
                    }

                    GuiUtils.ShowError(error, this);
                }
                else
                {
                    MainStatusLabel.Content = "Open crash dump succeeded: " + path;
                    string dacs = dacFiles == null || dacFiles.Length < 1 ? "No dac file?" : string.Join(Environment.NewLine, dacFiles);
                    GuiUtils.ShowInformation("Try Open Crash Dump", "Open Crash Dump Succeeded", dacs, null, this);
                }
            }
        }

        private ClrtDump TryOpenCrashDump(string path, out string error, out string[] dacFiles)
        {
            ClrtDump dump = null;
            error = null;
            dacFiles = null;
            try
            {
                if (path == null) return null;
                dump = ClrtDump.OpenDump(path, out error);
                return dump;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
            finally
            {
                if (dump != null)
                {
                    dacFiles = dump.DacPaths;
                }
                if (error != null)
                {
                    var pos = error.IndexOf(Constants.HeavyGreekCrossPadded, StringComparison.Ordinal);
                    if (pos > 0)
                    {
                        error = "Open Crash Dump Failed" + error.Substring(pos);
                    }
                    dump?.Dispose();
                }
                else
                {
                    string dacs = (dacFiles == null || dacFiles.Length < 1)
                        ? "No dac file?"
                        : string.Join(Environment.NewLine, dacFiles);
                }
            }
        }

        private void CreateCrashDumpClicked(object sender, RoutedEventArgs e)
        {
            ClrtDump dump = null;
            string error = null;
            string[] dacFiles = null;
            try
            {
                CreateCrashDump dlg = new CreateCrashDump() { Owner = Window.GetWindow(this) };
                var dlgResult = dlg.ShowDialog();
                if (dlgResult == true && dlg.IndexDump && System.IO.File.Exists(dlg.DumpPath))
                {
                    dump = TryOpenCrashDump(dlg.DumpPath, out error, out dacFiles);
                    if (dump != null)
                    {
                        dump.Dispose(); // TODO JRD -- pass dump to indexer
                        dump = null;
                        if (IsIndexAvailable(null)) CloseCurrentIndex();
                        Dispatcher.CurrentDispatcher.InvokeAsync(() => DoCreateDumpIndex(dlg.DumpPath));
                    }
                }

            }
            catch (Exception ex)
            {
                ShowError(Utils.GetExceptionErrorString(ex));
            }
        }

        #endregion Dump

        #region Index

        private void TypeDisplayModeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TypeDisplayMode.SelectedItem == null) return;
            string sel = (TypeDisplayMode.SelectedValue as ComboBoxItem).Content as string;
            if (sel == null) return;
            switch(sel)
            {
                case "Namespace/Class":
                    Setup.SetTypesDisplayMode("namespaces");
                    break;
                case "Class/Namespace":
                    Setup.SetTypesDisplayMode("types");
                    break;
                case "Full Name":
                    Setup.SetTypesDisplayMode("fulltypenames");
                    break;
            }
            if (!IsIndexAvailable(null) || TypeDisplayExists()) return;
            switch (Setup.TypesDisplayMode)
            {
                case "namespaces":
                    var namespaces = CurrentIndex.GetNamespaceDisplay();
                    DisplayNamespaceGrid(namespaces);
                    break;
                case "types":
                    DisplayTypesGrid(true);
                    break;
                default:
                    DisplayTypesGrid(false);
                    break;
            }
        }

        private bool TypeDisplayExists()
        {
            string currentMode;
            switch(Setup.TypesDisplayMode)
            {
                case "namespaces":
                    currentMode = "NamespaceTypeView";
                    break;
                case "types":
                    currentMode = "ReversedNameTypeView";
                    break;
                default:
                    currentMode = "NameTypeView";
                    break;
            }
            foreach (TabItem tabItem in MainTab.Items)
            {
                if (tabItem.Content is Grid)
                {
                    var grid = tabItem.Content as Grid;
                    if (grid.Name.StartsWith(currentMode)) return true;
                }
            }
            return false;
        }

        private void CreateDumpIndexClicked(object sender, RoutedEventArgs e)
        {
            if (IsIndexAvailable(null)) CloseCurrentIndex();
            var path = GuiUtils.SelectCrashDumpFile();
            if (path == null) return;
            DoCreateDumpIndex(path);
        }

        private async void DoCreateDumpIndex(string path)
        {
            Debug.Assert(path != null && System.IO.File.Exists(path));
            var dmpFileName = Path.GetFileNameWithoutExtension(path);
            // setup progress
            var indexingProgress = new IndexingProgress();

            indexingProgress.Init(this, GetFileInfo(path));

            SetStartTaskMainWindowState("Indexing: " + dmpFileName + ", please wait.");
            SetTitle(dmpFileName);

            var result = await Task.Factory.StartNew(() =>
                {
                string error;
                string indexPath;
                var indexer = new DumpIndexer(path);
                var ok = indexer.CreateDumpIndex2(_myVersion, indexingProgress.Progress, out indexPath, out error);
                return new Tuple<bool, string, string>(ok, error, indexPath);
                }, _dumpSTAScheduler);

            indexingProgress.Close(DumpFileMoniker.GetFilePath(-1, path, Constants.TxtIndexingInfoFilePostfix));

            Utils.ForceGcWithCompaction();
            SetEndTaskMainWindowState(result.Item1
                ? "Indexing: " + dmpFileName + " done."
                : "Indexing: " + dmpFileName + " failed.");

            if (!result.Item1)
            {
                SetTitle(dmpFileName + " [INDEXING FAILED]");
                ShowError(result.Item2);
                return;
            }
            Dispatcher.CurrentDispatcher.InvokeAsync(() => DoOpenDumpIndex(0, result.Item3));
        }

        private string GetFileInfo(string path)
        {
            Debug.Assert(System.IO.File.Exists(path));
            System.IO.FileInfo fi = new System.IO.FileInfo(path);
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
            sb.Append(path).AppendLine();
            sb.Append("File size: ").AppendLine(Utils.SizeString(fi.Length));
            var dt = fi.LastWriteTime.ToUniversalTime();
            sb.Append("Last write time (UTC): ").Append(dt.ToLongDateString()).Append(" ").AppendLine(dt.ToLongTimeString());
            return StringBuilderCache.GetStringAndRelease(sb);
        }

        private void OpenDumpIndexClicked(object sender, RoutedEventArgs e)
        {
            if (IsIndexAvailable(null)) CloseCurrentIndex();
            string path = null;
            if (sender != null && sender is MenuItem)
            {
                var menuItem = sender as MenuItem;
                if (menuItem.HasHeader && menuItem.Header is string && (menuItem.Header as string) == "Recent Indices")
                {
                    path = e.OriginalSource as string;
                }
            }
            if (path == null) path = GuiUtils.GetFolderPath(Setup.DumpsFolder);

            if (path == null) return;
            try
            {
                DoOpenDumpIndex(0, path);
            }
            catch (Exception ex)
            {
                ShowError(Utils.GetExceptionErrorString(ex));
            }
        }

        private async void DoOpenDumpIndex(int runtimeIndex, string path)
        {
            if (path == null) return;
            var progressHandler = new Progress<string>(MainStatusShowMessage);
            var progress = progressHandler as IProgress<string>;
            SetStartTaskMainWindowState("Opening index: " + Utils.GetPathLastFolder(path) + ", please wait...");

            var result = await Task.Factory.StartNew(() =>
            {
                string error;
                DumpIndex index = DumpIndex.OpenIndexInstanceReferences(_myVersion, path, runtimeIndex, out error, progress);
                KeyValuePair<string, KeyValuePair<string, int>[]>[] namespaces = null;
                progress.Report("OpenIndexInstanceReferences done");

                if (error == null && index != null)
                {
                    try
                    {
                        namespaces = index.GetNamespaceDisplay();
                        progress.Report("GetNamespaceDisplay done");
                    }
                    catch (Exception ex)
                    {
                        error = Utils.GetExceptionErrorString(ex);
                    }
                }
                return new Tuple<string, DumpIndex, KeyValuePair<string, KeyValuePair<string, int>[]>[]>(error, index, namespaces);
            }, _dumpSTAScheduler);

            try
            {
                progress.Report("Open index task done");
                SetEndTaskMainWindowState("Index: '" + DumpFileMoniker.GetMapName(path) +
                                          (result.Item1 == null ? "' is open." : "' open failed."));

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

                DisplayGeneralInfoGrid(CurrentIndex.DumpInfo);
                switch (Setup.TypesDisplayMode)
                {
                    case "namespaces":
                        var namespaces = CurrentIndex.GetNamespaceDisplay();
                        DisplayNamespaceGrid(namespaces);
                        break;
                    case "types":
                        DisplayTypesGrid(true);
                        break;
                    default:
                        DisplayTypesGrid(false);
                        break;
                }

                if (CurrentIndex.DeadlockFound)
                {
                    string error;
                    if (!DisplayDeadlock(CurrentIndex.Deadlock, out error))
                    {
                        ShowError(error);
                    }
                }
                Title = BaseTitle + Constants.BlackDiamondPadded + CurrentIndex.DumpFileName;
                RecentIndexList.Add(CurrentIndex.IndexFolder);

            }
            catch (Exception ex)
            {
                ShowError(Utils.GetExceptionErrorString(ex));
                return;
            }

        }

        public void SetTitle(string dmpName)
        {
            Title = BaseTitle + Constants.BlackDiamondPadded + dmpName;
        }

        private void CloseDumpIndexClicked(object sender, RoutedEventArgs e)
        {
            if (IsIndexAvailable(null)) CloseCurrentIndex();
        }

        private void IndexShowIndexingInfoClicked(object sender, RoutedEventArgs e)
        {
            if (!IsIndexAvailable("Show Indexing Info")) return;
            var path = CurrentIndex.GetFilePath(-1, Constants.TxtIndexingInfoFilePostfix);
            string error;
            if (!IndexingProgress.ShowIndexingInfo(this, path, out error))
            {
                ShowError(error);
            }
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
            DisplayListViewBottomGrid(result, Constants.BlackDiamond, "Modules",
                (string.IsNullOrWhiteSpace(title) ? "Modules" : title), null, path);
        }

        private async void IndexShowFinalizerQueueClicked(object sender, RoutedEventArgs e)
        {
            if (!IsIndexAvailable("Show Finalizer Queue")) return;

            SetStartTaskMainWindowState("Getting finalizer queue info, please wait...");

            // var finalizerInfo = await Task.Run(() => CurrentIndex.GetDisplayableFinalizationQueue());
            var finalizerInfo = await Task.Factory.StartNew(() =>
            {
                return CurrentIndex.GetDisplayableFinalizationQueue();
            }, DumpSTAScheduler);

            if (finalizerInfo.Item1 != null)
            {
                SetEndTaskMainWindowState("Getting finalizer queue info failed.");
                MessageBox.Show(finalizerInfo.Item1, "Action Failed", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            SetEndTaskMainWindowState("Getting finalizer queue info done.");
            DisplayFinalizerQueue(finalizerInfo.Item2);
        }

        private void IndexShowRootsClicked(object sender, RoutedEventArgs e)
        {
            if (!IsIndexAvailable("Show Roots")) return;

        }

        private async void IndexShowWeakReferencesClicked(object sender, RoutedEventArgs e)
        {
            if (!IsIndexAvailable("Show Weak References")) return;

            SetStartTaskMainWindowState("Getting WeakReference information, please wait...");

            var result = await Task.Factory.StartNew(() =>
            {
                string error;
                var info = CurrentIndex.GetWeakReferenceInfo(out error);
                return error == null ? info : new ListingInfo(error);
            }, DumpSTAScheduler);

            SetEndTaskMainWindowState(result.Error == null
                ? "Getting WeakReference information done."
                : "Getting WeakReference information failed.");

            if (result.Error != null)
            {
                ShowError(result.Error);
                return;
            }
            if (result.Items.Length < 1)
            {
                ShowInformation("Not Found", "WeakReference Information", "No WeakReference instances found", string.Empty);
                return;
            }

            DisplayWeakReferenceGrid(result, Constants.BlackDiamond, ReportNameWeakReferenceInfo, ReportTitleWeakReferenceInfo);
        }

        private void IndexShowBlockingThreadsClicked(object sender, RoutedEventArgs e)
        {
            if (!IsIndexAvailable("Show Blocks and Threads Graph")) return;

            string error;
            if (!CurrentIndex.LoadThreadBlockInfo(out error))
            {
                ShowError(error);
                return;
            }

            if (!DisplayThreadBlockMap(CurrentIndex.ThreadBlockgraph, out error))
            {
                ShowError(error);
            }
            return;
        }

        private void IndexShowThreadsClicked(object sender, RoutedEventArgs e)
        {
            if (!IsIndexAvailable("Cannot view threads, an index is not opened.")) return;
            Dispatcher.CurrentDispatcher.InvokeAsync(ExecuteGetThreadinfos);
        }

        private void IndexGetSizeInformationClicked(object sender, RoutedEventArgs e)
        {
            GetSizeInformation(false);
        }

        private void IndexGetBaseSizeInformationClicked(object sender, RoutedEventArgs e)
        {
            GetSizeInformation(true);
        }

        private async void GetSizeInformation(bool baseSize)
        {
            if (!IsIndexAvailable("Get Size Information")) return;

            SetStartTaskMainWindowState("Getting type size information: , please wait...");
            // var result = await Task.Run(() => CurrentIndex.GetAllTypesSizesInfo(baseSize));
            var result = await Task.Factory.StartNew(() =>
            {
                return CurrentIndex.GetAllTypesSizesInfo(baseSize);
            }, DumpSTAScheduler);

            SetEndTaskMainWindowState(result.Error == null
                ? "Getting type size information done."
                : "Getting sizes info failed.");

            if (result.Error != null)
            {
                MessageBox.Show(result.Error, "Getting Sizes Info Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DisplayListViewBottomGrid(result, Constants.BlackDiamond,
                baseSize ? ReportNameBaseSizeInfo : ReportNameSizeInfo,
                baseSize ? ReportTitleBaseSizeInfo : ReportTitleSizeInfo);
        }

        private long _minStringUsage = 1;

        private async void IndexStringUsageClicked(object sender, RoutedEventArgs e)
        {
            if (!IsIndexAvailable("Get String Usage")) return;

            bool addGenerationInfo = false;
            MenuItem menuItem = sender as MenuItem;
            Debug.Assert(menuItem != null);

            if ((menuItem.Header as string).Contains("Generations")) addGenerationInfo = true;

            if (!GetUserEnteredNumber("Minimum String Reference Count", "Enter number, hex format not allowed:", out _minStringUsage)) return;

            if (CurrentIndex.AreStringDataFilesAvailable())
                SetStartTaskMainWindowState("Getting string usage. Please wait...");
            else
                SetStartTaskMainWindowState(
                    "Getting string usage, string cache has to be created, it will take a while. Please wait...");

            var taskResult = await Task.Factory.StartNew(() =>
            {
                return CurrentIndex.GetStringStats((int)_minStringUsage, addGenerationInfo);
            }, DumpSTAScheduler);

            SetEndTaskMainWindowState(taskResult.Error == null
                ? "Collecting strings of: " + CurrentIndex.DumpFileName + " done."
                : "Collecting strings of: " + CurrentIndex.DumpFileName + " failed.");

            if (taskResult.Error != null)
            {
                MainStatusShowMessage("Getting Strings: " + CurrentIndex.DumpFileName + ", failed.");
                MessageBox.Show(taskResult.Error, "Getting Strings Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SWC.MenuItem[] menuItems = new SWC.MenuItem[]
            {
                new SWC.MenuItem {Header = "Copy List Row"},
                new SWC.MenuItem {Header = "GC Generation Distribution"},
                new SWC.MenuItem {Header = "Get References"},
                new SWC.MenuItem {Header = "Get References of String Prefix"},
                new SWC.MenuItem {Header = "Get Size of Strings with Prefix"}
            };
            DisplayListViewBottomGrid(taskResult, Constants.BlackDiamond, ReportNameStringUsage, ReportTitleStringUsage,
                menuItems);
        }

        #region InstanceInfo

        public bool IsValidHeapAddress(ulong addr)
        {
            if (CurrentIndex != null)
            {
                return CurrentIndex.GetInstanceIndex(addr) != Constants.InvalidIndex;
            }
            return false;
        }

        private ulong GetInstanceAddressFromUser(string title)
        {
            if (!IsIndexAvailable(title)) return Constants.InvalidAddress;
            ulong addr;
            if (!GetUserEnteredAddress("Instance Address", "Enter instance address, if not hex format prefix with n/N.", out addr)) return Constants.InvalidAddress;
            return addr;
        }

        private void ExecuteInstanceValue(object sender, ExecutedRoutedEventArgs e)
        {
            ulong addr = GetInstanceAddressFromUser("Instance Information");
            if (addr == Constants.InvalidAddress) return;
            var msg = "Getting object value at: " + Utils.RealAddressString(addr);
            Dispatcher.CurrentDispatcher.InvokeAsync(() => ExecuteInstanceValueQuery(msg, addr));
        }

        private void CanExecuteInstanceValue(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void ExecuteInstanceReferences(object sender, ExecutedRoutedEventArgs e)
        {
            ulong addr = GetInstanceAddressFromUser("Instance Information");
            if (addr == Constants.InvalidAddress) return;
            var msg = "Getting object references at: " + Utils.RealAddressString(addr);
            Dispatcher.CurrentDispatcher.InvokeAsync(() => ExecuteReferenceQuery(addr));
        }

        private void CanExecuteInstanceReferences(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void ExecuteInstanceHierarchy(object sender, ExecutedRoutedEventArgs e)
        {
            ulong addr = GetInstanceAddressFromUser("Instance Information");
            if (addr == Constants.InvalidAddress) return;
            var msg = "Getting object hierarchy at: " + Utils.RealAddressString(addr);
            Dispatcher.CurrentDispatcher.InvokeAsync(() => ExecuteInstanceHierarchyQuery("Get instance hierarchy " + Utils.AddressStringHeader(addr),
                                                                                          addr,
                                                                                          Constants.InvalidIndex));
        }

        private void CanExecuteInstanceHierarchy(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void AddressListDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            LbGetInstValueClicked(sender, null);
        }

        #endregion InstanceInfo

        private async void IndexCompareSizeInformationClicked(object sender, RoutedEventArgs e)
        {
            if (!IsIndexAvailable("Compare Size Information")) return;
            var path = GuiUtils.SelectCrashDumpFile();
            if (path == null) return;
            SetStartTaskMainWindowState("Getting type sizes to compare. Please wait...");

            var taskResult = await Task.Factory.StartNew(() =>
            {
                return CurrentIndex.CompareTypesWithOther(path);
            }, DumpSTAScheduler);

            SetEndTaskMainWindowState(taskResult.Error == null
                ? "Collecting sizes of: " + CurrentIndex.DumpFileName + " done."
                : "Collecting sizes of: " + CurrentIndex.DumpFileName + " failed.");

            DisplayListViewBottomGrid(taskResult, Constants.BlackDiamond, ReportNameSizeDiffs, ReportTitleSizeDiffs);
        }

        private async void IndexCompareStringInformationClicked(object sender, RoutedEventArgs e)
        {
            if (!IsIndexAvailable("Compare String _instances Information")) return;
            var path = GuiUtils.SelectCrashDumpFile();
            if (path == null) return;
            SetStartTaskMainWindowState("Getting string instances to compare. Please wait...");

            var taskResult = await Task.Factory.StartNew(() =>
            {
                return CurrentIndex.CompareStringsWithOther(path);
            }, DumpSTAScheduler);

            SetEndTaskMainWindowState(taskResult.Error == null
                ? "Collecting strings of: " + CurrentIndex.DumpFileName + " done."
                : "Collecting strings of: " + CurrentIndex.DumpFileName + " failed.");

            if (taskResult.Error != null)
            {
                ShowError(taskResult.Error);
                return;
            }

            DisplayListViewBottomGrid(taskResult, Constants.BlackDiamond, ReportNameSizeDiffs, ReportTitleSizeDiffs);
        }

        private void IndexViewCopyIndexPathClicked(object sender, RoutedEventArgs e)
        {
            if (!IsIndexAvailable("Copy the index path.")) return;
            string path = CurrentIndex.IndexFolder;
            Clipboard.SetText(path);
            MainStatusShowMessage("Copied to clipboard: " + path);
        }

        public void RecentIndicesClicked(object sender, RoutedEventArgs e)
        {
            if (IsIndexAvailable(null)) CloseCurrentIndex();
            var menuItem = sender as MenuItem;
            Debug.Assert(menuItem != null);
            var fileInfo = menuItem.Header as FileInfo;
            if (fileInfo != null)
            {
                Dispatcher.CurrentDispatcher.InvokeAsync(() => DoOpenDumpIndex(0, fileInfo.FilePath));
            }
        }

        #endregion Index

        #region AdhocQueries

        private async void AhqSelectDumpClicked(object sender, RoutedEventArgs e)
        {
            string error = null;
            string path = string.Empty;
            try
            {
                path = GuiUtils.SelectCrashDumpFile();
                if (path == null) return;
                if (CurrentAdhocDump != null && string.Compare(CurrentAdhocDump.DumpPath,path,StringComparison.OrdinalIgnoreCase)==0)
                {
                    MessageBox.Show(path + Environment.NewLine + "is already opened.", "Crash Dump Already Opened",MessageBoxButton.OK,MessageBoxImage.Information);
                    return;
                }

                ClrtDump dump = await Task.Factory.StartNew( ()=> ClrtDump.OpenDump(path, out error), _adhocSTAScheduler);

                if (dump != null)
                {
                    if (CurrentAdhocDump != null) CurrentAdhocDump.Dispose();
                    CurrentAdhocDump = dump;
                    AhqCurrentDump.Header = System.IO.Path.GetFileName(CurrentAdhocDump.DumpPath);
                    AhqCurrentDumpPath.Header = CurrentAdhocDump.DumpPath;
                }
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
            }
        }

        private async void StringUsageClicked(object sender, RoutedEventArgs e)
        {
            string error;
            var dumpFilePath = GuiUtils.SelectCrashDumpFile();
            if (dumpFilePath == null) return;
            var progressHandler = new Progress<string>(MainStatusShowMessage);
            var progress = progressHandler as IProgress<string>;

            if (!GetUserEnteredNumber("Minimum String Reference Count", "Enter number, hex format not allowed:", out _minStringUsage)) return;

            SetStartTaskMainWindowState("Getting string usage. Please wait...");

            //var taskResult = await Task.Run(() =>
            //{
            //    var dmp = ClrtDump.OpenDump(dumpFilePath, out error);
            //    if (dmp == null)
            //    {
            //        return new ListingInfo(error);
            //    }
            //    using (dmp)
            //    {
            //        var heap = dmp.Runtime.Heap;
            //        var addresses = ClrtDump.GetTypeAddresses(heap, "System.String", out error);
            //        if (error != null)
            //        {
            //            return new ListingInfo(error);
            //        }
            //        var strStats = ClrtDump.GetStringStats(heap, addresses, dumpFilePath, out error);
            //        return strStats.GetGridData((int)_minStringUsage, out error);
            //    }
            //});
            var taskResult = await Task.Factory.StartNew(() =>
            {
                var dmp = ClrtDump.OpenDump(dumpFilePath, out error);
                if (dmp == null)
                {
                    return new ListingInfo(error);
                }
                using (dmp)
                {
                    var heap = dmp.Runtime.Heap;
                    var addresses = ClrtDump.GetTypeAddresses(heap, "System.String", out error);
                    if (error != null)
                    {
                        return new ListingInfo(error);
                    }
                    var strStats = ClrtDump.GetStringStats(heap, addresses, dumpFilePath, out error);
                    return strStats.GetGridData((int)_minStringUsage, out error);
                }
            }, DumpSTAScheduler);

            SetEndTaskMainWindowState("Getting Strings: " + System.IO.Path.GetFileName(dumpFilePath) +
                                      (taskResult.Error == null ? ", done." : ", failed."));
            if (taskResult.Error != null)
            {
                MessageBox.Show(taskResult.Error, "Getting Strings Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SWC.MenuItem[] menuItems = new SWC.MenuItem[]
            {
                new SWC.MenuItem {Header = "Copy List Row"},
                new SWC.MenuItem {Header = "GC Generation Distribution"},
                new SWC.MenuItem {Header = "Get References"},
                new SWC.MenuItem {Header = "Get References of String Prefix"}
            };
            DisplayListViewBottomGrid(taskResult, Constants.BlackDiamond, ReportNameStringUsage, ReportTitleStringUsage,
                menuItems);
            RecentAdhocList.Add(dumpFilePath);
        }

        private async void AhqTypeCountClicked(object sender, RoutedEventArgs e)
        {
            string error;
            var dumpFilePath = GuiUtils.SelectCrashDumpFile();
            if (dumpFilePath == null) return;
            var progressHandler = new Progress<string>(MainStatusShowMessage);
            var progress = progressHandler as IProgress<string>;
            string typeName;
            if (!GetDlgString("Get Type Count", "EnterTypeName", " ", out typeName)) return;
            SetStartTaskMainWindowState("Getting type count. Please wait...");
            //var taskResult = await Task.Run(() =>
            //{
            //    var dmp = ClrtDump.OpenDump(dumpFilePath, out error);
            //    if (dmp == null)
            //    {
            //        return new KeyValuePair<string, int>(error, -1);
            //    }
            //    using (dmp)
            //    {
            //        var heap = dmp.Runtime.Heap;
            //        var count = ClrtDump.GetTypeCount(heap, typeName, out error);
            //        return new KeyValuePair<string, int>(error, count);
            //    }
            //});
            var taskResult = await Task.Factory.StartNew(() =>
            {
                var dmp = ClrtDump.OpenDump(dumpFilePath, out error);
                if (dmp == null)
                {
                    return new KeyValuePair<string, int>(error, -1);
                }
                using (dmp)
                {
                    var heap = dmp.Runtime.Heap;
                    var count = ClrtDump.GetTypeCount(heap, typeName, out error);
                    return new KeyValuePair<string, int>(error, count);
                }
            }, DumpSTAScheduler);

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
            var dumpFilePath = GuiUtils.SelectCrashDumpFile();
            if (dumpFilePath == null) return;
            ulong addr;
            if (!GetUserEnteredAddress("Enter an array address.", out addr)) return;
            var progressHandler = new Progress<string>(MainStatusShowMessage);
            var progress = progressHandler as IProgress<string>;
        }

        //private async void AhqOpenInstanceRefsClicked(object sender, RoutedEventArgs e)
        //{
        //	var dumpFilePath = SelectCrashDumpFile();
        //	if (dumpFilePath == null) return;

        //	var progressHandler = new Progress<string>(MainStatusShowMessage);
        //	var progress = progressHandler as IProgress<string>;
        //	var dmpFileName = Path.GetFileName(dumpFilePath);
        //	SetStartTaskMainWindowState("Indexing: " + dmpFileName + ", please wait.");

        //	var result = await Task.Run(() =>
        //	{
        //		string error;
        //		var index = DumpIndex.OpenIndexInstanceReferences(_myVersion, dumpFilePath, 0, out error, progress);
        //		if (index != null)
        //		{
        //			if (CurrentAdhocIndex != null)
        //			{
        //				CurrentAdhocIndex.Dispose();
        //			}
        //			CurrentAdhocIndex = index;
        //		}
        //		return new Tuple<bool, string>(index!=null, error);
        //	});

        //	Utils.ForceGcWithCompaction();
        //	SetEndTaskMainWindowState(result.Item1
        //		? "Indexing: " + dmpFileName + " done."
        //		: "Indexing: " + dmpFileName + " failed.");

        //	if (!result.Item1)
        //	{
        //		ShowError(result.Item2);
        //		return;
        //	}

        //}


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
                Debug.Assert(entries != null);
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
                        Dispatcher.CurrentDispatcher.InvokeAsync(
                            () => ExecuteGenerationQuery("Getting generation distribution", reportTitle, str, grid));
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

                                var task = Task<ValueTuple<string, AncestorNode>>.Factory.StartNew(() => CurrentIndex.GetTypesWithSpecificStringFieldListing(selStr));
                                task.Wait();

                                SetEndTaskMainWindowState(task.Result.Item1 == null
                                    ? "Searching for instances with string field done."
                                    : "Searching for instances with string field failed.");

                                if (task.Result.Item1 != null)
                                {
                                    //Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                                    //	ShowInformation("Empty List", "Search for string references failed",
                                    //			"References not found for string: " + selStr, null));
                                    Dispatcher.CurrentDispatcher.InvokeAsync(() => ShowError(task.Result.Item1));
                                    return;
                                }
                                DisplayTypeAncestorsGrid(task.Result.Item2);

                                //{

                                //SWC.MenuItem[] menuItems = new SWC.MenuItem[]
                                //{
                                //	new SWC.MenuItem {Header = "Copy List Row"},
                                //	new SWC.MenuItem {Header = "GC Generation Distribution"},
                                //	new SWC.MenuItem {Header = "Get References"},
                                //};
                                //DisplayListViewBottomGrid(task.Result, Constants.BlackDiamond, ReportNameTypesWithString,
                                //	ReportTitleSTypesWithString, menuItems);
                                break;
                            case ReportTitleSTypesWithString:
                                selStr = entries[ndx].Second;
                                var addr = Convert.ToUInt64(selStr, 16);

                                var report = CurrentIndex.GetParentReferencesReport(addr, 4);
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
                            var len = CurrentIndex.GetSizeOfStringsWithPrefix(sel, out error);
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

        #region Extras

        // TODO JRD
        private void ExtrasGetIpAddressValueClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                string value;
                if (!GetDlgString("Get IPv4 Address Value", "Enter IPv4 address (ex.: 192.154.1.4)", "", out value)) return;
                value = value.Trim();
                var result = Utils.GetIpAddressValue(value);
                Clipboard.SetText(result.ToString());
                MainStatusShowMessage("IPv4 address: " + value + ", long value is: " + result + ". The value is copied to the clipboard.");
            }
            catch (Exception ex)
            {
                ShowError(Utils.GetExceptionErrorString(ex));
            }
        }

        // TODO JRD
        private void ExtrasGetIpValueAddressClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                string value;
                if (!GetDlgString("Get IPv4 Address from Value", "Enter IPv4 address value (ex.: 67214016)", "", out value)) return;
                value = value.Trim();
                long val = Int64.Parse(value);
                var result = Utils.GetIpAddress(val);
                Clipboard.SetText(result);
                MainStatusShowMessage("IPv4 address of '" + value + "' is: " + result + ". The address is copied to the clipboard.");
            }
            catch (Exception ex)
            {
                ShowError(Utils.GetExceptionErrorString(ex));
            }
        }

        private async void ExtrasTestInstanceValuesClicked(object sender, RoutedEventArgs e)
        {
            if (!IsIndexAvailable("Test Instance Values")) return;
            SetStartTaskMainWindowState("Testing instance values, probably will take very long time. Please wait.");
            string error=null;

            var result = await Task.Factory.StartNew(() =>
            {
                return CurrentIndex.TestInstanceValues(out error);
            }, _dumpSTAScheduler);

            SetEndTaskMainWindowState(error == null
                ? "Testing instance values done."
                : "Testing instance values failed.");
        }

        #endregion Extras

        #region File Reports

        private async void FileReportClicked(object sender, RoutedEventArgs e)
        {
            Grid grid = GetReportGrid(true);
            if (grid == null) return;
            DumpFileMoniker dmpInfo;
            ListView listView;
            string gridName = Utils.GetNameWithoutId(grid.Name);
            switch (gridName)
            {
                case "IndexSizesInfo":
                    dmpInfo = (grid.Tag as Tuple<DumpFileMoniker, ulong>).Item1;
                    Debug.Assert(dmpInfo != null);
                    listView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, "TopListView");
                    Debug.Assert(listView != null);
                    var dataAry = listView.ItemsSource as sextuple<int, ulong, ulong, ulong, ulong, string>[];
                    Debug.Assert(dataAry != null);
                    var sortBy = listView.Tag as string;
                    Debug.Assert(sortBy != null);
                    var reportPath = dmpInfo.OutputFolder + System.IO.Path.DirectorySeparatorChar + dmpInfo.DumpFileName +
                                     ".TYPESIZES." + sortBy + ".txt";
                    break;
                case "IndexStringUsage":
                    dmpInfo = grid.Tag as DumpFileMoniker;
                    Debug.Assert(dmpInfo != null);
                    listView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, "TopListView");
                    Debug.Assert(listView != null);
                    var datAry = listView.ItemsSource as StringStatsDispEntry[];
                    Debug.Assert(datAry != null);
                    var repPath = dmpInfo.OutputFolder + System.IO.Path.DirectorySeparatorChar + "ALLSTRINGUSAGE.txt";
                    string error;
                    StringStatsDispEntry.WriteShortReport(datAry, repPath, "String usage in: " + CurrentIndex.DumpFileName,
                        datAry.Length,
                        new string[] { "Count" }, null, out error);
                    break;
                case ReportNameSizeDiffs:
                    dmpInfo = (grid.Tag as Tuple<string, DumpFileMoniker>).Item2;
                    Debug.Assert(dmpInfo != null);
                    listView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, "TopListView");
                    Debug.Assert(listView != null);

 
                    break;
            }

            var listingView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, "TopListView");
            if (listingView == null)
            {
                MainStatusShowMessage("Cannot write report, the current tab reporting is not supported.");
                return;
            }

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

            //var taskResult = await Task.Run(() =>
            //{
            //    string error;
            //    var ok = ReportFile.WriteReport(outpath, gridName, gridInfo.Item1, colSortInfo.Item1.ColInfos, descrLines, data,
            //        out error);
            //    return new Tuple<string, string>(error, outpath);
            //});
            var taskResult = await Task.Factory.StartNew(() =>
            {
                string error;
                var ok = ReportFile.WriteReport(outpath, gridName, gridInfo.Item1, colSortInfo.Item1.ColInfos, descrLines, data,
                    out error);
                return new Tuple<string, string>(error, outpath);
            }, DumpSTAScheduler);

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
                        sw.WriteLine("Grand total of all instances base sizes: " + Utils.SizeString(info.Item2) + ", (" +
                                     Utils.FormatBytes((long)info.Item2) + ")");
                        sw.WriteLine(
                            "Columns:  'Count', 'Total Size', 'Max Size', 'Min Size', 'Avg Size', 'Type'    ...all sizes in bytes.");
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
                    StringStatsDispEntry.WriteShortReport(datAry, repPath, "String usage in: " + CurrentIndex.DumpFileName, 100,
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
                case ListingGrid:
                    listView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, ListingGridView);
                    Tuple<ListingInfo, string> pair = listView.Tag as Tuple<ListingInfo, string>;
                    string lstPath = DumpFileMoniker.GetFileDistinctPath(CurrentIndex.AdhocFolder, pair.Item2 + ".txt");
                    ListingInfo.DumpListing(lstPath, pair.Item1, pair.Item2, out error, 100);
                    MainStatusShowMessage(error == null ? "Report written: " + lstPath : "Report writing failed: " + Utils.GetShorterStringRemoveNewlines(error, 40));
                    if (error != null) ShowError(error);
                    break;
                default:
                    MainStatusShowMessage("Cannot write report, the current tab reporting is not supported.");
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
            try
            {
                MDRDeskSetup dlg = new MDRDeskSetup() { Owner = Window.GetWindow(this) };
                dlg.ShowDialog();
                switch (Setup.TypesDisplayMode)
                {
                    case "namespaces":
                        TypeDisplayMode.SelectedIndex = 0;
                        break;
                    case "types":
                        TypeDisplayMode.SelectedIndex = 1;
                        break;
                    case "fulltypenames":
                        TypeDisplayMode.SelectedIndex = 2;
                        break;
                    default:
                        TypeDisplayMode.SelectedIndex = 0;
                        break;

                }
            }
            catch (Exception ex)
            {
                ShowError(Utils.GetExceptionErrorString(ex));
            }
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
            MainStatusShowMessage(message + "        tm:" + Utils.StopAndGetDurationString(_taskDurationStopwatch));
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

        private void ListingInfoListViewHeaderClick(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader column = e.OriginalSource as GridViewColumnHeader;
            if (column == null) return;
            if (column.Role == GridViewColumnHeaderRole.Padding) return;

            var mapListView = sender as ListView;
            if (mapListView == null) return;
            if (!(mapListView.Tag != null && mapListView.Tag is Tuple<ListingInfo, string>)) return;

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
            }
        }

        #endregion GUI Helpers

        #region Dialogs

        private bool GetDlgString(string title, string descr, string defValue, out string str)
        {
            str = null;
            InputStringDlg dlg = new InputStringDlg(descr, defValue ?? " ") { Title = title, Owner = Window.GetWindow(this) };
            var dlgResult = dlg.ShowDialog();
            if (dlg.DialogResult != null && (!dlg.DialogResult.Value || string.IsNullOrWhiteSpace(dlg.Answer))) return false;
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
            var cleanStr = Utils.CleanupHexString(str);
            if (cleanStr==string.Empty || !ulong.TryParse(cleanStr, System.Globalization.NumberStyles.AllowHexSpecifier, null, out addr))
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

        #region context menus

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
                sb.AppendLine(Utils.RealAddressString((ulong)item));
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
                MainStatusShowMessage("No address is copied to Clipboard. Address list is empty.");
                return;
            }

            var sb = StringBuilderCache.Acquire(maxLen * 18);
            for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
            {
                sb.AppendLine(Utils.RealAddressString(addresses[i]));
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

        private void AddrLstGetInstSizesClicked(object sender, RoutedEventArgs e)
        {
            ulong addr = GetAddressFromList(sender);
            if (addr == Constants.InvalidAddress) return;
            GetInstSizes(addr);
        }

        private async void GetInstSizes(ulong addr)
        {
            if (addr == Constants.InvalidAddress) return;
            SetStartTaskMainWindowState("Getting instance[" + Utils.RealAddressString(addr) + "], please wait...");

            //var result = await Task.Run(() =>
            //{
            //    string error;
            //    KeyValuePair<uint, uint> info = CurrentIndex.GetInstanceSizes(addr, out error);
            //    return new Tuple<string, KeyValuePair<uint, uint>>(error, info);
            //});
            var result = await Task.Factory.StartNew(() =>
            {
                string error;
                KeyValuePair<uint, uint> info = CurrentIndex.GetInstanceSizes(addr, out error);
                return new Tuple<string, KeyValuePair<uint, uint>>(error, info);
            }, DumpSTAScheduler);

            if (result.Item1 != null)
            {
                ShowError(result.Item1);
                SetEndTaskMainWindowState("Getting instance [" + Utils.RealAddressString(addr) + "] sizes failed.");
                return;
            }
            SetEndTaskMainWindowState("Instance [" + Utils.RealAddressString(addr)
                + "] sizes, base: " + Utils.LargeNumberString((ulong)result.Item2.Key)
                + ",  total: " + Utils.LargeNumberString((ulong)result.Item2.Value));
        }

        private ListBox GetTypeAddressesListBox(object sender)
        {
            string listName = null;
            if (sender is ListBox)
            {
                return (ListBox)sender;
            }
            var menuItem = sender as MenuItem;
            Debug.Assert(menuItem != null);
            var contextMenu = menuItem.Parent as ContextMenu;
            Debug.Assert(contextMenu != null);
            if (contextMenu.Tag is ListBox) return contextMenu.Tag as ListBox;

            var grid = GetCurrentTabGrid();
            string gridName = Utils.GetNameWithoutId(grid.Name);
            switch (gridName)
            {
                case GridNameTypeView:
                case GridReversedNameTypeView:
                    listName = "lbTypeNameAddresses";
                    break;
                case GridNameNamespaceTypeView:
                    listName = "lbTypeNamespaceAddresses";
                    break;
                case WeakReferenceViewGrid:
                    listName = "WeakReferenceObjectAddresses";
                    break;
                case AncestorTreeViewGrid:
                    listName = "AncestorAddressList";
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

        private void StackObjectCopyAddressClicked(object sender, RoutedEventArgs e)
        {
            ListView lv = GetContextMenuListView(sender);
            Debug.Assert(lv != null);
            var selections = lv.SelectedItems;
            if (selections == null || selections.Count < 1)
            {
                MainStatusShowMessage("No address is copied to Clipboard. No item(s) are selected.");
                return;
            }
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);

            foreach (var selection in selections)
            {
                string s = selection.ToString();
                sb.Append("0x00"); // clean byte first
                int pos = 4;
                while (!Char.IsWhiteSpace(s[pos]))
                {
                    sb.Append(s[pos]);
                    ++pos;
                }
                sb.AppendLine();
            }
            string result = StringBuilderCache.GetStringAndRelease(sb);
            if (selections.Count == 1) result = result.Trim();

            Clipboard.SetText(result);
            if (selections.Count == 1)
            {
                MainStatusShowMessage("Address: " + result + " is copied to Clipboard.");
                return;
            }
            MainStatusShowMessage(selections.Count + " addresses are copied to Clipboard.");

        }

        private void StackObjectCopyAddressAllClicked(object sender, RoutedEventArgs e)
        {
            ListView lv = GetContextMenuListView(sender);
            Debug.Assert(lv != null);
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);

            foreach (var item in lv.Items)
            {
                string s = item.ToString();
                sb.Append("0x00"); // clean byte first
                int pos = 4;
                while (!Char.IsWhiteSpace(s[pos]))
                {
                    sb.Append(s[pos]);
                    ++pos;
                }
                sb.AppendLine();
            }
            string result = StringBuilderCache.GetStringAndRelease(sb);
            if (lv.Items.Count == 1) result = result.Trim();

            Clipboard.SetText(result);
            if (lv.Items.Count == 1)
            {
                MainStatusShowMessage("Address: " + result + " is copied to Clipboard.");
                return;
            }
            MainStatusShowMessage(lv.Items.Count + " addresses are copied to Clipboard.");

        }

        private void StackObjectGetInstSizesClicked(object sender, RoutedEventArgs e)
        {
            ulong addr = GetStackObjectAddress(sender);
            if (addr == Constants.InvalidAddress) return;
            GetInstSizes(addr);
        }

        private void StackObjectInstValueClicked(object sender, RoutedEventArgs e)
        {
            ulong addr = GetStackObjectAddress(sender);
            if (addr == Constants.InvalidAddress) return;
            Dispatcher.CurrentDispatcher.InvokeAsync(() => ExecuteInstanceValueQuery("Getting stack object value: " + Utils.AddressString(addr), addr));
        }

        private void StackObjectDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            StackObjectInstValueClicked(sender, null);
        }

        private void StackObjectGetInstHierarchyClicked(object sender, RoutedEventArgs e)
        {
            ulong addr = GetStackObjectAddress(sender);
            if (addr == Constants.InvalidAddress) return;
            Dispatcher.CurrentDispatcher.InvokeAsync(() => ExecuteInstanceHierarchyQuery("Getting stack object hierarchy: " + Utils.AddressString(addr), addr, Constants.InvalidIndex));
        }

        private ulong GetStackObjectAddress(object sender)
        {
            ListView lv = GetContextMenuListView(sender);
            Debug.Assert(lv != null);
            var selections = lv.SelectedItems;
            if (selections == null || selections.Count < 1)
            {
                MainStatusShowMessage("No address is copied to Clipboard. No item(s) are selected.");
                return Constants.InvalidAddress;
            }
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
            string s = selections[0].ToString();
            sb.Append("0x00"); // clean byte first
            int pos = 4;
            while (!Char.IsWhiteSpace(s[pos]))
            {
                sb.Append(s[pos]);
                ++pos;
            }
            string result = StringBuilderCache.GetStringAndRelease(sb);
            ulong addr = Convert.ToUInt64(result, 16);
            return addr;
        }

        private ListView GetContextMenuListView(object sender)
        {
            if (sender is ListView) return (ListView)sender;
            var menuItem = sender as MenuItem;
            Debug.Assert(menuItem != null);
            return GetListView(menuItem);
        }

        private ListView GetListView(MenuItem menuItem)
        {
            var contextMenu = menuItem.Parent as ContextMenu;
            Debug.Assert(contextMenu != null);
            return (contextMenu.Tag is ListView) ? contextMenu.Tag as ListView : null;
        }

        #region TypeValuesReportContextMenu


        private listing<string> GetypeValuesReportRow()
        {
            try
            {
                var lstView = GetCurrentListView(TypeValuesReportView);
                Debug.Assert(lstView != null);
                var ndx = lstView.SelectedIndex;
                if (ndx < 0)
                {
                    // TODO JRD
                    return listing<string>.Empty;
                }

                var entries = lstView.ItemsSource as listing<string>[];
                return entries[ndx];
            }
            catch (Exception ex)
            {
                var error = Utils.GetExceptionErrorString(ex);
                MessageBox.Show(error, "GetypeValuesReportRow", MessageBoxButton.OK, MessageBoxImage.Error);
                return listing<string>.Empty;
            }

        }

        private void TypeValuesReportCopyRowClicked(object sender, RoutedEventArgs e)
        {
            var row = GetypeValuesReportRow();
            if (row.IsEmpty) return;
            var rowStr = GetListingRow(row);
            Clipboard.SetText(rowStr);
            MessageBox.Show(rowStr, "Copied to clipboard.", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void TypeValuesReportCopyAddressClicked(object sender, RoutedEventArgs e)
        {
            var row = GetypeValuesReportRow();
            if (row.IsEmpty) return;
            ulong addr = Utils.GetAddressValue(row.GetItem(0));
            string addrStr = Utils.RealAddressString(addr);
            Clipboard.SetText(addrStr);
            MessageBox.Show(addrStr, "Copied to clipboard.", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void TypeValuesReportGetParentRefsClicked(object sender, RoutedEventArgs e)
        {
            var row = GetypeValuesReportRow();
            if (row.IsEmpty) return;
            ulong addr = Utils.GetAddressValue(row.GetItem(0));
            DisplayInstanceParentReferences(addr);
        }

        private void TypeValuesReportGetInstSizesClicked(object sender, RoutedEventArgs e)
        {
            var row = GetypeValuesReportRow();
            if (row.IsEmpty) return;
            ulong addr = Utils.GetAddressValue(row.GetItem(0));
            GetInstSizes(addr);
        }

        private void TypeValuesReportGetInstValueClicked(object sender, RoutedEventArgs e)
        {
            var row = GetypeValuesReportRow();
            if (row.IsEmpty) return;
            ulong addr = Utils.GetAddressValue(row.GetItem(0));
            var msg = "Getting object value at: " + Utils.RealAddressString(addr);
            Dispatcher.CurrentDispatcher.InvokeAsync(() => ExecuteInstanceValueQuery(msg, addr));
        }

        private void TypeValuesReportGetInstHierarchyClicked(object sender, RoutedEventArgs e)
        {
            var row = GetypeValuesReportRow();
            if (row.IsEmpty) return;
            ulong addr = Utils.GetAddressValue(row.GetItem(0));
            Dispatcher.CurrentDispatcher.InvokeAsync(() => ExecuteInstanceHierarchyQuery("Get instance hierarchy " + Utils.AddressStringHeader(addr), addr, Constants.InvalidIndex));
        }

        private void TypeValuesReportViewMemoryClicked(object sender, RoutedEventArgs e)
        {
            var row = GetypeValuesReportRow();
            if (row.IsEmpty) return;
            ulong addr = Utils.GetAddressValue(row.GetItem(0));
            ShowMemoryViewWindow(addr);
        }

        #endregion TypeValuesReportContextMenu

        #endregion context menus

        //private void RecentDumpSelectionClicked(object sender, RoutedEventArgs e)
        //{
        //	int a = 0;
        //}

        private async void GenerateSizeDetailsReport(object sender, RoutedEventArgs e) // TODO JRD -- display as ListView (listing)
        {
            var lbTypeNames = GetTypeNameListBox(sender);
            var selectedItem = lbTypeNames.SelectedItems[0];
            if (selectedItem == null)
            {
                MessageBox.Show("No type is selected!", "Action Aborted", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
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

            var result = await Task.Factory.StartNew(() =>
            {
                string error;
                var info = CurrentIndex.GetTypeSizeDetails(typeId, out error);
                if (info == null)
                    return new Tuple<string, string, string>(error, null, null);

                var rep = Reports.DumpTypeSizeDetails(CurrentIndex.OutputFolder, typeName, info.Item1, info.Item2, info.Item3, info.Item4, info.Item5,
                    out error);

                return new Tuple<string, string, string>(null, rep.Item1, rep.Item2);
            }, DumpSTAScheduler);


            SetEndTaskMainWindowState("Getting size details for: '" + baseTypeName + "', done");

        }

        private async void GetTypeValuesReportClicked(object sender, RoutedEventArgs e)
        {

            var lbTypeNames = GetTypeNameListBox(sender);
            var selectedItem = lbTypeNames.SelectedItems[0];
            if (selectedItem == null)
            {
                MessageBox.Show("No type is selected!", "Action Aborted", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            string typeName = null;
            int typeId = Constants.InvalidIndex;
            if (selectedItem is KeyValuePair<string, int>) // namespace display
            {
                typeId = ((KeyValuePair<string, int>)selectedItem).Value;
                typeName = ((KeyValuePair<string, int>)selectedItem).Key;
            }
            var baseTypeName = Utils.BaseTypeName(typeName); // get type name without namespace

            SetStartTaskMainWindowState("Getting type details for: '" + baseTypeName + "', please wait...");

            //(string error, ClrtDisplayableType dispType, ulong[] instances) = await Task.Run(() =>
            //{
            //    return CurrentIndex.GetTypeDisplayableRecord(typeId);
            //});
            (string error, ClrtDisplayableType dispType, ulong[] instances) = await Task.Factory.StartNew(() =>
            {
                return CurrentIndex.GetTypeDisplayableRecord(typeId);
            }, DumpSTAScheduler);

            SetEndTaskMainWindowState("Getting type details for: '" + baseTypeName + "', done");


            if (error != null)
            {
                if (Utils.IsInformation(error))
                    ShowInformation("Type Values Report","Cannot be done.",error,null);
                else 
                    GuiUtils.ShowError(error, this);
                return;
            }
#pragma warning disable CS4014
            Dispatcher.CurrentDispatcher.InvokeAsync(() => DoDisplayTypeValueReportSetup(dispType));
#pragma warning restore CS4014
        }

        private ulong GetAddressFromList(object listBox)
        {
            AssertIndexIsAvailable();
            if (!IsIndexAvailable()) return Constants.InvalidAddress;
            var lbAddresses = GetTypeAddressesListBox(listBox);
            if (lbAddresses.SelectedItems.Count < 1)
            {
                MessageBox.Show("No address is selected!", "Action Aborted", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return Constants.InvalidAddress;
            }
            var selItem = lbAddresses.SelectedItems[0];
            if (selItem is ulong)
                return (ulong)selItem;
            if (selItem is string)
            {
                var result = Utils.GetFirstUlong((string)selItem);
                if (result.Key) return result.Value;
                MessageBox.Show("No address is found in ListBox item string!", "Action Aborted", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return Constants.InvalidAddress;
            }

            MessageBox.Show("Unknown type of ListBox item!", "Action Aborted", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return Constants.InvalidAddress;
        }

        private void LbGetInstValueClicked(object sender, RoutedEventArgs e)
        {
            AssertIndexIsAvailable();
            ulong addr = GetAddressFromList(sender);
            if (addr == Constants.InvalidAddress)
            {
                return;
            }
            var msg = "Getting object value at: " + Utils.RealAddressString(addr);
            Dispatcher.CurrentDispatcher.InvokeAsync(() => ExecuteInstanceValueQuery(msg, addr));
        }


        private void LbGetInstHierarchyClicked(object sender, RoutedEventArgs e)
        {
            ulong addr = GetAddressFromList(sender);
            if (addr == Constants.InvalidAddress) return;
            Dispatcher.CurrentDispatcher.InvokeAsync(() => ExecuteInstanceHierarchyQuery("Get instance hierarchy " + Utils.AddressStringHeader(addr), addr, Constants.InvalidIndex));
        }

        private void AssertIndexIsAvailable()
        {
            Debug.Assert(CurrentIndex != null);
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

        private bool AreReferencesAvailable(string caption = null)
        {
            if (CurrentIndex != null && CurrentIndex.HasInstanceReferences) return true;
            if (caption == null) return false;
            ShowInformation(caption, "References Not Available", "References were not indexed." + Environment.NewLine + "Check the setup 'Skip indexing reference'." + Environment.NewLine + "Sometimes this setting is used to when indexing huge dumps.", null);
            return false;
        }

        private void GetDlgString2(string title, string descr, string defValue, out string str)
        {
            str = null;
            InputStringDlg dlg = new InputStringDlg(descr, defValue ?? " ") { Title = title, Owner = Window.GetWindow(this) };
            var dlgResult = dlg.ShowDialog();
        }

        private readonly List<ulong> _lbTypeAddressesLastselections = new List<ulong>();

        private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBox lb = sender as ListBox;
            Debug.Assert(lb != null);
            if (lb.SelectedItems.Count > 0)
            {
                _lbTypeAddressesLastselections.Clear();
                foreach (var sel in lb.SelectedItems)
                {
                    _lbTypeAddressesLastselections.Add((ulong)sel);
                }
            }
        }

        private void GetTypeStringUsage(object sender, RoutedEventArgs e)
        {
            if (!IsIndexAvailable("Cannot get type string, an index is not opened.")) return;
            ListBox listBox = GetTypeNameListBox(sender);
            string typeName = null;
            if (listBox == null || listBox.SelectedItems.Count <= 0) return;
            var sel = listBox.SelectedItems[0];
            int typeId;
            typeName = GetTypeNameFromSelection(sel, out typeId);
            if (typeId == Constants.InvalidIndex) return;
            Dispatcher.CurrentDispatcher.InvokeAsync(() => ExecuteTypeFieldUsageQuery(typeName));
        }

        private void AddrLstViewMemoryClicked(object sender, RoutedEventArgs e)
        {
            if (!IsIndexAvailable("Cannot view memory, an index is not opened.")) return;
            ulong addr = GetAddressFromList(sender);
            if (addr == Constants.InvalidAddress) return;
            ShowMemoryViewWindow(addr);
        }

        public void ShowMemoryViewWindow(ulong addr)
        {
            if (!IsIndexAvailable("Show Memory View")) return;
            string error;
            var dumpClone = CurrentIndex.Dump.Clone(out error);
            if (dumpClone == null)
            {
                ShowError(error);
                return;
            }
            var wnd = new HexView(Utils.GetNewID(), _wndDct, dumpClone, addr) { Owner = this };
            if (!wnd.Init(out error))
            {
                wnd.Close();
                ShowError(error);
                return;
            }
            wnd.Show();
        }

        private void IndexViewMemoryClicked(object sender, RoutedEventArgs e)
        {
            ulong addr;
            if (!GetUserEnteredAddress("Memory Address", "Enter memory address, if not hex format prefix with n/N.", out addr))
                return;
            ShowMemoryViewWindow(addr);
        }

        private void AhqTypesSizeCountCmpClicked(object sender, RoutedEventArgs e)
        {

        }

        private async void TypesImplementingInterfaceClicked(object sender, RoutedEventArgs e)
        {
            string answer;
            bool result = GetDlgString("List Types Implementing Interface", "Enter interface name, with namespace if possible.", " ", out answer);
            if (!result || string.IsNullOrWhiteSpace(answer)) return;
            answer = answer.Trim();
            SetStartTaskMainWindowState("Getting types impl. interface: '" + answer + "', please wait...");

            //var res = await Task.Run(() =>
            //{
            //    string err;
            //    string[] lst = CurrentIndex.GetTypesImplementingInterface(answer, out err);
            //    return new ValueTuple<string, string[]>(null, lst);
            //});
            var res = await Task.Factory.StartNew(() =>
            {
                string err;
                string[] lst = CurrentIndex.GetTypesImplementingInterface(answer, out err);
                return new ValueTuple<string, string[]>(null, lst);
            }, DumpSTAScheduler);

            SetEndTaskMainWindowState("Getting types impl. interface: '" + answer + "', done");

            (string error, string[] types) = res;
            // TODO JRD -- display this in some window
        }

        private async void AhqImplementedInterfaceObjectsClicked(object sender, RoutedEventArgs e)
        {
            if (CurrentAdhocDump == null)
            {
                MessageBox.Show("No ad-hoc crash dump is opened.", "Action Aborted");
                return;
            }

            string interfaceName = null;
            if (!GetDlgString("Interface Name", "Enter Interface Full Name", " ", out interfaceName)) return;

            SetStartTaskMainWindowState("Searching for interface implementors, please wait...");
            (string error, SortedDictionary<string, List<ulong>> dct) = await Task.Factory.StartNew(() =>
            {
                return DmpNdxQueries.FQry.getObjectsImplementingInterface(CurrentAdhocDump.Heap, interfaceName);
            }, _adhocSTAScheduler);
            SetEndTaskMainWindowState("Searching for interface implementors done.");

            //(string error, SortedDictionary<string, List<ulong>> dct) = DmpNdxQueries.FQry.getObjectsImplementingInterface(CurrentAdhocDump.Heap, interfaceName);

            if (error != null)
            {
                ShowError(error);
                return;
            }

            string outPath = Path.GetDirectoryName(CurrentAdhocDump.DumpPath) + Path.DirectorySeparatorChar + Path.GetFileName(CurrentAdhocDump.DumpPath) + "." + interfaceName + ".objects.txt";
            StreamWriter wr = null;
            try
            {
                wr = new StreamWriter(outPath);
                foreach (var kv in dct)
                {
                    wr.WriteLine(kv.Key);
                    wr.WriteLine("   [" + kv.Value.Count + "]");
                    for (int i = 0, icnt = kv.Value.Count; i < icnt; ++i)
                    {
                        if (i == 0) wr.Write("   ");
                        if (i > 0 && (i % 5) == 0)
                        {
                            wr.WriteLine();
                            wr.Write("   ");
                        }
                        else
                        {
                            wr.Write(Utils.RealAddressString(kv.Value[i]) + ", ");
                        }
                    }
                    wr.WriteLine();
                }
                wr.Close();
                wr = null;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                ShowError(error);
            }
            finally
            {
                wr?.Close();
            }

        }

        private void HelpViewHelpClicked(object sender, RoutedEventArgs e)
        {
            ValueWindows.ShowHelpWindow(Setup.HelpFolder + Path.DirectorySeparatorChar + "README.md");
        }


    }

    public static class MenuCommands
    {
        public static readonly RoutedUICommand InstanceValueCmd = new RoutedUICommand
        (
            "Get Instance Value",
            "GetInstanceValue",
            typeof(MenuCommands),
            new InputGestureCollection()
            {
                new KeyGesture(Key.V, ModifierKeys.Alt)
            }
        );
        public static readonly RoutedUICommand InstanceReferencesCmd = new RoutedUICommand
        (
            "Get Instance References",
            "GetInstanceRefrences",
            typeof(MenuCommands),
            new InputGestureCollection()
            {
                new KeyGesture(Key.R, ModifierKeys.Alt)
            }
        );
        public static readonly RoutedUICommand InstanceHierarchyCmd = new RoutedUICommand
        (
            "Get Instance Hierarchy",
            "GetInstanceHierarchy",
            typeof(MenuCommands),
            new InputGestureCollection()
            {
                new KeyGesture(Key.H, ModifierKeys.Alt)
            }
        );
        //Define more commands here, just like the one above
    }

}
