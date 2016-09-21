using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class TempArena<T>
	{
		private List<T[]> _arrays;
		private List<T> _vector;
		private int _capacity;

		public TempArena(int capacity, int hintCount)
		{
			_capacity = capacity;
			_arrays = new List<T[]>(hintCount);
			_vector = new List<T>(capacity);
		}

		public int Count()
		{
			return  _arrays.Count * _capacity + _vector.Count;
		}

		public T LastItem()
		{
			if (_vector.Count < 1) return default(T);
			return _vector[_vector.Count - 1];
		}

		public T GetItemAt(int ndx)
		{
			Debug.Assert(ndx >= 0);
			if (_arrays.Count == 0)
			{
				if (ndx < _vector.Count)
					return _vector[ndx];
				return default(T);
			}
			int curOff = _capacity;
			int prevOff = 0;
			for (int i = 0, icnt = _arrays.Count; i < icnt; ++i)
			{
				if (ndx < curOff)
					return _arrays[i][ndx - prevOff];
				prevOff = curOff;
				curOff += _capacity;
			}
			ndx -= prevOff;
			Debug.Assert(ndx >= 0);
			if (ndx < _vector.Count)
			{
				return _vector[ndx];
			}
			return default(T);
		}


		public void Add(T val)
		{
			if (_vector.Count == _capacity)
			{
				_arrays.Add(_vector.ToArray());
				_vector.Clear();
			}
			_vector.Add(val);
		}

		public T[] GetArray()
		{
			int len = _arrays.Count*_capacity + _vector.Count;
			T[] ary = new T[len];
			int off = 0;
			for (int i = 0, icnt = _arrays.Count; i < icnt;  ++i)
			{
				Array.Copy(_arrays[i], 0, ary, off, _capacity);
				off += _capacity;
			}
			for (int i=0, icnt = _vector.Count; i < icnt; ++i)
			{
				ary[off++] = _vector[i];
			}
			return ary;
		}

		public T[] GetArrayAndClear()
		{
			int len = _arrays.Count * _capacity + _vector.Count;
			T[] ary = new T[len];
			int off = 0;
			for (int i = 0, icnt = _arrays.Count; i < icnt; ++i)
			{
				Array.Copy(_arrays[i], 0, ary, off, _capacity);
				off += _capacity;
				_arrays[i] = null;
			}
			for (int i = 0, icnt = _vector.Count; i < icnt; ++i)
			{
				ary[off++] = _vector[i];
			}
			_vector = null;
			return ary;
		}

		public static void Clear(ref TempArena<T> arena)
	    {
	        arena.Clear();
	        arena = null;
	    }

		public void Clear()
		{
			_arrays?.Clear();
			_vector?.Clear();
		}
	}
}
