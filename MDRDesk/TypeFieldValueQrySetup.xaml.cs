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

namespace MDRDesk
{
    /// <summary>
    /// Interaction logic for TypeFieldValueQrySetup.xaml
    /// </summary>
    public partial class TypeFieldValueQrySetup : Window
    {
        string _selectedTypeName = null;
        string _selectedFieldname = null;

        public TypeFieldValueQrySetup(string typeInfo, KeyValuePair<string, string>[] fields)
        {
            InitializeComponent();
            TypeInfo.Text = typeInfo;
            for (int i=0, icnt=fields.Length; i < icnt; ++i)
            {
                var txt = GuiUtils.GetTypeFieldTextBlock(fields[i].Key, fields[i].Value);
                TypeFields.Items.Add(txt);
            }
            TypeFields.SelectedIndex = 0;
        }

        private void DialogOkClicked(object sender, RoutedEventArgs e)
        {
            var sel = TypeFields.SelectedIndex;
            if (sel < 0) return;
            var item = TypeFields.Items[sel] as TextBlock;
            Debug.Assert(item != null);
            Drawing textBlockDrawing = VisualTreeHelper.GetDrawing(item);
            Debug.Assert(textBlockDrawing != null);
            var lst = new List<string>();
            GuiUtils.WalkDrawingForText(lst, textBlockDrawing);
            Debug.Assert(lst.Count == 2);
            _selectedFieldname = lst[0];
            _selectedTypeName = lst[1];
            this.DialogResult = true;
        }
    }
}
