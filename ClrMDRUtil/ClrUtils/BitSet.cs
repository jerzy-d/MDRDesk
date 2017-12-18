using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
    public class BitSet
    {
        ulong[] _bits;
        int _size; // count of used bits
        int _count; // count of on bits
        public int Count => _count;

        public BitSet(int size)
        {
            _size = size;
            _bits = new ulong[GetArrayLength(size)];
            _count = 0;
        }

        public bool Get(int ndx)
        {
            Debug.Assert(ndx >= 0 && ndx < _size);
            return (_bits[ndx / 64] & ((ulong)1 << (ndx % 64))) != 0;
        }

        public bool Set(int ndx)
        {
            Debug.Assert(ndx >= 0 && ndx < _size);
            if (Get(ndx)) return false;
            _bits[ndx / 64] |= ((ulong)1 << (ndx % 64));
            ++_count;
            return true;
        }

        public void Unset(int ndx)
        {
            if (Get(ndx))
            {
                _bits[ndx / 64] &= ~((ulong)1 << (ndx % 64));
                --_count;
            }
        }

        public int[] GetSetBitIndices()
        {
            int[] ary = new int[Count];
            int andx = 0;
            int ndx = 0;
            for (int i = 0, icnt = _bits.Length; i < icnt; ++i)
            {
                ulong dw = _bits[i];
                if (dw == 0)
                {
                    ndx += 64;
                    continue;
                }
                int tndx = ndx; // temp item index
                ulong mask = 0x0000000000000001;
                while (mask > 0 && dw > 0)
                {
                    if ((mask & dw) != 0)
                    {
                        ary[andx++] = tndx;
                        dw &= ~mask;
                    }
                    mask <<= 1;
                    ++tndx;
                }
                ndx += 64;
            }
            Debug.Assert(andx == Count);
            return ary;
        }

        private static int GetArrayLength(int n)
        {
            Debug.Assert(n > 0);
            return n > 0 ? (((n - 1) / 64) + 1) : 0;
        }
    }
}
