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
    /// Interaction logic for CrashDumpSearch.xaml
    /// </summary>
    public partial class CrashDumpSearch : Window
    {
        public CrashDumpSearch()
        {
            InitializeComponent();
        }

        private void OpenFolderClicked(object sender, RoutedEventArgs e)
        {
            string path = GuiUtils.GetFolderPath(Setup.DumpsFolder, ".map", this);
        }

        private void OpenFileClicked(object sender, RoutedEventArgs e)
        {

        }
    }
}
