
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
    //public class Cycle
    //{
    //    enum Color { White, Gray, Black }
    //    public static bool HaveCycle(int[][] graph)
    //    {
    //        Color[] colors = new Color[graph.Length];
    //        Stack<int> stack = new Stack<int>(64);
    //        for(int i = 0, icnt = graph.Length; i < icnt;  ++i)
    //        {
    //            if (colors[i] == Color.Black) continue;
    //            stack.Push(i);
    //            while(stack.Count > 0)
    //            {
    //                int source = stack.Pop();
    //                if (colors[source] == Color.Black) continue;
    //                if (graph[source] == null) // just in case
    //                {
    //                    colors[source] = Color.Black;
    //                    continue;
    //                }
    //                colors[source] = Color.Gray;
    //                for (int j = 0, jcnt = graph[source].Length; j < jcnt; ++j)
    //                {
    //                    int target = graph[source][j];
    //                    if (colors[target] == Color.Gray) return true;
    //                    if (colors[target] == Color.White)
    //                    {
    //                        colors[target] = Color.Gray;
    //                        stack.Push(target);
    //                    }
    //                }
    //            }
    //            colors[i] = Color.Black;
    //        }
    //        return false;
    //    }
    //}
}
