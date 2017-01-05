using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClrMDRIndex;

namespace ClrMDRUtil.Utils
{
	public class StringIdDct
	{
		private readonly SortedDictionary<string, int> _dct;

		public StringIdDct()
		{
			_dct = new SortedDictionary<string, int>(StringComparer.Ordinal);
		}

		public void Clear()
		{
			_dct.Clear();
		}

		public int Count
		{
			get
			{
				return _dct.Count;
			}
		}

		public int GetId(string s, out bool newId)
		{
			s = s?.Trim() ?? Constants.NullName;
			newId = false;
			int id;
			if (_dct.TryGetValue(s, out id))
			{
				return id;
			}
			newId = true;
			id = _dct.Count;
			_dct.Add(s, id);
			return id;
		}

		public int JustGetId(string s)
		{
			s = s?.Trim() ?? Constants.NullName;
			int id;
			if (_dct.TryGetValue(s, out id))
			{
				return id;
			}
			id = _dct.Count;
			_dct.Add(s, id);
			return id;
		}

		public int AddKey(string key)
		{
			bool newId;
			return GetId(key, out newId);
		}

		public string[] GetNamesSortedById()
		{
			var sary = new string[_dct.Count];
			foreach (var entry in _dct)
			{
				sary[entry.Value] = entry.Key;
			}
			return sary;
		}

		public bool DumpInIdOrder(string path, out string error)
		{
			error = null;
			StreamWriter wr = null;
			try
			{
				wr = new StreamWriter(path);
				var ary = _dct.ToArray();
				Array.Sort(ary, (a, b) => a.Value < b.Value ? -1 : (a.Value > b.Value ? 1 : 0));
				var len = ary.Length;
				wr.WriteLine(len.ToString());
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
				error = ClrMDRIndex.Utils.GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				wr?.Close();
			}
		}
	}


}
