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
        FileInfo[] _files;

        public FileUsage(FileInfo[] files)
        {
            InitializeComponent();
            _files = files;
        }
    }
}
