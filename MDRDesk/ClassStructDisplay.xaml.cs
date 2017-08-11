using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClrMDRIndex;

namespace MDRDesk
{
	/// <summary>
	/// Interaction logic for ClassStructDisplay.xaml
	/// </summary>
	public partial class ClassStructDisplay : IValueWindow
	{
		private int _id;
		//private ConcurrentDictionary<int, Window> _wndDct;
        private MainWindow _mainWindow;
        private ValueWindows.WndType _wndType;
        private bool _locked;

        public ValueWindows.WndType WndType => _wndType;
        public int Id => _id;
        public bool Locked => _locked;
        private Image _lockedImg, _unlockedImg;
        Button _lockBtn;


        public ClassStructDisplay(int id, string description, InstanceValue instValue, bool locked = false)
		{
            _wndType = ValueWindows.WndType.Tree;
			_id = id;
            _locked = locked;
			InitializeComponent();
            _mainWindow = GuiUtils.MainWindowInstance;
            _lockedImg = ValueWindows.LockedImage;
            _unlockedImg = ValueWindows.UnlockedImage;
            _lockBtn = new Button();
            _lockBtn.Click += LockBtnClicked;
            _lockBtn.ToolTip = "Lock/unlock content of this window.";
            LockBtnBarItem.Content = _lockBtn;
            UpdateInstanceValue(instValue, description);
            //_lockBtn.Content = locked ? _lockedImg : _unlockedImg;
            _lockBtn.Content = locked ? Constants.HeavyCheckMarkPadded : Constants.FilterHeader;
        }

        public void UpdateInstanceValue(InstanceValue instVal, string descr)
		{
            ClassStructInfo.Text = descr;
            var stackPanel = new StackPanel() { Orientation = Orientation.Horizontal };
			var textBlk = new TextBlock();
			textBlk.Inlines.Add(instVal.ToString());
			stackPanel.Children.Add(textBlk);
			var tvRoot = new TreeViewItem
			{
				Header = GuiUtils.GetInstanceValueStackPanel(instVal),
				Tag = instVal
			};

			var que = new Queue<KeyValuePair<InstanceValue, TreeViewItem>>();
			que.Enqueue(new KeyValuePair<InstanceValue, TreeViewItem>(instVal, tvRoot));
			while (que.Count > 0)
			{
				var info = que.Dequeue();
				InstanceValue parentNode = info.Key;
				TreeViewItem tvParentNode = info.Value;
				InstanceValue[] descendants = parentNode.Fields;
				for (int i = 0, icount = descendants.Length; i < icount; ++i)
				{
					var descNode = descendants[i];
					var tvNode = new TreeViewItem
					{
						Header = GuiUtils.GetInstanceValueStackPanel(descNode),
						Tag = descNode
					};
					tvParentNode.Items.Add(tvNode);
					que.Enqueue(new KeyValuePair<InstanceValue, TreeViewItem>(descNode, tvNode));
				}
			}

			var treeView = InstanceValueTreeview;
			treeView.Items.Clear();
			treeView.Items.Add(tvRoot);
			tvRoot.IsSelected = true;
			tvRoot.ExpandSubtree();
		}

		private void EventSetter_OnHandler(object sender, RequestBringIntoViewEventArgs e)
		{
			e.Handled = true;
		}

        private bool GetSelectedItem(out TreeViewItem selTreeItem, out InstanceValue inst)
        {
            inst = null;
            selTreeItem = InstanceValueTreeview.SelectedItem as TreeViewItem;
            if (selTreeItem == null) return false;
            var selInstValue = selTreeItem.Tag as InstanceValue;
            Debug.Assert(selInstValue != null);
            inst = selInstValue;
            return true;
        }

        private async void InstanceValueTreeview_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
            TreeViewItem selTreeItem;
            InstanceValue selInstValue;
            if (!GetSelectedItem(out selTreeItem, out selInstValue)) return;

            // TODO JRD -- 
            //if (TypeExtractor.IsString(selInstValue.Kind))
            //{
            //    if (selInstValue.Value.IsLong())
            //    {
            //        var wnd = new ContentDisplay(Utils.GetNewID(), _wndDct, selInstValue.GetDescription(), selInstValue) { Owner = _mainWindow };
            //        wnd.Show();
            //    }
            //    return;
            //}

            if (selInstValue.HaveFields()) return; // already has values

