using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
    public class DisjointSubsets
    {
        int[] _parents;
        int[] _ranks;

        public DisjointSubsets(int size)
        {
            _ranks = new int[size];
            _parents = iota(size);
        }

        public int Size => _ranks.Length;

        public int Find(int i)
        {
            if (_parents[i] == i) return i;
            int x = i;
            do
            {
                x = _parents[x];
            } while (_parents[x] != x);

            // path compression
            int r = x;
            x = i;
            while(_parents[x] != x)
            {
                int prev = x;
                x = _parents[x];
                _parents[prev] = r;
            }
            return r;
        }

        public int Merge(int a , int b)
        {
            Debug.Assert(a != b && a < _ranks.Length && b < _ranks.Length && Find(a) != Find(b));
            if (_ranks[a] > _ranks[b])
            {
                _parents[b] = a;
                return a;
            }
            _parents[a] = b;
            if (_ranks[a] == _ranks[b])
            {
                _ranks[b] = _ranks[b] + 1;
            }
            return b;
        }

        private int[] iota(int sz)
        {
            int[] ary = new int[sz];
            for (int i = 0; i < sz; ++i) ary[i] = i;
            return ary;
        }
    }
}
