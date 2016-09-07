using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class Arena<T>
	{
		private T[][] _buffer;
		private readonly int _bufSize;
		private int _curBuffer;
		private int _curIndex;
		private int _count;

		public int Count => _count;

		public Arena(int bufCnt, int bufSize)
		{
			_bufSize = bufSize;
			_buffer = new T[bufCnt][];
			_curBuffer = 0;
			_curIndex = 0;
			_count = 0;
			_buffer[_curBuffer] = new T[_bufSize];
		}

		public void Add(T item)
		{
			_buffer[_curBuffer][_curIndex] = item;
			if (++_curIndex == _bufSize)
			{
				if (++_curBuffer == _buffer.Length)
				{
					throw new OverflowException("ClrMDRIndex.Arena.Add: Insufficient buffer count." );
				}
				_buffer[_curBuffer] = new T[_bufSize];
				_curIndex = 0;
			}
			++_count;
		}

		public T Get(int index)
		{
			var bufNdx = index/_bufSize;
			var ndx = index%_bufSize;
			return _buffer[bufNdx][ndx];
		}

		public void Clear()
		{
			for (int i = 0, icnt = _buffer.Length; i < icnt; ++i)
			{
				_buffer[i] = null;
			}
			_buffer = null;
			_curBuffer = _curIndex = _count = 0;
		}
	}
}
