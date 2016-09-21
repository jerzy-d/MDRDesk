using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ClrMDRIndex
{
	public class StringIdDct
	{
		private const int MaxRefCnt = 1024*8;
		private readonly SortedDictionary<string, int> _dct;
		private readonly object _lock;

		public StringIdDct()
		{
			_dct = new SortedDictionary<string, int>(StringComparer.Ordinal);
			_lock = new object();
		}

		public void Clear()
		{
			lock (_lock)
			{
				_dct.Clear();
			}
		}

		public int Count
		{
			get
			{
				lock (_lock)
				{
					return _dct.Count;
				}
			}
		}

		public int GetId(string s, out bool newId)
		{
			s = s?.Trim() ?? Constants.NullName;
			lock (_lock)
			{
				newId = false;
				int id;
				if (_dct.TryGetValue(s, out id))
				{
					return id;
				}
				newId = true;
				id = _dct.Count;
				_dct.Add(s,id);
				return id;
			}
		}

		public int JustGetId(string s)
		{
			s = s?.Trim() ?? Constants.NullName;
			lock (_lock)
			{
				int id;
				if (_dct.TryGetValue(s, out id))
				{
					return id;
				}
				id = _dct.Count;
				_dct.Add(s, id);
				return id;
			}
		}

		public int AddKey(string key)
		{
			bool newId;
			return GetId(key, out newId);
		}

		public string[] GetNamesSortedById()
		{
			lock (_lock)
			{
				var sary = new string[_dct.Count];
				foreach (var entry in _dct)
				{
					sary[entry.Value] = entry.Key;
				}
				return sary;
			}
		}

		public bool DumpInIdOrder(string path, out string error)
		{
			error = null;
			StreamWriter wr = null;
			lock (_lock)
			{
				try
				{
					wr = new StreamWriter(path);
					var ary = _dct.ToArray();
					Array.Sort(ary,(a,b)=>a.Value < b.Value ? -1 : (a.Value>b.Value ? 1 : 0));
					var len = ary.Length;
					for (var i = 0; i < len; ++i)
					{
						wr.WriteLine(ary[i].Key);
					}
					wr.Close();
					wr = null;
					return true;
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					return false;
				}
				finally
				{
					wr?.Close();
				}
			}
		}

		/// <summary>
		/// Mostly for testing.
		/// </summary>
		/// <param name="cnt">Number of dictionaries to create.</param>
		/// <returns>Array of dictionary instances.</returns>
		public static StringIdDct[] GetArrayOfDictionaries(int cnt)
		{
			var dctAry = new StringIdDct[cnt];
			for (int i = 0; i < cnt; ++i)
			{
				dctAry[i] = new StringIdDct();
			}
			return dctAry;
		}
	}
}
