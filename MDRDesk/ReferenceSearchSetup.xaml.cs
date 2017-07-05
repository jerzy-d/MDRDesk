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
		private static InstanceReferences.ReferenceType _dataSource = InstanceReferences.ReferenceType.All;
		public InstanceReferences.ReferenceType DataSource => _dataSource;
		private static InstanceReferences.ReferenceType _direction = InstanceReferences.ReferenceType.Ancestors;
		public InstanceReferences.ReferenceType Direction => _direction;
        public InstanceReferences.ReferenceType Strict => (RefSearchStrict.IsChecked != null && (bool)RefSearchStrict.IsChecked) ? InstanceReferences.ReferenceType.Strict : InstanceReferences.ReferenceType.None;

        public bool Cancelled => !_okExit;
        public bool GetAllReferences => RefSearchAllLevels.IsChecked != null && (bool)RefSearchAllLevels.IsChecked;

        public int SearchDepthLevel => _searchDepthLevel;

		public ReferenceSearchSetup(string descr)
		{
			InitializeComponent();
			RefSearchInformation.Text = descr;
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
				case InstanceReferences.ReferenceType.Rooted:
					RefSearchRooted.IsChecked = true;
					break;
				case InstanceReferences.ReferenceType.All:
					RefSearchAll.IsChecked = true;
					break;
				case InstanceReferences.ReferenceType.Unrooted:
					RefSearchNotRooted.IsChecked = true;
					break;
				default:
					RefSearchAll.IsChecked = true;
					break;
			}

			switch (_direction)
			{
				case InstanceReferences.ReferenceType.Ancestors:
					RefSearchFieldParents.IsChecked = true;
					break;
				case InstanceReferences.ReferenceType.Descendants:
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
				_dataSource = InstanceReferences.ReferenceType.Rooted;
			else if (RefSearchAll.IsChecked == true)
				_dataSource = InstanceReferences.ReferenceType.All;
            else if (RefSearchNotRooted.IsChecked == true)
                _dataSource = InstanceReferences.ReferenceType.Unrooted;
            else if (RefSearchFinalizer.IsChecked == true)
                _dataSource = InstanceReferences.ReferenceType.Finalizer;
            else
                _dataSource = InstanceReferences.ReferenceType.All;

			if (RefSearchFieldParents.IsChecked == true)
				_direction = InstanceReferences.ReferenceType.Ancestors;
			else if (RefSearchParentFields.IsChecked == true)
				_direction = InstanceReferences.ReferenceType.Descendants;
			else
				_direction = InstanceReferences.ReferenceType.Ancestors;

			DialogResult = true;
		}
	}
}
