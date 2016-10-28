using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	public class TypeIdDct
	{
		private const int MaxRefCnt = 1024 * 8;
		private readonly SortedDictionary<string, int> _dct;
		private uint[] _baseTypeAndElementIds;
		private uint[] _fieldCounts;
		private int[] _instanceCounts;
		private readonly object _lock;
		public int Count => _dct.Count;

		public TypeIdDct()
		{
			_dct = new SortedDictionary<string, int>(StringComparer.Ordinal);
			_lock = new object();
			_baseTypeAndElementIds = Utils.EmptyArray<uint>.Value;
			_fieldCounts = Utils.EmptyArray<uint>.Value;
			_instanceCounts = Utils.EmptyArray<int>.Value;
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
				_dct.Add(s, id);
				return id;
			}
		}

		public void JustAddKey(string s)
		{
			s = s?.Trim() ?? Constants.NullName;
			lock (_lock)
			{
				if (_dct.ContainsKey(s)) return;
				var id = _dct.Count;
				_dct.Add(s, id);
			}
		}

		public int AddKey(string key)
		{
			bool newId;
			return GetId(key, out newId);
		}


		public void AddRefs(int typeId, int baseId, ClrElementType elem, int staticFldCnt, int fldCnt)
		{
			Debug.Assert(typeId >= 0);
			lock (_lock)
			{
				if (_fieldCounts.Length <= typeId)
				{
					var newSize = _fieldCounts.Length == 0 ? MaxRefCnt : _fieldCounts.Length + _fieldCounts.Length / 2;
					ResizeRefs(newSize);
					Debug.Assert(_fieldCounts.Length > typeId);
				}
				_baseTypeAndElementIds[typeId] = ((uint)elem << 24) | (uint)baseId;
				_fieldCounts[typeId] = (uint) staticFldCnt << 16 | (uint) fldCnt;
			}
		}

		public void AddCount(int typeId)
		{
			lock (_lock)
			{
				Debug.Assert(_instanceCounts.Length > typeId);
				_instanceCounts[typeId] += 1;
			}
		}

		private void ResizeRefs(int size)
		{
			var newBaseTypeAndElementIds = new uint[size];
			var newFieldCounts = new uint[size];
			var newInstanceCounts = new int[size];
			for (int i = 0, icnt = _baseTypeAndElementIds.Length; i < icnt; ++i)
			{
				newBaseTypeAndElementIds[i] = _baseTypeAndElementIds[i];
				newFieldCounts[i] = _fieldCounts[i];
				newInstanceCounts[i] = _instanceCounts[i];
			}
			_baseTypeAndElementIds = newBaseTypeAndElementIds;
			_fieldCounts = newFieldCounts;
			_instanceCounts = newInstanceCounts;
		}

		public bool DumpBasesAndElementTypes(string path, out string error)
		{
			return Utils.WriteUintArray(path, _baseTypeAndElementIds, out error);
		}

		public bool DumpTypeFieldCounts(string path, out string error)
		{
			return Utils.WriteUintArray(path, _fieldCounts, out error);
		}

		public bool DumpTypeInstanceCounts(string path, out string error)
		{
			return Utils.WriteIntArray(path, _instanceCounts, out error);
		}

		public string[] GetKeys()
		{
			lock (_lock)
			{
				var sary = new string[_dct.Count];
				int ndx = 0;
				foreach (var entry in _dct)
				{
					sary[ndx++] = entry.Key;
				}
				return sary;
			}
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

	}
}