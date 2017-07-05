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
        private const string InfoSeparator = "{####}";
        private static TextBox _progressText;
        private static TextBox _progressThreadText;
        private static TextBox _progressRefsText;
        private static TextBox _infoText;
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

        public void Init(MainWindow wnd, string indexingInfo)
        {
            var grid = wnd.TryFindResource("IndexingGrid") as Grid;
            Debug.Assert(grid != null);

            _progressText = (TextBox)LogicalTreeHelper.FindLogicalNode(grid, "IndexingList");
            Debug.Assert(_progressText != null);
            _prevDateTime = DateTime.Now;
            _progressText.Text = _prevDateTime.ToLongTimeString() + " : " + "INDEXING INITIALIZING...";

            _progressThreadText = (TextBox)LogicalTreeHelper.FindLogicalNode(grid, "IndexingThreadList");
            Debug.Assert(_progressThreadText != null);
            _progressThreadText.Text = "THREADS INFORMATION" + Environment.NewLine;

            _progressRefsText = (TextBox)LogicalTreeHelper.FindLogicalNode(grid, "IndexingRefsList");
            Debug.Assert(_progressRefsText != null);
            _progressRefsText.Text = "GENERATING INSTANCE REFERENCES" + Environment.NewLine;

            _infoText = (TextBox)LogicalTreeHelper.FindLogicalNode(grid, "IndexingInformation");
            Debug.Assert(_infoText != null);
            _infoText.Text = indexingInfo;

            _tab = wnd.DisplayTab(Constants.BlackDiamond, "Indexing", grid, "IndexingGrid");
        }

        /// <summary>
        /// Show tab with indexing information stored in the file: dump_file_name.~INDEXINFO.txt.
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

                var pos1 = text.IndexOf(InfoSeparator, StringComparison.Ordinal);
                Debug.Assert(pos1 > 0);
                int end1 = pos1;
                pos1 += InfoSeparator.Length;
                int pos2 = text.IndexOf(InfoSeparator, pos1);
                int end2 = pos2;
                pos2 += InfoSeparator.Length;
                int pos3 = text.IndexOf(InfoSeparator, pos2);
                int end3 = pos3;
                pos3 += InfoSeparator.Length;

                var info = text.Substring(0, end1).TrimStart();
                var progressInfo = text.Substring(pos1, end2 - pos1).TrimStart();
                var threadInfo = text.Substring(pos2, end3 - pos2).TrimStart();
                var refInfo = text.Substring(pos3).TrimStart();

                Debug.Assert(grid != null);
                _progressText = (TextBox)LogicalTreeHelper.FindLogicalNode(grid, "IndexingList");
                Debug.Assert(_progressText != null);
                _progressThreadText = (TextBox)LogicalTreeHelper.FindLogicalNode(grid, "IndexingThreadList");
                Debug.Assert(_progressThreadText != null);
                _progressThreadText.Text = threadInfo;
                _progressRefsText = (TextBox)LogicalTreeHelper.FindLogicalNode(grid, "IndexingRefsList");
                Debug.Assert(_progressRefsText != null);
                _progressRefsText.Text = refInfo;

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

        public void Close(string path)
        {
            StreamWriter sw = null;

            try
            {
                sw = new StreamWriter(path);
                sw.WriteLine(_infoText.Text);
                sw.WriteLine(InfoSeparator);
                sw.WriteLine(_progressText.Text);
                sw.WriteLine(InfoSeparator);
                sw.WriteLine(_progressThreadText.Text);
                sw.WriteLine(InfoSeparator);
                sw.WriteLine(_progressRefsText.Text);
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

                // generating instance references msg
                //
                if (msg[0] == Constants.HeavyAsterisk)
                {
                    msg = tmStr + " : " + msg;
                    _progressRefsText.AppendText(msg + Environment.NewLine);
                    _progressRefsText.ScrollToEnd();
                    return;
                }

                // thread information msg
                //
                if (msg[0] == Constants.HeavyCheckMark)
                {
                    msg = tmStr + " : " + msg;
                    _progressThreadText.AppendText(msg + Environment.NewLine);
                    _progressThreadText.ScrollToEnd();
                    return;
                }

                TimeSpan ts = dt - _prevDateTime;
                var duration = Utils.DurationString(ts);
                _prevDateTime = dt;
                _progressText.AppendText("  DURATION: " + duration + Environment.NewLine);
                msg = tmStr + " : " + msg;
                _progressText.AppendText(msg);
                _progressText.ScrollToEnd();
            }
        }
    }
}
