using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
    public class BGL
    {
        //public static void TestCycles()
        //{
        //    List<int>[] adjLst = new List<int>[14];
        //    adjLst[0] = new List<int>() { 1 };
        //    adjLst[1] = new List<int>() { 2 };
        //    adjLst[2] = new List<int>() { 3 };
        //    adjLst[3] = new List<int>() { 4, 5 };
        //    adjLst[4] = new List<int>() { 2, 6 };
        //    adjLst[5] = new List<int>();
        //    adjLst[6] = new List<int>() { 7, 9 };
        //    adjLst[7] = new List<int>() { 9 };
        //    adjLst[8] = new List<int>() { 7 };
        //    adjLst[9] = new List<int>() { 2, 8 };
        //    adjLst[10] = new List<int>() { 11 };
        //    adjLst[11] = new List<int>() { 10, 12 };
        //    adjLst[12] = new List<int>() { 13 };
        //    adjLst[13] = new List<int>() { 12 };

        //    CppUtils.GraphHelper graph = new CppUtils.GraphHelper();
        //    graph.Init(adjLst.Length, adjLst);
        //    bool res = graph.GetCycles();

        //}

        //public static int[][] GetCycles(List<int>[] adjLst, out string error)
        //{
        //    error = null;
        //    try
        //    {
        //        CppUtils.GraphHelper graph = new CppUtils.GraphHelper();
        //        graph.Init(adjLst.Length, adjLst);
        //        if (graph.GetCycles() && graph.HasCycles())
        //        {
        //            return graph.GetCycleArrays();
        //        }
        //        return Utils.EmptyArray<int[]>.Value;
        //    }
        //    catch (Exception ex)
        //    {
        //        error = Utils.GetExceptionErrorString(ex);
        //        return null;
        //    }
        //}
    }
}