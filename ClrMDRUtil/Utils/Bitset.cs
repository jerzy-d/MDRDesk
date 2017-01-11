using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRUtil
{
	public class Bitset
	{
		private const int BitCount = sizeof (ulong)*8; // ??

		private ulong[] _bits;

		public Bitset(int size)
		{
			int cnt = size/BitCount + ((size%BitCount) > 0 ? 1 : 0);
			_bits = new ulong[cnt];
		}

		public void Set(int i)
		{
			_bits[i/BitCount] |= (1UL << (i%BitCount));
		}

		public void Reset(int i)
		{
			_bits[i / BitCount] &= ~(1UL << (i % BitCount));
		}

		public bool IsSet(int i)
		{
			return (_bits[i / BitCount] & (1UL << (i % BitCount))) != 0;
		}
	}
}
