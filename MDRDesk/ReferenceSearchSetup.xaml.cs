using System;
using System.Windows;
using ClrMDRIndex;

namespace MDRDesk
{
	/// <summary>
	/// Interaction logic for ReferenceSearchSetup.xaml
	/// </summary>
	public partial class ReferenceSearchSetup
	{
		public enum DispMode
		{
			List, Tree, Graph
		}

		private static int _searchDepthLevel = 4;
		private bool _okExit;
		private static DispMode _displayMode = DispMode.Tree;
		public DispMode DisplayMode => _displayMode;
		private static References.DataSource _dataSource = References.DataSource.All;
		public References.DataSource DataSource => _dataSource;
		private static References.Direction _direction = References.Direction.FieldParent;
		public References.Direction Direction => _direction;

		public bool Cancelled => !_okExit;
		public bool GetAllReferences => RefSearchAll.IsChecked != null && (bool) RefSearchAll.IsChecked;

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
				default:
					RefSearchDisplayTree.IsChecked = true;
					break;
			}
			switch (_dataSource)
			{
				case References.DataSource.Rooted:
					RefSearchRooted.IsChecked = true;
					break;
				case References.DataSource.All:
					RefSearchBoth.IsChecked = true;
					break;
				case References.DataSource.Unrooted:
					RefSearchNotRooted.IsChecked = true;
					break;
				default:
					RefSearchBoth.IsChecked = true;
					break;
			}

			switch (_direction)
			{
				case References.Direction.FieldParent:
					RefSearchFieldParents.IsChecked = true;
					break;
				case References.Direction.ParentField:
					RefSearchParentFields.IsChecked = true;
					break;
				default:
					RefSearchFieldParents.IsChecked = true;
					break;
			}
		}

		private void DialogOkClicked(object sender, RoutedEventArgs e)
		{
			_okExit = true;

			int level;
			if (Int32.TryParse(RefSearchLevel.Text, out level)) _searchDepthLevel = level;
			else _searchDepthLevel = 4;

			if (RefSearchDisplayList.IsChecked == true) _displayMode = DispMode.List;
			else if (RefSearchDisplayTree.IsChecked == true) _displayMode = DispMode.Tree;
			else if (RefSearchDisplayGraph.IsChecked == true) _displayMode = DispMode.Graph;
			else _displayMode = DispMode.Tree;

			if (RefSearchRooted.IsChecked == true)
				_dataSource = References.DataSource.Rooted;
			else if (RefSearchBoth.IsChecked == true)
				_dataSource = References.DataSource.All;
			else if (RefSearchNotRooted.IsChecked == true)
				_dataSource = References.DataSource.Unrooted;
			else
				_dataSource = References.DataSource.All;

			if (RefSearchFieldParents.IsChecked == true)
				_direction = References.Direction.FieldParent;
			else if (RefSearchParentFields.IsChecked == true)
				_direction = References.Direction.ParentField;
			else
				_direction = References.Direction.FieldParent;

			DialogResult = true;
		}
	}
}
