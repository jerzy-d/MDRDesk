using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClrMDRIndex;

namespace MDRDesk
{
    /// <summary>
    /// Interaction logic for TypeValuesReportSetup.xaml
    /// </summary>
    public partial class TypeValuesReportSetup : Window
	{
		private ClrtDisplayableType _typeInfo;
		private TypeValuesQuery _query;
		private TreeViewItem _currentTreeViewItem;
		private HashSet<ClrtDisplayableType> _selection;
        private ClrtDisplayableType[] _selections;
		//private LinkedList<ClrtDisplayableType> _selectionList;


		public TypeValuesReportSetup(ClrtDisplayableType typeInfo)
		{
			_typeInfo = typeInfo;
			_query = new TypeValuesQuery();
			_selection = new HashSet<ClrtDisplayableType>();
			//_selectionList = new LinkedList<ClrtDisplayableType>();
			InitializeComponent();
			TypeValueReportTopTextBox.Text = typeInfo.GetDescription();
			UpdateTypeValueSetupGrid(typeInfo,null);
            TypeValueReportSelectedList.Items.Add("SELECTED VALUES " + Constants.DownwardsBlackArrow);
            TypeValueReportFilterList.Items.Add("FILTERS " + Constants.DownwardsBlackArrow);
		}

		private void UpdateTypeValueSetupGrid(ClrtDisplayableType dispType, TreeViewItem root)
		{
			bool realRoot = false;
			if (root == null)
			{
				realRoot = true;
				root = GuiUtils.GetTypeValueSetupTreeViewItem(dispType);
			}
			var fields = dispType.Fields;
			for (int i = 0, icnt = fields.Length; i < icnt; ++i)
			{
				var fld = fields[i];
				var node = GuiUtils.GetTypeValueSetupTreeViewItem(fld);
				root.Items.Add(node);
			}

			if (realRoot)
			{
				TypeValueReportTreeView.Items.Clear();
				TypeValueReportTreeView.Items.Add(root);
			}
			root.ExpandSubtree();
		}


		private async void TypeValueReportTreeView_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			TreeView tv = sender as TreeView;
			var selItem = tv.SelectedItem as TreeViewItem;
			Debug.Assert(selItem != null);
			var dispType = selItem.Tag as ClrtDisplayableType;
			Debug.Assert(dispType != null);

			string msg;
			if (!dispType.CanGetFields(out msg))
			{
				StatusText.Text = "Action failed for: '" + dispType.FieldName + "'. " + msg;
				return;
			}

			if (dispType.FieldIndex == Constants.InvalidIndex) // this root type, fields are already displayed
			{
				return;
			}

			var parent = selItem.Parent as TreeViewItem;
			Debug.Assert(parent != null);
			var parentDispType = parent.Tag as ClrtDisplayableType;
			Debug.Assert(parentDispType != null);

			StatusText.Text = "Getting type details for field: '" + dispType.FieldName + "', please wait...";
			Mouse.OverrideCursor = Cursors.Wait;

			var result = await Task.Run(() =>
			{
				string error;
				ClrtDisplayableType fldDispType = MainWindow.CurrentIndex.GetTypeDisplayableRecord(parentDispType, dispType, out error);
				if (fldDispType == null)
					return new Tuple<string, ClrtDisplayableType>(error, null);
				return new Tuple<string, ClrtDisplayableType>(null, fldDispType);
			});
			Mouse.OverrideCursor = null;

			if (result.Item1 != null)
			{
				if (Utils.IsInformation(result.Item1))
				{
					StatusText.Text = "Action failed for: '" + dispType.FieldName + "'. " + result.Item1;
					return;
				}
				StatusText.Text = "Getting type details for field: '" + dispType.FieldName + "', failed";
				GuiUtils.ShowError(result.Item1,this);
				return;
			}

			var fields = result.Item2.Fields;
			selItem.Items.Clear();
			for (int i = 0, icnt = fields.Length; i < icnt; ++i)
			{
				var fld = fields[i];
				var node = GuiUtils.GetTypeValueSetupTreeViewItem(fld);
				selItem.Items.Add(node);
			}
			selItem.ExpandSubtree();

			StatusText.Text = "Getting type details for field: '" + dispType.FieldName + "', done";
		}

