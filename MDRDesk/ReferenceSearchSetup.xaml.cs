using System;
using System.Windows;
using System.Windows.Controls;
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

            _dataSource = InstanceReferences.ReferenceType.None;
            if (RefSearchAll.IsChecked == true)
                _dataSource = InstanceReferences.ReferenceType.All;
            else
            {
                if (RefSearchRooted.IsChecked == true)
                    _dataSource = InstanceReferences.ReferenceType.Rooted;
                else if (RefSearchNotRooted.IsChecked == true)
                    _dataSource = InstanceReferences.ReferenceType.Unrooted;

                if (RefSearchFinalizer.IsChecked == true)
                    _dataSource |= InstanceReferences.ReferenceType.Finalizer;
            }


            if (_dataSource == InstanceReferences.ReferenceType.None)
                _dataSource = InstanceReferences.ReferenceType.All;

			if (RefSearchFieldParents.IsChecked == true)
				_direction = InstanceReferences.ReferenceType.Ancestors;
			else if (RefSearchParentFields.IsChecked == true)
				_direction = InstanceReferences.ReferenceType.Descendants;
			else
				_direction = InstanceReferences.ReferenceType.Ancestors;

			DialogResult = true;
		}


        private void DispModeRadioButtonClicked(object sender, RoutedEventArgs e)
        {
            if (RefSearchDisplayGraph.IsChecked == true)
            {
                RefSearchLevel.Text = "3";
                _searchDepthLevel = 3;
            }
        }

        private void ConsideredClicked(object sender, RoutedEventArgs e)
        {
            CheckBox chkBox = sender as CheckBox;
            bool ischecked = (bool)chkBox.IsChecked;
            if (chkBox != null)
            {
                switch (chkBox.Name)
                {
                    case "RefSearchAll":
                        if (ischecked)
                        {
                            RefSearchRooted.IsChecked = false;
                            RefSearchNotRooted.IsChecked = false;
                            RefSearchFinalizer.IsChecked = false;
                        }
                        break;
                    case "RefSearchRooted":
                        if (ischecked)
                        {
                            RefSearchAll.IsChecked = false;
                            RefSearchNotRooted.IsChecked = false;
                        }
                        break;
                    case "RefSearchNotRooted":
                        if (ischecked)
                        {
                            RefSearchAll.IsChecked = false;
                            RefSearchRooted.IsChecked = false;
                        }
                        break;
                    case "RefSearchFinalizer":
                        if (ischecked)
                        {
                            RefSearchAll.IsChecked = false;
                        }
                        break;

                }
            }
        }
    }
}
