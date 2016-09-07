using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class InstanceNode
	{
		private int _instanceId;
		private ulong _addr;
		private int _level;
		InstanceNode[] _nodes;
		private object _data;
		private RootInfo _rootInfo;

		public int InstanceId => _instanceId;
		public ulong Address => _addr;
		public int Level => _level;
		public InstanceNode[] Nodes => _nodes;
		public bool EmptyNodeList => _nodes.Length < 1;
		public int NodeCount => _nodes.Length;
		public object Data => _data;
		public RootInfo RootInfo => _rootInfo;

		public InstanceNode(int ndx, ulong addr, int level, InstanceNode[] nodes)
		{
			_instanceId = ndx;
			_addr = addr;
			_level = level;
			_nodes = nodes;
			_data = null;
			_rootInfo = null;
		}

		public InstanceNode(ulong addr, int level)
		{
			_instanceId = Constants.InvalidIndex;
			_addr = addr;
			_level = level;
			_nodes = Utils.EmptyArray<InstanceNode>.Value;
			_data = null;
			_rootInfo = null;
		}

        public InstanceNode(int ndx, ulong addr, int level)
        {
            _instanceId = ndx;
            _addr = addr;
            _level = level;
            _nodes = Utils.EmptyArray<InstanceNode>.Value;
            _data = null;
            _rootInfo = null;
        }

        public void SetRootInfo(RootInfo root)
		{
			_rootInfo = root;
		}

		public void SetInstanceIndex(int ndx)
		{
			_instanceId = ndx;
		}

		public void SetNodes(InstanceNode[] nodes)
		{
			_nodes = nodes;
		}

		public static InstanceNode DummyNode = new InstanceNode(IndexValue.InvalidIndex, Constants.InvalidAddress, 0, null);
	}

	public class InstanceNodeCmp : IComparer<InstanceNode>
	{
		public int Compare(InstanceNode a, InstanceNode b)
		{
			if (a.InstanceId == b.InstanceId)
			{
				if (a.Address == b.Address)
				{
					return a.Level < b.Level ? -1 : (a.Level > b.Level ? 1 : 0);
				}
				return a.Address < b.Address ? -1 : 1;
			}
			return a.InstanceId < b.InstanceId ? -1 : 1;
		}
	}
}
