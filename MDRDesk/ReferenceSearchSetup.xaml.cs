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
			this.DialogResult = true;
		}
	}
}
