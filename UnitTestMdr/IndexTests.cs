using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClrMDRIndex;
using ClrMDRUtil.Utils;
using Microsoft.Diagnostics.Runtime;
using DmpNdxQueries;


namespace UnitTestMdr
{
	[TestClass]
	public class IndexTests
	{

        string[] dumps = new string[]
        {
            /*  0 */    @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.dmp",
            /*  1 */    @"D:\Jerzy\WinDbgStuff\dumps\TradingService\Tortoise\tradingservice_0615.dmp",
            /*  2 */    @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Baly\analytics7_1510301630.Baly.dmp",
            /*  3 */    @"D:\Jerzy\WinDbgStuff\dumps\Analytics\BigOne\Analytics11_042015_2.BigOne.dmp"
        };

        //		string[] dumps = new string[]
        //{
        //            /*  0 */    @"C:\WinDbgStuff\Dumps\Analytics\Highline\analyticsdump111.dlk.dmp",
        //            /*  1 */    @"D:\Jerzy\WinDbgStuff\dumps\TradingService\Tortoise\tradingservice_0615.dmp",
        //            /*  2 */    @"C:\WinDbgStuff\Dumps\Analytics\Baly\AnalyticsLatencyDump06062016 03354291.dmp",
        //            /*  3 */    @"C:\WinDbgStuff\Dumps\Analytics\BigOne\Analytics11_042015_2.Big.dmp"
        //};
        #region test context/initialization

        private TestContext testContextInstance;

		/// <summary>
		///Gets or sets the test context which provides
		///information about and functionality for the current test run.
		///</summary>
		public TestContext TestContext
		{
			get { return testContextInstance; }
			set { testContextInstance = value; }
		}

		[AssemblyInitialize]
		public static void AssemblyInit(TestContext context)
		{
			Assert.IsTrue(TestConfiguration.ConfigureSettings());
		}

		#endregion test context/initialization

		#region threads

		[TestMethod]
		public void ThreadInformation()
		{
			string error = null;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			var index = OpenIndex();
			TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

			using (index)
			{
				stopWatch.Restart();
				var threads = DumpIndexer.GetThreads(index.Runtime);
				TestContext.WriteLine(index.DumpFileName + " THREAD COUNT: " + Utils.LargeNumberString(threads.Length) +
				                      ", GETTING THREAD LIST DURATION: " + Utils.StopAndGetDurationString(stopWatch));
				stopWatch.Restart();
				var heap = index.Dump.Heap;
				var blocks = DumpIndexer.GetBlockingObjects(heap);
				TestContext.WriteLine(index.DumpFileName + " BLOCKING OBJECT COUNT: " + Utils.LargeNumberString(blocks.Length) +
				                      ", GETTING BLOCKING OBJECTS DURATION: " + Utils.StopAndGetDurationString(stopWatch));

				var blksSingleOwned = new List<BlockingObject>();
				var blksOwnedByMany = new List<BlockingObject>();
				var blksWithWaiters = new List<BlockingObject>();

				var blksActive = new List<BlockingObject>();

				for (int i = 0, icnt = blocks.Length; i < icnt; ++i)
				{
					var blk = blocks[i];
					if (blk.Taken && blk.HasSingleOwner)
					{
						blksSingleOwned.Add(blk);
					}
					if (blk.Owners != null && blk.Owners.Count > 0)
					{
						blksOwnedByMany.Add(blk);
					}
					if (blk.Waiters != null && blk.Waiters.Count > 0)
					{
						blksWithWaiters.Add(blk);
					}
					if ((blk.Taken && blk.HasSingleOwner)
					    || (blk.Owners != null && blk.Owners.Count > 0)
					    || (blk.Waiters != null && blk.Waiters.Count > 0))
						blksActive.Add(blk);
				}


				var blksIntersection = blksWithWaiters.Intersect(blksOwnedByMany, new BlockingObjectEqualityCmp()).ToArray();
				blksIntersection = blksIntersection.Intersect(blksWithWaiters, new BlockingObjectEqualityCmp()).ToArray();

				var blksUnion = blksWithWaiters.Union(blksOwnedByMany, new BlockingObjectEqualityCmp()).ToArray();
				blksUnion = blksUnion.Union(blksWithWaiters, new BlockingObjectEqualityCmp()).ToArray();
			}

			Assert.IsNull(error, error);
		}

		[TestMethod]
		public void ThreadBlockingThreadGraphInfo()
		{
			string error = null;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			var index = OpenIndex();
			TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

			using (index)
			{
				stopWatch.Restart();
				var heap = index.Dump.Heap;
				var result = DmpNdxQueries.FQry.heapWarmup(heap);
				Assert.IsNull(result.Item1);
				TestContext.WriteLine("NULL OBJECT COUNT: " + Utils.LargeNumberString(result.Item2.Length)
				                      + " OBJECT COUNT: " + Utils.LargeNumberString(result.Item3)
				                      + ", HEAP WARMUP DURATION: " + Utils.StopAndGetDurationString(stopWatch));

				stopWatch.Restart();
				var threads = DumpIndexer.GetThreads(index.Runtime);
				TestContext.WriteLine("THREAD COUNT: " + Utils.LargeNumberString(threads.Length) +
				                      ", GETTING THREAD LIST DURATION: " + Utils.StopAndGetDurationString(stopWatch));
				stopWatch.Restart();
				var blocks = DumpIndexer.GetBlockingObjects(heap);
				TestContext.WriteLine("BLOCKING OBJECT COUNT: " + Utils.LargeNumberString(blocks.Length) +
				                      ", GETTING BLOCKING OBJECTS DURATION: " + Utils.StopAndGetDurationString(stopWatch));

				var threadSet = new HashSet<ClrThread>(new ClrThreadEqualityCmp());
				var blkGraph = new List<Tuple<BlockingObject, ClrThread[], ClrThread[]>>();
				var owners = new List<ClrThread>();
				var waiters = new List<ClrThread>();
				int nullOwnerCount = 0;
				int nullOwnersCount = 0;
				int nullWaitersCount = 0;
				for (int i = 0, icnt = blocks.Length; i < icnt; ++i)
				{
					var blk = blocks[i];
					owners.Clear();
					waiters.Clear();
					ClrThread owner = null;
					if (blk.Taken && blk.HasSingleOwner)
					{
						owner = blk.Owner;
						if (owner != null)
						{
							threadSet.Add(owner);
							owners.Add(owner);
						}
						else
						{
							++nullOwnerCount;
						}
					}

					if (blk.Owners != null && blk.Owners.Count > 0)
					{
						for (int j = 0, jcnt = blk.Owners.Count; j < jcnt; ++j)
						{
							var th = blk.Owners[j];
							if (th == null)
							{
								++nullOwnersCount;
								continue;
							}
							threadSet.Add(th);
							if (owner == null || owner.OSThreadId != th.OSThreadId)
								owners.Add(th);
						}
					}

					if (blk.Waiters != null && blk.Waiters.Count > 0)
					{
						for (int j = 0, jcnt = blk.Waiters.Count; j < jcnt; ++j)
						{
							var th = blk.Waiters[j];
							if (th == null)
							{
								++nullWaitersCount;
								continue;
							}
							threadSet.Add(th);
							waiters.Add(th);
						}
					}

					if (owners.Count > 0)
					{
						var ownerAry = owners.ToArray();
						var waiterAry = waiters.ToArray();
						blkGraph.Add(new Tuple<BlockingObject, ClrThread[], ClrThread[]>(blk, ownerAry, waiterAry));
					}
					else if (waiters.Count > 0)
					{
						blkGraph.Add(new Tuple<BlockingObject, ClrThread[], ClrThread[]>(blk, Utils.EmptyArray<ClrThread>.Value,
							waiters.ToArray()));
					}
				}

				int nullThreadBlkObjCount = 0;
				int notFoundThreadBlkObjCount = 0;
				int threadBlkObjCount = 0;
				var cmp = new BlockingObjectCmp();
				for (int i = 0, icnt = threads.Length; i < icnt; ++i)
				{
					var th = threads[i];
					if (th.BlockingObjects != null && th.BlockingObjects.Count > 0)
					{
						for (int j = 0, jcnt = th.BlockingObjects.Count; j < jcnt; ++j)
						{
							var blk = th.BlockingObjects[j];
							if (blk == null)
							{
								++nullThreadBlkObjCount;
								continue;
							}
							var ndx = Array.BinarySearch(blocks, blk, cmp);
							if (ndx < 0)
							{
								++notFoundThreadBlkObjCount;
								continue;
							}
							++threadBlkObjCount;
						}
					}
				}

				TestContext.WriteLine("ACTIVE BLOCKING OBJECT COUNT: " + Utils.LargeNumberString(blkGraph.Count));
				TestContext.WriteLine("ACTIVE THREADS COUNT: " + Utils.LargeNumberString(threadSet.Count));

				TestContext.WriteLine("NULL OWNER COUNT COUNT: " + Utils.LargeNumberString(nullOwnerCount));
				TestContext.WriteLine("NULL OWNERS COUNT COUNT: " + Utils.LargeNumberString(nullOwnersCount));
				TestContext.WriteLine("NULL WAITERS COUNT COUNT: " + Utils.LargeNumberString(nullWaitersCount));

				TestContext.WriteLine("NULL THREAD BLKOBJ COUNT: " + Utils.LargeNumberString(nullThreadBlkObjCount));
				TestContext.WriteLine("NOT FOUND THREAD BLKOBJ COUNT: " + Utils.LargeNumberString(notFoundThreadBlkObjCount));
				TestContext.WriteLine("THREAD BLKOBJ COUNT: " + Utils.LargeNumberString(threadBlkObjCount));

				Assert.IsTrue(true);
			}

			Assert.IsNull(error, error);
		}

