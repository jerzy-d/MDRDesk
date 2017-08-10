using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

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

        /// <summary>
        /// List of currently displayed windows.
        /// Used to close them when index is closing.
        /// </summary>
        private static ConcurrentDictionary<int, IValueWindow> _wndDct = new ConcurrentDictionary<int, IValueWindow>();

        private static object _lock = new object();


        public static void RemoveWindow(int id, WndType wndType)
        {
            lock (_lock)
            {
                IValueWindow wnd;
                _wndDct.TryRemove(id, out wnd);
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
