using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using ClrMDRIndex;

namespace MDRDesk
{
	public class IndexingProgress
	{
		private const string InfoSeparator = "{####}";
		private static TextBox _progressText;
		private static TextBox _infoText;
		private static DateTime _prevDateTime;
		IProgress<string> _progress;
		public IProgress<string> Progress => _progress;
		private CloseableTabItem _tab;


		public IndexingProgress()
		{
			_progress = new Progress<string>(ShowMessage);
		}

		public void Init(MainWindow wnd, string indexingInfo)
		{
			var grid = wnd.TryFindResource("IndexingGrid") as Grid;
			Debug.Assert(grid != null);
			_progressText = (TextBox) LogicalTreeHelper.FindLogicalNode(grid, "IndexingList");
			Debug.Assert(_progressText != null);
			_prevDateTime = DateTime.Now;
			_progressText.Text = _prevDateTime.ToLongTimeString() + " : " + "INDEXING INITIALIZING...";
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
				_progressText.Clear();
				_progressText = null;
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
			var dt = DateTime.Now;
			var tmStr = dt.ToLongTimeString();
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
