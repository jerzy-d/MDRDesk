using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{

    public class TypeValueQuery
    {
        private TypeValueQuery _parent;
        private int _parentIndex;
        private ClrType _type;
        private ClrElementKind _kind;
        private ClrInstanceField _field;
        private int _fldIndex;
        private FilterValue _filter;
        private List<string> _values;

        public TypeValueQuery Parent => _parent;
        public int ParentIndex => _parentIndex;
        public ClrType Type => _type;

        public TypeValueQuery(TypeValueQuery parent, int parentIndex)
        {
            _parent = parent;
            _parentIndex = parentIndex;
        }

        public void SetFields(ClrType type_, ClrElementKind kind, ClrInstanceField field, int fldIndex, FilterValue filter, bool getValues, int valuCntHint=0)
        {
            _type = type_;
            _kind = kind;
            _field = field;
            _fldIndex = fldIndex;
            _filter = filter;
            _values = getValues ? new List<string>(valuCntHint <= 0 ? 256 : valuCntHint) : null;
        }
    }

    //public class TypeValueQueryParentFieldCmp : IComparer<TypeValueQuery>
    //{
    //    public int Compare(TypeValueQuery q1, TypeValueQuery q2)
    //    {
    //        int cmp = q1.ParentIndex < q2.ParentIndex ? -1 : (q1.ParentIndex > q2.ParentIndex)
    //    }
    //}

    //public class TypeValue
    //{
    //	private int _typeId;
    //	private string _typeName;
    //	private TypeKind _kind;

    //	private List<FieldValue> _fields;
    //	public List<FieldValue> Fields => _fields;

    //	public TypeValue(int typeId, string typeName, TypeKind kind)
    //	{
    //		_typeId = typeId;
    //		_typeName = typeName;
    //		_kind = kind;
    //		_fields = null;
    //	}

    //}

    //public class FieldValue
    //{
    //	private int _typeId;
    //	private string _fieldName;
    //	private ClrType _clrType;
    //	private ClrInstanceField _instField;
    //	private TypeKind _kind;
    //	private List<string> _values;
    //	private List<FieldValue> _fields;
    //	private FilterValue _filter;

    //	public int TypeId => _typeId;
    //	public string FieldName => _fieldName;
    //	public ClrType ClType => _clrType;
    //	public ClrInstanceField InstField => _instField;
    //	public List<FieldValue> Fields => _fields;
    //	public FilterValue Filter;

    //	public FieldValue(int typeId, string fieldName, TypeKind kind)
    //	{
    //		_typeId = typeId;
    //		_fieldName = fieldName;
    //		_kind = kind;
    //	}

    //	public bool AddField(FieldValue fld)
    //	{
    //		if (_fields == null) _fields = new List<FieldValue>();
    //		if (_fields.Contains(fld)) return false;
    //		_fields.Add(fld);
    //		return true;
    //	}

    //	public void AddFilter(FilterValue filter)
    //	{
    //		_filter = filter;
    //	}

    //	public bool HasFields()
    //	{
    //		return _fields != null && _fields.Count > 0;
    //	}

    //	public bool HasFilter()
    //	{
    //		return _filter != null;
    //	}

    //	public bool Accept(object val, bool accept)
    //	{
    //		return true;
    //	}
    //}

    //public class FieldValueEqualityCmp : IEqualityComparer<FieldValue>
    //{
    //	public bool Equals(FieldValue b1, FieldValue b2)
    //	{
    //		if (b1.TypeId == b2.TypeId)
    //		{
    //			return Utils.SameStrings(b1.FieldName,b2.FieldName);
    //		}
    //		return true;
    //	}

    //	public int GetHashCode(FieldValue bx)
    //	{
    //		return bx.TypeId.GetHashCode() ^ bx.FieldName.GetHashCode();
    //	}
    //}

    [Serializable]
	public class FilterValue
	{
		[Flags]
		public enum Op
		{
			EXCLUDE = 1,
			EQ = 1 << 1,
			LT = 1 << 2,
			GT = 1 << 3,
			AND = 1 << 4,
            OR = 1 << 5,
            IGNORECASE = 1 << 6,
            REGEX = 1 << 7,
        }

        private Op _op;
		private object[] _values;
		private string _filterString;
		public string FilterString => _filterString;

		public FilterValue(string filterStr)
		{
			_filterString = filterStr;
		}

        public bool IsIgnoreCase()
        {
            return (_op & Op.IGNORECASE) != 0;
        }
        public bool IsRegex()
        {
            return (_op & Op.REGEX) != 0;
        }

        public FilterValue(Op op, object[] values)
		{
			_op = op;
			_values = values;
		}

		public bool Accept(object obj)
		{

			return false;
		}
	}
}
