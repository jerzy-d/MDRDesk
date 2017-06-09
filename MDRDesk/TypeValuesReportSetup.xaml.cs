using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClrMDRIndex;
using System.IO;
using System.Text.RegularExpressions;

namespace MDRDesk
{
    /// <summary>
    /// Interaction logic for TypeValuesReportSetup.xaml
    /// </summary>
    public partial class TypeValuesReportSetup : Window
	{
		private IndexProxy _indexProxy;
		private ClrtDisplayableType _typeInfo;
        private ulong[] _instances;
		private TreeViewItem _currentTreeViewItem;
		private HashSet<ClrtDisplayableType> _selection;
        private ClrtDisplayableType[] _needed;
        public ClrtDisplayableType[] Selections => _needed;


        public TypeValuesReportSetup(IndexProxy proxy, ClrtDisplayableType typeInfo, ulong[] instances)
		{
			_indexProxy = proxy;
			_typeInfo = typeInfo;
            _instances = instances;
			_selection = new HashSet<ClrtDisplayableType>(new ClrtDisplayableTypeIdComparer());
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
            if (dispType.HasFields)
            {
                var fields = dispType.Fields;
                for (int i = 0, icnt = fields.Length; i < icnt; ++i)
                {
                    var fld = fields[i];
                    var node = GuiUtils.GetTypeValueSetupTreeViewItem(fld);
                     root.Items.Add(node);
                }
            }
            if (dispType.HasAlternatives)
            {
                var alternatives = dispType.Alternatives;
                for (int i = 0, icnt = alternatives.Length; i < icnt; ++i)
                {
                    var alt = alternatives[i];
                    var node = GuiUtils.GetTypeValueSetupTreeViewItem(alt);
                    root.Items.Add(node);
                }
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

			if (dispType.FieldIndex == Constants.InvalidIndex || dispType.HasFields) // this root type, or fields are already displayed
			{
				return;
			}

			StatusText.Text = "Getting type details for field: '" + dispType.FieldName + "', please wait...";
			Mouse.OverrideCursor = Cursors.Wait;

            (string error, ClrtDisplayableType cdt, ulong[] instances) = await Task.Run(() =>
			{
                // TODO JRD -- replace with othetr metyhod !!!!
                return MainWindow.CurrentIndex.GetTypeDisplayableRecord(dispType.TypeId, dispType);
			});

			Mouse.OverrideCursor = null;

			if (error != null)
			{
				if (Utils.IsInformation(error))
				{
					StatusText.Text = "Action failed for: '" + cdt.FieldName + "'. " + error;
					return;
				}
				StatusText.Text = "Getting type details for field: '" + cdt.FieldName + "', failed";
				GuiUtils.ShowError(error,this);
				return;
			}

			var fields = cdt.Fields;
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
				if (dlg.Remove)
				{
					curDispItem.RemoveFilter();
				}
				else if (TypeExtractor.IsString(curDispItem.Kind))
				{
					var op = dlg.Operator;
					Regex regex = null;
					if (FilterValue.IsOp(FilterValue.Op.REGEX,op))
					{
						regex = new Regex(dlg.ValueString);
					}
					curDispItem.SetFilter(new FilterValue(dlg.ValueString, curDispItem.Kind, dlg.Operator, regex));
				}
				else
				{
					curDispItem.SetFilter(new FilterValue(dlg.ValueObject, curDispItem.Kind, dlg.Operator));
				}
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

  //      private ClrtDisplayableType[] GetOrderedSelection()
		//{
		//	var node = TypeValueReportTreeView.Items[0] as TreeViewItem;
		//	var que = new Queue<TreeViewItem>();
		//	que.Enqueue(node);
		//	LinkedList<ClrtDisplayableType> lst = new LinkedList<ClrtDisplayableType>();
		//	while(que.Count > 0)
		//	{
		//		node = que.Dequeue();
		//		lst.AddFirst(node.Tag as ClrtDisplayableType);
		//		if (node.Items == null) continue;
		//		for (int i = 0, icnt = node.Items.Count; i < icnt; ++i)
		//		{
		//			que.Enqueue(node.Items[i] as TreeViewItem);
		//		}
		//	}
  //          var lnode = lst.First;
  //          while(lnode!=null)
  //          {
  //              if (lnode.Value.IsMarked) break;
  //              lnode = lnode.Next;
  //              lst.RemoveFirst();
  //          }
  //          if (lst.Count < 1) return Utils.EmptyArray<ClrtDisplayableType>.Value;

  //          var needed = new HashSet<ClrtDisplayableType>(_selection);
  //          foreach(var sel in _selection)
  //          {
  //              var parent = sel.Parent;
  //              while(parent != null)
  //              {
  //                  needed.Add(parent);
  //                  parent = parent.Parent;
  //              }
  //          }
  //          lnode = lst.First;
  //          while (lnode != null)
  //          {
  //              var next = lnode.Next;
  //              if (!needed.Contains(lnode.Value))
  //                  lst.Remove(lnode);
  //              lnode = next;
  //          }
  //          var ary = lst.ToArray();
  //          Array.Reverse(ary);
  //          return ary;
		//}


		private ClrtDisplayableType[] GetOrderedSelection()
		{
			var needed = new HashSet<ClrtDisplayableType>(new ClrtDisplayableTypeIdComparer());
			needed.Add(_typeInfo);
			foreach (var sel in _selection)
			{
				needed.Add(sel);
                if (sel.Parent == null) continue;
				var parent = sel.Parent;
                if (parent.HasAlternatives && parent.HasAlternative(sel))
                {
                    parent = parent.Parent;
                    needed.Add(parent);
                    sel.SetParent(parent);
                    var tempSel = parent;
                    while (parent.HasAlternatives && parent.HasAlternative(tempSel))
                    {
                        parent = parent.Parent;
                        needed.Add(parent);
                        tempSel.SetParent(parent);
                        tempSel = parent;
                    }
                }

				while (parent != null)
				{
					needed.Add(parent);
					parent = parent.Parent;
				}
			}


			var que = new Queue<ClrtDisplayableType>(Math.Max(needed.Count * 2,64));
			var lst = new LinkedList<ClrtDisplayableType>();
			int ndx = 0;
			lst.AddFirst(_typeInfo);
			if (_typeInfo.Fields == null)
			{
				return lst.ToArray();
			}
			for (int i = 0, icnt = _typeInfo.Fields.Length; i < icnt; ++i)
			{
				que.Enqueue(_typeInfo.Fields[i]);
			}
			while (que.Count > 0)
			{
				var cdt = que.Dequeue();
				if (needed.Contains(cdt))
				{
					InsertClrtDisplayableType(lst, cdt);
				}

                if (cdt.HasFields)
                {
                    for (int i = 0, icnt = cdt.Fields.Length; i < icnt; ++i)
                    {
                        que.Enqueue(cdt.Fields[i]);
                    }
                }
                if (cdt.HasAlternatives)
                {
                    for (int i = 0, icnt = cdt.Alternatives.Length; i < icnt; ++i)
                    {
                        que.Enqueue(cdt.Alternatives[i]);
                    }
                }
			}
			return lst.ToArray();
		}

		private void InsertClrtDisplayableType(LinkedList<ClrtDisplayableType> lst, ClrtDisplayableType cdt)
		{
			long myParentId = cdt.Parent.Id;
			var node = lst.Last;
			while(true)
			{
				var val = node.Value;
				if (val.Id == myParentId || val.Parent.Id == myParentId)
				{
					lst.AddAfter(node, cdt);
					break;
				}
				if (node.Previous == null)
					throw new ArgumentException("[" + this.GetType().Name + "][InsertClrtDisplayableType] ClrtDisplayableType list is corrupted.");
				node = node.Previous;
			}
		}

		private void RunClicked(object sender, RoutedEventArgs e)
        {
            _needed = GetOrderedSelection();
			if (TypeValueSaveReportCheckBox.IsChecked.Value)
			{
				var tpName = Utils.BaseTypeName(_needed[0].TypeName);
				tpName = DumpFileMoniker.GetValidFileName(tpName);

				string spath = _indexProxy.FileMoniker.OutputFolder + Path.DirectorySeparatorChar + "TypeValuesSetup." + tpName + ".tvr";
				int pathNdx = 1;
				while(File.Exists(spath))
				{
					var newTpName = tpName + "(" + pathNdx.ToString() + ")";
					++pathNdx;
					spath = _indexProxy.FileMoniker.OutputFolder + Path.DirectorySeparatorChar + "TypeValuesSetup." + newTpName + ".tvr";
				}
				string error;
				bool r = ClrtDisplayableType.SerializeArray(spath, _needed, out error);

			}
			DialogResult = _needed.Length > 0;
        }

        private void CancelClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

		private void OpenClicked(object sender, RoutedEventArgs e)
		{
			string error = null;
			string path = GuiUtils.SelectFile(string.Format("*.{0}", "tvr"),
				string.Format("Type values setups (*.tvr)|*.tvr|All files (*.*)|*.*"), _indexProxy.FileMoniker.OutputFolder);
            if (path == null) return;
			try
			{
				ClrtDisplayableType[] queryItems = ClrtDisplayableType.DeserializeArray(path, out error);
				_typeInfo = ClrtDisplayableType.ClrtDisplayableTypeAryFixup(queryItems);
				TypeValueReportTreeView.Items.Clear();
				UpdateTypeValueSetupGrid(_typeInfo, null);
				TreeViewItem tvi = TypeValueReportTreeView.Items[0] as TreeViewItem;
				var que = new Queue<KeyValuePair<ClrtDisplayableType,TreeViewItem>>(_typeInfo.Fields.Length);
				for (int i = 0, icnt = _typeInfo.Fields.Length; i < icnt; ++i)
				{
					que.Enqueue(new KeyValuePair<ClrtDisplayableType, TreeViewItem>(_typeInfo.Fields[i], tvi.Items[i] as TreeViewItem));
				}
				while (que.Count > 0)
				{
					var kv = que.Dequeue();
					var dt = kv.Key;
					tvi = kv.Value; 
					if (dt.GetValue || dt.HasFilter)
					{
						UpdateSelection(dt);
					}
					if (dt.HasFields)
					{
						UpdateTypeValueSetupGrid(dt, tvi);
						for (int i = 0, icnt=dt.Fields.Length; i < icnt; ++i)
						{
							que.Enqueue(new KeyValuePair<ClrtDisplayableType, TreeViewItem>(dt.Fields[i], tvi.Items[i] as TreeViewItem));
						}
					}
                    else if (dt.HasAlternatives)
                    {
                        UpdateTypeValueSetupGrid(dt, tvi);
                        for (int i = 0, icnt = dt.Alternatives.Length; i < icnt; ++i)
                        {
                            que.Enqueue(new KeyValuePair<ClrtDisplayableType, TreeViewItem>(dt.Alternatives[i], tvi.Items[i] as TreeViewItem));
                        }
                    }
                }
                _instances = _indexProxy.GetTypeInstances(_typeInfo.TypeId);

            }
			catch(Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				GuiUtils.ShowError(error, this);
			}

		}
	}
}
