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
using Markdown.Xaml;

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
        Markdown.Xaml.Markdown _markdown;

        public string MdString { get; private set; }

        public HelpWindow(int id, ConcurrentDictionary<int, Window> wndDct, string path)
        {
            _id = id;
            _wndDct = wndDct;
            _path = path;
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(NavigationCommands.GoToPage, Navigate));
            _mdString = LoadHelpFile(_path);
            InitMarkdown();
            DocumentViewer.Document = _markdown.Transform(_mdString);


        }

        private void InitMarkdown()
        {
            _markdown = new Markdown.Xaml.Markdown();
            var docStyle = this.TryFindResource("DocumentStyle") as Style;
            _markdown.DocumentStyle = docStyle;

            var h1Style = this.TryFindResource("H1Style") as Style;
            _markdown.Heading1Style = h1Style;
            var h2Style = this.TryFindResource("H2Style") as Style;
            _markdown.Heading2Style = h2Style;
            var h3Style = this.TryFindResource("H3Style") as Style;
            _markdown.Heading3Style = h3Style;
            var h4Style = this.TryFindResource("H4Style") as Style;
            _markdown.Heading4Style = h4Style;
            var linkStyle = this.TryFindResource("LinkStyle") as Style;
            _markdown.LinkStyle = linkStyle;

            var imageStyle = this.TryFindResource("ImageStyle") as Style;
            _markdown.ImageStyle = imageStyle;
            var separatorStyle = this.TryFindResource("SeparatorStyle") as Style;
            _markdown.SeparatorStyle = separatorStyle;

            _markdown.AssetPathRoot = ClrMDRIndex.Setup.HelpFolder + System.IO.Path.DirectorySeparatorChar + "Documentation";

        }

        public void Navigate(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter == null || !(e.Parameter is string)) return;
            string param = e.Parameter as string;
            if (param[0] == '.') param = param.Substring(1);
            if (param[0] == '.') param = param.Substring(1);
            string path = ClrMDRIndex.Setup.HelpFolder + param;
            if (File.Exists(path))
            {
                string md = LoadHelpFile(path);
                DocumentViewer.Document = _markdown.Transform(md);
            }
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
