using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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
    /// Interaction logic for TreemapView.xaml
    /// </summary>
    public partial class TreemapView : Window
    {
        const int MinSize = 1024;
        const int MinCount = 10;
        DumpIndex _index;
        bool _counts; // if false we dealing with sizes
        double _minValue;
        double[] _values;
        string[] _typeNames;
        int[] _map;

        public TreemapView(DumpIndex index, bool counts=false, int minValue = -1)
        {
            Debug.Assert(index != null);
            _index = index;
            InitializeComponent();
            _counts = counts;
            _minValue = _counts ? (minValue == -1 ? MinCount : minValue) : (minValue == -1 ? MinSize : minValue);
            if (_minValue < 1.0) _minValue = 1.0;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            string error;
            (double[] values, string[] typeNames) = _counts ? _index.GetTypeCountsWithNames(out error) : _index.GetTypeSizesWithNames(out error);
            if (error != null)
            {
                Title = "Treemap Chart cannot be generated, please copy error and close this window.";
                GuiUtils.ShowError(error, this);
                return;
            }
            int[] map = Utils.Iota(values.Length);
            Array.Sort(values, map, new DoubleDescCmp()); // need to reverse order
            int validSizeCnt = 0;
            for (int i = 0, icnt = values.Length; i < icnt; ++i)
            {
                if (values[i] < _minValue) break;
                ++validSizeCnt;
            }
            if (_counts)
            {
                Title = "Treemap Chart Type Counts " + Utils.CountString(validSizeCnt) + " out of " + Utils.CountString(values.Length)
                        + "  min count: " + Utils.JustSizeString((ulong)_minValue);
            }
            else
            {
                Title = "Treemap Chart Type Sizes " + Utils.CountString(validSizeCnt) + " out of " + Utils.CountString(values.Length)
                    + "  min size: " + Utils.JustSizeString((ulong)_minValue);
            }
            _values = new double[validSizeCnt];
            Array.Copy(values, 0, _values, 0, validSizeCnt);
            _map = new int[validSizeCnt];
            Array.Copy(map, 0, _map, 0, validSizeCnt);
            _typeNames = typeNames;

            var tm = new Treemap(_values, 0, 0, MainRectangle.Width, MainRectangle.Height);
            tm.Squarify();
            Brush brush = _counts ? Brushes.LightSlateGray : Brushes.Wheat;
            for (int i = 0, icnt = tm.Rectangles.Length; i < icnt; ++i)
            {
                var tr = tm.Rectangles[i];
                var r = new Rectangle() { Stroke = Brushes.Black, Fill = brush, Width = tr.Width, Height = tr.Height, StrokeThickness = 0.5 };
                r.Tag = i;
                    //typeNames[map[i]] + Constants.HeavyRightArrowPadded + Utils.SizeString((ulong)vsizes[i]) + " bytes";
                Canvas.SetLeft(r, tr.X);
                Canvas.SetTop(r, tr.Y);
                MainRectangle.Children.Add(r);
            }
            string typeName = typeNames[map[0]];
            (ulong[] addresses, int unrootedCnt) = GetTypeAddresses(typeName);
            AddrListBox.ItemsSource = addresses;
            AddrListBox.ContextMenu.Tag = AddrListBox;
            AddressTextBox.Text = Utils.CountString(addresses.Length) + "/" + Utils.CountString(unrootedCnt);
            TypeNameLabel.Text = typeName;
        }

        int _currentIndex = -1;
        private void TreemapMouseMove(object sender, MouseEventArgs e)
        {
            Rectangle r = e.OriginalSource as Rectangle;
            int index = (int)r.Tag;
            if (index == _currentIndex) return;
            _currentIndex = index;
            string txt = _typeNames[_map[index]] + Constants.HeavyRightArrowPadded + Utils.SizeString((ulong)_values[index]) + (_counts ? string.Empty :" bytes");
            MainTextBox.Text = txt;
        }

        private void TreemapMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Rectangle r = e.OriginalSource as Rectangle;
                int index = (int)r.Tag;
                string typeName = _typeNames[_map[index]];
                (ulong[] addresses, int unrootedCnt) = GetTypeAddresses(typeName);
                AddrListBox.ItemsSource = addresses;
                AddressTextBox.Text = Utils.CountString(addresses.Length) + "/" + Utils.CountString(unrootedCnt);
                TypeNameLabel.Text = typeName;
            }
            catch (Exception ex)
            {
                GuiUtils.ShowError(Utils.GetExceptionErrorString(ex),this);
            }
        }

        private ValueTuple<ulong[],int> GetTypeAddresses(string typeName)
        {
            int typeId = _index.GetTypeId(typeName);
            int unrootedCount;
            ulong[] instances = _index.GetTypeInstances(typeId, out unrootedCount);
            return (instances,unrootedCount);
        }

        private void AddrListBoxDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ulong addr = GuiUtils.GetAddressFromList(this, sender);
            if (addr == Constants.InvalidAddress) return;
            Dispatcher.CurrentDispatcher.InvokeAsync(() => GuiUtils.MainWindowInstance.ExecuteInstanceValueQuery("Getting object value at: " + Utils.RealAddressString(addr), addr));
        }

        private void CopyAddressSelectionClicked(object sender, RoutedEventArgs e)
        {
            ulong addr = GuiUtils.GetAddressFromList(this, sender);
            if (addr == Constants.InvalidAddress) return;
            GuiUtils.CopyToClipboard(Utils.RealAddressString(addr));
        }

        private void CopyAddressAllClicked(object sender, RoutedEventArgs e)
        {
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
            foreach (var item in AddrListBox.Items)
                sb.AppendLine(Utils.RealAddressString((ulong)item));
            string result = StringBuilderCache.GetStringAndRelease(sb);
            GuiUtils.CopyToClipboard(result);
        }

        private void AddrLstRefsClicked(object sender, RoutedEventArgs e)
        {
            ulong addr = GuiUtils.GetAddressFromList(this, sender);
            if (addr == Constants.InvalidAddress) return;
            Dispatcher.CurrentDispatcher.InvokeAsync(() => GuiUtils.MainWindowInstance.DisplayInstanceParentReferences(addr));
        }

        private void AddrLstInstSizeClicked(object sender, RoutedEventArgs e)
        {
            ulong addr = GuiUtils.GetAddressFromList(this, sender);
            if (addr == Constants.InvalidAddress) return;
            string error;
            var kv = _index.GetInstanceSizes(addr, out error);
            if (error != null)
            {
                GuiUtils.ShowError(error, this);
                return;
            }
            MainTextBox.Text = "Instance: " + Utils.RealAddressString(addr) + ", base size: " + Utils.JustSizeString(kv.Key) + ", actual size: " + Utils.JustSizeString(kv.Value);
        }

        private void AddrLstInstValueClicked(object sender, RoutedEventArgs e)
        {
            ulong addr = GuiUtils.GetAddressFromList(this, sender);
            if (addr == Constants.InvalidAddress) return;
            Dispatcher.CurrentDispatcher.InvokeAsync(() => GuiUtils.MainWindowInstance.ExecuteInstanceValueQuery("Getting object value at: " + Utils.RealAddressString(addr), addr));
        }

        private void AddrLstInstHierarchyClicked(object sender, RoutedEventArgs e)
        {
            ulong addr = GuiUtils.GetAddressFromList(this, sender);
            if (addr == Constants.InvalidAddress) return;
            Dispatcher.CurrentDispatcher.InvokeAsync(() => GuiUtils.MainWindowInstance.ExecuteInstanceHierarchyQuery("Get instance hierarchy " + Utils.AddressStringHeader(addr), addr, Constants.InvalidIndex));
        }

        private void AddrLstViewMemoryClicked(object sender, RoutedEventArgs e)
        {
            ulong addr = GuiUtils.GetAddressFromList(this, sender);
            if (addr == Constants.InvalidAddress) return;
            Dispatcher.CurrentDispatcher.InvokeAsync(() => GuiUtils.MainWindowInstance.ShowMemoryViewWindow(addr));
        }

        private void TypeCopyNameClicked(object sender, RoutedEventArgs e)
        {
            GuiUtils.CopyToClipboard(TypeNameLabel.Text);
        }

        private void TypeGenerationDistributionClicked(object sender, RoutedEventArgs e)
        {
            string typeName = TypeNameLabel.Text;
            int id = Array.BinarySearch(_typeNames, typeName, StringComparer.Ordinal);
            var genHistogram = _index.GetTypeGcGenerationHistogram(id);
            var histStr = ClrtSegment.GetGenerationHistogramSimpleString(genHistogram);
            MainTextBox.Text = histStr + " " + typeName;
        }

        private void TypeReferencesClicked(object sender, RoutedEventArgs e)
        {
            string typeName = TypeNameLabel.Text;
            int typeId = Array.BinarySearch(_typeNames, typeName, StringComparer.Ordinal);
            Dispatcher.CurrentDispatcher.InvokeAsync(() => GuiUtils.MainWindowInstance.GetParentReferences(typeName, typeId));
        }

        //private void TypeSizeDetailsReportClicked(object sender, RoutedEventArgs e)
        //{

        //}

        private void TypeValuesReportClicked(object sender, RoutedEventArgs e)
        {
            string typeName = TypeNameLabel.Text;
            int typeId = Array.BinarySearch(_typeNames, typeName, StringComparer.Ordinal);
            Dispatcher.CurrentDispatcher.InvokeAsync(() => GuiUtils.MainWindowInstance.GetTypeValuesReport(typeName, typeId));
        }
    }

    class DoubleDescCmp : IComparer<double>
    {
        public int Compare(double d1, double d2)
        {
            return d2 < d1 ? -1 : (d2 > d1 ? 1 : 0);
        }
    }
}
