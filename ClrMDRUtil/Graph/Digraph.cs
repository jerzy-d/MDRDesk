using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Diagnostics.Runtime;

// ReSharper disable CheckNamespace

namespace ClrMDRIndex
{
	public class Digraph
	{
		public int VertexCount { get; private set; }
		public int EdgeCount { get; private set; }
		private readonly List<int>[] _adjacencyLists;

		public Digraph(int vertexCount, bool doNotInitAdjLists=false)
		{
			VertexCount = vertexCount;
			EdgeCount = 0;
			_adjacencyLists = new List<int>[vertexCount];
			if (!doNotInitAdjLists)
			{
				for (var i = 0; i < vertexCount; ++i)
				{
					_adjacencyLists[i] = new List<int>();
				}
			}
		}

		private List<int> CreateEmptyAdjList(int v, int size)
		{
			_adjacencyLists[v] = new List<int>(size);
			return _adjacencyLists[v];
		}

		private void SetEdgeCount(int edgeCnt)
		{
			EdgeCount = EdgeCount;
		}

		public List<int> AdjacencyList(int v)
		{
			if (v < 0 || v >= VertexCount)
			{
				throw new ArgumentOutOfRangeException("[Diagraph.AdjacencyList] Vertex index out of bounds, VertexCount = " + v + ",");
			}
			return _adjacencyLists[v];
		}

		public void AddEdge(int v, int w)
		{
			if (v < 0 || v >= VertexCount || w < 0 || w >= VertexCount)
			{
				throw new ArgumentOutOfRangeException("[Diagraph.AddEdge] Vertex index out of bounds, VertexCount = " + v + ", w = " + w + ".");
			}
			_adjacencyLists[v].Add(w);
			++EdgeCount;
		}

		public void AddDistinctEdge(int v, int w)
		{
			if (v < 0 || v >= VertexCount || w < 0 || w >= VertexCount)
			{
				throw new ArgumentOutOfRangeException("[Diagraph.AddDistinctEdge] Vertex index out of bounds, VertexCount = " + v + ", w = " + w + ".");
			}
			if (_adjacencyLists[v].Contains(w)) return;
			_adjacencyLists[v].Add(w);
			++EdgeCount;
		}

		public Digraph Reverse() {
			var r = new Digraph(VertexCount);
			for (int v = 0; v < VertexCount; ++v)
			{
				int vcount = _adjacencyLists[v].Count;
				for (int i = 0;  i < vcount; ++i)
				{
					r.AddEdge(_adjacencyLists[v][i], v);
				}
			}
			return r;
		}

		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.Append(VertexCount).Append(" vertices, ").Append(EdgeCount).Append(" edges. ").AppendLine();
			for (int v = 0; v < VertexCount; ++v)
			{
				sb.Append(v).Append(": ");
				var acount = _adjacencyLists[v].Count;
				for (var i = 0; i < acount; ++i)
				{
					sb.Append(_adjacencyLists[v][i]).Append(", ");
				}
				sb.AppendLine();
			}
			return sb.ToString();
		}

		public bool Dump(BinaryWriter wr, out string error)
		{
			error = null;
			try
			{
				wr.Write(VertexCount);
				wr.Write(EdgeCount);
				wr.Write(_adjacencyLists.Length);
				for (int i = 0, cnt = _adjacencyLists.Length; i < cnt; ++i)
				{
					var adj = _adjacencyLists[i];
					wr.Write(adj.Count);
					for (int j = 0, acnt = adj.Count; j < acnt; ++j)
					{
						wr.Write(adj[j]);
					}
				}

				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}

		public static Digraph Load(BinaryReader rd, out string error)
		{
			error = null;
			Digraph graph = null;
			try
			{
				var vcnt = rd.ReadInt32();
				graph = new Digraph(vcnt);
				var ecnt = rd.ReadInt32();
				graph.SetEdgeCount(ecnt);
				var acnt = rd.ReadInt32();
				for (int i = 0; i < acnt; ++i)
				{
					var lcount = rd.ReadInt32();
					var alst = graph.CreateEmptyAdjList(i,lcount);
					for (int j = 0; j < lcount; ++j)
					{
						var e = rd.ReadInt32();
						alst.Add(e);
					}
				}

				return graph;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		//public string ToString<T>(T[] nodes) where T : IEData
		//{
		//	var sb = new StringBuilder();
		//	sb.Append(VertexCount).Append(" vertices, ").Append(EdgeCount).Append(" edges. ").AppendLine();
		//	for (int v = 0; v < VertexCount; ++v)
		//	{
		//		sb.Append(v).Append(": ");
		//		var acount = _adjacencyLists[v].Count;
		//		for (var i = 0; i < acount; ++i)
		//		{
		//			sb.Append(_adjacencyLists[v][i]).Append(", ");
		//		}
		//		sb.AppendLine();
		//	}
		//	return sb.ToString();
		//}
	}
}

// ReSharper restore CheckNamespace
