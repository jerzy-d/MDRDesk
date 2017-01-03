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

		public ClassStructDisplay(int id, ConcurrentDictionary<int, Window> wndDct, string description, InstanceValue instValue)
		{
			TreeViewItem root;
			_id = id;
			_wndDct = wndDct;
			InitializeComponent();
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
				List<InstanceValue> descendants = parentNode.Values;
				for (int i = 0, icount = descendants.Count; i < icount; ++i)
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
			var selItem = InstanceValueTreeview.SelectedItem as TreeViewItem;
			if (selItem == null) return;
			var instValue = selItem.Tag as InstanceValue;
			Debug.Assert(instValue != null);

			if (instValue.HaveInnerValues()) return; // already has values

			if (instValue.Address == Constants.InvalidAddress)
			{
				StatusText.Text = "Value for " + instValue.TypeName + " cannot be expanded.";
				return;
			}

			var index = MainWindow.CurrentIndex;

			StatusText.Text = "Getting value at address: " + instValue.Address + ", please wait...";
			Mouse.OverrideCursor = Cursors.Wait;

			var result = await Task.Run(() =>
			{
				string error;
				var value = index.GetInstanceValue(instValue.Address, out error);
				if (error != null)
					return new Tuple<string, InstanceValue, string>(error, null, null);
				return new Tuple<string, InstanceValue, string>(null, value.Item1, value.Item2);
			});

			StatusText.Text = "Getting value at address: " + instValue.Address + (result.Item1 != null ? ", done." : ", failed.");
			Mouse.OverrideCursor = null;

			if (result.Item1 != null)
			{
				GuiUtils.ShowError(result.Item1,this);
				return;
			}

			var instvalue = result.Item2;

			var que = new Queue<KeyValuePair<InstanceValue, TreeViewItem>>();
			que.Enqueue(new KeyValuePair<InstanceValue, TreeViewItem>(instvalue, selItem));
			while (que.Count > 0)
			{
				var info = que.Dequeue();
				InstanceValue parentNode = info.Key;
				TreeViewItem tvParentNode = info.Value;
				List<InstanceValue> descendants = parentNode.Values;
				for (int i = 0, icount = descendants.Count; i < icount; ++i)
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
			selItem.ExpandSubtree();
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			Window wnd;
			_wndDct.TryRemove(_id, out wnd);
		}
	}
}
