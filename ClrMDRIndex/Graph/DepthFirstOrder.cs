using System.Collections.Generic;

// ReSharper disable CheckNamespace

namespace ClrMDRIndex
{
	public class DepthFirstOrder
	{
		private readonly bool[] _marked;		// marked[v] = has v been marked in dfs?
		private readonly int[] _pre;			// pre[v]    = preorder  number of v
		private readonly int[] _post;			// post[v]   = postorder number of v
		private readonly Queue<int> _preorder;	// vertices in preorder
		private readonly Queue<int> _postorder;	// vertices in postorder
		private int _preCounter;				// counter or preorder numbering
		private int _postCounter;				// counter for postorder numbering

		public DepthFirstOrder(Digraph g)
		{
			_pre = new int[g.VertexCount];
			_post = new int[g.VertexCount];
			_postorder = new Queue<int>();
			_preorder = new Queue<int>();
			_marked = new bool[g.VertexCount];
			for (var v = 0; v < g.VertexCount; ++v)
			{
				if (!_marked[v]) Dfs(g, v);
			}
		}

		private void Dfs(Digraph g, int v)
		{
			_marked[v] = true;
			_pre[v] = _preCounter++;
			_preorder.Enqueue(v);
			var adjList = g.AdjacencyList(v);
			for (var i = 0; i < adjList.Count; ++i)
			{
				var w = adjList[i];
				if (!_marked[w])
				{
					Dfs(g, w);
				}
			}
			_postorder.Enqueue(v);
			_post[v] = _postCounter++;
		}

		public IEnumerable<int> PostOrder()
		{
			return _postorder;
		}
		public IEnumerable<int> PreOrder()
		{
			return _preorder;
		}

		public int PostOrder(int v)
		{
			return _post[v];
		}

		public int PreOrder(int v)
		{
			return _pre[v];
		}

		public IEnumerable<int> ReversePost() 
		{
			var reverse = new Stack<int>();
			foreach (var v in _postorder)
			{
				reverse.Push(v);
			}
			return reverse;
		}

		public bool Check(out string error)
		{
			error = null;
			// check that post(v) is consistent with post()
			var r = 0;
			var order = PostOrder();
			foreach (var v in order)
			{
				if (PostOrder(v) != r)
				{
					error = "PostOrder(v) and PostOrder() inconsistent";
					return false;
				}
				r++;
			}

			// check that pre(v) is consistent with pre()
			r = 0;
			order = PreOrder();
			foreach (var v in order)
			{
				if (PreOrder(v) != r)
				{
					error = "PreOrder(v) and PreOrder() inconsistent";
					return false;
				}
				r++;
			}
			return true;
		}
	}
}

// ReSharper restore CheckNamespace
