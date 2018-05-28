using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClrMDRIndex;
using System.Text;

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

        public ClassStructDisplay(int id, string description, InstanceValue instValue, bool locked = true)
		{
            _wndType = ValueWindows.WndType.Tree;
			_id = id;
            _locked = locked;
			InitializeComponent();
            _mainWindow = GuiUtils.MainWindowInstance;
            _lockedImg = new Image();
            _lockedImg.Source = ValueWindows.LockedImage.Source;
            _unlockedImg = new Image();
            _unlockedImg.Source = ValueWindows.UnlockedImage.Source;
            UpdateInstanceValue(instValue, description);
            _locked = locked;
            LockBtn.Content = locked ? _lockedImg : _unlockedImg;
        }

        public void UpdateInstanceValue(InstanceValue instVal, string descr)
		{
            if (instVal.Parent == null)
                Title = TypeExtractor.GetDisplayableTypeName(instVal.TypeName);
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

        private ValueTuple<bool, TreeViewItem, InstanceValue>  GetSelectedItem()
        {
            TreeViewItem selTreeItem = InstanceValueTreeview.SelectedItem as TreeViewItem;
            if (selTreeItem == null) return (false,null,null);
            var selInstValue = selTreeItem.Tag as InstanceValue;
            Debug.Assert(selInstValue != null);
            if (selInstValue.Parent == null) return (false, null, null);
            return (true, selTreeItem, selInstValue);
        }

        private async void GetInstanceValue(TreeViewItem selTreeItem, InstanceValue selInstValue, bool rawValue=false)
		{
            if (TypeExtractor.IsString(selInstValue.Kind))
            {
                if (selInstValue.Value.IsLong())
                {
                    ValueWindows.ShowContentWindow(selInstValue.GetDescription(), selInstValue, ValueWindows.WndType.Content);
                }
                return;
            }

            if (selInstValue.HaveFields()) return; // already has values

            ulong addr = _mainWindow.GetAddressFromEntry(selInstValue.Value.FullContent);
            if (selInstValue.Address != Constants.InvalidAddress && addr != Constants.InvalidAddress && addr == selInstValue.Address)
            {
                if (!rawValue) // if known collection show it in a collection window
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

                if (fields.Length < 1) return;

                if (fields.Length == 1 && fields[0].IsArray())
                {
                    ValueWindows.ShowContentWindow(fields[0].GetDescription(), fields[0], ValueWindows.WndType.List);
                    return;
                }
                 
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
		}

        private void InstanceValueTreeview_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            (bool ok, TreeViewItem selTreeItem, InstanceValue selInstValue) = GetSelectedItem();
            if (!ok) return;
            GetInstanceValue(selTreeItem, selInstValue);
        }

        private void InstanceValueCopyAddressClicked(object sender, RoutedEventArgs e)
        {
            (bool ok, TreeViewItem selTreeItem, InstanceValue selInstValue) = GetSelectedItem();
            if (!ok) return;
            if (selInstValue.Address == Constants.InvalidAddress)
            {
                StatusText.Text = "The value's address is invalid.";
                return;
            }
            var addrStr = Utils.RealAddressString(selInstValue.Address);
            GuiUtils.CopyToClipboard(addrStr);
            StatusText.Text = "The address was copied to the clipboard.";
        }

        private void InstanceValueCopyValueClicked(object sender, RoutedEventArgs e)
        {
            (bool ok, TreeViewItem selTreeItem, InstanceValue selInstValue) = GetSelectedItem();
            if (!ok) return;
            var val = selInstValue.Value.FullContent;
            GuiUtils.CopyToClipboard(val);
            StatusText.Text = "The value was copied to the clipboard";
        }

        private void InstanceValueCopyEntry(object sender, RoutedEventArgs e)
        {
            TreeViewItem selTreeItem = InstanceValueTreeview.SelectedItem as TreeViewItem;
            if (selTreeItem == null) return;

            var sp = selTreeItem.Header as StackPanel;
            var tb = sp.Children[0] as TextBlock;

            StringBuilder s = new StringBuilder();
            foreach (var line in tb.Inlines)
            {
                if (line is System.Windows.Documents.Run)
                    s.Append(((System.Windows.Documents.Run)line).Text).Append(" ");
            }
            var text = s.ToString().Trim();
            GuiUtils.CopyToClipboard(text);
            StatusText.Text = "The entry was copied to the clipboard";
        }

        private void InstanceValueViewMemoryClicked(object sender, RoutedEventArgs e)
        {
            (bool ok, TreeViewItem selTreeItem, InstanceValue selInstValue) = GetSelectedItem();
            if (!ok) return;
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
            (bool ok, TreeViewItem selTreeItem, InstanceValue selInstValue) = GetSelectedItem();
            if (!ok) return;
            GetInstanceValue(selTreeItem, selInstValue);
        }

        private void InstanceValueGetValueRawClicked(object sender, RoutedEventArgs e)
        {
            (bool ok, TreeViewItem selTreeItem, InstanceValue selInstValue) = GetSelectedItem();
            if (!ok) return;
            GetInstanceValue(selTreeItem, selInstValue,true);
        }

        private void ButtonHelpClicked(object sender, RoutedEventArgs e)
        {
            ValueWindows.ShowHelpWindow(Setup.HelpFolder + Path.DirectorySeparatorChar + @"\Documentation\ValueWindows.md");
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
                GuiUtils.ShowError(ex, this);
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
