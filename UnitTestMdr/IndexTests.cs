using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClrMDRIndex;
using ClrMDRUtil.Utils;
using Microsoft.Diagnostics.Runtime;
using System.Globalization;
using System.Text;

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
        public void TestThreadFrames()
        {
            string error = null;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var index = OpenIndex(@"C:\WinDbgStuff\Dumps\Analytics\Highline\analyticsdump111.dlk.dmp.map");
            TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

            using (index)
            {
                Tuple<ClrtThread[], string[], KeyValuePair<int, ulong>[]> info = index.GetThreads(out error);
                Assert.IsNull(error, error);
                index.MapFrameGroupsToThreads(info.Item1, info.Item2);

                string path = index.GetAdHocPath("thread_info.txt");
                StreamWriter sw = null;
                try
                {
                    sw = new StreamWriter(path);
                    var ary = Utils.CloneArray<ClrtThread>(info.Item1);
                    Array.Sort(ary, new ClrThreadByFrameGroupCmp());
                    StringBuilder sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
                    var frmCounts = index.FrameGroupIdCounts;
                    for (int i = 0, icnt = ary.Length; i < icnt; ++i)
                    {
                        var th = ary[i];
                        sb.Clear();
                        sb.Append(th.FrameGroupId).Append(" [").Append(frmCounts[th.FrameGroupId]).Append("] ")
                            .Append(th.OSThreadId).Append(" / ").Append(th.ManagedThreadId);
                        sw.WriteLine(sb.ToString());
                    }
                }
                finally
                {
                    sw?.Close();
                }

                var thrd = FindThread(info.Item1, 6428);
                var thrd2 = FindThreadWithFrameGroup(info.Item1, 0);
            }

            Assert.IsNull(error, error);
        }

        private ClrtThread FindThread(ClrtThread[] threads, int osId)
        {
            for (int i = 0, icnt = threads.Length; i  < icnt; ++i)
            {
                if (threads[i].OSThreadId == osId) return threads[i];
            }
            return null;
        }

        private ClrtThread FindThreadWithFrameGroup(ClrtThread[] threads, int grpId)
        {
            for (int i = 0, icnt = threads.Length; i < icnt; ++i)
            {
                if (threads[i].FrameGroupId == grpId) return threads[i];
            }
            return null;
        }

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

        [TestMethod]
        public void TestCheckCycle()
        {
            string error = null;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var index = OpenIndex(@"C:\WinDbgStuff\Dumps\Analytics\Highline\analyticsdump111.dlk.dmp.map");
            TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

            using (index)
            {
                var digraph = index.ThreadBlockgraph;
                var haveCycle = DGraph.HasCycle(digraph.Graph.Graph);
                var result = Circuits.GetCycles(digraph.Graph.Graph, out error);
                Assert.IsTrue(haveCycle);
            }

            Assert.IsNull(error, error);
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

        [TestMethod]
        public void TestGetInterfaceImplementors()
        {
            string indexPath = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.dmp";
            string error = null;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var index = OpenIndex(indexPath);
            TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

            var dctTypeInterfaces = new SortedDictionary<string, List<string>>();
            var dctInterfaceTypes = new SortedDictionary<string, List<string>>();
            HashSet<int> done = new HashSet<int>();
            List<KeyValuePair<string,string>> notFoundList = new List<KeyValuePair<string, string>>(32);

            using (index)
            {
                ClrHeap heap = index.Dump.Runtime.Heap;
                var segs = heap.Segments;
                string[] typeNames = index.TypeNames;
                for (int i = 0, icnt = segs.Count; i < icnt; ++i)
                {
                    var seg = segs[i];
                    ulong addr = seg.FirstObject;
                    while (addr != 0ul)
                    {
                        ClrType clrType = heap.GetObjectType(addr);
                        if (clrType == null)
                        {
                            goto NEXT_OBJECT;
                        }

                        var interfaces = clrType.Interfaces;
                        if (interfaces != null && interfaces.Count > 0)
                        {
                            string clrTypeName = clrType.Name;
                            var typeNameNdx = Array.BinarySearch(typeNames, clrTypeName, StringComparer.Ordinal);
                            Assert.IsTrue(typeNameNdx >= 0);
                            if (done.Add(typeNameNdx))
                            {
                                var interfaceNames = new List<string>(interfaces.Count);
                                for (int j = 0, jcnt = interfaces.Count; j < jcnt; ++j)
                                {
                                    string interfaceName = interfaces[j].Name;
                                    interfaceNames.Add(interfaceName);
                                    var interfaceNameNdx = Array.BinarySearch(typeNames,interfaceName);
                                    if (interfaceNameNdx < 0)
                                    {
                                        notFoundList.Add(new KeyValuePair<string, string>(clrTypeName,interfaceName));
                                    }
                                    List<string> lst;
                                    if (dctInterfaceTypes.TryGetValue(interfaceName,out lst))
                                    {
                                        lst.Add(clrTypeName);
                                    }
                                    else
                                    {
                                        dctInterfaceTypes.Add(interfaceName, new List<string>() { clrTypeName });
                                    }

                                }
                                dctTypeInterfaces.Add(clrTypeName, interfaceNames);
                            }
                        }
                        NEXT_OBJECT:
                        addr = seg.NextObject(addr);
                    }

                }

                string path = index.GetFilePath(index.CurrentRuntimeIndex, Constants.TxtTypeInterfacesFilePostfix);
                var result = DumpIndex.SaveStringDictionaries(path, dctTypeInterfaces, dctInterfaceTypes, notFoundList, out error);
                Assert.IsTrue(result);
                TypeInterfaces tpIntr = TypeInterfaces.Load(path, out error);
                Assert.IsNotNull(tpIntr);
                Assert.IsNull(error);
            }

            Assert.IsNull(error, error);
        }


        #region type sizes and distribution

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
            SortedDictionary<ulong, int> dct = new SortedDictionary<ulong, int>();
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
                        dct.Add(sz, 1);
                    }
                }

                index.GetTypeFieldDefaultValues(typeId);
            }

            Assert.IsNull(error, error);
        }

        [TestMethod]
        public void TestAllIndicesGetTypeCounts()
        {
            string error = null;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var version = Assembly.GetExecutingAssembly().GetName().Version;

            string folder = @"D:\Jerzy\WinDbgStuff\dumps\Analytics";
            string[] ndxDirs = Directory.GetDirectories(folder, "*.map", SearchOption.AllDirectories);
            List<string> lstGood = new List<string>();
            List<string> lstBad = new List<string>();

            for (int i = 0, icnt=ndxDirs.Length; i < icnt; ++i)
            {
                var index = DumpIndex.OpenIndexInstanceReferences(version, ndxDirs[i], 0, out error);
                if (index != null)
                {
                    lstGood.Add(ndxDirs[i]);
                }
                else
                {
                    lstBad.Add(ndxDirs[i]);
                }
                index?.Dispose();
            }

            //try
            //{

            //}

            //var index = OpenIndex();
            //TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

            //using (index)
            //{

            //}

            //Assert.IsNull(error, error);
        }

        [TestMethod]
        public void TestArrayCounts()
        {
            string error;
            var map = OpenIndex(@"C:\WinDbgStuff\Dumps\Analytics\RCG\analytics3.dmp.map");
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            int totalAryCount = 0;
            ValueTuple<string, int, ClrElementKind, ValueTuple<ulong, int>[]>[] aryInfos = null;
            string path = string.Empty;
            using (map)
            {
                var result = map.GetArrayCounts(out error);
                Assert.IsNull(error, error);
                totalAryCount = result.Item1;
                aryInfos = result.Item2;
                path = map.GetAdHocPath("ArrayCounts.txt");
                var mapKinds = map.TypeKinds;
            }

            StreamWriter sw = null;
            try
            {
                sw = new StreamWriter(path);
                sw.WriteLine("Total Count: " + Utils.CountString(totalAryCount));
                int totalMaxCount = Int32.MinValue;
                ulong totalMaxAddr = Constants.InvalidAddress;
                string totaMaxType = string.Empty;
                for (int i = 0, icnt = aryInfos.Length; i < icnt; ++i)
                {
                    (string typeName, int typeId, ClrElementKind kind, ValueTuple<ulong, int>[] instances) = aryInfos[i];
                    int maxCount = Int32.MinValue;
                    ulong maxAddr = Constants.InvalidAddress; ;
                    for(int j = 0, jcnt = instances.Length; j < jcnt; ++j)
                    {
                        (ulong addr, int count) = instances[j];
                        if (count > maxCount)
                        {
                            maxCount = count;
                            maxAddr = addr;
                        }
                    }
                    if (totalMaxCount < maxCount)
                    {
                        totalMaxCount = maxCount;
                        totalMaxAddr = maxAddr;
                        totaMaxType = typeName;
                    }

                    sw.Write(Utils.CountStringHeader(maxCount));
                    sw.Write(Utils.RealAddressStringHeader(maxAddr));
                    if (TypeExtractor.IsStruct(kind))
                    {
                        sw.Write(Constants.HeavyAsteriskHeader);
                    }
                    sw.WriteLine(typeName);
                }
                sw.WriteLine("####");
                sw.Write(Utils.CountStringHeader(totalMaxCount));
                sw.Write(Utils.RealAddressStringHeader(totalMaxAddr));
                sw.WriteLine(totaMaxType);

            }
            finally
            {
                sw?.Close();
            }

            return;

            // get some stats

            // these counts are disjoint
            int emptyCnt = 0;
            int oneItemCnt = 0;
            int twoItemCnt = 0;
            int lessEq5Cnt = 0;
            int lessEq10Cnt = 0;
            int moreEq1000000Cnt = 0;
            int moreEq10000000Cnt = 0;

            int maxCnt = 0;

            var binHeap = new BinaryHeap<ValueTuple<int, string, ulong>>(new Utils.LambdaComparer<ValueTuple<int, string, ulong>>((a,b)=> a.Item1 < b.Item1 ? -1 : (a.Item1 > b.Item1 ? 1 : 0)));

            bool cntDone = false;
            const int TopCnt = 50;

            //ValueTuple<string, int, ValueTuple<ulong, int>[]>[]>

            for (int i = 0, icnt = aryInfos.Length; i < icnt; ++i)
            {
                //var aryInfo = aryInfos[i];
                (string typeName, int typeId, ClrElementKind kind, ValueTuple<ulong, int>[] instances) = aryInfos[i];

                for (int j = 0, jcnt = instances.Length; j < jcnt; ++j)
                {
                    var ary = instances[j];
                    var cnt = ary.Item2;

                    cntDone = true;
                    switch (cnt)
                    {
                        case 0:
                            ++emptyCnt;
                            break;
                        case 1:
                            ++oneItemCnt;
                            break;
                        case 2:
                            ++twoItemCnt;
                            break;
                        case 3:
                        case 4:
                        case 5:
                            ++lessEq5Cnt;
                            break;
                        case 6:
                        case 7:
                        case 8:
                        case 10:
                        case 11:
                            ++lessEq10Cnt;
                            break;
                        default:
                            cntDone = false;
                            break;
                    }

                    if (cntDone) continue;

                    if (maxCnt < cnt) maxCnt = cnt;
                    if (cnt >= 10000000)
                        ++moreEq10000000Cnt;
                    else if (cnt > 1000000)
                        ++moreEq1000000Cnt;

                    var hcnt = binHeap.Count;
                    if (hcnt < TopCnt) // populate heap
                    {
                        if (hcnt == 0) binHeap.Insert((cnt, typeName, ary.Item1));
                        else
                        {
                            if (binHeap.Peek().Item1 < cnt) binHeap.Insert((cnt, typeName, ary.Item1));
                        }
                    }
                    else
                    {
                        if (binHeap.Peek().Item1 < cnt)
                        {
                            binHeap.RemoveRoot();
                            binHeap.Insert((cnt, typeName, ary.Item1));
                        }
                    }
                }
            }

            var topCnt = binHeap.ToArray();
            Array.Sort(topCnt, (a, b) => a.Item1 < b.Item1 ? 1 : (a.Item1 > b.Item1 ? -1 : string.Compare(a.Item2, b.Item2, StringComparison.Ordinal)));

            stopWatch.Stop();
            TestContext.WriteLine("TEST DURATION: " + Utils.DurationString(stopWatch.Elapsed));
            TestContext.WriteLine("Array Total Count: " + Utils.SizeString((long)totalAryCount));
            TestContext.WriteLine("Array Type Count: " + Utils.SizeString((long)aryInfos.Length));
            TestContext.WriteLine("###");
            TestContext.WriteLine("Empty Array Count: " + Utils.SizeString(emptyCnt));
            TestContext.WriteLine("One Item Array Count: " + Utils.SizeString(oneItemCnt));
            TestContext.WriteLine("Two Item Array Count: " + Utils.SizeString(twoItemCnt));
            TestContext.WriteLine("Less Eq 5 Items Array Count: " + Utils.SizeString(lessEq5Cnt));
            TestContext.WriteLine("Less Eq 10 Items Array Count: " + Utils.SizeString(lessEq10Cnt));
            TestContext.WriteLine("###");
            TestContext.WriteLine("More Eq 1 million Count: " + Utils.SizeString(moreEq1000000Cnt));
            TestContext.WriteLine("More Eq 10 million Count: " + Utils.SizeString(moreEq10000000Cnt));
            TestContext.WriteLine("###");
            TestContext.WriteLine("Max Item Count: " + Utils.SizeString(maxCnt));
            TestContext.WriteLine("Top Counts:");
            foreach (var a in topCnt)
            {
                TestContext.WriteLine(Utils.SizeStringHeader(a.Item1) + Utils.AddressString(a.Item3) + "  " + a.Item2);
            }
        }


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
            string error = null;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var index = OpenIndex(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Ellerston\Eze.Analytics.Svc_170607_214916.dmp.map");
            TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

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

        private static bool Found(ClrtRoot root, ulong[] addrs)
        {
            for (int i = 0, icnt = addrs.Length; i < icnt; ++i)
            {
                var addr = addrs[i];
                if (Utils.RealAddress(root.Address) == addr) return true;
                if (Utils.RealAddress(root.Object) == addr) return true;
            }
            return false;
        }

		[TestMethod]
		public void TestIndexRoots()
		{
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			var index = OpenIndex(@"C:\WinDbgStuff\Dumps\Analytics\Cowen\Cowen.Analytics.Svc_170717_165238.dmp.map");
			Assert.IsNotNull(index);
			TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));
			stopWatch.Restart();

            ulong[] addrs = new ulong[]
            {
                0x000005403f97450,
                0x000005503f47028,
                0x000005503f47068,
                0x000005504557878,
            };

            int rAddrCnt = 0;
            int oAddrCnt = 0;

			using (index)
			{
                var roots = index.Roots;
                var staticVars = roots.StaticVariables;

                for (int i =0, icnt = staticVars.Length; i < icnt; ++i)
                {
                    var root = staticVars[i];
                    if (index.GetInstanceIndex(root.Address)!=Constants.InvalidIndex)
                    {
                        ++rAddrCnt;
                    }
                    if (index.GetInstanceIndex(root.Object) != Constants.InvalidIndex)
                    {
                        ++oAddrCnt;
                    }
                    if (Found(root, addrs))
                    {
                        var typeId = root.TypeId;
                        var typeName = index.GetTypeName(typeId);
                        TestContext.WriteLine(Utils.RealAddressStringHeader(root.Address)
                            + Utils.RealAddressStringHeader(root.Object)
                            + typeName);
                    }
                }



				var instances = index.Instances;
				var markedRoooted = 0;
				var markedFinalizer = 0;
				for (int i = 0, icnt = instances.Length; i < icnt; ++i)
				{
					var addr = instances[i];
					if (Utils.IsRooted(addr)) ++markedRoooted;
					if (Utils.IsFinalizer(addr)) ++markedFinalizer;
				}
                TestContext.WriteLine("FOUND ROOT ADDR COUNT: " 
                    + Utils.CountString(rAddrCnt)
                    + " out of "
                    + Utils.CountString(staticVars.Length));
                TestContext.WriteLine("FOUND OBJS ADDR COUNT: "
                    + Utils.CountString(oAddrCnt)
                    + " out of "
                    + Utils.CountString(staticVars.Length));


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


        private static string[] GetTypeValueAsStringOfAll(ClrHeap heap, ClrType clrType, ClrElementKind kind, ulong[] addrs, out string error, string path = null)
        {
            error = null;
            StreamWriter sw = null;
            ClrType myType = clrType;
            try
            {
                if (path != null) sw = new StreamWriter(path);
                string[] values = new string[addrs.Length];
                for (int i = 0, icnt = addrs.Length; i < icnt; ++i)
                {
                    var addr = addrs[i];
                    if (clrType == null)
                    {
                        myType = heap.GetObjectType(addr);
                        Assert.IsNotNull(myType);
                        kind = TypeExtractor.GetElementKind(myType);
                        Assert.IsFalse(kind == ClrElementKind.Unknown);
                    }

                    values[i] = ValueExtractor.GetTypeValueAsString(heap, addr, myType, kind);
                    if (sw != null)
                        sw.WriteLine(Utils.RealAddressStringHeader(addr) + values[i] + Constants.HeavyGreekCrossPadded + (clrType==null?myType.Name:string.Empty));
                }
                return values;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return null;
            }
            finally
            {
                sw?.Close();
            }
        }

        private static string[] GetTypeValues(DumpIndex index, ClrHeap heap, string typeName, string path)
        {
            string error;
            int typeId = index.GetTypeId(typeName);
            ulong[] addrs = index.GetTypeRealAddresses(typeId);

            if (addrs != null || addrs.Length > 0)
            {
                ClrType clrType = heap.GetObjectType(addrs[0]);
                Assert.IsNotNull(clrType);
                ClrElementKind kind = TypeExtractor.GetElementKind(clrType);
                Assert.IsFalse(kind == ClrElementKind.Unknown);
                Assert.IsTrue(Utils.SameStrings(clrType.Name,typeName));
                string[] values = GetTypeValueAsStringOfAll(heap, clrType, kind, addrs, out error, path);
                Assert.IsNull(error, error);
                Assert.IsNotNull(values);
                return values;
            }
            return Utils.EmptyArray<string>.Value;
        }

        private static string[] GetTypeValues(DumpIndex index, ClrHeap heap, ulong[] addrs, string path)
        {
            string error;
            if (addrs != null || addrs.Length > 0)
            {
                string[] values = GetTypeValueAsStringOfAll(heap, null, ClrElementKind.Unknown, addrs, out error, path);
                Assert.IsNull(error, error);
                Assert.IsNotNull(values);
                return values;
            }
            return Utils.EmptyArray<string>.Value;
        }



        [TestMethod]
        public void GetTypeValueAsString_Test()
        {
            string error = null;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var index = OpenIndex(@"D:\Jerzy\WinDbgStuff\dumps\TestApp\TestApp.exe_170818_102413.dmp.map");
            //var index = OpenIndex(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Cowen\Cowen.Analytics.Svc_170717_165238.dmp.map");
            //var index = OpenIndex(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.new2.dmp.map");
            
            TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

            using (index)
            {
                var heap = index.Heap;

                try
                {
                    string[] values = null;
                    ulong[] addrs = null;
                    var folder = index.OutputFolder + Path.DirectorySeparatorChar;

#if FALSE
                    values = GetTypeValues(index, heap, "System.Guid", folder + "System.Guid" + ".Values" + ".txt");
                    values = GetTypeValues(index, heap, "System.DateTime", folder + "System.DateTime" + ".Values" + ".txt");
                    values = GetTypeValues(index, heap, "System.TimeSpan", folder + "System.TimeSpan" + ".Values" + ".txt");
                    values = GetTypeValues(index, heap, "System.Decimal", folder + "System.Decimal" + ".Values" + ".txt");

                    addrs = index.GetSpecialKindTypeInstances(ClrElementKind.Exception);
                    values = GetTypeValues(index, heap, addrs, folder + "Exception" + ".Values" + ".txt");
#endif
                    addrs = index.GetSpecialKindTypeInstances(ClrElementKind.Enum);
                    values = GetTypeValues(index, heap, addrs, folder + "Enum" + ".Values" + ".txt");
                }
                catch (Exception ex)
                {
                    error = ex.ToString();
                    Assert.IsTrue(false, error);
                    TestContext.WriteLine(Environment.NewLine + error);
                }
            }

            Assert.IsNull(error, error);
        }

        [TestMethod]
        public void GetTypeReferences_Test()
        {
            string error = null;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            //var index = OpenIndex(@"D:\Jerzy\WinDbgStuff\dumps\TestApp\TestApp.exe_170818_102413.dmp.map");
            var index = OpenIndex(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Cowen\Cowen.Analytics.Svc_170717_165238.dmp.map");
            //var index = OpenIndex(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.dmp.map");

            TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

            using (index)
            {
                var heap = index.Heap;

                try
                {
                    string[] typeNames = index.TypeNames;
                    string[] strIds = index.StringIds;
                    IdReferences typeToFields = index.TypeFieldIds;
                    Assert.IsNotNull(typeToFields);
                    IdReferences fieldToTypes = index.FieldParentTypeIds;
                    Assert.IsNotNull(fieldToTypes);
                    string typeName = "ECS.Common.HierarchyCache.Structure.RealPosition";
                    var typeId = index.GetTypeId(typeName);
                    var ids = typeToFields.GetReferences(typeId);
                    string[] fldTypes = new string[ids.Key.Length];
                    string[] fldNames = new string[ids.Value.Length];
                    for (int i = 0, icnt = ids.Key.Length; i < icnt; ++i)
                    {
                        fldTypes[i] = typeNames[ids.Key[i]];
                        fldNames[i] = strIds[ids.Value[i]];
                    }

                    var outfolder = index.OutputFolder + Path.DirectorySeparatorChar;
                }
                catch (Exception ex)
                {
                    error = ex.ToString();
                    Assert.IsTrue(false, error);
                    TestContext.WriteLine(Environment.NewLine + error);
                }
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
			var index = OpenIndex(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Cowen\Cowen.Analytics.Svc_170717_165238.dmp.map");
			TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));
			ulong addr = 0x0000055099f9b08; //0x0000055070662f0; // 0x0000560623a968;

            using (index)
			{
                //(string error, InstanceValue inst) = ValueExtractor.ArrayContent(index//.IndexProxy, index.Heap, addr,null);
                (string error, InstanceValue inst, TypeExtractor.KnownTypes knownType) = index.GetInstanceValue(addr, null);
                Assert.IsNull(error, error);
			}

		}

        //[TestMethod]
        //public void TestDictionaryContent()
        //{
        //    Stopwatch stopWatch = new Stopwatch();
        //    stopWatch.Start();
        //    var index = OpenIndex(@"D:\Jerzy\WinDbgStuff\Dumps\Analytics\Cowen\Cowen.Analytics.Svc_170717_165238.dmp.map");
        //    TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));
        //    ulong addr = 0x0000550415a8f8; // 0x00005308d8b3c0; // 0x000055070662f0;
        //    using (index)
        //    {
        //        //       (string error, InstanceValue inst) = ValueExtractor.GetDictionaryInfo(index.IndexProxy, index.Heap, addr, null);
        //        var heap = index.GetHeap();
        //        var clrType = index.GetObjectType(addr);

        //        //(string error0, KeyValuePair<string, string>[] fldDescription, int count, ClrType dctType, ClrType entryKeyType, ClrType entryValueType, KeyValuePair< string,string >[] entryList) = CollectionContent.dictionaryContent(heap, addr);
        //        //Assert.IsNull(error0, error0);
        //        var (error1, description, values) = ValueExtractor.GetDictionaryContent(heap, addr);

        //    }

        //}

        [TestMethod]
        public void TestSortedDictionaryContent()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var index = OpenIndex(@"D:\Jerzy\WinDbgStuff\Dumps\Analytics\Cowen\Cowen.Analytics.Svc_170717_165238.dmp.map");
            TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));
            ulong addr = 0x000056124fa2b8; // 0x0000530f3bec68; // 0x00005604535698;
            using (index)
            {
                var heap = index.GetHeap();
                var (error, descr, values) = ValueExtractor.GetSortedDictionaryContent(heap, addr);

                Assert.IsNull(error,error);
            }
        }

        [TestMethod]
        public void TestSortedListContent()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var index = OpenIndex(@"D:\Jerzy\WinDbgStuff\Dumps\Analytics\Cowen\Cowen.Analytics.Svc_170717_165238.dmp.map");
            TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));
            ulong addr = 0x0000550440b180; // 0x0000560623aab8;
            using (index)
            {
                var heap = index.GetHeap();
                var (error, descr, values) = ValueExtractor.GetSortedListContent(heap, addr);

                Assert.IsNull(error, error);
            }
        }

        [TestMethod]
        public void TestListContent()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var index = OpenIndex(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.dmp.map");
            TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));
            ulong addr = 0x00000080447368; //;
            using (index)
            {
                var heap = index.GetHeap();
                var (error, descr, values) = ValueExtractor.GetListContent(heap, addr);

                Assert.IsNull(error, error);
            }
        }

        [TestMethod]
        public void TestFrameworkVersion()
        {
            string error;
            var ver = GetDotNetVersion.Get45PlusFromRegistry(out error);
            Assert.IsNull(error, error);
        }

        [TestMethod]
        public void TestHashSetContent()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var index = OpenIndex(@"D:\Jerzy\WinDbgStuff\Dumps\Analytics\Cowen\Cowen.Analytics.Svc_170717_165238.dmp.map");
            TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));
            ulong addr = 0x000053078c26e0; // 0x00005504146250; // 0x00005407533ca0;
            using (index)
            {
                var heap = index.GetHeap();
                var (error, description, values) = ValueExtractor.GetHashSetContent(heap, addr);
                Assert.IsNull(error, error);
            }

        }

        [TestMethod]
        public void TestQueueContent()
        {
            var index = OpenIndex(@"C:\WinDbgStuff\Dumps\Analytics\RCG\analytics3.dmp.map");
            // 0x0000074ab3c3b0 System.Collections.Generic.Queue<ECS.Common.Transport.EzeNotificationMessage>
            ulong addr = 0x0000074ab3c3b0;
            using (index)
            {
                var heap = index.Heap;
                (string error, KeyValuePair<string, string>[] values, string[] ex) = CollectionContent.QueueContentAsStrings(heap, addr);

                Assert.IsNull(error, error);
            }

        }

        [TestMethod]
        public void CollectionTest_array()
        {
            string[] paths = new string[]
            {
                @"D:\Jerzy\WinDbgStuff\dumps\TestApp\TestApp.exe_180126_134041.dmp.map",
                // 0x000000028e13e0 TestApp.TestEnum[]
                @"C:\WinDbgStuff\Dumps\TestApp\64\TestApp.exe_180126_201106.dmp.map",
                // 0x0001ab80007888 System.Collections.Generic.Dictionary+Entry<System.Collections.Generic.KeyValuePair<System.String,System.Int32>,System.Collections.Generic.KeyValuePair<System.String,System.Int32>>[]
            };
            var index = OpenIndex(paths[1]);
            ulong addr = 0x0001ab80007888;
            using (index)
            {
                var heap = index.Heap;
                (string error, ClrType type1, ClrType type2, StructFields structs, string[] values, StructValueStrings[] structValues)
                    = CollectionContent.GetArrayContentAsStrings(heap, addr);
                string[] structStrings = null;
                if (structValues != null)
                {
                    structStrings = new string[structValues.Length];
                    for (int i = 0, icnt = structValues.Length; i < icnt; ++i)
                    {
                        structStrings[i] = StructValueStrings.MergeValues(structValues[i]);
                    }
                }
                
                Assert.IsNull(error, error);
            }

        }

        [TestMethod]
        public void CollectionTest_List()
        {
            var testData = new ValueTuple<string, ValueTuple<string, ulong[]>[]>[]
            {
                (@"D:\Jerzy\WinDbgStuff\dumps\TestApp\64\TestApp.exe_180208_081642.dmp.map",
                    new ValueTuple<string,ulong[]>[]
                    {
                       ("System.Collections.Generic.List<System.TimeZoneInfo+AdjustmentRule>",new ulong[] {0x00000002a9ace8 }),
                    }
                ),
                (@"C:\WinDbgStuff\Dumps\TestApp\64\TestApp.exe_180207_072611.dmp.map",
                    new ValueTuple<string,ulong[]>[]
                    {
                       ("System.Collections.Generic.List<System.TimeZoneInfo+AdjustmentRule>",new ulong[] {0x00024008f1ac90 }),
                    }
                ),
            };
            var ndxPath = testData[1].Item1;
            var addr = testData[1].Item2[0].Item2[0];
            var index = OpenIndex(ndxPath);
            using (index)
            {
                var heap = index.Heap;
                (string error, KeyValuePair<string, string>[] decrs, string[] values) = CollectionContent.GetListContentAsStrings(heap, addr);

                Assert.IsNull(error, error);
            }
        }

        [TestMethod]
        public void CollectionTest_SortedList()
        {
            var testData = new ValueTuple<string, ValueTuple<string, ulong[]>[]>[]
            {
                (@"C:\WinDbgStuff\Dumps\Analytics\Baly\AnalyticsLatencyDump06062016 03354291.dmp.map",
                    new ValueTuple<string,ulong[]>[]
                    {
                       ("System.Collections.Generic.SortedList<System.DateTime,System.DateTime>",new ulong[] {0x0000a1ada51288 }),
                    }
                ),
            };
            var ndxPath = testData[0].Item1;
            var addr = testData[0].Item2[0].Item2[0];
            var index = OpenIndex(ndxPath);
            using (index)
            {
                var heap = index.Heap;
                (string error, KeyValuePair<string, string>[] decrs, KeyValuePair<string,string>[] values) = CollectionContent.GetSortedListContentAsStrings(heap, addr);

                Assert.IsNull(error, error);
            }
        }

        [TestMethod]
        public void CollectionTest_HashSet()
        {
            var testData = new ValueTuple<string, ValueTuple<string, ulong[]>[]>[]
            {
                (@"D:\Jerzy\WinDbgStuff\dumps\TestApp\64\TestApp.exe_180208_081642.dmp.map",
                    new ValueTuple<string,ulong[]>[]
                    {
                       ("System.Collections.Generic.HashSet<System.Char>",new ulong[] {0x00000002aa3598 }),
                       ("System.Collections.Generic.HashSet<System.DateTime>",new ulong[] {0x00000002aa31b0 }),
                       ("System.Collections.Generic.GenericEqualityComparer<System.Decimal>",new ulong[] {0x00000002aa30e8 }),
                       ("System.Collections.Generic.GenericEqualityComparer<System.Double>",new ulong[] {0x00000002aa34d0 }),
                       ("System.Collections.Generic.HashSet<System.Guid>",new ulong[] { 0x00000002aa3340 }),
                       ("System.Collections.Generic.HashSet<System.Single>",new ulong[] { 0x00000002aa3408 }),
                       ("System.Collections.Generic.HashSet<System.String>",new ulong[] { 0x00000002aa30a8 }),
                       ("System.Collections.Generic.HashSet<System.TimeSpan>",new ulong[] { 0x00000002aa3278 }),
                       ("System.Collections.Generic.HashSet<System.ValueTuple<System.String,System.Int32,System.ValueTuple<System.String,System.Int32>,System.String>>",new ulong[] { 0x00000002aa1ce8 }),
                       ("System.Collections.Generic.HashSet<TestApp.TestEnum>",new ulong[] { 0x00000002aa19f8 }),
                    }
                ),
                (@"C:\WinDbgStuff\Dumps\TestApp\64\TestApp.exe_180207_072611.dmp.map",
                    new ValueTuple<string,ulong[]>[]
                    {
                       ("System.Collections.Generic.HashSet<System.Char>",new ulong[] {0x00024008f23598 }),
                       ("System.Collections.Generic.HashSet<System.DateTime>",new ulong[] {0x00024008f231b0 }),
                       ("System.Collections.Generic.GenericEqualityComparer<System.Decimal>",new ulong[] {0x00024008f230e8 }),
                       ("System.Collections.Generic.GenericEqualityComparer<System.Double>",new ulong[] {0x00024008f234d0 }),
                       ("System.Collections.Generic.HashSet<System.Guid>",new ulong[] { 0x00024008f23340 }),
                       ("System.Collections.Generic.HashSet<System.Single>",new ulong[] { 0x00024008f23408 }),
                       ("System.Collections.Generic.HashSet<System.String>",new ulong[] { 0x00024008f230a8 }),
                       ("System.Collections.Generic.HashSet<System.TimeSpan>",new ulong[] { 0x00024008f23278 }),
                       ("System.Collections.Generic.HashSet<System.ValueTuple<System.String,System.Int32,System.ValueTuple<System.String,System.Int32>,System.String>>",new ulong[] { 0x00024008f21c90 }),
                       ("System.Collections.Generic.HashSet<TestApp.TestEnum>",new ulong[] { 0x00024008f219a0 }),
                    }
                ),
            };

            var ndxPath = testData[0].Item1;
            var addr = testData[0].Item2[8].Item2[0];
            var index = OpenIndex(ndxPath);
            using (index)
            {
                var heap = index.Heap;
                (string error, KeyValuePair<string, string>[] decrs, string[] values) = CollectionContent.HashSetContentAsStrings(heap, addr);

                Assert.IsNull(error, error);
            }

        }

        [TestMethod]
        public void CollectionTest_SortedDictionary()
        {
            var testData = new ValueTuple<string, ValueTuple<string, ulong[]>[]>[]
{
                (@"C:\WinDbgStuff\Dumps\Analytics\Baly\AnalyticsLatencyDump06062016 03354291.dmp.map",
                    new ValueTuple<string,ulong[]>[]
                    {
                       ("System.Collections.Generic.SortedDictionary<System.Int32,ECS.Entitlements.Module.Profiles.PricingGroup>",new ulong[] {0x0000a2ad238ce0 }),
                       ("System.Collections.Generic.SortedDictionary<System.Int32,System.Collections.Generic.List<ECS.Common.Threading.Queue.Queues.IMessageQueue>>",new ulong[] {0x0000a1ad9cba50 }),
                       ("System.Collections.Generic.SortedDictionary<System.Int64,System.Int64>",new ulong[] {0x0000a1ada299e0 }),
                       ("System.Collections.Generic.SortedDictionary<System.String,System.Object>",new ulong[] {0x0000a2c99260b8 }),
                    }
                ),
                (@"D:\Jerzy\WinDbgStuff\Dumps\Analytics\Baly\AnalyticsLatencyDump06062016 03354291.dmp.map",
                    new ValueTuple<string,ulong[]>[]
                    {
                       ("System.Collections.Generic.SortedDictionary<System.Int32,ECS.Entitlements.Module.Profiles.PricingGroup>",new ulong[] {0x0000a2ad238ce0 }),
                       ("System.Collections.Generic.SortedDictionary<System.Int32,System.Collections.Generic.List<ECS.Common.Threading.Queue.Queues.IMessageQueue>>",new ulong[] {0x0000a1ad9cba50, 0x0000a1ada3f1c0}),
                       ("System.Collections.Generic.SortedDictionary<System.Int64,System.Int64>",new ulong[] {0x0000a1ada299e0 }),
                       ("System.Collections.Generic.SortedDictionary<System.String,System.Object>",new ulong[] {0x0000a2c99260b8 }),
                    }
                ),
            };

            var ndxPath = testData[1].Item1;
           
            var index = OpenIndex(ndxPath);
            using (index)
            {

                var heap = index.Heap;
                var addr = testData[1].Item2[1].Item2[1];
                (string error, KeyValuePair<string, string>[] decrs, KeyValuePair<string, string>[] values) = CollectionContent.GetSortedDictionaryContentAsStrings(heap, addr);
                Assert.IsNull(error, error);

                //for (int i = 0, icnt = testData[1].Item2[1].Item2.Length; i < icnt; ++i)
                //{
                //    var addr = testData[1].Item2[0].Item2[i];
                //    (string error, KeyValuePair<string, string>[] decrs, KeyValuePair<string, string>[] values) = CollectionContent.GetSortedDictionaryContentAsStrings(heap, addr);
                //    Assert.IsNull(error, error);
                //}
            }
        }

        [TestMethod]
        public void CollectionTest_Dictionary()
        {
            string[] paths = new string[]
            {
/* 0*/          @"C:\WinDbgStuff\Dumps\Analytics\Baly\AnalyticsLatencyDump06062016 03354291.dmp.map",
                // 0x0000a1ada00610 System.Collections.Generic.Dictionary<System.Object,System.String>
                // 0x0000a12cf26938 System.Collections.Generic.Dictionary<System.Object,System.String>
/* 1*/          @"C:\WinDbgStuff\Dumps\Analytics\RCG\analytics3.dmp.map",
                // 0x000006caadcb68 System.Collections.Generic.Dictionary<System.String,ECS.Common.Collections.Tag>
                // 0x0000094afa1bb0 System.Collections.Generic.Dictionary<System.String,System.Boolean>
                // 0x000007caaa7408 System.Collections.Generic.Dictionary<Microsoft.Practices.ObjectBuilder.BuilderStage,System.Collections.Generic.List<Microsoft.Practices.ObjectBuilder.IBuilderStrategy>>
/* 2*/          @"D:\Jerzy\WinDbgStuff\dumps\Analytics\PrimeCap\a6.dmp.map",
                // 0x0000de8bdeeae8 System.Collections.Generic.Dictionary<System.String,ECS.Common.Transport.Event_Data.PricingNode>
                // TODO JRD TEST 0x0000dc8b887ba8 System.Collections.Generic.Dictionary<Microsoft.Practices.ObjectBuilder.BuilderPolicyKey,Microsoft.Practices.ObjectBuilder.IBuilderPolicy>
                // TODO JRD TEST 0x0000dc8b884de0 ERROR Unexpected special kind Crash Dump: a6.dmp(x64) MDR Desk  0.9.0.18[64 - bit] ❖ a6.dmp
                // TODO JRD TEST 0x0000de8bb32c58 System.Collections.Generic.Dictionary<Eze.Server.Common.Pulse.Common.Types.RelatedViews,System.Object>
                // TODO JRD TEST 0x0000dc8c06a748 System.Collections.Generic.HashSet<System.String>
            };

            var index = OpenIndex(paths[0]);
            ulong addr = 0x0000a1ada00610;
            using (index)
            {
                var heap = index.Heap;
                (string error, KeyValuePair<string, string>[] descr, KeyValuePair<string, string>[] values) = CollectionContent.DictionaryContentAsStrings(heap, addr);
                Assert.IsNull(error, error);
            }
        }

        [TestMethod]
        public void CollectionTest_ConcurrentDictionary()
        {
            string[] paths = new string[]
            {
                @"C:\WinDbgStuff\dumps\TestApp.exe_171224_113813.dmp",
                // 0x0002a98000f1e8 System.Collections.Concurrent.ConcurrentDictionary<System.Int32,TestApp.TestEnum>
                @"C:\WinDbgStuff\Dumps\Analytics\RCG\analytics3.dmp.map",
                // 0x0000064b45beb8 System.Collections.Concurrent.ConcurrentDictionary<ECS.Common.HierarchyCache.Structure.CacheKeySourceIdMap,System.Int32>
                // 0x0000064ad1dc28 System.Collections.Concurrent.ConcurrentDictionary<ECS.Common.HierarchyCache.Structure.SymbolAndSide,ECS.Common.HierarchyCache.Structure.IReadOnlyPosition>
                // 0x0000064abbc7a0 System.Collections.Concurrent.ConcurrentDictionary<Microsoft.Practices.CompositeUI.Utility.AbstractKey<Microsoft.Practices.CompositeUI.WorkItem>,Microsoft.Practices.CompositeUI.EventBroker.WorkItemSubscriptions>
                @"D:\Jerzy\WinDbgStuff\dumps\TestApp.exe_180108_083751.dmp.map",
                // 0x00000002bd04b0 System.Collections.Concurrent.ConcurrentDictionary<System.ValueTuple<System.String,System.String>,System.ValueTuple<System.Int32,System.String,System.ValueTuple<System.Int32,System.String>>>
            };
            var index = OpenIndex(paths[2]);
            ulong addr = 0x00000002bd04b0;
            using (index)
            {
                var heap = index.Heap;
                (string error, KeyValuePair<string, string>[] decrs, KeyValuePair<string, string>[] values) = CollectionContent.ConcurrentDictionaryContentAsStrings(heap, addr);

                Assert.IsNull(error, error);
            }

        }

        [TestMethod]
        public void CollectionTest_Queue()
        {
            var testData = new ValueTuple<string, ValueTuple<string, ulong[]>[]>[]
            {
                (@"C:\WinDbgStuff\Dumps\Analytics\Baly\AnalyticsLatencyDump06062016 03354291.dmp.map",
                    new ValueTuple<string,ulong[]>[]
                    {
                       ("System.Collections.Generic.Queue<ECS.Common.Communicator.Module.Services.EventExtensions.MessageData<ECS.Common.Transport.AnalyticsServerStatus>>",new ulong[] {0x0000a1b7581988 }),
                       ("System.Collections.Generic.Queue<ECS.Common.Utils.Logging.Log+LogCommand>",new ulong[] {0x0000a32cedfbb8 }),
                       ("System.Collections.Generic.Queue<System.String>",new ulong[] {0x0000a1ada3d8f8 }),
                    }
                ),
            };
            var ndxPath = testData[0].Item1;
            var addr = testData[0].Item2[2].Item2[0];
            var index = OpenIndex(ndxPath);
            using (index)
            {
                var heap = index.Heap;
                (string error, KeyValuePair<string, string>[] decrs, string[] values) = CollectionContent.QueueContentAsStrings(heap, addr);

                Assert.IsNull(error, error);
            }
        }

        [TestMethod]
        public void TestStringBuilderContent()
        {
            string[] paths = new string[]
            {
                @"C:\WinDbgStuff\dumps\TestApp.exe_171224_113813.dmp",
                @"C:\WinDbgStuff\Dumps\Analytics\RCG\analytics3.dmp.map",
                // 0x0000064fd30bf0 System.Text.StringBuilder
            };
            var index = OpenIndex(paths[1]);
            ulong addr = 0x0000064fd30bf0;
            using (index)
            {
                var heap = index.Heap;
                (string error, KeyValuePair<string, string>[] decrs, string value) = CollectionContent.StringBuilderContent(heap, addr);

                Assert.IsNull(error, error);
            }

        }

        [TestMethod]
        public void TestSortedSetContent()
        {
            string[] paths = new string[]
            {
                @"C:\WinDbgStuff\dumps\TestApp.exe_171224_113813.dmp",
                @"C:\WinDbgStuff\Dumps\Analytics\RCG\analytics3.dmp.map",
                // 0x00000850abd5a8 System.Collections.Generic.SortedSet<System.String>
                // 0x0000074aaba4e0 System.Collections.Generic.SortedSet<System.UInt16>
            };
            var index = OpenIndex(paths[1]);
            ulong addr = 0x0000074aaba4e0;
            using (index)
            {
                var heap = index.Heap;
                (string error, KeyValuePair<string, string>[] decrs, string[] values) = CollectionContent.SortedSetContentAsStrings(heap, addr);

                Assert.IsNull(error, error);
            }

        }


        [TestMethod]
        public void InstanceValue_StructInContext()
        {
            string[] paths = new string[]
            {
                @"D:\Jerzy\WinDbgStuff\dumps\TestApp.exe_180108_083751.dmp.map",
                // 0x00000002bc7130 System.Collections.Generic.Dictionary<System.ValueTuple<System.String,System.String>,System.ValueTuple<System.Int32,System.String,System.ValueTuple<System.Int32,System.String>>>

            };
            var index = OpenIndex(paths[0]);
            ulong addr = 0x00000002bc7130;
            using (index)
            {
                var heap = index.Heap;

                (string error, ClrType type, ClrElementKind kind, (ClrType[] fldTypes, ClrElementKind[] fldKinds, object[] values, StructFieldsInfo[] structInfos, StructValues[] structValues)) =
                    ClassValue.GetClassValues(heap, addr);
                Assert.IsNull(error, error);

                var infos = GatherInfo(type, kind, fldTypes, fldKinds, values, structValues);

                (ulong entriesAddr, ClrType entriesType) = CollectionContent.GetFieldUInt64AndType(type, "entries", fldTypes, values);

                (ClrType entriesElemType, ClrElementKind entriesElemKind, int entriesLen) = CollectionContent.ArrayInfo(heap, entriesType, entriesAddr);

                (ClrType keyTypeByName, ClrType valTypeByName) = TypeExtractor.GetKeyValuePairTypesByName(heap, type.Name, "System.Collections.Generic.Dictionary<");
                ClrElementKind keyKindByName = TypeExtractor.GetElementKind(keyTypeByName);
                ClrElementKind valKindByName = TypeExtractor.GetElementKind(valTypeByName);

                Assert.IsNull(error, error);
            }
        }

        private Tuple<string, string, string, string, string, string>[] GatherInfo(ClrType type, ClrElementKind kind, ClrType[] fldTypes, ClrElementKind[] fldKinds, object[] fldValues, StructValues[] structData)
        {
            Assert.IsTrue(type.Fields.Count == fldTypes.Length);
            int fldCount = type.Fields.Count;
            var infos = new Tuple<string, string, string, string, string, string>[fldCount];
            for (int i = 0; i < fldCount; ++i)
            {
                infos[i] = Tuple.Create(
                        type.Fields[i].Name,
                        type.Fields[i].Type.Name,
                        TypeExtractor.GetElementKind(type.Fields[i].Type).ToString(),
                        fldTypes[i].Name,
                        fldKinds[i].ToString(),
                        fldValues[i].ToString()
                    );
            }
            return infos;
        }

        [TestMethod]
        public void ArrayElement_Tests()
        {
            string[] paths = new string[]
            {
                @"D:\Jerzy\WinDbgStuff\dumps\TestApp.exe_180108_083751.dmp.map",
                // 0x00000002bc7230 System.Collections.Generic.Dictionary+Entry<System.ValueTuple<System.String,System.String>,System.ValueTuple<System.Int32,System.String,System.ValueTuple<System.Int32,System.String>>>[]
                // 0x00000002bc6a18 System.Collections.Generic.Dictionary+Entry<System.Int32,System.Object>[]
            };
            var index = OpenIndex(paths[0]);
            ulong addr = 0x00000002bc6a18;
            using (index)
            {
                var heap = index.Heap;

                ClrType aryType = heap.GetObjectType(addr);

                (ClrType entriesElemType, ClrElementKind entriesElemKind, int entriesLen) = ArrayInfo(heap, aryType, addr);

                (string error, ClrType aType, ClrType eType, StructFields sf, string[] vals, StructValueStrings[] structVals) = CollectionContent.GetArrayContentAsStrings(heap, addr);

                string[] structStrinfs = new string[structVals.Length];
                for (int i = 0, icnt = structVals.Length; i < icnt; ++i)
                {
                    structStrinfs[i] = StructValueStrings.MergeValues(structVals[i]);
                }

                Assert.IsNull(error, error);
            }

        }

        private ValueTuple<ClrType, ClrElementKind, int> ArrayInfo(ClrHeap heap, ClrType type, ulong addr)
        {
            ClrType elType = type.ComponentType;
            ClrElementKind elKind = TypeExtractor.GetElementKind(elType);
            int len = type.GetArrayLength(addr);
            if (elKind == ClrElementKind.Unknown || TypeExtractor.IsAmbiguousKind(elKind))
            {
                for (int i = 0; i < len; ++i)
                {

                }
            }
            return (elType, elKind, len);
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
        public void TestHexString()
        {
            ulong value;
            string s0 = Utils.CleanupHexString("0x00000055099f9b08");
            Assert.IsTrue(ulong.TryParse(s0, System.Globalization.NumberStyles.AllowHexSpecifier, null, out value));
            string s1 = Utils.CleanupHexString("✔x00005507066658");
            Assert.IsTrue(ulong.TryParse(s1, System.Globalization.NumberStyles.AllowHexSpecifier, null, out value));
            string s2 = Utils.CleanupHexString("x0000");
            Assert.IsTrue(ulong.TryParse(s2, System.Globalization.NumberStyles.AllowHexSpecifier, null, out value));
            string s3 = Utils.CleanupHexString("x300");
            Assert.IsTrue(ulong.TryParse(s3, System.Globalization.NumberStyles.AllowHexSpecifier, null, out value));
            string s4 = Utils.CleanupHexString("0x000300");
            Assert.IsTrue(ulong.TryParse(s4, System.Globalization.NumberStyles.AllowHexSpecifier, null, out value));

            string snull = Utils.CleanupHexString(null);
            Assert.IsTrue(snull == string.Empty);
            string sbad = Utils.CleanupHexString("zxxx");
            Assert.IsTrue(snull == string.Empty);


        }

        [TestMethod]
        public void Misc()
        {
#if FALSE
            char[] cary = new char[DumpFileMoniker.InvalidPathCharsAry.Length];
            Array.Copy(DumpFileMoniker.InvalidPathCharsAry, cary, cary.Length);
            //Array.Sort(cary,char)
#endif
#if FALSE
            BGL.TestCycles();
#endif
            var str0 = String.Format(CultureInfo.InvariantCulture, "{0:000000000.00}", 0m);
            var str1 = String.Format(CultureInfo.InvariantCulture, "{0:000000000.00}", 11.126m);
            var str2 = String.Format(CultureInfo.InvariantCulture, "{0:000000000.00}", -11.126m);

            var str1a = String.Format(CultureInfo.InvariantCulture, "{0,12:#.##}", 0m);
            var str1b = String.Format(CultureInfo.InvariantCulture, "{0,12:#.##}", 11.126m);
            var str1c = String.Format(CultureInfo.InvariantCulture, "{0,12:#.##}", -11.126m);

            var str2b = String.Format(CultureInfo.InvariantCulture, "{0,12:#.##}", 11.126m);
            var str2c = String.Format(CultureInfo.InvariantCulture, "{0,11:#.##}", -11.126m);
            var str2d = String.Format(CultureInfo.InvariantCulture, "{0,12:#.##}", 111m);
            var str2e = String.Format(CultureInfo.InvariantCulture, "{0,11:#.##}", -111m);

            var str3b = String.Format(CultureInfo.InvariantCulture, "{0,12:#.00}", 11.126m);
            var str3c = String.Format(CultureInfo.InvariantCulture, "{0,11:#.00}", -11.126m);
            var str3d = String.Format(CultureInfo.InvariantCulture, "{0,12:#.00}", 111m);
            var str3e = String.Format(CultureInfo.InvariantCulture, "{0,11:#.00}", -111m);


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
