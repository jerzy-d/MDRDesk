using System;
using System.Collections.Concurrent;
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

namespace MDRDesk
{
    /// <summary>
    /// Interaction logic for HelpWindow.xaml
    /// </summary>
    public partial class HelpWindow : Window
    {
        private int _id;
        private ConcurrentDictionary<int, Window> _wndDct;
        private string _mdString;
        private string _path;

        public string MdString { get; private set; }

        public HelpWindow(int id, ConcurrentDictionary<int, Window> wndDct, string path)
        {
            _id = id;
            _wndDct = wndDct;
            _path = path;
            InitializeComponent();
            LoadHelpFile(_path);
        }

        private string LoadHelpFile(string path)
        {
            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    return reader.ReadToEnd();
                }
            }
            catch(Exception ex)
            {
                return ex.ToString();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Window wnd;
            _wndDct.TryRemove(_id, out wnd);
        }

    }
}
