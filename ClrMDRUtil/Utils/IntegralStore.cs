using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{

    public class UlongStore<T>
    {
         const int _MAX = 2147482000/8;
        private int _max;
        private ulong[][] _store;
        private int _count;

        public UlongStore()
        {
            _max = _MAX;
            _store = null;
            _count = 0;
        }

        public UlongStore(int count, int max = _MAX)
        {
            _max = max > _MAX ? _MAX : max;
            var rest = count % _max;
            var n = count / _max + (rest > 0 ? 1 : 0);
            _store = new ulong[n][];
            for (int i = 0, icnt = n - 1; i < icnt; ++i) _store[i] = new ulong[_max];
            if (rest > 0)
            {
                _store[n - 1] = new ulong[rest];
            }
            _count = count;
        }


        public int Count => _count;

        public void Sort()
        {
            var rest = _count % _max;
            var n = _count / _max + (rest > 0 ? 1 : 0);
            for (int i = 0, icnt = n; i < icnt; ++i)
            {
                Array.Sort(_store[i]);
            }

            ULongIntSterta bh = new ULongIntSterta(n);
            for (int i = 0, icnt = n; i < icnt; ++i)
            {
                bh.Push(new ValueTuple<ulong, int,int>(_store[i][0],i,0));
            }

            int targetAry = 0, targetNdx = 0;
            while(bh.Count > 0)
            {
                (ulong hval, int hn, int hi) = bh.Pop();
                var val = _store[targetAry][targetNdx];
                if (val == hval && hn == targetAry)
                {

                }
            }

        }


        public ulong this[int ndx]
        {
            get
            {
                if (ndx >= _count) return Constants.InvalidAddress;
                var sndx = ndx / _max;
                ndx = ndx % _max;
                return _store[sndx][ndx];
            }
            set
            {
                if (ndx >= _count)
                {
                    throw new IndexOutOfRangeException("AddressStore");
                }
                var sndx = ndx / _max;
                ndx = ndx % _max;
                _store[sndx][ndx] = value;
            }
        }



    }
}
