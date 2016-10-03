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
        public string TypeName => _typeName;
        private readonly string _fieldName;
		public string FieldName => _fieldName;
	    private string _valueFilter;

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
            _valueFilter = null;
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
            string filterStr = string.Empty;
            if (_valueFilter != null)
            {
                filterStr = " " + Constants.LeftCurlyBracket.ToString() + _valueFilter + Constants.RightCurlyBracket.ToString() + " ";
            }

            return string.IsNullOrEmpty(_fieldName)
				? TypeHeader() + _typeName
				: _fieldName + filterStr + TypeHeader() + _typeName;
		}

	    public bool CanGetFields(out string msg)
	    {
	        msg = null;
	        switch (_category.Key)
	        {
                case ValueExtractor.TypeCategory.Reference:
	                switch (_category.Key)
	                {
                        case ValueExtractor.TypeCategory.Interface:
	                        msg = Constants.InterfaceHeader + "Cannot get fields of interface.";
	                        return false;
                        case ValueExtractor.TypeCategory.String:
                            msg = Constants.InterfaceHeader + "Cannot get fields, this type is considered primitive.";
                            return false;
                    }
                    break;
                case ValueExtractor.TypeCategory.Struct:
                    switch (_category.Key)
                    {
                        case ValueExtractor.TypeCategory.Interface:
                            msg = Constants.InterfaceHeader + "Cannot get fields of interface.";
                            return false;
                        case ValueExtractor.TypeCategory.TimeSpan:
                        case ValueExtractor.TypeCategory.Guid:
                        case ValueExtractor.TypeCategory.DateTime:
                            msg = Constants.InterfaceHeader + "Cannot get fields, this type is considered primitive.";
                            return false;
                    }
                    break;
                case ValueExtractor.TypeCategory.Primitive:
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
            return  bx.TypeName.GetHashCode() ^ bx.FieldName.GetHashCode();
        }
    }

}
