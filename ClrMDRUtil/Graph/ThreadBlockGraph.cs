using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Diagnostics;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
    public class ThreadBlockGraph
    {
        private DGraph _graph;
        public DGraph Graph => _graph;
        private int[][] _deadlock;
        public int[][] Deadlock => _deadlock;
        private int[] _graphMap;
        private int _graphThreadCount;

        public ThreadBlockGraph(int[][] graph, int edgeCount, int threadCnt, int[] map, int[][] deadlock)
        {
            _graph = new DGraph(graph, edgeCount);
            _graphThreadCount = threadCnt;
            _graphMap = map;
            _deadlock = deadlock;
        }

        public ThreadBlockGraph(DGraph graph, int threadCnt, int[] map, int[][] deadlock)
        {
            _graph = graph;
            _graphThreadCount = threadCnt;
            _graphMap = map;
            _deadlock = deadlock;
        }

        public bool HasDeadlock()
        {
            return _deadlock != null && _deadlock.Length > 0;
        }

        public bool IsThread(int graphNdx)
        {
            return graphNdx < _graphThreadCount;
        }

        public int GetThreadIndex(int graphNdx)
        {
            if (graphNdx < _graphThreadCount) return _graphMap[graphNdx];
            return Constants.InvalidIndex;
        }
        public int GetBlockIndex(int graphNdx)
        {
            Debug.Assert(graphNdx < _graphMap.Length);
            if (graphNdx >= _graphThreadCount) return _graphMap[graphNdx];
            return Constants.InvalidIndex;
        }

        public int GetIndex(int graphNdx)
        {
            return _graphMap[graphNdx];
        }

        public static ThreadBlockGraph BuildThreadBlockGraph(ClrThread[] threads, BlockingObject[] blocks, out string error)
        {
            error = null;
            try
            {
                var thrCmp = new ClrThreadCmp();
                var blkCmp = new BlockingObjectCmp();
                var ownerGraph = new List<KeyValuePair<int, List<int>>>(threads.Length);
                var waiterGraph = new Dictionary<int, List<int>>(threads.Length);
                var thSet = new HashSet<int>();
                var blkSet = new HashSet<int>();
                for (int i = 0, icnt = blocks.Length; i < icnt; ++i)
                {
                    var blk = blocks[i];
                    if (blk.Taken && blk.HasSingleOwner && blk.Owner != null)
                    {
                        var ndx = Array.BinarySearch(threads, blk.Owner, thrCmp);
                        if (ndx != Constants.InvalidIndex)
                        {
                            ownerGraph.Add(new KeyValuePair<int, List<int>>(i, new List<int>() { ndx }));
                            thSet.Add(ndx);
                            blkSet.Add(i);
                        }
                    }
                    else if (blk.Owners != null && blk.Owners.Count > 0)
                    {
                        List<int> owners = null;
                        foreach (ClrThread th in blk.Owners)
                        {
                            if (th != null)
                            {
                                var ndx = Array.BinarySearch(threads, th, thrCmp);
                                if (ndx != Constants.InvalidIndex)
                                {
                                    if (owners == null) owners = new List<int>();
                                    owners.Add(ndx);
                                    thSet.Add(ndx);
                                }

                            }
                        }
                        if (owners != null)
                        {
                            ownerGraph.Add(new KeyValuePair<int, List<int>>(i, owners));
                            blkSet.Add(i);
                        }
                    }

                    if (blk.Waiters != null && blk.Waiters.Count > 0)
                    {
                        //                    List<int> waiters = null;
                        bool hasWaiters = false;
                        foreach (ClrThread th in blk.Waiters)
                        {
                            if (th != null)
                            {
                                var ndx = Array.BinarySearch(threads, th, thrCmp);
                                if (ndx != Constants.InvalidIndex)
                                {
                                    List<int> blks;
                                    if (waiterGraph.TryGetValue(ndx, out blks))
                                    {
                                        if (!blks.Contains(i))
                                            blks.Add(i);
                                    }
                                    else
                                    {
                                        waiterGraph.Add(ndx, new List<int>() { i });
                                    }
                                    thSet.Add(ndx);
                                    hasWaiters = true;
                                }
                            }
                        }
                        if (hasWaiters)
                        {
                            blkSet.Add(i);
                        }
                    }
                }
                var graph = new int[thSet.Count + blkSet.Count][];
                // for circuit check
                for (int i = 0, icnt = graph.Length; i < icnt; ++i)
                {
                    graph[i] = Utils.EmptyArray<int>.Value;
                }

                var thary = thSet.ToArray();
                Array.Sort(thary);
                var blkary = blkSet.ToArray();
                Array.Sort(blkary);
                int edgeCount = 0;
                int graphThreadCnt = thary.Length;

                foreach (var kv in waiterGraph)
                {
                    int thGraphNdx = Array.BinarySearch(thary, kv.Key);
                    Debug.Assert(thGraphNdx >= 0);
                    var graphAry = new int[kv.Value.Count];
                    for (int i = 0, icnt = graphAry.Length; i < icnt; ++i)
                    {
                        int blkGraphNdx = Array.BinarySearch(blkary, kv.Value[i]);
                        Debug.Assert(blkGraphNdx >= 0);
                        graphAry[i] = blkGraphNdx + graphThreadCnt;
                    }
                    graph[thGraphNdx] = graphAry;
                    edgeCount += graphAry.Length;
                }

                int tharylen = thary.Length;
                for (int i = 0, icnt = ownerGraph.Count; i < icnt; ++i)
                {
                    int blkGraphNdx = Array.BinarySearch(blkary, ownerGraph[i].Key);
                    Debug.Assert(blkGraphNdx >= 0);
                    List<int> lst = ownerGraph[i].Value;
                    var graphAry = new int[lst.Count];
                    for (int j = 0, jcnt = graphAry.Length; j < jcnt; ++j)
                    {
                        int thGraphNdx = Array.BinarySearch(thary, lst[j]);
                        Debug.Assert(thGraphNdx >= 0);
                        graphAry[j] = thGraphNdx;
                    }
                    graph[blkGraphNdx + graphThreadCnt] = graphAry;
                    edgeCount += graphAry.Length;
                }

                var map = new int[thary.Length + blkary.Length];
                Array.Copy(thary, 0, map, 0, thary.Length);
                Array.Copy(blkary, 0, map, thary.Length, blkary.Length);


                int[][] deadlock = null;
                if (DGraph.HasCycle(graph))
                {
                    deadlock = Circuits.GetCycles(graph);
                }
                return new ThreadBlockGraph(graph, edgeCount, graphThreadCnt, map, deadlock);

            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
        }

        static public bool Dump(string path, ThreadBlockGraph tbgraph, out string error)
        {
            error = null;
            BinaryWriter bw = null;
            try
            {
                bw = new BinaryWriter(File.Open(path, FileMode.Create));

                DGraph.Dump(bw, tbgraph.Graph,out error);
                bw.Write(tbgraph._graphThreadCount);
                var map = tbgraph._graphMap;
                if (map != null)
                {
                    bw.Write(map.Length);
                    for (int i = 0, icnt = map.Length; i < icnt; ++i)
                    {
                        bw.Write(map[i]);
                    }
                }
                else
                {
                    bw.Write((int)0);
                }
                var deadlock = tbgraph.Deadlock;
                if (deadlock != null && deadlock.Length > 0)
                {
                    bw.Write(deadlock.Length);
                    for (int i = 0, icnt = deadlock.Length; i < icnt; ++i)
                    {
                        var cycle = deadlock[i];
                        bw.Write(cycle.Length);
                        for(int j = 0, jcnt = cycle.Length; j < jcnt; ++j)
                        {
                            bw.Write(cycle[j]);
                        }
                    }
                }
                else
                {
                    bw.Write((int)0);
                }

                return true;
            }
            catch(Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return false;
            }
            finally
            {
                bw?.Close();
            }
        }

        static public ThreadBlockGraph Load(string path, out string error)
        {
            error = null;
            BinaryReader bw = null;
            try
            {
                bw = new BinaryReader(File.Open(path, FileMode.Open,FileAccess.Read));

                DGraph dgraph = DGraph.Load(bw, out error);
                int graphThreadCount = bw.ReadInt32();
                int mapCnt = bw.ReadInt32();
                int[] map = null;
                if (mapCnt > 0)
                {
                    map = new int[mapCnt];
                    for (int i = 0, icnt = map.Length; i < icnt; ++i)
                    {
                        map [i] = bw.ReadInt32();
                    }
                }
                int[][] deadlock = null;
                int deadlockCnt = bw.ReadInt32();
                if (deadlockCnt > 0)
                {
                    deadlock = new int[deadlockCnt][];
                    for (int i = 0; i < deadlockCnt; ++i)
                    {
                        int cycleLen = bw.ReadInt32();
                        int[] cycle = new int[cycleLen];
                        for (int j = 0; j < cycleLen; ++j)
                        {
                            cycle[j] = bw.ReadInt32();
                        }
                        deadlock[i] = cycle;
                    }
                }

                return new ThreadBlockGraph(dgraph, graphThreadCount, map, deadlock);
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
            finally
            {
                bw?.Close();
            }
        }

        /// <summary>
        /// Just for testing.
        /// </summary>
        static public void Dump(StreamWriter sw, ThreadBlockGraph tbgraph)
        {
            sw.WriteLine("ThreadCount: " + tbgraph._graphThreadCount);
            DGraph.Dump(sw, tbgraph._graph);
            if (tbgraph._deadlock != null)
            {
                sw.WriteLine("Deadlock");
                for(int i = 0, icnt = tbgraph._deadlock.Length; i < icnt; ++i)
                {
                    sw.Write(Utils.CountStringHeader(i));
                    var dary = tbgraph._deadlock[i];
                    for (int j = 0, jcnt = dary.Length; j < jcnt; ++j)
                    {
                        sw.Write(dary[j] + " ");
                    }
                    sw.WriteLine();
                }
            }
            sw.WriteLine("Graph Map");
            for (int i = 0, icnt = tbgraph._graphMap.Length; i < icnt; ++i)
            {
                sw.WriteLine(Utils.CountStringHeader(i) + tbgraph._graphMap[i]);
            }
        }
    }
}
