using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace MDRDesk
{
	public class RecentFileList
	{
	    private int _maxItems;
	    private MenuItem _menuItem;
//	    private List<FileInfo> _list;
	    private object _lock;
        public RecentFileList(MenuItem menuItem, int maxItems = 7)
		{
		    _maxItems = maxItems;
		    _menuItem = menuItem;
            //_list = new List<FileInfo>(_maxItems);
            _lock = new object();
		}

	    public string[] GetPaths()
	    {
	        lock (_lock)
	        {
				string[] lst = new string[_menuItem.Items.Count];
		        int ndx = 0;
				foreach (var item in _menuItem.Items)
		        {
			        var menuItem = item as MenuItem;
			        FileInfo fileInfo = menuItem.Header as FileInfo;
			        lst[ndx++] = fileInfo.FilePath;
		        }
                //string[] lst = new string[_list.Count];
                //for (int i = 0, icnt = lst.Length; i < icnt; ++i)
                //{
                //    lst[i] = _list[i].FilePath;
                //}
	            return lst;
	        }
        }

	    public void Add(string path)
	    {
	        lock (_lock)
	        {
                AddImpl(path);

	   //         _menuItem.ItemsSource = _list;
				//foreach (var item in _menuItem.Items)
				//{
				//	var menuItem = item as MenuItem;
				//	menuItem.Click += ((MainWindow)Application.Current.MainWindow).RecentIndicesClicked;
				//}
			}
	    }

	    public void Add(IList<string> lst)
	    {
	        if (lst == null || lst.Count < 1) return;
            lock (_lock)
            {
                for (int i =0, icnt = lst.Count; i < icnt; ++i)
                {
                    AddImpl(lst[i]);
					
                }
            }
        }

	    private void AddImpl(string path)
	    {
            if (!CanAdd(path)) return;
		    int count = _menuItem.Items.Count;
			if (count == _maxItems)
            {
	            var menuItem = _menuItem.Items[count - 1] as MenuItem;
	            menuItem.Click -= ((MainWindow) Application.Current.MainWindow).RecentIndicesClicked;
				_menuItem.Items.RemoveAt(count - 1);
            }
			var menu = new MenuItem();
		    var fInfo = new FileInfo(path);
		    menu.Header = fInfo;
			menu.Click += ((MainWindow)Application.Current.MainWindow).RecentIndicesClicked;
			_menuItem.Items.Insert(0,menu);
			//_list.Insert(0, new FileInfo(path));
        }

	    private bool CanAdd(string path)
	    {
	        if (!(Directory.Exists(path) || File.Exists(path))) return false;
			foreach (var item in _menuItem.Items)
			{
				var menuItem = item as MenuItem;
				FileInfo fileInfo = menuItem.Header as FileInfo;
				if (string.Compare(fileInfo.FilePath, path, StringComparison.OrdinalIgnoreCase) == 0) return false;
			}
			return true;

			//for (int i = 0, icnt = _list.Count; i < icnt; ++i)
	  //      {
	  //          if (string.Compare(_list[i].FilePath, path, StringComparison.OrdinalIgnoreCase) == 0) return false;
	  //      }
	        return true;
	    }
	}

    public class FileInfo
	{
		string _fileName;
		string _filePath;

		public string FileName => _fileName;
		public string FilePath => _filePath;

		public FileInfo(string filePath)
		{
			_fileName = Path.GetFileName(filePath);
			_filePath = filePath;
		}

		public FileInfo(string file, string filePath)
		{
			_fileName = file;
			_filePath = filePath;
		}

		public override string ToString()
		{
			return _fileName;
		}
	}

}