		[TestMethod]
		public void BuildBlockThreadGraph()
		{
			string error = null;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			var index = OpenIndex();
			TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

			using (index)
			{
				stopWatch.Restart();
				var heap = index.Dump.Heap;
				var threads = DumpIndexer.GetThreads(index.Runtime);
				TestContext.WriteLine("THREAD COUNT: " + Utils.LargeNumberString(threads.Length) +
				                      ", GETTING THREAD LIST DURATION: " + Utils.StopAndGetDurationString(stopWatch));
				stopWatch.Restart();
				var blocks = DumpIndexer.GetBlockingObjects(heap);
				TestContext.WriteLine("BLOCKING OBJECT COUNT: " + Utils.LargeNumberString(blocks.Length) +
				                      ", GETTING BLOCKING OBJECTS DURATION: " + Utils.StopAndGetDurationString(stopWatch));

				var threadSet = new HashSet<ClrThread>(new ClrThreadEqualityCmp());
				var blkGraph = new List<Tuple<BlockingObject, ClrThread[], ClrThread[]>>();
				var allBlkList = new List<BlockingObject>();
				var owners = new List<ClrThread>();
				var waiters = new List<ClrThread>();
				int nullOwnerCount = 0;
				int nullOwnersCount = 0;
				int nullWaitersCount = 0;
				for (int i = 0, icnt = blocks.Length; i < icnt; ++i)
				{
					var blk = blocks[i];
					owners.Clear();
					waiters.Clear();
					ClrThread owner = null;
					if (blk.Taken && blk.HasSingleOwner)
					{
						owner = blk.Owner;
						if (owner != null)
						{
							threadSet.Add(owner);
							owners.Add(owner);
						}
						else
						{
							++nullOwnerCount;
						}
					}

					if (blk.Owners != null && blk.Owners.Count > 0)
					{
						for (int j = 0, jcnt = blk.Owners.Count; j < jcnt; ++j)
						{
							var th = blk.Owners[j];
							if (th == null)
							{
								++nullOwnersCount;
								continue;
							}
							threadSet.Add(th);
							if (owner == null || owner.OSThreadId != th.OSThreadId)
								owners.Add(th);
						}
					}

					if (blk.Waiters != null && blk.Waiters.Count > 0)
					{
						for (int j = 0, jcnt = blk.Waiters.Count; j < jcnt; ++j)
						{
							var th = blk.Waiters[j];
							if (th == null)
							{
								++nullWaitersCount;
								continue;
							}
							threadSet.Add(th);
							waiters.Add(th);
						}
					}

					if (owners.Count > 0)
					{
						var ownerAry = owners.ToArray();
						var waiterAry = waiters.ToArray();
						blkGraph.Add(new Tuple<BlockingObject, ClrThread[], ClrThread[]>(blk, ownerAry, waiterAry));
						allBlkList.Add(blk);
					}
					else if (waiters.Count > 0)
					{
						blkGraph.Add(new Tuple<BlockingObject, ClrThread[], ClrThread[]>(blk, Utils.EmptyArray<ClrThread>.Value,
							waiters.ToArray()));
						allBlkList.Add(blk);
					}
				}

				int threadBlkObjCount = 0;
				List<KeyValuePair<ClrThread, BlockingObject[]>> threadBlocks = new List<KeyValuePair<ClrThread, BlockingObject[]>>();
				var cmp = new BlockingObjectCmp();
				var blkList = new List<BlockingObject>();
				var thrBlkList = new List<BlockingObject>();
				var thrList = new List<ClrThread>();
				for (int i = 0, icnt = threads.Length; i < icnt; ++i)
				{
					var th = threads[i];
					if (th.BlockingObjects != null && th.BlockingObjects.Count > 0)
					{
						blkList.Clear();
						for (int j = 0, jcnt = th.BlockingObjects.Count; j < jcnt; ++j)
						{
							var blk = th.BlockingObjects[j];
							if (blk != null)
							{
								thrBlkList.Add(blk);
								blkList.Add(blk);
								++threadBlkObjCount;
							}
						}
						if (blkList.Count > 0)
						{
							threadBlocks.Add(new KeyValuePair<ClrThread, BlockingObject[]>(th, blkList.ToArray()));
							thrList.Add(th);
						}
					}
				}

				var thrCmp = new ClrThreadCmp();
				var blkCmp = new BlockingObjectCmp();
				// blocks and threads found in blocking objects
				//
				var blkThreadAry = threadSet.ToArray();
				Array.Sort(blkThreadAry, thrCmp);
				var blkBlockAry = allBlkList.ToArray();
				Array.Sort(blkBlockAry, blkCmp);
				var threadBlocksAry = blkGraph.ToArray();
				Array.Sort(threadBlocksAry, new BlkObjInfoCmp());

				// threads and blocks found in thread objects
				//
				var thrThreadAry = thrList.ToArray();
				Array.Sort(thrThreadAry, thrCmp);
				var thrBlockAry = thrBlkList.ToArray();
				Array.Sort(thrBlockAry, blkCmp);

				int blkThreadCount = blkThreadAry.Length;
				Digraph graph = new Digraph(blkThreadCount + blkBlockAry.Length);

				for (int i = 0, cnt = threadBlocksAry.Length; i < cnt; ++i)
				{
					var blkInfo = threadBlocksAry[i];
					for (int j = 0, tcnt = blkInfo.Item2.Length; j < tcnt; ++j)
					{
						var ndx = Array.BinarySearch(blkThreadAry, blkInfo.Item2[j], thrCmp);
						Debug.Assert(ndx >= 0);
						graph.AddDistinctEdge(blkThreadCount + i, ndx);
					}
					for (int j = 0, tcnt = blkInfo.Item3.Length; j < tcnt; ++j)
					{
						var ndx = Array.BinarySearch(blkThreadAry, blkInfo.Item3[j], thrCmp);
						Debug.Assert(ndx >= 0);
						graph.AddDistinctEdge(ndx, blkThreadCount + i);
					}
				}

				var cycle = new DirectedCycle(graph);
				var cycles = cycle.GetCycle();


				TestContext.WriteLine("ACTIVE BLOCKING OBJECT COUNT: " + Utils.LargeNumberString(blkGraph.Count));
				TestContext.WriteLine("ACTIVE THREADS COUNT: " + Utils.LargeNumberString(threadSet.Count));

				TestContext.WriteLine("NULL OWNER COUNT COUNT: " + Utils.LargeNumberString(nullOwnerCount));
				TestContext.WriteLine("NULL OWNERS COUNT COUNT: " + Utils.LargeNumberString(nullOwnersCount));
				TestContext.WriteLine("NULL WAITERS COUNT COUNT: " + Utils.LargeNumberString(nullWaitersCount));

				TestContext.WriteLine("THREAD BLKOBJ COUNT: " + Utils.LargeNumberString(threadBlkObjCount));

				Assert.IsTrue(true);
			}

			Assert.IsNull(error, error);
		}

