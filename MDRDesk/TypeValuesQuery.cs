using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using ClrMDRIndex;

namespace MDRDesk
{
    public class TypeValuesQuery
    {
        private TreeViewItem _currentTreeViewItem;
        public TreeViewItem CurrentTreeViewItem => _currentTreeViewItem;

        private List<ClrtDisplayableType> _selected;
        private SortedList<ClrtDisplayableType, object> _filters;
        private ClrtDisplayableTypeEqualityCmp _eqCmp = new ClrtDisplayableTypeEqualityCmp();

        public TypeValuesQuery()
        {
            _selected = new List<ClrtDisplayableType>();
            _filters = new SortedList<ClrtDisplayableType, object>();
        }

        public void SetCurrentTreeViewItem(TreeViewItem item)
        {
            _currentTreeViewItem = item;
        }

        public void AddFilter(ClrtDisplayableType dispItem, string filter)
        {
            
        }

        public void AddSelected(ClrtDisplayableType dispItem)
        {
            if (!_selected.Contains(dispItem))
                _selected.Add(dispItem);
        }
        public void RemoveSelected(ClrtDisplayableType dispItem)
        {
            if (_selected.Contains(dispItem))
                _selected.Remove(dispItem);
        }
    }
}
