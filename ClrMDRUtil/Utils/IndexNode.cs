using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class IndexNode
	{
		public int Index;
		public int Level;
		public IndexNode[] Nodes;

		public IndexNode(int index, int level)
		{
			Index = index;
			Level = level;
			Nodes = Utils.EmptyArray<IndexNode>.Value;
		}

		public void AddNodes(IndexNode[] nodes)
		{
			Nodes = nodes;
		}
	}
}