		[TestMethod]
		public void SaveBlockThreadInfo()
		{
			string error = null;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			var index = OpenIndex();
			TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

			using (index)
			{
				stopWatch.Restart();
				var heap = index.Dump.Heap;
				var threads = DumpIndexer.GetThreads(index.Runtime);
				TestContext.WriteLine("THREAD COUNT: " + Utils.LargeNumberString(threads.Length) +
				                      ", GETTING THREAD LIST DURATION: " + Utils.StopAndGetDurationString(stopWatch));
				stopWatch.Restart();
				var blocks = DumpIndexer.GetBlockingObjects(heap);
				TestContext.WriteLine("BLOCKING OBJECT COUNT: " + Utils.LargeNumberString(blocks.Length) +
				                      ", GETTING BLOCKING OBJECTS DURATION: " + Utils.StopAndGetDurationString(stopWatch));

				var threadSet = new HashSet<ClrThread>(new ClrThreadEqualityCmp());
				var blkGraph = new List<Tuple<BlockingObject, ClrThread[], ClrThread[]>>();
				var allBlkList = new List<BlockingObject>();
				var owners = new List<ClrThread>();
				var waiters = new List<ClrThread>();
				for (int i = 0, icnt = blocks.Length; i < icnt; ++i)
				{
					var blk = blocks[i];
					owners.Clear();
					waiters.Clear();
					ClrThread owner = null;
					if (blk.Taken && blk.HasSingleOwner)
					{
						owner = blk.Owner;
						if (owner != null)
						{
							threadSet.Add(owner);
							owners.Add(owner);
						}
					}

					if (blk.Owners != null && blk.Owners.Count > 0)
					{
						for (int j = 0, jcnt = blk.Owners.Count; j < jcnt; ++j)
						{
							var th = blk.Owners[j];
							if (th != null)
							{
								threadSet.Add(th);
								if (owner == null || owner.OSThreadId != th.OSThreadId)
									owners.Add(th);
							}
						}
					}

					if (blk.Waiters != null && blk.Waiters.Count > 0)
					{
						for (int j = 0, jcnt = blk.Waiters.Count; j < jcnt; ++j)
						{
							var th = blk.Waiters[j];
							if (th != null)
							{
								threadSet.Add(th);
								waiters.Add(th);
							}
						}
					}

					if (owners.Count > 0)
					{
						var ownerAry = owners.ToArray();
						var waiterAry = waiters.ToArray();
						blkGraph.Add(new Tuple<BlockingObject, ClrThread[], ClrThread[]>(blk, ownerAry, waiterAry));
						allBlkList.Add(blk);
					}
					else if (waiters.Count > 0)
					{
						blkGraph.Add(new Tuple<BlockingObject, ClrThread[], ClrThread[]>(blk, Utils.EmptyArray<ClrThread>.Value,
							waiters.ToArray()));
						allBlkList.Add(blk);
					}
				}

				var thrCmp = new ClrThreadCmp();
				var blkCmp = new BlockingObjectCmp();
				var blkInfoCmp = new BlkObjInfoCmp();

				// blocks and threads found in blocking objects
				//
				var blkThreadAry = threadSet.ToArray();
				Array.Sort(blkThreadAry, thrCmp);
				var blkBlockAry = allBlkList.ToArray();
				Array.Sort(blkBlockAry, blkCmp);
				var threadBlocksAry = blkGraph.ToArray();
				Array.Sort(threadBlocksAry, blkInfoCmp);

				// create maps
				//
				int[] blkMap = new int[blkBlockAry.Length];
				int[] thrMap = new int[blkThreadAry.Length];
				for (int i = 0, icnt = blkMap.Length; i < icnt; ++i)
				{
					blkMap[i] = Array.BinarySearch(blocks, blkBlockAry[i], blkCmp);
					Assert.IsTrue(blkMap[i] >= 0);
				}

				for (int i = 0, icnt = thrMap.Length; i < icnt; ++i)
				{
					thrMap[i] = Array.BinarySearch(threads, blkThreadAry[i], thrCmp);
					Assert.IsTrue(thrMap[i] >= 0);
				}

				int blkThreadCount = blkThreadAry.Length;
				Digraph graph = new Digraph(blkThreadCount + blkBlockAry.Length);

				for (int i = 0, cnt = threadBlocksAry.Length; i < cnt; ++i)
				{
					var blkInfo = threadBlocksAry[i];
					for (int j = 0, tcnt = blkInfo.Item2.Length; j < tcnt; ++j)
					{
						var ndx = Array.BinarySearch(blkThreadAry, blkInfo.Item2[j], thrCmp);
						Debug.Assert(ndx >= 0);
						graph.AddDistinctEdge(blkThreadCount + i, ndx);
					}
					for (int j = 0, tcnt = blkInfo.Item3.Length; j < tcnt; ++j)
					{
						var ndx = Array.BinarySearch(blkThreadAry, blkInfo.Item3[j], thrCmp);
						Debug.Assert(ndx >= 0);
						graph.AddDistinctEdge(ndx, blkThreadCount + i);
					}
				}

				var cycle = new DirectedCycle(graph);
				var cycles = cycle.GetCycle();
				BinaryWriter bw = null;
				try
				{
					var path = index.GetFilePath(0, Constants.MapThreadsAndBlocksGraphFilePostfix);
					bw = new BinaryWriter(File.Open(path, FileMode.Create));
					bw.Write(cycles.Length);
					for (int i = 0, icnt = cycles.Length; i < icnt; ++i)
					{
						bw.Write(cycles[i]);
					}
					bw.Write(thrMap.Length);
					for (int i = 0, icnt = thrMap.Length; i < icnt; ++i)
					{
						bw.Write(thrMap[i]);
					}
					bw.Write(blkMap.Length);
					for (int i = 0, icnt = blkMap.Length; i < icnt; ++i)
					{
						bw.Write(blkMap[i]);
					}
					graph.Dump(bw, out error);
					Assert.IsNull(error, error);
				}
				catch (Exception ex)
				{
					Assert.IsTrue(false, ex.ToString());
				}
				finally
				{
					bw?.Close();
					bw = null;
				}

				// save threads and blocks
				//
				try
				{
					var path = index.GetFilePath(0, Constants.MapThreadsAndBlocksFilePostfix);
					bw = new BinaryWriter(File.Open(path, FileMode.Create));
					bw.Write(threads.Length);
					for (int i = 0, icnt = threads.Length; i < icnt; ++i)
					{
						var clrtThread = new ClrtThread(threads[i], blocks, blkCmp);
						Assert.IsNotNull(clrtThread);
						clrtThread.Dump(bw);
					}
					bw.Write(blocks.Length);
					for (int i = 0, icnt = blocks.Length; i < icnt; ++i)
					{
						var blk = blocks[i];
						var ndx = Array.BinarySearch(blkBlockAry, blk, blkCmp);
						var typeId = index.GetTypeId(blk.Object);
						var clrtBlock = new ClrtBlkObject(blk, ndx, typeId);
						Assert.IsNotNull(clrtBlock);
						clrtBlock.Dump(bw);
					}
				}
				catch (Exception ex)
				{
					Assert.IsTrue(false, ex.ToString());
				}
				finally
				{
					bw?.Close();
					bw = null;
				}



				TestContext.WriteLine("ACTIVE BLOCKING OBJECT COUNT: " + Utils.LargeNumberString(blkGraph.Count));
				TestContext.WriteLine("ACTIVE THREADS COUNT: " + Utils.LargeNumberString(threadSet.Count));



				Assert.IsTrue(true);
			}

			Assert.IsNull(error, error);
		}

		public class BlkObjInfoCmp : IComparer<Tuple<BlockingObject, ClrThread[], ClrThread[]>>
		{
			public int Compare(Tuple<BlockingObject, ClrThread[], ClrThread[]> a,
				Tuple<BlockingObject, ClrThread[], ClrThread[]> b)
			{
				return a.Item1.Object < b.Item1.Object ? -1 : (a.Item1.Object > b.Item1.Object ? 1 : 0);
			}
		}


