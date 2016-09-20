﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms.VisualStyles;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Diagnostics.Runtime;
using ClrMDRIndex;
using DmpNdxQueries;
using Application = System.Windows.Application;
using Binding = System.Windows.Data.Binding;
using Clipboard = System.Windows.Clipboard;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Label = System.Windows.Controls.Label;
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
	public partial class MainWindow : Window
	{
		#region Reports

		private const string ReportTitleStringUsage = "String Usage";
		private const string ReportNameStringUsage = "StringUsage";
		private const string ReportTitleSTypesWithString = "Types With String";
		private const string ReportNameTypesWithString = "TypesWithString";
		private const string ReportTitleSizeInfo = "Type Size Information";
		private const string ReportNameSizeInfo = "SizesInfo";
		private const string ReportTitleInstRef = "Instance Refs";
		private const string ReportNameInstRef = "InstRef";
		private const string ReportTitleSizeDiffs = "Count;/Size Comp";
		private const string ReportNameSizeDiffs = "SizesDiff";

		private const string GridNameNamespaceTypeView = "NamespaceTypeView";
		private const string GridNameFullNameTypeView = "HeapIndexTypeView";
		private const string GridNameClassTypeView = "ReversedHeapIndexTypeView";

		private string GetReportTitle(ListView lst)
		{
			if (lst.Tag is Tuple<ListingInfo, string>)
			{
				string name = (lst.Tag as Tuple<ListingInfo, string>).Item2;
				return name ?? String.Empty;
			}
			return String.Empty;
		}
		
		private bool IsReport(ListView lst, string title)
		{
			if (lst.Tag is Tuple<ListingInfo, string>)
			{
				string name = (lst.Tag as Tuple<ListingInfo, string>).Item2;
				if (Utils.SameStrings(title, name)) return true;
			}
			return false;
		}

		#endregion Reports

		#region Display Grids

		/// <summary>
		/// Show information about the crash dump.
		/// We have this stored in a text file, in a index folder, with postfix : ~INDEXIFO.txt.
		/// </summary>
		/// <param name="info">General crush dump info.</param>
		/// <param name="histograms">GC generation information.</param>
		private void DisplayGeneralInfoGrid(string info, Tuple<string, long>[][] histograms)
		{
			var grid = this.TryFindResource("GeneralInfoView") as Grid;
			Debug.Assert(grid != null);

			var txtBlock = (TextBlock)LogicalTreeHelper.FindLogicalNode(grid, "GeneralInfoText");
			Debug.Assert(txtBlock != null);
			txtBlock.Inlines.Add(info);

			var generationLabel = (Label)LogicalTreeHelper.FindLogicalNode(grid, "GenerationInfoLabel");
			Debug.Assert(generationLabel != null);
			generationLabel.Content = "Generation totals. " +
				ClrtSegment.GetGenerationsString(new string[] { "OBJECTS Counts", "Sizes", Constants.HeavyGreekCrossPadded+" FREE Counts", "Sizes" }, histograms);

			var chartGrid = (Grid)LogicalTreeHelper.FindLogicalNode(grid, "GeneralInfoChart");
			Debug.Assert(chartGrid != null);

			var grid1 = (Grid)LogicalTreeHelper.FindLogicalNode(chartGrid, "GeneralInfoChart1");
			Debug.Assert(grid1 != null);
			var grid2 = (Grid)LogicalTreeHelper.FindLogicalNode(chartGrid, "GeneralInfoChart2");
			Debug.Assert(grid2 != null);
			var grid3 = (Grid)LogicalTreeHelper.FindLogicalNode(chartGrid, "GeneralInfoChart3");
			Debug.Assert(grid3 != null);
			var grid4 = (Grid)LogicalTreeHelper.FindLogicalNode(chartGrid, "GeneralInfoChart4");
			Debug.Assert(grid4 != null);

			System.Windows.Forms.Integration.WindowsFormsHost host0 = new System.Windows.Forms.Integration.WindowsFormsHost();
			host0.Child = DmpNdxQueries.Auxiliaries.getColumnChartWithTitle(histograms[0],"Object Counts");
			System.Windows.Forms.Integration.WindowsFormsHost host1 = new System.Windows.Forms.Integration.WindowsFormsHost();
			host1.Child = DmpNdxQueries.Auxiliaries.getColumnChartWithTitle(histograms[1], "Object Sizes");
			System.Windows.Forms.Integration.WindowsFormsHost host2 = new System.Windows.Forms.Integration.WindowsFormsHost();
			host2.Child = DmpNdxQueries.Auxiliaries.getColumnChartWithTitle(histograms[2], "Free Counts");
			System.Windows.Forms.Integration.WindowsFormsHost host3 = new System.Windows.Forms.Integration.WindowsFormsHost();
			host3.Child = DmpNdxQueries.Auxiliaries.getColumnChartWithTitle(histograms[3], "Free Sizes");

			grid1.Children.Add(host0);
			grid2.Children.Add(host1);
			grid3.Children.Add(host2);
			grid4.Children.Add(host3);

			var tab = new CloseableTabItem() { Header = Constants.BlackDiamond + " General Info", Content = grid, Name = "GeneralInfoViewTab" };
			MainTab.Items.Add(tab);
			MainTab.SelectedItem = tab;
			MainTab.UpdateLayout();
		}

		private void DisplayNamespaceGrid(KeyValuePair<string, KeyValuePair<string, int>[]>[] namespaces)
		{
			var grid = this.TryFindResource(GridNameNamespaceTypeView) as Grid;
			Debug.Assert(grid != null);
			//grid.Name = "NamespaceTypeView__" + Utils.GetNewID();
			grid.Name = grid.Name + "__" + Utils.GetNewID();
			grid.Tag = namespaces;
			var lb = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, "lbTpNamespaces");
			Debug.Assert(lb != null);
			lb.ItemsSource = namespaces;
			var nsCountLabel = (Label)LogicalTreeHelper.FindLogicalNode(grid, @"lTpNsCount");
			nsCountLabel.Content = Utils.LargeNumberString(namespaces.Length);
			var tab = new CloseableTabItem() { Header = Constants.BlackDiamond + " Types", Content = grid, Name = "HeapIndexTypeViewTab" };
			MainTab.Items.Add(tab);
			MainTab.SelectedItem = tab;
			MainTab.UpdateLayout();
			lb.SelectedIndex = 0;
		}

		private void lbTpNamespacesSelectionChange(object sender, SelectionChangedEventArgs e)
		{
			var grid = GetCurrentTabGrid();
			var lb = sender as ListBox;
			var lbTpNames = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, @"lbTpNames");
			var selndx = lb.SelectedIndex;
			if (selndx < 0) return;
			var data = lb.ItemsSource as KeyValuePair<string, KeyValuePair<string, int>[]>[];
			Debug.Assert(lbTpNames != null);
			var classCountLabel = (Label)LogicalTreeHelper.FindLogicalNode(grid, @"lTpClassCount");
			lbTpNames.ItemsSource = data[selndx].Value;
			lbTpNames.SelectedIndex = 0;
			classCountLabel.Content = Utils.LargeNumberString(data[selndx].Value.Length);
		}

		private void lbTpNamesSelectionChange(object sender, SelectionChangedEventArgs e)
		{
			var grid = GetCurrentTabGrid();
			var lbTpNames = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, @"lbTpNames");
			var selndx = lbTpNames.SelectedIndex;
			if (selndx < 0) return;

			var data = lbTpNames.ItemsSource as KeyValuePair<string, int>[];

			ClrElementType elem;
			var addresses = CurrentMap.GetTypeAddressesFromSortedIndex(data[selndx].Value, out elem);
			var lbTpAddresses = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, @"lbTpAddresses");
			var addrCountLabel = (Label)LogicalTreeHelper.FindLogicalNode(grid, @"lTpAddressCount");
			addrCountLabel.Content = Utils.LargeNumberString(addresses.Length);
			lbTpAddresses.ItemsSource = addresses;

		}

		private void DisplayTypesGrid(bool reversedTypeNames)
		{
			var grid = this.TryFindResource(GridNameFullNameTypeView) as Grid;
			Debug.Assert(grid != null);
			//grid.Name = (reversedTypeNames ? "Reversed" : string.Empty) + "HeapIndexTypeView__" + Utils.GetNewID();
			grid.Name = (reversedTypeNames ? GridNameClassTypeView : GridNameFullNameTypeView) + "__" + Utils.GetNewID();
			var lb = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, "lbTypeNames");
			Debug.Assert(lb != null);
			if (reversedTypeNames)
				lb.ItemsSource = CurrentMap.ReversedTypeNames;
			else
				lb.ItemsSource = CurrentMap.TypeNames;
			var lab = (Label)LogicalTreeHelper.FindLogicalNode(grid, "lTypeCount");
			Debug.Assert(lab != null);
			lab.Content = "type count: " + string.Format("{0:#,###}", CurrentMap.TypeCount());
			var tab = new CloseableTabItem() { Header = Constants.BlackDiamond + " Types", Content = grid, Name = "HeapIndexTypeViewTab" };
			MainTab.Items.Add(tab);
			MainTab.SelectedItem = tab;
			MainTab.UpdateLayout();
		}

		private void lbTypeNamesSelectionChange(object sender, SelectionChangedEventArgs e)
		{
			Debug.Assert(sender is ListBox);
			var grid = ((ListBox)sender).Parent as Grid;
			var lbNames = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, @"lbTypeNames");
			var lbAddresses = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, @"lbTypeAddresses");
			Debug.Assert(lbAddresses != null);
			ClrElementType elem;
			ulong[] addresses;
			if (grid.Name.StartsWith("Reversed"))
				addresses = CurrentMap.GetTypeAddressesFromReversedNameIndex(lbNames.SelectedIndex, out elem);
			else
				addresses = CurrentMap.GetTypeAddressesFromSortedIndex(lbNames.SelectedIndex, out elem);
			var lab = (Label)LogicalTreeHelper.FindLogicalNode(grid, @"lAddressCount");
			Debug.Assert(lab != null);
			lab.Content = Utils.LargeNumberString(addresses.Length);
			var lelem = (Label)LogicalTreeHelper.FindLogicalNode(grid, @"lElementType");
			Debug.Assert(lelem != null);
			lelem.Content = elem;
			lbAddresses.ItemsSource = addresses;
			lbAddresses.SelectedIndex = 0;
		}

		private void DisplayListViewBottomGrid(ListingInfo info, char prefix, string name, string reportTitle, SWC.MenuItem[] menuItems = null, string filePath = null)
		{
			var grid = this.TryFindResource("ListViewBottomGrid") as Grid;
			grid.Name = name + "__" + Utils.GetNewID();
			Debug.Assert(grid != null);
			string path;
			if (filePath == null)
				path = CurrentMap != null ? CurrentMap.DumpPath : CurrentAdhocDump?.DumpPath;
			else
				path = filePath;
			grid.Tag = new Tuple<string, DumpFileMoniker>(reportTitle, new DumpFileMoniker(path));
			var listView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, "TopListView");
			GridView gridView = (GridView)listView.View;

			// save data and listing name in listView
			//
			listView.Tag = new Tuple<ListingInfo, string>(info, reportTitle);

			for (int i = 0, icnt = info.ColInfos.Length; i < icnt; ++i)
			{
				var gridColumn = new GridViewColumn
				{
					Header = info.ColInfos[i].Name,
					DisplayMemberBinding = new Binding(listing<string>.PropertyNames[i]),
					Width = info.ColInfos[i].Width,
				};
				gridView.Columns.Add(gridColumn);
			}

			listView.ItemsSource = info.Items;
			var bottomGrid = (Panel)LogicalTreeHelper.FindLogicalNode(grid, "BottomGrid");
			Debug.Assert(bottomGrid != null);
			TextBox txtBox = new TextBox
			{
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = SW.VerticalAlignment.Stretch,
				Foreground = Brushes.DarkGreen,
				Text = info.Notes,
				FontWeight = FontWeights.Bold
			};
			bottomGrid.Children.Add(txtBox);

			if (menuItems == null)
			{
				SWC.MenuItem mi = new SWC.MenuItem { Header = "Copy List Row", Tag = listView };
				menuItems = new SWC.MenuItem[]
				{
					mi
				};
			}
			foreach (var menu in menuItems)
			{
				menu.Tag = listView;
				menu.Click += ListViewBottomGridClick;
			}
			listView.ContextMenu = new SWC.ContextMenu();
			listView.ContextMenu.ItemsSource = menuItems;

			var tab = new CloseableTabItem() { Header = prefix + " " + reportTitle, Content = grid, Name = name + Utils.GetNewID() };
			MainTab.Items.Add(tab);
			MainTab.SelectedItem = tab;
			MainTab.UpdateLayout();
		}

		private void DisplayDependencyNodeGrid(DependencyNode root)
		{


			TreeViewItem tvRoot = new TreeViewItem();
			tvRoot.Header = root.ToString();
			tvRoot.Tag = root;
			Queue<KeyValuePair<DependencyNode, TreeViewItem>> que = new Queue<KeyValuePair<DependencyNode, TreeViewItem>>();
			que.Enqueue(new KeyValuePair<DependencyNode, TreeViewItem>(root, tvRoot));
			while (que.Count > 0)
			{
				var info = que.Dequeue();
				DependencyNode parentNode = info.Key;
				TreeViewItem tvParentNode = info.Value;
				DependencyNode[] descendants = parentNode.Descendants;
				for (int i = 0, icount = descendants.Length; i < icount; ++i)
				{
					var descNode = descendants[i];
					TreeViewItem tvNode = new TreeViewItem();
					tvNode.Header = descNode.ToString();
					tvNode.Tag = descNode;
					tvParentNode.Items.Add(tvNode);
					que.Enqueue(new KeyValuePair<DependencyNode, TreeViewItem>(descNode, tvNode));
				}
			}

			var grid = this.TryFindResource("TreeViewGrid") as Grid;
			Debug.Assert(grid != null);
			grid.Name = "DependencyNodeTreeView__" + Utils.GetNewID();
			grid.Tag = root;
			var treeView = (TreeView)LogicalTreeHelper.FindLogicalNode(grid, "treeView");
			Debug.Assert(treeView != null);
			treeView.Items.Add(tvRoot);

			//tvRoot.ExpandSubtree();

			var tab = new CloseableTabItem() { Header = Constants.BlackDiamond + " Type References", Content = grid, Name = "HeapIndexTypeViewTab" };
			MainTab.Items.Add(tab);
			MainTab.SelectedItem = tab;
			MainTab.UpdateLayout();
		}

		private void DisplayInstanceHierarchyGrid(Tuple<InstanceValue, AncestorDispRecord[]> instanceInfo)
		{
			InstanceValue instVal = instanceInfo.Item1;
			var tvRoot = new TreeViewItem
			{
				Header = instVal.ToString(),
				Tag = instVal
			};

			Queue<KeyValuePair<InstanceValue, TreeViewItem>> que = new Queue<KeyValuePair<InstanceValue, TreeViewItem>>();
			que.Enqueue(new KeyValuePair<InstanceValue, TreeViewItem>(instVal, tvRoot));
			while (que.Count > 0)
			{
				var info = que.Dequeue();
				InstanceValue parentNode = info.Key;
				TreeViewItem tvParentNode = info.Value;
				List<InstanceValue> descendants = parentNode.Values;
				for (int i = 0, icount = descendants.Count; i < icount; ++i)
				{
					var descNode = descendants[i];
					var tvNode = new TreeViewItem
					{
						Header = descNode.ToString(),
						Tag = descNode
					};
					tvParentNode.Items.Add(tvNode);
					que.Enqueue(new KeyValuePair<InstanceValue, TreeViewItem>(descNode, tvNode));
				}
			}

			var mainGrid = this.TryFindResource("InstanceHierarchyGrid") as Grid;
			Debug.Assert(mainGrid != null);
			mainGrid.Name = "InstanceHierarchyGrid__" + Utils.GetNewID();

			var ancestorNameList = (ListBox)LogicalTreeHelper.FindLogicalNode(mainGrid, "InstHierarchyAncestorNames");
			ancestorNameList.ItemsSource = instanceInfo.Item2;

			var treeViewGrid = (Grid)LogicalTreeHelper.FindLogicalNode(mainGrid, "InstHierarchyFieldGrid");
			Debug.Assert(treeViewGrid != null);
			var treeView = (TreeView)LogicalTreeHelper.FindLogicalNode(treeViewGrid, "InstHierarchyFieldTreeview");
			Debug.Assert(treeView != null);
			treeView.Items.Add(tvRoot);
			SetSelectedItem(treeView, tvRoot);
			tvRoot.ExpandSubtree();

			var tab = new CloseableTabItem() { Header = Constants.BlackDiamond + " Instance Hierarchy", Content = mainGrid, Name = "InstanceHierarchyGrid" };
			MainTab.Items.Add(tab);
			MainTab.SelectedItem = tab;
			MainTab.UpdateLayout();
			ancestorNameList.SelectedIndex = 0;
		}

		private void InstHierarchyTreeViewExpanded(object sender, RoutedEventArgs e)
		{
			var treeView = sender as TreeView;
			Debug.Assert(treeView != null);
			if (treeView.SelectedItem == null)
				(treeView.Items[0] as TreeViewItem).BringIntoView();
			else
				(treeView.SelectedItem as TreeViewItem).BringIntoView();
		}

		private void InstHierarchyAncestorChanged(object sender, SelectionChangedEventArgs e)
		{
			var lstBox = sender as ListBox;
			Debug.Assert(lstBox!=null);
			var selectedItem = lstBox.SelectedItem;
			if (selectedItem==null) return;
			var grid = GetCurrentTabGrid();
			var lbInstances = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, @"InstHierarchyAncestorAddresses");
			Debug.Assert(lbInstances!=null);
			lbInstances.ItemsSource = (selectedItem as AncestorDispRecord).Instances;

		}

		#endregion Display Grids

		#region MessageBox

		private void ShowInformation(string caption, string header, string text, string details)
		{
			var dialog = new MdrMessageBox()
			{
				Owner = this,
				Caption = string.IsNullOrWhiteSpace(caption) ? "Message" : caption,
				InstructionHeading = string.IsNullOrWhiteSpace(header) ? "???" : header,
				InstructionText = string.IsNullOrWhiteSpace(text) ? String.Empty : text,
				DeatilsText = string.IsNullOrWhiteSpace(details) ? String.Empty : details
			};
			dialog.SetButtonsPredefined(EnumPredefinedButtons.Ok);
			dialog.DetailsExpander.Visibility = string.IsNullOrEmpty(details) ? Visibility.Collapsed : Visibility.Visible;
			var result = dialog.ShowDialog();
		}

		private void ShowError(string errStr)
		{
			string[] parts = errStr.Split(new[] { Constants.HeavyGreekCrossPadded }, StringSplitOptions.None);
			Debug.Assert(parts.Length > 2);
			var dialog = new MdrMessageBox()
			{
				Owner = this,
				Caption = parts[0],
				InstructionHeading = parts[1],
				InstructionText = parts[2],
				DeatilsText = parts.Length > 3 ? parts[3] : string.Empty
			};
			dialog.SetButtonsPredefined(EnumPredefinedButtons.Ok);
			dialog.DetailsExpander.Visibility = string.IsNullOrWhiteSpace(dialog.DeatilsText) ? Visibility.Collapsed : Visibility.Visible;
			var result = dialog.ShowDialog();
		}


		private MdrMessageBox GetErrorMsgBox(string errStr)
		{
			string[] parts = errStr.Split(new[] { Constants.HeavyGreekCrossPadded }, StringSplitOptions.None);
			Debug.Assert(parts.Length > 2);
			var dialog = new MdrMessageBox()
			{
				Owner = this,
				Caption = parts[0],
				InstructionHeading = parts[1],
				InstructionText = parts[2],
				DeatilsText = parts.Length > 3 ? parts[3] : string.Empty
			};
			dialog.SetButtonsPredefined(EnumPredefinedButtons.Ok);
			dialog.DetailsExpander.Visibility = string.IsNullOrWhiteSpace(dialog.DeatilsText) ? Visibility.Collapsed : Visibility.Visible;
			return dialog;
		}

		#endregion MessageBox

		#region Map Queries

		private async void ExecuteReferenceQuery(string statusMessage, ulong addr, ulong[] addresses, int level)
		{
			MainStatusShowMessage(statusMessage + ", please wait...");
			MainToolbarTray.IsEnabled = false;
			Mouse.OverrideCursor = Cursors.Wait;
			var result = await Task.Run(() =>
			{
				return CurrentMap.GetFieldReferencesReport(addr, level);
			});

			Mouse.OverrideCursor = null;
			MainToolbarTray.IsEnabled = true;

			if (result.Error != null)
			{
				MainStatusShowMessage(statusMessage + ": FAILED!");
				MessageBox.Show(result.Error, "QUERY FAILED", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			MainStatusShowMessage(statusMessage + ": DONE");
			DisplayListViewBottomGrid(result, Constants.BlackDiamond, ReportNameInstRef, ReportTitleInstRef);

		}

		private async void ExecuteGenerationQuery(string statusMessage, ulong[] addresses, Grid grid)
		{
			SetStartTaskMainWindowState(statusMessage + ", please wait...");

			var result = await Task.Run(() =>
			{
				return CurrentMap.GetGenerationHistogram(addresses);
			});

			Expander expander = null;
			if (grid.Name.StartsWith("HeapIndexTypeView__"))
				expander = (Expander)LogicalTreeHelper.FindLogicalNode(grid, @"TypeViewDataExpander");
			else
				expander = (Expander)LogicalTreeHelper.FindLogicalNode(grid, @"ExtraDataExpander");

			System.Windows.Forms.Integration.WindowsFormsHost host = new System.Windows.Forms.Integration.WindowsFormsHost();

			host.Child = DmpNdxQueries.Auxiliaries.getColumnChart(ClrtSegment.GetGenerationHistogramTuples(result));

			if (addresses.Length == 1)
				expander.Header = "Generation histogram for instance at: " + Utils.AddressString(addresses[0]) + " " + ClrtSegment.GetGenerationHistogramSimpleString(result);
			else
				expander.Header = "Generation histogram: " + ClrtSegment.GetGenerationHistogramSimpleString(result);
			host.HorizontalAlignment = HorizontalAlignment.Stretch;
			host.VerticalAlignment = SW.VerticalAlignment.Stretch;
			var expanderGrid = new Grid { Height = 100 };
			expanderGrid.Children.Add(host);
			host.HorizontalAlignment = HorizontalAlignment.Stretch;
			expander.Content = expanderGrid;
			expander.IsExpanded = true;

			SetEndTaskMainWindowState(statusMessage + ", DONE.");
		}

		private async void ExecuteGenerationQuery(string statusMessage, string reportTitle, string str, Grid grid)
		{
			MainStatusShowMessage(statusMessage + ", please wait...");
			MainToolbarTray.IsEnabled = false;

			Mouse.OverrideCursor = Cursors.Wait;
			var result = await Task.Run(() =>
			{
				string error;
				int[] genHistogram = null;
				switch (reportTitle)
				{
					case ReportTitleStringUsage:
						genHistogram = CurrentMap.GetStringGcGenerationHistogram(str, out error);
						break;
					default:
						genHistogram = CurrentMap.GetTypeGcGenerationHistogram(str, out error);
						break;
				}
				return new Tuple<string, int[]>(error, genHistogram);
			});

			Mouse.OverrideCursor = null;
			MainToolbarTray.IsEnabled = true;
			if (result.Item1 != null)
			{
				MainStatusShowMessage(statusMessage + ": FAILED");
				MessageBox.Show(result.Item1, "Generation Lookup Failed", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			else
				MainStatusShowMessage(statusMessage + ": DONE");

			Expander expander = null;
			if (grid.Name.StartsWith("HeapIndexTypeView__"))
				expander = (Expander)LogicalTreeHelper.FindLogicalNode(grid, @"TypeViewDataExpander");
			else
				expander = (Expander)LogicalTreeHelper.FindLogicalNode(grid, @"ExtraDataExpander");

			System.Windows.Forms.Integration.WindowsFormsHost host = new System.Windows.Forms.Integration.WindowsFormsHost();

			host.Child = DmpNdxQueries.Auxiliaries.getColumnChart(ClrtSegment.GetGenerationHistogramTuples(result.Item2));

			expander.Header = "Generation histogram for " + str + " " + ClrtSegment.GetGenerationHistogramSimpleString(result.Item2);
			host.HorizontalAlignment = HorizontalAlignment.Stretch;
			host.VerticalAlignment = SW.VerticalAlignment.Stretch;
			var expanderGrid = new Grid { Height = 100 };
			expanderGrid.Children.Add(host);
			host.HorizontalAlignment = HorizontalAlignment.Stretch;
			expander.Content = expanderGrid;
			expander.IsExpanded = true;
		}

		private async void ExecuteInstanceHierarchyQuery(string statusMessage, ulong addr)
		{
			SetStartTaskMainWindowState(statusMessage + ", please wait...");

			var result = await Task.Run(() =>
			{
				string error;
				Tuple<InstanceValue, AncestorDispRecord[]> instanceInfo = CurrentMap.GetInstanceInfo(addr, out error);

				return Tuple.Create(error, instanceInfo);
			});

			Mouse.OverrideCursor = null;
			MainToolbarTray.IsEnabled = true;

			if (result.Item1 != null)
			{
				SetEndTaskMainWindowState(statusMessage + ", FAILED.");
				ShowError(result.Item1);
				return;
			}

			DisplayInstanceHierarchyGrid(result.Item2);

			SetEndTaskMainWindowState(statusMessage + ", DONE.");
		}



		#endregion Map Queries

		#region TabItem Cleanup

		public void ClearTabItem(Grid grid)
		{
			int a = 0; // TODO JRD -- implement this -- make sure that delegates are removed
		}

		#endregion TabItem Cleanup

		#region Utils

		private static long _lastEnteredValue = 0;

		private bool GetUserEnteredNumber(string title, string descr, out long value)
		{
			value = 0;
			string str;
			if (!GetDlgString(title,
				string.IsNullOrWhiteSpace(descr) ? "Enter number:" : descr,
				_lastEnteredValue > 0 ? _lastEnteredValue.ToString() : " ",
				out str)) return false;
			str = str.Trim();
			if (!Int64.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
			{
				MessageBox.Show("Not valid number string: " + str + ".", "INVALID INPUT", MessageBoxButton.OK,
					MessageBoxImage.Error);
				return false;
			}
			_lastEnteredValue = value;
			return true;
		}

		private bool GetUserEnteredAddress(string title, string descr, out ulong value)
		{
			value = 0;
			string str;
			if (!GetDlgString(title,
				string.IsNullOrWhiteSpace(descr) ? "Enter number:" : descr,
				" ",
				out str)) return false;
			str = str.Trim();
			bool parseResult = false;
			if (str.Length > 0 && (str[0] == 'n' || str[0] == 'N'))
			{
				str = str.Substring(1);
				parseResult = UInt64.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
			}
			else
			{
				if (str.Length > 0 && str.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) str = str.Substring(2);
				parseResult = UInt64.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
			}
			if (!parseResult)
			{
				MessageBox.Show("Not valid number string: " + str + ".", "INVALID INPUT", MessageBoxButton.OK,
					MessageBoxImage.Error);
				return false;
			}
			return true;
		}

		public static void SetSelectedItem(TreeView control, object item)
		{
			try
			{
				var dObject = control.ItemContainerGenerator.ContainerFromItem(item);

				//uncomment the following line if UI updates are unnecessary
				((TreeViewItem)dObject).IsSelected = true;

				MethodInfo selectMethod = typeof(TreeViewItem).GetMethod("Select",
					BindingFlags.NonPublic | BindingFlags.Instance);

				selectMethod.Invoke(dObject, new object[] { true });
			}
			catch { }
		}
		#endregion Utils

	}
}