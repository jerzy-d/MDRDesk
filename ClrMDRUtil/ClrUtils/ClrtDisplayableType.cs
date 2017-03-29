using System;
using System.Collections.Generic;

namespace ClrMDRIndex
{
	public class ClrtDisplayableType
	{
		private ClrtDisplayableType _parent;
		private readonly int _typeId;
		private readonly int _fieldIndex;
		private readonly string _typeName;
		private readonly string _fieldName;
		private FilterValue _valueFilter;
		private bool _getValue;

		public int TypeId => _typeId;
		public int FieldIndex => _fieldIndex;
		public string TypeName => _typeName;
		public string FieldName => _fieldName;
		public FilterValue Filter => _valueFilter;
		public ClrtDisplayableType Parent => _parent;

		private ClrElementKind _kind;
        public ClrElementKind Kind => _kind;

        private ClrtDisplayableType[] _fields;
		public ClrtDisplayableType[] Fields => _fields;

		public ClrtDisplayableType(ClrtDisplayableType parent, int typeId, int fieldIndex, string typeName, string fieldName, ClrElementKind kind)
		{
			_parent = parent;
			_typeId = typeId;
			_fieldIndex = fieldIndex;
			_typeName = typeName;
			_fieldName = fieldName;
			_kind = kind;
			_fields = Utils.EmptyArray<ClrtDisplayableType>.Value;
			_valueFilter = null;
			_getValue = false;
		}

		public string GetDescription()
		{
			return _typeName + Environment.NewLine
			       + (HasFields() ? "Field Count: " + _fields.Length : string.Empty);
		}

		public bool HasFields()
		{
			return _fields != null && _fields.Length > 0;
		}

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

		public void ToggleGetValue()
		{
			_getValue = !_getValue;
		}

		public bool HasFilter()
		{
			return _valueFilter != null;
		}
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
			if (_getValue && HasFilter())
				return Constants.HeavyCheckMark.ToString() + Constants.FilterHeader;
			if (_getValue)
				return Constants.HeavyCheckMarkHeader;
			if (HasFilter())
				return Constants.FilterHeader;
			return string.Empty;
		}

		public override string ToString()
		{
			return string.IsNullOrEmpty(_fieldName)
				? TypeHeader() + _typeName
				: SelectionStr() + _fieldName + FilterStr(_valueFilter) + TypeHeader() + _typeName;
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
                        msg = Constants.InterfaceHeader + "Cannot get fields, this type is considered primitive.";
                        return false;
                    case ClrElementKind.Interface:
                        msg = Constants.InterfaceHeader + "Cannot get fields of an interface.";
                        return false;
                    case ClrElementKind.Enum:
                        msg = Constants.PrimitiveHeader + "Cannot get fields, this type is primitive.";
                        return false;
                    case ClrElementKind.Exception:
                        return true;
                    case ClrElementKind.System__Canon:
                        msg = Constants.PrimitiveHeader + "Cannot get fields, this is System__Canon type.";
                        return false;
                    case ClrElementKind.SystemObject:
                        msg = Constants.PrimitiveHeader + "Cannot get fields, this is System.Object.";
                        return false;
                    case ClrElementKind.SystemVoid:
                        msg = Constants.PrimitiveHeader + "Cannot get fields, this is System.Void.";
                        return false;
                    case ClrElementKind.Abstract:
                        msg = Constants.PrimitiveHeader + "Cannot get fields, this is abstract class.";
                        return false;
                }
                throw new ApplicationException("ClrtDisplayableType.TypeHeader() Not all cases are handled for (specKind != ClrElementKind.Unknown).");
            }
            else
            {
                switch (TypeExtractor.GetStandardKind(_kind))
                {
                    case ClrElementKind.String:
                        msg = Constants.InterfaceHeader + "Cannot get fields, this type is considered primitive.";
                        return false;
                    case ClrElementKind.SZArray:
                    case ClrElementKind.Array:
                    case ClrElementKind.Object:
                    case ClrElementKind.Class:
                        return true;
                    case ClrElementKind.Struct:
                        return true;
                    case ClrElementKind.Unknown:
                        msg = Constants.InterfaceHeader + "Cannot get fields, the type is unknown.";
                        return false;
                    default:
                        msg = Constants.PrimitiveHeader + "Cannot get fields, this type is primitive.";
                        return false;
                }
            }
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

}
