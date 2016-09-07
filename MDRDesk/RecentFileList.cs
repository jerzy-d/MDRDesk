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
	    private List<FileInfo> _list;
	    private object _lock;
        public RecentFileList(MenuItem menuItem, int maxItems = 7)
		{
		    _maxItems = maxItems;
		    _menuItem = menuItem;
            _list = new List<FileInfo>(_maxItems);
            _lock = new object();
		}

	    public void Add(string path)
	    {
	        lock (_lock)
	        {
                AddImpl(path);
	            _menuItem.ItemsSource = _list;
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
                _menuItem.ItemsSource = _list;
            }
        }

	    private void AddImpl(string path)
	    {
            if (CanAdd(path)) return;
            if (_list.Count == _maxItems)
            {
                _list.RemoveAt(_list.Count - 1);
            }
            _list.Insert(0, new FileInfo(path));
        }

	    private bool CanAdd(string path)
	    {
	        if (!(Directory.Exists(path) || File.Exists(path))) return false;
	        for (int i = 0, icnt = _list.Count; i < icnt; ++i)
	        {
	            if (string.Compare(_list[i].FilePath, path, StringComparison.OrdinalIgnoreCase) == 0) return true;
	        }
	        return false;
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
