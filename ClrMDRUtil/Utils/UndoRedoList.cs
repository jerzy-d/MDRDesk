using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ClrMDRIndex
{
	public class UndoRedoList<T>
	{
		private  readonly List<T> _undoList;
		private readonly List<T> _redoList;
		private readonly IEqualityComparer<T> _comparer;

		public UndoRedoList(IEqualityComparer<T> cmp)
		{
			_comparer = cmp;
			_undoList = new List<T>();
			_redoList = new List<T>();
		}

		public void AddToUndo(T item)
		{
			AddToList(_undoList, item);
		}

		public void AddToList(List<T> lst, T item)
		{
			if (lst.Count > 0)
			{
				if (!_comparer.Equals(lst.Last(), item))
				{
					lst.Add(item);
				}
				return;
			}
			lst.Add(item);
		}

		public T Undo(out bool canUndo)
		{
			canUndo = false;
			if (_undoList.Count < 2) return default(T);
			int lstNdx = _undoList.Count - 1;
			T item = _undoList[lstNdx];
			AddToList(_redoList,item);
			_undoList.RemoveAt(lstNdx);
			--lstNdx;
			item = _undoList[lstNdx];
			_undoList.RemoveAt(lstNdx);
			canUndo = true;
			return item;
		}

		public T Redo(out bool canRedo)
		{
			canRedo = false;
			if (_redoList.Count < 1) return default(T);
			int lstNdx = _redoList.Count - 1;
			T item = _redoList[lstNdx];
			AddToList(_undoList,item);
			_redoList.RemoveAt(lstNdx);
			canRedo = true;
			return item;
		}

		public T Undo(T currentItem, out bool canUndo)
		{
			canUndo = false;
			if (_undoList.Count < 2) return default(T);
			int lstNdx = _undoList.Count - 1;
			T item = _undoList[lstNdx];
			Debug.Assert(_comparer.Equals(currentItem,item));
			AddToList(_redoList,currentItem);
			_undoList.RemoveAt(lstNdx);
			--lstNdx;
			item = _undoList[lstNdx];
			_undoList.RemoveAt(lstNdx);
			canUndo = true;
			return item;
		}

		public T Redo(T currentItem, out bool canRedo)
		{
			canRedo = false;
			if (_redoList.Count < 1) return default(T);
			AddToUndo(currentItem);
			int lstNdx = _redoList.Count - 1;
			T item = _redoList[lstNdx];
			_redoList.RemoveAt(lstNdx);
			canRedo = true;
			return item;
		}
	}
}
