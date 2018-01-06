using System.Collections.Generic;
using System.Windows;
using ClrMDRIndex;

namespace MDRDesk
{
    public static class ValueWindows
    {
        public enum WndType
        {
            Content,
            List,
            KeyValues,
            Tree,
            Help,
            WndTypeCount
        }

        private static LinkedList<IValueWindow>[] _unlockledWindows= new[] 
        {
            new LinkedList<IValueWindow>(),
            new LinkedList<IValueWindow>(),
            new LinkedList<IValueWindow>(),
            new LinkedList<IValueWindow>(),
            new LinkedList<IValueWindow>(),
        };

        public static readonly System.Windows.Controls.Image LockedImage = new System.Windows.Controls.Image()
            { Source = ((System.Windows.Controls.Image)Application.Current.FindResource("LockPng")).Source };
        public static readonly System.Windows.Controls.Image UnlockedImage = new System.Windows.Controls.Image()
            { Source = ((System.Windows.Controls.Image)Application.Current.FindResource("UnlockPng")).Source };

        /// <summary>
        /// List of currently displayed windows.
        /// Used to close them when index is closing.
        /// </summary>
        private static Dictionary<int, IValueWindow> _wndDct = new Dictionary<int, IValueWindow>();

        private static object _lock = new object();

        public static void ShowHelpWindow(string path)
        {
            lock(_lock)
            {
                var list = _unlockledWindows[(int)WndType.Help];
                if (list.Count > 0)
                {
                    var node = list.First;
                    list.RemoveFirst();
                    list.AddLast(node);
                    node.Value.UpdateInstanceValue(null, path);
                    return;
                }
                int id = Utils.GetNewID();
                var wnd = new HelpWindow(Utils.GetNewID(), path) { Owner = GuiUtils.MainWindowInstance };
                if (wnd == null)
                    throw new MdrDeskException("[ValueWindows.ShowHelpWindow] Creating help window failed.");
                _wndDct.Add(id, wnd);
                if (!wnd.Locked)
                {
                    list.AddFirst(wnd);
                }
                ((Window)wnd).Show();
            }
        }

        public static void ShowContentWindow(string description, InstanceValue inst, WndType wndType)
        {
            lock (_lock)
            {
                var list = _unlockledWindows[(int)wndType];
                if (list.Count > 0)
                {
                    var node = list.First;
                    list.RemoveFirst();
                    list.AddLast(node);
                    node.Value.UpdateInstanceValue(inst, description);
                    return;
                }
                int id = Utils.GetNewID();
                IValueWindow wnd = null;
                switch (wndType)
                {
                    case WndType.Content:
                        wnd = new ContentDisplay(id, description != null ? description : inst.GetDescription(), inst) { Owner = GuiUtils.MainWindowInstance };
                        break;
                    case WndType.Tree:
                        wnd = new ClassStructDisplay(id, inst.GetDescription(), inst) { Owner = GuiUtils.MainWindowInstance };
                        break;
                    case WndType.List:
                        wnd = new CollectionDisplay(id, inst.GetDescription(), inst) { Owner = GuiUtils.MainWindowInstance };
                        break;
                    case WndType.KeyValues:
                        wnd = new KeyValueCollectionDisplay(id, inst.GetDescription(), inst) { Owner = GuiUtils.MainWindowInstance };
                        break;
                }
                if (wnd == null)
                    throw new MdrDeskException("[ValueWindows.ShowContentWindow] Creating content window failed.");
                _wndDct.Add(id, wnd);
                if (!wnd.Locked)
                {
                    list.AddFirst(wnd);
                }
                ((Window)wnd).Show();
            }
        }

        public static void RemoveWindow(int id, WndType wndType)
        {
            lock (_lock)
            {
                _wndDct.Remove(id);
                var list = _unlockledWindows[(int)wndType];
                if (list.Count < 1) return;
                var node = list.First;
                while(node != null)
                {
                    if (node.Value.Id == id)
                    {
                        list.Remove(node);
                        break;
                    }
                    node = node.Next;
                }
            }
        }

        public static void RemoveFromUnlocked(int id, WndType wndType)
        {
            lock (_lock)
            {
                var list = _unlockledWindows[(int)wndType];
                if (list.Count < 1) return;
                var node = list.First;
                while (node != null)
                {
                    if (node.Value.Id == id)
                    {
                        list.Remove(node);
                        break;
                    }
                    node = node.Next;
                }
            }
        }

        private static void AddToUnlocked(int id, WndType wndType)
        {
            IValueWindow wnd;
            if (_wndDct.TryGetValue(id, out wnd))
            {
                var list = _unlockledWindows[(int)wndType];
                if (list.Count < 1)
                {
                    list.AddFirst(wnd);
                    return;
                }
                var node = list.First;
                while (node != null)
                {
                    if (node.Value.Id == id)
                    {
                        return;
                    }
                    node = node.Next;
                }
                list.AddFirst(wnd);
            }
        }

        public static void ChangeMyLock(int id, WndType wndType, bool lockme)
        {
            lock (_lock)
            {
                if (lockme)
                {
                    RemoveFromUnlocked(id, wndType);
                }
                else
                {
                    AddToUnlocked(id, wndType);
                }
            }
        }

        public static void CloseAll()
        {
            lock(_lock)
            {
                for(int i = 0, icnt = _unlockledWindows.Length; i < icnt; ++i)
                {
                    _unlockledWindows[i].Clear();
                }
                foreach (var kv in _wndDct)
                {
                    ((Window)kv.Value).Close();
                }
                _wndDct.Clear();
            }
        }
    }
}
