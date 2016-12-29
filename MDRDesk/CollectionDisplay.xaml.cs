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
	/// Interaction logic for CollectionDisplay.xaml
	/// </summary>
	public partial class CollectionDisplay : Window
	{
		private bool _indicesShown;

		public CollectionDisplay(string[] values, string descr)
		{
			InitializeComponent();
			CollectionValues.ItemsSource = values;
			CollectionInfo.Text = descr;
		}

		private void ShowArrayIndicesClicked(object sender, RoutedEventArgs e)
		{
			if (_indicesShown)
			{
				string[] values = CollectionValues.ItemsSource as string[];
				Debug.Assert(values != null);
				int cnt = values.Length;
				string[] newValues = new string[cnt];
				for (int i = 0, icnt = values.Length; i < icnt; ++i)
				{
					var val = values[i];
					var pos = val.IndexOf("] ");
					newValues[i] = val.Substring(pos + "] ".Length);
				}
				_indicesShown = false;
				CollectionValues.ItemsSource = newValues;
			}
			else
			{
				string[] values = CollectionValues.ItemsSource as string[];
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
	}
}
