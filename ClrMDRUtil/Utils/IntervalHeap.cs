using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class IntervalHeap<T>
	{
		private int _currentSize;
		private TwoElement<T>[] _heap;

		public int Capacity => _heap.Length;
		public int Size => _currentSize;
		public bool CanInsert => _currentSize < _heap.Length;

		private int LastNode => (_currentSize/2) + (_currentSize%2);
		private bool IsOdd => (_currentSize%2) > 0;

		public IntervalHeap(int capacity)
		{
			_heap = new TwoElement<T>[capacity/2 + (capacity%2) + 1];
			_currentSize = 0;
		}

		private bool Less(T a, T b)
		{
			return Comparer<T>.Default.Compare(a, b) < 0;
		}
		private bool LessOrEqual(T a, T b)
		{
			var cmp = Comparer<T>.Default.Compare(a, b);
			return cmp < 0 || cmp ==0;
		}
		private bool Greater(T a, T b)
		{
			return Comparer<T>.Default.Compare(a, b) > 0;
		}

		public bool Insert(T x)
		{
			if (!CanInsert) return false;
			if (_currentSize == 0)
			{
				_heap[1]._left = x;
				_heap[1]._right = x;
				++_currentSize;
				return true;
			}
			if (_currentSize == 1)
			{
				if (Less(x,_heap[1]._left)) _heap[1]._left = x;
				else _heap[1]._right = x;
				++_currentSize;
				return true;
			}
			int lastNode = LastNode;
			bool minHeap = false;
			if (IsOdd)
			{
				if (Less(x, _heap[lastNode]._left)) minHeap = true;
			}
			else
			{
				++lastNode;
				if (LessOrEqual(x, _heap[lastNode/2]._left)) minHeap = true;
			}
			if (minHeap)
			{
				int i = lastNode;
				while (i != 1 && Less(x, _heap[i/2]._left))
				{
					_heap[i]._left = _heap[i/2]._left;
					i /= 2;
				}
				_heap[i]._left = x;
				++_currentSize;
				if (IsOdd)
				{
					_heap[lastNode]._right = _heap[lastNode]._left;
				}
			}
			else
			{
				int i = lastNode;
				while (i != 1 && Greater(x,_heap[i/2]._right))
				{
					_heap[i]._right = _heap[i/2]._right;
					i /= 2;
				}
				_heap[i]._right = x;
				++_currentSize;
				if (IsOdd)
				{
					_heap[lastNode]._left = _heap[lastNode]._right;
				}
			}

			return true;
		}

		public string Dump()
		{
			StringBuilder sb = new StringBuilder(512);
			int cnt = (IsOdd ? _currentSize + 1 : _currentSize)/2 + 1;
			for (int i = 1; i < cnt; ++i)
			{
				sb.Append("(").Append(_heap[i]._left.ToString()).Append(", ").Append(_heap[i]._right).Append(") ");
			}
			return sb.ToString();
		}

		struct TwoElement<U>
		{
			public U _left ;
			public U _right;

			public TwoElement(U l, U r)
			{
				_left = l;
				_right = r;
			} 
		}
	}
}
