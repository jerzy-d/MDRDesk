using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
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

		public static TextBlock GetClrtDisplayableTypeTextBlock(ClrtDisplayableType val)
		{
			var txtBlk = new TextBlock();
			txtBlk.Inlines.Add("   ");
			var selection = val.SelectionStr();
			if (!string.IsNullOrEmpty(selection))
				txtBlk.Inlines.Add(new Bold(new Run(selection + "  ")) { Foreground = Brushes.DarkGreen });
			if (!string.IsNullOrEmpty(val.FieldName))
				txtBlk.Inlines.Add(new Italic(new Run(val.FieldName + "  ") { Foreground = Brushes.DarkRed }));
			txtBlk.Inlines.Add(new Run("  " + val.TypeName));
			return txtBlk;
			//        return string.IsNullOrEmpty(_fieldName)
			//? TypeHeader() + _typeName
			//: SelectionStr() + _fieldName + FilterStr(_valueFilter) + TypeHeader() + _typeName;

		}

		public static StackPanel GetInstanceValueStackPanel(InstanceValue val)
		{
			var stackPanel = new StackPanel() { Orientation = Orientation.Horizontal };
			stackPanel.Children.Add(GetInstanceValueTextBlock(val));
			return stackPanel;
		}

		public static TextBlock GetInstanceValueTextBlock(InstanceValue val)
		{
			var txtBlk = new TextBlock();
			if (!string.IsNullOrWhiteSpace(val.FieldName))
				txtBlk.Inlines.Add(new Italic(new Run(val.FieldName + "   ") { Foreground = Brushes.DarkRed }));
			txtBlk.Inlines.Add(new Bold(new Run(InstanceValueValueString(val))));
			txtBlk.Inlines.Add(new Run("   " + val.TypeName));
			return txtBlk;
		}

		public static string InstanceValueValueString(InstanceValue val)
		{
			var value = val.Value.Content;
			return ((value.Length > 0 && value[0] == Constants.NonValueChar)
						? (Constants.FancyKleeneStar.ToString() + Utils.RealAddressString(val.Address))
						: val.Value.ToString());
		}

		public static void ShowError(string errStr, Window wnd)
		{
			string[] parts = errStr.Split(new[] { Constants.HeavyGreekCrossPadded }, StringSplitOptions.None);
			MdrMessageBox dialog;

			if (parts.Length > 2)
			{
				dialog = new MdrMessageBox()
				{
					Owner = wnd,
					Caption = parts[0],
					InstructionHeading = parts[1],
					InstructionText = parts[2],
					DeatilsText = parts.Length > 3 ? parts[3] : string.Empty
				};
			}
			else if (parts.Length > 1)
			{
				dialog = new MdrMessageBox()
				{
					Owner = wnd,
					Caption = "ERROR",
					InstructionHeading = parts[0],
					InstructionText = parts[1],
					DeatilsText = string.Empty
				};
			}
			else
			{
				dialog = new MdrMessageBox()
				{
					Owner = wnd,
					Caption = "ERROR",
					InstructionHeading = "ERROR",
					InstructionText = errStr,
					DeatilsText = string.Empty
				};
			}

			dialog.SetButtonsPredefined(EnumPredefinedButtons.Ok);
			dialog.DetailsExpander.Visibility = string.IsNullOrWhiteSpace(dialog.DeatilsText) ? Visibility.Collapsed : Visibility.Visible;
			dialog.ShowDialog();
		}
	}
}