		[TestMethod]
		public void TestIndexThreadInfo()
		{
			const string indexPath = @"C:\WinDbgStuff\Dumps\Analytics\AnalyticsMemory\A2_noDF.dmp.map";
			string error = null;
			StreamWriter sw = null;
			var rootEqCmp = new ClrRootEqualityComparer();
			var rootCmp = new ClrRootObjCmp();

			using (var index = OpenIndex(indexPath))
			{
				try
				{

					var runtime = index.Runtime;
					var heap = runtime.Heap;
					var stackTraceLst = new List<ClrStackFrame>();
					var threads = DumpIndexer.GetThreads(runtime);
					var threadLocalDeadVars = new ClrRoot[threads.Length][];
					var threadLocalAliveVars = new ClrRoot[threads.Length][];
					var threadFrames = new ClrStackFrame[threads.Length][];
					var frames = new SortedDictionary<string, int>();
					var stackObjects = new SortedDictionary<string, int>();
					var totalStaclObjectsCount = 0;
					for (int i = 0, icnt = threads.Length; i < icnt; ++i)
					{
						var t = threads[i];
						stackTraceLst.Clear();
						foreach (var st in t.EnumerateStackTrace())
						{
							stackTraceLst.Add(st);
							if (stackTraceLst.Count > 100) break;
						}
						threadLocalAliveVars[i] = t.EnumerateStackObjects(false).ToArray();
						var all = t.EnumerateStackObjects(true).ToArray();
						threadLocalDeadVars[i] = all.Except(threadLocalAliveVars[i], rootEqCmp).ToArray();
						threadFrames[i] = stackTraceLst.ToArray();
					}

					var path = index.AdhocFolder + Path.DirectorySeparatorChar + "TestIndexThreadsInfo.txt";
					sw = new StreamWriter(path);
					HashSet<ulong> localsAlive = new HashSet<ulong>();
					HashSet<ulong> localsDead = new HashSet<ulong>();
					var localAliveDups = new HashSet<ClrRoot>(rootEqCmp);
					var localDeadDups = new HashSet<ClrRoot>(rootEqCmp);
					int dupLocals = 0;
					for (int i = 0, icnt = threads.Length; i < icnt; ++i)
					{
						ClrThread th = threads[i];
						var frm = threadFrames[i];
						var vars = threadLocalDeadVars[i];
						sw.WriteLine(th.OSThreadId.ToString() + "/" + th.ManagedThreadId + "  " + Utils.RealAddressString(th.Address));
						sw.WriteLine("    alive locals");
						for (int j = 0, jcnt = threadLocalAliveVars[i].Length; j < jcnt; ++j)
						{
							ClrRoot root = threadLocalAliveVars[i][j];
							if (!localsAlive.Add(root.Object))
							{
								localAliveDups.Add(root);
								++dupLocals;
							}
							ClrType clrType = heap.GetObjectType(root.Object);
							var typeId = index.GetTypeId(root.Object);
							sw.Write("    " + Utils.RealAddressStringHeader(root.Object));
							if (clrType != null)
								sw.Write(clrType.Name);
							else
								sw.Write(Constants.NullTypeName);
							sw.WriteLine("  [" + typeId + "]");

							int cnt;
							var stackObj = Utils.RealAddressStringHeader(root.Object) + typeId.ToString();
							++totalStaclObjectsCount;
							if (stackObjects.TryGetValue(stackObj, out cnt))
							{
								stackObjects[stackObj] = cnt + 1;
							}
							else
							{
								stackObjects.Add(stackObj, 1);
							}
						}

						sw.WriteLine("    dead locals");
						for (int j = 0, jcnt = threadLocalDeadVars[i].Length; j < jcnt; ++j)
						{
							ClrRoot root = threadLocalDeadVars[i][j];
							if (!localsDead.Add(root.Object))
							{
								localDeadDups.Add(root);
								++dupLocals;
							}
							ClrType clrType = heap.GetObjectType(root.Object);
							var typeId = index.GetTypeId(root.Object);
							sw.Write("    " + Utils.RealAddressStringHeader(root.Object));
							if (clrType != null)
								sw.Write(clrType.Name);
							else
								sw.Write(Constants.NullTypeName);
							sw.WriteLine("  [" + typeId + "]");
							int cnt;
							var stackObj = Utils.RealAddressStringHeader(root.Object) + typeId.ToString();
							++totalStaclObjectsCount;
							if (stackObjects.TryGetValue(stackObj, out cnt))
							{
								stackObjects[stackObj] = cnt + 1;
							}
							else
							{
								stackObjects.Add(stackObj, 1);
							}
						}
						for (int j = 0, jcnt = threadFrames[i].Length; j < jcnt; ++j)
						{
							ClrStackFrame fr = threadFrames[i][j];
							if (fr.Method != null)
							{
								string fullSig = fr.Method.GetFullSignature();
								if (fullSig == null)
									fullSig = fr.Method.Name;
								if (fullSig == null) fullSig = "UKNOWN METHOD";
								var frameStr = Utils.RealAddressStringHeader(fr.InstructionPointer) + fullSig;
								sw.WriteLine("  " + frameStr);
								int cnt;
								if (frames.TryGetValue(frameStr, out cnt))
								{
									frames[frameStr] = cnt + 1;
								}
								else
								{
									frames.Add(frameStr, 1);
								}

							}
							else
							{
								string sig = string.IsNullOrEmpty(fr.DisplayString) ? "UKNOWN METHOD" : fr.DisplayString;
								var frameStr = Utils.RealAddressStringHeader(fr.InstructionPointer) + sig;
								sw.WriteLine("  " + frameStr);
								int cnt;
								if (frames.TryGetValue(frameStr, out cnt))
								{
									frames[frameStr] = cnt + 1;
								}
								else
								{
									frames.Add(frameStr, 1);
								}
							}
						}
					}

					var localAliveDupsAry = localAliveDups.ToArray();
					var localDeadDupsAry = localDeadDups.ToArray();



					TestContext.WriteLine("LOCAL OBJECT ALIVE COUNT: " + localsAlive.Count);
					TestContext.WriteLine("LOCAL OBJECT DEAD COUNT: " + localsDead.Count);
					TestContext.WriteLine("LOCAL OBJECT DUPLICATE COUNT: " + dupLocals);

				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
				finally
				{
					sw?.Close();
				}
			}
		}


		[TestMethod]
		public void TestIndexSaveThreadInfo()
		{
			const string indexPath = @"C:\WinDbgStuff\Dumps\Analytics\AnalyticsMemory\A2_noDF.dmp.map";
			string error = null;
			var rootEqCmp = new ClrRootEqualityComparer();
			var rootCmp = new ClrRootObjCmp();

			using (var index = OpenIndex(indexPath))
			{
				try
				{

					var runtime = index.Runtime;
					var heap = runtime.Heap;
					var stackTraceLst = new List<ClrStackFrame>();
					var threads = DumpIndexer.GetThreads(runtime);
					var threadLocalDeadVars = new ClrRoot[threads.Length][];
					var threadLocalAliveVars = new ClrRoot[threads.Length][];
					var threadFrames = new ClrStackFrame[threads.Length][];

					var frames = new StringIdDct();
					var stObjCmp = new Utils.KVIntUlongCmpAsc();
					var stackObject = new SortedDictionary<KeyValuePair<int, ulong>, int>(stObjCmp);

					for (int i = 0, icnt = threads.Length; i < icnt; ++i)
					{
						var t = threads[i];
						stackTraceLst.Clear();
						foreach (var st in t.EnumerateStackTrace())
						{
							stackTraceLst.Add(st);
							if (stackTraceLst.Count > 100) break;
						}
						threadLocalAliveVars[i] = t.EnumerateStackObjects(false).ToArray();
						var all = t.EnumerateStackObjects(true).ToArray();
						threadLocalDeadVars[i] = all.Except(threadLocalAliveVars[i], rootEqCmp).ToArray();
						threadFrames[i] = stackTraceLst.ToArray();
					}


					string[] typeNames = index.TypeNames;
					var frameIds = new List<int>();
					var aliveIds = new List<int>();
					var deadIds = new List<int>();

					for (int i = 0, icnt = threads.Length; i < icnt; ++i)
					{
						aliveIds.Clear();
						for (int j = 0, jcnt = threadLocalAliveVars[i].Length; j < jcnt; ++j)
						{
							ClrRoot root = threadLocalAliveVars[i][j];
							ClrType clrType = heap.GetObjectType(root.Object);
							var typeName = clrType == null ? Constants.NullTypeName : clrType.Name;
							var typeId = Array.BinarySearch(typeNames, typeName, StringComparer.Ordinal);
							if (typeId < 0) typeId = Constants.InvalidIndex;
							int stackId;
							if (!stackObject.TryGetValue(new KeyValuePair<int, ulong>(typeId, root.Object), out stackId))
							{
								stackId = stackObject.Count;
								stackObject.Add(new KeyValuePair<int, ulong>(typeId, root.Object), stackId);
							}
							aliveIds.Add(stackId);
						}

						deadIds.Clear();
						for (int j = 0, jcnt = threadLocalDeadVars[i].Length; j < jcnt; ++j)
						{
							ClrRoot root = threadLocalDeadVars[i][j];
							ClrType clrType = heap.GetObjectType(root.Object);
							var typeName = clrType == null ? Constants.NullTypeName : clrType.Name;
							var typeId = Array.BinarySearch(typeNames, typeName, StringComparer.Ordinal);
							if (typeId < 0) typeId = Constants.InvalidIndex;
							int stackId;
							if (!stackObject.TryGetValue(new KeyValuePair<int, ulong>(typeId, root.Object), out stackId))
							{
								stackId = stackObject.Count;
								stackObject.Add(new KeyValuePair<int, ulong>(typeId, root.Object), stackId);
							}
							deadIds.Add(stackId);
						}


						frameIds.Clear();
						for (int j = 0, jcnt = threadFrames[i].Length; j < jcnt; ++j)
						{
							ClrStackFrame fr = threadFrames[i][j];
							if (fr.Method != null)
							{
								string fullSig = fr.Method.GetFullSignature();
								if (fullSig == null)
									fullSig = fr.Method.Name;
								if (fullSig == null) fullSig = "UKNOWN METHOD";
								var frameStr = Utils.RealAddressStringHeader(fr.InstructionPointer) + fullSig;
								var frId = frames.JustGetId(frameStr);
								frameIds.Add(frId);
							}
							else
							{
								string sig = string.IsNullOrEmpty(fr.DisplayString) ? "UKNOWN METHOD" : fr.DisplayString;
								var frameStr = Utils.RealAddressStringHeader(fr.InstructionPointer) + sig;
								var frId = frames.JustGetId(frameStr);
								frameIds.Add(frId);
							}
						}
					}

				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
				finally
				{
					//sw?.Close();
				}
			}
		}


		#endregion threads

		#region type default values

		[TestMethod]
		public void GetTypeDefaultValues()
		{
			string error = null;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			var index = OpenIndex();
			TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

			using (index)
			{
				var typeId = index.GetTypeId("ECS.Common.HierarchyCache.Structure.RealPosition");
				index.GetTypeFieldDefaultValues(typeId);
			}

			Assert.IsNull(error, error);
		}

		#endregion type default values

		[TestMethod]
		public void GetTypeSizeHistogram()
		{
			string error = null;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			var index = OpenIndex(Setup.DumpsFolder + @"\Analytics\Ellerston\Eze.Analytics.Svc_170309_130146.BIG.dmp.map");
			TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));
			string typeName = "Free";
			int[] genHistogram = new int[5];
			SortedDictionary<ulong,int> dct = new SortedDictionary<ulong, int>();
			using (index)
			{
				ClrHeap heap = index.Heap;
				var typeId = index.GetTypeId(typeName);
				var addresses = index.GetTypeRealAddresses(typeId);
				for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
				{
					var addr = addresses[i];
					var gen = heap.GetGeneration(addr);
					genHistogram[gen] += 1;
					ClrType clrType = heap.GetObjectType(addr);
					Assert.IsNotNull(clrType);
					var sz = clrType.GetSize(addr);
					int cnt;
					if (dct.TryGetValue(sz, out cnt))
					{
						dct[sz] = cnt + 1;
					}
					else
					{
						dct.Add(sz,1);
					}
				}

				index.GetTypeFieldDefaultValues(typeId);
			}

			Assert.IsNull(error, error);
		}


