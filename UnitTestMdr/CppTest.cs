using System;
using System.IO;
using System.Collections.Concurrent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using ClrMDRUtil.Utils;
using ClrMDRIndex;

namespace UnitTestMdr
{
	[TestClass]
	public class CppTest
	{
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

		private ulong[] GenerateData0()
		{
			return new ulong[] {
				0x00000280254a20,  // 0
				0x00000280254b40,  // 1
				0x00000280254c68,  // 2
				0x00000280254d80,  // 3
				0x00000280254ea0,  // 4
				0x00000280254fa0,  // 5
				0x00000280255108,  // 6
				0x00000280255210,  // 7
				0x000002802552d8,  // 8
				0x00000280255400,  // 9
				0x00000280255570,  // 10
				0x000002802556a0,  // 1
				0x000002802557d0,  // 2
				0x000002802558f8,  // 3
				0x00000280255a60,  // 4
				0x00000280255c18,  // 5
				0x00000280255d50,  // 6
				0x00000280255ed0,  // 7
				0x00000280256040,  // 8
				0x00000280256168,  // 9
				0x000002802562a0,  // 20
				0x000002802563e0,  // 1
				0x00000280256580,  // 2
				0x000002802566b8,  // 3
				0x000002802567f8,  // 4
			};
		}

