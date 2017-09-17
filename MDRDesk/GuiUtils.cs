using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ClrMDRIndex;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace MDRDesk
{
	static class GuiUtils
	{
		public static string LastError { get; private set; }

        public static MainWindow MainWindowInstance => (MainWindow)(((App)Application.Current).MainWindow);

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
					string path = Path.GetFullPath(initialDir);
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
					string path = Path.GetFullPath(initialDir);
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
			if (val.IsDummy)
			{
				txtBlk.Inlines.Add(new Italic(new Run(val.FieldName + "  ") { Foreground = Brushes.DarkBlue }));
				txtBlk.Inlines.Add(new Run("  " + val.TypeName));
				return txtBlk;
			}
			var selection = val.SelectionStr();
			if (!string.IsNullOrEmpty(selection))
				txtBlk.Inlines.Add(new Bold(new Run(selection + "  ")) { Foreground = Brushes.DarkGreen });
			if (!string.IsNullOrEmpty(val.FieldName))
				txtBlk.Inlines.Add(new Italic(new Run(val.FieldName + "  ") { Foreground = Brushes.DarkRed }));
			txtBlk.Inlines.Add(new Run("  " + val.TypeName));
            if (val.HasAddresses)
                txtBlk.Inlines.Add(new Italic(new Run("  [" + Utils.CountString(val.Addresses.Length) + "]")) { Foreground = Brushes.DarkGreen });

            return txtBlk;
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

        public static TextBlock GetTypeFieldTextBlock(string fldName, string typeName)
        {
            var txtBlk = new TextBlock();
            txtBlk.Inlines.Add(new Italic(new Run(fldName + "   ") { Foreground = Brushes.DarkRed }));
            txtBlk.Inlines.Add(new Bold(new Run(typeName)));
            return txtBlk;
        }

        public static void WalkDrawingForText(List<string> texts, Drawing d)
        {
            var glyphs = d as GlyphRunDrawing;
            if (glyphs != null)
            {
                texts.Add(new string(glyphs.GlyphRun.Characters.ToArray()));
            }
            else
            {
                var g = d as DrawingGroup;
                if (g != null)
                {
                    foreach (Drawing child in g.Children)
                    {
                        WalkDrawingForText(texts, child);
                    }
                }
            }
        }

        #region displayable type


        public static TreeViewItem GetTypeValueSetupTreeViewItem(ClrtDisplayableType dispType)
		{
			var txtBlk = GetClrtDisplayableTypeStackPanel(dispType);
			var node = new TreeViewItem
			{
				Header = txtBlk,
				Tag = dispType,
			};
			txtBlk.Tag = node;
			return node;
		}

		public static void UpdateTypeValueSetupTreeViewItem(TreeViewItem node, ClrtDisplayableType dispType)
		{
			var txtBlk = GetClrtDisplayableTypeStackPanel(dispType);
			node.Header = txtBlk;
			node.Tag = dispType;
			txtBlk.Tag = node;
		}

     
        /// <summary>
        /// Get type display panel.
        /// </summary>
        /// <param name="dispType">Type information.</param>
        /// <returns>Adorned panel.</returns>
        public static StackPanel GetClrtDisplayableTypeStackPanel(ClrtDisplayableType dispType)
		{
			var stackPanel = new StackPanel() { Orientation = Orientation.Horizontal };

			Image image = new Image();
			if (dispType.IsDummy)
			{
				image.Source = ((Image)Application.Current.FindResource("GroupPng")).Source;
				stackPanel.Children.Add(image);
				stackPanel.Children.Add(GetClrtDisplayableTypeTextBlock(dispType));
				return stackPanel;
			}

			var kind = dispType.Kind;
            var specKind = TypeExtractor.GetSpecialKind(kind);
            if (specKind != ClrElementKind.Unknown)
            {
                switch(specKind)
                {
                    case ClrElementKind.Free:
                    case ClrElementKind.Guid:
                    case ClrElementKind.DateTime:
                    case ClrElementKind.TimeSpan:
                    case ClrElementKind.Decimal:
                        image.Source = ((Image)Application.Current.FindResource("PrimitivePng")).Source;
                        break;
                    case ClrElementKind.Interface:
                        image.Source = ((Image)Application.Current.FindResource("InterfacePng")).Source;
                        break;
                    case ClrElementKind.Enum:
                        image.Source = ((Image)Application.Current.FindResource("EnumPng")).Source;
                        break;
                    case ClrElementKind.SystemObject:
                    case ClrElementKind.System__Canon:
                    case ClrElementKind.Exception:
                    case ClrElementKind.Abstract:
                        image.Source = ((Image)Application.Current.FindResource("ClassPng")).Source;
                        break;
                }
            }
            else
            {
                switch (TypeExtractor.GetStandardKind(kind))
                {
                    case ClrElementKind.String:
                        image.Source = ((Image)Application.Current.FindResource("PrimitivePng")).Source;
                        break;
                    case ClrElementKind.SZArray:
                    case ClrElementKind.Array:
                        image.Source = ((Image)Application.Current.FindResource("ArrayPng")).Source;
                        break;
                    case ClrElementKind.Object:
                    case ClrElementKind.Class:
                        image.Source = ((Image)Application.Current.FindResource("ClassPng")).Source;
                        break;
                    case ClrElementKind.Struct:
                        image.Source = ((Image)Application.Current.FindResource("StructPng")).Source;
                        break;
                    case ClrElementKind.Unknown:
                        image.Source = ((Image)Application.Current.FindResource("QuestionPng")).Source;
                        break;
                    default:
                        image.Source = ((Image)Application.Current.FindResource("PrimitivePng")).Source;
                        break;
                }
            }

			stackPanel.Children.Add(image);
			stackPanel.Children.Add(GetClrtDisplayableTypeTextBlock(dispType));
			return stackPanel;
		}


		#endregion

		public static void AddListViewColumn(Grid grid, string lstName, string colHeader, int width)
		{
			var alist = (ListView)LogicalTreeHelper.FindLogicalNode(grid, lstName);
			Debug.Assert(alist!=null);
			GridView gridView = (GridView)alist.View;
			Debug.Assert(gridView!=null);
			var gridColumn = new GridViewColumn
			{
				Header = colHeader,
				Width = width,
			};
			gridView.Columns.Add(gridColumn);
		}

		public static string InstanceValueValueString(InstanceValue val)
		{
			var value = val.Value.ToString();
			return ((value.Length > 0 && value[0] == Constants.NonValueChar)
						? (Constants.FancyKleeneStar.ToString() + Utils.RealAddressString(val.Address))
						: val.Value.ToString());
		}

		public static void ShowInformation(string caption, string header, string text, string details, Window wnd)
		{
			var dialog = new MdrMessageBox()
			{
				Owner = wnd,
				Caption = string.IsNullOrWhiteSpace(caption) ? "Message" : caption,
				InstructionHeading = string.IsNullOrWhiteSpace(header) ? "???" : header,
				InstructionText = string.IsNullOrWhiteSpace(text) ? String.Empty : text,
				DeatilsText = string.IsNullOrWhiteSpace(details) ? String.Empty : details
			};
			dialog.SetButtonsPredefined(EnumPredefinedButtons.Ok);
			dialog.DetailsExpander.Visibility = string.IsNullOrEmpty(details) ? Visibility.Collapsed : Visibility.Visible;
			dialog.ShowDialog();
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

        public static void NotImplementedMsgBox(string what)
        {
            MessageBox.Show("Not implemented yet.", what, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static  string GetExtraDataString(KeyValuePair<string, string>[] data, string typeName, ulong addr)
        {
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
            sb.AppendLine(typeName);
            sb.AppendLine(Utils.AddressString(addr));
            if (data == null || data.Length < 1)
            {
                sb.Append("No information available.").AppendLine();
                return StringBuilderCache.GetStringAndRelease(sb);
            }
            for (int i = 0, icnt = data.Length; i < icnt; ++i)
            {
                sb.Append(data[i].Key).Append(" = ").Append(data[i].Value).AppendLine();
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        /// <summary>
        /// Getting ulong value from string, which might be our standard report entry.
        /// First good hex string value is returned.
        /// </summary>
        /// <param name="dispAddr">Some string possibly contaning address (ulong value).</param>
        /// <returns>Ulong value, or 0 if no hex string present.</returns>
        public static ulong TryGetAddressValue(string dispAddr)
        {
            dispAddr = dispAddr.Trim();
            // it might be our standard entry with  ✚  separator
            var entries = dispAddr.Split(new string[] { Constants.HeavyGreekCrossPadded },StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0, icnt = entries.Length; i < icnt; ++i)
            {
                var str = entries[i].Trim();
                if (str.Length != Constants.AddressStringLength) continue;
                var subStr = str.Substring(4); // get rid of flags and x char
                if (!Utils.IsHex(subStr)) continue;
                try
                {
                    ulong addr = Convert.ToUInt64(subStr, 16);
                    return addr;
                }
                catch
                {
                    continue;
                }
            }
            return Constants.InvalidAddress;
        }

        public static string GetMenuItemName(object obj)
        {
            MenuItem menuItem = obj as MenuItem;
            return (menuItem == null) ? null : menuItem.Name;
        }
    }
}
