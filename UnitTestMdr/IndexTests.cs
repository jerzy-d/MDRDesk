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
					var path = index.GetFilePath(0,Constants.MapThreadsAndBlocksGraphFilePostfix);
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
						var clrtThread = new ClrtThread(threads[i],blocks,blkCmp);
						Assert.IsNotNull(clrtThread);
						clrtThread.Dump(bw);
					}
					bw.Write(blocks.Length);
					for (int i = 0, icnt = blocks.Length; i < icnt; ++i)
					{
						var blk = blocks[i];
						var ndx = Array.BinarySearch(blkBlockAry,blk,blkCmp);
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

		#region get list of specific clr objects



		#endregion get list of specific clr objects

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
