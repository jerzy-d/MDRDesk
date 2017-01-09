using System;
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
	/// Interaction logic for ReferenceSearchSetup.xaml
	/// </summary>
	public partial class ReferenceSearchSetup : Window
	{
		public enum DispMode
		{
			List, Tree, Graph
		}

		private static int _searchDepthLevel = 4;
		private bool _okExit = false;
		private static DispMode _displayMode = DispMode.Tree;
		private static References.DataSource _dataSource = References.DataSource.RootedFields;

		public bool Cancelled => !_okExit;
		public DispMode DisplayMode => _displayMode;
		public bool GetAllReferences => (bool)RefSearchAll.IsChecked;
		public int SearchDepthLevel => _searchDepthLevel;

		public ReferenceSearchSetup(string descr)
		{
			InitializeComponent();
			RefSearchInformation.Content = descr;
			RefSearchAll.IsChecked = false;
			RefSearchLevel.Text = _searchDepthLevel.ToString();
			switch (_displayMode)
			{
				case DispMode.List:
					RefSearchDisplayList.IsChecked = true;
					break;
				case DispMode.Tree:
					RefSearchDisplayTree.IsChecked = true;
					break;
				case DispMode.Graph:
					RefSearchDisplayGraph.IsChecked = true;
					break;
			}
			switch (_dataSource)
			{
				case References.DataSource.RootedParents:
					RefSearchPrntRooted.IsChecked = true;
					break;
				case References.DataSource.RootedFields:
					RefSearchRooted.IsChecked = true;
					break;
				case References.DataSource.AllFields:
					RefSearchBoth.IsChecked = true;
					break;
				case References.DataSource.UnrootedParents:
					RefSearchPrntNotRooted.IsChecked = true;
					break;
				case References.DataSource.UnrootedFields:
					RefSearchPrntNotRooted.IsChecked = true;
					break;
				case References.DataSource.AllParents:
					RefSearchPrntBoth.IsChecked = true;
					break;
				default:
					RefSearchRooted.IsChecked = true;
					break;
			}
		}

		private void DialogOkClicked(object sender, RoutedEventArgs e)
		{
			_okExit = true;
			int level;
			if (Int32.TryParse(RefSearchLevel.Text, out level))
			{
				_searchDepthLevel = level;
			}
			if (RefSearchDisplayList.IsChecked == true) _displayMode = DispMode.List;
			else if (RefSearchDisplayTree.IsChecked == true) _displayMode = DispMode.Tree;
			else if (RefSearchDisplayGraph.IsChecked == true) _displayMode = DispMode.Graph;

			if (RefSearchPrntRooted.IsChecked == true)
				_dataSource = References.DataSource.RootedParents;
			else if (RefSearchRooted.IsChecked == true)
				_dataSource = References.DataSource.RootedFields;
			else if (RefSearchBoth.IsChecked == true)
				_dataSource = References.DataSource.AllFields;

			else if (RefSearchPrntNotRooted.IsChecked == true)
				_dataSource = References.DataSource.UnrootedParents;
			else if (RefSearchPrntNotRooted.IsChecked == true)
				_dataSource = References.DataSource.UnrootedFields;
			else if (RefSearchPrntBoth.IsChecked == true)
				_dataSource = References.DataSource.AllParents;

			this.DialogResult = true;
		}
	}
}
