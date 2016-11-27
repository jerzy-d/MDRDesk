using System.Collections.Generic;

// ReSharper disable CheckNamespace

namespace ClrMDRIndex
{
	public class DirectedCycle
	{
		private readonly bool[] _marked;       // marked[v] = has vertex v been marked?
		private readonly int[] _edgeTo;        // edgeTo[v] = previous vertex on path to v
		private readonly bool[] _onStack;      // onStack[v] = is vertex on the stack?
		private Stack<int> _cycle;				// directed cycle (or null if no such cycle)

		public DirectedCycle(Digraph g)
		{
			_marked = new bool[g.VertexCount];
			_onStack = new bool[g.VertexCount];
			_edgeTo = new int[g.VertexCount];
			for (int v = 0; v < g.VertexCount; v++)
				if (!_marked[v]) Dfs(g, v);
		}

		private void Dfs(Digraph g, int v)
		{
			_onStack[v] = true;
			_marked[v] = true;
			var adjList = g.AdjacencyList(v);
			foreach (int w in adjList)
			{
				// short circuit if directed cycle found
				if (_cycle != null) return;

				//found new vertex, so recur
				if (!_marked[w])
				{
					_edgeTo[w] = v;
					Dfs(g, w);
				}

					// trace back directed cycle
				else if (_onStack[w])
				{
					_cycle = new Stack<int>();
					for (var x = v; x != w; x = _edgeTo[x])
					{
						_cycle.Push(x);
					}
					_cycle.Push(w);
					_cycle.Push(v);
				}
			}

			_onStack[v] = false;
		}

		public bool HasCycle()
		{
			return _cycle != null;
		}

		public int[] GetCycle()
		{
			return _cycle?.ToArray() ?? Utils.EmptyArray<int>.Value;
		}

		public bool Check(out string error)
		{
			error = null;
			if (HasCycle())
			{
				// verify cycle
				int first = -1, last = -1;
				foreach (int v in _cycle)
				{
					if (first == -1) first = v;
					last = v;
				}
				if (first != last)
				{
					error = "cycle begins with " + first + " and ends with " + last;
					return false;
				}
			}
			return true;
		}
	}
}

// ReSharper restore CheckNamespace
