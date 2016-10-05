using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	public class TypeValue
	{
		private int _typeId;
		private string _typeName;
		private TypeCategories _category;

		private List<FieldValue> _fields;
		public List<FieldValue> Fields => _fields;

		public TypeValue(int typeId, string typeName, TypeCategories category)
		{
			_typeId = typeId;
			_typeName = typeName;
			_category = category;
			_fields = null;
		}

	}

	public class FieldValue
	{
		private int _typeId;
		private string _typeName;
		private int _fieldIndex;
		private string _fieldName;
		private ClrType _clrType;
		private ClrInstanceField _instField;
		private TypeCategories _category;
		private List<string> _values;
		private List<FieldValue> _fields;
		private FilterValue _filter;

		public int TypeId => _typeId;
		public int FieldIndex => _fieldIndex;
		public ClrType ClType => _clrType;
		public ClrInstanceField InstField => _instField;
		public List<FieldValue> Fields => _fields;
		public FilterValue Filter;

		public FieldValue(int typeId, string typeName, int fieldIndex, string fieldName, TypeCategories category)
		{
			_typeId = typeId;
			_typeName = typeName;
			_fieldIndex = fieldIndex;
			_category = category;
		}

		public bool AddField(FieldValue fld)
		{
			if (_fields.Contains(fld)) return false;
			_fields.Add(fld);
			return true;
		}

		public void AddFilter(FilterValue filter)
		{
			_filter = filter;
		}

		public bool HasFields()
		{
			return _fields != null && _fields.Count > 0;
		}

		public bool HasFilter()
		{
			return _filter != null;
		}

		public bool Accept(object val, bool accept)
		{
			return true;
		}
	}

	public class FieldValueEqualityCmp : IEqualityComparer<FieldValue>
	{
		public bool Equals(FieldValue b1, FieldValue b2)
		{
			if (b1.TypeId == b2.TypeId)
			{
				return b1.FieldIndex == b2.FieldIndex;
			}
			return true;
		}

		public int GetHashCode(FieldValue bx)
		{
			return bx.TypeId.GetHashCode() ^ bx.FieldIndex.GetHashCode();
		}
	}

	public class FilterValue
	{
		private string _valueStr;
		private object _valueObj;
		private bool _include;

		public string ValueStr => _valueStr;

		public FilterValue(string val, bool include)
		{
			_valueStr = val;
			_include = include;
		}

		public bool Accept(string value, bool accept)
		{
			if (Utils.SameStrings(value, _valueStr))
				return true;
			return false;
		}

	}

}
