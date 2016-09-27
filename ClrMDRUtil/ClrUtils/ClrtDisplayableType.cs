using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class ClrtDisplayableType
	{
		private int _typeId;
		private string _typeName;
		private string _fieldName;
		private KeyValuePair<ValueExtractor.TypeCategory, ValueExtractor.TypeCategory> _category;
		private ClrtDisplayableType[] _fields;

		public ClrtDisplayableType(int typeId, string typeName, string fieldName,
			KeyValuePair<ValueExtractor.TypeCategory, ValueExtractor.TypeCategory> category)
		{
			_typeId = typeId;
			_typeName = typeName;
			_fieldName = fieldName;
			_category = category;
			_fields = Utils.EmptyArray<ClrtDisplayableType>.Value;
		}



	}
}
