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
            CollectionInfo.Text = GetExtraDataString(instVal.ExtraData as KeyValuePair<string, string>[],instVal.TypeName, instVal.Address);
            KeyValuePairs.ItemsSource = instVal.KeyValuePairs;
        }

        private string GetExtraDataString(KeyValuePair<string, string>[] data, string typeName, ulong addr)
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Window wnd;
            _wndDct.TryRemove(_id, out wnd);
        }

        private void KeyValueDoubleClicked(object sender, MouseButtonEventArgs e)
        {

        }

        private void KeyCopyClicked(object sender, RoutedEventArgs e)
        {

        }

        private void CopyValueClicked(object sender, RoutedEventArgs e)
        {

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

        }

        private void ValueGetInstValueClicked(object sender, RoutedEventArgs e)
        {

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
