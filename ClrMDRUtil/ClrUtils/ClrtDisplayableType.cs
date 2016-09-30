using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class ClrtDisplayableType
	{
		private readonly int _typeId;
		public int TypeId => _typeId;
		private readonly int _fieldIndex;
		public int FieldIndex => _fieldIndex;
		private readonly string _typeName;
		private readonly string _fieldName;
		public string FieldName => _fieldName;
		private KeyValuePair<ValueExtractor.TypeCategory, ValueExtractor.TypeCategory> _category;
		private ClrtDisplayableType[] _fields;
		public ClrtDisplayableType[] Fields => _fields;

		public ClrtDisplayableType(int typeId, int fieldIndex, string typeName, string fieldName,
			KeyValuePair<ValueExtractor.TypeCategory, ValueExtractor.TypeCategory> category)
		{
			_typeId = typeId;
			_fieldIndex = fieldIndex;
			_typeName = typeName;
			_fieldName = fieldName;
			_category = category;
			_fields = Utils.EmptyArray<ClrtDisplayableType>.Value;
		}

		public void AddFields(ClrtDisplayableType[] fields)
		{
			if (fields!=null)
				Array.Sort(fields,new ClrtDisplayableTypeByFieldCmp());
			_fields = fields;

		}

		private string TypeHeader()
		{
			if (_category.Key == ValueExtractor.TypeCategory.Reference)
			{
				return _category.Value == ValueExtractor.TypeCategory.Interface ? Constants.InterfaceHeader : Constants.ClassHeader;
			}
			else if (_category.Key == ValueExtractor.TypeCategory.Struct)
			{
				return _category.Value == ValueExtractor.TypeCategory.Interface ? Constants.InterfaceHeader : Constants.StructHeader;
			}
			return Constants.PrimitiveHeader;
		}

		public override string ToString()
		{
			return string.IsNullOrEmpty(_fieldName)
				? TypeHeader() + _typeName
				: _fieldName + TypeHeader() + _typeName;
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
}
