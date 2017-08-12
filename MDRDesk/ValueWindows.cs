using ClrMDRIndex;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

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
            WndTypeCount
        }

        private static LinkedList<IValueWindow>[] _unlockledWindows= new[] 
        {
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


        public static void ShowTreeContentWindow(string description, InstanceValue inst, Window owner)
        {
            lock(_lock)
            {
                var list = _unlockledWindows[(int)WndType.Tree];
                if (list.Count > 0)
                {
                    var node = list.First;
                    list.RemoveFirst();
                    list.AddLast(node);
                    ((ClassStructDisplay)node.Value).UpdateInstanceValue(inst, description);
                    return;
                }
                int id = Utils.GetNewID();
                var wnd = new ClassStructDisplay(id, inst.GetDescription(), inst) { Owner = owner };
                _wndDct.Add(id, wnd);
                if (!wnd.Locked)
                {
                    list.AddFirst(wnd);
                }
                wnd.Show();
            }
        }

        public static void ShowContentWindow(string description, InstanceValue inst, Window owner)
        {
            lock (_lock)
            {
                var list = _unlockledWindows[(int)WndType.Content];
                if (list.Count > 0)
                {
                    var node = list.First;
                    list.RemoveFirst();
                    list.AddLast(node);
                    ((ContentDisplay)node.Value).UpdateInstanceValue(inst, description);
                    return;
                }
                int id = Utils.GetNewID();
                var wnd = new ContentDisplay(id, inst.GetDescription(), inst) { Owner = owner };
                _wndDct.Add(id, wnd);
                if (!wnd.Locked)
                {
                    list.AddFirst(wnd);
                }
                wnd.Show();
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
