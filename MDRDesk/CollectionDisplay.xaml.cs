using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ClrMDRIndex;
using System.Windows.Interop;

namespace MDRDesk
{
    /// <summary>
    /// Interaction logic for CollectionDisplay.xaml
    /// </summary>
    public partial class CollectionDisplay : Window
    {
        private int _id;
        private ConcurrentDictionary<int, Window> _wndDct;
        private bool _indicesShown;
        private InstanceValue _instanceValue;

        public CollectionDisplay(int id, ConcurrentDictionary<int, Window> wndDct, InstanceValue instVal, string descr)
        {
            _id = id;
            _wndDct = wndDct;
            InitializeComponent();
            _instanceValue = instVal;
            Debug.Assert(instVal.ArrayValues != null);
            CollectionValues.ItemsSource = instVal.ArrayValues;
            CollectionInfo.Text = descr;
            wndDct.TryAdd(id, this);
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
            Window wnd;
            _wndDct.TryRemove(_id, out wnd);
        }

        private void CopyItemSelectionClicked(object sender, RoutedEventArgs e)
        {
            if (CollectionValues.SelectedIndex < 0) return;
            DisplayableString item = (DisplayableString)CollectionValues.SelectedItem;
            string value = item.Content;
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
                sb.AppendLine(data[i].Content);
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

        private void ItemListDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            if (CollectionValues.SelectedIndex < 0) return;
            DisplayableString item = (DisplayableString)CollectionValues.SelectedItem;
            string value = item.Content;
            if (TypeExtractor.IsArray(_instanceValue.Kind) && _instanceValue.Fields != null && _instanceValue.Fields.Length > 0)
            {
                if ((TypeExtractor.IsNonStringObjectReference(_instanceValue.Fields[0].Kind)))
                {
                    var owner = (MainWindow)Owner;
                    var addr = Utils.GetAddressValue(value);
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(() => owner.ExecuteInstanceValueQuery("Getting object value at: " + Utils.RealAddressString(addr), addr));
                }
            }
        }

        private void ItemLstGetTypeValuesReportClicked(object sender, RoutedEventArgs e)
        {
            if (_instanceValue.ArrayValues == null || _instanceValue.ArrayValues.Length < 2) return;
            if ((TypeExtractor.IsNonStringObjectReference(_instanceValue.Fields[0].Kind)))
            {
                var owner = (MainWindow)Owner;
                //System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(() => owner.ExecuteInstanceValueQuery("Getting object value at: " + Utils.RealAddressString(addr), addr));
            }

        }
    }
}
