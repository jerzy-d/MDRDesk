using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

namespace ClrMDRIndex
{
    [Serializable]
	public class ClrtDisplayableType
	{
		private ClrtDisplayableType _parent;
		private readonly int _typeId;
		private readonly int _fieldIndex; // index of a parent field
		private readonly string _typeName;
		private readonly string _fieldName;
		private FilterValue _valueFilter;
		private bool _getValue;
		private ClrtDisplayableType[] _alternatives;
		private long _id;
		private ulong[] _addresses;

		public int TypeId => _typeId;
		public int FieldIndex => _fieldIndex;
		public string TypeName => _typeName;
		public string FieldName => _fieldName;
		public FilterValue Filter => _valueFilter;
		public ClrtDisplayableType Parent => _parent;
		public ClrtDisplayableType[] Alternatives => _alternatives;
		public bool HasAlternatives => _alternatives != null;

		private ClrElementKind _kind;
        public ClrElementKind Kind => _kind;

        private ClrtDisplayableType[] _fields;
		public ClrtDisplayableType[] Fields => _fields;
		public long Id => _id;

		public ClrtDisplayableType(ClrtDisplayableType parent, int typeId, int fieldIndex, string typeName, string fieldName, ClrElementKind kind)
		{
			_id = GetId();
			_parent = parent;
			_typeId = typeId;
			_fieldIndex = fieldIndex;
			_typeName = typeName;
			_fieldName = fieldName;
			_kind = kind;
			_fields = Utils.EmptyArray<ClrtDisplayableType>.Value;
			_valueFilter = null;
			_getValue = false;
			_alternatives = null;
		}

		public void AddAlternative(ClrtDisplayableType dtype)
		{
			if (_alternatives == null)
			{
				_alternatives = new ClrtDisplayableType[] { dtype };
				return;
			}
			for(int i = 0, icnt = _alternatives.Length; i < icnt; ++i)
			{
				if (dtype.TypeId == _alternatives[i].TypeId) return;
			}
			var newAry = new ClrtDisplayableType[_alternatives.Length + 1];
			Array.Copy(_alternatives, newAry, _alternatives.Length);
			newAry[_alternatives.Length] = dtype;
			_alternatives = newAry;
		}

		public bool HasAlternative(string typeName)
		{
			if (_alternatives == null) return false;
			for (int i = 0, icnt = _alternatives.Length; i < icnt; ++i)
			{
				if (Utils.SameStrings(typeName,_alternatives[i].TypeName)) return true;
			}
			return false;
		}

        public bool HasAlternative(ClrtDisplayableType cdt)
        {
            if (_alternatives == null) return false;
            for (int i = 0, icnt = _alternatives.Length; i < icnt; ++i)
            {
                if (_alternatives[i].Id == cdt.Id) return true;
            }
            return false;
        }

        public string GetDescription()
		{
			return _typeName + Environment.NewLine
			       + (HasFields ? "Field Count: " + _fields.Length : string.Empty);
		}

		public bool HasFields => _fields != null && _fields.Length > 0;
		

		public void AddFields(ClrtDisplayableType[] fields)
		{
			if (fields != null)
				Array.Sort(fields, new ClrtDisplayableTypeByFieldCmp());
			_fields = fields;

		}

		public void SetGetValue(bool getVal)
		{
			_getValue = getVal;
		}

		public void SetParent(ClrtDisplayableType parent)
		{
			_parent = parent;
		}

		public void ResetId(long id)
		{
			_id = id;
		}

		public void ToggleGetValue()
		{
			_getValue = !_getValue;
		}

		public bool HasFilter => _valueFilter != null;
		public bool GetValue => _getValue;
        public bool IsMarked => HasFilter || GetValue;


		public void SetFilter(FilterValue filter)
		{
			_valueFilter = filter;
		}

		public void RemoveFilter()
		{
			_valueFilter = null;
		}

