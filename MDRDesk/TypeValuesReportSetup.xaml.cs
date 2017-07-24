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
    /// The dialog to setup a type values report.
    /// </summary>
    public partial class TypeValuesReportSetup : Window
    {
        private IndexProxy _indexProxy;
        private ClrtDisplayableType _typeInfo;
        private TreeViewItem _currentTreeViewItem;
        private HashSet<ClrtDisplayableType> _selection;
        private TypeValueQuery _query;
        public TypeValueQuery Query => _query;
        public ulong[] Instances => _typeInfo.Addresses;


        public TypeValuesReportSetup(IndexProxy proxy, ClrtDisplayableType typeInfo)
        {
            Debug.Assert(typeInfo.HasAddresses);
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

            //(string error, ClrtDisplayableType cdt, ulong[] instances) = await Task.Run(() =>
            //{
            //    // TODO JRD -- replace with other method !!!!
            //    List<ClrtDisplayableType> lst = new List<ClrtDisplayableType>() { dispType };
            //    if (dispType.HasAddresses)
            //    {
            //        return MainWindow.CurrentIndex.GetTypeDisplayableRecord(dispType.TypeId, dispType, null, dispType.Addresses);
            //    }
            //    var parent = dispType.RealParent;
            //    while (parent != null)
            //    {
            //        lst.Add(parent);
            //        if (parent.HasAddresses) break;
            //        parent = parent.RealParent;
            //    }
            //    lst.Reverse();
            //    return MainWindow.CurrentIndex.GetTypeDisplayableRecord(dispType.TypeId, dispType, lst.ToArray(), lst[0].Addresses);
            //});
            (string error, ClrtDisplayableType cdt, ulong[] instances) = await Task.Factory.StartNew(() =>
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
            }, GuiUtils.MainWindowInstance.DumpSTAScheduler);

            Mouse.OverrideCursor = null;

            if (error != null)
            {
                if (Utils.IsInformation(error))
                {
                    StatusText.Text = "Action failed for: '" + dispType.FieldName + "'. " + error;
                    GuiUtils.ShowInformation("TypeValues Report", "Show type fields failed.", error, null, this);
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
            if (curDispItem.HasAlternatives)
            {
                StatusText.Text = "This field '" + curDispItem.FieldName + "', has alternatives, itself cannot be queried.";
                return;
            }
            curDispItem.ToggleGetValue();
            GuiUtils.UpdateTypeValueSetupTreeViewItem(_currentTreeViewItem, curDispItem);
            UpdateSelection(curDispItem);
        }

        private void TypeValueReportFilterMenuitem_OnClick(object sender, RoutedEventArgs e)
        {
            if (_currentTreeViewItem == null) return;
            var curDispItem = _currentTreeViewItem.Tag as ClrtDisplayableType;
            Debug.Assert(curDispItem != null);
            if (curDispItem.HasAlternatives)
            {
                StatusText.Text = "This field '" + curDispItem.FieldName + "', has alternatives, itself cannot be queried.";
                return;
            }
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

        private int GetValueSelectionCount()
        {
            return _selection.Count(s => s.GetValue);
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
                if (dispType.GetValue)
                {
                    int currentCount = _selection.Count(s => s.GetValue);
                    if (listing<string>.MaxListingCount <= currentCount)
                    {
                        GuiUtils.ShowInformation("Type Value Report","Cannot select this item.","We must not have more than " + listing<string>.MaxListingCount + " items selected.", null, this);
                        return;
                    }
                }

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
            for (int i = qryLst.Count - 1; i >= 0; --i)
            {
                if (qryLst[i].Id == id) return qryLst[i];
            }
            return null;
        }

        private void GetQueryHelper(ClrtDisplayableType disp, TypeValueQuery qry, Dictionary<long, List<ClrtDisplayableType>> dct)
        {
            var items = dct[disp.Id]; // has to be there
            for(int i = 0, icnt = items.Count; i < icnt; ++i)
            {
                var item = items[i];
                TypeValueQuery q = new TypeValueQuery(item.Id, qry, item.TypeName, item.TypeId, item.FieldName, item.FieldIndex, item.IsAlternative, item.Filter, item.GetValue);
                qry.AddChild(q);
                GetQueryHelper(item, q, dct);
            }
        }

        private TypeValueQuery GetQuery()
        {
            int valCount = 1;
            var dct = new Dictionary<long, List<ClrtDisplayableType>>(_selection.Count*4);
            dct.Add(_typeInfo.Id, new List<ClrtDisplayableType>());
            var cmp = new ClrtDisplayableIdComparer();
            foreach (var sel in _selection)
            {
                if (sel.GetValue) ++valCount;
                if (!dct.ContainsKey(sel.Id))
                    dct.Add(sel.Id, new List<ClrtDisplayableType>());
                var parent = sel.RealParent;
                var cursel = sel;
                while (parent != null)
                {
                    List<ClrtDisplayableType> lst;
                    if (dct.TryGetValue(parent.Id,out lst))
                    {
                        if (!lst.Contains(cursel,cmp))
                            lst.Add(cursel);
                    }
                    else
                    {
                        dct.Add(parent.Id, new List<ClrtDisplayableType>() { cursel });
                    }
                    cursel = parent;
                    parent = parent.RealParent;
                }
            }

            TypeValueQuery root = new TypeValueQuery(_typeInfo.Id, null, _typeInfo.TypeName, _typeInfo.TypeId, "ADDRESS", Constants.InvalidIndex, false, null, true);
            GetQueryHelper(_typeInfo, root, dct);

            string[] values = new string[valCount * _typeInfo.Addresses.Length];
            root.SetValuesStore(values, 0, valCount);
            int dataNdx = 1;
            if (root.HasChildren)
            {
                foreach (var child in root.Children)
                {
                    SetValuesStore(child, values, ref dataNdx, valCount);
                    SetAlternativeSiblings(child);
                }
            }

            return root;
        }

        private void SetValuesStore(TypeValueQuery qry, string[] data, ref int ndx, int width)
        {
            if (qry.GetValue)
            {
                qry.SetValuesStore(data, ndx, width);
                ++ndx;
            }
            if (qry.HasChildren)
            {
                foreach (var child in qry.Children)
                {
                    SetValuesStore(child, data, ref ndx, width);
                }
            }
        }

        private void SetAlternativeSiblings(TypeValueQuery qry)
        {
            qry.SetAlternativeSiblings();
            if (qry.HasChildren)
            {
                foreach (var child in qry.Children)
                {
                    SetAlternativeSiblings(child);
                }
            }
        }

        private ClrtDisplayableType[] GetOrderedSelectionForSaving(out string error)
        {
            error = null;
            try
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
            catch(Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
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
            if (TypeValueSaveReportCheckBox.IsChecked.Value)
            {
                string error;
                ClrtDisplayableType[] tosave = GetOrderedSelectionForSaving(out error);
                if (error != null)
                {
                    GuiUtils.ShowError(error,this);
                    return;
                }
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
                bool r = ClrtDisplayableType.SerializeArray(spath, tosave, out error);
                if (error != null)
                {
                    GuiUtils.ShowError(error, this);
                    return;
                }
            }
            DialogResult = _selection.Count > 0;
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
                TypeValueReportTopTextBox.Text = _typeInfo.GetDescription();
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
