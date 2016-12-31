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
	/// Interaction logic for ClassStructDisplay.xaml
	/// </summary>
	public partial class ClassStructDisplay : Window
	{
		private InstanceValue _instanceValue;

		public ClassStructDisplay(string description, InstanceValue instValue)
		{
			InitializeComponent();
			_instanceValue = instValue;
			ClassStructInfo.Text = description;
		}

		private void EventSetter_OnHandler(object sender, RequestBringIntoViewEventArgs e)
		{
			e.Handled = true;
		}

		private void InstHierarchyFieldTreeview_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			throw new NotImplementedException();
		}
	}
}
