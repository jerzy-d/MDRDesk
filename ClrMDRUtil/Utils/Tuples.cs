using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{

	public interface ISmallTuple
	{
		int Count { get; }
	}

	// ReSharper disable InconsistentNaming

	public struct pair<T, U> : ISmallTuple
	{
		public T First;
		public U Second;

		public T PFirst => First;
		public U PSecond => Second;

		public int Count => 2;

		public pair(T t, U u)
		{
			First = t;
			Second = u;
		}
	}

    public struct str_ulong_pair
    {
        private string _str;
        private ulong _ulong;

        public string Str => _str;
        public ulong Ulong => _ulong;

        public str_ulong_pair(string s, ulong u)
        {
            _str = s;
            _ulong = u;
        }
    }

	public struct triple<T, U, V> : ISmallTuple
	{
		public T First;
		public U Second;
		public V Third;

		public T PFirst => First;
		public U PSecond => Second;
		public V PThird => Third;

		public int Count => 3;

		public triple(T t, U u, V v)
		{
			First = t;
			Second = u;
			Third = v;
		}
	}

	public struct quadruple<T, U, V, W> : ISmallTuple
	{
		public T First;
		public U Second;
		public V Third;
		public W Forth;

		public T PFirst => First;
		public U PSecond => Second;
		public V PThird => Third;
		public W PForth => Forth;

		public int Count => 4;

		public quadruple(T t, U u, V v, W w)
		{
			First = t;
			Second = u;
			Third = v;
			Forth = w;
		}
	}

	public struct quintuple<T, U, V, W, X> : ISmallTuple
	{
		public T First;
		public U Second;
		public V Third;
		public W Forth;
		public X Fifth;

		public T PFirst => First;
		public U PSecond => Second;
		public V PThird => Third;
		public W PForth => Forth;
		public X PFifth => Fifth;

		public int Count => 5;

		public quintuple(T t, U u, V v, W w, X x)
		{
			First = t;
			Second = u;
			Third = v;
			Forth = w;
			Fifth = x;
		}
	}

	public struct sextuple<T, U, V, W, X, Y> : ISmallTuple
	{
		public T First;
		public U Second;
		public V Third;
		public W Forth;
		public X Fifth;
		public Y Sixth;

		public T PFirst => First;
		public U PSecond => Second;
		public V PThird => Third;
		public W PForth => Forth;
		public X PFifth => Fifth;
		public Y PSixth => Sixth;

		public int Count => 6;

		public sextuple(T t, U u, V v, W w, X x, Y y)
		{
			First = t;
			Second = u;
			Third = v;
			Forth = w;
			Fifth = x;
			Sixth = y;
		}
	}

	public struct listing<T>
	{
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
		};

		private T[] _ary;
		private int _offset;
		private int _count;

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

	    public T Item(int i)
	    {
	        return _ary[_offset + i];
	    }

		public T First	=> _ary[_offset];
		public T Second => _ary[_offset + 1];
		public T Third	=> _ary[_offset + 2];
		public T Forth	=> _ary[_offset + 3];
		public T Fifth	=> _ary[_offset + 4];
		public T Sixth	=> _ary[_offset + 5];
		public T Seventh => _ary[_offset + 6];
		public T Eighth => _ary[_offset + 7];
		public T Ninth	=> _ary[_offset + 8];
		public T Tenth	=> _ary[_offset + 9];

		public int Count => _count;
		public int Offset => _offset;
		public int ItemsCount => _ary.Length/_count;

		public T GetItem(int ndx)
		{
			Debug.Assert(ndx >= 0 && ndx < _count);
			return _ary[_offset + ndx];
		}

		public T GetLastItem()
		{
			return _ary[_offset + (_count-1)];
		}
	}

	// ReSharper restore InconsistentNaming
}
