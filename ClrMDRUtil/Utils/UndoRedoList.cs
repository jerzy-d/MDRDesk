using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ClrMDRIndex
{
	public class UndoRedoList<T,U> where T : class, IGetKey<U>
	{
		private  readonly List<T> _list;
		private int _current;
		private readonly Dictionary<U, T> _cache;
		private readonly IEqualityComparer<U> _comparer;

		public UndoRedoList(IEqualityComparer<U> comparer)
		{
			_list = new List<T>();
			_cache = new Dictionary<U, T>(comparer);
			_comparer = comparer;
		}

		public void Add(T item)
		{
			var key = item.GetKey();
			T existing;
			if (!_cache.TryGetValue(key, out existing))
			{
				_cache.Add(key, item);
				existing = item;
			}
			if (_list.Count == 0)
			{
				_list.Add(existing);
				return;
			}
			++_current;
			if (_current < _list.Count)
				_list.Insert(_current,existing);
			else
				_list.Add(existing);
		}

		public T GetExisting(U key)
		{
			T existing;
			if (_cache.TryGetValue(key, out existing))
			{
				return existing;
			}
			return null;
		}

		public T Undo(out bool canUndo)
		{
			canUndo = _list.Count > 1 && _current > 0;
			if (!canUndo) return _list[_current];
			--_current;
			return _list[_current];
		}

		public T Redo(out bool canRedo)
		{
			canRedo = _list.Count > 1 && _current < _list.Count - 1;
			if (!canRedo) return _list[_current];
			++_current;
			return _list[_current];
		}
	}
}
