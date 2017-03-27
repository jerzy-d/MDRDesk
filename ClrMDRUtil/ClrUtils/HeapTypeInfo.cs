using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;
using System.Collections;

namespace ClrMDRIndex
{
   public class TypeValueReportInfo
    {
        private ulong[] _addresses;
        private int _typeId;
        HeapTypeInfo _typeInfo;

    }


    public class HeapTypeInfo
    {
        private ClrType _clrType;
        public ClrType Type => _clrType;
        private ClrInstanceField _field;
        public ClrInstanceField Field => _field;

        private HeapTypeInfo _parent;
        public HeapTypeInfo Parent => _parent;
        private HeapTypeInfo[] _children;
        public HeapTypeInfo[] Children => _children;


    }

	public class TypeValueReportInfoItem
	{
        private TypeValueReportInfoItem _parent;
        private TypeValueReportInfoItem[] _children;

        private string _fieldName;
		private ValueFilter _filter;

		public void SetFilter(ValueFilter filter)
		{
			_filter = filter;
		}



	}

	public class ValueFilter
	{
		private object _value;
		private IComparer _comparer;
        private bool _skip;
        public bool Skip => _skip;
        

		public ValueFilter(object value, IComparer comparer, bool skip=false)
		{
			_value = value;
			_comparer = comparer;
            _skip = skip;
		}

		public bool Accept(object value)
		{
			return _comparer.Compare(_value, value) == 0;
		}
	}
}
