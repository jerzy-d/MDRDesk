using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	public class InstanceSizeNode
	{
		private int _nodeId;
		public int NodeId => _nodeId;
		private string _typeName;
		private ClrElementType _elementType;
		private string _fieldName;
		private string _value;
		private ulong _size;
		private InstanceSizeNode[] _nodes;

		public InstanceSizeNode(int id, string typeName, ClrElementType elem, string fldName, string value, ulong sz)
		{
			_nodeId = id;
			_typeName = typeName;
			_elementType = elem;
			_fieldName = fldName;
			_value = value;
			_size = sz;
			_nodes = Utils.EmptyArray<InstanceSizeNode>.Value;
		}

		public void AddNodes(IList<InstanceSizeNode> lst)
		{
			_nodes = lst.ToArray();
		}

		public override string ToString()
		{
			return Utils.SizeStringHeader((long) _size)
					+ _fieldName
					+ " {" + _value + "} "
					+ _typeName;
		}
	}
}
