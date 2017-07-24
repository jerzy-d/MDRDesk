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
	public partial class ClassStructDisplay
	{
		private int _id;
		private ConcurrentDictionary<int, Window> _wndDct;
        private MainWindow _mainWindow;

		public ClassStructDisplay(int id, ConcurrentDictionary<int, Window> wndDct, string description, InstanceValue instValue)
		{
			TreeViewItem root;
			_id = id;
			_wndDct = wndDct;
			InitializeComponent();
            _mainWindow = GuiUtils.MainWindowInstance;
            ClassStructInfo.Text = description;
			UpdateInstanceValue(instValue, out root);
			wndDct.TryAdd(id, this);
		}

		private void UpdateInstanceValue(InstanceValue instVal, out TreeViewItem tvRoot)
		{
			var stackPanel = new StackPanel() { Orientation = Orientation.Horizontal };
			var textBlk = new TextBlock();
			textBlk.Inlines.Add(instVal.ToString());
			stackPanel.Children.Add(textBlk);
			tvRoot = new TreeViewItem
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

		private async void InstanceValueTreeview_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var selTreeItem = InstanceValueTreeview.SelectedItem as TreeViewItem;
			if (selTreeItem == null) return;
			var selInstValue = selTreeItem.Tag as InstanceValue;
			Debug.Assert(selInstValue != null);

			if (selInstValue.HaveFields()) return; // already has values

			if (selInstValue.Address == Constants.InvalidAddress)
			{
				StatusText.Text = "Value for " + selInstValue.TypeName + " cannot be expanded.";
				return;
			}

			var index = MainWindow.CurrentIndex;

			StatusText.Text = "Getting value at address: " + selInstValue.Address + ", please wait...";
			Mouse.OverrideCursor = Cursors.Wait;

			//(string error, InstanceValue[] fields) = await Task.Run(() =>
			//{
   //             return index.GetInstanceValueFields(selInstValue.Address, selInstValue.Parent);
			//});

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
				GuiUtils.ShowError(error,this);
				return;
			}

			if (fields.Length == 1 && fields[0].IsArray())
			{
				var wnd = new CollectionDisplay(Utils.GetNewID(), _wndDct, fields[0], MainWindow.GetInstanceValueDescription(fields[0])) { Owner = Application.Current.MainWindow };
				wnd.Show();
				return;
			}

			if (fields.Length > 0)
            {
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

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			Window wnd;
			_wndDct.TryRemove(_id, out wnd);
		}
	}
}