		[TestMethod]
		public void GetTypeNamesAndCounts()
		{
			string error = null;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			var index = OpenIndex(Setup.DumpsFolder + @"\Compliance\Eze.Compliance.Svc_170503_131515.dmp.map");
			TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));
//			var dct = new Dictionary<int, int>();
			var sdct = new SortedDictionary<string, KeyValuePair<int, int>>();
			using (index)
			{
				var types = index.InstanceTypes;
				for (int i = 0, icnt = types.Length; i < icnt; ++i)
				{
					var typeId = types[i];
					var typeName = index.GetTypeName(typeId);
					typeName = Utils.GetWinDbgTypeName(typeName);
					KeyValuePair<int, int> kv;
					if (sdct.TryGetValue(typeName, out kv))
					{
						sdct[typeName] = new KeyValuePair<int,int>(kv.Key+1,kv.Value);
					}
					else
					{
						sdct.Add(typeName, new KeyValuePair<int, int>(1,0));
					}
				}
				//string[] typeNames = new string[dct.Count];
				//int[] counts = new int[dct.Count];
				//int ndx = 0;
				//int totalCnt = 0;
				//foreach(var kv in dct)
				//{
				//	string typeName = index.GetTypeName(kv.Key);
				//	typeNames[ndx] = typeName;
				//	counts[ndx] = kv.Value;
				//	totalCnt += kv.Value;
				//	++ndx;
				//}
				//Array.Sort(typeNames, counts);

			}

			StreamReader sr = null;
			StreamWriter sw = null;
			int _1Count = 0;
			int _2Count = 0;
			try
			{
				var seps = new char[] { ' ' };
				sr = new StreamReader(@"C:\WinDbgStuff\Dumps\Compliance\Eze.Compliance.Svc_170503_131515.HEA2E7F.tmp.Cleaned.txt");
				string ln = sr.ReadLine();

				while(ln!=null)
				{
					string[] data = ln.Split(seps,StringSplitOptions.RemoveEmptyEntries);
					Debug.Assert(data.Length == 2);
					int cnt = Int32.Parse(data[1]);
					string typeName = data[0];

					KeyValuePair<int, int> kv;
					if (sdct.TryGetValue(typeName, out kv))
					{
						sdct[typeName] = new KeyValuePair<int, int>(kv.Key, kv.Value+cnt);
					}
					else
					{
						sdct.Add(typeName, new KeyValuePair<int, int>(0, cnt));
					}

					ln = sr.ReadLine();

				}


				sr.Close();
				sr = null;
				sw = new StreamWriter(@"C:\WinDbgStuff\Dumps\Compliance\Eze.Compliance.Svc_170503_131515.HEA2E7F.TypeDiff.txt");

				foreach(var kv in sdct)
				{
					_1Count += kv.Value.Key;
					_2Count += kv.Value.Value;
					sw.Write(kv.Key);
					sw.WriteLine("  1[" + kv.Value.Key + "] 2[" + kv.Value.Value + "]");
				}
				sw.WriteLine("#### TOTALS  1[" + _1Count + "] 2[" + _2Count + "]");


			}
			finally
			{
				sr?.Close();
				sw?.Close();
			}