		private string TypeHeader()
		{
            var specKind = TypeExtractor.GetSpecialKind(_kind);
            if (specKind != ClrElementKind.Unknown)
            {
                switch (specKind)
                {
                    case ClrElementKind.Free:
                    case ClrElementKind.Guid:
                    case ClrElementKind.DateTime:
                    case ClrElementKind.TimeSpan:
                    case ClrElementKind.Decimal:
                        return Constants.PrimitiveHeader;
                    case ClrElementKind.Interface:
                        return Constants.InterfaceHeader;
                    case ClrElementKind.Enum:
                        return Constants.PrimitiveHeader;
                    case ClrElementKind.SystemVoid:
                        return Constants.StructHeader;
                    case ClrElementKind.SystemObject:
                    case ClrElementKind.System__Canon:
                    case ClrElementKind.Exception:
                    case ClrElementKind.Abstract:
                        return Constants.ClassHeader;
                }
                throw new ApplicationException("ClrtDisplayableType.TypeHeader() Not all cases are handled for (specKind != ClrElementKind.Unknown).");
            }
            else
            {
                switch (TypeExtractor.GetStandardKind(_kind))
                {
                    case ClrElementKind.String:
                        return Constants.PrimitiveHeader;
                    case ClrElementKind.SZArray:
                    case ClrElementKind.Array:
                    case ClrElementKind.Object:
                    case ClrElementKind.Class:
                        return Constants.ClassHeader;
                    case ClrElementKind.Struct:
                        return Constants.StructHeader;
                    case ClrElementKind.Unknown:
                        return Constants.PrimitiveHeader;
                    default:
                        return Constants.PrimitiveHeader;
                }
            }
        }

		private string FilterStr(FilterValue filterValue)
		{
			if (filterValue == null || filterValue.FilterString==null) return string.Empty;
			if (filterValue.FilterString.Length > 54)
			{
				return " " + Constants.LeftCurlyBracket.ToString() + filterValue.FilterString.Substring(0, 54) + "..." + Constants.RightCurlyBracket.ToString() + " ";
			}
			return " " + Constants.LeftCurlyBracket.ToString() + filterValue.FilterString + Constants.RightCurlyBracket.ToString() + " ";
		}

		public string SelectionStr()
		{
			if (_getValue && HasFilter)
				return Constants.HeavyCheckMark.ToString() + Constants.FilterHeader;
			if (_getValue)
				return Constants.HeavyCheckMarkHeader;
			if (HasFilter)
				return Constants.FilterHeader;
			return string.Empty;
		}

		public override string ToString()
		{
            if (string.IsNullOrEmpty(_fieldName)) return _typeName;
            var filter = FilterStr(_valueFilter);
            if (string.IsNullOrEmpty(filter)) filter = " ";
			return SelectionStr() + _fieldName + filter + _typeName;
		}

		public bool CanGetFields(out string msg)
		{
			msg = null;
            var specKind = TypeExtractor.GetSpecialKind(_kind);
            if (specKind != ClrElementKind.Unknown)
            {
                switch (specKind)
                {
                    case ClrElementKind.Free:
                    case ClrElementKind.Guid:
                    case ClrElementKind.DateTime:
                    case ClrElementKind.TimeSpan:
                    case ClrElementKind.Decimal:
                        msg = "Cannot get fields, this type is considered primitive.";
                        return false;
                    case ClrElementKind.Interface:
                        msg = "Cannot get fields of an interface.";
                        return false;
                    case ClrElementKind.Enum:
                        msg = "Cannot get fields, this type is primitive.";
                        return false;
                    case ClrElementKind.Exception:
                        return true;
                    case ClrElementKind.System__Canon:
                        msg = "Cannot get fields, this is System__Canon type.";
                        return false;
                    case ClrElementKind.SystemObject:
                        msg = "Cannot get fields, this is System.Object.";
                        return false;
                    case ClrElementKind.SystemVoid:
                        msg = "Cannot get fields, this is System.Void.";
                        return false;
                    case ClrElementKind.Abstract:
                        msg = "Cannot get fields, this is abstract class.";
                        return false;
                }
                throw new ApplicationException("ClrtDisplayableType.TypeHeader() Not all cases are handled for (specKind != ClrElementKind.Unknown).");
            }
            else
            {
                switch (TypeExtractor.GetStandardKind(_kind))
                {
                    case ClrElementKind.String:
                        msg = "Cannot get fields, this type is considered primitive.";
                        return false;
                    case ClrElementKind.SZArray:
                    case ClrElementKind.Array:
                    case ClrElementKind.Object:
                    case ClrElementKind.Class:
                        return true;
                    case ClrElementKind.Struct:
                        return true;
                    case ClrElementKind.Unknown:
                        msg = "Cannot get fields, the type is unknown.";
                        return false;
                    default:
                        msg = "Cannot get fields, this type is primitive.";
                        return false;
                }
            }
        }

