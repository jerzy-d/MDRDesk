using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;
using System.Text.RegularExpressions;

namespace ClrMDRIndex
{

    public class TypeValueQuery
    {
        private long _id;
        private TypeValueQuery _parent;
        private TypeValueQuery[] _children;
        private string _typeName;
        private string _fieldName;
        private ClrType _type;
        private ClrElementKind _kind;
        private ClrInstanceField _field;
        private int _fldIndex;
        private FilterValue _filter;
        private bool _getValue;
        private string[] _values;
        private int _valIndex;
        private int _valOffset;

        // alternative types
        private bool _alternative; // am I alternative
        private bool _alternativeChild; // do I have alternative children
        private ClrType _altParentType; // I am alternative and this is corresponding parent instance type
        private ClrElementKind _altParentKind; // and corresponding kind
        private ClrInstanceField _altParentField; // and corresponding field

        public long Id => _id;
        public TypeValueQuery Parent => _parent;
        public TypeValueQuery[] Children => _children;
        public ClrType Type => _type;
        public ClrInstanceField Field => _field;
        public ClrElementKind Kind => _kind;
        public int FieldIndex => _fldIndex;
        public FilterValue Filter => _filter;
		public string[] Values => _values;
        public bool IsAlternative => _alternative;
        public bool HasAlternativeChild => _alternativeChild;
        public string TypeName => _typeName;
        public string FieldName => _fieldName;

        public bool GetValue => _getValue;
        public bool HasFilter => _filter != null;
        public bool IsInternal => TypeExtractor.IsInternal(_kind);
        public bool HasChildren => _children != null;

        public string[] Data => _values;
        public int RowValueCount => _valOffset;
        public int ValueCount => _valOffset < 1 ? 0 : _valIndex / _valOffset;




        public TypeValueQuery(long id, TypeValueQuery parent, string typeName, string fieldName, int fieldIndex, bool isAlternative, FilterValue filter, bool getValue)
        {
            _id = id;
            _parent = parent;
            _typeName = typeName;
            _fieldName = fieldName;
            _fldIndex = fieldIndex;
            _alternative = isAlternative;
            _altParentKind = ClrElementKind.Unknown;
            _getValue = getValue;
        }

        public void AddChild(TypeValueQuery child)
        {
            if (child.IsAlternative)
                _alternativeChild = true;
            if (_children == null)
            {
                _children = new TypeValueQuery[] { child };
                return;
            }
            TypeValueQuery[] ary = new TypeValueQuery[_children.Length + 1];
            int ndx = 0;
            for (int i = 0, icnt = _children.Length; i < icnt; ++i)
            {
                if (child != null && child._fldIndex < _children[i]._fldIndex)
                {
                    ary[ndx++] = child;
                    child = null;
                }
                ary[ndx++] = _children[i];
            }
            if (child != null) ary[_children.Length] = child;
            _children = ary;
        }

        public bool AssignAlternativeParent(ClrType type, ClrElementKind kind, ClrInstanceField fld)
        {
            if (Utils.SameStrings(_fieldName,fld.Name) && Utils.SameStrings(_typeName,type.Name) )
            {
                _altParentType = type;
                _altParentKind = kind;
                _altParentField = fld;
                return true;
            }
            return false;
        }

        public void SetTypeAndKind(ClrType clrType, ClrElementKind kind)
        {
            _type = clrType;
            _kind = kind;
        }

        public bool IsCompatibleField(int fldIndex, string fldName, string fldTypeName)
        {
            if (_children != null &&   fldIndex >= 0 && fldIndex < _children.Length)
            {
                var child = _children[fldIndex];
                return Utils.SameStrings(fldName, child._fieldName) && Utils.SameStrings(fldTypeName, child._typeName);
            }
            return false;
        }

        public void SetField(ClrType parent)
        {
            if (parent.Fields != null && _fldIndex >= 0 && _fldIndex < parent.Fields.Count)
            {
                _field = parent.Fields[_fldIndex];
                _kind = TypeExtractor.GetElementKind(_field.Type);
            }
        }

        public void SetValuesStore(string[] data, int index, int width)
        {
            _values = data;
            _valIndex = index;
            _valOffset = width;
        }

        public void AddValue(string val)
        {
            _values[_valIndex] = val;
            _valIndex += _valOffset;
        }

