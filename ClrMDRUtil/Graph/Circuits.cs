/*
 * Enumerating Circuits and Loops in Graphs with Self-Arcs and Multiple-Arcs
 * K.A. Hawick and H.A. James
 * Computer Science, Institute for Information and Mathematical Sciences,
 * Massey University, North Shore 102-904, Auckland, New Zealand
 * k.a.hawick@massey.ac.nz; heath.james@sapac.edu.au
 * Tel: +64 9 414 0800
 * Fax: +64 9 441 8181
 * Technical Report CSTN-013
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
    public class Circuits
    {
        int[][] _graph;
        bool[] _blocked;
        List<int>[] _B;
        Stack<int> _stack;
        List<int[]> _cycles;
        int _start;


        public Circuits(int[][] g)
        {
            _graph = g;
            _blocked = new bool[g.Length];
            _B = new List<int>[g.Length];
            _cycles = new List<int[]>();
            _stack = new Stack<int>();
            _start = 0;
        }

        public Circuits(IList<int>[] adjLsts)
        {
            _graph = new int[adjLsts.Length][];
            for(int i = 0, icnt = adjLsts.Length; i < icnt; ++i)
            {
                var adjLst = adjLsts[i];
                _graph[i] = adjLst == null ? Utils.EmptyArray<int>.Value : adjLst.ToArray();
            }
            _blocked = new bool[adjLsts.Length];
            _B = new List<int>[adjLsts.Length];
            _cycles = new List<int[]>();
            _stack = new Stack<int>();
            _start = 0;
        }

        bool NotInList(IList<int> lst, int val)
        {
            return lst==null || !lst.Contains(val);
        }
        bool InList(IList<int> lst, int val)
        {
            return lst!=null && lst.Contains(val);
        }

        void EmptyList(IList<int> lst)
        {
            lst?.Clear();
        }

        void AddToList(IList<int> lst, int val)
        {
            Debug.Assert(lst != null);
            lst.Add(val);
        }

        int RemoveFromList(IList<int> lst, int val)
        {
            if (lst == null) return 0;
            int rmvCount = 0;
            int lstCnt = lst.Count;
            for(int i = 0; i < lstCnt; ++i)
            {
                if (lst[i] == val)
                {
                    ++rmvCount;
                    --lstCnt;
                    for (int j = i; j < lstCnt; ++j)
                    {
                        lst[j] = lst[j + 1];
                    }
                    --i;
                }
            }
            return rmvCount;
        }

        int CountArcs()
        {
            int cnt = 0;
            for (int i = 0, icnt = _graph.Length; i < icnt; ++i)
            {
                cnt += _graph[i] == null ? 0 : _graph[i].Length;
            }
            return cnt;
        }

        void Unblock(int u)
        {
            _blocked[u] = false;
            if (_B[u] != null)
            {
                for (int wPos = 1; wPos < _B[u].Count; ++wPos)
                {
                    // for each w in B[u]
                    int w = _B[u][wPos];
                    wPos -= RemoveFromList(_B[u], w);
                    if (_blocked[w])
                        Unblock(w);
                }
            }
        }

        void SaveCycle()
        {
            int[] cycle = new int[_stack.Count+1];
            int ndx = _stack.Count-1;
            foreach (var v in _stack)
            {
                cycle[ndx--] = v;
            }
            cycle[_stack.Count] = _stack.Last();  // add closing vertex
           _cycles.Add(cycle);
        }

        void Reset()
        {
            for (int i = 0, icnt = _graph.Length; i < icnt; ++i)
            {
                _blocked[i] = false;
                EmptyList(_B[i]);
            }
        }

        bool circuit(int v)
        { // based on Johnson ’s logical procedure CIRCUIT
            bool f = false;
            _stack.Push(v);
            _blocked[v] = true;
            for (int wPos = 0; wPos < _graph[v].Length; ++wPos)
            { // for each w in list Ak[v]:
                int w = _graph[v][wPos];
                if (w < _start) continue; // ignore relevant parts of Ak
                if (w == _start)
                { // we have a circuit,
                    //if (enumeration)
                    {
                        SaveCycle(); // print out the stack to record the circuit
                    }
                    Debug.Assert(_stack.Count <= _graph.Length);
                    f = true;
                }
                else if (!_blocked[w])
                {
                    if (circuit(w)) f = true;
                }
            }
            if (f)
            {
                Unblock(v);
            }
            else
            {
                for (int wPos = 0; wPos < _graph[v].Length; ++wPos)
                { // for each w in list Ak[v]:
                    int w = _graph[v][wPos];
                    if (w < _start) continue;  // ignore relevant parts of Ak
                    if (NotInList(_B[w], v))
                    {
                        if (_B[w] == null) _B[w] = new List<int>();
                        AddToList(_B[w], v);
                    }
                }
            }
            v = _stack.Pop();
            return f;
        }

        public static int[][] GetCycles(int[][] graph)
        {
            Circuits cr = new Circuits(graph);
            int vertexCount = graph.Length;
            while(cr._start < vertexCount)
            {
                try
                {
                    cr.Reset();
                    cr.circuit(cr._start);
                    cr._start = cr._start + 1;
                }
                catch(Exception ex)
                {
                    int a = 1;
                }
            }
            return cr._cycles.ToArray();
        }
    }
}
