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
			_fields = fields;
		}

		public override string ToString()
		{
			return string.IsNullOrEmpty(_fieldName)
				? _typeName
				: _fieldName + Constants.HeavyRightArrowPadded + _typeName;
		}
	}
}
