using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;
using ClrMDRIndex;
using ClrMDRUtil.Utils;
using DmpNdxQueries;
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace UnitTestMdr
{
	[TestClass]
	public class DumpTests
	{
		#region Fields/Properties

		// legal values for m_state
		private const int CANNOT_BE_CANCELED = 0;
		private const int NOT_CANCELED = 1;
		private const int NOTIFYING = 2;
		private const int NOTIFYINGCOMPLETE = 3;

		private const string _dumpPath = @"C:\WinDbgStuff\Dumps\TestApp\TestApp.exe_161118_202111.dmp";


		#endregion Fields/Properties

		#region TestContext/Initialization

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

		#endregion TestContext/Initialization

		#region Misc

		private static string GetFieldTypeKey(ClrType clrType, ClrInstanceField fld)
		{
			return fld.Name + Constants.HeavyGreekCross + fld.Type.Name + Constants.HeavyGreekCross + clrType.Name;
		}

		[TestMethod]
		public void TestMisc()
		{

			var c1 = Utils.NumberOfDigits(0);
			c1 = Utils.NumberOfDigits(5);
			c1 = Utils.NumberOfDigits(10);
			c1 = Utils.NumberOfDigits(15);
			c1 = Utils.NumberOfDigits(99);
			c1 = Utils.NumberOfDigits(100);
			c1 = Utils.NumberOfDigits(100000000);

			BinaryWriter bw =
				new BinaryWriter(
					File.Open(@"D:\Jerzy\WinDbgStuff\dumps\DumpSearch\DumpSearch.exe_160711_121816.map\Test.map",
						FileMode.Create));
			bw.Write((int)10);
			for (int i = 0; i < 10; ++i)
			{
				bw.Write((ulong)(i + 1));
			}
			bw.Close();
			bw = null;

			ulong uval = UInt64.Parse("1,456", NumberStyles.AllowThousands);

			ulong val = 0x0000a2d4fd7ee0;
			object obj = val;
			var str = obj.ToString();

			var maxDt = DateTime.MaxValue;
			var maxDtStr = maxDt.ToShortDateString();
			var minDt = DateTime.MinValue;
			var minDtStr = minDt.ToShortDateString();

			int[] items0 = new[] { 2, 3, 4, 4, 5, 8, 8, 9, 10, 10, 12, 15, 15, 16, 16, 20, 25, 25 };
			var iheap0 = new IntervalHeap<int>(50);
			for (int i = 0; i < items0.Length; ++i)
			{
				iheap0.Insert(items0[i]);
				var view = iheap0.Dump();
			}
			var v0 = iheap0.Dump();

			int[] items = new[] { 2, 20, 8, 10, 4, 7 };
			var iheap = new IntervalHeap<int>(50);
			for (int i = 0; i < items.Length; ++i)
			{
				iheap.Insert(items[i]);
				var view = iheap.Dump();
			}
			var v = iheap.Dump();

			int[] items2 = new[] { 2, 4, 8, 10, 7, 20 };
			var iheap2 = new IntervalHeap<int>(50);
			for (int i = 0; i < items2.Length; ++i)
			{
				iheap2.Insert(items2[i]);
				var view = iheap2.Dump();
			}
			var v2 = iheap2.Dump();

		}


		[TestMethod]
		public void TestThisLocking()
		{
			var lou = new Lou();

			var t1 = new Thread(lou.LockingMethod);
			t1.Start();
			Thread.Sleep(1000);
			var t2 = new Thread(lou.RegularMethod);
			t2.Start();

			t2.Join();
			t1.Join();
		}


		[TestMethod]
		public void TestUtilsMethods()
		{
			string str = "[3498] ";
			int val = Utils.ConvertToInt(str, 1, 5);
			Assert.IsTrue(val == 3498);
		}


		[TestMethod]
		public void TestPatternCount()
		{
			int caseId = 1;
			int lineCount = 0;
			int count = 0;
			const string pattern0 = " {\"00";
			const string pattern1 = " [    -1]  ";
			const string path0 = @"C:\Users\jdomi\AppData\Local\Temp\HEA7E97.tmp";
			const string path1 =
				@"C:\WinDbgStuff\Dumps\Analytics\Baly\analytics9_1512161604.good.map\ad-hoc.queries\analytics9_1512161604.good.dmp.TYPEINFOS0.txt";
			StreamReader rd = null;

			try
			{
				switch (caseId)
				{
					case 0:
						rd = new StreamReader(path0);
						break;
					case 1:
						rd = new StreamReader(path1);
						break;

				}


				string ln;
				while ((ln = rd.ReadLine()) != null)
				{
					++lineCount;
					//                   

					switch (caseId)
					{
						case 0:
							if (ln.StartsWith(pattern0, StringComparison.Ordinal)) ++count;
							break;
						case 1:
							if (ln.Contains(pattern1)) ++count;
							break;

					}
				}

				testContextInstance.WriteLine("Total number of pattern lines: " + count + ", line count: " + lineCount);
			}
			catch (Exception ex)
			{
				Assert.IsTrue(false, Utils.GetExceptionErrorString(ex));
			}
			finally
			{
				rd?.Close();
			}


		}

		[TestMethod]
		public void TestArraySorting()
		{

			ulong[] addrs = new ulong[]
			{
				0x00000000000000,
				0x00009fd480cf70,
				0x007ffb939d49c0,
				0x007ffb939d4a78,
				0x007ffb939d4b30,
				0x007ffb939db130,
				0x007ffb92540f10,
				0x007ffb9254971,
				0x007ffb92540bc8,
				0x007ffb939f3310,
				0x007ffb91a376b0,
				0x007ffb92357370,
				0x007ffb91a377e8,
				0x007ffb91a37860,
				0x007ffb939db250,
				0x007ffb939db190,
				0x007ffb939db1f0,
				0x007ffb96a11a20
			};

			string[] names = new string[]
			{
				"A_00000000000000",
				"B_00009fd480cf70",
				"C_007ffb939d49c0",
				"D_007ffb939d4a78",
				"E_007ffb939d4b30",
				"F_007ffb939db130",
				"G_007ffb92540f10",
				"H_007ffb9254971",
				"I_007ffb92540bc8",
				"J_007ffb939f3310",
				"L_007ffb91a376b0",
				"M_007ffb92357370",
				"N_007ffb91a377e8",
				"O_007ffb91a37860",
				"P_007ffb939db250",
				"R_007ffb939db190",
				"S_007ffb939db1f0",
				"T_007ffb96a11a20"
			};

			int[] map = new int[names.Length];
			Utils.Iota(map);
			ulong[] origAddrs = new ulong[names.Length];
			Array.Copy(addrs, origAddrs, addrs.Length);
			Array.Sort(addrs, map);

			string[] outStrings = new string[names.Length];

			for (int i = 0, icnt = names.Length; i < icnt; ++i)
			{
				int ndx = Array.BinarySearch(addrs, origAddrs[i]);
				outStrings[i] = names[map[ndx]];
			}
		}

		[TestMethod]
		public void TestAppDomains()
		{
			var clrDump = GetDump();
			using (clrDump)
			{
				var runtime = clrDump.Runtimes[0];
				var result = FQry.warmupHeap(runtime);
				Assert.IsTrue(result.Item1 == null, result.Item1);

				List<string> appDomainNames = new List<string>(16);
				foreach (var appDomain in runtime.AppDomains)
				{
					var name = appDomain.Name;
					appDomainNames.Add(name);
				}
				Assert.IsTrue(appDomainNames.Count > 0, "App domains list is empty!");
			}
		}

		[TestMethod]
		public async Task TestTypeMethodTables()
		{
			ClrtDump clrtDump = null;
			Tuple<string, SortedDictionary<KeyValuePair<string, ulong>, int>> result = null;
			var startDt = DateTime.UtcNow;
			result = await Task.Run(() =>
			{
				clrtDump = GetDump();
				using (clrtDump)
				{
					var runtime = clrtDump.Runtimes[0];
					result = FQry.getTypeWithMethodTables(runtime.Heap);
					Assert.IsTrue(result.Item1 == null, result.Item1);
					var tmSpan = DateTime.UtcNow - startDt;

					StreamWriter wr = null;
					try
					{
						wr = new StreamWriter(TestConfiguration.OutPath0 + "TypesMethodTables.txt");
						wr.WriteLine("### duration: " + tmSpan.Hours + ":" + tmSpan.Minutes + ":" + tmSpan.Seconds);
						wr.WriteLine("### method table [instance count] type name");
						foreach (var kv in result.Item2)
						{
							wr.Write(Auxiliaries.fullAddressString(kv.Key.Value));
							wr.Write(" [" + Auxiliaries.sortableLengthString((ulong)kv.Value) + "] ");
							wr.WriteLine(kv.Key.Key);
						}
					}
					finally
					{
						wr?.Close();
					}
				}
				return result;
			});
			Assert.IsTrue(result != null);
			Assert.IsTrue(result.Item1 == null);
		}

		[TestMethod]
		public void TestGetInterfaceObjects()
		{
			string error = null;
			var clrDump = GetDump();
			StreamWriter wr = null;
			using (clrDump)
			{
				try
				{
					var runtime = clrDump.Runtimes[0];
					var result = FQry.getInterfaceObjects(runtime.Heap, "System.ServiceModel.ICommunicationObject");
					Assert.IsNull(result.Item1, result.Item1);
					var path = TestConfiguration.OutPath0 + "System.ServiceModel.ICommunicationObject.objects.txt";
					wr = new StreamWriter(path);
					foreach (var kv in result.Item2)
					{
						wr.WriteLine(kv.Key);
						wr.WriteLine("   [" + kv.Value.Count + "]");
						for (int i = 0, icnt = kv.Value.Count; i < icnt; ++i)
						{
							if (i == 0) wr.Write("   ");
							if (i > 0 && (i % 5) == 0)
							{
								wr.WriteLine();
								wr.Write("   ");
							}
							else
							{
								wr.Write(Utils.AddressString(kv.Value[i]) + ", ");
							}
						}
						wr.WriteLine();
					}
					wr.Close();
					wr = null;
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
				finally
				{
					wr?.Close();
				}
			}
		}

		[TestMethod]
		public void TestGetNamespaceObjects()
		{
			string error = null;
			var clrDump = GetDump();
			StreamWriter wr = null;
			using (clrDump)
			{
				try
				{
					var runtime = clrDump.Runtimes[0];
					var result = FQry.getNamespaceObjects(runtime.Heap, "System.ServiceModel.");
					Assert.IsNull(result.Item1, result.Item1);
					var path = TestConfiguration.OutPath0 + "System.ServiceModel.Namespace.objects.txt";
					wr = new StreamWriter(path);
					SortedDictionary<string, List<ulong>> dct;
					for (int d = 0; d < 3; ++d)
					{
						if (d == 0)
							dct = d == 0 ? result.Item2 : (d == 1 ? result.Item3 : result.Item4);

						foreach (var kv in result.Item2)
						{
							wr.WriteLine(kv.Key);
							wr.WriteLine("   [" + kv.Value.Count + "]");
							for (int i = 0, icnt = kv.Value.Count; i < icnt; ++i)
							{
								if (i == 0) wr.Write("   ");
								if (i > 0 && (i % 5) == 0)
								{
									wr.WriteLine();
									wr.Write("   ");
								}
								else
								{
									wr.Write(Utils.AddressString(kv.Value[i]) + ", ");
								}
							}
							wr.WriteLine();
						}
					}
					wr.Close();
					wr = null;
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
				finally
				{
					wr?.Close();
				}
			}
		}

		[TestMethod]
		public void TestArraySizes()
		{
			string error = null;
			string arrayTypeName = "System.Threading.CancellationTokenRegistration[]";
			var sizes = new List<int>(10000);
			var values = new List<object[]>(10000);

			using (var clrDump = GetDump())
			{
				try
				{
					var aryTypeIsKnown = false;
					var fields = new List<KeyValuePair<string, ClrInstanceField>>();
					var fieldsCnt = -1;
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType == null || clrType.Name != arrayTypeName) goto NEXT_OBJECT;
							if (!aryTypeIsKnown)
							{
								ClrType aType = clrType.ComponentType;
								for (int j = 0, jcnt = aType.Fields.Count; j < jcnt; ++j)
								{
									fields.Add(new KeyValuePair<string, ClrInstanceField>(aType.Fields[j].Name,
										aType.Fields[j]));
								}
								fieldsCnt = fields.Count;
								aryTypeIsKnown = true;
							}

							var sz = clrType.GetArrayLength(addr);
							sizes.Add(sz);
							for (int j = 0; j < sz; ++j)
							{
								var aryElemAddr = clrType.GetArrayElementAddress(addr, j);
								var vals = new object[fieldsCnt];
								var allNull = true;
								for (int f = 0; f < fieldsCnt; ++f)
								{
									vals[f] = fields[f].Value.GetValue(aryElemAddr);
									if (vals[f] != null) allNull = false;
								}
								if (!allNull) values.Add(vals);
							}

							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

		[TestMethod]
		public void TestIntArrayMapping()
		{
			int[] ary = new[] { 2, 1, 2, 1, 0, 2, 2, 0, 2, 2, 1, 1, 3, 2, 3 };
			int[] offs;
			var map = Utils.GetIntArrayMapping(ary, out offs);
			for (int i = 0; i < offs.Length - 1; ++i)
			{
				var outAry = Utils.GetIdArray(i, ary, map, offs);
				Assert.IsTrue(AllValuesEqual(outAry, i));
			}
		}

		bool AllValuesEqual(int[] ary, int val)
		{
			for (int i = 0; i < ary.Length - 1; ++i)
			{
				if (ary[i] != val) return false;

			}
			return true;
		}

		#endregion Misc

		[TestMethod]
		public void TestRootedInfo2()
		{
			Stopwatch stopWatch = new Stopwatch();
			//string dmpPath = @"D:\Jerzy\WinDbgStuff\dumps\TestApp\TestApp.exe_161031_093521.dmp";
			//string dmpPath = @"C:\WinDbgStuff\dumps\Analytics\ConvergeEx\Analytics_Post.dmp";
			string dmpPath = @"D:\Jerzy\WinDbgStuff\Dumps\Analytics\Highline\analyticsdump111.dlk.dmp";
			
			string error = null;
			var errors = new ConcurrentBag<string>();
			// open crush dump
			//
			stopWatch.Start();
			var clrtDump = GetDump(dmpPath, out error);
			Assert.IsNull(error,error);
			using (clrtDump)
			{
				try
				{
					var runtime = clrtDump.Runtimes[0];
					var heap = runtime.Heap;
					var getOpenDumpDuration = Utils.StopAndGetDurationString(stopWatch);

					DumpFileMoniker fileMoniker = new DumpFileMoniker(clrtDump.DumpPath);

					// get heap address count
					//
					stopWatch.Restart();
					var addrCount = DumpIndexer.GetHeapAddressCount(heap);
					var getAddressCountDuration = Utils.StopAndGetDurationString(stopWatch);

					// get type names
					//
					stopWatch.Restart();
					string[] typeNames = DumpIndexer.GetTypeNames(heap, out error);
					Assert.IsTrue(error == null, error);
					var getTypeDuration = Utils.StopAndGetDurationString(stopWatch);

					stopWatch.Restart();
					var rtmFinQue = runtime.EnumerateFinalizerQueueObjectAddresses().ToArray();
					Assert.IsNull(error, error);
					var getRunTmFinQueDuration = Utils.StopAndGetDurationString(stopWatch);

					stopWatch.Restart();
					var finalizableScan = ClrtDump.GetFinalizableInstances(heap, out error);
					Assert.IsNull(error, error);
					var getFinalizableDuration = Utils.StopAndGetDurationString(stopWatch);

					// get roots
					//
					StringIdDct strIds = new StringIdDct();
					stopWatch.Restart();
					var rootAddresses = ClrtRootInfo.GetRootAddresses(0, clrtDump.Runtimes[0], heap, typeNames, strIds, fileMoniker, out error);
					Assert.IsNull(error, error);
					ClrtRootInfo clrtRootInfo = ClrtRootInfo.Load(0,fileMoniker,out error);
					Assert.IsNull(error, error);
					var getRootsDuration = Utils.StopAndGetDurationString(stopWatch);

					// get heap finalize queue
					//
					stopWatch.Restart();
					var finQue = ClrtDump.GetFinalizeQue(heap, out error);
					var getHeapFinQueDuration = Utils.StopAndGetDurationString(stopWatch);


					TestContext.WriteLine("#### DUMP: " + fileMoniker.DumpFileName);
					TestContext.WriteLine("#### DURATIONS:");
					TestContext.WriteLine("open dump:         " + getOpenDumpDuration);
					TestContext.WriteLine("address count:     " + getAddressCountDuration);
					TestContext.WriteLine("type names:        " + getTypeDuration);
					TestContext.WriteLine("runtime fin queue: " + getRunTmFinQueDuration);
					TestContext.WriteLine("finalizable:       " + getFinalizableDuration);
					TestContext.WriteLine("roots:             " + getRootsDuration);
					TestContext.WriteLine("heap fin queue:    " + getHeapFinQueDuration);


					TestContext.WriteLine("#### COUNTS:");
					TestContext.WriteLine("instances:              " + Utils.CountString(addrCount));
					TestContext.WriteLine("typeNames:              " + Utils.CountString(typeNames.Length));
					TestContext.WriteLine("rtm fin. queue:         " + Utils.CountString(rtmFinQue.Length));
					TestContext.WriteLine("finalizable total:      " + Utils.CountString(finalizableScan.Item1.Length));
					TestContext.WriteLine("finalizable suppressed: " + Utils.CountString(finalizableScan.Item2.Length));
					TestContext.WriteLine("roots unique address:   " + Utils.CountString(rootAddresses.Item1.Length));
					TestContext.WriteLine("heap fin queue:         " + Utils.CountString(finQue.Length));

				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

		//[TestMethod]
		//public void TestInstanceReferences()
		//{

		//	string error;
		//	var version = Assembly.GetExecutingAssembly().GetName().Version;
		//	Stopwatch stopWatch = new Stopwatch();
		//	string dmpPath = @"D:\Jerzy\WinDbgStuff\dumps\TestApp\TestApp.exe_161031_093521.dmp";
		//	//string dmpPath = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\ConvergEx\Analytics_Post.dmp";
		//	var index = DumpIndex.OpenIndexInstanceReferences(version, dmpPath, 0, out error);
		//	Assert.IsNotNull(index);
		//	ulong addr1 = 0x00000000029ff798;
		//	ulong addr2 = 0x0000000002a25398;
		//	using (index)
		//	{
		//		var typeName1 = index.GetTypeName(addr1);
		//		var result1 = index.GetReferences(addr1, out error);
		//		var typeName2 = index.GetTypeName(addr2);
		//		var result2 = index.GetReferences(addr2, out error);
		//		var details = index.GetParentDetails(addr2, out error);
		//		var refrences = index.GetParentReferences(addr2, out error);
		//	}
		//}

		[TestMethod]
		public void TestInstanceAddresses()
		{
			string error;
			var version = Assembly.GetExecutingAssembly().GetName().Version;
			Stopwatch stopWatch = new Stopwatch();
			string dmpPath = @"D:\Jerzy\WinDbgStuff\dumps\TestApp\TestApp.exe_161031_093521.dmp";
			//string dmpPath = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\ConvergEx\Analytics_Post.dmp";
			var index = DumpIndex.OpenIndexInstanceReferences(version, dmpPath, 0, out error);
			Assert.IsNotNull(index);

			using (index)
			{
				var instances = index.Instances;
				Utils.WriteAddressAsStringArray(index.AdhocFolder + Path.DirectorySeparatorChar + "AddressList.txt", instances,
					out error);
			}
		}


		#region Memory

		[TestMethod]
		public void TestMemoryRegions()
		{
			string error = null;
			using (var clrDump = GetDump())
			{
				try
				{
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					List<KeyValuePair<ClrMemoryRegion, string>> lst = new List<KeyValuePair<ClrMemoryRegion, string>>(512);
					foreach (var mr in runtime.EnumerateMemoryRegions())
					{
						lst.Add(new KeyValuePair<ClrMemoryRegion, string>(mr, mr.ToString(true)));
					}
					int cnt = lst.Count;

				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

		[TestMethod]
		public void TestLOHFragmentation()
		{
			string error = null;
			using (var clrDump = GetDump(_dumpPath))
			{
				var runtime = clrDump.Runtimes[0];
				var heap = runtime.Heap;
				var resultOk = DumpIndex.GetLohInfo(runtime, out error);
				Assert.IsTrue(resultOk);
			}
		}

		//[TestMethod]
		//public void TestTypeSizeHierarchy()
		//{
		//	//ulong rootAddr = 0x0000a12d115ba8;
		//	//ulong rootAddr = 0x0000a12d116110;
		//	ulong rootAddr = 0x0000a12d116320;
		//	string error = null;
		//	using (var clrDump = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Baly\AnalyticsLatencyDump06062016 03354291.dmp"))
		//	{
		//		var runtime = clrDump.Runtimes[0];

		//		InstanceSizeNode root;
		//		ulong totalSize;
		//		var ok = ClrtDump.GetInstanceSizeHierarchy(runtime, rootAddr, out root, out totalSize, out error);
		//		Assert.IsTrue(ok);
		//	}
		//}

		#endregion Memory

		#region FinalizerQueue

		[TestMethod]
		public void TestCompareFinalizableLists()
		{
			string error = null;

			List<ulong> sosFinLst = new List<ulong>(2000000);
			StreamReader sr = null;
			var path =
				@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Baly\AnalyticsLatencyDump06062016 03354291.map\ad-hoc.queries\sosFinalizerQueueAllReady.txt";
			try
			{
				sr = new StreamReader(path);
				string ln;
				while ((ln = sr.ReadLine()) != null)
				{
					if (ln.StartsWith("Finalizable but not rooted:  "))
					{
						var begin = "Finalizable but not rooted:  ".Length;
						var end = Utils.SkipNonWhites(ln, begin);
						while (end > begin)
						{
							var substr = ln.Substring(begin, end - begin);
							sosFinLst.Add(Convert.ToUInt64(substr, 16));
							begin = Utils.SkipWhites(ln, end);
							end = Utils.SkipNonWhites(ln, begin);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Assert.IsTrue(false, ex.ToString());
			}
			finally
			{
				sr?.Close();
			}
			ulong[] sosFinAry = sosFinLst.ToArray();
			sosFinLst.Clear();
			sosFinLst = null;

			using (var clrDump = GetDump())
			{
				try
				{
					var runtime = clrDump.Runtimes[0];
					List<ulong> runtmFinQueLst = new List<ulong>(2000000);
					foreach (var addr in runtime.EnumerateFinalizerQueueObjectAddresses())
					{
						runtmFinQueLst.Add(addr);
					}
					ulong[] runtmFinQueAry = runtmFinQueLst.ToArray();
					runtmFinQueLst.Clear();
					runtmFinQueLst = null;
					runtime.Flush();

					var heap = runtime.Heap;
					List<ulong> heapFinQueLst = new List<ulong>(2000000);
					foreach (var addr in heap.EnumerateFinalizableObjectAddresses())
					{
						heapFinQueLst.Add(addr);
					}
					ulong[] heapFinQueAry = heapFinQueLst.ToArray();
					heapFinQueLst.Clear();
					heapFinQueLst = null;

					List<ulong> heapFinRootLst = new List<ulong>(2000000);
					foreach (var root in heap.EnumerateRoots())
					{
						if (root.Kind == GCRootKind.Finalizer)
							heapFinRootLst.Add(root.Object);
					}
					ulong[] heapFinRootAry = heapFinRootLst.ToArray();
					heapFinRootLst.Clear();
					heapFinRootLst = null;

					runtime.Flush();
					heap = null;

					Array.Sort(runtmFinQueAry);
					Array.Sort(heapFinQueAry);
					Array.Sort(heapFinRootAry);
					Array.Sort(sosFinAry);

					heap = runtime.Heap;
					SortedDictionary<string, List<ulong>> sosHistogramDct = new SortedDictionary<string, List<ulong>>(StringComparer.Ordinal);
					for (int i = 0, icnt = sosFinAry.Length; i < icnt; ++i)
					{
						var clrType = heap.GetObjectType(sosFinAry[i]);
						List<ulong> lst;
						if (sosHistogramDct.TryGetValue(clrType.Name, out lst))
						{
							lst.Add(sosFinAry[i]);
							continue;
						}
						sosHistogramDct.Add(clrType.Name, new List<ulong>(32) { sosFinAry[i] });
					}

					var sosHistogram = Utils.GetHistogram<ulong>(sosHistogramDct);



				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}


		#endregion  FinalizerQueue

		#region Roots/Statics

		/// <summary>
		/// TODO JRD -- redo
		/// </summary>
		//[TestMethod]
		//public void TestStaticFields()
		//{
		//	string error = null;
		//	StreamWriter wr = null;
		//	var clrDump = GetDump();
		//	var map = OpenMap0();
		//	Assert.IsNotNull(map);
		//	byte[] byteBuffer = new byte[32 * 1024];
		//	byte[] lenBytes = new byte[4];
		//	int notFoundOnHeapCnt = 0;
		//	int valueClassOnHeapCnt = 0;
		//	StringIdAsyncDct idDct = new StringIdAsyncDct();
		//	using (clrDump)
		//	{
		//		try
		//		{
		//			ulong[][] instances;
		//			uint[][] sizes;
		//			int[][] instanceTypes;
		//			StringIdAsyncDct[] stringIds = StringIdAsyncDct.GetArrayOfDictionaries(clrDump.RuntimeCount);
		//			ConcurrentBag<string>[] errors = new ConcurrentBag<string>[clrDump.RuntimeCount];
		//			for (int i = 0; i < clrDump.RuntimeCount; ++i)
		//				errors[i] = new ConcurrentBag<string>();
		//			var typeInfos = Indexer.GetTypeInfos(clrDump, null, stringIds, errors, out instances, out sizes, out instanceTypes, out error);
		//			Assert.IsNotNull(typeInfos, error);

		//			var path = TestConfiguration.OutPath0 + clrDump.DumpFileName + ".STATICVARS.map";
		//			wr = new StreamWriter(path);
		//			var runtime = clrDump.Runtimes[0];
		//			var appDomains = Indexer.GetAppDomains(runtime, idDct);
		//			var names = idDct.GetNamesSortedById();
		//			var appDomainsCount = appDomains.AppDomainCount;
		//			wr.WriteLine("### ClrAppDomains " + appDomainsCount);
		//			wr.WriteLine("[" + Utils.AddressString(appDomains.SystemDomain.Address) + " " + names[appDomains.SystemDomain.NameId] + " {" + appDomains.SystemDomain.Id + "}");
		//			wr.WriteLine("[" + Utils.AddressString(appDomains.SharedDomain.Address) + " " + names[appDomains.SharedDomain.NameId] + " {" + appDomains.SharedDomain.Id + "}");
		//			for (int i = 0, icnt = appDomainsCount; i < icnt; ++i)
		//			{
		//				wr.WriteLine("[" + Utils.AddressString(appDomains[i].Address) + " " + names[appDomains[i].NameId] + " {" + appDomains[i].Id + "}");
		//			}
		//			var heap = runtime.Heap;
		//			var segs = heap.Segments;
		//			var doneSet = new HashSet<string>();
		//			wr.WriteLine("### ClrTypes and static fiels.");
		//			for (int i = 0, icnt = segs.Count; i < icnt; ++i)
		//			{
		//				var seg = segs[i];
		//				ulong addr = seg.FirstObject;
		//				while (addr != 0ul)
		//				{
		//					var clrType = heap.GetObjectType(addr);
		//					if (clrType == null || clrType.StaticFields.Count < 1) goto NEXT_OBJECT;
		//					if (!doneSet.Add(clrType.Name)) goto NEXT_OBJECT;
		//					bool isParentStruct = false;
		//					if (clrType.IsValueClass)
		//					{
		//						isParentStruct = true;
		//						++valueClassOnHeapCnt;
		//					}

		//					wr.WriteLine(Utils.AddressString(addr) + " " + string.Format("{0,08}", clrType.StaticFields.Count)
		//						+ " " + clrType.ElementType + " " + clrType.Name);
		//					for (int j = 0, jcnt = clrType.StaticFields.Count; j < jcnt; ++j) // skipping system and shared domains
		//					{
		//						var staticField = clrType.StaticFields[j];
		//						var staticFieldType = staticField.Type;
		//						Assert.IsNotNull(staticFieldType);
		//						wr.WriteLine("   " + staticField.Name + " " + staticField.Type.ElementType + " " + staticField.Type.Name);
		//						for (int k = 2, kcnt = appDomainsCount; k < kcnt; ++k)
		//						{
		//							string valAsStr = Constants.NullName;
		//							string addrAsStr = Constants.ZeroAddressStr;
		//							object value = null;
		//							var appDomain = appDomains[k];
		//							var isInitialized = false; //staticField.IsInitialized(appDomain);
		//							if (isInitialized)
		//							{
		//								value = null; //staticField.GetValue(appDomain);
		//								if (value != null)
		//								{
		//									if (staticFieldType.ElementType == ClrElementType.String)
		//									{
		//										ulong oaddr = (ulong)value;
		//										addrAsStr = Utils.AddressString(oaddr);
		//										valAsStr = ClrMDRIndex.ValueExtractor.GetStringAtAddress(oaddr, heap);
		//									}
		//									else if (staticFieldType.ElementType == ClrElementType.Object)
		//									{
		//										try
		//										{
		//											ulong oaddr = (ulong)value;
		//											addrAsStr = Utils.AddressString(oaddr);
		//											if (oaddr == 0UL)
		//											{
		//												valAsStr = Constants.NullName;
		//											}
		//											else
		//											{
		//												var oid = map.GetInstanceIdAtAddr(oaddr);
		//												if (oid == Constants.InvalidIndex)
		//												{
		//													valAsStr = "{not found on heap}";
		//													++notFoundOnHeapCnt;
		//												}
		//												else
		//												{
		//													valAsStr = addrAsStr;
		//												}

		//											}
		//										}
		//										catch (InvalidCastException icex)
		//										{
		//											valAsStr = "{ulong invalid cast}";
		//										}
		//									}
		//									else if (staticFieldType.ElementType == ClrElementType.Struct)
		//									{
		//										ulong oaddr = (ulong)value;
		//										addrAsStr = Utils.AddressString(oaddr);
		//										if (oaddr == 0UL)
		//										{
		//											valAsStr = Constants.NullName;
		//										}
		//										else
		//										{
		//											switch (staticFieldType.Name)
		//											{
		//												case "System.Decimal":
		//													valAsStr = ClrMDRIndex.ValueExtractor.GetDecimalValue(oaddr, staticFieldType, null);
		//													break;
		//												case "System.DateTime":
		//													valAsStr = ClrMDRIndex.ValueExtractor.GetDateTimeValue(oaddr, staticFieldType);
		//													break;
		//												case "System.TimeSpan":
		//													valAsStr = ClrMDRIndex.ValueExtractor.GetTimeSpanValue(oaddr, staticFieldType);
		//													break;
		//												case "System.Guid":
		//													valAsStr = ClrMDRIndex.ValueExtractor.GetGuidValue(oaddr, staticFieldType);
		//													break;
		//												default:
		//													valAsStr = addrAsStr + " " + staticFieldType.Name;
		//													break;
		//											}
		//										}
		//									}
		//								}

		//							}
		//							else
		//							{
		//								valAsStr = "{not initialized}";
		//							}
		//							wr.WriteLine("      " + addrAsStr + " " + valAsStr);

		//						}
		//					}

		//					NEXT_OBJECT:
		//					addr = seg.NextObject(addr);
		//				}
		//			}
		//			wr.Write("### Not found on heap count: " + notFoundOnHeapCnt);
		//			wr.Write("### Value class on heap count: " + valueClassOnHeapCnt);
		//		}
		//		catch (Exception ex)
		//		{
		//			error = Utils.GetExceptionErrorString(ex);
		//			Assert.IsTrue(false, error);
		//		}
		//		finally
		//		{
		//			wr?.Close();
		//		}
		//	}
		//}


		#endregion Roots/Statics

		#region Strings, Arrays, Dictionaries, etc...

		[TestMethod]
		public void TestTypeStringFields()
		{
			string typeName = @"ECS.Common.HierarchyCache.Structure.RealPosition";
			string error = null;
			using (var clrDump = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\DupCacheIssue\A2_noDF.dmp"))
			{
				List<ulong> addrLst = new List<ulong>(1024);
				try
				{
					var heap = clrDump.GetFreshHeap();
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType == null) goto NEXT_OBJECT;
							if (!Utils.SameStrings(typeName, clrType.Name)) goto NEXT_OBJECT;
							addrLst.Add(addr);

							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}

					heap = clrDump.GetFreshHeap();
					ClrtDump.GetTypeStringUsage(heap, addrLst.ToArray(), out error);

				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}


		[TestMethod]
		public void TestGetEmptyStrings()
		{
			var t1 = string.IsNullOrEmpty("  ");
			var t2 = string.IsNullOrWhiteSpace("   ");
			var t3 = string.IsNullOrWhiteSpace(" \r\n");

			string error = null;
			using (var clrDump = GetDump())
			{
				SortedDictionary<string, List<ulong>> dct = new SortedDictionary<string, List<ulong>>();
				try
				{
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType == null || !clrType.IsString) goto NEXT_OBJECT;
							var str = ClrMDRIndex.ValueExtractor.GetStringAtAddress(addr, heap);
							if (Utils.IsSpaces(str))
							{
								str = Utils.SortableSizeString(str.Length);
								List<ulong> lst;
								if (dct.TryGetValue(str, out lst))
								{
									lst.Add(addr);
									goto NEXT_OBJECT;
								}
								dct.Add(str, new List<ulong>() { addr });
							}

							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}
					StreamWriter wr = null;
					try
					{
						var path = TestConfiguration.OutPath0 + clrDump.DumpFileName + ".EMPTYSTRINGADDRESSES.txt";
						wr = new StreamWriter(path);
						foreach (var kv in dct)
						{
							wr.WriteLine("### " + kv.Key + "  [" + Utils.SizeString(kv.Value.Count) + "]");
							wr.Write(Utils.AddressString(kv.Value[0]));
							for (int i = 1, icnt = kv.Value.Count; i < icnt; ++i)
							{
								wr.Write(", " + Utils.AddressString(kv.Value[i]));
							}
							wr.WriteLine();
						}
						wr.Close();
						wr = null;

						path = TestConfiguration.OutPath0 + clrDump.DumpFileName + ".SPACESONLYSTRINGS.txt";
						wr = new StreamWriter(path);
						wr.WriteLine("Dump: " + clrDump.DumpFileName);
						wr.WriteLine();
						wr.WriteLine("Space ' ' only strings, length (Unicode char count) and instance count.");
						wr.WriteLine();
						foreach (var kv in dct)
						{
							wr.WriteLine(" " + kv.Key + "    [" + Utils.SizeString(kv.Value.Count) + "]");
						}
						wr.Close();
						wr = null;


					}
					finally
					{
						wr?.Close();
					}


				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}


		[TestMethod]
		public void TestAddressesOfString()
		{
			string str = @"PULSE\TAVCOLUMN";
			string error = null;
			using (var clrDump = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\Eze.Analytics.O971_160825_163744.AfterToUpper.dmp"))
			{
				List<ulong> addrLst = new List<ulong>(1024);
				try
				{
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType == null || !clrType.IsString) goto NEXT_OBJECT;
							var stVal = ClrMDRIndex.ValueExtractor.GetStringAtAddress(addr, heap);
							if (Utils.SameStrings(stVal, str))
							{
								addrLst.Add(addr);
							}

							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}
					StreamWriter wr = null;
					try
					{
						Tuple<string, string> dmpFolders = DumpFileMoniker.GetAndCreateMapFolders(clrDump.DumpPath, out error);

						var path = dmpFolders.Item2 + @"\PULSE_TAVCOLUMN_ADDRESSES.txt";

						wr = new StreamWriter(path);
						wr.WriteLine("### \"" + str + "\" [" + addrLst.Count + "]");
						for (int i = 0, icnt = addrLst.Count; i < icnt; ++i)
						{
							wr.WriteLine(Utils.AddressString(addrLst[i]));
						}
						wr.Close();
						wr = null;
					}
					finally
					{
						wr?.Close();
					}
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

		//[TestMethod]
		//public void TestGetArrayContent()
		//{
		//	var str1 = Utils.AddressString(0);
		//	var str2 = Utils.AddressString(427092);


		//	var dmp = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Lou\Eze.Analytics.App_161018_173251.dmp");
		//	//var dmp = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\TestApp\TestApp.exe_161010_075709.dmp");
		//	using (dmp)
		//	{
		//		var rntm = dmp.Runtime;
		//		var domains = rntm.AppDomains.ToArray();

		//		//ulong addr = 0x00000002cf7ba8;
		//		ulong addr = 0x00000013379d80;

		//		var clrSidekick = Types.getTypeSidekickAtAddress(dmp.Heap, addr);

		//		var aryValuesa = DmpNdxQueries.CollectionContent.getArrayContent(dmp.Heap, addr);
		//	}

		//}

		private bool NoNullEntries(string[] ary)
		{
			for (int i = 0, icnt = ary.Length; i < icnt; ++i)
			{
				if (ary[i] == null) return false;
			}
			return true;
		}



		//[TestMethod]
		//public void TestGetDecimalArrayContent()
		//{
		//	ulong aryAddr = 0x0002189f5af8b8;
		//	var dmp = GetDump(_dumpPath);
		//	using (dmp)
		//	{
		//		var heap = dmp.Heap;
		//		var result = CollectionContent.aryInfo(heap, aryAddr);
		//		Assert.IsNull(result.Item1, result.Item1);
		//		string[] strings = new string[result.Item4];
		//		for (int i = 0, icnt = result.Item4; i < icnt; ++i)
		//		{
		//			strings[i] = CollectionContent.aryElemDecimal(heap, aryAddr, result.Item2, result.Item3, i);
		//		}
		//		Assert.IsTrue(NoNullEntries(strings));
		//	}
		//}


		//[TestMethod]
		//public void TestGetArrayContent2()
		//{
		//	var str1 = Utils.AddressString(0);
		//	var str2 = Utils.AddressString(427092);


		//	//var dmp = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\TestApp\TestApp.exe_161021_104608.dmp");
		//	//var dmp = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\TestApp\TestApp.exe_161010_075709.dmp");
		//	var dmp = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\TestApp\TestApp.exe_161021_104608.dmp");
		//	using (dmp)
		//	{
		//		ulong dateTimeAryAddr = 0x29fe980;
		//		var clrSidekick1 = Types.getTypeSidekickAtAddress(dmp.Heap, dateTimeAryAddr);
		//		var aryValues1 = DmpNdxQueries.CollectionContent.getArrayContent(dmp.Heap, dateTimeAryAddr);

		//		ulong decimalAryAddr = 0x29feab8;
		//		var clrSidekick2 = Types.getTypeSidekickAtAddress(dmp.Heap, decimalAryAddr);
		//		var aryValues2 = DmpNdxQueries.CollectionContent.getArrayContent(dmp.Heap, decimalAryAddr);

		//		ulong guidAryAddr = 0x29fe9f0;
		//		var clrSidekick3 = Types.getTypeSidekickAtAddress(dmp.Heap, guidAryAddr);
		//		var aryValues3 = DmpNdxQueries.CollectionContent.getArrayContent(dmp.Heap, guidAryAddr);

		//		ulong timeSpanAryAddr = 0x29fe910;
		//		var clrSidekick4 = Types.getTypeSidekickAtAddress(dmp.Heap, timeSpanAryAddr);
		//		var aryValues4 = DmpNdxQueries.CollectionContent.getArrayContent(dmp.Heap, timeSpanAryAddr);

		//	}

		//}


		//[TestMethod]
		//public void TestTypeSidekick()
		//{

		//	//var dmp = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Presentation\Eze.Analytics.Svc.exe_161010_122402.dmp");
		//	var dmp = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\ConvergEx\Analytics.dmp");
		//	using (dmp)
		//	{
		//		var rntm = dmp.Runtime;
		//		var domains = rntm.AppDomains.ToArray();


		//		//ulong addr = 0x00000080c21010;
		//		ulong addr = 0x00008b6e9f0b08; // 0x00008b5eac3b88;
		//		var clrSidekick = Types.getTypeSidekickAtAddress(dmp.Heap, addr);
		//	}

		//}

		#endregion Strings, Arrays, Dictionaries, etc...

		#region CancellationToken Tests

		[TestMethod]
		public void TestCancellationTokenSource()
		{
			string error = null;
			List<string> valList = new List<string>(1024 * 4);
			using (var clrDump = GetDump())
			{
				try
				{
					var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType == null) goto NEXT_OBJECT;

							if (clrType.Name != "System.Threading.CancellationTokenSource") goto NEXT_OBJECT;

							sb.Clear();
							var m_state = (int)clrType.GetFieldByName("m_state").GetValue(addr);
							var m_statestr = CancellationTokenSourceStateStr(m_state);
							var m_disposed = (bool)clrType.GetFieldByName("m_disposed").GetValue(addr);
							var m_threadIDExecutingCallbacks = (int)clrType.GetFieldByName("m_threadIDExecutingCallbacks").GetValue(addr);
							var m_linkingRegistrations = clrType.GetFieldByName("m_linkingRegistrations");
							var m_linkingRegistrationsAddr = (ulong)m_linkingRegistrations.GetValue(addr);
							ClrType m_linkingRegistrationsType = m_linkingRegistrations.Type;
							if (m_linkingRegistrationsType == null)
								m_linkingRegistrationsType = heap.GetObjectType(m_linkingRegistrationsAddr);

							var m_linkingRegistrationsCount = m_linkingRegistrationsType != null
								? m_linkingRegistrationsType.GetArrayLength(m_linkingRegistrationsAddr)
								: -1;

							sb.Append("[").Append(Utils.AddressString(addr)).Append("] ")
								.Append(m_statestr).Append(" | ")
								.Append(m_disposed ? "disposed" : "not-disposed").Append(" | ")
								.Append(m_threadIDExecutingCallbacks).Append(" | ")
								.Append("m_linkingRegistrations: " + m_linkingRegistrationsCount);
							valList.Add(sb.ToString());



							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}

					StringBuilderCache.Release(sb);
					var path = TestConfiguration.OutPath0 + clrDump.DumpFileName + ".CANCELTOKENSOURCE.txt";
					Utils.WriteStringList(path, valList, out error);

				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

		private string CancellationTokenSourceStateStr(int st)
		{
			switch (st)
			{
				case CANNOT_BE_CANCELED:
					return "CANNOT_BE_CANCELED";
				case NOT_CANCELED:
					return "NOT_CANCELED";
				case NOTIFYING:
					return "NOTIFYING";
				case NOTIFYINGCOMPLETE:
					return "NOTIFYINGCOMPLETE";
				default:
					return "UNKNOWN";
			}
		}

		[TestMethod]
		public void TestCancellationCallbackInfo()
		{
			string error = null;

			using (var map = OpenMap0())
			using (var clrDump = GetDump())
			{
				var callInfos = new List<ClrType>(7000000);
				var callInfoAddrs = new List<ulong>(7000000);
				var callInfoParents = new List<ClrType>(70000);
				var callInfoParentAddrs = new List<ulong>(70000);
				var callInfoParentNames = new SortedSet<string>();

				var spAryParents = new List<ClrType>(70000);
				var spAryParentAddrs = new List<ulong>(70000);
				var spAryParentNames = new SortedSet<string>();

				try
				{
					var aryFragmentsCallbackId =
						map.GetTypeId("System.Threading.SparselyPopulatedArrayFragment<System.Threading.CancellationCallbackInfo>");
					var aryFragmentsCallbacks = map.GetTypeRealAddresses(aryFragmentsCallbackId);

					var aryFragmentsCannonId =
						//						map.GetTypeId("System.Threading.SparselyPopulatedArrayFragment<System.__Canon>");
						map.GetTypeId("System.Threading.SparselyPopulatedArray<System.Threading.CancellationCallbackInfo>[]");
					var aryFragmentsCannons = map.GetTypeRealAddresses(aryFragmentsCannonId);

					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType == null) goto NEXT_OBJECT;

							if (clrType.Name == "System.Threading.CancellationCallbackInfo")
							{
								callInfos.Add(clrType);
								callInfoAddrs.Add(addr);
							}
							else
							{
								for (int f = 0, fcnt = clrType.Fields.Count; f < fcnt; ++f)
								{
									var fld = clrType.Fields[f];
									if (fld.Type.Name == "System.Threading.CancellationCallbackInfo")
									{
										callInfoParents.Add(clrType);
										callInfoParentAddrs.Add(addr);
										callInfoParentNames.Add(clrType.Name);
									}
									else if (fld.Type.Name ==
											 "System.Threading.SparselyPopulatedArrayFragment<System.Threading.CancellationCallbackInfo>")
									{
										spAryParents.Add(clrType);
										spAryParentAddrs.Add(addr);
										spAryParentNames.Add(clrType.Name);
									}
								}
							}

							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

		[TestMethod]
		public void TestCancellationTokens()
		{
			string error = null;
			var clrDump = GetDump();
			ulong[] cancelSourceInstances;
			var dctWithCancelSourceField = new SortedDictionary<string, List<ulong>>();
			var dctWithCancelTokenField = new SortedDictionary<string, List<ulong>>();
			ulong[] cancelTokSrcInFinQue;
			int fqCnt = 0;
			using (clrDump)
			{
				try
				{
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					{
						List<ulong> cancelSourceLst = new List<ulong>(700000);
						for (int i = 0, icnt = segs.Count; i < icnt; ++i)
						{
							var seg = segs[i];
							ulong addr = seg.FirstObject;
							while (addr != 0ul)
							{
								var clrType = heap.GetObjectType(addr);
								if (clrType == null) goto NEXT_OBJECT;

								if (clrType.Name == "System.Threading.CancellationTokenSource")
								{
									cancelSourceLst.Add(addr);
									goto NEXT_OBJECT;
								}

								for (int j = 0, jcnt = clrType.Fields.Count; j < jcnt; ++j)
								{
									if (clrType.Fields[j].Type.Name == "System.Threading.CancellationTokenSource")
									{
										List<ulong> lst;
										var key = clrType.Name + "|" + clrType.Fields[j].Name;
										if (dctWithCancelSourceField.TryGetValue(key, out lst))
										{
											lst.Add(addr);
										}
										else
										{
											dctWithCancelSourceField.Add(key, new List<ulong>() { addr });
										}
									}
									if (clrType.Fields[j].Type.Name == "System.Threading.CancellationToken")
									{
										List<ulong> lst;
										var key = clrType.Name + "|" + clrType.Fields[j].Name;
										if (dctWithCancelTokenField.TryGetValue(key, out lst))
										{
											lst.Add(addr);
										}
										else
										{
											dctWithCancelTokenField.Add(key, new List<ulong>() { addr });
										}
									}

								}

								NEXT_OBJECT:
								addr = seg.NextObject(addr);
							}
						}
						cancelSourceInstances = cancelSourceLst.ToArray();
					}

					{
						var fq = runtime.EnumerateFinalizerQueueObjectAddresses().ToArray();
						fqCnt = fq.Length;
						var cancelTokSrcInFinQueLst = new List<ulong>(5000);
						for (int i = 0, icnt = fq.Length; i < icnt; ++i)
						{
							var cl = heap.GetObjectType(fq[i]);
							if (cl != null && cl.Name == "System.Threading.CancellationTokenSource")
							{
								cancelTokSrcInFinQueLst.Add(fq[i]);
							}
						}

						cancelTokSrcInFinQue = cancelTokSrcInFinQueLst.ToArray();
					}

					StreamWriter wr = null;
					try
					{
						var path = TestConfiguration.OutPath0 + clrDump.DumpFileName + ".CANCELTKNSRC.txt";
						wr = new StreamWriter(path);
						wr.WriteLine("### System.Threading.CancellationTokenSource instance count: " + cancelSourceInstances.Length);
						wr.WriteLine("### Finalizer queue count: " + fqCnt + ", CancellationTokenSource in fin. queue count: " + cancelTokSrcInFinQue.Length);
						wr.WriteLine();
						wr.WriteLine("### _instances with CancellationTokenSource field, count: " + dctWithCancelSourceField.Count);
						wr.WriteLine("### instance count, field name, type name");
						foreach (var kv in dctWithCancelSourceField)
						{
							var cnt = kv.Value.Count;
							var pos = kv.Key.IndexOf('|');
							Assert.IsTrue(pos >= 0);
							var tpName = kv.Key.Substring(0, pos);
							var fldName = kv.Key.Substring(pos + 1);
							wr.WriteLine("[" + cnt + "] " + fldName + "   " + tpName);
						}

						wr.WriteLine();
						wr.WriteLine("### _instances with CancellationToken field, count: " + dctWithCancelTokenField.Count);
						wr.WriteLine("### instance count, field name, type name");
						foreach (var kv in dctWithCancelTokenField)
						{
							var cnt = kv.Value.Count;
							var pos = kv.Key.IndexOf('|');
							Assert.IsTrue(pos >= 0);
							var tpName = kv.Key.Substring(0, pos);
							var fldName = kv.Key.Substring(pos + 1);
							wr.WriteLine("[" + cnt + "] " + fldName + "   " + tpName);
						}

					}
					catch (Exception ex)
					{
						error = Utils.GetExceptionErrorString(ex);
						Assert.IsTrue(false, error);
					}
					finally
					{
						wr?.Close();
					}
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

		#endregion CancellationToken Tests

		#region Threads/Sync Objects

		[TestMethod]
		public void TestGetThreadTraces()
		{
			string error = null;
			using (var clrDump = GetDump())
			{
				var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
				var fileMoniker = new DumpFileMoniker(Setup.GetRecentAdhocPath(0));
				StreamWriter wr = null;
				wr = new StreamWriter(fileMoniker.OutputFolder + Path.DirectorySeparatorChar + "THREADINFOS.txt");
				try
				{
					ClrRuntime runtime = clrDump.Runtimes[0];
					var stackTraceLst = new List<ClrStackFrame>();
					var thInfos = new  KeyValuePair<ClrThread, ClrStackFrame[]>[runtime.Threads.Count];
					int ndx = 0;
					foreach (var th in runtime.Threads)
					{
						stackTraceLst.Clear();
						foreach (var trace in th.EnumerateStackTrace())
						{
							stackTraceLst.Add(trace);
						}
						thInfos[ndx++] = new KeyValuePair<ClrThread, ClrStackFrame[]>(th,stackTraceLst.ToArray());
					}
					Assert.IsTrue(thInfos.Length > 0);
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
				finally
				{
					StringBuilderCache.Release(sb);
					wr?.Close();
				}
			}
		}


		//[TestMethod]
		//public void TestBuildThreadMap()
		//{
		//	string error = null;
		//	var blkObjTypeNames = new SortedSet<string>();
		//	using (var clrDump = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\ConvergEx\Analytics2.dmp"))
		//	{
		//		try
		//		{
		//			var runtime = clrDump.Runtimes[0];
		//			var threads = Indexer.GetThreads(runtime);
		//			var heap = runtime.Heap;
		//			var blkObjects = Indexer.GetBlockingObjects(runtime);
		//			ClrType[] blkTypes = new ClrType[blkObjects.Length];
		//			for (int i = 0, icnt = blkObjects.Length; i < icnt; ++i)
		//			{
		//				var blkObjType = heap.GetObjectType(blkObjects[i].Object);
		//				Assert.IsNotNull(blkObjType);
		//				blkTypes[i] = blkObjType;
		//				blkObjTypeNames.Add(blkObjType.Name);
		//			}
		//			var typeInfos = new KeyValuePair<ulong[], int[]>(Utils.EmptyArray<ulong>.Value, Utils.EmptyArray<int>.Value);
		//			var blks = new ClrtBlkObject[blkObjects.Length];
		//			var blkAddresses = new ulong[blkObjects.Length];
		//			for (int i = 0, icnt = blkObjects.Length; i < icnt; ++i)
		//			{
		//				blks[i] = new ClrtBlkObject(blkObjects[i], threads, typeInfos);
		//				blkAddresses[i] = blkObjects[i].Object;
		//			}
		//			var thrds = new ClrtThread[threads.Length];
		//			for (int i = 0, icnt = threads.Length; i < icnt; ++i)
		//			{
		//				thrds[i] = new ClrtThread(threads[i], blkAddresses);
		//			}

		//			var thrdBlkMap = new ThreadBlockMap(blks, thrds);
		//			thrdBlkMap.CreateGrapByBlkObjects(out error);
		//			Assert.IsNull(error, error);
		//			bool hasCycle = thrdBlkMap.HasCycle(out error);
		//			Assert.IsNull(error, error);

		//			var segs = heap.Segments;
		//			for (int i = 0, icnt = segs.Count; i < icnt; ++i)
		//			{
		//				var seg = segs[i];
		//				ulong addr = seg.FirstObject;
		//				while (addr != 0ul)
		//				{
		//					var clrType = heap.GetObjectType(addr);
		//					if (clrType == null) goto NEXT_OBJECT;



		//					NEXT_OBJECT:
		//					addr = seg.NextObject(addr);
		//				}
		//			}
		//		}
		//		catch (Exception ex)
		//		{
		//			error = Utils.GetExceptionErrorString(ex);
		//			Assert.IsTrue(false, error);
		//		}
		//	}
		//}

		[TestMethod]
		public void TestThreadInstances()
		{
			string error = null;
			using (var clrDump = GetDump())
			{
				try
				{
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType == null || clrType.Name != "System.Threading.Thread") goto NEXT_OBJECT;
							//if (clrType == null) goto NEXT_OBJECT;
							for (int j = 0, jcnt = clrType.Fields.Count; j < jcnt; ++j)
							{
								var fld = clrType.Fields[j];
								switch (fld.Name)
								{
									case "m_Context":
										break;
									case "m_ExecutionContext":
										break;
									case "m_SynchronizationContext":
										break;
									case "m_Name":
										break;
									case "m_Delegate":
										break;
									case "m_ThreadStartArg":
										break;
									case "m_ExecutionContextBelongsToOuterScope":
										break;
								}
							}
							for (int j = 0, jcnt = clrType.StaticFields.Count; j < jcnt; ++j)
							{

							}




							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

		[TestMethod]
		public void TestWorkItems()
		{
			using (var clrDump = GetDump())
			{
				var runtime = clrDump.Runtimes[0];
				Tuple<string, int[], List<Auxiliaries.AddrNameStruct>> result = FQry.getManagedWorkItem(runtime);
				Assert.IsTrue(result.Item1 == null, result.Item1);
			}
		}

		#endregion Threads/Sync Objects

		#region Types

		[TestMethod]
		public void TestSearchTypeInDumps()
		{
			const string typeName = @"System.ServiceModel.Description.IOperationBehavior[]";
			const string folderPath = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Baly\Extra";
			string[] fileList = Directory.GetFiles(folderPath, @"*.dmp");
			Debug.Assert(fileList.Length > 0, "Folder does not contain *.dmp files.");
			var results = new List<triple<string, int, string>>(fileList.Length);

			//BlockingCollection<string> requests = new BlockingCollection<string>(fileList.Length);
			for (int i = 0, icnt = fileList.Length; i < icnt; ++i)
			{
				try
				{
					string error;
					TestSearchTypeInDump(fileList[i], typeName, results, out error);
				}
				catch (Exception ex)
				{
					string error = Utils.GetExceptionErrorString(ex);
				}
			}
			TestContext.WriteLine(typeName);
			for (int i = 0, icnt = results.Count; i < icnt; ++i)
			{
				var res = results[i];
				if (res.Third != null)
				{
					TestContext.WriteLine(res.First + Environment.NewLine + res.Third);
					continue;
				}
				TestContext.WriteLine("[" + res.PSecond + "]  " + res.First);
			}

			//requests.Add(fileList[i]);

			//var procCnt = Math.Min(Environment.ProcessorCount, fileList.Length);
			//Task[] tasks = new Task[procCnt];
			////var results = new BlockingCollection<triple<string, int, string>>(fileList.Length);

			//for (int i = 0, icnt = tasks.Length; i < icnt; ++i)
			//{
			//	tasks[i] = new Task(() =>
			//	{
			//		while (requests.Count > 0)
			//		{
			//			string dmpPath;
			//			if (!requests.TryTake(out dmpPath))
			//			{
			//				break;
			//			}
			//			string error;
			//			TestSearchTypeInDump(dmpPath, typeName, results, out error);
			//		}
			//	}, TaskCreationOptions.LongRunning);
			//	tasks[i].Start();
			//}
			//Task.WaitAll(tasks);

			Assert.IsTrue(results.Count == fileList.Length);

		}

		public void TestSearchTypeInDump(string path, string typeName, List<triple<string, int, string>> que, out string error)
		{
			error = null;
			var clrDump = GetDump(path, out error);
			if (error != null) return;
			using (clrDump)
			{
				try
				{
					int count = 0;
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType == null) goto NEXT_OBJECT;
							if (Utils.SameStrings(clrType.Name, typeName))
								++count;

							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}
					que.Add(new triple<string, int, string>(path, count, null));
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					que.Add(new triple<string, int, string>(path, 0, error));
				}
			}
		}

		[TestMethod]
		public void TestNodeContent()
		{
			string typeName = @"System.Collections.Concurrent.ConcurrentDictionary+Node<System.String,ECS.Common.Collections.Common.triple<System.String,System.Int32,System.Int32>>";
			string error = null;
			using (var clrDump = GetDump(@"dumpPath"))
			{
				List<ulong> addrLst = new List<ulong>(1024);
				try
				{
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType == null || !Utils.SameStrings(clrType.Name, typeName)) goto NEXT_OBJECT;

							var fld = clrType.GetFieldByName("m_key");
							var sval = (string)fld.GetValue(addr, false, true);

							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}
					StreamWriter wr = null;
					try
					{
						Tuple<string, string> dmpFolders = DumpFileMoniker.GetAndCreateMapFolders(clrDump.DumpPath, out error);

						var path = dmpFolders.Item2 + @"\DupKeys.txt";

						wr = new StreamWriter(path);
						wr.WriteLine("### \"" + typeName + "\" [" + addrLst.Count + "]");
						for (int i = 0, icnt = addrLst.Count; i < icnt; ++i)
						{
							wr.WriteLine(Utils.AddressString(addrLst[i]));
						}
						wr.Close();
						wr = null;
					}
					finally
					{
						wr?.Close();
					}
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

		[TestMethod]
		public void Test_RealPosition_Type()
		{
			string typeName = @"ECS.Common.HierarchyCache.Structure.RealPosition";
			string error = null;

			using (var clrDump = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Centurion\AnalyticsCenturion.4.18.17.dmp"))
			{
				var addrLst = new List<String[]>(1024);
				try
				{
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType == null || !Utils.SameStrings(clrType.Name, typeName)) goto NEXT_OBJECT;
							string[] data = new string[7];
							data[0] = Utils.AddressString(addr);

							var fld = clrType.GetFieldByName("posID");
							if (fld == null) goto NEXT_OBJECT;
							data[1] = (string)fld.GetValue(addr, false, true);

							var prtcd = clrType.GetFieldByName("portcd");
							data[2] = (string)prtcd.GetValue(addr, false, true);
                            if (string.Compare("CPLP", data[2], StringComparison.OrdinalIgnoreCase) != 0) goto NEXT_OBJECT;

							fld = clrType.GetFieldByName("sec");
							object faddrObj = fld.GetValue(addr, false, false);
							ulong faddr = (ulong?)faddrObj ?? 0UL;
							string sval;
							if (faddr == 0UL)
							{
								sval = Constants.NullValue;
							}
							else
							{
								var fld2 = fld.Type.GetFieldByName("securitySimpleState");
								var faddr2 = (ulong)fld2.GetValue(faddr, false);
								var fld3 = fld2.Type.GetFieldByName("Symbol");
								sval = (string)fld3.GetValue(faddr2, false, true);
							}
							data[3] = sval;
                            if (string.Compare("MC", data[3], StringComparison.OrdinalIgnoreCase) != 0) goto NEXT_OBJECT;

                            fld = clrType.GetFieldByName("filledAmount");
							faddr = (ulong)fld.GetAddress(addr, true);
							data[4] = ValueExtractor.GetDecimalValue(faddr, fld.Type, "0,0.00");

							fld = clrType.GetFieldByName("netcost");
							faddr = (ulong)fld.GetAddress(addr, true);
							data[5] = ValueExtractor.GetDecimalValue(faddr, fld.Type, "0,0.00");

							fld = clrType.GetFieldByName("tradeState");
							data[6] = (string)fld.GetValue(addr, false, true);

							addrLst.Add(data);

							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}

					StreamWriter wr = null;
					try
					{
						Tuple<string, string> dmpFolders = DumpFileMoniker.GetAndCreateMapFolders(clrDump.DumpPath, out error);

						var path = dmpFolders.Item2 + @"\RealPositions.AnalyticsCenturion.4.18.17.dmp.txt";

						wr = new StreamWriter(path);

						wr.WriteLine("### MDRDESK REPORT: RealPosition");
						wr.WriteLine("### TITLE: RealPosition");
						wr.WriteLine("### COUNT: " + Utils.LargeNumberString(addrLst.Count));
						wr.WriteLine("### COLUMNS: Address|ulong \u271A posID|string \u271A portcd|string \u271A Symbol|string \u271A filledAmount|string \u271A netcost|string \u271A tradeState|string");
						wr.WriteLine("### SEPARATOR:  \u271A ");
						wr.WriteLine("#### RealPosition count: " + Utils.LargeNumberString(addrLst.Count));

						for (int i = 0, icnt = addrLst.Count; i < icnt; ++i)
						{
							wr.Write(addrLst[i][0] + Constants.HeavyGreekCrossPadded);
							wr.Write(addrLst[i][1] + Constants.HeavyGreekCrossPadded);
							wr.Write(addrLst[i][2] + Constants.HeavyGreekCrossPadded);
							wr.Write(addrLst[i][3] + Constants.HeavyGreekCrossPadded);
							wr.Write(addrLst[i][4] + Constants.HeavyGreekCrossPadded);
							wr.Write(addrLst[i][5] + Constants.HeavyGreekCrossPadded);
							wr.WriteLine(addrLst[i][6]);
						}
						wr.Close();
						wr = null;
					}
					finally
					{
						wr?.Close();
					}
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

        [TestMethod]
        public void Test_RealPosition_Type2()
        {
            string typeName = @"ECS.Common.HierarchyCache.Structure.RealPosition";
            string error = null;

            using (var clrDump = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Centurion\AnalyticsCenturion.4.18.17.dmp"))
            {
                var addrLst = new List<String[]>(1024);
                try
                {
                    var runtime = clrDump.Runtimes[0];
                    var heap = runtime.Heap;
                    var segs = heap.Segments;
                    for (int i = 0, icnt = segs.Count; i < icnt; ++i)
                    {
                        var seg = segs[i];
                        ulong addr = seg.FirstObject;
                        while (addr != 0ul)
                        {
                            var clrType = heap.GetObjectType(addr);
                            if (clrType == null || !Utils.SameStrings(clrType.Name, typeName)) goto NEXT_OBJECT;

                            string[] data = new string[4];
                            data[0] = Utils.RealAddressString(addr);

                            var fld = clrType.GetFieldByName("posID");
                            if (fld == null) goto NEXT_OBJECT;
                            data[1] = (string)fld.GetValue(addr, false, true);

                            fld = clrType.GetFieldByName("sec");
                            object faddrObj = fld.GetValue(addr, false, false);
                            ulong faddr = (ulong?)faddrObj ?? 0UL;
                            string sval;
                            if (faddr == 0UL)
                            {
                                sval = Constants.NullValue;
                            }
                            else
                            {
                                var fld2 = fld.Type.GetFieldByName("securitySimpleState");
                                var faddr2 = (ulong)fld2.GetValue(faddr, false);
                                var fld3 = fld2.Type.GetFieldByName("Symbol");
                                sval = (string)fld3.GetValue(faddr2, false, true);
                            }
                            data[2] = sval;


                            fld = clrType.GetFieldByName("positionStateManager");
                            faddr = (ulong)fld.GetValue(addr, false);
                            var posMngr = heap.GetObjectType(faddr);
                            if (posMngr == null) goto NEXT_OBJECT;

                            fld = posMngr.GetFieldByName("positionLevelCache");
                            faddr = (ulong)fld.GetValue(faddr, false);
                            var levelCache = heap.GetObjectType(faddr);
                            if (levelCache == null) goto NEXT_OBJECT;

                            fld = levelCache.GetFieldByName("multiFeedCache");
                            faddr = (ulong)fld.GetValue(faddr, false);
                            var cacheFeed = heap.GetObjectType(faddr);
                            if (cacheFeed == null) goto NEXT_OBJECT;

                            fld = cacheFeed.GetFieldByName("decimalCaches");
                            faddr = (ulong)fld.GetValue(faddr, false);
                            var decCache = heap.GetObjectType(faddr);
                            if (decCache == null) goto NEXT_OBJECT;

                            fld = decCache.GetFieldByName("count");
                            int val = (int)fld.GetValue(faddr, false);
                            if (val < 2) goto NEXT_OBJECT;

                            data[3] = val.ToString();

                            addrLst.Add(data);

                            NEXT_OBJECT:
                            addr = seg.NextObject(addr);
                        }
                    }

                    StreamWriter wr = null;
                    try
                    {
                        Tuple<string, string> dmpFolders = DumpFileMoniker.GetAndCreateMapFolders(clrDump.DumpPath, out error);

                        var path = dmpFolders.Item2 + @"\RealPositions2.AnalyticsCenturion.4.18.17.dmp.txt";

                        wr = new StreamWriter(path);

                        wr.WriteLine("### MDRDESK REPORT: RealPosition");
                        wr.WriteLine("### TITLE: RealPosition");
                        wr.WriteLine("### COUNT: " + Utils.LargeNumberString(addrLst.Count));
                        wr.WriteLine("### COLUMNS: Address|ulong \u271A posID|string \u271A Symbol|string \u271A count|int");
                        wr.WriteLine("### SEPARATOR:  \u271A ");
                        wr.WriteLine("#### RealPosition count: " + Utils.LargeNumberString(addrLst.Count));

                        for (int i = 0, icnt = addrLst.Count; i < icnt; ++i)
                        {
                            wr.Write(addrLst[i][0] + Constants.HeavyGreekCrossPadded);
                            wr.Write(addrLst[i][1] + Constants.HeavyGreekCrossPadded);
                            wr.Write(addrLst[i][2] + Constants.HeavyGreekCrossPadded);
                            wr.WriteLine(addrLst[i][3]);
                        }
                        wr.Close();
                        wr = null;
                    }
                    finally
                    {
                        wr?.Close();
                    }
                }
                catch (Exception ex)
                {
                    error = Utils.GetExceptionErrorString(ex);
                    Assert.IsTrue(false, error);
                }
            }
        }

        [TestMethod]
        public void Test_RowCache_Type()
        {
            string typeName = @"Eze.Server.Common.Pulse.CalculationCache.RowCache";
            string error = null;

            using (var clrDump = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Centurion\AnalyticsCenturion.4.18.17.dmp"))
            {
                var addrLst = new List<String[]>(1024);
                try
                {
                    var runtime = clrDump.Runtimes[0];
                    var heap = runtime.Heap;
                    var segs = heap.Segments;
                    for (int i = 0, icnt = segs.Count; i < icnt; ++i)
                    {
                        var seg = segs[i];
                        ulong addr = seg.FirstObject;
                        while (addr != 0ul)
                        {
                            var clrType = heap.GetObjectType(addr);
                            if (clrType == null || !Utils.SameStrings(clrType.Name, typeName)) goto NEXT_OBJECT;
                            string[] data = new string[7];
                            data[0] = Utils.AddressString(addr);

                            var fld = clrType.GetFieldByName("key");
                            if (fld == null) goto NEXT_OBJECT;
                            data[1] = (string)fld.GetValue(addr, false, true);
                            if (data[1].IndexOf("MC") < 0) goto NEXT_OBJECT;

                            data[2] = "??????";
                            data[3] = "??????";
                            data[4] = "??????";
                            data[5] = "??????";
                            data[6] = "??????";

                            //var prtcd = clrType.GetFieldByName("portcd");
                            //data[2] = (string)prtcd.GetValue(addr, false, true);
                            //if (string.Compare("CPLP", data[2], StringComparison.OrdinalIgnoreCase) != 0) goto NEXT_OBJECT;

                            //fld = clrType.GetFieldByName("sec");
                            //object faddrObj = fld.GetValue(addr, false, false);
                            //ulong faddr = (ulong?)faddrObj ?? 0UL;
                            //string sval;
                            //if (faddr == 0UL)
                            //{
                            //    sval = Constants.NullValue;
                            //}
                            //else
                            //{
                            //    var fld2 = fld.Type.GetFieldByName("securitySimpleState");
                            //    var faddr2 = (ulong)fld2.GetValue(faddr, false);
                            //    var fld3 = fld2.Type.GetFieldByName("Symbol");
                            //    sval = (string)fld3.GetValue(faddr2, false, true);
                            //}
                            //data[3] = sval;
                            //if (string.Compare("MC", data[3], StringComparison.OrdinalIgnoreCase) != 0) goto NEXT_OBJECT;

                            //fld = clrType.GetFieldByName("filledAmount");
                            //faddr = (ulong)fld.GetAddress(addr, true);
                            //data[4] = ValueExtractor.GetDecimalValue(faddr, fld.Type, "0,0.00");

                            //fld = clrType.GetFieldByName("netcost");
                            //faddr = (ulong)fld.GetAddress(addr, true);
                            //data[5] = ValueExtractor.GetDecimalValue(faddr, fld.Type, "0,0.00");

                            //fld = clrType.GetFieldByName("tradeState");
                            //data[6] = (string)fld.GetValue(addr, false, true);

                            addrLst.Add(data);

                            NEXT_OBJECT:
                            addr = seg.NextObject(addr);
                        }
                    }

                    StreamWriter wr = null;
                    try
                    {
                        Tuple<string, string> dmpFolders = DumpFileMoniker.GetAndCreateMapFolders(clrDump.DumpPath, out error);

                        var path = dmpFolders.Item2 + @"\RowCache.AnalyticsCenturion.4.18.17.dmp.txt";

                        wr = new StreamWriter(path);

                        wr.WriteLine("### MDRDESK REPORT: RowCache");
                        wr.WriteLine("### TITLE: RealPosition");
                        wr.WriteLine("### COUNT: " + Utils.LargeNumberString(addrLst.Count));
                        wr.WriteLine("### COLUMNS: Address|ulong \u271A key|string \u271A portcd|string \u271A Symbol|string \u271A filledAmount|string \u271A netcost|string \u271A tradeState|string");
                        wr.WriteLine("### SEPARATOR:  \u271A ");
                        wr.WriteLine("#### RowCache count: " + Utils.LargeNumberString(addrLst.Count));

                        for (int i = 0, icnt = addrLst.Count; i < icnt; ++i)
                        {
                            wr.Write(addrLst[i][0] + Constants.HeavyGreekCrossPadded);
                            wr.Write(addrLst[i][1] + Constants.HeavyGreekCrossPadded);
                            wr.Write(addrLst[i][2] + Constants.HeavyGreekCrossPadded);
                            wr.Write(addrLst[i][3] + Constants.HeavyGreekCrossPadded);
                            wr.Write(addrLst[i][4] + Constants.HeavyGreekCrossPadded);
                            wr.Write(addrLst[i][5] + Constants.HeavyGreekCrossPadded);
                            wr.WriteLine(addrLst[i][6]);
                        }
                        wr.Close();
                        wr = null;
                    }
                    finally
                    {
                        wr?.Close();
                    }
                }
                catch (Exception ex)
                {
                    error = Utils.GetExceptionErrorString(ex);
                    Assert.IsTrue(false, error);
                }
            }
        }

        [TestMethod]
        public void Test_SideSplitterSharesItem_Type()
        {
            string typeName = @"Eze.Server.Common.Pulse.Common.Types.SideSplitterSharesItem";
            ulong[] addresses = new ulong[] {
            0x00000380c0e788,
            0x00000380c0f028,
            0x00000380c0fa98,
            0x00000380c11228,
            0x00000380c121b0,
            0x00000380c13348,
            };

            string error = null;

            using (var clrDump = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Centurion\AnalyticsCenturion.4.18.17.dmp"))
            {
                var addrLst = new List<String[]>(1024);
                try
                {
                    var runtime = clrDump.Runtimes[0];
                    var heap = runtime.Heap;
                    var segs = heap.Segments;
                    for (int i = 0, icnt = segs.Count; i < icnt; ++i)
                    {
                        var seg = segs[i];
                        ulong addr = seg.FirstObject;
                        while (addr != 0ul)
                        {
                            var clrType = heap.GetObjectType(addr);
                            if (clrType == null || !Utils.SameStrings(clrType.Name, typeName)) goto NEXT_OBJECT;
                            string[] data = new string[7];
                            data[0] = Utils.RealAddressString(addr);

                            var fld = clrType.GetFieldByName("rowCache");
                            if (fld == null) goto NEXT_OBJECT;

                            ulong val = (ulong)fld.GetValue(addr, false, false);
                            if (Array.IndexOf(addresses, val) < 0) goto NEXT_OBJECT;
                            data[1] = Utils.RealAddressString(val);

                            data[2] = "??????";
                            data[3] = "??????";
                            data[4] = "??????";
                            data[5] = "??????";
                            data[6] = "??????";

                            //var prtcd = clrType.GetFieldByName("portcd");
                            //data[2] = (string)prtcd.GetValue(addr, false, true);
                            //if (string.Compare("CPLP", data[2], StringComparison.OrdinalIgnoreCase) != 0) goto NEXT_OBJECT;

                            //fld = clrType.GetFieldByName("sec");
                            //object faddrObj = fld.GetValue(addr, false, false);
                            //ulong faddr = (ulong?)faddrObj ?? 0UL;
                            //string sval;
                            //if (faddr == 0UL)
                            //{
                            //    sval = Constants.NullValue;
                            //}
                            //else
                            //{
                            //    var fld2 = fld.Type.GetFieldByName("securitySimpleState");
                            //    var faddr2 = (ulong)fld2.GetValue(faddr, false);
                            //    var fld3 = fld2.Type.GetFieldByName("Symbol");
                            //    sval = (string)fld3.GetValue(faddr2, false, true);
                            //}
                            //data[3] = sval;
                            //if (string.Compare("MC", data[3], StringComparison.OrdinalIgnoreCase) != 0) goto NEXT_OBJECT;

                            //fld = clrType.GetFieldByName("filledAmount");
                            //faddr = (ulong)fld.GetAddress(addr, true);
                            //data[4] = ValueExtractor.GetDecimalValue(faddr, fld.Type, "0,0.00");

                            //fld = clrType.GetFieldByName("netcost");
                            //faddr = (ulong)fld.GetAddress(addr, true);
                            //data[5] = ValueExtractor.GetDecimalValue(faddr, fld.Type, "0,0.00");

                            //fld = clrType.GetFieldByName("tradeState");
                            //data[6] = (string)fld.GetValue(addr, false, true);

                            addrLst.Add(data);

                            NEXT_OBJECT:
                            addr = seg.NextObject(addr);
                        }
                    }

                    StreamWriter wr = null;
                    try
                    {
                        Tuple<string, string> dmpFolders = DumpFileMoniker.GetAndCreateMapFolders(clrDump.DumpPath, out error);

                        var path = dmpFolders.Item2 + @"\SideSplitterSharesItem.AnalyticsCenturion.4.18.17.dmp.txt";

                        wr = new StreamWriter(path);

                        wr.WriteLine("### MDRDESK REPORT: SideSplitterSharesItem");
                        wr.WriteLine("### TITLE: RealPosition");
                        wr.WriteLine("### COUNT: " + Utils.LargeNumberString(addrLst.Count));
                        wr.WriteLine("### COLUMNS: Address|ulong \u271A key|string \u271A portcd|string \u271A Symbol|string \u271A filledAmount|string \u271A netcost|string \u271A tradeState|string");
                        wr.WriteLine("### SEPARATOR:  \u271A ");
                        wr.WriteLine("#### RowCache count: " + Utils.LargeNumberString(addrLst.Count));

                        for (int i = 0, icnt = addrLst.Count; i < icnt; ++i)
                        {
                            wr.Write(addrLst[i][0] + Constants.HeavyGreekCrossPadded);
                            wr.Write(addrLst[i][1] + Constants.HeavyGreekCrossPadded);
                            wr.Write(addrLst[i][2] + Constants.HeavyGreekCrossPadded);
                            wr.Write(addrLst[i][3] + Constants.HeavyGreekCrossPadded);
                            wr.Write(addrLst[i][4] + Constants.HeavyGreekCrossPadded);
                            wr.Write(addrLst[i][5] + Constants.HeavyGreekCrossPadded);
                            wr.WriteLine(addrLst[i][6]);
                        }
                        wr.Close();
                        wr = null;
                    }
                    finally
                    {
                        wr?.Close();
                    }
                }
                catch (Exception ex)
                {
                    error = Utils.GetExceptionErrorString(ex);
                    Assert.IsTrue(false, error);
                }
            }
        }

        [TestMethod]
		public void TestSpecificFilteredType2()
		{
			string typeName = @"ECS.Common.HierarchyCache.Structure.CashEffectPosition";
			string error = null;
			using (var clrDump = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Scopia\Eze.Analytics.Svc_160929_103505.dmp"))
			{
				var addrLst = new List<string[]>(1024);
				try
				{
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType == null || !Utils.SameStrings(clrType.Name, typeName)) goto NEXT_OBJECT;

							string[] data = new string[5];
							data[0] = Utils.AddressString(addr);

							var fld = clrType.GetFieldByName("positionID");
							data[1] = (string)fld.GetValue(addr, false, true);

							var prtcd = clrType.GetFieldByName("nonCashPositionID");
							data[2] = (string)prtcd.GetValue(addr, false, true);

							fld = clrType.GetFieldByName("affectedPosition");
							object foundAddrObj = fld.GetValue(addr, false, false);
							ulong foundAddr = foundAddrObj == null ? 0UL : (ulong)foundAddrObj;
							data[3] = Utils.AddressString(foundAddr);


							fld = clrType.GetFieldByName("cashEffectSecurity");
							foundAddrObj = fld.GetValue(addr, false, false);
							ulong faddr = foundAddrObj == null ? 0UL : (ulong)foundAddrObj;
							string sval = Constants.NullValue;
							if (faddr != 0UL)
							{
								var fld2 = fld.Type.GetFieldByName("securitySimpleState");
								var faddr2 = (ulong)fld2.GetValue(faddr, false);
								var fld3 = fld2.Type.GetFieldByName("Symbol");
								sval = (string)fld3.GetValue(faddr2, false, true);
							}
							data[4] = sval;

							addrLst.Add(data);

							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}

					StreamWriter wr = null;
					try
					{
						Tuple<string, string> dmpFolders = DumpFileMoniker.GetAndCreateMapFolders(clrDump.DumpPath, out error);

						var path = dmpFolders.Item2 + @"\CashEffectPosition.160929_110751.dmp.txt";

						wr = new StreamWriter(path);

						wr.WriteLine("### MDRDESK REPORT: CashEffectPosition");
						wr.WriteLine("### TITLE: CashEffectPosition");
						wr.WriteLine("### COUNT: " + Utils.LargeNumberString(addrLst.Count));
						wr.WriteLine("### COLUMNS: Address|ulong \u271A positionID|string \u271A nonCashPositionID|string \u271A affectedPosition|string \u271A cashEffectSecurity|string");
						wr.WriteLine("### SEPARATOR:  \u271A ");
						wr.WriteLine("#### CashEffectPosition count: " + Utils.LargeNumberString(addrLst.Count));

						for (int i = 0, icnt = addrLst.Count; i < icnt; ++i)
						{
							wr.Write(addrLst[i][0] + Constants.HeavyGreekCrossPadded);
							wr.Write(addrLst[i][1] + Constants.HeavyGreekCrossPadded);
							wr.Write(addrLst[i][2] + Constants.HeavyGreekCrossPadded);
							wr.Write(addrLst[i][3] + Constants.HeavyGreekCrossPadded);
							wr.WriteLine(addrLst[i][4]);
						}
						wr.Close();
						wr = null;
					}
					finally
					{
						wr?.Close();
					}
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

		[TestMethod]
		public void TestSpecificFilteredTypePulsePositionStateManager()
		{
			string typeName = @"Eze.Server.Common.Pulse.Common.Types.PulsePositionStateManager";
			string error = null;
			int min = Int32.MaxValue;
			int max = Int32.MinValue;
			int totalOfCounts = 0;
			int totalOfDct = 0;
			int totalOfTypeInstances = 0;
			SortedDictionary<int, int> histogram = new SortedDictionary<int, int>(new Utils.IntCmpDesc());

			using (var clrDump = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Harding\prcdmpAnalyticsHarding.20151029.dmp"))
			{
				try
				{
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					ClrType fldClrType = null;
					ClrInstanceField fldClrTypeFld = null;
					int dctCnt = 0;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						dctCnt = 0;
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType == null || !Utils.SameStrings(clrType.Name, typeName)) goto NEXT_OBJECT;
							++totalOfTypeInstances;

							var fld = clrType.GetFieldByName("aggregationSet");
							var fldAddr = Auxiliaries.getReferenceFieldAddress(addr, fld, false);
							if (fldAddr == Constants.InvalidAddress) continue;
							if (fldClrType == null)
								fldClrType = heap.GetObjectType(fldAddr);
							if (fldClrType != null)
							{
								if (fldClrTypeFld == null)
								{
									fldClrTypeFld = fldClrType.GetFieldByName("count");
								}
								if (fldClrTypeFld != null)
								{
									dctCnt = (int)fldClrTypeFld.GetValue(fldAddr, false);
									int c;
									if (histogram.TryGetValue(dctCnt, out c))
									{
										histogram[dctCnt] = c + 1;
									}
									else
									{
										histogram.Add(dctCnt,1);
									}
									if (max < dctCnt) max = dctCnt;
									if (min > dctCnt) min = dctCnt;
								}
							}

							++totalOfDct;
							totalOfCounts += dctCnt;

							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}

					StreamWriter wr = null;
					try
					{
						Tuple<string, string> dmpFolders = DumpFileMoniker.GetAndCreateMapFolders(clrDump.DumpPath, out error);
						var path = dmpFolders.Item2 + Path.DirectorySeparatorChar +  @"\DctCounts." + Path.GetFileName(clrDump.DumpPath) + ".txt";
						wr = new StreamWriter(path);

						wr.WriteLine("#### Dump: " + clrDump.DumpPath);
						wr.WriteLine();
						wr.WriteLine("#### " + typeName + " instances count: " + Utils.LargeNumberString(totalOfTypeInstances));
						wr.WriteLine("#### aggregationSet field instances count: " + Utils.LargeNumberString(totalOfDct));
						wr.WriteLine("#### aggregationSet dictionaries total item count: " + Utils.LargeNumberString(totalOfCounts));
						wr.WriteLine("#### Dictionary counts, min: " + min + ", max: " + max + ", avg: " + ((int)Math.Round((double)totalOfCounts/(double)totalOfDct)));
						wr.WriteLine();
						wr.WriteLine("#### Counts Histograms: dct count -> frequency  ||  frequency -> dct count");
						wr.WriteLine();
						var histAry = histogram.ToArray();
						Array.Sort(histAry,(a,b) => a.Value < b.Value ? 1 : (a.Value > b.Value ? -1 : 0) );
						var ndx = 0;
						foreach (var kv in histogram)
						{
							wr.Write(Utils.SizeString(kv.Key) + "  ->  " + Utils.SizeString(kv.Value));
							wr.Write("  ||  ");
							wr.WriteLine(Utils.SizeString(histAry[ndx].Value) + "  ->  " + Utils.SizeString(histAry[ndx].Key));
							++ndx;

						}
						wr.Close();
						wr = null;
					}
					finally
					{
						wr?.Close();
					}
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}


		[TestMethod]
		public void TestSpecificFilteredType3()
		{
			string typeName = @"ECS.Common.HierarchyCache.Structure.RealPosition";
			string error = null;
			HashSet<string> posIdDct = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			{
				"T150579455",
				"T150579453",
				"T150579452",
				"T150579450",
				"T150579449",
				"T150579447",
				"T150579446",
				"T150579454",
				"T150579451",
				"T150579448",
				"T150569223",
				"T150569224",
				"H150569223",
				"H150569224",
				"T150579493",
				"T150579490",
				"T150579488",
				"T150579836",
				"T150579845",
				"T150579840",
				"T150579797"
			};





			using (var clrDump = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Scopia\Eze.Analytics.Svc_160929_103505.dmp"))
			{
				var addrLst = new List<sextuple<ulong, string, string, string, string, string>>(1024);
				try
				{
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType == null || !Utils.SameStrings(clrType.Name, typeName)) goto NEXT_OBJECT;
							var fld = clrType.GetFieldByName("posID");
							if (fld == null) goto NEXT_OBJECT;
							var val = (string)fld.GetValue(addr, false, true);
							if (val == null || !posIdDct.Contains(val.Trim())) goto NEXT_OBJECT;

							var prtcd = clrType.GetFieldByName("portcd");
							var prtcdVal = (string)prtcd.GetValue(addr, false, true);

							fld = clrType.GetFieldByName("sec");
							ulong faddr = (ulong)fld.GetValue(addr, false);
							var fld2 = fld.Type.GetFieldByName("securitySimpleState");
							var faddr2 = (ulong)fld2.GetValue(faddr, false);
							var fld3 = fld2.Type.GetFieldByName("Symbol");
							var sval = (string)fld3.GetValue(faddr2, false, true);

							//if (string.Compare(symbolName, sval, StringComparison.OrdinalIgnoreCase) != 0) goto NEXT_OBJECT;

							fld = clrType.GetFieldByName("filledAmount");
							faddr = (ulong)fld.GetAddress(addr, true);
							var fval = ValueExtractor.GetDecimalValue(faddr, fld.Type, "0,0.00");

							fld = clrType.GetFieldByName("netcost");
							faddr = (ulong)fld.GetAddress(addr, true);
							var nval = ValueExtractor.GetDecimalValue(faddr, fld.Type, "0,0.00");

							fld = clrType.GetFieldByName("tradeState");
							var tstate = (string)fld.GetValue(addr, false, true);



							addrLst.Add(new sextuple<ulong, string, string, string, string, string>(addr, val, fval, nval, tstate, prtcdVal));

							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}

					addrLst.Sort(new SextupleUlongStrStrCmp2());
					StreamWriter wr = null;
					try
					{
						Tuple<string, string> dmpFolders = DumpFileMoniker.GetAndCreateMapFolders(clrDump.DumpPath, out error);

						var path = dmpFolders.Item2 + @"\SelectedIds.RealPositions.160929_103505.dmp.txt";

						wr = new StreamWriter(path);
						wr.WriteLine("### " + "Selected Ids RealPosition" + " [" + addrLst.Count + "]" + "  ADDRESSES, FILLED AMOUNT, NETCOST, TRADESTATE, PORTFOLIO");
						for (int i = 0, icnt = addrLst.Count; i < icnt; ++i)
						{
							wr.Write(Utils.AddressStringHeader(addrLst[i].First));
							wr.Write(addrLst[i].Second + "  ");
							wr.Write(addrLst[i].Third + " ");
							wr.Write(addrLst[i].Forth + " ");
							wr.Write(addrLst[i].Fifth + " ");
							wr.WriteLine(addrLst[i].Sixth);
						}
						wr.Close();
						wr = null;
					}
					finally
					{
						wr?.Close();
					}
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

		[TestMethod]
		public void TestSpecificFilteredType4()
		{
			string typeName = @"ECS.Common.HierarchyCache.Structure.CashEffectPosition";
			string error = null;
			//using (var clrDump = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Scopia\Eze.Analytics.Svc_160921_134355.dmp"))
			using (var clrDump = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Scopia\Eze.Analytics.Svc_160929_103505.dmp"))
			//using (var clrDump = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Scopia\Eze.Analytics.Svc_160929_110751.dmp"))
			{
				var addrLst = new List<quintuple<ulong, string, string, string, string>>(1024);
				try
				{
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType == null || !Utils.SameStrings(clrType.Name, typeName)) goto NEXT_OBJECT;

							var fld = clrType.GetFieldByName("cashEffectSecurity");
							if (fld == null) goto NEXT_OBJECT;
							var foundObj = fld.GetValue(addr, false, false);
							ulong foundAddr = foundObj == null ? 0UL : (ulong)foundObj;
							string foundAddrStr;
							if (foundAddr == 0UL)
							{
								foundAddrStr = Utils.AddressString(foundAddr);
							}
							else goto NEXT_OBJECT;

							fld = clrType.GetFieldByName("positionID");
							if (fld == null) goto NEXT_OBJECT;
							var val = (string)fld.GetValue(addr, false, true);
							if (val == null || val.Length < 2 || !(val[0] == 'T' && char.IsDigit(val[1]))) goto NEXT_OBJECT;

							var prtcd = clrType.GetFieldByName("nonCashPositionID");
							var prtcdVal = (string)prtcd.GetValue(addr, false, true);

							//fld = clrType.GetFieldByName("cashEffectSecurity");
							//ulong faddr = (ulong)fld.GetValue(addr, false);
							//var fld2 = fld.Type.GetFieldByName("securitySimpleState");
							//var faddr2 = (ulong)fld2.GetValue(faddr, false);
							//var fld3 = fld2.Type.GetFieldByName("Symbol");
							//var sval = (string)fld3.GetValue(faddr2, false, true);

							////if (string.Compare(symbolName, sval, StringComparison.OrdinalIgnoreCase) != 0) goto NEXT_OBJECT;

							//fld = clrType.GetFieldByName("filledAmount");
							//faddr = (ulong)fld.GetAddress(addr, true);
							//var fval = ValueExtractor.GetDecimalValue(faddr, fld.Type, "0,0.00");

							//fld = clrType.GetFieldByName("netcost");
							//faddr = (ulong)fld.GetAddress(addr, true);
							//var nval = ValueExtractor.GetDecimalValue(faddr, fld.Type, "0,0.00");

							//fld = clrType.GetFieldByName("tradeState");
							//var tstate = (string)fld.GetValue(addr, false, true);



							addrLst.Add(new quintuple<ulong, string, string, string, string>(addr, foundAddrStr, "???", val, prtcdVal));

							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}

					//addrLst.Sort(new SextupleUlongStrStrCmp2());
					StreamWriter wr = null;
					try
					{
						Tuple<string, string> dmpFolders = DumpFileMoniker.GetAndCreateMapFolders(clrDump.DumpPath, out error);

						var path = dmpFolders.Item2 + @"\CashEffectPositionWithNull.160929_103505.dmp.txt";

						wr = new StreamWriter(path);
						wr.WriteLine("### " + "CashEffectPosition" + " [" + addrLst.Count + "]" + "  ADDRESSES, affectedPosition, Symbol, positionID, nonCashPositionID");
						for (int i = 0, icnt = addrLst.Count; i < icnt; ++i)
						{
							wr.Write(Utils.AddressStringHeader(addrLst[i].First));
							wr.Write(addrLst[i].Second + "  ");
							wr.Write(addrLst[i].Third + " ");
							wr.Write(addrLst[i].Forth + " ");
							wr.WriteLine(addrLst[i].Fifth);
						}
						wr.Close();
						wr = null;
					}
					finally
					{
						wr?.Close();
					}
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}
		public class TripleUlongStrStrCmp2 : IComparer<triple<ulong, string, string>>
		{
			public int Compare(triple<ulong, string, string> a, triple<ulong, string, string> b)
			{
				return string.Compare(a.Second, b.Second, StringComparison.OrdinalIgnoreCase);
			}
		}

		public class QuintupleUlongStrStrCmp2 : IComparer<quintuple<ulong, string, string, string, string>>
		{
			public int Compare(quintuple<ulong, string, string, string, string> a, quintuple<ulong, string, string, string, string> b)
			{
				return string.Compare(a.Second, b.Second, StringComparison.OrdinalIgnoreCase);
			}
		}

		public class SextupleUlongStrStrCmp2 : IComparer<sextuple<ulong, string, string, string, string, string>>
		{
			public int Compare(sextuple<ulong, string, string, string, string, string> a, sextuple<ulong, string, string, string, string, string> b)
			{
				return string.Compare(a.Second, b.Second, StringComparison.OrdinalIgnoreCase);
			}
		}


		[TestMethod]
		public void TestFieldParents()
		{
			string error = null;
			string typeName = "DumpSearch.ADummy";
			var dct = new SortedDictionary<string, List<ulong>>(StringComparer.Ordinal);
			var fldRefList = new List<KeyValuePair<ulong, int>>(64);
			using (var clrDump = GetCurrentDump())
			{
				try
				{
					var addresses = GetTypeAddresses(clrDump, typeName);

					var heap = clrDump.GetFreshHeap();
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							if (Array.BinarySearch(addresses, addr) >= 0) goto NEXT_OBJECT; // our type instance
							var clrType = heap.GetObjectType(addr);
							if (clrType == null) goto NEXT_OBJECT;

							fldRefList.Clear();
							clrType.EnumerateRefsOfObjectCarefully(addr, (address, off) =>
							{
								fldRefList.Add(new KeyValuePair<ulong, int>(address, off));
							});

							bool isArray = clrType.IsArray;
							for (int j = 0, jcnt = fldRefList.Count; j < jcnt; ++j)
							{
								var fldAddr = fldRefList[j].Key;
								if (Array.BinarySearch(addresses, fldAddr) < 0) continue; // not my type
								string fldName = string.Empty;
								if (!isArray)
								{
									ClrInstanceField fld;
									int childFieldOffset;
									if (clrType.GetFieldForOffset(fldRefList[j].Value, clrType.IsValueClass, out fld,
										out childFieldOffset))
									{
										fldName = fld.Name;
									}
								}
								else
								{
									fldName = "[]";
								}
								string lookupName = clrType.Name + " FIELD: " + fldName;
								List<ulong> lst;
								if (dct.TryGetValue(lookupName, out lst))
								{
									lst.Add(addr);
								}
								else
								{
									dct.Add(lookupName, new List<ulong>() { addr });
								}
							}

							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

		[TestMethod]
		public void TestStringFieldParents()
		{
			string error = null;
			//string stringContent = "ComplexStructDummy.adummy.9";
			string stringContent = "ComplexStructDummy" + Constants.FancyKleeneStar;
			var dct = new SortedDictionary<string, List<ulong>>(StringComparer.Ordinal);
			var fldRefList = new List<KeyValuePair<ulong, int>>(64);
			using (var clrDump = GetCurrentDump())
			{
				try
				{
					var addresses = GetTypeAddresses(clrDump, "System.String");
					Assert.IsNotNull(addresses);
					var heap = clrDump.GetFreshHeap();
					var myStringAddresses = ClrtDump.GetSpecificStringAddresses(heap, addresses, stringContent, out error);
					Assert.IsNotNull(myStringAddresses, error);
					Assert.IsNull(error, error);

					heap = clrDump.GetFreshHeap();
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							if (Array.BinarySearch(addresses, addr) >= 0) goto NEXT_OBJECT; // skip strings
							var clrType = heap.GetObjectType(addr);
							if (clrType == null) goto NEXT_OBJECT;

							fldRefList.Clear();
							clrType.EnumerateRefsOfObjectCarefully(addr, (address, off) =>
							{
								fldRefList.Add(new KeyValuePair<ulong, int>(address, off));
							});

							bool isArray = clrType.IsArray;
							for (int j = 0, jcnt = fldRefList.Count; j < jcnt; ++j)
							{
								var fldAddr = fldRefList[j].Key;
								if (Array.BinarySearch(myStringAddresses, fldAddr) < 0) continue; // not my string
								string fldName = string.Empty;
								if (!isArray)
								{
									ClrInstanceField fld;
									int childFieldOffset;
									if (clrType.GetFieldForOffset(fldRefList[j].Value, clrType.IsValueClass, out fld,
										out childFieldOffset))
									{
										fldName = fld.Name;
									}
								}
								else
								{
									fldName = "[]";
								}
								string lookupName = clrType.Name + Constants.FieldSymbolPadded + fldName;
								List<ulong> lst;
								if (dct.TryGetValue(lookupName, out lst))
								{
									lst.Add(addr);
								}
								else
								{
									dct.Add(lookupName, new List<ulong>() { addr });
								}
							}

							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

		private ulong[] GetTypeAddresses(ClrtDump dump, string typeName)
		{
			List<ulong> lst = new List<ulong>(4 * 1024);
			var heap = dump.GetFreshHeap();
			var segs = heap.Segments;
			for (int i = 0, icnt = segs.Count; i < icnt; ++i)
			{
				var seg = segs[i];
				ulong addr = seg.FirstObject;
				while (addr != 0ul)
				{
					var clrType = heap.GetObjectType(addr);
					if (clrType == null) goto NEXT_OBJECT;

					if (Utils.SameStrings(clrType.Name, typeName))
					{
						lst.Add(addr);
					}

					NEXT_OBJECT:
					addr = seg.NextObject(addr);
				}
			}
			return lst.ToArray();
		}


		[TestMethod]
		public void TestGetTypeWithStringField()
		{
			var fldRefList = new List<KeyValuePair<ulong, int>>(64);
			const string str = "sec_realtime.ASK";
			//HashSet<string> typeNames = new HashSet<string>(StringComparer.Ordinal);
			SortedDictionary<string, KeyValuePair<string, int>> dct = new SortedDictionary<string, KeyValuePair<string, int>>(StringComparer.Ordinal);
			List<KeyValuePair<string, string>> additional = new List<KeyValuePair<string, string>>(512);
			string error = null;
			using (var clrDump = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\Eze.Analytics.O971_160721_163520.FC.7030MB.dmp"))
			{
				try
				{
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType?.Fields == null || clrType.IsString || !clrType.ContainsPointers) goto NEXT_OBJECT;
							//if (dct.ContainsKey(clrType.Name)) goto NEXT_OBJECT;

							if (clrType.IsArray)
							{
								int cnt = 0;
								if (clrType.ComponentType.IsString)
								{
									var len = clrType.GetArrayLength(addr);
									for (int j = 0; j < len; ++j)
									{
										var strObj = clrType.GetArrayElementValue(addr, j);
										if (strObj != null && (strObj as string) == str)
										{
											++cnt;
										}
									}
								}
								if (cnt < 1) goto NEXT_OBJECT;
								KeyValuePair<string, int> info;
								if (dct.TryGetValue(clrType.Name, out info))
								{
									dct[clrType.Name] = new KeyValuePair<string, int>(info.Key, info.Value + cnt);
								}
								else
								{
									dct.Add(clrType.Name, new KeyValuePair<string, int>("[]", cnt));
								}

								goto NEXT_OBJECT;
							}


							for (int j = 0, jcnt = clrType.Fields.Count; j < jcnt; ++j)
							{
								var fld = clrType.Fields[j];
								if (!fld.IsObjectReference) continue;
								if (fld.Type == null || !fld.Type.IsString) continue;
								var fldObj = fld.GetValue(addr);
								if (fldObj == null) continue;
								//var strAddr = (ulong) fldObj;
								//if (strAddr == 0UL) continue;
								//var fldStr = ValueExtractor.GetStringAtAddress(strAddr, heap);

								if ((string)fldObj == str)
								{
									var typeName = clrType.Name;
									var fldName = fld.Name;
									KeyValuePair<string, int> info;
									if (dct.TryGetValue(clrType.Name, out info))
									{
										if (fldName != info.Key)
											additional.Add(new KeyValuePair<string, string>(typeName, fldName));
										dct[typeName] = new KeyValuePair<string, int>(info.Key, info.Value + 1);
									}
									else
									{
										dct.Add(typeName, new KeyValuePair<string, int>(fldName, 1));
									}
								}
							}

							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

		[TestMethod]
		public void TestFindInstanceWithField()
		{
			const string typePrefix = "System.Windows.Forms.";
			const string fldName = "window";
			const ulong fldContentAsAddr = 0x4dea4524;
			string error = null;
			List<ulong> lst = new List<ulong>(256);

			using (var clrDump = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\OMS\AustralianSuper\EzeOMSFrozen07272016.dmp"))
			{
				try
				{
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType == null || !clrType.Name.StartsWith(typePrefix)) goto NEXT_OBJECT;

							var fld = clrType.GetFieldByName(fldName);
							if (fld != null)
							{
								var obj = fld.GetValue(addr);
								if (obj is ulong && fldContentAsAddr == (ulong)obj)
								{
									lst.Add(addr);
								}
							}

							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}



		[TestMethod]
		public void TestDerivedTypes()
		{
			//const string baseTypeName = "System.Threading.EventWaitHandle";
			const string baseTypeName = "System.Object";
			var dct = new SortedDictionary<string, KeyValuePair<string, List<ulong>>>();
			Queue<string> que = new Queue<string>();
			HashSet<ulong> processedAddresses = new HashSet<ulong>();
			HashSet<string> referenceBases = new HashSet<string>();
			StreamWriter wr = null;
			string error = null;
			using (var clrDump = GetDump())
			{
				try
				{
					//var path = TestConfiguration.OutPath0 + clrDump.DumpFileName + ".EventWaitHandle.DERIVED.map";
					var path = TestConfiguration.OutPath0 + clrDump.DumpFileName + ".Object.DERIVED.txt";
					wr = new StreamWriter(path);

					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					var curBaseName = baseTypeName;
					referenceBases.Add(curBaseName);
					que.Enqueue(baseTypeName);

					while (que.Count > 0)
					{
						curBaseName = que.Dequeue();
						for (int i = 0, icnt = segs.Count; i < icnt; ++i)
						{
							var seg = segs[i];
							ulong addr = seg.FirstObject;
							while (addr != 0ul)
							{
								var clrType = heap.GetObjectType(addr);
								if (clrType?.BaseType == null) goto NEXT_OBJECT;

								if (clrType.BaseType.Name == curBaseName)
								{
									if (!processedAddresses.Add(addr)) goto NEXT_OBJECT;
									KeyValuePair<string, List<ulong>> instances;
									if (dct.TryGetValue(clrType.Name, out instances))
									{
										instances.Value.Add(addr);
									}
									else
									{
										dct.Add(clrType.Name, new KeyValuePair<string, List<ulong>>(curBaseName, new List<ulong>(16) { addr }));
										if (referenceBases.Add(clrType.Name))
										{
											que.Enqueue(clrType.Name);
										}
									}
								}

								NEXT_OBJECT:
								addr = seg.NextObject(addr);
							}
						}
					}

					Assert.IsTrue(que.Count == 0, "");
					foreach (var kv in dct)
					{
						wr.WriteLine(Utils.SortableSizeStringHeader(kv.Value.Value.Count) + kv.Key + " <- " + kv.Value.Key);
					}
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
				finally
				{
					wr?.Close();
				}
			}
		}

		//[TestMethod]
		//public void TestGetTypeInfos()
		//{
		//	string error = null;
		//	int dupcnt = 0;
		//	using (var clrDump = GetDump())
		//	{
		//		try
		//		{

		//			List<string>[] dups0;
		//			var result0 = Indexer.GetTypes(clrDump, out dups0, out error);


		//			var runtime = clrDump.Runtimes[0];
		//			runtime.Flush();
		//			var heap = runtime.Heap;
		//			var tdct = new SortedDictionary<string, ClrType>(StringComparer.Ordinal);
		//			tdct.Add(Constants.NullTypeName,null);
		//			foreach (var clrType in heap.EnumerateTypes())
		//			{
		//				var tname = clrType.Name + "'" + clrType.MethodTable;

		//				ClrType clrt;
		//				if (tdct.TryGetValue(tname, out clrt))
		//				{
		//					++dupcnt;
		//				}
		//				else
		//				{
		//					tdct.Add(tname,clrType);
		//				}
		//			}

		//			Assert.IsTrue(dupcnt==0);

		//			StreamWriter wr = null;
		//			try
		//			{
		//				wr = new StreamWriter(@"d:\types.txt");
		//				int c = tdct.Count;
		//				wr.WriteLine(c);
		//				StringBuilder sb = new StringBuilder(256);
		//				foreach (var kv in tdct)
		//				{
		//					var clrType = kv.Value;
		//					if (clrType == null)
		//					{
		//						wr.WriteLine(kv.Key + Environment.NewLine + "   " + "NULL TYPE");
		//						continue;
		//					}
		//					var name = clrType.Name;
		//					var baseName = clrType.BaseType?.Name ?? Constants.NullTypeName;
		//					var methodTbl = clrType.MethodTable;
		//					var element = clrType.ElementType;
		//					var fldCnt = clrType.Fields.Count;
		//					var staticFldCnt = clrType.StaticFields.Count;
		//					var runTimeType = clrType.IsRuntimeType;
		//					wr.WriteLine(name);
		//					sb.Clear();
		//					sb.Append("   ").Append(Utils.AddressStringHeader(methodTbl))
		//						.Append(runTimeType ? "(T) " : "(F) ")
		//						.Append(Utils.SmallNumberHeader(staticFldCnt))
		//						.Append(Utils.SmallNumberHeader(fldCnt))
		//						.Append(element).Append("   ").Append(baseName);
		//					wr.WriteLine(sb.ToString());
		//				}
		//			}
		//			catch (Exception)
		//			{

		//				throw;
		//			}
		//			finally 
		//			{
		//				wr?.Close();
		//			}

		//			List<string>[] dups;
		//			var result = Indexer.GetTypes(clrDump, out dups, out error);

		//			Assert.IsNotNull(result);
		//			Assert.IsNull(dups);

		//			runtime.Flush();
		//			heap = runtime.Heap;

		//			int dupcnt1 = 0;
		//			var typeDct1 = new SortedDictionary<string, ClrType>(StringComparer.Ordinal);
		//			typeDct1.Add(Constants.NullTypeName, null);
		//			foreach (var clrType in heap.EnumerateTypes())
		//			{
		//				var tname = clrType.Name + " ' " + clrType.MethodTable;
		//				ClrType clrt;
		//				if (typeDct1.TryGetValue(tname, out clrt))
		//				{
		//					++dupcnt1;
		//				}
		//				else
		//				{
		//					typeDct1.Add(tname, clrType);
		//				}
		//			}

		//			Assert.IsTrue(dupcnt1 == 0);

		//		}
		//		catch (Exception ex)
		//		{
		//			error = Utils.GetExceptionErrorString(ex);
		//			Assert.IsTrue(false, error);
		//		}
		//	}
		//}

		//[TestMethod]
		//public void TestEnumerateTypes()
		//{
		//	string error = null;
		//	int dupcnt = 0;
		//	using (var clrDump = GetDump())
		//	{
		//		try
		//		{

		//			List<string>[] duplicateTypeMethodTables;
		//			var typeInfos = Indexer.GetTypes(clrDump, out duplicateTypeMethodTables, out error);
		//			string[][] typeNames = typeInfos.Item1;
		//			ClrElementType[][] typeElements = typeInfos.Item2;
		//			int[][] typeBases = typeInfos.Item3;
		//			ulong[][] typeMethodTables = typeInfos.Item6;
		//			int[][] typeMethodTablesMap = typeInfos.Item7;


		//			var path = TestConfiguration.OutPath0 + clrDump.DumpFileName + ".TYPELIST.txt";

		//			StreamWriter wr = null;
		//			try
		//			{
		//				ulong[] origTypeMethodTables = new ulong[typeMethodTables[0].Length];
		//				Array.Copy(typeMethodTables[0],origTypeMethodTables,origTypeMethodTables.Length);
		//				int[] origTypeMethodTablesMap = new int[typeMethodTablesMap[0].Length];
		//				Array.Copy(typeMethodTablesMap[0],origTypeMethodTablesMap,origTypeMethodTablesMap.Length);
		//				Array.Sort(origTypeMethodTablesMap,origTypeMethodTables);

		//				wr = new StreamWriter(path);
		//				wr.WriteLine(typeNames[0].Length);
		//				for (int i = 0, icnt = typeNames[0].Length; i < icnt; ++i)
		//				{
		//					wr.Write(Utils.SmallNumberHeader(i));
		//					wr.Write(Utils.SmallNumberHeader(typeBases[0][i]));
		//					wr.Write(Utils.AddressStringHeader(origTypeMethodTables[i]));
		//					wr.Write(typeElements[0][i] + "  ");
		//					wr.WriteLine(typeNames[0][i]);
		//				}
		//			}
		//			catch (Exception)
		//			{

		//				throw;
		//			}
		//			finally
		//			{
		//				wr?.Close();
		//			}

		//			var runtime = clrDump.Runtimes[0];
		//			runtime.Flush();
		//			var heap = runtime.Heap;

		//			var segs = heap.Segments;
		//			for (int i = 0, icnt = segs.Count; i < icnt; ++i)
		//			{
		//				var seg = segs[i];
		//				ulong addr = seg.FirstObject;
		//				while (addr != 0ul)
		//				{
		//					var clrType = heap.GetObjectType(addr);
		//					if (clrType == null) goto NEXT_OBJECT;

		//					if (clrType.Name == "Free")
		//					{
		//						var r0 = GetTypeInfo(typeNames, typeElements, typeBases, typeMethodTables, typeMethodTablesMap,
		//							clrType.MethodTable);
		//						var r1 = GetTypeInfo(typeNames, typeElements, typeBases, typeMethodTables, typeMethodTablesMap,
		//							clrType.Name, clrType.MethodTable);
		//						Assert.IsTrue(
		//							r0.Item1 == r1.Item1
		//							&& r0.Item2 == r1.Item2
		//							&& r0.Item3 == r1.Item3
		//							&& r0.Item4 == r1.Item4
		//							&& r0.Item5 == r1.Item5
		//							);
		//					}


		//					NEXT_OBJECT:
		//					addr = seg.NextObject(addr);
		//				}
		//			}
		//			Assert.IsTrue(true);
		//		}
		//		catch (Exception ex)
		//		{
		//			Assert.IsTrue(false,Utils.GetExceptionErrorString(ex));
		//		}
		//	}
		//}


		[TestMethod]
		public void TestBaseType()
		{
			var baseName = "ECS.Common.HierarchyCache.Structure.Position";
			var typeNames = new SortedSet<string>(StringComparer.Ordinal);
			string error = null;
			using (var clrDump = GetDump(@"C:\WinDbgStuff\Dumps\Analytics\Baly\AnalyticsLatencyDump06062016 03354291.dmp"))
			{
				try
				{
					var dct = new SortedDictionary<uint, List<string>>();
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType?.BaseType == null) goto NEXT_OBJECT;
							if (baseName == clrType.BaseType.Name)
								typeNames.Add(clrType.Name);

							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}
					var typeNameAry = typeNames.ToArray();
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}


		private static Tuple<int, string, ClrElementType, ulong, int> GetTypeInfo(string[][] typeNames,
										ClrElementType[][] typeElements,
										int[][] typeBases,
										ulong[][] typeMethodTables,
										int[][] typeMethodTablesMap,
										ulong methodTable)
		{
			int ndx = Array.BinarySearch(typeMethodTables[0], methodTable);
			int id = typeMethodTablesMap[0][ndx];
			string name = typeNames[0][id];
			int baseType = typeBases[0][id];
			ClrElementType elem = typeElements[0][id];
			return new Tuple<int, string, ClrElementType, ulong, int>(id, name, elem, methodTable, baseType);
		}



		private static Tuple<int, string, ClrElementType, ulong, int> GetTypeInfo(string[][] typeNames,
								ClrElementType[][] typeElements,
								int[][] typeBases,
								ulong[][] typeMethodTables,
								int[][] typeMethodTablesMap,
								string typeName,
								ulong mthdTbl)
		{
			int id = Array.BinarySearch(typeNames[0], typeName, StringComparer.Ordinal);
			string name = typeNames[0][id];
			int baseType = typeBases[0][id];
			ClrElementType elem = typeElements[0][id];

			int mapId = Array.BinarySearch(typeMethodTables[0], mthdTbl);
			ulong methodTable = typeMethodTables[0][mapId];

			return new Tuple<int, string, ClrElementType, ulong, int>(id, name, elem, methodTable, baseType);
		}

		[TestMethod]
		public void TestMetadataTokensAndMethodtbls()
		{
			string error = null;
			using (var clrDump = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\Eze.Analytics.O971_160824_095212.FC.dupcache8a.dmp"))
			{
				try
				{
					var tokensDct = new SortedDictionary<uint, List<string>>();
					var methodDct = new SortedDictionary<ulong, List<string>>();
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							ClrType clrType = heap.GetObjectType(addr);
							if (clrType == null) goto NEXT_OBJECT;
							var clrTypeName = clrType.Name;
							var metadataToken = clrType.MetadataToken;
							List<string> names;
							if (tokensDct.TryGetValue(metadataToken, out names))
							{

								if (!names.Contains(clrTypeName))
									names.Add(clrTypeName);
							}
							else
							{
								tokensDct.Add(metadataToken, new List<string>() { clrTypeName });
							}

							var mthTbl = clrType.MethodTable;
							if (methodDct.TryGetValue(mthTbl, out names))
							{
								if (!names.Contains(clrTypeName))
									names.Add(clrTypeName);
							}
							else
							{
								methodDct.Add(mthTbl, new List<string>() { clrTypeName });
							}

							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}
					//var path = TestConfiguration.OutPath0 + clrDump.DumpFileName + ".Metatokens.txt";
					var path = clrDump.DumpPath + ".MetatokensMethodTables.txt";
					StreamWriter wr = null;
					try
					{
						StringBuilder sb = new StringBuilder(4096);
						wr = new StreamWriter(path);
						wr.WriteLine("#### METATOKENS, count: " + tokensDct.Count);
						foreach (var kv in tokensDct)
						{
							wr.Write(Utils.UintHexStringHeader(kv.Key));
							kv.Value.Sort(StringComparer.Ordinal);
							sb.Clear();
							sb.Append(Utils.SmallIdHeader(kv.Value.Count));
							sb.Append(kv.Value[0]);
							for (int j = 1, jcnt = kv.Value.Count; j < jcnt; ++j)
							{
								sb.Append("; ").Append(kv.Value[j]);
							}
							wr.WriteLine(sb.ToString());
						}
						wr.WriteLine("#### METHODTABLES, count: " + methodDct.Count);
						foreach (var kv in methodDct)
						{
							wr.Write(Utils.AddressStringHeader(kv.Key));
							kv.Value.Sort(StringComparer.Ordinal);
							sb.Clear();
							sb.Append(Utils.SmallIdHeader(kv.Value.Count));
							sb.Append(kv.Value[0]);
							for (int j = 1, jcnt = kv.Value.Count; j < jcnt; ++j)
							{
								sb.Append("; ").Append(kv.Value[j]);
							}
							wr.WriteLine(sb.ToString());
						}
					}
					catch (Exception ex)
					{
						error = Utils.GetExceptionErrorString(ex);
						Assert.IsTrue(false, error);
					}
					finally
					{
						wr?.Close();
					}

				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

		const string dmpRootDir = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Raya\";

		private string[] dmps = new string[]
		{
			"Eze.Analytics.Svc.exe_160622_104356NFNC0.dmp",
			"Eze.Analytics.Svc.exe_160622_104627NFNC1.dmp",
			"Eze.Analytics.Svc.exe_160622_104931NFNC2.dmp",
			"Eze.Analytics.Svc.exe_160622_105920NFNC3.dmp",
			"Eze.Analytics.Svc.exe_160622_110507NFNC4.dmp",
			"Eze.Analytics.Svc.exe_160622_111454NFNC5.dmp",
			"Eze.Analytics.Svc.exe_160622_112540NFNC6.dmp",
			"Eze.Analytics.Svc.exe_160622_113329NFNC7.dmp",
			"Eze.Analytics.Svc.exe_160622_114947NFNC8.dmp",
			"Eze.Analytics.Svc.exe_160622_125135NFNC9.dmp",
			"Eze.Analytics.Svc.exe_160622_140603NFNC10.dmp",
			"Eze.Analytics.Svc.exe_160622_160225NFNC11.dmp",
		};



		[TestMethod]
		public void TestCompareInstances()
		{
			string error = null;
			try
			{
				for (int f = 0; f < dmps.Length; ++f)
				{
					string dmpPath = dmpRootDir + dmps[f];
					var mapAndOutPaths = DumpFileMoniker.GetAndCreateMapFolders(dmpPath, out error);
					ClrtDump clrDump = OpenCrashDump(dmpPath, out error);
					using (clrDump)
					{
						var dct = new SortedDictionary<string, KeyValuePair<int, ulong>>();
						var runtime = clrDump.Runtimes[0];
						var heap = runtime.Heap;
						var segs = heap.Segments;
						for (int i = 0, icnt = segs.Count; i < icnt; ++i)
						{
							var seg = segs[i];
							ulong addr = seg.FirstObject;
							while (addr != 0ul)
							{
								var clrType = heap.GetObjectType(addr);
								if (clrType == null || clrType.Name == "Error") goto NEXT_OBJECT;

								var name = clrType.Name;
								var size = clrType.GetSize(addr);
								var metadataToken = clrType.MetadataToken;
								KeyValuePair<int, ulong> info;
								if (dct.TryGetValue(name, out info))
								{
									dct[name] = new KeyValuePair<int, ulong>(info.Key + 1, info.Value + size);
								}
								else
								{
									dct.Add(name, new KeyValuePair<int, ulong>(1, size));
								}

								NEXT_OBJECT:
								addr = seg.NextObject(addr);
							}
						}
						var path = mapAndOutPaths.Item2 + Path.DirectorySeparatorChar + clrDump.DumpFileName + ".TYPECOUNTANDSIZES.txt";
						StreamWriter wr = null;
						try
						{
							StringBuilder sb = new StringBuilder(4096);
							wr = new StreamWriter(path);
							wr.WriteLine(dct.Count);
							foreach (var kv in dct)
							{
								wr.WriteLine("[" + kv.Value.Key + "] (" + kv.Value.Value + ") " + kv.Key);
							}
						}
						catch (Exception)
						{
							;
						}
						finally
						{
							wr?.Close();
						}
					} // using
				} // for loop
			}
			catch (Exception ex)
			{
				Assert.IsTrue(false, Utils.GetExceptionErrorString(ex));
			}
		}

		[TestMethod]
		public void TestGetTypeInstanceColumns()
		{
			string error = null;

			string[] fieldNames = new[]
			{
				"_columnName",
				"table"
			};
			string typeName = "System.Data.DataColumn";
			string dmpPath = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Baly\AnalyticsLatencyDump06062016 03354291.dmp";
			var mapAndOutPaths = DumpFileMoniker.GetAndCreateMapFolders(dmpPath, out error);
			int colWithNullTblCnt = 0;
			HashSet<ulong> dataSets = new HashSet<ulong>();

			try
			{
				ClrtDump clrDump = OpenCrashDump(dmpPath, out error);
				using (clrDump)
				{
					var colDct = new SortedDictionary<string, KeyValuePair<int, List<ulong>>>();
					var tblDct = new SortedDictionary<ulong, List<string>>();

					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					ClrInstanceField[] flds = new ClrInstanceField[fieldNames.Length];
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType == null || clrType.Name != typeName) goto NEXT_OBJECT;
							var fld1 = clrType.GetFieldByName(fieldNames[0]);
							string str = (string)fld1.GetValue(addr, false, true);
							if (str == null) goto NEXT_OBJECT;
							var fld2 = clrType.GetFieldByName(fieldNames[1]);
							var obj = fld2.GetValue(addr);
							if (obj == null)
							{
								++colWithNullTblCnt;
								goto NEXT_OBJECT;
							}
							ulong tbl = (ulong)obj;

							KeyValuePair<int, List<ulong>> kv;
							if (colDct.TryGetValue(str, out kv))
							{
								var lst = kv.Value;
								if (!lst.Contains(tbl))
									lst.Add(tbl);
								colDct[str] = new KeyValuePair<int, List<ulong>>(kv.Key + 1, lst);
							}
							else
							{
								var lst = new List<ulong>() { tbl };
								colDct.Add(str, new KeyValuePair<int, List<ulong>>(1, lst));
							}
							List<string> slst;
							if (tblDct.TryGetValue(tbl, out slst))
							{
								if (!slst.Contains(str))
									slst.Add(str);
							}
							else
							{
								tblDct.Add(tbl, new List<string>() { str });
							}

							dataSets.Add(tbl);


							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}

					foreach (var kv in tblDct)
					{
						kv.Value.TrimExcess();
						kv.Value.Sort(StringComparer.Ordinal);

					}
					SortedDictionary<List<string>, List<ulong>> colTblDct = new SortedDictionary<List<string>, List<ulong>>(new Utils.StrListCmp());
					foreach (var kv in tblDct)
					{
						List<ulong> tbls;
						if (colTblDct.TryGetValue(kv.Value, out tbls))
						{
							tbls.Add(kv.Key);
						}
						else
						{
							colTblDct.Add(kv.Value, new List<ulong>() { kv.Key });
						}
					}

					var path = mapAndOutPaths.Item2 + Path.DirectorySeparatorChar + clrDump.DumpFileName + typeName + ".TYPESELECTEDFIELDS.txt";
					StreamWriter wr = null;
					try
					{
						StringBuilder sb = new StringBuilder(4096);
						wr = new StreamWriter(path);
						wr.WriteLine("### columns with null table count: " + colWithNullTblCnt);
						wr.WriteLine("### unique column names count: " + colDct.Count);
						wr.WriteLine();

						int total = 0;
						foreach (var kv in colDct)
						{
							wr.WriteLine("[" + kv.Value.Key + "] (" + kv.Value.Value.Count + ") " + kv.Key);
							total += kv.Value.Key * kv.Value.Value.Count;
						}
						wr.WriteLine();
						wr.WriteLine("### total column count: " + total);
						wr.WriteLine();



						wr.WriteLine();
						wr.WriteLine("### table same cols count: " + colTblDct.Count);
						wr.WriteLine();
						total = 0;
						foreach (var kv in colTblDct)
						{
							wr.Write("[" + kv.Value.Count + "] (" + kv.Key.Count + ") ");
							wr.Write(kv.Key[0]);
							for (int i = 1, icnt = kv.Key.Count; i < icnt; ++i)
							{
								wr.Write(", " + kv.Key[i]);
							}
							wr.WriteLine();
							total += kv.Value.Count * kv.Key.Count;
						}
						wr.WriteLine();
						wr.WriteLine("### total column count: " + total);
						wr.WriteLine();

					}
					catch (Exception)
					{
						;
					}
					finally
					{
						wr?.Close();
					}
				} // using
			}
			catch (Exception ex)
			{
				Assert.IsTrue(false, Utils.GetExceptionErrorString(ex));
			}
		}

		[TestMethod]
		public void TestCompareInstancesMergeReports()
		{
			string error;
			List<string> lst = new List<string>(dmps.Length);
			StringBuilder sb = new StringBuilder(256);
			for (int i = 0; i < dmps.Length; ++i)
			{
				sb.Clear();
				string dmpBaseName = dmps[i].Substring(0, dmps[i].Length - 4);
				string reportName = dmps[i] + ".TYPECOUNTANDSIZES.txt";
				sb.Append(dmpRootDir).Append(dmpBaseName).Append(".map").Append(Path.DirectorySeparatorChar)
					.Append(Constants.AdHocQueryDirName).Append(Path.DirectorySeparatorChar).Append(reportName);
				lst.Add(sb.ToString());
			}
			string outFileName = dmpRootDir + "TYPECOUNTANDSIZES.merged.txt";

			var dmpCmp = new DumpComparison();
			var result = dmpCmp.MergeTypeCountAndSizeReports(lst, outFileName, out error);
			Assert.IsTrue(result, error);
		}

		[TestMethod]
		public void TestTypeSizes()
		{
			string error = null;
			using (var clrDump = GetDump())
			{
				try
				{
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType == null || clrType.Name == "Free") goto NEXT_OBJECT;

							if (clrType.IsString)
							{
								var bsz = clrType.BaseSize;
								var sz = clrType.GetSize(addr);
								var str = ClrMDRIndex.ValueExtractor.GetStringAtAddress(addr, heap);
								var lenInBytes = str.Length * 2 + 26;
								var csz = Utils.RoundupToPowerOf2Boundary(lenInBytes, 8);

							}
							else if (clrType.ElementType == ClrElementType.SZArray)
							{
								var bsz = clrType.BaseSize;
								var sz = clrType.GetSize(addr);
								var elCnt = clrType.GetArrayLength(addr);
							}
							else if (clrType.Name.StartsWith("System.Collections.Generic.Dictionary<"))
							{
								var bsz = clrType.BaseSize;
								var sz = clrType.GetSize(addr);
							}


							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

		[TestMethod]
		public void TestTypeSize()
		{
			ulong[] testTypeSizeAddress = new ulong[]
			{
				0x0000a12f531808,
				0x0000a12f531848,
				0x0000a12f531888,
				0x0000a12f5318c8,
				0x0000a4aeeba478,
				0x0000a4aeeba4b8,
				0x0000a4aeeba4f8,
			};

			string error = null;
			ulong totalSize = 0;
			using (var clrDump = GetDump())
			{
				try
				{
					Stopwatch stopWatch = new Stopwatch();
					stopWatch.Start();
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					HashSet<ulong> done = new HashSet<ulong>();
					string typeName = string.Empty;
					for (int i = 0, icnt = testTypeSizeAddress.Length; i < icnt; ++i)
					{
						var addr = testTypeSizeAddress[i];
						var clrType = heap.GetObjectType(addr);
						if (i == 0) typeName = clrType.Name;
						var baseSize = clrType.BaseSize;
						var size = clrType.GetSize(addr);
						var gsize = ClrtDump.GetObjectSize(heap, clrType, addr, done);
						totalSize += gsize;
					}

					stopWatch.Stop();
					TestContext.WriteLine("TEST DURATION: " + Utils.DurationString(stopWatch.Elapsed));
					TestContext.WriteLine("Type (at first address): " + typeName);
					TestContext.WriteLine("Address count: " + testTypeSizeAddress.Length);
					TestContext.WriteLine("Total size: " + totalSize);

				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}




		[TestMethod]
		public void TestTypeSizes2()
		{
			ulong[] testTypeSizeAddress = new ulong[]
			{
				// int[]
//0x000000026efc30,
//0x000000026efc98,
//0x000000026f0008,
//0x000000026f0058,
//0x000000026f03c8,
//0x000000026f0418,
// TimeSpan[]
//0x000000023736d0,
//0x000000026f0520,
////System.Decimal[,]
//0x00000002376ed0,
//// System.Globalization.CalendarData[]
//0x000000023745a0,
//// DumpSearch.DummyClass[]
//0x000000023736f8,
//0x000000026ef938,
//0x000000026f0548,
// System.Guid[]
0x00017e130bfc50,
0x00017e130bfc68,
0x00017e130bfcc0,
0x00017e130c0148,
0x00017e130c01a0,
0x00017e130c0628,
0x00017e130c0680,
			};

			string error = null;
			ulong totalSize = 0;
			using (var clrDump = GetDump(_dumpPath))
			{
				try
				{
					Stopwatch stopWatch = new Stopwatch();
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					string typeName = string.Empty;
					List<quadruple<int, ulong, int, ulong>> results = new List<quadruple<int, ulong, int, ulong>>(testTypeSizeAddress.Length);
					HashSet<ulong> done = new HashSet<ulong>();
					for (int i = 0, icnt = testTypeSizeAddress.Length; i < icnt; ++i)
					{
						var addr = testTypeSizeAddress[i];
						var clrType = heap.GetObjectType(addr);
						if (i == 0) typeName = clrType.Name;
						var baseSize = clrType.BaseSize;
						var size = clrType.GetSize(addr);
						var acnt = clrType.GetArrayLength(addr);
						var gsize = ClrtDump.GetObjectSize(heap, clrType, addr, done);
						totalSize += gsize;
						results.Add(new quadruple<int, ulong, int, ulong>(baseSize, size, acnt, gsize));
					}
					stopWatch.Stop();
					TestContext.WriteLine("TEST DURATION: " + Utils.DurationString(stopWatch.Elapsed));
					TestContext.WriteLine("Type (at first address): " + typeName);
					TestContext.WriteLine("Address count: " + testTypeSizeAddress.Length);
					TestContext.WriteLine("Total size: " + totalSize);
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

		[TestMethod]
		public void TestSeletedTypeTotalSize()
		{
			//string typeName = "ECS.Common.HierarchyCache.MarketData.FastSmartWeakEvent<System.EventHandler>";
			//string typeName = "DumpSearch.DummyClass";
			string typeName = "DumpSearch.ComplexStruct[]";
			string error = null;
			ulong[] typeAddresses = new[] { 0x00000027ff968ul };

			using (var clrDump = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\DumpSearch\DumpSearch.exe_160711_121816.dmp"))
			{
				try
				{
					var runtime = clrDump.Runtimes[0];
					ClrHeap heap = null;

					if (typeAddresses == null)
					{
						List<ulong> lst = new List<ulong>(200000);
						heap = runtime.Heap;
						var segs = heap.Segments;
						for (int i = 0, icnt = segs.Count; i < icnt; ++i)
						{
							var seg = segs[i];
							ulong addr = seg.FirstObject;
							while (addr != 0ul)
							{
								var clrType = heap.GetObjectType(addr);
								if (clrType == null) goto NEXT_OBJECT;

								if (clrType.Name == typeName)
									lst.Add(addr);
								NEXT_OBJECT:
								addr = seg.NextObject(addr);
							}
						}
						typeAddresses = lst.ToArray();
					}
					runtime.Flush();
					heap = runtime.Heap;

					ulong totalSize = 0;
					HashSet<ulong> done = new HashSet<ulong>();
					Stopwatch stopWatch = new Stopwatch();
					stopWatch.Start();

					for (int i = 0, icnt = typeAddresses.Length; i < icnt; ++i)
					{
						var addr = typeAddresses[i];
						var clrType = heap.GetObjectType(addr);

						var gsize = ClrtDump.GetObjectSize(heap, clrType, addr, done);
						totalSize += gsize;

					}

					stopWatch.Stop();
					TestContext.WriteLine("TEST DURATION: " + Utils.DurationString(stopWatch.Elapsed));
					TestContext.WriteLine("Type name: " + typeName);
					TestContext.WriteLine("Address count: " + typeAddresses.Length);
					TestContext.WriteLine("Total size: " + totalSize);

				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

		[TestMethod]
		public void TestFindStringParents()
		{
			string error = null;
			string strContent = "CSUS";
			var dct = new Dictionary<string, List<KeyValuePair<ulong, ulong>>>(StringComparer.Ordinal);
			var set = new HashSet<string>(StringComparer.Ordinal);
			using (var clrDump = GetDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\Counters1\Eze.Analytics.Svc.exe_161007_133429.dmp"))
			{
				try
				{
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType == null) goto NEXT_OBJECT;
							if (clrType.Fields == null || clrType.Fields.Count < 1) goto NEXT_OBJECT;
							if (set.Contains(clrType.Name)) goto NEXT_OBJECT;
							bool hasStrings = false;
							bool isValueClass = clrType.IsValueClass;
							for (int j = 0, jcnt = clrType.Fields.Count; j < jcnt; ++j)
							{
								var fld = clrType.Fields[i];
								if (!fld.Type.IsString) continue;
								hasStrings = true;
								var str = (string)fld.GetValue(addr, isValueClass, true);
								if (str == null) continue;
								if (Utils.SameStrings(strContent, str))
								{
									var key = GetFieldTypeKey(clrType, fld);
									List<KeyValuePair<ulong, ulong>> lst;
									if (dct.TryGetValue(key, out lst))
									{
										var strAddr = (ulong)fld.GetValue(addr, isValueClass, false);
										lst.Add(new KeyValuePair<ulong, ulong>(strAddr, addr));
									}
								}
							}
							if (!hasStrings)
							{
								set.Add(clrType.Name);
							}
							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}



		#endregion Types

		#region Snippets

		[TestMethod]
		public void TestSnippet()
		{
			string error = null;
			using (var clrDump = GetDump())
			{
				try
				{
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType == null) goto NEXT_OBJECT;


							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

		[TestMethod]
		public void TestSnippet2()
		{
			string error = null;
			using (var clrDump = GetDump())
			{
				try
				{
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

		[TestMethod]
		public void TestSnippet3()
		{
			string str = @"some string";
			string error = null;
			using (var clrDump = GetDump(@"dumpPath"))
			{
				List<ulong> addrLst = new List<ulong>(1024);
				try
				{
					var runtime = clrDump.Runtimes[0];
					var heap = runtime.Heap;
					var segs = heap.Segments;
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						var seg = segs[i];
						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							var clrType = heap.GetObjectType(addr);
							if (clrType == null || !clrType.IsString) goto NEXT_OBJECT;
							var stVal = ClrMDRIndex.ValueExtractor.GetStringAtAddress(addr, heap);
							if (Utils.SameStrings(stVal, str))
							{
								addrLst.Add(addr);
							}

							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
					}
					StreamWriter wr = null;
					try
					{
						Tuple<string, string> dmpFolders = DumpFileMoniker.GetAndCreateMapFolders(clrDump.DumpPath, out error);

						var path = dmpFolders.Item2 + @"\someName_ADDRESSES.txt";

						wr = new StreamWriter(path);
						wr.WriteLine("### \"" + str + "\" [" + addrLst.Count + "]");
						for (int i = 0, icnt = addrLst.Count; i < icnt; ++i)
						{
							wr.WriteLine(Utils.AddressString(addrLst[i]));
						}
						wr.Close();
						wr = null;
					}
					finally
					{
						wr?.Close();
					}
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					Assert.IsTrue(false, error);
				}
			}
		}

        #endregion Snippets


        #region references

        string[] excludedTypeNames = new[]
{
                "Free",
                "System.DateTime",
                "System.Decimal",
                "System.Guid",
                "System.String",
                "System.TimeSpan",

            };

        bool IsExludedType(string typeName)
        {
            return Array.IndexOf(excludedTypeNames, typeName) >= 0;
        }


        [TestMethod]
        public void TestRefProcessing3()
        {
            string outFolderPath = null;
            string[] dumps = new string[]
            {
            /*  0 */    @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.dmp",
            /*  1 */    @"D:\Jerzy\WinDbgStuff\dumps\TradingService\Tortoise\tradingservice_0615.dmp",
            /*  2 */    @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Baly\analytics7_1510301630.Baly.dmp",
            /*  3 */    @"D:\Jerzy\WinDbgStuff\dumps\Analytics\BigOne\Analytics11_042015_2.BigOne.dmp"
            };
            ClrtDump dmp = GetDump(dumps[3]);
            ulong[] instances = null;
            //BlockingCollection<KeyValuePair<int, ulong[]>> queue = null;
            Thread oThread = null;
            BinaryWriter bw = null;
            Stopwatch stopWatch = new Stopwatch();
            Stopwatch totStopWatch = new Stopwatch();
            stopWatch.Start();
            totStopWatch.Start();
            TestContext.WriteLine("DUMP PATH: " + dmp.DumpPath);
            TestContext.WriteLine(dmp.DumpFileName + " size: " + Utils.LargeNumberString(dmp.DumpSize()) + ", created: " + Utils.DateTimeString(dmp.DumpCreationTime()) + ", last written: " + Utils.DateTimeString(dmp.DumpLastWriteTime()));
            TestContext.WriteLine("DUMP OPEN DURATION: " + Utils.StopAndGetDurationStringAndRestart(stopWatch));
            int buffer_size = 1024;
            ulong[] buffer = new ulong[buffer_size];

            using (dmp)
            {
                var heap = dmp.Heap;
                try
                {
                    outFolderPath = Path.GetDirectoryName(dmp.DumpPath) + Path.DirectorySeparatorChar + "tests";
                    if (!Directory.Exists(outFolderPath))
                    {
                        Directory.CreateDirectory(outFolderPath);
                    }
                    bw = new BinaryWriter(File.Open(outFolderPath + @"\instances.bin", FileMode.Create));
                    bw.Write((int)0);
                    int acount = 0;
                    var segs = heap.Segments;
                    for (int segNdx = 0, icnt = segs.Count; segNdx < icnt; ++segNdx)
                    {
                        var seg = segs[segNdx];
                        ulong addr = seg.FirstObject;
                        while (addr != 0ul)
                        {
                            var clrType = heap.GetObjectType(addr);
                            if (clrType == null) goto NEXT_OBJECT;

                            bw.Write(addr);
                            ++acount;

                            NEXT_OBJECT:
                            addr = seg.NextObject(addr);
                        }
                    }
                    bw.Seek(0, SeekOrigin.Begin);
                    bw.Write(acount);
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

                TestContext.WriteLine("COLLECTING INSTANCES DURATION: " + Utils.StopAndGetDurationStringAndRestart(stopWatch));

                string error;
                instances = Utils.ReadUlongArray(outFolderPath + @"\instances.bin", out error);
                bw = new BinaryWriter(File.Open(outFolderPath + @"\refsdata.bin", FileMode.Create));


                TestContext.WriteLine("INSTANCE COUNT: " + Utils.CountString(instances.Length) + " READING INSTANCES DURATION: " + Utils.StopAndGetDurationStringAndRestart(stopWatch));

                var fieldAddrOffsetList = new List<ulong>(64);
                for (int i = 0, icnt = instances.Length; i < icnt; ++i)
                {
                    var addr = instances[i];
                    var clrType = heap.GetObjectType(addr);
                    Assert.IsNotNull(clrType);
                    //if (clrType.Name== "Eze.Server.Common.Pulse.Common.Types.ServerColumnPostionLevelCacheDictionary<System.Decimal>")
                    //{
                    //    int a = 1;
                    //}
                    if (IsExludedType(clrType.Name))
                    {
                        bw.Write((int)0);
                        continue;
                    }

                    fieldAddrOffsetList.Clear();
                    clrType.EnumerateRefsOfObjectCarefully(addr, (address, off) =>
                    {
                        fieldAddrOffsetList.Add(address);
                    });
                    if (fieldAddrOffsetList.Count < 1)
                    {
                        bw.Write((int)0);
                        continue;
                    }
                    bw.Write(fieldAddrOffsetList.Count + 1);
                    bw.Write(addr);
                    fieldAddrOffsetList.Sort();
                    for (int j = 0, jcnt = fieldAddrOffsetList.Count; j < jcnt; ++j)
                    {
                        ulong childAddr = fieldAddrOffsetList[j];
                        //if (childAddr == 0x000000008027cd20 || childAddr == 0x000000008027e5b8)
                        //{
                        //    int a = 1;
                        //}
                        bw.Write(childAddr);
                    }
                }
            }
            bw.Close();
            bw = null;
            TestContext.WriteLine("SAVING REFERENCE DATA DURATION: " + Utils.StopAndGetDurationStringAndRestart(stopWatch));

            Scullion bld = new Scullion(outFolderPath,
                                        instances,
                                        @"instances.bin",
                                        @"refsdata.bin",
                                        @"fwdrefsoffsets.bin",
                                        @"fwdrefs.bin",
                                        @"bwdrefsoffsets.bin",
                                        @"bwdrefs.bin"
                                        );

            oThread = new Thread(new ThreadStart(bld.BuildReferences));
            oThread.Start();

            oThread.Join();

            TestContext.WriteLine("REFLAG COUNT: " + Utils.CountString(bld.GetReflagCount()));
            TestContext.WriteLine("NOTFOUND COUNT: " + Utils.CountString(bld.GetNotFoundCount()));
            KeyValuePair<int, int> reversedMinmax = bld.GetReversedMinMax();
            TestContext.WriteLine("REVERSED MINMAX: " + Utils.CountString(reversedMinmax.Key) + " - " + Utils.CountString(reversedMinmax.Value));

            TestContext.WriteLine("BUILDING REFERENCES DURATION: " + Utils.StopAndGetDurationString(stopWatch));
            TestContext.WriteLine("TOTAL DURATION: " + Utils.StopAndGetDurationString(totStopWatch));

        }




        #endregion references

        #region Open Dump/Map

        /// <summary>
        /// Opens dump map using th from App.config, key: test_mapfolder.
        /// </summary>
        /// <returns>Instance of a dump map, or null on failure.</returns>
        private DumpIndex OpenMap0()
		{
			string error;
			var map = DumpIndex.OpenIndexInstanceReferences(new Version(1, 0, 0), TestConfiguration.MapPath0, 0, out error);
			Assert.IsTrue(map != null);
			return map;
		}


		private ClrtDump GetDump(string path, out string error)
		{
			var clrDump = OpenCrashDump(path, out error);
			return clrDump;
		}

		private ClrtDump GetDump(string path)
		{
			string error;
			var clrDump = OpenCrashDump(path, out error);
			Assert.IsTrue(clrDump != null, error);
			return clrDump;
		}

		private ClrtDump GetDump()
		{
			string error;
			Assert.IsTrue(Setup.RecentAdhocList.Count>0);
			var clrDump = OpenCrashDump(Setup.GetRecentAdhocPath(0), out error);
			Assert.IsTrue(clrDump != null, error);
			return clrDump;
		}

		private ClrtDump GetCurrentDump()
		{
			string error;
			var clrDump = OpenCrashDump(_dumpPath, out error);
			Assert.IsTrue(clrDump != null, error);
			return clrDump;
		}


		private static ClrtDump OpenCrashDump(string path, out string error)
		{
			error = null;
			var clrDump = new ClrtDump(path);
			var initOk = clrDump.Init(out error);
			return clrDump;
		}


		#endregion Open Dump/Map

	}
}
