using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class InstanceTypeNode
	{
		private int _typeId;
		private string _typeName;
		private int _instNdx;
		private ulong _addr;
		private int _level;
        private InstanceTypeNode[] _nodes;
	    private string _rootDescr;


        public int TypeId => _typeId;
		public string TypeName => _typeName;
		public InstanceTypeNode[] TypeNodes => _nodes;
		public int InstNdx => _instNdx;
		public ulong Address => _addr;
		public int Level => _level;

        public InstanceTypeNode(int typeId, string typeName, int instNdx, ulong addr, int level)
        {
            _typeId = typeId;
            _typeName = typeName;
            _instNdx = instNdx;
            _addr = addr;
            _level = level;
            _nodes = Utils.EmptyArray<InstanceTypeNode>.Value;
            _rootDescr = null;
        }

        public void GetNodeList(Queue<InstanceTypeNode> que, List<InstanceTypeNode> lst, bool reversed)
		{
			Debug.Assert(que.Count == 0);
			Debug.Assert(lst.Count == 0);
			if (_nodes.Length < 1) return;
			for (int i = 0, icnt = _nodes.Length; i < icnt; ++i)
			{
				lst.Add(_nodes[i]);
				que.Enqueue(_nodes[i]);
			}
			while (que.Count > 0)
			{
				var node = que.Dequeue();
				for (int i = 0, icnt = node._nodes.Length; i < icnt; ++i)
				{
					lst.Add(node._nodes[i]);
					que.Enqueue(node._nodes[i]);
				}
			}
			if (reversed)
				lst.Reverse();
		}

		public void AddNodes(InstanceTypeNode[] nodes)
		{
			_nodes = nodes;
		}

	    public void AddRoot(string rootDescr)
	    {
	        _rootDescr = rootDescr;
	    }

	    public override string ToString()
	    {
	        return Utils.AddressStringHeader(_addr) + _typeName + _rootDescr;
	    }

	    public static InstanceTypeNode DummyNode = new InstanceTypeNode(IndexValue.InvalidIndex, string.Empty, Constants.InvalidIndex,Constants.InvalidAddress,0);

	}
}
