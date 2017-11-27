using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
    public class DGraph
    {
        int _edgeCount;
        int[][] _graph;

        public int EdgeCount => _edgeCount;
        public int[][] Graph => _graph;

        public DGraph(List<int>[] adjLists, int edgeCount)
        {
            _edgeCount = edgeCount;
            _graph = GetJaggedArrayGraph(adjLists);
        }

        public DGraph(int[][] adjLists, int edgeCount)
        {
            _edgeCount = edgeCount;
            _graph = adjLists;
        }

        public static void AddDistinctEdge(List<int>[] adjLists, int v, int w, ref int edgeCount)
        {
            if (adjLists[v]==null)
            {
                adjLists[v] = new List<int>() { w };
                return;
            }
            if (adjLists[v].Contains(w)) return;
            adjLists[v].Add(w);
            ++edgeCount;
        }

        public static int[][] GetJaggedArrayGraph(List<int>[] adjLists)
        {
            int[][] g = new int[adjLists.Length][];
            for (int i = 0, icnt = adjLists.Length; i < icnt; ++i)
            {
                var lst = adjLists[i];
                g[i] = lst==null ? Utils.EmptyArray<int>.Value :  lst.ToArray();
            }
            return g;
        }

        enum Color { White, Gray, Black }
        public static bool HasCycle(int[][] graph)
        {
            Color[] colors = new Color[graph.Length];
            Stack<int> stack = new Stack<int>(64);
            for (int i = 0, icnt = graph.Length; i < icnt; ++i)
            {
                if (colors[i] == Color.Black) continue;
                stack.Push(i);
                while (stack.Count > 0)
                {
                    int source = stack.Pop();
                    if (colors[source] == Color.Black) continue;
                    if (graph[source] == null) // just in case
                    {
                        colors[source] = Color.Black;
                        continue;
                    }
                    colors[source] = Color.Gray;
                    for (int j = 0, jcnt = graph[source].Length; j < jcnt; ++j)
                    {
                        int target = graph[source][j];
                        if (colors[target] == Color.Gray) return true;
                        if (colors[target] == Color.White)
                        {
                            colors[target] = Color.Gray;
                            stack.Push(target);
                        }
                    }
                }
                colors[i] = Color.Black;
            }
            return false;
        }

        public static bool Dump(BinaryWriter wr, DGraph graph, out string error)
        {
            return Dump(wr, graph.Graph, graph.EdgeCount, out error);
        }

        public static bool Dump(BinaryWriter wr, int[][] graph, int edgeCount, out string error)
        {
            error = null;
            try
            {
                wr.Write(graph.Length);
                wr.Write(edgeCount);
                for (int i = 0, cnt = graph.Length; i < cnt; ++i)
                {
                    var adj = graph[i];
                    wr.Write(adj.Length);
                    for (int j = 0, acnt = adj.Length; j < acnt; ++j)
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

        public static DGraph Load(BinaryReader rd, out string error)
        {
            error = null;
            try
            {
                var vcnt = rd.ReadInt32();
                var edgeCount = rd.ReadInt32();
                var edges = new int[vcnt][];
                for (int i = 0; i < vcnt; ++i)
                {
                    var lcount = rd.ReadInt32();
                    edges[i] = lcount == 0 ? Utils.EmptyArray<int>.Value : new int[lcount];
                    for (int j = 0; j < lcount; ++j)
                    {
                        var e = rd.ReadInt32();
                        edges[i][j]=e;
                    }
                }
                return new DGraph(edges,edgeCount);
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
        }
    }
}
