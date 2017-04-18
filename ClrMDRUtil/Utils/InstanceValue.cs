﻿using System;
using System.Collections.Generic;
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
		private DisplayableString[] _aryValues;
		private InstanceValue[] _fields;
		private InstanceValue _parent;

        public int TypeId => _typeId;
        public ClrElementKind Kind => _kind;
        public int FieldIndex => _fieldNdx;
        public ulong Address => _address;
		public string TypeName => _typeName;
		public string FieldName => _fieldName;
        public DisplayableString Value => _value;
		public InstanceValue[] Fields => _fields;
		public DisplayableString[] ArrayValues => _aryValues;
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

		public override string ToString()
	    {
	        string value = _value.Content;
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
