using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ClrMDRIndex;

namespace MDRDesk
{
    /// <summary>
    /// Show tab with indexing information.
    /// It is shown when indexing a crash dump, or when dump_file_name.~INDEXINFO.txt is opened,
    /// </summary>
    public class IndexingProgress
    {
        private const string InfoSeparator = "{####}"; // to separate information from progress info in the indexing file
        private static TextBox _progressText; // progress messages
         private static TextBox _infoText; // general information
        private static DateTime _prevDateTime;
        IProgress<string> _progress;
        public IProgress<string> Progress => _progress;
        private CloseableTabItem _tab;
        private static object _lock;


        public IndexingProgress()
        {
            _progress = new Progress<string>(ShowMessage);
            _lock = new object();
        }

        /// <summary>
        /// Show indexing tab.
        /// </summary>
        public void Init(MainWindow wnd, string indexingInfo)
        {
            var grid = wnd.TryFindResource("IndexingGrid") as Grid;
            Debug.Assert(grid != null);
            _progressText = (TextBox)LogicalTreeHelper.FindLogicalNode(grid, "IndexingList");
            Debug.Assert(_progressText != null);
            _prevDateTime = DateTime.Now;
            _progressText.Text = _prevDateTime.ToLongTimeString() + " : " + "INDEXING INITIALIZING...";
            _infoText = (TextBox)LogicalTreeHelper.FindLogicalNode(grid, "IndexingInformation");
            Debug.Assert(_infoText != null);
            _infoText.Text = indexingInfo;
            _tab = wnd.DisplayTab(Constants.BlackDiamond, "Indexing", grid, "IndexingGrid");
        }

        /// <summary>
        /// Show tab with indexing information stored in the file: dump_file_name.~INDEXINGINFO.txt.
        /// </summary>
        /// <param name="wnd">Application main window.</param>
        /// <param name="path">Full file path.</param>
        /// <param name="error">[OUT] Error message or null (if no errors).</param>
        /// <returns>True if successful, false otherwise.</returns>
		public static bool ShowIndexingInfo(MainWindow wnd, string path, out string error)
        {
            error = null;
            StreamReader sr = null;
            try
            {
                var grid = wnd.TryFindResource("IndexingGrid") as Grid;
                Debug.Assert(grid != null);
                sr = new StreamReader(path);
                var text = sr.ReadToEnd();
                sr.Close();
                sr = null;

                var pos = text.IndexOf(InfoSeparator, StringComparison.Ordinal);
                Debug.Assert(pos > 0);
                int end = pos;
                pos += InfoSeparator.Length;

                var info = text.Substring(0, end).TrimStart();

                var progressInfo = text.Substring(pos, text.Length-pos).TrimStart();

                Debug.Assert(grid != null);
                _progressText = (TextBox)LogicalTreeHelper.FindLogicalNode(grid, "IndexingList");
                Debug.Assert(_progressText != null);

                _progressText.Text = progressInfo;
                _infoText = (TextBox)LogicalTreeHelper.FindLogicalNode(grid, "IndexingInformation");
                Debug.Assert(_infoText != null);
                _infoText.Text = info;

                wnd.DisplayTab(Constants.BlackDiamond, "Indexing", grid, "IndexingGrid");
                return true;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return false;
            }
            finally
            {
                sr?.Close();
            }
        }

        /// <summary>
        /// Save indexing information in the text file.
        /// </summary>
        /// <param name="path">Indexing information file path.</param>
        public void Close(string path)
        {
            StreamWriter sw = null;
            try
            {
                sw = new StreamWriter(path);
                sw.WriteLine(_infoText.Text);
                sw.WriteLine(InfoSeparator);
                sw.WriteLine(_progressText.Text);
            }
            catch (Exception)
            {
            }
            finally
            {
                sw?.Close();
            }
        }

        /// <summary>
        /// Append message to the appropriate text box.
        /// Messages are coming from an indexing progess delegete.
        /// </summary>
        /// <param name="msg">Text to display.</param>
        public static void ShowMessage(string msg)
        {
            if (string.IsNullOrWhiteSpace(msg)) return;
            lock (_lock)
            {
                var dt = DateTime.Now;
                var tmStr = dt.ToLongTimeString();
                // get duration of previous action
                //
                TimeSpan ts = dt - _prevDateTime;
                var duration = Utils.DurationString(ts);
                _prevDateTime = dt;
                // append duration to the previous action
                //
                _progressText.AppendText("  DURATION: " + duration + Environment.NewLine);
                // display current message
                //
                msg = tmStr + " : " + msg;
                _progressText.AppendText(msg);
                _progressText.ScrollToEnd();
            }
        }
    }
}
