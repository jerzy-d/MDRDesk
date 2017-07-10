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
        private TreeViewItem _currentTreeViewItem;
        private HashSet<ClrtDisplayableType> _selection;
        private ClrtDisplayableType[] _needed;
        public ClrtDisplayableType[] Selections => _needed;
        private TypeValueQuery _query;
        public TypeValueQuery Query => _query;
        public ulong[] Instances => _typeInfo.Addresses;
 

        public TypeValuesReportSetup(IndexProxy proxy, ClrtDisplayableType typeInfo)
        {
            _indexProxy = proxy;
            _typeInfo = typeInfo;
            _selection = new HashSet<ClrtDisplayableType>(new ClrtDisplayableIdComparer());
            InitializeComponent();
            TypeValueReportTopTextBox.Text = typeInfo.GetDescription();
            UpdateTypeValueSetupGrid(typeInfo, null);
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
            else
            {
                root.Tag = dispType;
            }

            if (dispType.HasFields)
            {
                DisplayNodes(root, dispType.Fields);
            }
            else if (dispType.HasAlternatives)
            {
                DisplayNodes(root, dispType.Alternatives);
            }

            if (realRoot)
            {
                TypeValueReportTreeView.Items.Clear();
                TypeValueReportTreeView.Items.Add(root);
            }
            root.ExpandSubtree();
        }

        private void DisplayNodes(TreeViewItem root, ClrtDisplayableType[] fields)
        {
            for (int i = 0, icnt = fields.Length; i < icnt; ++i)
            {
                var fld = fields[i];
                var node = GuiUtils.GetTypeValueSetupTreeViewItem(fld);
                root.Items.Add(node);
                if (fld.HasFields)
                {
                    DisplayNodes(node, fld.Fields);
                }
                else if (fld.HasAlternatives)
                {
                    DisplayNodes(node, fld.Alternatives);
                }
            }
        }


        private async void TypeValueReportTreeView_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            TreeView tv = sender as TreeView;
            var selItem = tv.SelectedItem as TreeViewItem;
            Debug.Assert(selItem != null);
            var dispType = selItem.Tag as ClrtDisplayableType;
            Debug.Assert(dispType != null);

            string msg;

            if (dispType.HasFields)
            {
                StatusText.Text = "'" + dispType.FieldName + "' already has fields.";
                return;
            }

            if (!dispType.CanGetFields(out msg))
            {
                StatusText.Text = "Cannot get fields of '" + dispType.FieldName + "'. " + msg;
                return;
            }

            StatusText.Text = "Getting type details for field: '" + dispType.FieldName + "', please wait...";
            Mouse.OverrideCursor = Cursors.Wait;

            (string error, ClrtDisplayableType cdt, ulong[] instances) = await Task.Run(() =>
            {
                // TODO JRD -- replace with other method !!!!
                List<ClrtDisplayableType> lst = new List<ClrtDisplayableType>() { dispType };
                if (dispType.HasAddresses)
                {
                    return MainWindow.CurrentIndex.GetTypeDisplayableRecord(dispType.TypeId, dispType, null, dispType.Addresses);
                }
                var parent = dispType.RealParent;
                while (parent != null)
                {
                    lst.Add(parent);
                    if (parent.HasAddresses) break;
                    parent = parent.RealParent;
                }
                lst.Reverse();
                return MainWindow.CurrentIndex.GetTypeDisplayableRecord(dispType.TypeId, dispType, lst.ToArray(), lst[0].Addresses);
            });

            Mouse.OverrideCursor = null;

            if (error != null)
            {
                if (Utils.IsInformation(error))
                {
                    StatusText.Text = "Action failed for: '" + dispType.FieldName + "'. " + error;
                    return;
                }
                StatusText.Text = "Getting type details for field: '" + dispType.FieldName + "', failed";
                GuiUtils.ShowError(error, this);
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
                    if (FilterValue.IsOp(FilterValue.Op.REGEX, op))
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
                Debug.Assert(typeInfo != null);
                TypeValueReportTopTextBox.Text = _typeInfo.GetDescription() + " ...selected: " + typeInfo.FieldName + " / " + typeInfo.TypeName;
            }
        }

        /// <summary>
        /// Update selections/filters for type display loaded form a file.
        /// </summary>
        /// <param name="dispType">Information loaded from a file.</param>
        private void UpdateSelections(ClrtDisplayableType dispType)
        {
            if (dispType.GetValue || dispType.HasFilter)
            {
                UpdateSelection(dispType);
            }
            if (dispType.HasFields)
            {
                for (int i = 0, icnt = dispType.Fields.Length; i < icnt; ++i)
                {
                    UpdateSelections(dispType.Fields[i]);
                }
            }
            if (dispType.HasAlternatives)
            {
                for (int i = 0, icnt = dispType.Alternatives.Length; i < icnt; ++i)
                {
                    UpdateSelections(dispType.Alternatives[i]);
                }
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
            UpdateSelectionListBox(dispType, TypeValueReportSelectedList, dispType.GetValue);
            UpdateSelectionListBox(dispType, TypeValueReportFilterList, dispType.HasFilter);
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


        private TypeValueQuery FindQuery(long id, List<TypeValueQuery> qryLst)
        {
            for (int i = qryLst.Count-1; i >=0; --i)
            {
                if (qryLst[i].Id == id) return qryLst[i];
            }
            return null;
        }

        private TypeValueQuery GetQuery()
        {
            int valCount = 1;
            foreach (var sel in _selection)
            {
                if (sel.GetValue) ++valCount;
            }
            string[] values = new string[valCount * _typeInfo.Addresses.Length];
            TypeValueQuery root = new TypeValueQuery(_typeInfo.Id, null, _typeInfo.TypeName, "ADDRESS", Constants.InvalidIndex, false, null, values, 0, valCount);
            var needed = new HashSet<ClrtDisplayableType>(new ClrtDisplayableIdComparer());
            needed.Add(_typeInfo);
            var tempLst = new List<ClrtDisplayableType>(16);
            var qryLst = new List<TypeValueQuery>(16);
            qryLst.Add(root);
            int valOffset = valCount;
            int valIndex = 1;
            foreach (var sel in _selection)
            {
                if (needed.Contains(sel)) continue;
                var parent = sel.RealParent;
                while (parent != null && !needed.Contains(parent))
                {
                    tempLst.Add(parent);
                    needed.Add(parent);
                    parent = parent.RealParent;
                }
                tempLst.Reverse();
                foreach (var item in tempLst)
                {
                    var qry = FindQuery(item.RealParent.Id, qryLst);
                    if (qry == null) throw new ArgumentException("[TypeValuesReportSetup.GetQuery] FindQuery returned null.");
                    TypeValueQuery q = item.GetValue 
                        ? new TypeValueQuery(item.Id, qry, item.TypeName, item.FieldName, item.FieldIndex, item.IsAlternative, item.Filter, values, valIndex, valCount)
                        : new TypeValueQuery(item.Id, qry, item.TypeName, item.FieldName, item.FieldIndex, item.IsAlternative, item.Filter, null,   0,        0);
                    if (item.GetValue)
                    {
                        valIndex += valCount;
                    }
                    qryLst.Add(q);
                    qry.AddChild(q);
                }
            }
            return root;
        }

        private ClrtDisplayableType[] GetOrderedSelection()
        {
            var lst = new LinkedList<ClrtDisplayableType>();
            var needed = new HashSet<ClrtDisplayableType>(new ClrtDisplayableIdComparer());
            var tempLst = new List<ClrtDisplayableType>(16);
            needed.Add(_typeInfo);
            lst.AddFirst(_typeInfo); // parent of all
            foreach (var sel in _selection)
            {
                if (needed.Contains(sel)) continue;
                if (sel.Parent == null)
                {
                    lst.AddFirst(sel);
                    needed.Add(sel);
                    continue;
                }
                tempLst.Clear();
                tempLst.Add(sel);
                var parent = sel.RealParent;
                while (parent != null && !needed.Contains(parent))
                {
                    tempLst.Add(parent);
                    needed.Add(parent);
                    parent = parent.RealParent;
                }
                tempLst.Reverse();
                foreach(var item in tempLst)
                {
                    var itemParentId = item.RealParent.Id;
                    var itemFieldIndex = item.FieldIndex;

                    // look for parent
                    var prev = lst.Last;
                    while (prev.Value.Id != itemParentId)
                    {
                        var node = prev.Previous;
                        if (node != null) prev = node;
                    }
                    lst.AddAfter(prev, item);
                    //// look for place
                    //var next = prev.Next;
                    //while(next != null && next.Value.RealParent.Id == itemParentId && next.Value.FieldIndex < itemFieldIndex)
                    //{
                    //    prev = next;
                    //    next = next.Next;
                    //}
                    
                }
            }

            //var que = new Queue<ClrtDisplayableType>(Math.Max(needed.Count * 2, 64));
            //var lst = new LinkedList<ClrtDisplayableType>();
            //int ndx = 0;
            //lst.AddFirst(_typeInfo);
            //if (_typeInfo.Fields == null)
            //{
            //    return lst.ToArray();
            //}
            //for (int i = 0, icnt = _typeInfo.Fields.Length; i < icnt; ++i)
            //{
            //    que.Enqueue(_typeInfo.Fields[i]);
            //}
            //while (que.Count > 0)
            //{
            //    var cdt = que.Dequeue();
            //    if (needed.Contains(cdt))
            //    {
            //        InsertClrtDisplayableType(lst, cdt);
            //    }

            //    if (cdt.HasFields)
            //    {
            //        for (int i = 0, icnt = cdt.Fields.Length; i < icnt; ++i)
            //        {
            //            que.Enqueue(cdt.Fields[i]);
            //        }
            //    }
            //    if (cdt.HasAlternatives)
            //    {
            //        for (int i = 0, icnt = cdt.Alternatives.Length; i < icnt; ++i)
            //        {
            //            que.Enqueue(cdt.Alternatives[i]);
            //        }
            //    }
            //}

            return lst.ToArray();
        }

        private ClrtDisplayableType[] GetOrderedSelectionForSaving()
        {
            var needed = new HashSet<ClrtDisplayableType>(new ClrtDisplayableIdComparer());
            needed.Add(_typeInfo);
            foreach (var sel in _selection)
            {
                needed.Add(sel);
                if (sel.Parent == null) continue;
                var parent = sel.Parent;
                while (parent != null)
                {
                    needed.Add(parent);
                    parent = parent.Parent;
                }
            }

            var que = new Queue<ClrtDisplayableType>(Math.Max(needed.Count * 2, 64));
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
            while (true)
            {
                var val = node.Value;
                if (val.Id == myParentId)
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
            _query = GetQuery();
            _needed = GetOrderedSelection();
            if (TypeValueSaveReportCheckBox.IsChecked.Value)
            {
                ClrtDisplayableType[] tosave = GetOrderedSelectionForSaving();
                var tpName = Utils.BaseTypeName(tosave[0].TypeName);
                tpName = DumpFileMoniker.GetValidFileName(tpName);

                string spath = _indexProxy.FileMoniker.OutputFolder + Path.DirectorySeparatorChar + "TypeValuesSetup." + tpName + ".tvr";
                int pathNdx = 1;
                while (File.Exists(spath))
                {
                    var newTpName = tpName + "(" + pathNdx.ToString() + ")";
                    ++pathNdx;
                    spath = _indexProxy.FileMoniker.OutputFolder + Path.DirectorySeparatorChar + "TypeValuesSetup." + newTpName + ".tvr";
                }
                string error;
                bool r = ClrtDisplayableType.SerializeArray(spath, tosave, out error);
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
                UpdateSelections(_typeInfo);
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                GuiUtils.ShowError(error, this);
            }

        }
    }
}
