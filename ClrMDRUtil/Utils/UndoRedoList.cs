using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class UndoRedoList<T>
	{
		List<T> _undoList;
		List<T> _redoList;

		public void AddToUndo(T item)
		{
			_undoList.Add(item);
		}

		public T Undo(T currentItem)
		{
			if (_undoList.Count < 1) return default(T);
			_redoList.Add(currentItem);
			int lstNdx = _undoList.Count - 1;
			T item = _undoList[lstNdx];
			_undoList.RemoveAt(lstNdx);
			return item;
		}

		public T Redo(T currentItem)
		{
			if (_undoList.Count < 1) return default(T);
			_redoList.Add(currentItem);
			int lstNdx = _undoList.Count - 1;
			T item = _undoList[lstNdx];
			_undoList.RemoveAt(lstNdx);
			return item;
		}
	}
}
