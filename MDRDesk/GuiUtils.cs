using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClrMDRIndex;

namespace MDRDesk
{
	static class GuiUtils
	{
		public static string LastError { get; private set; }

		public static string SelectCrashDumpFile()
		{
			return SelectFile(string.Format("*.{0}", Constants.CrashDumpFileExt),
							string.Format("Dump Map Files|*.{0}|All Files|*.*", Constants.CrashDumpFileExt));
		}

		private static string[] SelectCrashDumpFiles()
		{
			return SelectFiles(string.Format("*.{0}", Constants.CrashDumpFileExt),
							string.Format("Dump Map Files|*.{0}|All Files|*.*", Constants.CrashDumpFileExt));
		}

		public static string SelectAssemblyFile()
		{
			return SelectFile(string.Format("*.{0}", "dll"),
							string.Format("Dll files (*.dll)|*.dll|Exe files (*.exe)|*.exe"), null);
		}

		public static string SelectExeFile(string initialDir=null)
		{
			return SelectFile(string.Format("*.{0}", "exe"),
							string.Format("Exe files (*.exe)|*.exe"), initialDir);
		}

		public static string SelectFile(string defExtension, string filter, string initialDir = null)
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
				LastError = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public static string[] SelectFiles(string defExtension, string filter, string initialDir = null)
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
				LastError = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public static string GetFolderPath(string initialFolder)
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
	}
}
