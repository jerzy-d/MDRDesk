using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
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
	/// Interaction logic for CollectionDisplay.xaml
	/// </summary>
	public partial class CollectionDisplay : Window
	{
		private int _id;
		private ConcurrentDictionary<int, Window> _wndDct;
		private bool _indicesShown;
		private InstanceValue _instanceValue;

		public CollectionDisplay(int id, ConcurrentDictionary<int, Window> wndDct, InstanceValue instVal, string descr)
		{
			_id = id;
			_wndDct = wndDct;
			InitializeComponent();
			Debug.Assert(instVal.ArrayValues != null);
			CollectionValues.ItemsSource = instVal.ArrayValues;
			CollectionInfo.Text = descr;
			wndDct.TryAdd(id, this);
		}

		private void ShowArrayIndicesClicked(object sender, RoutedEventArgs e)
		{
			if (_indicesShown)
			{
				CollectionValues.ItemsSource = _instanceValue.ArrayValues;
			}
			else
			{
				string[] values = _instanceValue.ArrayValues;
				Debug.Assert(values!=null);
				int cnt = values.Length;
				int digitCount = Utils.NumberOfDigits(cnt);
				string[] newValues = new string[cnt];
				string format = "[{0," + digitCount.ToString() + ":#,###,###}] ";
				for (int i = 0, icnt = values.Length; i < icnt; ++i)
				{
					newValues[i] = Utils.SizeStringHeader(i, digitCount, format) + values[i];
				}
				_indicesShown = true;
				CollectionValues.ItemsSource = newValues;
			}


		}

		public void Window_Closing(object sender, CancelEventArgs e)
		{
			Window wnd;
			_wndDct.TryRemove(_id, out wnd);
		}
	}
}
