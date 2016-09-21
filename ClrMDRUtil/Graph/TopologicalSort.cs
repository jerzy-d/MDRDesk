using System.Collections.Generic;

// ReSharper disable CheckNamespace

namespace ClrMDRIndex
{
	public class TopologicalSort
	{
		private IEnumerable<int> _order;    // topological order

		public bool Sort(Digraph g, bool checkCycle)
		{
			var hasCycle = false;
			if (checkCycle)
			{
				var finder = new DirectedCycle(g);
				hasCycle = finder.HasCycle();
			}
			if (hasCycle) return false;
			var dfs = new DepthFirstOrder(g);
			_order = dfs.ReversePost();
			return HasOrder();
		}

		public IEnumerable<int> Order()
		{
			return _order;
		}
		
		public bool HasOrder()
		{
			return _order != null;
		}
	}
}

// ReSharper restore CheckNamespace