        public bool Accept(object val)
        {
            if (_filter == null) return true;
            return _filter.Accept(val);
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
			NONE = 0,
			EXCLUDE = 1,
			EQ = 1 << 1,
			LT = 1 << 2,
			GT = 1 << 3,
			AND = 1 << 4,
            OR = 1 << 5,
            IGNORECASE = 1 << 6,
            REGEX = 1 << 7,
			LTEQ = 1 << 8,
			GTEQ = 1 << 9,
			NOTEQ = 1 << 10,

			COUNT = 12
		}

		public static string GetOpDescr(Op op)
		{
			switch(op)
			{
				case Op.NONE:
					return "none";
				case Op.EXCLUDE:
					return "exclude";
				case Op.EQ:
					return "equal";
				case Op.LT:
					return "less than";
				case Op.GT:
					return "greater than";
				case Op.AND:
					return "and";
				case Op.OR:
					return "or";
				case Op.IGNORECASE:
					return "ignore case";
				case Op.REGEX:
					return "regex";
				case Op.LTEQ:
					return "less then or equal";
				case Op.GTEQ:
					return "greater then or equal";
				case Op.NOTEQ:
					return "not equal";
				default:
					return "none";
			}
		}

		public static Op GetOpFromDescr(string opstr)
		{
			opstr = opstr.ToLower().Trim();
			switch (opstr)
			{
				case "none":
					return Op.NONE;
				case "exclude":
					return Op.EXCLUDE;
				case "equal":
					return Op.EQ;
				case "less than":
					return Op.LT;
				case "greater than":
					return Op.GT;
				case "and":
					return Op.AND;
				case "or":
					return Op.OR;
				case "ignore case":
					return Op.IGNORECASE;
				case "regex":
					return Op.REGEX;
				case "less then or equal":
					return Op.LTEQ;
				case "greater then or equal":
					return Op.GTEQ;
				case "not equal":
					return Op.NOTEQ;
				default:
					return Op.NONE;
			}
		}

		public static bool IsOp(Op op, Op opVal)
		{
			return ((int)op & (int)opVal) != 0;
		}

		private Op _op;
		private object[] _values;
		private object _value;
		private string _filterString;
		private Regex _regex;
		public string FilterString => _filterString;
		private ClrElementKind _kind;

		//public FilterValue(string filterStr, ClrElementKind kind, Op oper)
		//{
		//	_filterString = filterStr;
		//	_kind = kind;
		//	_op = oper;
		//}

		public FilterValue(string filterStr, ClrElementKind kind, Op oper, Regex regex)
		{
			_filterString = filterStr;
			_kind = kind;
			_op = oper;
			_regex = regex;
		}

		public FilterValue(object value, ClrElementKind kind, Op oper)
		{
			_value = value;
			_kind = kind;
			_op = oper;
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
			bool accept = true;
			var specKind = TypeExtractor.GetSpecialKind(_kind);

			if (specKind != ClrElementKind.Unknown)
			{
				switch (specKind)
				{
					case ClrElementKind.Exception:
					case ClrElementKind.Enum:
					case ClrElementKind.Free:
						break;
					case ClrElementKind.Guid:
						accept = AcceptGuid((Guid)obj, (Guid)_value, _op);
						break;
					case ClrElementKind.DateTime:
						accept = AcceptDateTime((DateTime)obj, (DateTime)_value, _op);
						break;
					case ClrElementKind.TimeSpan:
						accept = AcceptTimeSpan((TimeSpan)obj, (TimeSpan)_value, _op);
						break;
					case ClrElementKind.Decimal:
						accept = AcceptDecimal((decimal)obj, (decimal)_value, _op);
						break;
					case ClrElementKind.SystemVoid:
						break;
					case ClrElementKind.SystemObject:
					case ClrElementKind.Interface:
					case ClrElementKind.Abstract:
					case ClrElementKind.System__Canon:
						accept = AcceptUlong((ulong)obj, (ulong)_value, _op);
						break;
					default:
						break;
				}
			}
			else
			{
				var stdKind = TypeExtractor.GetStandardKind(_kind);
				switch (stdKind)
				{
					case ClrElementKind.Class:
					case ClrElementKind.Struct:
						break;
					case ClrElementKind.SZArray:
					case ClrElementKind.Array:
					case ClrElementKind.Object:
						accept = AcceptUlong((ulong)obj, (ulong)_value, _op);
						break;
					case ClrElementKind.Boolean:
					case ClrElementKind.Char:
					case ClrElementKind.Int8:
					case ClrElementKind.UInt8:
					case ClrElementKind.Int16:
					case ClrElementKind.UInt16:
					case ClrElementKind.Int32:
					case ClrElementKind.UInt32:
					case ClrElementKind.Int64:
					case ClrElementKind.UInt64:
						accept = AcceptUlong((ulong)obj, (ulong)_value, _op);
						break;
					case ClrElementKind.Float:
					case ClrElementKind.Double:
						accept = AcceptDouble((double)obj, (double)_value, _op);
						break;
					case ClrElementKind.String:
						accept = AcceptString((string)obj, _filterString, _op);
						break;
					case ClrElementKind.Pointer:
					case ClrElementKind.NativeInt:
					case ClrElementKind.NativeUInt:
					case ClrElementKind.FunctionPointer:
						accept = AcceptUlong((ulong)obj, (ulong)_value, _op);
						break;
					default:
						break;
				}
			}

			return accept;
		}

