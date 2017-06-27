using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ClrMDRIndex;

namespace MDRDesk
{
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
			_progressText = (TextBox) LogicalTreeHelper.FindLogicalNode(grid, "IndexingList");
			Debug.Assert(_progressText != null);
			_prevDateTime = DateTime.Now;
			_progressText.Text = _prevDateTime.ToLongTimeString() + " : " + "INDEXING INITIALIZING...";
            _progressThreadText = (TextBox)LogicalTreeHelper.FindLogicalNode(grid, "IndexingThreadList");
            Debug.Assert(_progressThreadText != null);
            _progressRefsText = (TextBox)LogicalTreeHelper.FindLogicalNode(grid, "IndexingRefsList");
            Debug.Assert(_progressRefsText != null);


            _infoText = (TextBox) LogicalTreeHelper.FindLogicalNode(grid, "IndexingInformation");
			Debug.Assert(_infoText != null);
			_infoText.Text = indexingInfo;
			_tab = wnd.DisplayTab(Constants.BlackDiamond, "Indexing", grid, "IndexingGrid");
		}

		public static bool ShowIndexingInfo(MainWindow wnd, string path, out string error)
		{
			error = null;
			StreamReader sr = null;
			try
			{
				var grid = wnd.TryFindResource("IndexingGrid") as Grid;

				sr = new StreamReader(path);
				var text = sr.ReadToEnd();
				var pos = text.IndexOf(InfoSeparator,StringComparison.Ordinal);
				Debug.Assert(pos>0);
				var info = text.Substring(0, pos);
				var progressInfo = text.Substring(pos + InfoSeparator.Length);
				Debug.Assert(grid != null);
				_progressText = (TextBox) LogicalTreeHelper.FindLogicalNode(grid, "IndexingList");
				Debug.Assert(_progressText != null);
                _progressThreadText = (TextBox)LogicalTreeHelper.FindLogicalNode(grid, "IndexingThreadList");
                Debug.Assert(_progressThreadText != null);
                _progressRefsText = (TextBox)LogicalTreeHelper.FindLogicalNode(grid, "IndexingRefsList");
                Debug.Assert(_progressRefsText != null);

                _progressText.Text = progressInfo;
				_infoText = (TextBox) LogicalTreeHelper.FindLogicalNode(grid, "IndexingInformation");
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
			}
			catch (Exception)
			{
			}
			finally
			{
				sw?.Close();
			}
		}

		public static void ShowMessage(string msg)
		{
            if (string.IsNullOrWhiteSpace(msg)) return;
            lock (_lock)
            {
                var dt = DateTime.Now;
                var tmStr = dt.ToLongTimeString();
                if (msg[0] == Constants.HeavyAsterisk)
                {
                    msg = tmStr + " : " + msg;
                    _progressRefsText.AppendText(msg + Environment.NewLine);
                    _progressRefsText.ScrollToEnd();
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