        public static bool SerializeArray(string path, ClrtDisplayableType[] ary, out string error)
        {
            error = null;
            Stream stream = null;
            try
            {
                IFormatter formatter = new BinaryFormatter();
                stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                formatter.Serialize(stream, ary);
                return true;
            }
            catch(Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return false;
            }
            finally
            {
                stream?.Close();
            }

        }

        public static ClrtDisplayableType[] DeserializeArray(string path, out string error)
        {
            error = null;
            Stream stream = null;
            try
            {
                IFormatter formatter = new BinaryFormatter();
                stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                ClrtDisplayableType[] ary = (ClrtDisplayableType[])formatter.Deserialize(stream);
                return ary;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
            finally
            {
                stream?.Close();
            }

        }

		private static long _idSeed=0;
		private static long GetId()
		{
			return Interlocked.Increment(ref _idSeed);
		}

		public static ClrtDisplayableType ClrtDisplayableTypeAryFixup(ClrtDisplayableType[] ary)
		{
			Queue<ClrtDisplayableType> que = new Queue<ClrtDisplayableType>();
			que.Enqueue(ary[0]);
			long id = 0;
			while(que.Count > 0)
			{
				var dt = que.Dequeue();
				dt.ResetId(++id);
				if (dt.HasFields)
				{
					for (int i = 0, icnt = dt.Fields.Length; i < icnt; ++i)
					{
						var fld = dt.Fields[i];
						fld.SetParent(dt);
						que.Enqueue(fld);
						if (fld.HasAlternatives)
						{
							for (int j = 0, jcnt = fld.Alternatives.Length; j < jcnt; ++j)
							{
								var alt = fld.Alternatives[j];
								alt.SetParent(fld);
								que.Enqueue(alt);
							}
						}
					}
				}

			}

			return ary[0];
		}

	}



	public class ClrtDisplayableTypeByFieldCmp : IComparer<ClrtDisplayableType>
	{
		public int Compare(ClrtDisplayableType a, ClrtDisplayableType b)
		{
			var cmp = string.Compare(a.FieldName, b.FieldName, StringComparison.Ordinal);
			if (cmp == 0)
			{
				cmp = a.FieldIndex < b.FieldIndex ? -1 : (a.FieldIndex > b.FieldIndex ? 1 : 0);
			}
			return cmp;
		}
	}

	public class ClrtDisplayableTypeEqualityCmp : IEqualityComparer<ClrtDisplayableType>
	{
		public bool Equals(ClrtDisplayableType b1, ClrtDisplayableType b2)
		{
			var cmp = string.Compare(b1.TypeName, b2.TypeName, StringComparison.Ordinal);
			if (cmp == 0)
			{
				cmp = string.Compare(b1.FieldName, b2.FieldName, StringComparison.Ordinal);
			}
			return cmp == 0;
		}

		public int GetHashCode(ClrtDisplayableType bx)
		{
			return bx.TypeName.GetHashCode() ^ bx.FieldName.GetHashCode();
		}
	}

	public class ClrtDisplayableTypeIdComparer : IEqualityComparer<ClrtDisplayableType>
	{
		public bool Equals(ClrtDisplayableType b1, ClrtDisplayableType b2)
		{
			return b1.Id == b2.Id;
		}

		public int GetHashCode(ClrtDisplayableType bx)
		{
			return bx.Id.GetHashCode();
		}
	}
	public class ClrtDisplayableTypeIdComparison : IComparer<ClrtDisplayableType>
	{
		public int Compare(ClrtDisplayableType a, ClrtDisplayableType b)
		{
			long aId = a.Parent == null ? -1 : a.Parent.Id;
			long bId = b.Parent == null ? -1 : b.Parent.Id;

			var cmp = aId < bId ? -1 : (aId > bId ? 1 : 0);
			if (cmp == 0)
			{
				cmp = a.Id < b.Id ? -1 : (a.Id > b.Id ? 1 : 0);
			}
			return cmp;
		}
	}
}
