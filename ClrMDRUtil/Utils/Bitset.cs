using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class Bitset
	{
		private const int BitCount = sizeof (ulong)*8; // ??

		private ulong[] _bits;
		private int _size;
		public int Size => _size;
		private int _setCount;
		public int SetCount => _setCount;

		public Bitset(int size)
		{
			int cnt = size/BitCount + ((size%BitCount) > 0 ? 1 : 0);
			_bits = new ulong[cnt];
			_size = size;
			_setCount = 0;
		}

		public Bitset(int size, int setCount, ulong[] bits)
		{
			_bits = bits;
			_size = size;
			_setCount = setCount;
		}

		public int[] GetUnsetIndices()
		{
			if (_setCount < 1) return Utils.EmptyArray<int>.Value;
			int[] ary = new int[_size-_setCount];
			int ndx = 0;
			for (int i = 0, icnt = _size; i < icnt; ++i)
			{
				if (IsSet(i)) continue;
				ary[ndx] = i;
			}
			return ary;
		}

		public void Set(int i)
		{
			if (!IsSet(i)) ++_setCount;
			_bits[i/BitCount] |= (1UL << (i%BitCount));
		}

		public void Reset(int i)
		{
			if (IsSet(i)) --_setCount;
			_bits[i / BitCount] &= ~(1UL << (i % BitCount));
		}

		public bool IsSet(int i)
		{
			return (_bits[i / BitCount] & (1UL << (i % BitCount))) != 0;
		}

		public Bitset Clone()
		{
			ulong[] newbits = new ulong[_bits.Length];
			Buffer.BlockCopy(_bits,0,newbits,0,_bits.Length*sizeof(ulong));
			return new Bitset(_size,_setCount, newbits);
		}
	}
}
