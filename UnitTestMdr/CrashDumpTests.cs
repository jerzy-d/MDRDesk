using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using ClrMDRIndex;
using DmpNdxQueries;
using Microsoft.Diagnostics.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestMdr
{
	/// <summary>
	/// Summary description for CrashDumpTests
	/// </summary>
	[TestClass]
	public class CrashDumpTests
	{
		#region ctrs/context/initialization

		public CrashDumpTests()
		{
			//
			// TODO: Add constructor logic here
			//
		}

		private TestContext testContextInstance;

		/// <summary>
		///Gets or sets the test context which provides
		///information about and functionality for the current test run.
		///</summary>
		public TestContext TestContext
		{
			get
			{
				return testContextInstance;
			}
			set
			{
				testContextInstance = value;
			}
		}

		#region Additional test attributes
		//
		// You can use the following additional attributes as you write your tests:
		//
		// Use ClassInitialize to run code before running the first test in the class
		// [ClassInitialize()]
		// public static void MyClassInitialize(TestContext testContext) { }
		//
		// Use ClassCleanup to run code after all tests in a class have run
		// [ClassCleanup()]
		// public static void MyClassCleanup() { }
		//
		// Use TestInitialize to run code before running each test 
		// [TestInitialize()]
		// public void MyTestInitialize() { }
		//
		// Use TestCleanup to run code after each test has run
		// [TestCleanup()]
		// public void MyTestCleanup() { }
		//
		#endregion

		#endregion ctrs/context/initialization

		#region collection content

		#region array content

		[TestMethod]
		public void TestGetStringArrayContent()
		{
			ulong aryAddr = 0x0002189f5af6a0;
			var dmp = OpenDump(1);
			using (dmp)
			{
				var heap = dmp.Heap;
				var result = CollectionContent.aryInfo(heap, aryAddr);
				Assert.IsNull(result.Item1, result.Item1);
				string[] strings = new string[result.Item4];
				for (int i = 0, icnt = result.Item4; i < icnt; ++i)
				{
					strings[i] = CollectionContent.aryElemString(heap, aryAddr, result.Item2, result.Item3, i);
				}
				Assert.IsTrue(NoNullEntries(strings));
				var aryresult = CollectionContent.getAryContent(heap, aryAddr);
				Assert.IsNull(aryresult.Item1);
				Assert.IsTrue(Utils.SameStringArrays(strings,aryresult.Item5));
			}
		}

		[TestMethod]
		public void TestGetDecimalArrayContent()
		{
			ulong aryAddr = 0x0002189f5af8b8;
			var dmp = OpenDump(1);
			using (dmp)
			{
				var heap = dmp.Heap;
				var result = CollectionContent.aryInfo(heap, aryAddr);
				Assert.IsNull(result.Item1, result.Item1);
				string[] strings = new string[result.Item4];
				for (int i = 0, icnt = result.Item4; i < icnt; ++i)
				{
					strings[i] = CollectionContent.aryElemDecimal(heap, aryAddr, result.Item2, result.Item3, i);
				}
				Assert.IsTrue(NoNullEntries(strings));
				var aryresult = CollectionContent.getAryContent(heap, aryAddr);
				Assert.IsNull(aryresult.Item1);
				Assert.IsTrue(Utils.SameStringArrays(strings, aryresult.Item5));
			}
		}

		[TestMethod]
		public void TestGetDateTimeArrayContent()
		{
			ulong aryAddr = 0x0002189f5af780;
			var dmp = OpenDump(1);
			using (dmp)
			{
				var heap = dmp.Heap;
				var result = CollectionContent.aryInfo(heap, aryAddr);
				Assert.IsNull(result.Item1, result.Item1);
				string[] strings = new string[result.Item4];
				for (int i = 0, icnt = result.Item4; i < icnt; ++i)
				{
					strings[i] = CollectionContent.aryElemDatetimeR(heap, aryAddr, result.Item2, result.Item3, i);
				}
				Assert.IsTrue(NoNullEntries(strings));
				var aryresult = CollectionContent.getAryContent(heap, aryAddr);
				Assert.IsNull(aryresult.Item1);
				Assert.IsTrue(Utils.SameStringArrays(strings, aryresult.Item5));
			}
		}

		[TestMethod]
		public void TestGetTimespanArrayContent()
		{
			ulong aryAddr = 0x0002189f5af710;
			var dmp = OpenDump(1);
			using (dmp)
			{
				var heap = dmp.Heap;
				var result = CollectionContent.aryInfo(heap, aryAddr);
				Assert.IsNull(result.Item1, result.Item1);
				string[] strings = new string[result.Item4];
				for (int i = 0, icnt = result.Item4; i < icnt; ++i)
				{
					strings[i] = CollectionContent.aryElemTimespanR(heap, aryAddr, result.Item2, result.Item3, i);
				}
				Assert.IsTrue(NoNullEntries(strings));
				var aryresult = CollectionContent.getAryContent(heap, aryAddr);
				Assert.IsNull(aryresult.Item1);
				Assert.IsTrue(Utils.SameStringArrays(strings, aryresult.Item5));
			}
		}

		[TestMethod]
		public void TestGetGuidArrayContent()
		{
			ulong aryAddr = 0x0002189f5af7f0;
			var dmp = OpenDump(1);
			using (dmp)
			{
				var heap = dmp.Heap;
				var result = CollectionContent.aryInfo(heap, aryAddr);
				Assert.IsNull(result.Item1, result.Item1);
				string[] strings = new string[result.Item4];
				for (int i = 0, icnt = result.Item4; i < icnt; ++i)
				{
					strings[i] = CollectionContent.aryElemGuid(heap, aryAddr, result.Item2, result.Item3, i);
				}
				Assert.IsTrue(NoNullEntries(strings));
				var aryresult = CollectionContent.getAryContent(heap, aryAddr);
				Assert.IsNull(aryresult.Item1);
				Assert.IsTrue(Utils.SameStringArrays(strings, aryresult.Item5));
			}
		}

		[TestMethod]
		public void TestGetBooleanArrayContent()
		{
			ulong aryAddr = 0x0002189f5a3e68;
			var dmp = OpenDump(1);
			using (dmp)
			{
				var heap = dmp.Heap;
				var result = CollectionContent.aryInfo(heap, aryAddr);
				Assert.IsNull(result.Item1, result.Item1);
				string[] strings = new string[result.Item4];
				for (int i = 0, icnt = result.Item4; i < icnt; ++i)
				{
					strings[i] = CollectionContent.aryElemPrimitive(heap, aryAddr, result.Item2, result.Item3, i);
				}
				Assert.IsTrue(NoNullEntries(strings));
				var aryresult = CollectionContent.getAryContent(heap, aryAddr);
				Assert.IsNull(aryresult.Item1);
				Assert.IsTrue(Utils.SameStringArrays(strings, aryresult.Item5));
			}
		}

		private bool NoNullEntries(string[] ary)
		{
			for (int i = 0, icnt = ary.Length; i < icnt; ++i)
			{
				if (ary[i] == null) return false;
			}
			return true;
		}

		#endregion array content

		#region System.Collections.Generic.Dictionary<TKey,TValue> content

		[TestMethod]
		public void TestGetDictionaryContent()
		{
			ulong dctAddr = 0x00015e80012f58;
			var dmp = OpenDump(1);
			using (dmp)
			{
				var heap = dmp.Heap;
				var clrType = heap.GetObjectType(dctAddr);
				if (!clrType.Name.StartsWith("System.Collections.Generic.Dictionary<")) return;
				var result = CollectionContent.getDictionaryInfo(heap, dctAddr, clrType);
				Assert.IsNotNull(result);

				var dctResult = CollectionContent.dictionaryContent(heap, dctAddr);

				Assert.IsNotNull(dctResult);
				Assert.IsNull(dctResult.Item1, dctResult.Item1);
			}
		}

		#endregion System.Collections.Generic.Dictionary<TKey,TValue> content

		#region System.Collections.Generic.SortedDictionary<TKey,TValue> content

		[TestMethod]
		public void TestGetSortedDictionaryContent()
		{
			ulong dctAddr = 0x00015e80013030;
			var dmp = OpenDump(1);
			using (dmp)
			{
				var heap = dmp.Heap;
				var result = CollectionContent.getSortedDicionaryContent(heap, dctAddr);
				Assert.IsNotNull(result);
				Assert.IsNull(result.Item1, result.Item1);
			}
		}

		#endregion System.Collections.Generic.SortedDictionary<TKey,TValue> content

		#region System.Collections.Generic.HashSet<T> content

		[TestMethod]
		public void TestGetHashSetContent()
		{
			ulong[] dctAddrs =new ulong[]
			{
				0x00023f00013130, // string
				0x00023f00013170, // decimal
				0x00023f00013238, // DateTime
				0x00023f00013300, // TimeSpan
				0x00023f000133c8, // Guid
				0x00023f00013490, // float, System.Single
				0x00023f00013558, // double
				0x00023f00013620, // char

			};

			string[] valTypeNames = new string[dctAddrs.Length];
			string[][] values = new string[dctAddrs.Length][];
			var dmp = OpenDump(1);
			int index = 0;
			using (dmp)
			{
				var heap = dmp.Heap;
				FQry.heapWarmup(heap);
				ClrType setType = null;
				goto GET_CONTENT;
				for (int i = 3, icnt = dctAddrs.Length; i < 4; ++i)
				{
					setType = heap.GetObjectType(dctAddrs[i]);

					ClrInstanceField slotsFld = setType.GetFieldByName("m_slots");
					ulong slotsAddr = (ulong) slotsFld.GetValue(dctAddrs[i]);
					ClrType slotsType = heap.GetObjectType(slotsAddr);
					var lastIndex = Auxiliaries.getFieldIntValue(heap, dctAddrs[i], setType, "m_lastIndex");
					var setCount = Auxiliaries.getFieldIntValue(heap, dctAddrs[i], setType, "m_count");
					ClrType compType = slotsType.ComponentType;

					var hashCodeFld = compType.GetFieldByName("hashCode");
					var valueFld = compType.GetFieldByName("value");


					var valType = valueFld.Type;
					if (valType.Name == "ERROR" || valType.Name=="System.__Canon")
					{
						var mt = ValueExtractor.ReadUlongAtAddress(dctAddrs[i] + 96, heap);
						var tp = heap.GetTypeByMethodTable(mt);
						if (tp != null)
						{
							valType = tp;
						}
						else
						{
							index = 0;
							while (index < lastIndex)
							{

								var elemAddr = slotsType.GetArrayElementAddress(slotsAddr, index);
								var hash = Auxiliaries.getIntValue(elemAddr, hashCodeFld, true);
								if (hash >= 0)
								{
									var valAddr = Auxiliaries.getReferenceFieldAddress(elemAddr, valueFld, true);
									tp = heap.GetObjectType(valAddr);
									if (tp != null)
									{
										valType = tp;
										break;
									}
								}

								++index;
							}
						}
					}
					var kind = TypeKinds.GetTypeKind(valType);
					values[i] = new string[setCount];
					index = 0;
					var valIndex = 0;
					while (index < lastIndex)
					{

						var elemAddr = slotsType.GetArrayElementAddress(slotsAddr, index);
						var hash = Auxiliaries.getIntValue(elemAddr, hashCodeFld, true);
						if (hash >= 0)
						{
							string value = Types.getFieldValue(heap, elemAddr, true, valueFld, kind);
							values[i][valIndex++] = value;
						}

						++index;
					}

					valTypeNames[i] = valType.Name;
				}

				return;
GET_CONTENT:
				for (int i = 0, icnt = dctAddrs.Length; i < icnt; ++i)
				{
					var setResult = CollectionContent.getHashSetContent(heap, dctAddrs[i]);
					Assert.IsNotNull(setResult);
					Assert.IsNull(setResult.Item1);
					values[i] = setResult.Item2;
				}

			}
		}

		#endregion  System.Collections.Generic.HashSet<T> content


		#region System.Text.StringBuilder

		[TestMethod]
		public void TestStringBuilderContent()
		{
			ulong addr = 0x0001e7e526c388;
			string expectedString =
				@"0aaaaaaaaaa1aaaaaaaaaa2aaaaaaaaaa3aaaaaaaaaa4aaaaaaaaaa5aaaaaaaaaa6aaaaaaaaaa7aaaaaaaaaa8aaaaaaaaaa9aaaaaaaaaa";
			var dmp = OpenDump(1);
			using (dmp)
			{
				var heap = dmp.Heap;

				var str = CollectionContent.getStringBuilderString(heap, addr);

				Assert.IsNotNull(str);
			}
		}

		#endregion System.Text.StringBuilder

		#endregion collection content

		#region threads

		[TestMethod]
		public void TestBlocking()
		{
			var dmp = OpenDump(@"C:\WinDbgStuff\Dumps\Analytics\AnalyticsMemory\A2_noDF.dmp");
			uint ThreadId = 13680;
			using (dmp)
			{
				ClrThread thread;
				var threads = DumpIndexer.GetThreads(dmp.Runtime);
				for (int i = 0, icnt = threads.Length; i < icnt; ++i)
				{
					if (threads[i].OSThreadId == ThreadId)
					{
						thread = threads[i];
					}
				}

				var heap = dmp.Heap;
				BlockingObject[] freeBlks;
				var blocks = DumpIndexer.GetBlockingObjectsEx(heap,out freeBlks);

				List<BlockingObject> obloks = new List<BlockingObject>(256);
				List<BlockingObject> wbloks = new List<BlockingObject>(256);
				List<BlockingObject> wwbloks = new List<BlockingObject>(256);
				HashSet<string> typeSet = new HashSet<string>();
				int nullTypeCnt = 0;
				for (int i = 0, icnt = blocks.Length; i < icnt; ++i)
				{
					var block = blocks[i];

					if (block.HasSingleOwner && block.Taken && block.Owner!=null && block.Owner.OSThreadId==ThreadId)
					{
						wwbloks.Add(block);
						var clrType = heap.GetObjectType(block.Object);
						if (clrType != null)
							typeSet.Add(clrType.Name);
						else
							++nullTypeCnt;
					}
					for (int j = 0, jcnt = block.Owners.Count; j < jcnt; ++j)
					{
						if (block.Owners[j]==null) continue;
						if (block.Owners[j].OSThreadId == ThreadId)
						{
							obloks.Add(block);
							var clrType = heap.GetObjectType(block.Object);
							if (clrType != null)
								typeSet.Add(clrType.Name);
							else
								++nullTypeCnt;
						}
					}
					for (int j = 0, jcnt = block.Waiters.Count; j < jcnt; ++j)
					{
						if (block.Waiters[j] == null) continue;
						if (block.Waiters[j].OSThreadId == ThreadId)
						{
							wbloks.Add(block);
							var clrType = heap.GetObjectType(block.Object);
							if (clrType != null)
								typeSet.Add(clrType.Name);
							else
								++nullTypeCnt;

						}
					}
				}
				int notFreeCnt = 0;
				for (int i = 0, icnt = freeBlks.Length; i < icnt; ++i)
				{
					var blk = freeBlks[i];
					var clrType = heap.GetObjectType(blk.Object);
					if (clrType != null && clrType.Name != "Free")
					{
						++notFreeCnt;
					}
				}
				Assert.IsTrue(notFreeCnt == 0);
				int a = 1;

			}
		}

		[TestMethod]
		public void TestThreadInfo()
		{
			const string dumpPath = @"C:\WinDbgStuff\Dumps\Analytics\AnalyticsMemory\A2_noDF.dmp";
			string error = null;
			StreamWriter sw = null;
			var rootEqCmp = new ClrRootEqualityComparer();
			var rootCmp = new ClrRootObjCmp();

			using (var clrDump = OpenDump(dumpPath))
			{
				try
				{
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.GetHeap();
					var stackTraceLst = new List<ClrStackFrame>();
					var threads = DumpIndexer.GetThreads(runtime);
					var threadLocalDeadVars = new ClrRoot[threads.Length][];
					var threadLocalAliveVars = new ClrRoot[threads.Length][];
					var threadFrames = new ClrStackFrame[threads.Length][];

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

					var path = DumpFileMoniker.GetAndCreateOutFolder(dumpPath, out error) + Path.DirectorySeparatorChar + "TestThreadsInfo.txt";
					sw = new StreamWriter(path);
					HashSet<ulong> locals = new HashSet<ulong>();
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
							if (!locals.Add(root.Object))
							{
								localAliveDups.Add(root);
								++dupLocals;
							}
							ClrType clrType = heap.GetObjectType(root.Object);
							sw.Write("    " + Utils.RealAddressStringHeader(root.Object));
							if (clrType != null)
								sw.WriteLine(clrType.Name);
							else
								sw.WriteLine();
						}

						sw.WriteLine("    dead locals");
						for (int j = 0, jcnt = threadLocalDeadVars[i].Length; j < jcnt; ++j)
						{
							ClrRoot root = threadLocalDeadVars[i][j];
							if (!locals.Add(root.Object))
							{
								localDeadDups.Add(root);
								++dupLocals;
							}
							ClrType clrType = heap.GetObjectType(root.Object);
							sw.Write("    " + Utils.RealAddressStringHeader(root.Object));
							if (clrType != null)
								sw.WriteLine(clrType.Name);
							else
								sw.WriteLine();
						}
						for (int j = 0, jcnt = threadFrames[i].Length; j < jcnt; ++j)
						{
							ClrStackFrame fr = threadFrames[i][j];
							if (fr.Method != null)
							{
								string fullSig = fr.Method.GetFullSignature();
								if (fullSig == null)
									fullSig = fr.Method.Name;
								if (fullSig == null) fullSig = "UNKNOWN";
								sw.WriteLine("  " + Utils.RealAddressStringHeader(fr.InstructionPointer)
												+ Utils.RealAddressStringHeader(fr.Method.NativeCode)
												+ fullSig);

							}
							else
							{
								sw.WriteLine("  METHOD UKNOWN");
							}
							if (!string.IsNullOrEmpty(fr.DisplayString))
								sw.WriteLine("  " + fr.DisplayString);
							else
								sw.WriteLine("  ???");
						}
					}

					var localAliveDupsAry = localAliveDups.ToArray();
					var localDeadDupsAry = localDeadDups.ToArray();



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

		#endregion threads

		#region open dump

		public static ClrtDump OpenDump(int indexNdx = 0)
		{
			string error;
			var path = Setup.GetRecentAdhocPath(indexNdx);
			Assert.IsNotNull(path, "Setup returned null when asked for index " + indexNdx + ".");
			var clrDump = new ClrtDump(path);
			var initOk = clrDump.Init(out error);
			Assert.IsTrue(initOk, "ClrtDump.Init failed for dump: " + path + Environment.NewLine + error);
			return clrDump;
		}

		public static ClrtDump OpenDump(string path)
		{
			string error;
			var clrDump = new ClrtDump(path);
			bool initOk = clrDump.Init(out error);
			Assert.IsTrue(initOk, "ClrtDump.Init failed for dump: " + path + Environment.NewLine + error);
			return clrDump;
		}

		#endregion open dump
	}
}
