using System.Diagnostics;

namespace ClrMDRIndex
{
    public struct listing<T>
    {
        public static int MaxListingCount => PropertyNames.Length;

        public static string[] PropertyNames = new[]
        {
            "First",
            "Second",
            "Third",
            "Forth",
            "Fifth",
            "Sixth",
            "Seventh",
            "Eighth",
            "Ninth",
            "Tenth",
            "Eleventh"
        };

        private T[] _ary;
        private int _offset;
        private int _count;

        public static listing<T> Empty = new listing<T>(null, Constants.InvalidIndex, Constants.InvalidIndex);

        public listing(T[] ary, int offset, int count)
        {
            _ary = ary;
            _offset = offset;
            _count = count;
        }

        public listing(listing<T> other)
        {
            _ary = other._ary;
            _offset = other._offset;
            _count = other._count;
        }

        public bool IsEmpty => _ary == null;

        public T Item(int i)
        {
            return _ary[_offset + i];
        }

        public T First => _ary[_offset];
        public T Second => _ary[_offset + 1];
        public T Third => _ary[_offset + 2];
        public T Forth => _ary[_offset + 3];
        public T Fifth => _ary[_offset + 4];
        public T Sixth => _ary[_offset + 5];
        public T Seventh => _ary[_offset + 6];
        public T Eighth => _ary[_offset + 7];
        public T Ninth => _ary[_offset + 8];
        public T Tenth => _ary[_offset + 9];
        public T Eleventh => _ary[_offset + 9];

        public int Count => _count;
        public int Offset => _offset;
        public int ItemsCount => _ary.Length / _count;

        public T GetItem(int ndx)
        {
            Debug.Assert(ndx >= 0 && ndx < _count);
            return _ary[_offset + ndx];
        }

        public T GetLastItem()
        {
            return _ary[_offset + (_count - 1)];
        }
    }
}