            ulong addr = _mainWindow.GetAddressFromEntry(selInstValue.Value.FullContent);
            if (selInstValue.Address != Constants.InvalidAddress && addr != Constants.InvalidAddress && addr == selInstValue.Address)
            {
                if (StatusRawMode.IsChecked.Value == false) // if known collection show it in a collection window
                {
                    if (TypeExtractor.IsKnownType(selInstValue.TypeName))
                    {
                        var msg = "Getting object value at: " + Utils.RealAddressString(selInstValue.Address);
                        _mainWindow.ExecuteInstanceValueQuery(msg, selInstValue.Address);
                        return;
                    }
                }
                var index = MainWindow.CurrentIndex;

                StatusText.Text = "Getting value at address: " + selInstValue.Address + ", please wait...";
                Mouse.OverrideCursor = Cursors.Wait;

                (string error, InstanceValue[] fields) = await Task.Factory.StartNew(() =>
                {
                    return index.GetInstanceValueFields(selInstValue.Address, selInstValue.Parent);
                }, _mainWindow.DumpSTAScheduler);


                if (Utils.IsInformation(error))
                    StatusText.Text = error;
                else
                    StatusText.Text = "Getting fields at address: " + selInstValue.Address + (fields != null ? ", done." : ", failed.");
                Mouse.OverrideCursor = null;

                if (error != null && !Utils.IsInformation(error))
                {
                    GuiUtils.ShowError(error, this);
                    return;
                }

                // TODO JRD
                //if (fields.Length == 1 && fields[0].IsArray())
                //{
                //    var wnd = new CollectionDisplay(Utils.GetNewID(), _wndDct, fields[0], fields[0].GetDescription(), TypeExtractor.KnownTypes.Unknown) { Owner = Application.Current.MainWindow };
                //    wnd.Show();
                //    return;
                //}

                if (fields.Length > 0)
                {
                    selInstValue.SetFields(fields);
                    for (int i = 0, icount = fields.Length; i < icount; ++i)
                    {
                        var fld = fields[i];
                        var tvNode = new TreeViewItem
                        {
                            Header = GuiUtils.GetInstanceValueStackPanel(fld),
                            Tag = fld
                        };
                        selTreeItem.Items.Add(tvNode);
                    }
                }

                selTreeItem.ExpandSubtree();

            }
            else
			{
				StatusText.Text = "Value for " + selInstValue.TypeName + " cannot be expanded.";
				return;
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
            var wndtask = Task.Factory.StartNew(() => ValueWindows.RemoveWindow(_id, _wndType));
            wndtask.Wait();
		}

        private void InstanceValueCopyAddressClicked(object sender, RoutedEventArgs e)
        {
            TreeViewItem selTreeItem;
            InstanceValue selInstValue;
            if (!GetSelectedItem(out selTreeItem, out selInstValue)) return;
            if (selInstValue.Address == Constants.InvalidAddress)
            {
                StatusText.Text = "The value's address is invalid.";
                return;
            }
            var addrStr = Utils.RealAddressString(selInstValue.Address);
            Clipboard.SetText(addrStr);
            StatusText.Text = "The address was copied to the clipboard.";
        }

        private void InstanceValueCopyValueClicked(object sender, RoutedEventArgs e)
        {
            TreeViewItem selTreeItem;
            InstanceValue selInstValue;
            if (!GetSelectedItem(out selTreeItem, out selInstValue)) return;
            var val = selInstValue.Value.FullContent;
            Clipboard.SetText(val);
            StatusText.Text = "The value was copied to the clipboard";
        }

        private void InstanceValueViewMemoryClicked(object sender, RoutedEventArgs e)
        {
            TreeViewItem selTreeItem;
            InstanceValue selInstValue;
            if (!GetSelectedItem(out selTreeItem, out selInstValue)) return;
            if (selInstValue.Address == Constants.InvalidAddress)
            {
                StatusText.Text = "The value's address is invalid.";
                return;
            }
            var addr = Utils.RealAddress(selInstValue.Address);
            _mainWindow.ShowMemoryViewWindow(addr);
        }

        private void InstanceValueGetValueClicked(object sender, RoutedEventArgs e)
        {
            TreeViewItem selTreeItem;
            InstanceValue selInstValue;
            if (!GetSelectedItem(out selTreeItem, out selInstValue)) return;
            if (selInstValue.Address == Constants.InvalidAddress)
            {
                StatusText.Text = "The value's address is invalid.";
                return;
            }

            // TODO JRD -- ContentDisplay, get this back
            //if (selInstValue.Value.IsLong())
            //{
            //    var wnd = new ContentDisplay(Utils.GetNewID(), _wndDct, selInstValue.GetDescription(), selInstValue) { Owner = _mainWindow };
            //    wnd.Show();
            //    return;
            //}
        }

        private void LockBtnClicked(object sender, RoutedEventArgs e)
        {
            if (_locked)
            {
                _locked = false;
                //_lockBtn.Content = _unlockedImg;
                _lockBtn.Content = Constants.FilterHeader;                    ;
                ValueWindows.ChangeMyLock(_id, _wndType, false);
            }
            else
            {
                _locked = true;
                //_lockBtn.Content = _lockedImg;
                _lockBtn.Content = Constants.HeavyCheckMarkPadded;
                ValueWindows.ChangeMyLock(_id, _wndType, true);
            }
        }


        // https://stackoverflow.com/questions/16319063/wpf-change-button-background-image-when-clicked
    }
}
