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
using ClrMDRIndex;

namespace MDRDesk
{
    /// <summary>
    /// Interaction logic for KeyValueCollectionDisplay.xaml
    /// </summary>
    public partial class KeyValueCollectionDisplay : Window, IValueWindow
    {
        private int _id;
        InstanceValue _instValue;
        private ValueWindows.WndType _wndType;
        private bool _locked;
        public ValueWindows.WndType WndType => _wndType;
        public int Id => _id;
        public bool Locked => _locked;
        private Image _lockedImg, _unlockedImg;

        public KeyValueCollectionDisplay(int id, string description, InstanceValue instVal, bool locked = true)
        {
            _wndType = ValueWindows.WndType.KeyValues;
            _id = id;
             InitializeComponent();
            _lockedImg = new Image();
            _lockedImg.Source = ValueWindows.LockedImage.Source;
            _unlockedImg = new Image();
            _unlockedImg.Source = ValueWindows.UnlockedImage.Source;
            UpdateInstanceValue(instVal, description);
            _locked = locked;
            LockBtn.Content = locked ? _lockedImg : _unlockedImg;
        }

        public void UpdateInstanceValue(InstanceValue instVal, string descr)
        {
            _instValue = instVal;
            Title = TypeExtractor.GetDisplayableTypeName(instVal.TypeName);
            CollectionInfo.Text = GuiUtils.GetExtraDataString(instVal.ExtraData as KeyValuePair<string, string>[], instVal.TypeName, instVal.Address);
            KeyValuePairs.ItemsSource = instVal.KeyValuePairs;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var wndtask = Task.Factory.StartNew(() => ValueWindows.RemoveWindow(_id, _wndType));
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
            var txtBlk = e.OriginalSource as TextBlock;
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
            GuiUtils.CopyToClipboard(value);
        }

        private void CopyValueClicked(object sender, RoutedEventArgs e)
        {
            string value = GetSelectionString(false); // for value
            if (value == null) return;
            GuiUtils.CopyToClipboard(value);
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
            GetInstValueClicked(true); // true for key
        }

        private void ValueGetInstValueClicked(object sender, RoutedEventArgs e)
        {
            GetInstValueClicked(false); // false for value
        }

        private void GetInstValueClicked(bool forKey)
        {
            string value = GetSelectionString(forKey); // true for key
            if (value == null) return;
            ulong addr = GetAddressFromEntry(value);
            if (addr != Constants.InvalidAddress)
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(() => ((MainWindow)Owner).ExecuteInstanceValueQuery("Getting object value at: " + Utils.RealAddressString(addr), addr));
            }
            if (DisplayableString.IsLargeString(value))
            {
                var inst = new InstanceValue(Constants.InvalidIndex, ClrElementKind.Unknown, Constants.InvalidAddress, "FROM: " + _instValue.TypeName, null, value);
                ValueWindows.ShowContentWindow("A collection item " + (forKey ? "key." : "value."), inst, ValueWindows.WndType.Content);
                return;
            }
            ((MainWindow)Owner).MainStatusShowMessage("The value requested cannot be elaborated more. The values is what you see.");
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
                GuiUtils.CopyToClipboard(sb.ToString());
                MessageBox.Show("Collection content copied to clipboard.", string.Empty, MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // otherwise write to a file TODO JRD
        }

        private void ButtonHelpClicked(object sender, RoutedEventArgs e)
        {
            ValueWindows.ShowHelpWindow(Setup.HelpFolder + System.IO.Path.DirectorySeparatorChar + @"\Documentation\ValueWindows.md");
        }

        private void AddHotKeys()
        {
            try
            {
                RoutedCommand firstSettings = new RoutedCommand();
                firstSettings.InputGestures.Add(new KeyGesture(Key.F1));
                CommandBindings.Add(new CommandBinding(firstSettings, ButtonHelpClicked));
            }
            catch (Exception ex)
            {
                GuiUtils.ShowError(Utils.GetExceptionErrorString(ex), this);
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
