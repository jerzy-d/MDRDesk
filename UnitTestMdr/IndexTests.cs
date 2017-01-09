using System;
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

				int a = 1;
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
					var heap = runtime.GetHeap();
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
					var heap = runtime.GetHeap();
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

		#region references

		[TestMethod]
		public void TestReferences()
		{
			string error = null;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			var index = OpenIndex();
			TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

			using (index)
			{
				string path = index.GetFilePath(0, Constants.MapFieldParentsRootedPostfix);
				int[] rootedParents;
				int[][] rootedRefs;
				References.LoadReferences(path, out rootedParents, out rootedRefs, out error);
				Assert.IsNull(error);

			}

	Assert.IsNull(error, error);
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
				var rootAddresses = index.RootAddresses;
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
			string error = null;
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

		#endregion roots

		#region get list of specific clr objects



			#endregion get list of specific clr objects

		#region misc

			[
			TestMethod]
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
				Assert.IsTrue(ValueExtractor.IsKnownType(typeNames[i]));
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
