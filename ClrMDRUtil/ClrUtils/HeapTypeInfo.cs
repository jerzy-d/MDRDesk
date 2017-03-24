using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
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
}