		private bool AcceptDecimal(decimal val, decimal other, Op op) 
		{
			if (FilterValue.IsOp(Op.EQ, op))
				return val == other;
			else if (FilterValue.IsOp(Op.LT, op))
				return val < other;
			else if (FilterValue.IsOp(Op.LTEQ, op))
				return val <= other;
			else if (FilterValue.IsOp(Op.GT, op))
				return val > other;
			else if (FilterValue.IsOp(Op.GTEQ, op))
				return val >= other;
			else
				return true;
		}

		private bool AcceptDouble(double val, double other, Op op)
		{
			if (FilterValue.IsOp(Op.EQ, op))
				return val == other;
			else if (FilterValue.IsOp(Op.LT, op))
				return val < other;
			else if (FilterValue.IsOp(Op.LTEQ, op))
				return val <= other;
			else if (FilterValue.IsOp(Op.GT, op))
				return val > other;
			else if (FilterValue.IsOp(Op.GTEQ, op))
				return val >= other;
			else
				return true;
		}

		private bool AcceptUlong(ulong val, ulong other, Op op)
		{
			if (FilterValue.IsOp(Op.EQ, op))
				return val == other;
			else if (FilterValue.IsOp(Op.LT, op))
				return val < other;
			else if (FilterValue.IsOp(Op.LTEQ, op))
				return val <= other;
			else if (FilterValue.IsOp(Op.GT, op))
				return val > other;
			else if (FilterValue.IsOp(Op.GTEQ, op))
				return val >= other;
			else
				return true;
		}

		private bool AcceptDateTime(DateTime val, DateTime other, Op op)
		{
			if (FilterValue.IsOp(Op.EQ, op))
				return val == other;
			else if (FilterValue.IsOp(Op.LT, op))
				return val < other;
			else if (FilterValue.IsOp(Op.LTEQ, op))
				return val <= other;
			else if (FilterValue.IsOp(Op.GT, op))
				return val > other;
			else if (FilterValue.IsOp(Op.GTEQ, op))
				return val >= other;
			else
				return true;
		}

		private bool AcceptTimeSpan(TimeSpan val, TimeSpan other, Op op)
		{
			if (FilterValue.IsOp(Op.EQ, op))
				return val == other;
			else if (FilterValue.IsOp(Op.LT, op))
				return val < other;
			else if (FilterValue.IsOp(Op.LTEQ, op))
				return val <= other;
			else if (FilterValue.IsOp(Op.GT, op))
				return val > other;
			else if (FilterValue.IsOp(Op.GTEQ, op))
				return val >= other;
			else
				return true;
		}

		private bool AcceptGuid(Guid val, Guid other, Op op)
		{
			if (FilterValue.IsOp(Op.EQ, op))
				return val == other;
			else if (FilterValue.IsOp(Op.NOTEQ, op))
				return val != other;
			else
				return true;
		}

		private bool AcceptString(string val, string other, Op op)
		{
			if (FilterValue.IsOp(Op.REGEX, op))
				return _regex.IsMatch(other);

			else if (FilterValue.IsOp(Op.EQ, op))
				return string.Compare(val, other, op == Op.IGNORECASE ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) == 0;
			else if (FilterValue.IsOp(Op.NOTEQ, op))
				return string.Compare(val, other, op == Op.IGNORECASE ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) != 0;
			return true;
		}


		public bool AcceptNull()
		{
			return true;
		}

	}
}
