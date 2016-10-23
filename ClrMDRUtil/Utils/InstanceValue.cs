using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class InstanceValue
	{
		private int _typeId;
		private int _fieldNdx; // If _fieldNdx value is valid, then this is a structure with fields, and address is of parent's instance.
		private ulong _address;
		private string _typeName;
		private string _fieldName;
		private ValueString _value;
		private List<InstanceValue> _values;

		public int FieldIndex => _fieldNdx;
		public ulong Address => _address;
		public string TypeName => _typeName;
		public string FieldName => _fieldName;
        public ValueString Value => _value;
		public List<InstanceValue> Values => _values;

		public InstanceValue(int typeId, ulong addr, string typeName, string fldName, string value, int fldNdx = Constants.InvalidIndex)
		{
			_typeId = typeId;
			_address = addr;
		    _typeName = typeName;
		    _fieldName = fldName;
		    _value = new ValueString(value);
		    _values = new List<InstanceValue>(0);
			_fieldNdx = fldNdx;
		}



	    public void Addvalue(InstanceValue val)
	    {
	        _values.Add(val);
	    }

		public void SortByFieldName()
		{
			_values?.Sort(new InstanceValueFieldCmp());
		}

		public override string ToString()
	    {
	        string value = _value.Content;
	        return
				_fieldName
				+ "  "
				+ Constants.LeftCurlyBracket
	            + ((value.Length> 0 && value[0] == Constants.NonValueChar) ? (Constants.FancyKleeneStar.ToString() + Utils.AddressString(_address)) : _value.ToString())
				+ Constants.RightCurlyBracket
				+ "   "
				+ _typeName;
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
}
