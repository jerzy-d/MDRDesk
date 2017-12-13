using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Diagnostics;
using ClrMDRIndex;

namespace MDRDesk
{
    /// <summary>
    /// Interaction logic for FolderSelector.xaml
    /// </summary>
    public partial class FolderSelector : Window
    {
        DriveInfo[] _drives;
        TreeViewItem _currentTreeViewItem;
        string _filter;
        string _rootFolder;
        string _selectedPath;
        public string SelectedPath => _selectedPath;

        public FolderSelector(string title = null, string rootFolder=null, string filter = null)
        {
            if (title != null) Title = title;
            _filter = string.IsNullOrWhiteSpace(filter) ? null : filter;
            _rootFolder = string.IsNullOrWhiteSpace(rootFolder) ? null : rootFolder;
            if (_rootFolder != null && !Directory.Exists(_rootFolder)) _rootFolder = null;
            InitializeComponent();
         }

        private void OnInitialized(object sender, EventArgs e)
        {
            _drives = DriveInfo.GetDrives();
            for (int i = 0, icnt = _drives.Length; i < icnt; ++i)
            {
                Folder folder = new Folder(_drives[i].RootDirectory, _drives[i]);
                TreeViewItem item = GetTypeValueSetupTreeViewItem(folder);
                item.Tag = folder;
                FolderTreeView.Items.Add(item);
            }
            ExpandRoot();
            FolderTreeView.UpdateLayout();
            if (_currentTreeViewItem != null)
            {
                _currentTreeViewItem.BringIntoView();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _drives = DriveInfo.GetDrives();
            for (int i = 0, icnt = _drives.Length; i < icnt; ++i)
            {
                Folder folder = new Folder(_drives[i].RootDirectory, _drives[i]);
                TreeViewItem item = GetTypeValueSetupTreeViewItem(folder);
                item.Tag = folder;
                FolderTreeView.Items.Add(item);
            }
            ExpandRoot();
            FolderTreeView.UpdateLayout();
            if (_currentTreeViewItem != null)
            {
                //ScrollViewer scroller = null;
                //DependencyObject border = VisualTreeHelper.GetChild(FolderTreeView, 0);
                //if (border != null)
                //{
                //    scroller = VisualTreeHelper.GetChild(border, 0) as ScrollViewer;
                //}
                //scroller.ScrollToBottom();
                _currentTreeViewItem.BringIntoView();
            }
        }

        private void ExpandRoot()
        {
            if (_rootFolder == null) return;
            char[] delimiters = new char[] { '\\', '/' };
            string[] dirs = _rootFolder.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
            dirs[0] = dirs[0] + System.IO.Path.DirectorySeparatorChar; // this should be disk drive
            TreeViewItem selitem = null;
            foreach (var item in FolderTreeView.Items)
            {
                TreeViewItem titem = item as TreeViewItem;
                Folder folder = (Folder)titem.Tag;
                if (string.Compare(folder.FullPath, dirs[0], StringComparison.OrdinalIgnoreCase) == 0)
                {
                    selitem = titem;
                    break;
                }
            }
            if (selitem == null) return;
            string curPath = dirs[0].Substring(0,dirs[0].Length-1); // remove path delimeter from drive
            ExpandTreeViewItem(selitem);
            for (int i = 1, icnt = dirs.Length; i < icnt; ++i)
            {
                curPath = curPath + System.IO.Path.DirectorySeparatorChar + dirs[i];
                bool foundNext = false;
                foreach (var item in _currentTreeViewItem.Items)
                {
                    TreeViewItem titem = item as TreeViewItem;
                    Folder folder = (Folder)titem.Tag;
                    if (string.Compare(folder.FullPath, curPath, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        ExpandTreeViewItem(titem);
                        foundNext = true;
                        break;
                    }
                }
                if (!foundNext) break;
            }
        }

        private void FolderTreeViewSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var item = e.NewValue as TreeViewItem;
            if (item != null)
            {
                _currentTreeViewItem = item;
                Debug.Assert(_currentTreeViewItem.Tag is Folder);
                Folder folder = (Folder)_currentTreeViewItem.Tag;
                FolderTxtBlock.Text = folder.FullPath;
                if (item.Items == null || item.Items.Count < 1)
                {
                    item.ItemsSource = GetSubFolders(folder);
                    item.ExpandSubtree();
                }
                _selectedPath = folder.FullPath;
                if (MatchesFilter(_selectedPath))
                    btnDialogOk.IsEnabled = true;
                else
                    btnDialogOk.IsEnabled = false;
            }
        }

        void ExpandTreeViewItem(TreeViewItem item)
        {
            Debug.Assert(item != null);
            _currentTreeViewItem = item;
            Debug.Assert(_currentTreeViewItem.Tag is Folder);
            Folder folder = (Folder)_currentTreeViewItem.Tag;
            FolderTxtBlock.Text = folder.FullPath;
            if (item.Items == null || item.Items.Count < 1)
            {
                item.ItemsSource = GetSubFolders(folder);
                item.ExpandSubtree();
            }
            _selectedPath = folder.FullPath;
            if (MatchesFilter(_selectedPath))
                btnDialogOk.IsEnabled = true;
            else
                btnDialogOk.IsEnabled = false;
        }

        private TreeViewItem[] GetSubFolders(Folder folder)
        {
            if (!folder.IsReady) return Utils.EmptyArray<TreeViewItem>.Value;
            try
            {
                DirectoryInfo di = folder.DirectoryInfo;
                var dirs = di.GetDirectories();
                TreeViewItem[] items = new TreeViewItem[dirs.Length];
                for (int i = 0, icnt = dirs.Length; i < icnt; ++i)
                {
                    Folder fldr = new Folder(dirs[i], null);
                    TreeViewItem item = GetTypeValueSetupTreeViewItem(fldr);
                    item.Tag = fldr;
                    items[i] = item;
                }
                return items;
            }
            catch (UnauthorizedAccessException uex)
            {
                GuiUtils.ShowInformation("Unauthorized Access", "Can't view content of this folder.", folder.FullPath, null, this);
                return Utils.EmptyArray<TreeViewItem>.Value;
            }
            catch (Exception ex)
            {
                GuiUtils.ShowError(Utils.GetExceptionErrorString(ex),this);
                return Utils.EmptyArray<TreeViewItem>.Value;
            }
        }

        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }

