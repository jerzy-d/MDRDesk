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
	/// Interaction logic for InputStringDlg.xaml
	/// </summary>
	public partial class InputStringDlg : Window
	{
		public InputStringDlg(string inputInfo, string defaultOutput = "")
		{
			InitializeComponent();
			lblQuestion.Content = inputInfo;
			txtAnswer.Text = defaultOutput;
		}

		private void DialogOkClicked(object sender, RoutedEventArgs e)
		{
			this.DialogResult = true;
		}

		private void Window_ContentRendered(object sender, EventArgs e)
		{
			txtAnswer.SelectAll();
			txtAnswer.Focus();
		}

		public string Answer
		{
			get { return txtAnswer.Text; }
		}
	}
}
