using System.Collections.Generic;
using System.Linq;

namespace ClrMDRIndex
{
	public class StringCache
	{
		private readonly Dictionary<string, string> _cache;

		public StringCache(IEqualityComparer<string> comparer, int capacity = 0)
		{
			if (capacity > 0)
				_cache = new Dictionary<string, string>(capacity,comparer);
			else
				_cache = new Dictionary<string, string>(comparer);
		}

		public StringCache(int capacity = 0)
		{
			if (capacity > 0)
				_cache = new Dictionary<string, string>(capacity);
			else
				_cache = new Dictionary<string, string>();
		}

		public string GetCachedString(string str)
		{
			string s;
			if (_cache.TryGetValue(str, out s))
			{
				return s;
			}
			_cache.Add(str,str);
			return str;
		}

		public string[] GetAllStrings()
		{
			return _cache.Keys.ToArray();
		}

		public int Count()
		{
			return _cache.Count;
		}
	}

	public class BlockingStringCache
	{
		private readonly Dictionary<string, string> _cache;
		private readonly object _lock;

		public BlockingStringCache(IEqualityComparer<string> comparer, int capacity = 0)
		{
			_lock = new object();
			if (capacity > 0)
				_cache = new Dictionary<string, string>(capacity, comparer);
			else
				_cache = new Dictionary<string, string>(comparer);
		}

		public BlockingStringCache(int capacity = 0)
		{
			_lock = new object();
			if (capacity > 0)
				_cache = new Dictionary<string, string>(capacity);
			else
				_cache = new Dictionary<string, string>();
		}

		public string GetCachedString(string str)
		{
			string s;
			lock (_lock)
			{
				if (_cache.TryGetValue(str, out s))
				{
					return s;
				}
				_cache.Add(str, str);
			}
			return str;
		}

		public string[] GetAllStrings()
		{
			string[] ary;
			lock (_lock)
			{
				ary = _cache.Keys.ToArray();
			}
			return ary;
		}

		public int Count()
		{
			int count;
			lock (_lock)
			{
				count = _cache.Count;
			}
			return count;
		}
	}
}
