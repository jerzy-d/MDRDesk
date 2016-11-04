﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Diagnostics.Runtime;
using ClrMDRIndex;
using Binding = System.Windows.Data.Binding;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Label = System.Windows.Controls.Label;
using ListBox = System.Windows.Controls.ListBox;
using ListView = System.Windows.Controls.ListView;
using MessageBox = System.Windows.MessageBox;
using Panel = System.Windows.Controls.Panel;
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
        private const string ReportNameWeakReferenceInfo = "WeakReferenceInfo";
        private const string ReportTitleWeakReferenceInfo = "WeakReference Information";
        private const string ReportTitleInstRef = "Instance Refs";
        private const string ReportNameInstRef = "InstRef";
        private const string ReportTitleSizeDiffs = "Count;/Size Comp";
        private const string ReportNameSizeDiffs = "SizesDiff";

		// type display grids
		//
		private const string GridKeyNamespaceTypeView = "NamespaceTypeView";
		private const string GridKeyNameTypeView = "NameTypeView";

		private const string GridNameNamespaceTypeView = "NamespaceTypeView";
		private const string GridNameTypeView = "NameTypeView";
		private const string GridReversedNameTypeView = "ReversedNameTypeView";

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

            var lines = info.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0, icnt = lines.Length; i < icnt; ++i)
            {
                var ln = lines[i];
                if (ln.StartsWith("RUNTIME"))
                {
                    txtBlock.Inlines.Add(Environment.NewLine);
                    txtBlock.Inlines.Add(new Run(ln) { FontWeight = FontWeights.Bold, FontSize = 16, });
                    txtBlock.Inlines.Add(Environment.NewLine);
                    continue;
                }
                var pos = ln.IndexOf(':');
                if (pos > 0)
                {
                    txtBlock.Inlines.Add(new Run(ln.Substring(0, pos + 1)) { FontWeight = FontWeights.Bold, FontSize = 12 });
                    txtBlock.Inlines.Add("   " + ln.Substring(pos + 2));
                    txtBlock.Inlines.Add(Environment.NewLine);
                }
                else
                {
                    txtBlock.Inlines.Add(ln);
                    txtBlock.Inlines.Add(Environment.NewLine);
                }

            }

            //txtBlock.Inlines.Add(info);

            var generationLabel = (Label)LogicalTreeHelper.FindLogicalNode(grid, "GenerationInfoLabel");
            Debug.Assert(generationLabel != null);
            generationLabel.Content = "Generation totals. " +
                ClrtSegment.GetGenerationsString(new string[] { "OBJECTS Counts", "Sizes", Constants.HeavyGreekCrossPadded + " FREE Counts", "Sizes" }, histograms);

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
            host0.Child = DmpNdxQueries.Auxiliaries.getColumnChartWithTitle(histograms[0], "Object Counts");
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

		#region types main displays

		private void DisplayNamespaceGrid(KeyValuePair<string, KeyValuePair<string, int>[]>[] namespaces)
        {
            var grid = this.TryFindResource(GridKeyNamespaceTypeView) as Grid;
            Debug.Assert(grid != null);
            grid.Name = GridNameNamespaceTypeView + "__" + Utils.GetNewID();
            grid.Tag = namespaces;
            var lb = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, "lbTpNamespaces");
            Debug.Assert(lb != null);
            lb.ItemsSource = namespaces;
            var nsCountLabel = (Label)LogicalTreeHelper.FindLogicalNode(grid, @"lTpNsCount");
			Debug.Assert(nsCountLabel!=null);
            nsCountLabel.Content = Utils.LargeNumberString(namespaces.Length);
            var tab = new CloseableTabItem() { Header = Constants.BlackDiamond + " Types", Content = grid, Name = "HeapIndexTypeViewTab" };
            MainTab.Items.Add(tab);
            MainTab.SelectedItem = tab;
            MainTab.UpdateLayout();
            lb.SelectedIndex = 0;
        }

        private void lbTpNamespacesSelectionChange(object sender, SelectionChangedEventArgs e)
        {
			Debug.Assert(CurrentIndex != null);
			var grid = GetCurrentTabGrid();
            var lb = sender as ListBox;
            var lbNamespaceViewTypeNames = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, @"lbNamespaceViewTypeNames");
            var selndx = lb.SelectedIndex;
            if (selndx < 0) return;
            var data = lb.ItemsSource as KeyValuePair<string, KeyValuePair<string, int>[]>[];
            Debug.Assert(lbNamespaceViewTypeNames != null);
            var classCountLabel = (Label)LogicalTreeHelper.FindLogicalNode(grid, @"lTpClassCount");
            lbNamespaceViewTypeNames.ItemsSource = data[selndx].Value;
            lbNamespaceViewTypeNames.SelectedIndex = 0;
            classCountLabel.Content = Utils.LargeNumberString(data[selndx].Value.Length);
        }

        private void lbTpNamesSelectionChange(object sender, SelectionChangedEventArgs e)
        {
			Debug.Assert(CurrentIndex!=null);
            var grid = GetCurrentTabGrid();
            var lbNamespaceViewTypeNames = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, @"lbNamespaceViewTypeNames");
            var selndx = lbNamespaceViewTypeNames.SelectedIndex;
            if (selndx < 0) return;

            var data = lbNamespaceViewTypeNames.ItemsSource as KeyValuePair<string, int>[];
            var addresses = CurrentIndex.GetTypeInstances(data[selndx].Value);
            var lbTpNsTypeInfo = (Label)LogicalTreeHelper.FindLogicalNode(grid, @"lbTpNsTypeInfo");
            lbTpNsTypeInfo.Content = data[selndx].Value.ToString();
            var lbTpAddresses = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, @"lbTypeNamespaceAddresses");
            var addrCountLabel = (Label)LogicalTreeHelper.FindLogicalNode(grid, @"lTpAddressCount");
            addrCountLabel.Content = Utils.LargeNumberString(addresses.Length);
            lbTpAddresses.ItemsSource = addresses;

        }

        private void DisplayTypesGrid(bool reversedTypeNames)
        {
			Debug.Assert(CurrentIndex != null);
			var grid = this.TryFindResource(GridKeyNameTypeView) as Grid;
            Debug.Assert(grid != null);
            grid.Name = (reversedTypeNames ? GridReversedNameTypeView : GridNameTypeView) + "__" + Utils.GetNewID();
            var lb = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, "lbTypeViewTypeNames");
            Debug.Assert(lb != null);
            if (reversedTypeNames)
                lb.ItemsSource = CurrentIndex.ReversedTypeNames;
            else
                lb.ItemsSource = CurrentIndex.DisplayableTypeNames;
            var lab = (Label)LogicalTreeHelper.FindLogicalNode(grid, "lTypeCount");
            Debug.Assert(lab != null);
            lab.Content = "type count: " + string.Format("{0:#,###}", CurrentIndex.UsedTypeCount);
            var tab = new CloseableTabItem() { Header = Constants.BlackDiamond + " Types", Content = grid, Name = "HeapIndexTypeViewTab" };
            MainTab.Items.Add(tab);
            MainTab.SelectedItem = tab;
            MainTab.UpdateLayout();
        }

        private void lbTypeNamesSelectionChange(object sender, SelectionChangedEventArgs e)
        {
			Debug.Assert(CurrentIndex != null);
			Debug.Assert(sender is ListBox);
            var grid = ((ListBox)sender).Parent as Grid;
            var lbNames = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, @"lbTypeViewTypeNames");
			Debug.Assert(lbNames != null);
			var lbAddresses = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, @"lbTypeNameAddresses");
            Debug.Assert(lbAddresses != null);
			var selndx = lbNames.SelectedIndex;
			if (selndx < 0) return;
			var data = lbNames.ItemsSource as KeyValuePair<string, int>[];
			var addresses = CurrentIndex.GetTypeInstances(data[selndx].Value);
            var lab = (Label)LogicalTreeHelper.FindLogicalNode(grid, @"lAddressCount");
            Debug.Assert(lab != null);
            lab.Content = Utils.LargeNumberString(addresses.Length);
            lbAddresses.ItemsSource = addresses;
            lbAddresses.SelectedIndex = 0;
        }

		private ListBox GetTypeNamesListBox(object sender)
		{
			var grid = GetCurrentTabGrid();
			string listName = grid.Name.StartsWith(GridNameNamespaceTypeView, StringComparison.Ordinal) ? "lbNamespaceViewTypeNames" : "lbTypeViewTypeNames";
			var lbNames = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, listName);
			Debug.Assert(lbNames != null);
			return lbNames;
		}

		private string GetTypeNameFromSelection(object sel, out int typeId)
		{
			typeId = Constants.InvalidIndex;
			string typeName = string.Empty;
			if (sel is KeyValuePair<string, int>)
			{
				KeyValuePair<string, int> kv = (KeyValuePair<string, int>)sel;
				typeName = CurrentIndex.GetTypeName(kv.Value);
				typeId = kv.Value;
			}
			else
			{
				typeName = sel.ToString();
			}
			return typeName;
		}

	    private KeyValuePair<string, int> GetTypeNameInfo(object obj)
	    {
			Debug.Assert(obj is KeyValuePair<string, int>);
			return (KeyValuePair<string, int>)obj;
		}

	    private bool GetTypeNameInfo(object sender, out string typeName, out int typeId)
	    {
		    typeName = null;
		    typeId = Constants.InvalidIndex;
			var lbNames = GetTypeNamesListBox(sender);
			if (lbNames == null || lbNames.SelectedItems.Count < 1)
			{
				MessageBox.Show("No type is selected!", "Action Aborted", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return false;
			}
			var kv = GetTypeNameInfo(lbNames.SelectedItems[0]);
		    typeName = kv.Key;
		    typeId = kv.Value;
			return true;
	    }

		private void CopyTypeNameClicked(object sender, RoutedEventArgs e)
		{
			string typeName;
			int typeId;
			if (!GetTypeNameInfo(sender, out typeName, out typeId)) return;
			Clipboard.SetText(typeName);
			MainStatusShowMessage("Copied to Clipboard: " + typeName);
		}

		private void GenerationDistributionClicked(object sender, RoutedEventArgs e)
		{
			string typeName;
			int typeId;
			if (!GetTypeNameInfo(sender, out typeName, out typeId)) return;
			var genHistogram = CurrentIndex.GetTypeGcGenerationHistogram(typeId);
			var histStr = ClrtSegment.GetGenerationHistogramSimpleString(genHistogram);
			MainStatusShowMessage(typeName + ": " + histStr);
		}

		private void GetFieldValuesClicked(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("Not implemented yet.", "Get Field Values", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		private async void GetInstancesHeapSizeClicked(object sender, bool baseSizes)
		{
			string typeName;
			int typeId;
			if (!GetTypeNameInfo(sender, out typeName, out typeId)) return;
			string prefix = baseSizes ? "[BASE SIZE} " : "[TOTAL SIZE} ";
			SetStartTaskMainWindowState(prefix + "getting size, please wait...");
			var result = await Task.Run(() =>
			{
				string error;
				var tuple = CurrentIndex.GetTypeTotalSizes(typeId, baseSizes, out error);
				return new Tuple<string, ulong, KeyValuePair<uint, ulong>[]>(error, tuple.Item1, tuple.Item2);
			});
			string msg;
			if (result.Item1 != null)
			{
				msg = prefix + "getting size failed.";
				MessageBox.Show(result.Item1, msg, MessageBoxButton.OK, MessageBoxImage.Error);
			}
			else
			{
				msg = prefix + Utils.BaseTypeName(typeName) + " size = " + Utils.SizeString(result.Item2);
			}
			SetEndTaskMainWindowState(msg);
		}

		private void GetTotalHeapSizeClicked(object sender, RoutedEventArgs e)
		{
			GetInstancesHeapSizeClicked(sender, false);
		}

		private void GetTotalHeapSizeReportClicked(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("Not implemented yet.", "Get Type Total Heap Size Report", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		private void GetHeapBaseSizeClicked(object sender, RoutedEventArgs e)
		{
			GetInstancesHeapSizeClicked(sender, true);
		}

		private void GetHeapBaseSizeReportClicked(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("Not implemented yet.", "Get Type Heap Base Size Report", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		#endregion types main displays


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
            var treeView = (TreeView)LogicalTreeHelper.FindLogicalNode(grid, "treeView");
            Debug.Assert(treeView != null);
            treeView.Tag = root;
            Debug.Assert(treeView != null);
            treeView.Items.Add(tvRoot);

            //tvRoot.ExpandSubtree();

            var tab = new CloseableTabItem() { Header = Constants.BlackDiamond + " Type References", Content = grid, Name = "HeapIndexTypeViewTab" };
            MainTab.Items.Add(tab);
            MainTab.SelectedItem = tab;
            MainTab.UpdateLayout();
        }

        private void DisplayTypeValueSetupGrid(ClrtDisplayableType dispType)
        {
            var mainGrid = this.TryFindResource("TypeValueReportSetupGrid") as Grid;
            Debug.Assert(mainGrid != null);
            mainGrid.Name = "TypeValueReportSetupGrid__" + Utils.GetNewID();
			var typeValQry = new TypeValuesQuery();
	        mainGrid.Tag = typeValQry;
            TreeView treeView = UpdateTypeValueSetupGrid(dispType, mainGrid, null);
            TreeViewItem treeViewItem = (treeView.Items[0] as TreeViewItem);
			typeValQry.SetCurrentTreeViewItem(treeViewItem);
            var tab = new CloseableTabItem() { Header = Constants.BlackDiamond + " Type Value Setup", Content = mainGrid, Name = mainGrid.Name + "_tab" };
            MainTab.Items.Add(tab);
            MainTab.SelectedItem = tab;
	        if (treeViewItem != null)
	        {
		        treeViewItem.IsSelected = true;
	        }

			MainTab.UpdateLayout();
		}

		private TreeViewItem GetTypeValueSetupTreeViewItem(ClrtDisplayableType dispType)
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

		private void UpdateTypeValueSetupTreeViewItem(TreeViewItem node, ClrtDisplayableType dispType)
		{
			var txtBlk = GetClrtDisplayableTypeStackPanel(dispType);
			node.Header = txtBlk;
			node.Tag = dispType;
			txtBlk.Tag = node;
		}

		private TreeView UpdateTypeValueSetupGrid(ClrtDisplayableType dispType, Grid mainGrid, TreeViewItem root)
        {
            bool realRoot = false;
            if (root == null)
            {
                realRoot = true;
                root = GetTypeValueSetupTreeViewItem(dispType);
            }
            var fields = dispType.Fields;
            for (int i = 0, icnt = fields.Length; i < icnt; ++i)
            {
                var fld = fields[i];
                var node = GetTypeValueSetupTreeViewItem(fld);
                root.Items.Add(node);
            }

            var treeView = (TreeView)LogicalTreeHelper.FindLogicalNode(mainGrid, "TypeValueReportTreeView");
            Debug.Assert(treeView != null);
            if (realRoot)
            {
                treeView.Items.Clear();
                treeView.Items.Add(root);
            }
            root.ExpandSubtree();
            root.BringIntoView();
            //treeView.UpdateLayout();
            return treeView;
        }

        private void TypeValueReportMouseDown(object sender, MouseButtonEventArgs e)
        {
            //TreeViewItem item = e.Source as TreeViewItem;
            Run item = e.Source as Run;
            StackPanel panel = null;
            while (item is Run)
            {
                if (item.Parent is TextBlock)
                {
                    var txtBlk = item.Parent as TextBlock;
                    panel = txtBlk.Parent as StackPanel;
                    break;
                }
                item = item.Parent as Run;
            }
            if (panel != null)
            {
                var grid = GetCurrentTabGrid();
                var curSelectionInfo = grid.Tag as TypeValuesQuery;
                Debug.Assert(curSelectionInfo != null);
                curSelectionInfo.SetCurrentTreeViewItem(panel.Tag as TreeViewItem);
            }

        }

        private void TypeValueReportSelectClicked(object sender, RoutedEventArgs e)
        {
            var grid = GetCurrentTabGrid();
            Debug.Assert(grid != null);
            var curSelectionInfo = grid.Tag as TypeValuesQuery;
            Debug.Assert(curSelectionInfo != null);
            if (curSelectionInfo.CurrentTreeViewItem != null)
            {
                var dispType = curSelectionInfo.CurrentTreeViewItem.Tag as ClrtDisplayableType;
                Debug.Assert(dispType != null);
                dispType.ToggleGetValue();
	            UpdateTypeValueSetupTreeViewItem(curSelectionInfo.CurrentTreeViewItem, dispType);
            }
        }

		private void TypeValueReportMouseOnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			var item = e.NewValue as TreeViewItem;
			if (item != null)
			{
				var grid = GetCurrentTabGrid();
				Debug.Assert(grid != null);
				var curSelectionInfo = grid.Tag as TypeValuesQuery;
				Debug.Assert(curSelectionInfo != null);
				curSelectionInfo.SetCurrentTreeViewItem(item);
				item.BringIntoView();
			}

		}

		private string SetFilterChar(string header, bool set)
        {
            if (string.IsNullOrEmpty(header)) return header;
            if (set)
            {
                if (set && header[0] == Constants.HeavyCheckMark && header[1] != Constants.FilterChar)
                {
                    header = header[0].ToString() + Constants.FilterHeader + header.Substring(2);
                }
                else if (header[0] != Constants.FilterChar)
                {
                    header = Constants.FilterHeader + header;
                }
            }
            return header;
        }

        private void TypeValueReportFilterClicked(object sender, RoutedEventArgs e)
        {
            var grid = GetCurrentTabGrid();
            Debug.Assert(grid != null);
            var curSelectionInfo = grid.Tag as TypeValuesQuery;
            Debug.Assert(curSelectionInfo != null);
            if (curSelectionInfo.CurrentTreeViewItem != null)
            {
                var dlg = new TypeValueFilterDlg(curSelectionInfo.CurrentTreeViewItem.Tag as ClrtDisplayableType)
                {
                    Owner = this,

                };
                bool? dlgResult = dlg.ShowDialog();
                if (dlgResult == true)
                {
                    string val = dlg.Value;

                    var dispType = curSelectionInfo.CurrentTreeViewItem.Tag as ClrtDisplayableType;
                    Debug.Assert(dispType != null);
                    dispType.SetFilter(new FilterValue(val, true));
                    curSelectionInfo.CurrentTreeViewItem.Header = dispType.ToString();

                    //string header = curSelectionInfo.CurrentTreeViewItem.Header as string;
                    //               curSelectionInfo.CurrentTreeViewItem.Header = SetFilterChar(header, true);
                }

                //Debug.Assert(header != null);
                //curSelectionInfo.CurrentTreeViewItem.Header = (header[0] == Constants.HeavyCheckMark)
                //    ? header.Substring(2)
                //    : Constants.HeavyCheckMarkHeader + header;
            }
        }

        #region Instance Hierarchy Traversing

        private void DisplayInstanceHierarchyGrid(Tuple<InstanceValue, AncestorDispRecord[]> instanceInfo)
        {
            var mainGrid = this.TryFindResource("InstanceHierarchyGrid") as Grid;
            Debug.Assert(mainGrid != null);
            mainGrid.Name = "InstanceHierarchyGrid__" + Utils.GetNewID();
            var undoRedoList = new UndoRedoList<Tuple<InstanceValue, AncestorDispRecord[]>>(new InstanceHierarchyInfoEqCmp());
            undoRedoList.AddToUndo(instanceInfo);
            mainGrid.Tag = undoRedoList;
            TreeViewItem root;
            var ancestorList = UpdateInstanceHierarchyGrid(instanceInfo, mainGrid, out root);

            var tab = new CloseableTabItem() { Header = Constants.BlackDiamond + " Instance Hierarchy", Content = mainGrid, Name = "InstanceHierarchyGrid" };
            MainTab.Items.Add(tab);
            MainTab.SelectedItem = tab;
            MainTab.UpdateLayout();
            ancestorList.SelectedIndex = 0;
            root.BringIntoView();
        }

        private ListBox UpdateInstanceHierarchyGrid(Tuple<InstanceValue, AncestorDispRecord[]> instanceInfo, Grid mainGrid, out TreeViewItem tvRoot)
        {
            InstanceValue instVal = instanceInfo.Item1;
            var stackPanel = new StackPanel() { Orientation = Orientation.Horizontal };
            // var image = new Image() { Source = @"C:\projects\csharp\MDRDesk\MDRDesk\Images\Structure_grey_16x.png" };
            var textBlk = new TextBlock();
            textBlk.Inlines.Add(instVal.ToString());
            stackPanel.Children.Add(textBlk);
            tvRoot = new TreeViewItem
            {

                Header = GetInstanceValueStackPanel(instVal),
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
                        Header = GetInstanceValueStackPanel(descNode),
                        Tag = descNode
                    };
                    tvParentNode.Items.Add(tvNode);
                    que.Enqueue(new KeyValuePair<InstanceValue, TreeViewItem>(descNode, tvNode));
                }
            }

            var mainLabel = (Label)LogicalTreeHelper.FindLogicalNode(mainGrid, "InstHierarchyValueLabel");
            mainLabel.Content = instVal;

            var ancestorNameList = (ListBox)LogicalTreeHelper.FindLogicalNode(mainGrid, "InstHierarchyAncestorNames");
            ancestorNameList.ItemsSource = instanceInfo.Item2;

            var treeViewGrid = (Grid)LogicalTreeHelper.FindLogicalNode(mainGrid, "InstHierarchyFieldGrid");
            Debug.Assert(treeViewGrid != null);
            var treeView = (TreeView)LogicalTreeHelper.FindLogicalNode(treeViewGrid, "InstHierarchyFieldTreeview");
            Debug.Assert(treeView != null);
            treeView.Items.Clear();
            treeView.Items.Add(tvRoot);
            TreeViewItem treeViewItem = (treeView.Items[0] as TreeViewItem);
            tvRoot.ExpandSubtree();
            tvRoot.IsSelected = true;
            tvRoot.BringIntoView();
            var lstAddresses = (ListBox)LogicalTreeHelper.FindLogicalNode(mainGrid, "InstHierarchyAncestorAddresses");
            lstAddresses.ItemsSource = null;
            return ancestorNameList;
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
            Debug.Assert(lstBox != null);
            var selectedItem = lstBox.SelectedItem;
            if (selectedItem == null) return;
            var grid = GetCurrentTabGrid();
            var lbInstances = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, @"InstHierarchyAncestorAddresses");
            Debug.Assert(lbInstances != null);
            lbInstances.ItemsSource = (selectedItem as AncestorDispRecord).Instances;

        }

        private async void InstHierarchyTreeViewDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            TreeView tv = sender as TreeView;
            var selItem = tv.SelectedItem as TreeViewItem;
            var instValue = selItem.Tag as InstanceValue;
            Debug.Assert(instValue != null);
            if (instValue.Address != Constants.InvalidAddress)
            {
                SetStartTaskMainWindowState("Getting instance info" + ", please wait...");
                ulong addr = instValue.Address;
                int fldNdx = instValue.FieldIndex;
                var result = await Task.Run(() =>
                {
                    string error;
                    Tuple<InstanceValue, AncestorDispRecord[]> instanceInfo = CurrentMap.GetInstanceInfo(addr, fldNdx, out error);
                    return Tuple.Create(error, instanceInfo);
                });

                if (result.Item1 != null)
                {
                    SetEndTaskMainWindowState("Getting instance info" + ", FAILED.");
                    if (result.Item1[0] == Constants.InformationSymbol)
                        ShowInformation("Instance Hierarchy", "Instance " + Utils.AddressString(addr) + " seatrch failed", result.Item1, null);
                    else
                        ShowError(result.Item1);
                    return;
                }

                var mainGrid = GetCurrentTabGrid();
                Debug.Assert(mainGrid.Tag is UndoRedoList<Tuple<InstanceValue, AncestorDispRecord[]>>);
                ((UndoRedoList<Tuple<InstanceValue, AncestorDispRecord[]>>)mainGrid.Tag).AddToUndo(result.Item2);
                TreeViewItem tvRoot;
                var ancestorList = UpdateInstanceHierarchyGrid(result.Item2, mainGrid, out tvRoot);
                ancestorList.SelectedIndex = 0;
                tvRoot.BringIntoView();
                SetEndTaskMainWindowState("Getting instance info" + ", DONE.");
            }
        }

        private async void InstHierarchyAncestorAddressesDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            var lstBox = sender as ListBox;
            Debug.Assert(lstBox != null);
            if (lstBox.SelectedItem != null)
            {
                var selectedAddress = (ulong)lstBox.SelectedItem;
                SetStartTaskMainWindowState("Getting instance info" + ", please wait...");
                var result = await Task.Run(() =>
                {
                    string error;
                    Tuple<InstanceValue, AncestorDispRecord[]> instanceInfo = CurrentMap.GetInstanceInfo(selectedAddress, Constants.InvalidIndex, out error);
                    return Tuple.Create(error, instanceInfo);
                });

                if (result.Item1 != null)
                {
                    SetEndTaskMainWindowState("Getting instance info" + ", FAILED.");
                    if (result.Item1[0] == Constants.InformationSymbol)
                        ShowInformation("Instance Hierarchy", "Instance " + Utils.AddressString(selectedAddress) + " seatrch failed", result.Item1, null);
                    else
                        ShowError(result.Item1);
                    return;
                }

                var mainGrid = GetCurrentTabGrid();
                Debug.Assert(mainGrid.Tag is UndoRedoList<Tuple<InstanceValue, AncestorDispRecord[]>>);
                ((UndoRedoList<Tuple<InstanceValue, AncestorDispRecord[]>>)mainGrid.Tag).AddToUndo(result.Item2);
                TreeViewItem tvItem;
                var ancestorList = UpdateInstanceHierarchyGrid(result.Item2, mainGrid, out tvItem);
                ancestorList.SelectedIndex = 0;
                tvItem.IsSelected = true;
                tvItem.BringIntoView();

                SetEndTaskMainWindowState("Getting instance info" + ", DONE.");

            }
        }

        private void InstHierarchyUndoClicked(object sender, RoutedEventArgs e)
        {
            var mainGrid = GetCurrentTabGrid();
            Debug.Assert(mainGrid.Tag is UndoRedoList<Tuple<InstanceValue, AncestorDispRecord[]>>);
            bool canUndo;
            var data = ((UndoRedoList<Tuple<InstanceValue, AncestorDispRecord[]>>)mainGrid.Tag).Undo(out canUndo);
            if (canUndo)
            {
                TreeViewItem tvItem;
                var ancestorList = UpdateInstanceHierarchyGrid(data, mainGrid, out tvItem);
                ancestorList.SelectedIndex = 0;
                tvItem.IsSelected = true;
                tvItem.BringIntoView();
            }
        }

        private void InstHierarchyRedoClicked(object sender, RoutedEventArgs e)
        {
            var mainGrid = GetCurrentTabGrid();
            Debug.Assert(mainGrid.Tag is UndoRedoList<Tuple<InstanceValue, AncestorDispRecord[]>>);
            bool canRedo;
            var data = ((UndoRedoList<Tuple<InstanceValue, AncestorDispRecord[]>>)mainGrid.Tag).Redo(out canRedo);
            if (canRedo)
            {
                TreeViewItem tvItem;
                var ancestorList = UpdateInstanceHierarchyGrid(data, mainGrid, out tvItem);
                ancestorList.SelectedIndex = 0;
                tvItem.IsSelected = true;
                tvItem.BringIntoView();
            }
        }

        static void OnTreeViewItemSelected(object sender, RoutedEventArgs e)
        {
            // Only react to the Selected event raised by the TreeViewItem
            // whose IsSelected property was modified. Ignore all ancestors
            // who are merely reporting that a descendant's Selected fired.
            if (!Object.ReferenceEquals(sender, e.OriginalSource))
                return;

            TreeViewItem item = e.OriginalSource as TreeViewItem;
            if (item != null)
                item.BringIntoView();
        }


        #endregion Instance Hierarchy Traversing

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

        private void MessageBoxShowError(string errStr)
        {
            string[] parts = errStr.Split(new[] { Constants.HeavyGreekCrossPadded }, StringSplitOptions.None);
            Debug.Assert(parts.Length > 2);
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
            for (int i = 1, icnt = parts.Length; i < icnt; ++i)
                sb.AppendLine(parts[i]);
            MessageBox.Show(StringBuilderCache.GetStringAndRelease(sb), parts[0], MessageBoxButton.OK,
                MessageBoxImage.Error);
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

            if (expander == null)
            {
                var genStr = ClrtSegment.GetGenerationHistogramSimpleString(result);
                SetEndTaskMainWindowState("Object generation at address(s): " + genStr);
                return;
            }

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

        private async void ExecuteInstanceHierarchyQuery(string statusMessage, ulong addr, int fldNdx)
        {
            SetStartTaskMainWindowState(statusMessage + ", please wait...");

            var result = await Task.Run(() =>
            {
                string error;
                Tuple<InstanceValue, AncestorDispRecord[]> instanceInfo = CurrentMap.GetInstanceInfo(addr, fldNdx, out error);

                return Tuple.Create(error, instanceInfo);
            });

            if (result.Item1 != null)
            {
                SetEndTaskMainWindowState(statusMessage + ", FAILED.");
                if (result.Item1[0] == Constants.InformationSymbol)
                    ShowInformation("Instance Hierarchy", "Instance " + Utils.AddressString(addr) + " seatrch failed", result.Item1, null);
                else
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
            // TODO JRD -- handle all grid types

            grid.Tag = null;
            var gridNamePrefix = Utils.GetNameWithoutId(grid.Name);
            switch (gridNamePrefix)
            {
                case "InstanceHierarchyGrid":
                    break;
            }
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

        //public static void SetSelectedItem(TreeView control, object item)
        //{
        //	try
        //	{
        //		var dObject = control.ItemContainerGenerator.ContainerFromItem(item);

        //		//uncomment the following line if UI updates are unnecessary
        //		((TreeViewItem)dObject).IsSelected = true;

        //		MethodInfo selectMethod = typeof(TreeViewItem).GetMethod("Select",
        //			BindingFlags.NonPublic | BindingFlags.Instance);

        //		selectMethod.Invoke(dObject, new object[] { true });
        //	}
        //	catch { }
        //}

        private StackPanel GetClrtDisplayableTypeStackPanel (ClrtDisplayableType dispType)
        {
            var stackPanel = new StackPanel() { Orientation = Orientation.Horizontal };
            var cat = dispType.Category;
            Image image = new Image();
            if (cat.First == TypeCategory.Reference)
            {
                if (cat.Second == TypeCategory.Interface)
                    image.Source = ((Image)Application.Current.FindResource("InterfacePng")).Source;
                else if (cat.Second == TypeCategory.Array)
                    image.Source = ((Image)Application.Current.FindResource("ArrayPng")).Source;
                else
                {
                    image.Source = ((Image)Application.Current.FindResource("ClassPng")).Source;
                }
            }
            else if (cat.First == TypeCategory.Struct)
            {
                image.Source = ((Image)Application.Current.FindResource("StructPng")).Source;
            }
            else
            {
                image.Source = ((Image)Application.Current.FindResource("PrimitivePng")).Source;
            }

            stackPanel.Children.Add(image);
            stackPanel.Children.Add(GetClrtDisplayableTypeTextBlock(dispType));
            return stackPanel;
        }

        private TextBlock GetClrtDisplayableTypeTextBlock(ClrtDisplayableType val)
        {
            var txtBlk = new TextBlock();
			txtBlk.Inlines.Add("   ");
            var selection = val.SelectionStr();
            if (!string.IsNullOrEmpty(selection))
                txtBlk.Inlines.Add(new Bold(new Run(selection + "  ")) {Foreground = Brushes.DarkGreen});
            if (!string.IsNullOrEmpty(val.FieldName))
                txtBlk.Inlines.Add(new Italic(new Run(val.FieldName + "  ") {Foreground = Brushes.DarkRed}));
            txtBlk.Inlines.Add(new Run("  " + val.TypeName));
            return txtBlk;
    //        return string.IsNullOrEmpty(_fieldName)
    //? TypeHeader() + _typeName
    //: SelectionStr() + _fieldName + FilterStr(_valueFilter) + TypeHeader() + _typeName;

        }



        private StackPanel GetInstanceValueStackPanel(InstanceValue val)
        {
            var stackPanel = new StackPanel() { Orientation = Orientation.Horizontal };
            stackPanel.Children.Add(GetInstanceValueTextBlock(val));
            return stackPanel;
        }

        private TextBlock GetInstanceValueTextBlock(InstanceValue val)
        {
            var txtBlk = new TextBlock();
            if (!string.IsNullOrWhiteSpace(val.FieldName))
                txtBlk.Inlines.Add(new Italic(new Run(val.FieldName + "   ") { Foreground = Brushes.DarkRed }));
            txtBlk.Inlines.Add(new Bold(new Run(InstanceValueValueString(val))));
            txtBlk.Inlines.Add(new Run("   " + val.TypeName));
            return txtBlk;
        }

        private string InstanceValueValueString(InstanceValue val)
        {
            var value = val.Value.Content;
            return ((value.Length > 0 && value[0] == Constants.NonValueChar)
                        ? (Constants.FancyKleeneStar.ToString() + Utils.AddressString(val.Address))
                        : val.Value.ToString());
        }

        #endregion Utils

    }

	internal class InstanceHierarchyInfoEqCmp : IEqualityComparer<Tuple<InstanceValue, AncestorDispRecord[]>>
	{
		public bool Equals(Tuple<InstanceValue, AncestorDispRecord[]> b1, Tuple<InstanceValue, AncestorDispRecord[]> b2)
		{
			return b1.Item1.Address == b2.Item1.Address;
		}

		public int GetHashCode(Tuple<InstanceValue, AncestorDispRecord[]> bx)
		{
			return bx.Item1.Address.GetHashCode();
		}

	}
}
