using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
    /// <summary>
    /// Helper class to gather type/instance references.
    /// </summary>
	public class IndexNode
	{
		public int Index; // instance index
		public int Level; // level in references tree
		public IndexNode[] Nodes; // this instance references

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
