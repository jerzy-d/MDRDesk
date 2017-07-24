using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
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
	/// Interaction logic for ContentDisplay.xaml
	/// </summary>
	public partial class ContentDisplay : Window
	{
		private int _id;
		private ConcurrentDictionary<int, Window> _wndDct;
		private bool _wordWrapped;
		private InstanceValue _instanceValue;

		public ContentDisplay(int id, ConcurrentDictionary<int, Window> wndDct, string description, InstanceValue instVal)
		{
			_id = id;
			_wndDct = wndDct;
			InitializeComponent();
			_wordWrapped = true;
			_instanceValue = instVal;
			ContentInfo.Text = description;
			ContentValue.Text = instVal.Value.FullContent;
			wndDct.TryAdd(id, this);
		}

		private void WordWrapButtonClicked(object sender, RoutedEventArgs e)
		{
			if (_wordWrapped)
			{
				ContentValue.TextWrapping = TextWrapping.NoWrap;
				_wordWrapped = false;
			}
			else
			{
				ContentValue.TextWrapping = TextWrapping.Wrap;
				_wordWrapped = true;
			}

		}
		public void Window_Closing(object sender, CancelEventArgs e)
		{
			Window wnd;
			_wndDct.TryRemove(_id, out wnd);
		}
	}
}
