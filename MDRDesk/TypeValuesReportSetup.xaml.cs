using System;
using System.Collections.Generic;
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

namespace MDRDesk
{
	/// <summary>
	/// Interaction logic for TypeValuesReportSetup.xaml
	/// </summary>
	public partial class TypeValuesReportSetup : Window
	{
		private ClrtDisplayableType _typeInfo;
		private TypeValuesQuery _query;
		private TreeViewItem _currentTreeViewItem;


		public TypeValuesReportSetup(ClrtDisplayableType typeInfo)
		{
			_typeInfo = typeInfo;
			_query = new TypeValuesQuery();
			InitializeComponent();
			TypeValueReportTopTextBox.Text = typeInfo.GetDescription();
			UpdateTypeValueSetupGrid(typeInfo,null);
		}

		private void UpdateTypeValueSetupGrid(ClrtDisplayableType dispType, TreeViewItem root)
		{
			bool realRoot = false;
			if (root == null)
			{
				realRoot = true;
				root = GuiUtils.GetTypeValueSetupTreeViewItem(dispType);
			}
			var fields = dispType.Fields;
			for (int i = 0, icnt = fields.Length; i < icnt; ++i)
			{
				var fld = fields[i];
				var node = GuiUtils.GetTypeValueSetupTreeViewItem(fld);
				root.Items.Add(node);
			}

			if (realRoot)
			{
				TypeValueReportTreeView.Items.Clear();
				TypeValueReportTreeView.Items.Add(root);
			}
			root.ExpandSubtree();
		}


		private async void TypeValueReportTreeView_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			TreeView tv = sender as TreeView;
			var selItem = tv.SelectedItem as TreeViewItem;
			Debug.Assert(selItem != null);
			var dispType = selItem.Tag as ClrtDisplayableType;
			Debug.Assert(dispType != null);

			string msg;
			if (!dispType.CanGetFields(out msg))
			{
				StatusText.Text = "Action failed for: '" + dispType.FieldName + "'. " + msg;
				return;
			}

			if (dispType.FieldIndex == Constants.InvalidIndex) // this root type, fields are already displayed
			{
				return;
			}

			var parent = selItem.Parent as TreeViewItem;
			Debug.Assert(parent != null);
			var parentDispType = parent.Tag as ClrtDisplayableType;
			Debug.Assert(parentDispType != null);

			StatusText.Text = "Getting type details for field: '" + dispType.FieldName + "', please wait...";
			Mouse.OverrideCursor = Cursors.Wait;

			var result = await Task.Run(() =>
			{
				string error;
				ClrtDisplayableType fldDispType = MainWindow.CurrentIndex.GetTypeDisplayableRecord(parentDispType, dispType, out error);
				if (fldDispType == null)
					return new Tuple<string, ClrtDisplayableType>(error, null);
				return new Tuple<string, ClrtDisplayableType>(null, fldDispType);
			});
			Mouse.OverrideCursor = null;

			if (result.Item1 != null)
			{
				if (Utils.IsInformation(result.Item1))
				{
					StatusText.Text = "Action failed for: '" + dispType.FieldName + "'. " + result.Item1;
					return;
				}
				StatusText.Text = "Getting type details for field: '" + dispType.FieldName + "', failed";
				GuiUtils.ShowError(result.Item1,this);
				return;
			}

			var fields = result.Item2.Fields;
			selItem.Items.Clear();
			for (int i = 0, icnt = fields.Length; i < icnt; ++i)
			{
				var fld = fields[i];
				var node = GuiUtils.GetTypeValueSetupTreeViewItem(fld);
				selItem.Items.Add(node);
			}
			selItem.ExpandSubtree();

			StatusText.Text = "Getting type details for field: '" + dispType.FieldName + "', done";
		}

		private void TypeValueReportSelectMenuitem_OnClick(object sender, RoutedEventArgs e)
		{
			if (_currentTreeViewItem == null) return;
			var curDispItem = _currentTreeViewItem.Tag as ClrtDisplayableType;
			Debug.Assert(curDispItem != null);
			curDispItem.ToggleGetValue();
			GuiUtils.UpdateTypeValueSetupTreeViewItem(_currentTreeViewItem, curDispItem);
		}

		private void TypeValueReportFilterMenuitem_OnClick(object sender, RoutedEventArgs e)
		{
			if (_currentTreeViewItem == null) return;
			var curDispItem = _currentTreeViewItem.Tag as ClrtDisplayableType;
			Debug.Assert(curDispItem != null);
			var dlg = new TypeValueFilterDlg(_currentTreeViewItem.Tag as ClrtDisplayableType) { Owner = this };
			bool? dlgResult = dlg.ShowDialog();
			if (dlgResult == true)
			{
				string val = dlg.Value;
				if (string.IsNullOrEmpty(val))
					curDispItem.RemoveFilter();
				else
					curDispItem.SetFilter(new FilterValue(val));
//				_currentTreeViewItem.Header = curDispItem.ToString();
				GuiUtils.UpdateTypeValueSetupTreeViewItem(_currentTreeViewItem, curDispItem);
			}
		}

		private void EventSetter_OnHandler(object sender, RequestBringIntoViewEventArgs e)
		{
			e.Handled = true;
		}

		private void TypeValueReportTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			var item = e.NewValue as TreeViewItem;
			if (item != null)
			{
				_currentTreeViewItem = item;
				var typeInfo = _currentTreeViewItem.Tag as ClrtDisplayableType;
				Debug.Assert(typeInfo!=null);
				TypeValueReportTopTextBox.Text = _typeInfo.GetDescription() + " ...selected: " + typeInfo.FieldName + " / " + typeInfo.TypeName;

			}
		}
	}
}
