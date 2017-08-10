using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class InstanceValue
	{
		private int _typeId;
        private ClrElementKind _kind;
        private int _fieldNdx; // if _fieldNdx value is valid, then this is index of the parent field.
		private ulong _address;
		private string _typeName;
		private string _fieldName;
        private DisplayableString _value;
        private KeyValuePair<DisplayableString, DisplayableString>[] _keyValuePairs;
        private DisplayableString[] _aryValues;
        private string[] _aryTypes;
		private InstanceValue[] _fields;
		private InstanceValue _parent;
        private object _extraData;

        public int TypeId => _typeId;
        public ClrElementKind Kind => _kind;
        public int FieldIndex => _fieldNdx;
        public ulong Address => _address;
		public string TypeName => _typeName;
		public string FieldName => _fieldName;
        public DisplayableString Value => _value;
		public InstanceValue[] Fields => _fields;
		public DisplayableString[] ArrayValues => _aryValues;
        public KeyValuePair<DisplayableString, DisplayableString>[] KeyValuePairs => _keyValuePairs;
        public object ExtraData => _extraData;
		public InstanceValue Parent => _parent;

		public InstanceValue(int typeId, ClrElementKind kind, ulong addr, string typeName, string fldName, string value, int fldNdx = Constants.InvalidIndex, InstanceValue parent = null)
		{
			_typeId = typeId;
            _kind = kind;
			_address = addr;
		    _typeName = typeName;
		    _fieldName = fldName;
		    _value = new DisplayableString(value);
		    _fields = Utils.EmptyArray<InstanceValue>.Value;
			_fieldNdx = fldNdx;
			_aryValues = null;
			_parent = parent;
		}

        public InstanceValue(int typeId, ulong addr, string typeName, string fldName, string value, int fldNdx = Constants.InvalidIndex)
        {
            _typeId = typeId;
            _kind = ClrElementKind.Unknown;
            _address = addr;
            _typeName = typeName;
            _fieldName = fldName;
            _value = new DisplayableString(value);
			_fields = Utils.EmptyArray<InstanceValue>.Value;
            _fieldNdx = fldNdx;
            _aryValues = null;
			_parent = null;
        }


        public bool HaveFields()
		{
			return _fields != null && _fields.Length > 0;
		}

	    public void SetFields(InstanceValue[] fields)
	    {
			_fields = fields;
	    }

        public void AddArrayTypes(List<string> lst)
        {
            Debug.Assert(lst.Count > 2);
            _aryTypes = new string[lst.Count - 1];
            for (int i = 1, icnt = lst.Count; i < icnt; ++i)
            {
                _aryTypes[i - 1] = lst[i];
            }
            Array.Sort(_aryTypes, StringComparer.Ordinal);
        }

		public void AddArrayValues(string[] aryvalues)
		{
            if (aryvalues == null)
            {
                _aryValues = null;
                return;
            }
            _aryValues = new DisplayableString[aryvalues.Length];
            for (int i = 0, icnt = aryvalues.Length; i < icnt; ++i)
            {
                _aryValues[i] = new DisplayableString(aryvalues[i]);
            }
		}

        public void AddKeyValuePairs(KeyValuePair<string,string>[] keyValuePairs)
        {
            if (keyValuePairs == null)
            {
                _keyValuePairs = null;
                return;
            }
            _keyValuePairs = new KeyValuePair<DisplayableString, DisplayableString>[keyValuePairs.Length];
            for (int i = 0, icnt = keyValuePairs.Length; i < icnt; ++i)
            {
                _keyValuePairs[i] = new KeyValuePair<DisplayableString, DisplayableString>(
                                            new DisplayableString(keyValuePairs[i].Key),
                                            new DisplayableString(keyValuePairs[i].Value));
            }
        }

        public void AddExtraData(object data)
        {
            _extraData = data;
        }

        public bool IsArray()
		{
			return _aryValues != null;
		}

		public int ArrayLength()
		{
			return _aryValues == null ? 0 : _aryValues.Length;
		}

		public void SortByFieldName()
		{
			if (_fields == null || _fields.Length < 1) return;
			Array.Sort(_fields, new InstanceValueFieldCmp());
		}

        public void SortByFieldIndex()
        {
            if (_fields == null || _fields.Length < 1) return;
            Array.Sort(_fields, new InstanceValueFieldNdxCmp());
        }

        public string GetDescription()
        {
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
            if (IsArray())
            {
                sb.Append("Type:      ").AppendLine(TypeName);
                sb.Append("Item Type: ").AppendLine(Fields[0].TypeName);
                sb.Append("Address:   ").AppendLine(Utils.RealAddressString(Address));
                sb.Append("Lenght:    ").AppendLine(Utils.CountString(ArrayLength()));
            }
            else
            {
                sb.Append("Type:      ").AppendLine(TypeName);
                sb.Append("Address:   ").AppendLine(Utils.RealAddressString(Address));
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        public override string ToString()
	    {
	        string value = _value.ToString();
	        return
				_fieldName
				+ "  "
				+ Constants.LeftCurlyBracket
	            + ((value.Length> 0 && value[0] == Constants.NonValueChar) ? (Constants.FancyKleeneStar.ToString() + Utils.RealAddressString(_address)) : _value.ToString())
				+ Constants.RightCurlyBracket
				+ "   "
				+ _typeName;
	    }
	}


	public interface IGetKey<T>
	{
		T GetKey();
	}

	public class InstanceValueAndAncestors : IGetKey<Tuple<ulong,int>>
	{
		public InstanceValue Instance { get; private set; }
		public AncestorDispRecord[] Ancestors { get; private set; }

		public InstanceValueAndAncestors(InstanceValue instance, AncestorDispRecord[] ancestors)
		{
			Instance = instance;
			Ancestors = ancestors;
		}

		public Tuple<ulong, int> GetKey()
		{
			return new Tuple<ulong, int>(Instance.Address,Instance.FieldIndex);
		}
	}

	internal class InstanceValueFieldCmp : IComparer<InstanceValue>
	{
		public int Compare(InstanceValue a, InstanceValue b)
		{
			int cmp = string.Compare(a.FieldName, b.FieldName, StringComparison.Ordinal);
			return cmp == 0
				? string.Compare(a.TypeName, b.TypeName, StringComparison.Ordinal)
				: cmp;
		}
	}

    internal class InstanceValueFieldNdxCmp : IComparer<InstanceValue>
    {
        public int Compare(InstanceValue a, InstanceValue b)
        {
            return a.FieldIndex < b.FieldIndex ? -1 : (a.FieldIndex > b.FieldIndex ? 1 : 0);
        }
    }

    internal class InstanceHierarchyInfoEqCmp : IEqualityComparer<Tuple<InstanceValue, AncestorDispRecord[]>>
	{
		public bool Equals(Tuple<InstanceValue, AncestorDispRecord[]> b1, Tuple<InstanceValue, AncestorDispRecord[]> b2)
		{
			return b1.Item1.Address == b2.Item1.Address;
		}

		public int GetHashCode(Tuple<InstanceValue, AncestorDispRecord[]> bx)
		{
			return bx.Item1.Address.GetHashCode();
		}

	}

	public class InstanceHierarchyKeyEqCmp : IEqualityComparer<Tuple<ulong,int>>
	{
		public bool Equals(Tuple<ulong,int> b1, Tuple<ulong,int> b2)
		{
			return b1.Item1 == b2.Item1 && b1.Item2 == b2.Item2;
		}

		public int GetHashCode(Tuple<ulong,int> bx)
		{
			return bx.Item1.GetHashCode()^bx.Item2.GetHashCode();
		}

	}
}
