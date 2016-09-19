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
		private ulong _address;
	    private string _typeName;
	    private string _fieldName;
		private string _value;
		private List<InstanceValue> _values;
	    public List<InstanceValue> Values => _values;

		public InstanceValue(int typeId, ulong addr, string typeName, string fldName, string value)
		{
			_typeId = typeId;
			_address = addr;
		    _typeName = typeName;
		    _fieldName = fldName;
		    _value = value;
		    _values = new List<InstanceValue>(0);
		}



	    public void Addvalue(InstanceValue val)
	    {
	        _values.Add(val);
	    }

	    public override string ToString()
	    {
	        return
	            ((_value.Length> 1 && _value[0] == Constants.NonValueChar) ? Utils.AddressStringHeader(_address) : _value)
	            + "(" + _fieldName + ") " + _typeName;
	    }
	}
}
