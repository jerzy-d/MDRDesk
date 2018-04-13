using System;
using System.Collections.Generic;
using System.IO;
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

namespace MDRDeskInstaller
{
    /// <summary>
    /// Interaction logic for FileUsage.xaml
    /// </summary>
    public partial class FileUsage : Window
    {
        private string _error;
        public string Error => _error;
        public bool Result;
        public MainWindow MainWindowInstance => (MainWindow)(((App)Application.Current).MainWindow);


        public FileUsage()
        {
            InitializeComponent();
            PopulateLockedFileList();
        }

        private bool PopulateLockedFileList()
        {
            LockedFileList.Items.Clear();
            if (MainWindowInstance._lockedFiles != null && MainWindowInstance._lockedFiles.Length > 1)
            {
                foreach (var item in MainWindowInstance._lockedFiles)
                {
                    LockedFileList.Items.Add(item);
                }
                return true;
            }
            return false;
        }

        private void CancelBtnClicked(object sender, RoutedEventArgs e)
        {
            Result = false;
            Close();
        }

        private void TryAgainBtnClicked(object sender, RoutedEventArgs e)
        {
            MainWindowInstance.CheckFiles(out _error);
            bool stillSome = PopulateLockedFileList();
            if (_error != null)
            {
                Result = false;
                Close();
            }
            if (!stillSome)
            {
                Result = true;
                Close();
            }
        }
    }
}
