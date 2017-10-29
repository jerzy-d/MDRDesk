using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Markdown.Xaml;
using ClrMDRIndex;

namespace MDRDesk
{
    /// <summary>
    /// Interaction logic for HelpWindow.xaml
    /// </summary>
    public partial class HelpWindow : Window, IValueWindow
    {
        private int _id;
        private string _mdString;
        private string _path;
        Markdown.Xaml.Markdown _markdown;
        private ValueWindows.WndType _wndType;
        private bool _locked;
        public ValueWindows.WndType WndType => _wndType;
        public int Id => _id;
        public bool Locked => _locked;
        private Image _lockedImg, _unlockedImg;
        public string MdString { get; private set; }

        public HelpWindow(int id, string path, bool locked=false)
        {
            _wndType = ValueWindows.WndType.Help;
            _id = id;
            _path = path;
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(NavigationCommands.GoToPage, Navigate));
            InitMarkdown();
            SetLock(locked);
            UpdateInstanceValue(null, path);
        }

        private void SetLock(bool locked)
        {
            _locked = locked;
            _lockedImg = new Image();
            _lockedImg.Source = ValueWindows.LockedImage.Source;
            _unlockedImg = new Image();
            _unlockedImg.Source = ValueWindows.UnlockedImage.Source;
            LockBtn.Content = locked ? _lockedImg : _unlockedImg;
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
            var h5Style = this.TryFindResource("H5Style") as Style;
            _markdown.Heading5Style = h5Style;
            var linkStyle = this.TryFindResource("LinkStyle") as Style;
            _markdown.LinkStyle = linkStyle;

            var imageStyle = this.TryFindResource("ImageStyle") as Style;
            _markdown.ImageStyle = imageStyle;
            var separatorStyle = this.TryFindResource("SeparatorStyle") as Style;
            _markdown.SeparatorStyle = separatorStyle;

            _markdown.AssetPathRoot = ClrMDRIndex.Setup.HelpFolder + System.IO.Path.DirectorySeparatorChar + "Documentation" + System.IO.Path.DirectorySeparatorChar;

        }

        public void UpdateInstanceValue(InstanceValue instVal, string descr)
        {
            _path = descr;
            string title;
            _mdString = LoadHelpFile(_path, out title);
            DocumentViewer.Document = _markdown.Transform(_mdString);
            this.Title = title;
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
                Dispatcher.CurrentDispatcher.InvokeAsync(() => ValueWindows.ShowHelpWindow(path));
                //ValueWindows.ShowHelpWindow(path);
                //string title;
                //string md = LoadHelpFile(path, out title);
                //DocumentViewer.Document = _markdown.Transform(md);
                //this.Title = title;
            }
        }

        private string LoadHelpFile(string path, out string title)
        {
            title = string.Empty;
            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string text = reader.ReadToEnd();

                    int start = text.IndexOf("##") + "##".Length;
                    int end = text.IndexOf("\n", start);
                    title = end <= start ? "MDR Desk" : text.Substring(start, end - start).Trim();
                    return text;
                }
            }
            catch(Exception ex)
            {
                return ex.ToString();
            }
        }

        private void LockBtnClicked(object sender, RoutedEventArgs e)
        {
            if (_locked)
            {
                _locked = false;
                LockBtn.Content = _unlockedImg;
                ValueWindows.ChangeMyLock(_id, _wndType, false);
            }
            else
            {
                _locked = true;
                LockBtn.Content = _lockedImg;
                ValueWindows.ChangeMyLock(_id, _wndType, true);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var wndtask = Task.Factory.StartNew(() => ValueWindows.RemoveWindow(_id, _wndType));
        }

    }
}