        private TreeViewItem GetTypeValueSetupTreeViewItem(Folder folder)
        {
            var txtBlk = GetClrtDisplayableTypeStackPanel(folder);
            var node = new TreeViewItem
            {
                Header = txtBlk,
                Tag = folder,
            };
            txtBlk.Tag = node;
            return node;
        }
        private StackPanel GetClrtDisplayableTypeStackPanel(Folder folder)
        {
            var stackPanel = new StackPanel() { Orientation = Orientation.Horizontal };

            Image image = new Image();
            if (folder.IsDrive)
            {
                DriveType driveType = folder.DriveType;
                switch(driveType)
                {
                    case DriveType.Fixed:
                        image.Source = ((Image)Application.Current.FindResource("HardDiskPng")).Source;
                        break;
                    default:
                        image.Source = ((Image)Application.Current.FindResource("HardDiskPng")).Source;
                        break;
                }
            }
            else
            {
                image.Source = ((Image)Application.Current.FindResource("FolderPng")).Source;
            }

            stackPanel.Children.Add(image);
            stackPanel.Children.Add(GetClrtDisplayableTypeTextBlock(folder));
            return stackPanel;
        }

        private TextBlock GetClrtDisplayableTypeTextBlock(Folder folder)
        {
            var txtBlk = new TextBlock();
            txtBlk.Inlines.Add("   ");
            if (folder.IsDrive)
            {
                (string name, string dtype, string space, string free) = folder.FolderDescription();
                txtBlk.Inlines.Add(new Run(name + " "));
                txtBlk.Inlines.Add(new Bold(new Run(dtype + " ")) { Foreground = Brushes.DarkGreen });
                txtBlk.Inlines.Add(new Italic(new Run(space + "/" + free) { Foreground = Brushes.Black }));
            }
            else
            {
                if (MatchesFilterExactly(folder.FullPath))
                    txtBlk.Inlines.Add(new Run(folder.ToString()) { FontWeight=FontWeights.Bold, FontStyle=FontStyles.Italic});
                else
                    txtBlk.Inlines.Add(new Run(folder.ToString()));
            }
            return txtBlk;
        }

        bool MatchesFilter(string path)
        {
            if (_filter == null) return true;
            if (path.EndsWith(_filter, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        bool MatchesFilterExactly(string path)
        {
            if (_filter == null) return false;
            return path.EndsWith(_filter, StringComparison.OrdinalIgnoreCase);
        }

        private void DialogOkClicked(object sender, RoutedEventArgs e)
        {
           this.DialogResult = true;
        }

    }

    internal struct Folder
    {
        DirectoryInfo _folder;
        public DirectoryInfo DirectoryInfo => _folder;
        DriveInfo _drive;
        public bool IsDrive => _drive != null;
        public DriveType DriveType => (_drive == null) ? System.IO.DriveType.Unknown : _drive.DriveType;
        public bool IsReady => (_drive == null) ? true : _drive.IsReady;
        public string FullPath => _folder.FullName;

        public Folder(DirectoryInfo folder, DriveInfo drive=null)
        {
            _folder = folder;
            _drive = drive;
        }
 
        public override string ToString()
        {
            return _folder.Name;
        }


        public ValueTuple<string,string,string,string> FolderDescription()
        {
            string dtype = _drive.DriveType.ToString();
            string name = _drive.Name;
            string space = _drive.IsReady ? Utils.JustSizeString((ulong)_drive.TotalSize) : "?";
            string free = _drive.IsReady ? Utils.JustSizeString((ulong)_drive.AvailableFreeSpace) : "?";
            return (name, dtype, space, free);
        }
    }

}
