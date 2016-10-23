using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	public class ClrtDisplayableType
	{
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

		private TypeCategories _category;
        public TypeCategories Category => _category;

        private ClrtDisplayableType[] _fields;
		public ClrtDisplayableType[] Fields => _fields;

		public ClrtDisplayableType(int typeId, int fieldIndex, string typeName, string fieldName, TypeCategories category)
		{
			_typeId = typeId;
			_fieldIndex = fieldIndex;
			_typeName = typeName;
			_fieldName = fieldName;
			_category = category;
			_fields = Utils.EmptyArray<ClrtDisplayableType>.Value;
			_valueFilter = null;
			_getValue = false;
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
			if (_category.First == TypeCategory.Reference)
			{
				return _category.Second == TypeCategory.Interface ? Constants.InterfaceHeader : Constants.ClassHeader;
			}
			else if (_category.First == TypeCategory.Struct)
			{
				return _category.Second == TypeCategory.Interface ? Constants.InterfaceHeader : Constants.StructHeader;
			}
			return Constants.PrimitiveHeader;
		}

		private string FilterStr(FilterValue filterValue)
		{
			if (filterValue == null || filterValue.ValueStr==null) return string.Empty;
			if (filterValue.ValueStr.Length > 54)
			{
				return " " + Constants.LeftCurlyBracket.ToString() + filterValue.ValueStr.Substring(0, 54) + "..." + Constants.RightCurlyBracket.ToString() + " ";
			}
			return " " + Constants.LeftCurlyBracket.ToString() + filterValue.ValueStr + Constants.RightCurlyBracket.ToString() + " ";
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
			switch (_category.First)
			{
				case TypeCategory.Reference:
					switch (_category.Second)
					{
						case TypeCategory.Interface:
							msg = Constants.InterfaceHeader + "Cannot get fields of interface.";
							return false;
						case TypeCategory.String:
							msg = Constants.InterfaceHeader + "Cannot get fields, this type is considered primitive.";
							return false;
					}
					break;
				case TypeCategory.Struct:
					switch (_category.Second)
					{
						case TypeCategory.Interface:
							msg = Constants.InterfaceHeader + "Cannot get fields of interface.";
							return false;
						case TypeCategory.TimeSpan:
						case TypeCategory.Guid:
						case TypeCategory.DateTime:
							msg = Constants.InterfaceHeader + "Cannot get fields, this type is considered primitive.";
							return false;
					}
					break;
				case TypeCategory.Primitive:
					msg = Constants.PrimitiveHeader + "Cannot get fields, this type is primitive.";
					return false;
				default:
					msg = "Cannot get fields, uknown type.";
					return false;
			}
			return true;
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
