using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClrMDRIndex;

namespace MDRDesk
{
    /// <summary>
    /// Interaction logic for CollectionDisplay.xaml
    /// </summary>
    public partial class CollectionDisplay : Window, IValueWindow
    {
        private int _id;
        private bool _indicesShown;
        private InstanceValue _instanceValue;
        private ValueWindows.WndType _wndType;
        private bool _locked;
        public ValueWindows.WndType WndType => _wndType;
        public int Id => _id;
        public bool Locked => _locked;
        private Image _lockedImg, _unlockedImg;

        public CollectionDisplay(int id, string descr, InstanceValue instVal, bool locked = false)
        {
            _wndType = ValueWindows.WndType.List;
            _id = id;
             InitializeComponent();
            SetLock(locked);
            UpdateInstanceValue(instVal, descr);
        }

        private void SetLock(bool locked)
        {
            _locked = locked;
            _lockedImg = new Image();
            _lockedImg.Source = ValueWindows.LockedImage.Source;
            _unlockedImg = new Image();
            _unlockedImg.Source = ValueWindows.UnlockedImage.Source;
            LockBtn.Content = locked ? _lockedImg : _unlockedImg;
        }

        public void UpdateInstanceValue(InstanceValue instVal, string descr)
        {
            _instanceValue = instVal;
            Debug.Assert(instVal.ArrayValues != null);
            CollectionValues.ItemsSource = instVal.ArrayValues;
            CollectionInfo.Text = descr == null ? GuiUtils.GetExtraDataString(instVal.ExtraData as KeyValuePair<string, string>[], instVal.TypeName, instVal.Address) : descr;
        }

        private void ShowArrayIndicesClicked(object sender, RoutedEventArgs e)
        {
            if (_indicesShown)
            {
                CollectionValues.ItemsSource = _instanceValue.ArrayValues;
                _indicesShown = false;
            }
            else
            {
                DisplayableString[] values = _instanceValue.ArrayValues;
                Debug.Assert(values != null);
                int cnt = values.Length;
                int digitCount = Utils.NumberOfDigits(cnt);
                DisplayableString[] newValues = new DisplayableString[cnt];
                string format = "[{0," + digitCount.ToString() + ":#,###,###}] ";
                for (int i = 0, icnt = values.Length; i < icnt; ++i)
                {
                    newValues[i] = new DisplayableString(Utils.SizeStringHeader(i, digitCount, format) + values[i]);
                }
                _indicesShown = true;
                CollectionValues.ItemsSource = newValues;
            }
        }

        public void Window_Closing(object sender, CancelEventArgs e)
        {
            var wndtask = Task.Factory.StartNew(() => ValueWindows.RemoveWindow(_id, _wndType));
        }

        private void CopyItemSelectionClicked(object sender, RoutedEventArgs e)
        {
            if (CollectionValues.SelectedIndex < 0) return;
            DisplayableString item = (DisplayableString)CollectionValues.SelectedItem;
            string value = item.FullContent;
            Clipboard.SetText(value);
        }

        private void CopyItemAllClicked(object sender, RoutedEventArgs e)
        {
            DisplayableString[] data = CollectionValues.ItemsSource as DisplayableString[];
            int maxLen = Math.Min(10000, data.Length);
            if (maxLen < 1) return;
            var sb = StringBuilderCache.Acquire(maxLen * 18);
            for (int i = 0, icnt = data.Length; i < icnt; ++i)
            {
                sb.AppendLine(data[i].FullContent);
            }
            string str = StringBuilderCache.GetStringAndRelease(sb);
            Clipboard.SetText(str);
        }

        private void ItemLstGetParentRefsClicked(object sender, RoutedEventArgs e)
        {
            GuiUtils.NotImplementedMsgBox("ItemLstGetParentRefsClicked");
        }

        private void ItemLstGetInstsClicked(object sender, RoutedEventArgs e)
        {
            GuiUtils.NotImplementedMsgBox("ItemLstGetInstsClicked");
        }

        private void ItemLstGetInstValueClicked(object sender, RoutedEventArgs e)
        {
            ItemListDoubleClicked(sender, null);
        }

        private void ItemLstGetInstHierarchyClicked(object sender, RoutedEventArgs e)
        {
            GuiUtils.NotImplementedMsgBox("ItemLstGetInstHierarchyClicked");
        }

        private void ItenLstViewMemoryClicked(object sender, RoutedEventArgs e)
        {
            GuiUtils.NotImplementedMsgBox("ItenLstViewMemoryClicked");
        }

        private ulong GetAddressFromEntry(string entry)
        {
            ulong addr = GuiUtils.TryGetAddressValue(entry);
            if (addr != Constants.InvalidAddress)
            {
                return ((MainWindow)Owner).IsValidHeapAddress(addr) ? addr : Constants.InvalidAddress;
            }
            return Constants.InvalidAddress;
        }

        private void ItemListDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            if (CollectionValues.SelectedIndex < 0) return;
            DisplayableString item = (DisplayableString)CollectionValues.SelectedItem;
            if (_instanceValue.Fields != null && _instanceValue.Fields.Length == 1 && _instanceValue.Fields[0].Kind == ClrElementKind.String)
            {
                if (!item.IsLong()) return;
                var fld = _instanceValue.Fields[0];
                var inst = new InstanceValue(fld.TypeId, fld.Address, fld.TypeName, string.Empty, item.FullContent);
                Dispatcher.CurrentDispatcher.InvokeAsync(() => ValueWindows.ShowContentWindow(fld.GetDescription(), inst, ValueWindows.WndType.Content));
                return;
            }
            ulong addr = GetAddressFromEntry(item.FullContent);
            if (addr != Constants.InvalidAddress)
            {
                Dispatcher.CurrentDispatcher.InvokeAsync(() => ((MainWindow)Owner).ExecuteInstanceValueQuery("Getting object value at: " + Utils.RealAddressString(addr), addr));
            }
        }

        private void ItemLstGetTypeValuesReportClicked(object sender, RoutedEventArgs e)
        {
            if (_instanceValue.ArrayValues == null || _instanceValue.ArrayValues.Length < 2) return;
            if ((TypeExtractor.IsNonStringObjectReference(_instanceValue.Fields[0].Kind)))
            {
                var owner = (MainWindow)Owner;
                // TODO JRD
                //System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(() => owner.ExecuteInstanceValueQuery("Getting object value at: " + Utils.RealAddressString(addr), addr));
            }

        }

        private void LockBtnClicked(object sender, RoutedEventArgs e)
        {
            if (_locked)
            {
                _locked = false;
                LockBtn.Content = _unlockedImg;
                ValueWindows.ChangeMyLock(_id, _wndType, false);
            }
            else
            {
                _locked = true;
                LockBtn.Content = _lockedImg;
                ValueWindows.ChangeMyLock(_id, _wndType, true);
            }
        }
    }
}