		[TestMethod]
		public void TestRefProcessing0()
		{
			BlockingCollection<KeyValuePair<int, ulong[]>> queue = new BlockingCollection<KeyValuePair<int, ulong[]>>();
			var instances = GenerateData0();
			Scullions.ReferenceBuilder bld = new Scullions.ReferenceBuilder(queue, @"C:\WinDbgStuff\Dumps\Tests");
			bld.Init(instances);
			Thread oThread = new Thread(new ThreadStart(bld.Work));
			oThread.Start();

			queue.Add(new KeyValuePair<int, ulong[]>(0, new ulong[] { instances[0], instances[2], instances[0], instances[1] }));
			queue.Add(new KeyValuePair<int, ulong[]>(1, new ulong[] { instances[7], instances[2], instances[11], instances[12] }));
			queue.Add(new KeyValuePair<int, ulong[]>(2, new ulong[] { instances[8], instances[1], instances[0], instances[2], instances[13], instances[14] }));
			queue.Add(new KeyValuePair<int, ulong[]>(3, Utils.EmptyArray<ulong>.Value));
			queue.Add(new KeyValuePair<int, ulong[]>(4, new ulong[] { instances[3], instances[2], instances[0], instances[1] }));

			queue.Add(new KeyValuePair<int, ulong[]>(-1, Utils.EmptyArray<ulong>.Value));
			oThread.Join();
		}

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
		public void TestRefProcessing1()
		{
			string outFolderPath = @"C:\WinDbgStuff\Dumps\Tests";
			ClrtDump dmp = OpenDump(@"C:\WinDbgStuff\Dumps\Analytics\BigOne\Analytics11_042015_2.Big.dmp");
			ulong[] instances = null;
			BlockingCollection<KeyValuePair<int, ulong[]>> queue = null;
			Thread oThread = null;
			BinaryWriter bw = null;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			TestContext.WriteLine(dmp.DumpFileName + " DUMP OPEN DURATION: " + Utils.StopAndGetDurationStringAndRestart(stopWatch));

			using (dmp)
			{
				var heap = dmp.Heap;
				try
				{
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
				catch(Exception ex)
				{
					Assert.IsTrue(false, ex.ToString());
				}
				finally
				{
					bw?.Close();
					bw = null;
				}
				TestContext.WriteLine(dmp.DumpFileName + " COLLECTING INSTANCES DURATION: " + Utils.StopAndGetDurationStringAndRestart(stopWatch));

				string error;
				instances = Utils.ReadUlongArray(outFolderPath + @"\instances.bin", out error);

				TestContext.WriteLine(dmp.DumpFileName + " INSTANCE COUNT: " + instances.Length + " READING INSTANCES DURATION: " + Utils.StopAndGetDurationStringAndRestart(stopWatch));

				queue = new BlockingCollection<KeyValuePair<int, ulong[]>>();
				Scullions.ReferenceBuilder bld = new Scullions.ReferenceBuilder(queue, outFolderPath);
				bld.Init(instances);

				oThread = new Thread(new ThreadStart(bld.Work));
				oThread.Start();

				var fieldAddrOffsetList = new List<KeyValuePair<ulong, int>>(64);
				for (int i = 0, icnt = instances.Length; i < icnt; ++i)
				{
					var addr = instances[i];
					var clrType = heap.GetObjectType(addr);
					Assert.IsNotNull(clrType);
					if (IsExludedType(clrType.Name))
					{
						queue.Add(new KeyValuePair<int, ulong[]>(i, Utils.EmptyArray<ulong>.Value));
						continue;
					}

					fieldAddrOffsetList.Clear();
					clrType.EnumerateRefsOfObjectCarefully(addr, (address, off) =>
					{
						fieldAddrOffsetList.Add(new KeyValuePair<ulong, int>(address, off));
					});
					if (fieldAddrOffsetList.Count < 1)
					{
						queue.Add(new KeyValuePair<int, ulong[]>(i, Utils.EmptyArray<ulong>.Value));
						continue;
					}
					ulong[] data = new ulong[fieldAddrOffsetList.Count + 1];
					data[0] = addr;
					for (int j = 0, jcnt = fieldAddrOffsetList.Count; j < jcnt; ++j)
					{
						data[j + 1] = fieldAddrOffsetList[j].Key;
					}
					queue.Add(new KeyValuePair<int, ulong[]>(i, data));
				}
			}
			queue.Add(new KeyValuePair<int, ulong[]>(-1, Utils.EmptyArray<ulong>.Value));
			oThread.Join();

			TestContext.WriteLine(dmp.DumpFileName + " BUILDING REFERENCES DURATION: " + Utils.StopAndGetDurationString(stopWatch));

		}

		[TestMethod]
		public void TestRefProcessing2()
		{
			//string outFolderPath = @"C:\WinDbgStuff\Dumps\Analytics\BigOne\Tests";
			//ClrtDump dmp = OpenDump(@"C:\WinDbgStuff\Dumps\Analytics\BigOne\Analytics11_042015_2.Big.dmp");
			string outFolderPath = @"C:\WinDbgStuff\Dumps\Analytics\Highline\Tests";
			ClrtDump dmp = OpenDump(@"C:\WinDbgStuff\Dumps\Analytics\Highline\analyticsdump111.dlk.dmp");
			ulong[] instances = null;
			//BlockingCollection<KeyValuePair<int, ulong[]>> queue = null;
			Thread oThread = null;
			BinaryWriter bw = null;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			TestContext.WriteLine(dmp.DumpFileName + " DUMP OPEN DURATION: " + Utils.StopAndGetDurationStringAndRestart(stopWatch));

			using (dmp)
			{
				var heap = dmp.Heap;
				try
				{
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

				TestContext.WriteLine(dmp.DumpFileName + " COLLECTING INSTANCES DURATION: " + Utils.StopAndGetDurationStringAndRestart(stopWatch));

				string error;
				instances = Utils.ReadUlongArray(outFolderPath + @"\instances.bin", out error);
				bw = new BinaryWriter(File.Open(outFolderPath + @"\refsdata.bin", FileMode.Create));


				TestContext.WriteLine(dmp.DumpFileName + " INSTANCE COUNT: " + instances.Length + " READING INSTANCES DURATION: " + Utils.StopAndGetDurationStringAndRestart(stopWatch));

				var fieldAddrOffsetList = new List<KeyValuePair<ulong, int>>(64);
				for (int i = 0, icnt = instances.Length; i < icnt; ++i)
				{
					var addr = instances[i];
					var clrType = heap.GetObjectType(addr);
					Assert.IsNotNull(clrType);
					if (IsExludedType(clrType.Name))
					{
						bw.Write((int)0);
						continue;
					}

					fieldAddrOffsetList.Clear();
					clrType.EnumerateRefsOfObjectCarefully(addr, (address, off) =>
					{
						fieldAddrOffsetList.Add(new KeyValuePair<ulong, int>(address, off));
					});
					if (fieldAddrOffsetList.Count < 1)
					{
						bw.Write((int)0);
						continue;
					}
					//ulong[] data = new ulong[fieldAddrOffsetList.Count + 1];
					bw.Write(fieldAddrOffsetList.Count + 1);
					bw.Write(addr);
					for (int j = 0, jcnt = fieldAddrOffsetList.Count; j < jcnt; ++j)
					{
						bw.Write(fieldAddrOffsetList[j].Key);
					}
				}
			}
			bw.Close();
			bw = null;
			TestContext.WriteLine(dmp.DumpFileName + " SAVING REFERENCE DATA DURATION: " + Utils.StopAndGetDurationStringAndRestart(stopWatch));

			Scullions.ReferenceBuilder bld = new Scullions.ReferenceBuilder(outFolderPath);
			bld.Init(instances);
			oThread = new Thread(new ThreadStart(bld.Work2));
			oThread.Start();

			oThread.Join();

			TestContext.WriteLine(dmp.DumpFileName + " REFLAG COUNT: " + bld.GetReflagCount());
			TestContext.WriteLine(dmp.DumpFileName + " NOTFOUND COUNT: " + bld.GetNotFoundCount());
			KeyValuePair<int,int> reversedMinmax = (KeyValuePair<int,int>)bld.GetReversedMinMax();
			TestContext.WriteLine(dmp.DumpFileName + " REVERSED MINMAX: " + reversedMinmax.Key + " - " + reversedMinmax.Value);

			TestContext.WriteLine(dmp.DumpFileName + " BUILDING REFERENCES DURATION: " + Utils.StopAndGetDurationString(stopWatch));

		}

		#region open dump

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
