using ClrMDRIndex;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

namespace MDRDesk
{
    /// <summary>
    /// Interaction logic for KeyValueCollectionDisplay.xaml
    /// </summary>
    public partial class KeyValueCollectionDisplay : Window
    {
        private int _id;
        private ConcurrentDictionary<int, Window> _wndDct;
        InstanceValue _instValue;

        public KeyValueCollectionDisplay(int id, ConcurrentDictionary<int, Window> wndDct, InstanceValue instVal, TypeExtractor.KnownTypes knownType)
        {
            _id = id;
            _wndDct = wndDct;
            InitializeComponent();
            _instValue = instVal;
            Title = TypeExtractor.GetKnowTypeName(knownType);
            CollectionInfo.Text = GuiUtils.GetExtraDataString(instVal.ExtraData as KeyValuePair<string, string>[],instVal.TypeName, instVal.Address);
            KeyValuePairs.ItemsSource = instVal.KeyValuePairs;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Window wnd;
            _wndDct.TryRemove(_id, out wnd);
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

        private void KeyValueDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            ulong addr = Constants.InvalidAddress;
            var txtBlk = (TextBlock)e.OriginalSource;
            if (txtBlk == null) return;
            string data = txtBlk.Text;
            if (data == null || data.Length < 16) return;
            if (data[data.Length - 1] != Constants.HorizontalEllipsisChar)
            {
                addr = GetAddressFromEntry(data);
                if (addr == Constants.InvalidAddress) return;
            }

            if (addr == Constants.InvalidAddress)
            {
                DependencyObject originalSource = (DependencyObject)e.OriginalSource;
                while ((originalSource != null) && !(originalSource is ListViewItem))
                {
                    originalSource = VisualTreeHelper.GetParent(originalSource);
                }
                if (originalSource != null)
                {
                    if (e.OriginalSource is TextBlock)
                    {
                        var item = ((ListView)sender).ItemContainerGenerator.ItemFromContainer(originalSource);
                        if (item is KeyValuePair<DisplayableString, DisplayableString>)
                        {
                            KeyValuePair<DisplayableString, DisplayableString> kv = (KeyValuePair<DisplayableString, DisplayableString>)item;
                            if (kv.Key.FullContent.StartsWith(data))
                            {
                                addr = GetAddressFromEntry(kv.Key.FullContent);
                            }
                            else if (kv.Value.FullContent.StartsWith(data))
                            {
                                addr = GetAddressFromEntry(kv.Value.FullContent);
                            }
                        }
                    }
                }
            }

            if (addr != Constants.InvalidAddress)
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(() => ((MainWindow)Owner).ExecuteInstanceValueQuery("Getting object value at: " + Utils.RealAddressString(addr), addr));
            }

            // TODO JRD -- display large strings

        }

        private string GetSelectionString(bool getKey)
        {
            if (KeyValuePairs.SelectedIndex < 0) return null;
            var kv = (KeyValuePair<DisplayableString, DisplayableString>)KeyValuePairs.SelectedItem;
            return getKey ? kv.Key.FullContent : kv.Value.FullContent;
        }

        private void KeyCopyClicked(object sender, RoutedEventArgs e)
        {
            string value = GetSelectionString(true); // true for key
            if (value == null) return;
            Clipboard.SetText(value);
        }

        private void CopyValueClicked(object sender, RoutedEventArgs e)
        {
            string value = GetSelectionString(false); // for value
            if (value == null) return;
            Clipboard.SetText(value);
        }

        private void KeyGetParentRefsClicked(object sender, RoutedEventArgs e)
        {

        }

        private void ValueGetParentRefsClicked(object sender, RoutedEventArgs e)
        {

        }

        private void KeyGetInstSizesClicked(object sender, RoutedEventArgs e)
        {

        }

        private void ValueGetInstSizesClicked(object sender, RoutedEventArgs e)
        {

        }

        private void KeyGetInstValueClicked(object sender, RoutedEventArgs e)
        {
            string value = GetSelectionString(true); // true for key
            if (value == null) return;
            ulong addr = GetAddressFromEntry(value);
            if (addr != Constants.InvalidAddress)
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(() => ((MainWindow)Owner).ExecuteInstanceValueQuery("Getting object value at: " + Utils.RealAddressString(addr), addr));
            }
            // TODO JRD -- display large strings
        }

        private void ValueGetInstValueClicked(object sender, RoutedEventArgs e)
        {
            string value = GetSelectionString(false); // false for value
            if (value == null) return;
            ulong addr = GetAddressFromEntry(value);
            if (addr != Constants.InvalidAddress)
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(() => ((MainWindow)Owner).ExecuteInstanceValueQuery("Getting object value at: " + Utils.RealAddressString(addr), addr));
            }
            // TODO JRD -- display large strings
        }

        private void KeyGetInstHierarchyClicked(object sender, RoutedEventArgs e)
        {

        }

        private void ValueGetInstHierarchyClicked(object sender, RoutedEventArgs e)
        {

        }

        private void KeyViewMemoryClicked(object sender, RoutedEventArgs e)
        {

        }

        private void ValueViewMemoryClicked(object sender, RoutedEventArgs e)
        {

        }

        private void ShowArrayIndicesClicked(object sender, RoutedEventArgs e)
        {

        }

        private void KeyValueCopyAllClicked(object sender, RoutedEventArgs e)
        {
            var data = _instValue.KeyValuePairs;
            string addrStr = Utils.RealAddressStringHeader(_instValue.Address);
            long size = addrStr.Length + _instValue.TypeName.Length + Environment.NewLine.Length;
            for (int i = 0, icnt = data.Length; i < icnt; ++i)
            {
                var kv = data[i];
                size += kv.Key.SizeInBytes;
                size += kv.Value.SizeInBytes;
                size += Environment.NewLine.Length + Constants.HeavyGreekCrossPadded.Length;
            }
            if (size < 64*1024)
            {
                StringBuilder sb = new StringBuilder((int)size);
                sb.Append(addrStr).AppendLine(_instValue.TypeName);
                for (int i = 0, icnt = data.Length; i < icnt; ++i)
                {
                    var kv = data[i];
                    sb.Append(kv.Key.FullContent).Append(Constants.HeavyGreekCrossPadded).Append(kv.Value.FullContent);
                    sb.AppendLine();
                }
                Clipboard.SetText(sb.ToString());
                MessageBox.Show("Collection content copied to clipboard.", string.Empty, MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // otherwise write to a file TODO JRD
        }
    }
}