			Assert.IsNull(error, error);
		}

        #region type sizes and distribution



        #endregion type sizes and distribution

        #region references

        [TestMethod]
        public void TestReferences0()
        {
            string error = null;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            string dumpPath = dumps[0];
            var testFolder = Path.GetDirectoryName(dumpPath);

            //FwdOffsets,
            //FwdRefs,
            //BwdOffsets,
            //BwdRefs

            string[] testFolders = new string[]
            {
                testFolder + @"\tests" + Path.DirectorySeparatorChar + "fwdrefsoffsets.bin",
                testFolder + @"\tests" + Path.DirectorySeparatorChar + "fwdrefs.bin",
                testFolder + @"\tests" + Path.DirectorySeparatorChar + "bwdrefsoffsets.bin",
                testFolder + @"\tests" + Path.DirectorySeparatorChar + "bwdrefs.bin",
            };

            var index = OpenIndex(dumpPath + ".map");


            TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

            using (index)
            {
                var typeName = "Eze.Server.Common.Pulse.Common.Types.ServerColumnPostionLevelCacheDictionary<System.Decimal>";
                var typeId = index.GetTypeId(typeName);
                var typeInstanceNdxs = index.GetTypeAllInstanceIndices(typeId);
                var typeInstanceAddrs = index.GetTypeRealAddresses(typeId);
                Array.Sort(typeInstanceNdxs);
                Array.Sort(typeInstanceAddrs);

                string path = testFolder + @"\tests" + Path.DirectorySeparatorChar + "instances.bin";
                var testIndices = Utils.ReadUlongArray(path, out error);
                for (int i = 0, icnt = typeInstanceNdxs.Length; i < icnt; ++i)
                {
                    Assert.IsTrue(typeInstanceAddrs[i] == testIndices[typeInstanceNdxs[i]]);
                }

                var referencer = new InstanceReferences(index.Instances, testFolders);


                using (referencer)
                {
                    var kv = referencer.GetAncestors(typeInstanceNdxs, 2, out error);
                }
                Assert.IsNull(error);
            }

            Assert.IsNull(error, error);
        }


		[TestMethod]
		public void TestReferences1()
		{
			string error = null;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			string dumpPath = dumps[0];
			var testFolder = Path.GetDirectoryName(dumpPath);

			//FwdOffsets,
			//FwdRefs,
			//BwdOffsets,
			//BwdRefs

			string[] testFolders = new string[]
			{
				testFolder + @"\tests" + Path.DirectorySeparatorChar + "fwdrefsoffsets.bin",
				testFolder + @"\tests" + Path.DirectorySeparatorChar + "fwdrefs.bin",
				testFolder + @"\tests" + Path.DirectorySeparatorChar + "bwdrefsoffsets.bin",
				testFolder + @"\tests" + Path.DirectorySeparatorChar + "bwdrefs.bin",
			};
			string indexPath = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.dmp.map";

			var typeName = "Eze.Server.Common.Pulse.Common.Types.ServerColumnPostionLevelCacheDictionary<System.Decimal>";
			ulong[] instances = Utils.ReadUlongArray(indexPath + @"\analyticsdump111.dlk.dmp.`INSTANCES[0].bin", out error);
			int[] types = Utils.ReadIntArray(indexPath + @"\analyticsdump111.dlk.dmp.`INSTANCETYPES[0].bin", out error);
			string[] typeNames = Utils.GetStringListFromFile(indexPath + @"\analyticsdump111.dlk.dmp.`TYPENAMES[0].txt", out error);
			int typeId = Array.BinarySearch(typeNames, typeName, StringComparer.Ordinal);
			Assert.IsFalse(typeId < 0);
			var lst = new List<int>(1024);
			for(int i = 0, icnt = instances.Length; i < icnt; ++i)
			{
				if (typeId == types[i]) lst.Add(i);
			}
			lst.Sort();

			var referencer = new InstanceReferences(instances, testFolders);
//			(bool countsSame, bool offse3tsSame) = referencer.TestOffsets();
			using (referencer)
			{
				var kv = referencer.GetAncestors(lst.ToArray(), 2, out error);
			}
			Assert.IsNull(error);

		}

        [TestMethod]
        public void TestReferencesRoots()
        {
            string error = null;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            string dumpPath = dumps[0];
            var testFolder = Path.GetDirectoryName(dumpPath);

            ulong[] testAddrs = new ulong[]
 {
        0x0000000080116588,   /*0x4000000080116588,*/
        0x0000000080119f08,   /*0x4000000080119f08,*/
        0x0000000080116340,   /*0x4000000080116340,*/
        0x0000000080116328,   /*0x4000000080116328,*/
        0x0000000080116210,   /*0x4000000080116210,*/
        0x000000008011a718,   /*0x400000008011a718,*/
        0x00000000801160e0,   /*0xc0000000801160e0,*/
                              //0x000000008011a880,   /*0x400000008011a880,*/
                              //0x00000000801161a8    /*0xc0000000801161a8*/
 };

            int[] testIndices = new int[testAddrs.Length];

            string indexPath = @"C:\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.new2.dmp.map";
            ulong addr = 0x00000080116588;
            var index = OpenIndex(indexPath);
            using (index)
            {
                for (int i = 0, icnt = testAddrs.Length; i < icnt; ++i)
                {
                    testIndices[i] = index.GetInstanceIndex(testAddrs[i]);
                }

                var indices = index.GetAllReferences(addr, InstanceReferences.ReferenceType.All | InstanceReferences.ReferenceType.Ancestors, out error);
                (ulong[] roots, ulong[] objects) = index.GetRootAddresses(out error);

                int i1 = Utils.AddressSearch(roots, 0x000000804ba788);
                int i2 = Utils.AddressSearch(objects, 0x000000804ba788);

                int j1 = Utils.AddressSearch(roots, 0x000000804ba7e8);
                int j2 = Utils.AddressSearch(objects, 0x000000804ba7e8);

                int k1 = Utils.AddressSearch(roots, 0x0000000003491650);
                int k2 = Utils.AddressSearch(objects, 0x0000000003491650);

                Assert.IsNull(error, error);
                ulong[] addresses = new ulong[indices.Length];
                for (int i = 0, icnt = indices.Length; i < icnt; ++i)
                {
                    ulong a = index.GetInstanceAddress(indices[i]);
                    addresses[i] = a;
                    if (Utils.AddressSearch(roots,a)>=0)
                    {
                        int b = 1;
                    }
                    if (Utils.AddressSearch(objects, a) >= 0)
                    {
                        int b = 1;
                    }
                }
             }


            Assert.IsNull(error);
        }


        ulong[] suspects = new ulong[]
        {
            0x000069a5e44df8
,0x000069b3c711f8
,0x000069b8244f18
,0x000069b8fef880
,0x000069b981bfc0
,0x000069bab95ae0
,0x000069bcfb46e0
,0x000069bdefab28
,0x000069c070ae48
,0x000069c4d8b8b8
,0x000069c55714e8
,0x000069c6406fa0
,0x000069ca0d42b0
,0x000069cdf446e8
,0x000069cee02e40
,0x000069d0434b58
,0x000069d265fb40
,0x00006aa7f72930
,0x00006ab3d15030
,0x00006ab5dd5b48
,0x00006ab6d3ed00
,0x00006ab80b7550
,0x00006ab8f45ec0
,0x00006abd558b70
,0x00006ac004a538
,0x00006ac40dcbc0
,0x00006aca485208
,0x00006acc60aa80
,0x00006accbcdaf0
,0x00006acd920f18
,0x00006ace0c72d0
,0x00006acf024480
,0x00006ad2a2a860
,0x00006baeaaa258
,0x00006bb07f3bd0
,0x00006bb0f35fd8
,0x00006bb132ff58
,0x00006bb18612a8
,0x00006bb2d61210
,0x00006bb5683558
,0x00006bb6c48478
,0x00006bba3e9498
,0x00006bbb4ecfd8
,0x00006bbbe83fb0
,0x00006bbcbdde30
,0x00006bbdbc6520
,0x00006bc00e2a30
,0x00006bc124f688
,0x00006bc20c8f78
,0x00006bc3c71cd8
,0x00006bc415d650
,0x00006bc7b40c00
,0x00006bc876bba8
,0x00006bcb5aa610
,0x00006bccb10508
,0x00006bd02277f8
,0x00006cb4dfd1b0
,0x00006cb717df58
,0x00006cb740c348
,0x00006cbb0efa68
,0x00006cbcabc1c8
,0x00006cc168f538
,0x00006cc249af58
,0x00006cc4194098
,0x00006cc4e06c58
,0x00006cc6ffb780
,0x00006cc76735f8
,0x00006cc8bda6f0
,0x00006cc9e41570
,0x00006ccacdfb30
,0x00006ccbe23b18
,0x00006ccd0e36c0
,0x00006cce2394a8
,0x00006cd2dd9428
,0x00006cd46c5120
,0x00006cd606f808
        }          ;

        [TestMethod]
        public void TestInstancesTimestamps()
        {
            ulong addr = 0x0256e64c;
            string error = null;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var index = OpenIndex(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Ellerston\Eze.Analytics.Svc_170607_214916.dmp.map");
            TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

            InstanceValue inst;
            using (index)
            {
                var heap = index.Heap;
                ClrType clrType = heap.GetObjectType(suspects[0]);
                ClrInstanceField fld = clrType.GetFieldByName("createTimeStamp");
                DateTime[] dts = new DateTime[suspects.Length];
                for (int i = 0, icnt = suspects.Length; i < icnt; ++i)
                {
                    long tmstmp = (long)fld.GetValue(suspects[i], false, false);
                    var dt = new DateTime(tmstmp,DateTimeKind.Utc);
                    dts[i] = dt;
                }
                Array.Sort(dts);

                string typeName = @"Eze.Server.Common.Pulse.CalculationCache.RelatedViewsCache";
                int typeId = index.GetTypeId(typeName);
                ulong[] all = index.GetTypeRealAddresses(typeId);

                DateTime[] adts = new DateTime[all.Length];
                for (int i = 0, icnt = suspects.Length; i < icnt; ++i)
                {
                    long tmstmp = (long)fld.GetValue(all[i], false, false);
                    var dt = new DateTime(tmstmp, DateTimeKind.Utc);
                    adts[i] = dt;
                }
                Array.Sort(adts);

            }

            Assert.IsNull(error, error);
        }

        int[] GetTypeIds(DumpIndex index, IndexNode node, HashSet<int> typeIdSet, Queue<IndexNode> que)
        {
            que.Clear();
            que.Enqueue(node);
            while(que.Count > 0)
            {
                var n = que.Dequeue();
                for(int i = 0, icnt = n.Nodes.Length; i < icnt; ++i)
                {
                    var cn = n.Nodes[i];
                    int typeId = index.GetTypeId(cn.Index);
                    typeIdSet.Add(typeId);
                    que.Enqueue(cn);
                }
            }
            return Utils.EmptyArray<int>.Value;
        }

        string[] GetTypeNames(DumpIndex index, IEnumerable<int> set)
        {
            string[] names = new string[set.Count()];
            int ndx = 0;
            foreach(var id in set)
            {
                names[ndx++] = index.GetTypeName(id);
            }
            Array.Sort(names,StringComparer.Ordinal);
            return names;
        }

        #endregion references

        #region roots

        [TestMethod]
		public void TestRootScan()
		{
			string error = null;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			var index = OpenIndex();
			Assert.IsNotNull(index);
			TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

			stopWatch.Restart();
			using (index)
			{
				var rootAddresses = index.RootObjects;
				Assert.IsTrue(Utils.IsSorted(rootAddresses));
				var finalizerAddresses = index.FinalizerAddresses;
				Assert.IsTrue(Utils.IsSorted(finalizerAddresses));
				var ndxInstances = index.Instances;
				var instances = Utils.GetRealAddresses(ndxInstances);
				index.Dump.WarmupHeap();
				var heap = index.Dump.Heap;

				List<ulong> rootLst = new List<ulong>(1024*10);
				for (int i = 0, icnt = rootAddresses.Length; i < icnt; ++i)
				{
					var addr = Utils.RealAddress(rootAddresses[i]);
					var rndx = Utils.AddressSearch(instances, addr);
					if (rndx >= 0)
						rootLst.Add(addr);
				}

				var fldRefList = new List<KeyValuePair<ulong, int>>(64);
				var rootAry = rootLst.ToArray();
				rootLst = null;

				HashSet<ulong> rooted = new HashSet<ulong>();
				Queue<ulong> que = new Queue<ulong>(1024*1024*10);
				SortedDictionary<ulong, ulong[]> objRefs = new SortedDictionary<ulong, ulong[]>();
				SortedDictionary<ulong, List<ulong>> fldRefs = new SortedDictionary<ulong, List<ulong>>();
				var emptyFlds = 0;
				var maxFlds = 0;
				var maxRefs = 1;

				for (int i = 0, icnt = rootAry.Length; i < icnt; ++i)
				{
					var raddr = rootAry[i];
					var clrType = heap.GetObjectType(raddr);
					Assert.IsNotNull(clrType);
					fldRefList.Clear();
					clrType.EnumerateRefsOfObjectCarefully(raddr, (address, off) =>
					{
						fldRefList.Add(new KeyValuePair<ulong, int>(address, off));
					});
					if (rooted.Add(raddr))
					{
						if (fldRefList.Count < 1)
						{
							++emptyFlds;
							continue;
						}
						var fldAry = fldRefList.Select(kv => kv.Key).ToArray();
						if (fldAry.Length > maxFlds)
							maxFlds = fldAry.Length;
						Array.Sort(fldAry);
						objRefs.Add(raddr, fldAry);
						for (int j = 0; j < fldAry.Length; ++j)
						{
							var faddr = fldAry[j];
							que.Enqueue(faddr);
							List<ulong> lst;
							if (fldRefs.TryGetValue(faddr, out lst))
							{
								var fndx = lst.BinarySearch(raddr);
								if (fndx < 0)
								{
									lst.Insert(~fndx, raddr);
									if (maxRefs < lst.Count)
										maxRefs = lst.Count;
								}
							}
							else
							{
								fldRefs.Add(faddr, new List<ulong>() {raddr});
							}
						}
					}
				}
				while (que.Count > 0)
				{
					var addr = que.Dequeue();
					if (!rooted.Add(addr)) continue;

					var clrType = heap.GetObjectType(addr);
					Assert.IsNotNull(clrType);
					fldRefList.Clear();
					clrType.EnumerateRefsOfObjectCarefully(addr, (address, off) =>
					{
						fldRefList.Add(new KeyValuePair<ulong, int>(address, off));
					});
					if (fldRefList.Count < 1)
					{
						++emptyFlds;
						continue;
					}
					var fldAry = fldRefList.Select(kv => kv.Key).ToArray();
					if (fldAry.Length > maxFlds)
						maxFlds = fldAry.Length;
					Array.Sort(fldAry);
					if (!objRefs.ContainsKey(addr))
						objRefs.Add(addr, fldAry);

					for (int j = 0; j < fldAry.Length; ++j)
					{
						var faddr = fldAry[j];
						que.Enqueue(faddr);
						List<ulong> lst;
						if (fldRefs.TryGetValue(faddr, out lst))
						{
							var fndx = lst.BinarySearch(addr);
							if (fndx < 0)
							{
								lst.Insert(~fndx, addr);
								if (maxRefs < lst.Count)
									maxRefs = lst.Count;
							}
						}
						else
						{
							fldRefs.Add(faddr, new List<ulong>() {addr});
						}
					}
				}

				var rootedAry = rooted.ToArray();
				Array.Sort(rootedAry);
				Utils.SetAddressBit(rootedAry, instances, (ulong) Utils.RootBits.Rooted);
				Utils.SetAddressBit(finalizerAddresses, instances, (ulong) Utils.RootBits.Finalizer);

				DumpDiffs(index, ndxInstances, instances, @"RootedDiffs.txt", (ulong)Utils.RootBits.Rooted);
				DumpDiffs(index, ndxInstances, instances, @"FinilizeDiffs.txt", (ulong)Utils.RootBits.Finalizer);

			}

			Assert.IsNull(error, error);
			TestContext.WriteLine(index.DumpFileName + " ROOTS SCAN DURATION: " + Utils.StopAndGetDurationString(stopWatch));
		}

		void DumpDiffs(DumpIndex index, ulong[] ary1, ulong[] ary2, string fileName, ulong bit)
		{
			Assert.IsTrue(ary1.Length == ary2.Length,"Address arrays have different length.");
			var path = index.GetAdHocPath(fileName);
			StreamWriter sw = null;
			try
			{
				sw = new StreamWriter(path);
				for (int i = 0, icnt = ary1.Length; i < icnt; ++i)
				{
					var addr1 = ary1[i];
					var raddr1 = Utils.RealAddress(addr1);
					var addr2 = ary2[i];
					var raddr2 = Utils.RealAddress(addr2);
					Assert.IsTrue(raddr2 == raddr1, "Addresses are different!"); // should not happen
					bool ary1On = Utils.HasBit(addr1, bit);
					bool ary2On = Utils.HasBit(addr2, bit);
					if ((ary1On && ary2On) || (!ary1On && !ary2On)) continue;

					string typeName = index.GetTypeName(raddr1);
					sw.WriteLine(Utils.RealAddressStringHeader(raddr1) + "  " + Convert.ToInt32(ary1On) + "  " + Convert.ToInt32(ary2On) + "  " + typeName);
				}
			}
			catch (Exception ex)
			{
				Assert.IsTrue(false, ex.ToString());
			}
			finally
			{
				sw?.Close();
			}
		}


		[TestMethod]
		public void TestIndexRoots()
		{
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			var index = OpenIndex();
			Assert.IsNotNull(index);
			TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));
			stopWatch.Restart();
			using (index)
			{
				var instances = index.Instances;
				var markedRoooted = 0;
				var markedFinalizer = 0;
				for (int i = 0, icnt = instances.Length; i < icnt; ++i)
				{
					var addr = instances[i];
					if (Utils.IsRooted(addr)) ++markedRoooted;
					if (Utils.IsFinalizer(addr)) ++markedFinalizer;
				}
				TestContext.WriteLine("INSTANCE COUNT: " + Utils.LargeNumberString(instances.Length));
				TestContext.WriteLine("MARKED ROOTED COUNT: " + Utils.LargeNumberString(markedRoooted));
				TestContext.WriteLine("MARKED FINALIZER COUNT: " + Utils.LargeNumberString(markedFinalizer));
			}
		}

		/// <summary>
		/// Find roots, finalizer addresses in heap.
		/// </summary>
        [TestMethod]
        public void TestIndexRootsVsHeap()
        {
            string error;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
			var dumpPath = dumps[0];
			var index = OpenIndex(dumpPath+".map");
			TestContext.WriteLine("DUMP: " + dumpPath);

            Assert.IsNotNull(index);
            TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));
            stopWatch.Restart();
            using (index)
            {
                var instances = index.Instances;
                var rootInfo = index.GetRoots(out error);
                var roots = rootInfo.RootAddresses;
				Assert.IsTrue(Utils.AreAddressesSorted(roots));
                var finalizer = rootInfo.FinalizerAddresses;
				Assert.IsTrue(Utils.AreAddressesSorted(finalizer));

				int notFoundFin = NotFoundCount(instances, finalizer);
				int notFoundOther = NotFoundCount(instances, roots);

				(ClrtRoot[] rootRoots, ulong[] rootRootAddrs, ClrtRoot[] rootObjects, ulong[] rootObjAddrs) = rootInfo.GetRootObjectSearchList();
				var rootRootsDct = new Dictionary<GCRootKind, int>(1024);
				GetNotFountCounts(instances, rootRoots, rootRootAddrs, rootRootsDct);
				var rootObjsDct = new Dictionary<GCRootKind, int>(1024);
				GetNotFountCounts(instances, rootObjects, rootObjAddrs, rootObjsDct);


				TestContext.WriteLine("INSTANCE COUNT: " + Utils.LargeNumberString(instances.Length));
				TestContext.WriteLine("NOT FOUND FINALIZER COUNT: " + Utils.CountString(notFoundFin) + " out of " + Utils.CountString(finalizer.Length));
				TestContext.WriteLine("NOT FOUND ROOTS COUNT: " + Utils.CountString(notFoundOther) + " out of " + Utils.CountString(roots.Length));

				TestContext.WriteLine("NOT FOUND ROOTS  " + Utils.CountString(rootRootAddrs.Length));
				foreach(var kindCount in rootRootsDct)
				{
					TestContext.WriteLine(kindCount.Key.ToString() + "   " + kindCount.Value);
				}
				TestContext.WriteLine("NOT FOUND OBJECTS  " + Utils.CountString(rootObjAddrs.Length));
				foreach (var kindCount in rootObjsDct)
				{
					TestContext.WriteLine(kindCount.Key.ToString() + "   " + kindCount.Value);
				}
			}
        }

		int NotFoundCount(ulong[] instances, ulong[] addrs)
		{
			int notFound = 0;
			for (int i = 0, icnt = addrs.Length; i < icnt; ++i)
			{
				var addr = addrs[i];
				var ndx = Utils.AddressSearch(instances, addr);
				if (ndx < 0) ++notFound;
			}
			return notFound;
		}

		void GetNotFountCounts(ulong[] instances, ClrtRoot[] roots, ulong[] addrs, Dictionary<GCRootKind, int> dct)
		{
			for (int i = 0, icnt = addrs.Length; i < icnt; ++i)
			{
				var addr = addrs[i];
				var ndx = Utils.AddressSearch(instances, addr);
				if (ndx < 0)
				{
					GCRootKind k = ClrtRoot.GetGCRootKind(roots[i].RootKind);

					int cnt;
					if (dct.TryGetValue(k, out cnt))
					{
						dct[k] = cnt + 1;
					}
					else
					{
						dct.Add(k, 1);
					}
				}
			}
		}

		#endregion roots

		#region disassemble

		[TestMethod]
		public void TestDisassemble()
		{
			string error = null;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			var index = OpenIndex();
			TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));
			ulong addr = 0x000000800812a8;
			using (index)
			{
				string[] methods = index.GetMethodNames(addr);
				string methodName = "LoadConfigurationGlobal";
				string[] instructions = index.Disassemble(addr, methodName, out error);

			}

			Assert.IsNull(error, error);
		}



        #endregion disassemble

        #region instance value

        [TestMethod]
        public void TestInstanceValue()
        {
            ulong addr = 0x0256e64c;
            string error = null;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var index = OpenIndex(@"C:\WinDbgStuff\Dumps\TestApp\32\TestApp.exe_170415_073854.dmp.map");
            TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

			InstanceValue inst;
            using (index)
            {
                (error, inst) = ValueExtractor.GetInstanceValue(index.IndexProxy, index.Heap, addr, Constants.InvalidIndex,null);
            }

            Assert.IsNull(error, error);
        }

		[TestMethod]
		public void TestInstanceValueMisc()
		{
			// ulong addr = 0x0001e0d8eb77f8; // 64 bit
			// ulong addr = 0x00000002564fe4; // 32 bit

			ulong addr = Environment.Is64BitProcess ? (ulong)0x0001e0d8eb77f8 : (ulong)0x00000002564fe4;
			string error = null;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			var index = Environment.Is64BitProcess
						? OpenIndex(@"C:\WinDbgStuff\Dumps\TestApp\64\TestApp.exe_170415_073758.dmp.map")
						: OpenIndex(@"C:\WinDbgStuff\Dumps\TestApp\32\TestApp.exe_170415_073854.dmp.map");
						

			TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

			using (index)
			{
				var heap = index.Heap;
				var clrType = heap.GetObjectType(addr);
				var fld = clrType.GetFieldByName("_testStruct");
				ulong faddr = addr + (ulong)fld.Offset + DumpIndex.WordSize;
				ulong faddr2 = fld.GetAddress(addr, false);
				string[] values = new string[fld.Type.Fields.Count];

				try
				{
					//ulong faddr = fld.GetAddress(addr, true);
					for (int i = 0, icnt = fld.Type.Fields.Count; i < icnt; ++i)
					{
						var sfld = fld.Type.Fields[i];
						ClrType fldType;
						ClrElementKind fldKind;
                        ulong fldAddr;
						values[i] = ValueExtractor.GetFieldValue(index.IndexProxy, heap, faddr, sfld, true, out fldType, out fldKind, out fldAddr);
						TestContext.WriteLine(values[i]);
					}
				}
				catch(Exception ex)
				{
					Assert.IsTrue(false, ex.ToString());
				}

			}

			Assert.IsNull(error, error);
		}

		[TestMethod]
		public void TestArrayContent()
		{
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			var index = OpenIndex(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Cowen\Cowen.Analytics.Svc_170713_162556.dmp.map");
			TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));
			ulong addr = 0x000087e74b3210;
			using (index)
			{
				(string error, InstanceValue inst) = ValueExtractor.ArrayContent(index.IndexProxy, index.Heap, addr,null);
				Assert.IsNull(error, error);
			}

		}

        #endregion instance value

        #region type values report

        //[TestMethod]
        //public void TestSavedTypeValuesReport()
        //{
        //    string error;
        //    Stopwatch stopWatch = new Stopwatch();
        //    stopWatch.Start();
        //    var index = OpenIndex(@"C:\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.dmp.map");
        //    TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

        //    using (index)
        //    {
        //        // deserialize query
        //        //
        //        string qpath = @"C:\WinDbgStuff\Dumps\Analytics\Highline\analyticsdump111.dlk.dmp.map\ad-hoc.queries\ClrtDisplayableType.2017-05-27-05-58-41-608.bin";
        //        ClrtDisplayableType[] queryItems = ClrtDisplayableType.DeserializeArray(qpath, out error);
        //        try
        //        {
        //            ListingInfo listing = index.GetTypeValuesReport(queryItems, out error);
        //        }
        //        catch (Exception ex)
        //        {
        //            Assert.IsTrue(false, ex.ToString());
        //        }

        //    }

        //    Assert.IsNull(error, error);
        //}


        [TestMethod]
        public void TestTypeValuesReport()
        {
            string error = null;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var index = OpenIndex(@"C:\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.dmp.map");
            TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

            using (index)
            {
                // deserialize query
                //
                string typeName = @"Eze.Server.Common.Pulse.Common.Types.PositionLevelCache";
                try
                {
                    int typeId = index.GetTypeId(typeName);

                    (string err, ClrtDisplayableType cdt, ulong[] instances) = index.GetTypeDisplayableRecord(typeId, null);
                }
                catch (Exception ex)
                {
                    Assert.IsTrue(false, ex.ToString());
                }

            }

            Assert.IsNull(error, error);
        }

		#endregion type values report

		#region get list of specific clr objects

		#endregion get list of specific clr objects

		#region misc

		[TestMethod]
		public void TestKnownTypes()
		{
			string[] typeNames = new string[]
			{
				"System.Collections.Generic.Dictionary<System.Type,System.String>",
				"System.Collections.Generic.SortedDictionary<System.Int64,System.Int64>",
				"System.Collections.Generic.HashSet<System.Int32>",
				"System.Text.StringBuilder",
			};

			for (int i = 0, icnt = typeNames.Length; i < icnt; ++i)
			{
				Assert.IsTrue(TypeExtractor.IsKnownType(typeNames[i]));
			}
		}

		[TestMethod]
		public void TestMapping()
		{
			char[] chars = new[] {'d', 'a', 'b', 'f', 'h', 'e', 'g', 'c'};
			int[] map = Utils.Iota(chars.Length);
			char[] sorted = new char[chars.Length];
			Buffer.BlockCopy(chars,0,sorted,0,chars.Length*sizeof(char));
			Array.Sort(sorted,map);
			int[] map2 = Utils.Iota(map.Length);
			Array.Sort(map,map2);
			for (int i = 0, icnt = chars.Length; i < icnt; ++i)
			{
				char c1 = chars[i];
				char c2 = sorted[map2[i]];
				Assert.AreEqual(c1,c2);
			}
		}

		#endregion misc

		#region template

		[TestMethod]
		public void TestSnippet()
		{
			string error = null;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			var index = OpenIndex();
			TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

			using (index)
			{

			}

			Assert.IsNull(error,error);
		}

		#endregion template

		#region open index

		public static DumpIndex OpenIndex(int indexNdx=0)
		{
			string error;
			var indexPath = Setup.GetRecentIndexPath(indexNdx);
			Assert.IsNotNull(indexPath,"Setup returned null when asked for index " + indexNdx + ".");
			var version = Assembly.GetExecutingAssembly().GetName().Version;
			var index = DumpIndex.OpenIndexInstanceReferences(version, indexPath, 0, out error);
			Assert.IsNotNull(index, error);
			return index;
		}

		public static DumpIndex OpenIndex(string mapPath)
		{
			string error;
			var version = Assembly.GetExecutingAssembly().GetName().Version;
			var index = DumpIndex.OpenIndexInstanceReferences(version, mapPath, 0, out error);
			Assert.IsNotNull(index, error);
			return index;
		}

		#endregion open index

	}
}
