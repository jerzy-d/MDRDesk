using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;
using System.IO;
using ClrMDRIndex;

namespace MDRDesk
{
	/// <summary>
	/// Interaction logic for CreateCrashDump.xaml
	/// </summary>
	public partial class CreateCrashDump
	{
		private string _procdumpPath;
		private string _outputPath;
		private string _dumpPath;
		private int _processId = -1;
		private string _error;
		public string Error => _error;

		public bool IndexDump => IndexCheckBox.IsChecked != null && IndexCheckBox.IsChecked.Value;

		public string DumpPath => _dumpPath;

		public CreateCrashDump()
		{
			InitializeComponent();
            AddHotKeys();

            Process[] processlist = Process.GetProcesses();
			KeyValuePair<int, string>[] entries = new KeyValuePair<int, string>[processlist.Length];
			for (int i = 0, icnt = entries.Length; i < icnt; ++i)
			{
				Process proc = processlist[i];
				var desc = proc.ProcessName;
				entries[i] = new KeyValuePair<int, string>(proc.Id, desc);
			}

			Array.Sort(entries, (a, b) =>
			{
				var cmp = string.Compare(a.Value,b.Value,StringComparison.CurrentCultureIgnoreCase);
				if (cmp == 0)
				{
					cmp = a.Key < b.Key ? -1 : (a.Key > b.Key ? 1 : 0);
				}
				return cmp;
			});

			ProcdumpPath.Text = string.Empty;
			var folder = Setup.ProcDumpFolder;
			if (Directory.Exists(folder))
			{
				var procdumpPath = folder + Path.DirectorySeparatorChar + "procdump.exe";
				if (File.Exists(procdumpPath))
				{
					_procdumpPath = procdumpPath;
					ProcdumpPath.Text = procdumpPath;
				}
			}

			OutputFolder.Text = string.Empty;
			folder = Setup.DumpsFolder;
			if (Directory.Exists(folder))
			{
				_outputPath = folder;
				OutputFolder.Text = folder;
			}

			ProcessList.ItemsSource = entries;
			int myProcessId = Process.GetCurrentProcess().Id;
			Title = Title + "     [PID: " + myProcessId + "]";
		}

		private async void ButtonCreateDumpClicked(object sender, RoutedEventArgs e)
		{
			try
			{
				SelectedDump.Text = "Generating dummp, please wait...";
				SelectedDump.Foreground = Brushes.DarkRed;
				Mouse.OverrideCursor = Cursors.Wait;
				string options = ProcumpOptions.Text.Trim() + " ";

				var result = await Task.Run(() =>
				{
					string command = options + _processId.ToString() + " \"" + _outputPath + "\"";
					// create the ProcessStartInfo using "cmd" as the program to be run,
					// and "/c " as the parameters.
					// Incidentally, /c tells cmd that we want it to execute the command that follows,
					// and then exit.
					ProcessStartInfo procStartInfo = new ProcessStartInfo(_procdumpPath, command);

					// The following commands are needed to redirect the standard output.
					// This means that it will be redirected to the Process.StandardOutput StreamReader.
					procStartInfo.RedirectStandardOutput = true;
					procStartInfo.UseShellExecute = false;
					// Do not create the black window.
					procStartInfo.CreateNoWindow = true;
					// Now we create a process, assign its ProcessStartInfo and start it
					var proc = new Process();
					proc.StartInfo = procStartInfo;
					proc.Start();
					// Get the output into a string
					string output = proc.StandardOutput.ReadToEnd();
					return GetDumpPath(output);
				});

				Mouse.OverrideCursor = null;
				_dumpPath = result;
				SelectedDump.Foreground = Brushes.DarkGreen;
				SelectedDump.Text = _dumpPath;
				if (IndexDump)
					DialogResult = true;
			}
			catch (Exception ex)
			{
				_error = Utils.GetExceptionErrorString(ex);
				DialogResult = false;
				Close();
			}
			finally
			{
				Mouse.OverrideCursor = null;
				if (IndexDump)
					Close();
			}
		}

		private string GetDumpPath(string procDumpOutput)
		{
			int pos = procDumpOutput.IndexOf(_outputPath, StringComparison.OrdinalIgnoreCase);
			if (pos > 0)
			{
				int end = pos + _outputPath.Length;
				for (int icnt = procDumpOutput.Length; end < icnt; ++end)
				{
					var c = procDumpOutput[end];
					if (c=='\r' || c=='\n') break;
				}
				return procDumpOutput.Substring(pos, end - pos);
			}
			return string.Empty;
		}

		private void ButtonCloseClicked(object sender, RoutedEventArgs e)
		{
			IndexCheckBox.IsChecked = false;
			Close();
		}

		private void ProcessList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var selectedItem = ProcessList.SelectedItem;
			if (selectedItem != null)
			{
				_processId = ((KeyValuePair<int, string>)selectedItem).Key;
				var processName = ((KeyValuePair<int, string>)selectedItem).Value;
				SelectedDump.Text = "selected process: " + _processId.ToString() + "  " + processName;
				ButtonCreateDump.IsEnabled = true;
			}
		}

		private void ButtonOutputFolder_OnClick(object sender, RoutedEventArgs e)
		{
			string currentPath = OutputFolder.Text.Trim();
			if (!Directory.Exists(currentPath))
				currentPath = null;
			currentPath = GuiUtils.GetFolderPath(currentPath);
			if (Directory.Exists(currentPath))
			{
				OutputFolder.Text = currentPath;
				_outputPath = currentPath;
			}
		}

		private void ButtonProcdumpPath_OnClick(object sender, RoutedEventArgs e)
		{
			var currentPath = GuiUtils.SelectExeFile();
			if (File.Exists(currentPath))
			{
				ProcdumpPath.Text = currentPath;
				_procdumpPath = currentPath + Path.DirectorySeparatorChar + "procdump.exe";
			}
		}

        private void ButtonHelpClicked(object sender, RoutedEventArgs e)
        {
            ValueWindows.ShowHelpWindow(Setup.HelpFolder + Path.DirectorySeparatorChar + @"\Documentation\DumpLocalProcess.md");
        }

        private void AddHotKeys()
        {
            try
            {
                RoutedCommand firstSettings = new RoutedCommand();
                firstSettings.InputGestures.Add(new KeyGesture(Key.F1));
                CommandBindings.Add(new CommandBinding(firstSettings, ButtonHelpClicked));
            }
            catch (Exception err)
            {
                //handle exception error
            }
        }

        public static bool CannotFindProcdumpExe()
        {
            if (Setup.ProcDumpFolder == "") return true;
            if (Directory.Exists(Setup.ProcDumpFolder))
            {
                string path = Setup.ProcDumpFolder + Path.DirectorySeparatorChar + "procdump.exe";
                if (File.Exists(path)) return false;
            }
            return true;
        }
    }
}