		private void TypeValueReportSelectMenuitem_OnClick(object sender, RoutedEventArgs e)
		{
			if (_currentTreeViewItem == null) return;
			var curDispItem = _currentTreeViewItem.Tag as ClrtDisplayableType;
			Debug.Assert(curDispItem != null);
			curDispItem.ToggleGetValue();
			GuiUtils.UpdateTypeValueSetupTreeViewItem(_currentTreeViewItem, curDispItem);
            UpdateSelection(curDispItem);
        }

        private void TypeValueReportFilterMenuitem_OnClick(object sender, RoutedEventArgs e)
		{
			if (_currentTreeViewItem == null) return;
			var curDispItem = _currentTreeViewItem.Tag as ClrtDisplayableType;
			Debug.Assert(curDispItem != null);
			var dlg = new TypeValueFilterDlg(_currentTreeViewItem.Tag as ClrtDisplayableType) { Owner = this };
			bool? dlgResult = dlg.ShowDialog();
			if (dlgResult == true)
			{
				string val = dlg.Value;
				if (string.IsNullOrEmpty(val))
					curDispItem.RemoveFilter();
				else
					curDispItem.SetFilter(new FilterValue(val));
				GuiUtils.UpdateTypeValueSetupTreeViewItem(_currentTreeViewItem, curDispItem);
				UpdateSelection(curDispItem);
			}
		}

		private void EventSetter_OnHandler(object sender, RequestBringIntoViewEventArgs e)
		{
			e.Handled = true;
		}

		private void TypeValueReportTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			var item = e.NewValue as TreeViewItem;
			if (item != null)
			{
				_currentTreeViewItem = item;
				var typeInfo = _currentTreeViewItem.Tag as ClrtDisplayableType;
				Debug.Assert(typeInfo!=null);
				TypeValueReportTopTextBox.Text = _typeInfo.GetDescription() + " ...selected: " + typeInfo.FieldName + " / " + typeInfo.TypeName;


			}
		}

		private void UpdateSelection(ClrtDisplayableType dispType)
		{
			Debug.Assert(dispType != null);
			if (dispType.GetValue || dispType.HasFilter)
			{
				_selection.Add(dispType);
			}
			else
			{
				_selection.Remove(dispType);
			}
            UpdateSelectionListBox(dispType, TypeValueReportSelectedList,dispType.GetValue);
            UpdateSelectionListBox(dispType, TypeValueReportFilterList,dispType.HasFilter);
        }

        private void UpdateSelectionListBox(ClrtDisplayableType dispType, ListBox lb, bool include)
        {
            bool contains = lb.Items.Contains(dispType);
            if (include)
            {
                if (contains) return;
                lb.Items.Add(dispType);
            }
            else
            {
                if (contains)
                    lb.Items.Remove(dispType);
            }
        }

        private ClrtDisplayableType[] GetOrderedSelection()
		{
			var node = TypeValueReportTreeView.Items[0] as TreeViewItem;
			var que = new Queue<TreeViewItem>();
			que.Enqueue(node);
			LinkedList<ClrtDisplayableType> lst = new LinkedList<ClrtDisplayableType>();
			while(que.Count > 0)
			{
				node = que.Dequeue();
				lst.AddFirst(node.Tag as ClrtDisplayableType);
				if (node.Items == null) continue;
				for (int i = 0, icnt = node.Items.Count; i < icnt; ++i)
				{
					que.Enqueue(node.Items[i] as TreeViewItem);
				}
			}
            var lnode = lst.First;
            while(lnode!=null)
            {
                if (lnode.Value.IsMarked) break;
                lnode = lnode.Next;
                lst.RemoveFirst();
            }
            if (lst.Count < 1) return Utils.EmptyArray<ClrtDisplayableType>.Value;

            var needed = new HashSet<ClrtDisplayableType>(_selection);
            foreach(var sel in _selection)
            {
                var parent = sel.Parent;
                while(parent != null)
                {
                    needed.Add(parent);
                    parent = parent.Parent;
                }
            }
            lnode = lst.First;
            while (lnode != null)
            {
                var next = lnode.Next;
                if (!needed.Contains(lnode.Value))
                    lst.Remove(lnode);
                lnode = next;
            }
            var ary = lst.ToArray();
            Array.Reverse(ary);
            return ary;
		}

        private void RunClicked(object sender, RoutedEventArgs e)
        {
            _selections = GetOrderedSelection();
            DialogResult = _selections.Length > 0;
        }

        private void CancelClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
