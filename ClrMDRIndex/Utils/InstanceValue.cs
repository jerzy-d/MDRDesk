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
		private ulong _address;
		private string _value;
		private InstanceValue[] _values;

		public InstanceValue(int typeId, ulong addr)
		{
			_typeId = typeId;
			_address = addr;
		}
	}
}
