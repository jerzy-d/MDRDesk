using System;
using System.Collections.Generic;
using System.Drawing.Printing;
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
    /// Interaction logic for TypeValueFilterDlg.xaml
    /// </summary>
    public partial class TypeValueFilterDlg : Window
    {
        private ClrtDisplayableType _dispType;
        private string _value;
        public string Value => _value;

        public TypeValueFilterDlg(ClrtDisplayableType dispType)
        {
            InitializeComponent();
            LbTypeName.Content = dispType.ToString();
            _dispType = dispType;
        }

        private void DialogOkClicked(object sender, RoutedEventArgs e)
        {
            _value = TbTypeValue.Text.Trim();
            this.DialogResult = true;
        }
    }
}
