using System.Windows;
using ClrMDRIndex;

namespace MDRDesk
{
	/// <summary>
	/// Interaction logic for TypeValueFilterDlg.xaml
	/// </summary>
	public partial class TypeValueFilterDlg
	{
		// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
		private readonly ClrtDisplayableType _dispType;
		// ReSharper restore PrivateFieldCanBeConvertedToLocalVariable
		private string _value;
		public string Value => _value;

		public TypeValueFilterDlg(ClrtDisplayableType dispType)
		{
			InitializeComponent();
			LbTypeName.Content = dispType.ToString();
			_dispType = dispType;
			if (_dispType.HasFilter())
			{
				TbTypeValue.Text = _dispType.Filter.FilterString;
			}
		}

		private void DialogOkClicked(object sender, RoutedEventArgs e)
		{
			_value = TbTypeValue.Text.Trim();
			DialogResult = true;
		}
	}
}
