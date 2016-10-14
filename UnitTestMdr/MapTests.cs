using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClrMDRIndex;
using Microsoft.Diagnostics.Runtime;
using DmpNdxQueries;

namespace UnitTestMdr
{
	[TestClass]
	public class MapTests
	{
		private const string _mapPath = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Scopia\noDeleteMsgs.map";
		private const string _typeName = @"ECS.Common.HierarchyCache.Structure.RealPosition";

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

		#region Instance Dependencies

		[TestMethod]
		public void TestFieldDependencyFields()
		{
		    string error;
            //var map = OpenMap(@"D:\Jerzy\WinDbgStuff\Dumps\DumpTest\DumpTest.exe_160820_073947.map");
            var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\DupCacheIssue\A2_noDF.map");
            var outPath = map.ReportPath + Path.DirectorySeparatorChar + "FIELD_DEPENDENCIES.txt";
		    using (map)
		    {
                map.DebugFieldDependencyDump(outPath, out error);
            }

            Assert.IsNull(error,error);
		}

        [TestMethod]
        public void TestDependencyTree()
        {
            const string typeName = @"ECS.Common.Collections.Common.EzeBitVector";
            string error;
			//var map = OpenMap(@"D:\Jerzy\WinDbgStuff\Dumps\DumpTest\DumpTest.exe_160820_073947.map");
			//var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\DupCacheIssue\A2_noDF.map");
			var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\DupCacheIssue\A2_noDF.map");
			using (map)
            {
                int typeId = map.GetTypeId(typeName);
                ulong[] typeAddresses = map.GetTypeAddresses(typeId);
				Tuple<DependencyNode, int> result = map.GetAddressesDescendants(typeId, typeAddresses, 6, out error);
	            var count = result.Item2;
            }

            Assert.IsNull(error, error);
        }


        [TestMethod]
        public void TestFieldDependencyFields2()
        {
            string error;
            //var map = OpenMap(@"D:\Jerzy\WinDbgStuff\Dumps\DumpTest\DumpTest.exe_160820_073947.map");
            var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\DupCacheIssue\A2_noDF.map");
			Assert.IsNotNull(map);
			List<string> errors = new List<string>();
			using (map)
	        {
				//KeyValuePair<ulong, KeyValuePair<ulong, int>[]>[] parentInfos = map.FieldDependencies.GetMultiFieldParents(new ulong[]{ 0x000000031eb5c8 }, errors);
				KeyValuePair<ulong, KeyValuePair<ulong, int>[]>[] parentInfos = map.FieldDependencies.GetMultiFieldParents(new ulong[] { 0x0032bcdd8 }, errors);
			}

			//Assert.IsNull(error, error);
		}

		#endregion Instance Dependencies


		[TestMethod]
		public void TestBuildTestUnitIssues()
		{
			const string typeName = @"Microsoft.VisualStudio.TestTools.TestTypes.Unit.UnitTestResult";
			string error=null;
			//var map = OpenMap(@"D:\Jerzy\WinDbgStuff\Dumps\DumpTest\DumpTest.exe_160820_073947.map");
			//var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\DupCacheIssue\A2_noDF.map");
			var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\mainlinebuildissue\LouDump.map");
			using (map)
			{
				int typeId = map.GetTypeId(typeName);
				ulong[] typeAddresses = map.GetTypeAddresses(typeId);
				var heap = map.GetFreshHeap();
				ClrType clrType = null;
				ClrInstanceField m_timerResults = null;
				List<ulong> lst = new List<ulong>();

				for (int i = 0, icnt = typeAddresses.Length; i < icnt; ++i)
				{
					if (clrType == null)
					{
						clrType = heap.GetObjectType(typeAddresses[i]);
						if (clrType != null)
						{
							m_timerResults = clrType.GetFieldByName("m_timerResults");
							break;
						}
					}
				}


				for (int i = 0, icnt = typeAddresses.Length; i < icnt; ++i)
				{
					ulong address = SpecializedQueries.getAddressFromField(typeAddresses[i], m_timerResults, false);
					if (address == Constants.InvalidAddress)
						lst.Add(typeAddresses[i]);
				}
				int a = 1;

				
			}
			Assert.IsNull(error, error);
		}

		#region Specialized Queries


		[TestMethod]
		public void TestWeakReferenceFields()
		{
			string error;
			ulong address = 0x0000008000c0d8;
			var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\Eze.Analytics.Svc.exe.EzeBitVector.PadOff.map");
			Assert.IsNotNull(map);
			List<string> errors = new List<string>();
			using (map)
			{
				int totalCount;
				KeyValuePair<int, ulong[]>[] prefaddresses = map.GetTypeWithPrefixAddresses("System.WeakReference",false,out totalCount);

				int typeId = map.GetTypeId("System.WeakReference");
				ulong[] addresses = map.GetTypeAddresses(typeId);

				var heap = map.GetFreshHeap();
				ClrType weakReferenceType = heap.GetObjectType(address); // System.WeakReference
				ClrInstanceField m_handleField = weakReferenceType.Fields[0];
				object m_handleValue = m_handleField.GetValue(address, false, false);
				ClrType m_handleType = m_handleField.Type; //  System.IntPtr
				ClrInstanceField m_valueField = m_handleType.Fields[0];
				ulong m_valueValue = (ulong)m_valueField.GetValue((ulong)(long)m_handleValue, true, false);
				ClrType eeferencedType = heap.GetObjectType(m_valueValue); // type this WeakReference points to

				//ulong[] addrTest = new[] {address};
				var result = DmpNdxQueries.SpecializedQueries.getWeakReferenceInfos(heap, addresses, m_handleField, m_valueField);
				if (result.Item1 != null)
				{
					Assert.IsTrue(false,result.Item1);
				}



				string repPath = map.ReportPath + @"\WeakReferenceObjects.txt";
				StreamWriter sw = null;
				try
				{
					sw = new StreamWriter(repPath);
					var infos = result.Item2;
					Array.Sort(infos,new Utils.TripleUlUlStrByStrUl2Cmp());

					sw.WriteLine("### MDRDESK REPORT: WeakReference");
					sw.WriteLine("### TITLE: WeakReference");
					sw.WriteLine("### COUNT: " + Utils.LargeNumberString(infos.Length));
					sw.WriteLine("### COLUMNS: Address|ulong \u271A Object Address|string \u271A Object Type|string");
					sw.WriteLine("### SEPARATOR:  \u271A ");
					sw.WriteLine("#### WeakReference count: " + Utils.LargeNumberString(infos.Length));

					sw.WriteLine("#### Total WeakReference instance count: " + infos.Length);
					sw.WriteLine("#### Clumns: address of WeakReference, address of type pointed to, type name");
					sw.WriteLine("####");
					for (int i = 0, icnt = infos.Length; i < icnt; ++i)
					{
						sw.Write(Utils.AddressStringHeader(infos[i].First) + Constants.HeavyGreekCrossPadded);
						sw.Write(Utils.AddressStringHeader(infos[i].Second) + Constants.HeavyGreekCrossPadded);
						sw.WriteLine(infos[i].Third);
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

			//Assert.IsNull(error, error);
		}



	    [TestMethod]
	    public void TestStringBuilderContent()
	    {
	        string expectedString =
	            @"0aaaaaaaaaa1aaaaaaaaaa2aaaaaaaaaa3aaaaaaaaaa4aaaaaaaaaa5aaaaaaaaaa6aaaaaaaaaa7aaaaaaaaaa8aaaaaaaaaa9aaaaaaaaaa";
	        var sbTypeName = @"System.Text.StringBuilder";
            var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\TestApp\TestApp.exe_160926_081822.map");
	        bool found = false;
 			using (map)
 			{
 			    var typeId = map.GetTypeId(sbTypeName);
                Assert.IsFalse(typeId==Constants.InvalidIndex);
 			    ulong[] typeAddresses = map.GetTypeAddresses(typeId);
                Assert.IsTrue(typeAddresses!=null && typeAddresses.Length>0);
 			    for (int i = 0, icnt = typeAddresses.Length; i < icnt; ++i)
 			    {
                    var str = SpecializedQueries.getStringBuilderString(map.GetFreshHeap(), typeAddresses[i]);
 			        if (string.Compare(expectedString, str, StringComparison.Ordinal) == 0)
 			            found = true;
 			    }
                Assert.IsTrue(found);
 			}
	    }


		[TestMethod]
		public void TestMisc()
		{
			System.Collections.Generic.Dictionary<System.String, System.Collections.Generic.IList<System.String>> dct =
				new Dictionary<string, IList<string>>();
			var tp = dct.GetType();
			var niceName = DmpNdxQueries.Types.NiceTypeName(tp);

			string s = "aaabbb";
			var pos = s.IndexOf('`');
			var s2 = s.Substring(0, pos);

		}

		[TestMethod]
        public void TestConcurrentDictionaryContent()
        {
            var sbTypeName = @"System.Collections.Concurrent.ConcurrentDictionary<System.String,System.Collections.Generic.KeyValuePair<System.String,System.String>>";
            var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\TestApp\TestApp.exe_160928_083345.map");
            bool found = false;
            using (map)
            {
                var typeId = map.GetTypeId(sbTypeName);
                Assert.IsFalse(typeId == Constants.InvalidIndex);
                ulong[] typeAddresses = map.GetTypeAddresses(typeId);
                Assert.IsTrue(typeAddresses != null && typeAddresses.Length > 0);
                for (int i = 0, icnt = typeAddresses.Length; i < icnt; ++i)
                {
                    SpecializedQueries.getConcurrentDictionaryContent(map.GetFreshHeap(), typeAddresses[i]);
                }
            }
        }

        #endregion Specialized Queries

        [TestMethod]
        public void TestTypeNamespaceDisplay()
        {
            using (var map = OpenMap(@"D:\Jerzy\WinDbgStuff\Dumps\DumpTest\DumpTest.exe_160820_073947.map"))
            {
                var dispInfo = map.GetNamespaceDisplay();
            }
        }


		[TestMethod]
		public void TestDumpTypeFields()
		{
			const string typeName = @"ECS.Common.Data.Settings.SettingRec";
			string error;
			var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\DupCacheIssue\A2_noDF.map");
			Assert.IsNotNull(map);
			List<string> errors = new List<string>();
			using (map)
			{
				var typeId = map.GetTypeId(typeName);
				Assert.IsTrue(IndexValue.IsIndex(typeId));
				Assert.IsFalse(IndexValue.IsInvalidIndex(typeId));

				var typeAddresses = map.GetTypeAddresses(typeId);

				var heap = map.GetFreshHeap();
				ClrInstanceField[] fields=null;
				string[] values = null;
				StreamWriter sw = null;
				try
				{
					sw = new StreamWriter(map.ReportPath + Path.DirectorySeparatorChar + typeName + ".txt");
					for (int i = 0, icnt = typeAddresses.Length; i < icnt; ++i)
					{
						ulong addr = typeAddresses[i];
						ClrType clrType = heap.GetObjectType(addr);

						if (i == 0)
						{
							fields = new ClrInstanceField[clrType.Fields.Count];
							values = new string[clrType.Fields.Count];
							sw.WriteLine("#### " + typeName);
							fields[0] = clrType.Fields[0];
							sw.Write("#### address" + Constants.HeavyGreekCrossPadded + "offset");
							for (int j = 0, jcnt = clrType.Fields.Count; j < jcnt; ++j)
							{
								fields[j] = clrType.Fields[j];
								sw.Write(Constants.HeavyGreekCrossPadded + fields[j].Name + " " + Utils.SmallNumberHeader(fields[j].Offset));
							}
							sw.WriteLine();
						}

						for (int j = 0, jcnt = fields.Length; j < jcnt; ++j)
						{
							ClrInstanceField fld = fields[j];
							if (fld.IsObjectReference)
								values[j] = (string) fld.GetValue(addr, false, true);
							else
								values[j] = ValueExtractor.GetDateTimeValue(addr, fld, false);
						}
						sw.Write(Utils.AddressStringHeader(addr));
						for (int j = 0, jcnt = fields.Length; j < jcnt; ++j)
						{
							sw.Write(Constants.HeavyGreekCrossPadded + values[j]);
						}
						sw.WriteLine();
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

			//Assert.IsNull(error, error);
		}

		//[TestMethod]
		////public void TestParentGraph()
		////{
		////	string error;
		//	var map = OpenMap0();

		//	const ulong addr = 0x000000a1d518e230;

		//	var result = map.GetParentReferences(addr,out error);
		//	Assert.IsNull(error,error);

		//	string name1 = map.GetTypeNameAtAddr(addr);
		//	int typeId = map.GetInstanceIdAtAddr(addr);
		//	string name2 = map.GetTypeName(typeId);
		//	Assert.IsTrue(name1 == name2);
		//}

		//[TestMethod]
		//public void TestRoots()
		//{
		//    ulong addr = 0x000000800f5a10;
		//    string error;
		//    var map = OpenMap(@"C:\WinDbgStuff\Dumps\Analytics\AnalyticsMemory\Eze.Analytics.O971_160722_152115.FC.4300MB.map");
		//    var heap = map.GetFreshHeap();
		//    var clrType = heap.GetObjectType(addr);
		//}

		//      [TestMethod]
		//public void TestMultipleParentGraphs()
		//{
		//	string error;
		//	//const string typeName = "System.Threading.ReaderWriterLock";
		//	const string typeName = "System.Threading.CancellationTokenRegistration[]";

		//	var map = OpenMap0();
		//	StreamWriter wr = null;
		//	//BinaryWriter bw = null;
		//	try
		//	{
		//		var id = map.GetTypeId(typeName);
		//		Assert.IsFalse(IndexValue.IsInvalidIndex(id));
		//		Assert.IsTrue(IndexValue.IsIndex(id));

		//		var addresses = map.GetTypeAddresses(id);
		//		var icnt = addresses.Length; //Math.Min(addresses.Length, 10);
		//		var resultLst = new List<KeyValuePair<ulong, string>[]>(addresses.Length);
		//		var orphans = new List<ulong>(256);
		//		var parented = new List<ulong>(256);
		//		var strCache = new StringCache(5000);
		//		var lst = new List<InstanceTypeNode>(64);
		//		var que = new Queue<InstanceTypeNode>();

		//		for (int i = 0; i < icnt; ++i)
		//		{
		//			var result = map.GetParentReferences(addresses[i], out error, 1);
		//			que.Clear();
		//			lst.Clear();
		//			result.GetNodeList(que,lst,false);
		//			if (lst.Count < 1)
		//			{
		//				orphans.Add(addresses[i]);
		//				continue;
		//			}
		//			parented.Add(addresses[i]);
		//			var ary = new KeyValuePair<ulong, string>[lst.Count];
		//			for (int j = 0, jcnt = lst.Count; j < jcnt; ++j)
		//			{
		//				ary[j] = new KeyValuePair<ulong, string>(lst[j].Address,lst[j].TypeName);
		//			}
		//			Array.Sort(ary, (a, b) =>
		//			{
		//				var cmp = string.Compare(a.Value, b.Value,StringComparison.Ordinal);
		//				if (cmp == 0) return a.Key < b.Key ? -1 : (a.Key > b.Key ? 1 : 0);
		//				return cmp;
		//			});
		//			resultLst.Add(ary);
		//		}

		//		List<ClrtRoot> rootLst = new List<ClrtRoot>();
		//		for (int i = 0; i < icnt; ++i)
		//		{
		//			ClrtRoot info;
		//			bool inFinilizerQueue;
		//			if (map.GetRootInfo(addresses[i], out info, out inFinilizerQueue))
		//			{
		//				rootLst.Add(info);
		//			}
		//		}



		//		var results = resultLst.ToArray();
		//		Array.Sort(results, (a, b) => string.Compare(a[0].Value,b[0].Value,StringComparison.Ordinal) );
		//		icnt = results.Length;
		//		orphans.Sort();
		//		wr = new StreamWriter(TestConfiguration.OutPath0 + typeName + ".PARENTS.txt");

		//		wr.WriteLine("###");
		//		wr.WriteLine("### " + typeName + " ... total count: " + (results.Length + orphans.Count));
		//		wr.WriteLine("### with parents ... count: " + results.Length + ", 1st level parents only");
		//		wr.WriteLine("### [instance address] -> ([parent address] parent name)+");

		//		for (int i = 0; i < icnt; ++i)
		//		{
		//			wr.Write("[" + Utils.AddressString(addresses[i]) + "] -> ");
		//			if (results[i].Length < 0)
		//			{
		//				wr.WriteLine();
		//				continue;
		//			}
		//			for (int j = 0, jcnt = results[i].Length; j < jcnt; ++j)
		//			{
		//				var res = results[i];
		//				wr.Write("[" + Utils.AddressString(res[j].Key) + "] " + res[j].Value + " ");
		//			}
		//			wr.WriteLine();
		//		}

		//		wr.WriteLine("###");
		//		wr.WriteLine("### ORPHANS ... count: " + orphans.Count);
		//		wr.WriteLine("### instance addresses");

		//		icnt = orphans.Count;
		//		for (int i = 0; i < icnt; ++i)
		//		{
		//			if (i > 0 && (i%6)==0)
		//				wr.WriteLine();
		//			wr.Write(Utils.AddressString(orphans[i]) + ", ");
		//		}
		//		wr.WriteLine();
		//		wr.Close();
		//		wr = null;

		//		var writeResult = Utils.WriteUlongArray(TestConfiguration.OutPath0 + typeName + ".ORPHANS.map", orphans, out error);
		//		Assert.IsTrue(writeResult,error);
		//		writeResult = Utils.WriteUlongArray(TestConfiguration.OutPath0 + typeName + ".PARENTED.map", parented, out error);
		//		Assert.IsTrue(writeResult, error);

		//		//bw = new BinaryWriter(File.Open(TestConfiguration.OutPath0 + typeName + ".ORPHANS.map", FileMode.Create));
		//		//bw.Write(orphans.Count);
		//		//for (int i = 0; i < icnt; ++i)
		//		//{
		//		//	bw.Write(orphans[i]);
		//		//}
		//		//bw.Close();
		//		//bw = null;
		//	}
		//	catch (Exception ex)
		//	{
		//		Assert.Fail(Utils.GetExceptionErrorString(ex));
		//	}
		//	finally
		//	{
		//		wr?.Close();
		//	}

		//}


		#region Types

		[TestMethod]
		public void TestTypeElemnts()
		{
			string error;
			var map = OpenMap(@"");

			var ets0 = map.GetElementTypes(ClrElementType.Struct);

			var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
			List<string> lst = new List<string>(ets0.Length);
			for (int i = 0, icnt = ets0.Length; i < icnt; ++i)
			{
				var tp = ets0[i];
				var fldInfo = map.GetFieldNamesAndTypes(tp.Key);
				sb.Clear();
				sb.Append(tp.Value).Append("  ").AppendLine(Utils.SmallIdHeader(tp.Key));

				for (int j = 0, jcnt = fldInfo.Length; j < jcnt; ++j)
				{
					var fld = fldInfo[j];
					sb.Append("   ").Append(Utils.SmallIdHeader(fld.First)).Append(fld.Second).Append("  ").AppendLine(fld.Third);
				}
				lst.Add(sb.ToString());
			}
			StringBuilderCache.Release(sb);

			lst.Sort(StringComparer.Ordinal);
			var path = TestConfiguration.OutPath0 + map.DumpBaseName + ".STRUCTURES.txt";
			Utils.WriteStringList(path, lst, out error);
			Assert.IsNull(error,error);

			var ets1 = map.GetElementTypes(ClrElementType.Unknown);
			Assert.IsTrue(ets1.Length==0);
			var nonEmptys = map.GetNonemptyElementTypes();
			var ets2 = map.GetElementTypes(ClrElementType.Array);
		}

		[TestMethod]
		public void TestTypeAccess()
		{
			const string typeName =
				"System.Collections.Concurrent.ConcurrentDictionary+Tables<System.Int32,System.Collections.Concurrent.ConcurrentDictionary<System.Int32,ECS.Common.Transport.HierarchyCache.IReadOnlyRealtimePrice>>";
			string error;
			var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\ConvergEx\Analytics.map");
			using (map)
			{
				int totalCount;
				var result = map.GetTypeWithPrefixAddresses(typeName, true, out totalCount);

				var id = map.GetTypeId(typeName);
				var tname = map.GetTypeName(id);
				Debug.Assert(tname == typeName);
				var addresses = map.GetTypeAddresses(id);
			}

		}

		[TestMethod]
		public void TestTypeInstances()
		{
			const string typeName = "System.Threading.ReaderWriterLock";
			var map = OpenMap(@"");

			var did = map.GetTypeId("Eze.Server.Common.Pulse.Common.Types.PositionCalcGroupToRowDeltaApplicationInfo");
			Assert.IsTrue(IndexValue.IsIndex(did));
			Assert.IsFalse(IndexValue.IsInvalidIndex(did));
			var daddresses = map.GetTypeAddresses(did);

			var rid = map.GetTypeId("Eze.Server.Common.Pulse.CalculationCache.RowCache");
			Assert.IsTrue(IndexValue.IsIndex(rid));
			Assert.IsFalse(IndexValue.IsInvalidIndex(rid));
			var raddresses = map.GetTypeAddresses(rid);



			var heap = map.GetFreshHeap();
			List<ulong> adrLst = new List<ulong>(daddresses.Length);
			HashSet<ulong> adrSet = new HashSet<ulong>();
			var bitVecDct = new SortedDictionary<ulong, List<ulong[]>>();
			for (int i = 0, icnt = daddresses.Length; i < icnt; ++i)
			{
				var addr = daddresses[i];
				var clrType = heap.GetObjectType(addr);
				Assert.IsNotNull(clrType);
				var faddr = (ulong)clrType.Fields[0].GetValue(addr);
				adrLst.Add(faddr);
				adrSet.Add(faddr);

				var bvAddr = (ulong) clrType.Fields[1].GetValue(addr);
				var bvType = heap.GetObjectType(bvAddr);
				if (bvType == null)
				{
					continue;
				}
				var bitsFldAddr = (ulong)bvType.Fields[0].GetValue(bvAddr);
				var bitsType = heap.GetObjectType(bitsFldAddr);
				if (bitsType == null)
				{
					continue;
				}
				var bitsCnt = bitsType.GetArrayLength(bitsFldAddr);
				var bitsAry = new ulong[bitsCnt];
				for (int j = 0; j < bitsCnt; ++j)
				{
					bitsAry[j] = (ulong)bitsType.GetArrayElementValue(bitsFldAddr, j);
				}
				Array.Sort(bitsAry);
				List<ulong[]> lst;
				if (bitVecDct.TryGetValue(faddr, out lst))
				{
					lst.Add(bitsAry);
				}
				else
				{
					bitVecDct.Add(faddr,new List<ulong[]>() {bitsAry});
				}
			}

			var daryAll = adrLst.ToArray();
			Array.Sort(daryAll);
			var darySet = adrSet.ToArray();
			Array.Sort(darySet);
			Array.Sort(raddresses);

			var rdDif = raddresses.Except(darySet).ToArray();
			var drDif = darySet.Except(raddresses).ToArray();

			int diffCnt = 0;

			foreach (var kv in bitVecDct)
			{
				if (!AreArraysTheSame(kv.Value))
				{
					++diffCnt;
				}
			}

			Assert.IsNotNull(daddresses, "Map.GetTypeAddresses returned null.");
		}

		bool AreArraysTheSame(IList<ulong[]> lst)
		{
			if (lst.Count < 2) return true;
			var prevAry = lst[0];
			for (int i = 1, icnt = lst.Count; i < icnt; ++i)
			{
				if (prevAry.Length != lst[i].Length) return false;
				var ary = lst[i];
				for (int j = 0, jcnt = ary.Length; j < jcnt; ++j)
				{
					if (prevAry[j] != ary[j]) return false;
				}
				prevAry = ary;
			}

			return true;
		}

		[TestMethod]
		public void TestDumpReversedTypeName()
		{
			var map = OpenMap(@"");
			Assert.IsNotNull(map);
			var outFile = DumpFileMoniker.GetOuputFolder(map.DumpPath) + Path.DirectorySeparatorChar + "REVERSEDNAMES.txt";
			string error;
			var ok = map.DumpReversedNames(outFile, out error);
			Assert.IsTrue(ok,error);
		}

		#endregion Types


		#region Instance References

		//[TestMethod]
		//public void TestFindInstanceParents()
		//{
		//	string error = null;

		//	ulong addr = 0x000000027ff968;

		//	using (var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\DumpSearch\DumpSearch.exe_160711_121816.map"))
		//	{
		//		try
		//		{
		//			var result = map.GetFieldReferences(addr, out error);
		//			InstanceTypeNode rootNode = map.GetParentReferences(addr, out error);


		//		}
		//		catch (Exception ex)
		//		{
		//			error = Utils.GetExceptionErrorString(ex);
		//			Assert.IsTrue(false, error);
		//		}
		//	}
		//}

		#endregion Instance References


		//[TestMethod]
		//public void TestFindInstanceParent()
		//{
		//	const string typeName = "System.EventHandler";
		//	string[] typePrefixes = new string[] { "ECS.", "Eze." };
		//	string error = null;
		//	Queue<InstanceTypeNode> que = new Queue<InstanceTypeNode>();
		//	List<InstanceTypeNode> lst = new List<InstanceTypeNode>(32);
		//	SortedDictionary<string, List<ulong>> dct = new SortedDictionary<string, List<ulong>>();

		//	using (var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\OMS\AustralianSuper\EzeOMSFrozen07272016.map"))
		//	{
		//		try
		//		{

		//			var id = map.GetTypeId(typeName);
		//			Assert.IsTrue(IndexValue.IsIndex(id));
		//			Assert.IsFalse(IndexValue.IsInvalidIndex(id));
		//			var addresses = map.GetTypeAddresses(id);
		//			for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
		//			{
		//				lst.Clear();
		//				que.Clear();
		//				var addr = addresses[i];
		//				var node = map.GetParentReferences(addr, out error, 3);
		//				node.GetNodeList(que, lst, false);
		//				for (int j = 0, jcnt = lst.Count; j < jcnt; ++j)
		//				{
		//					if (Utils.StartsWithPrefix(lst[j].TypeName, typePrefixes))
		//					{
		//						List<ulong> alst;
		//						if (dct.TryGetValue(lst[j].TypeName, out alst))
		//						{
		//							alst.Add(lst[j].Address);
		//						}
		//						else
		//						{
		//							dct.Add(lst[j].TypeName,new List<ulong>() { lst[j].Address });
		//						}
		//					}
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
		public void TestTypeParents()
		{
			const string typeName = "System.Threading.CancellationTokenRegistration[]";
			var map = OpenMap(@"");

			var id = map.GetTypeId(typeName);
			Assert.IsTrue(IndexValue.IsIndex(id));
			Assert.IsFalse(IndexValue.IsInvalidIndex(id));

			var addresses = map.GetTypeAddresses(id);
			Assert.IsNotNull(addresses, "Map.GetTypeAddresses returned null.");
		}

		[TestMethod]
		public void TestTypeNames()
		{
			//const string typeName = "System.Threading.CancellationTokenRegistration[]";
			string typeName = "System.String";
			var map = OpenMap(@"");


			var names = map.TypeNames;
			Tuple<string, string> badCouple;
			var isSorted = Utils.IsSorted(names, out badCouple);
			var cmp1 = string.Compare(badCouple.Item1, badCouple.Item2);
			var cmp2 = string.Compare(badCouple.Item1, badCouple.Item2, StringComparison.Ordinal);

			var nnames = Utils.CloneArray(names);
			Array.Sort(nnames,StringComparer.Ordinal);
			Tuple<string, string> nbadCouple;
			var nisSorted = Utils.IsSorted(nnames, out nbadCouple);


			var id = map.GetTypeId(typeName);
			string name = map.GetTypeName(id);

			Assert.IsTrue(IndexValue.IsIndex(id));
			Assert.IsFalse(IndexValue.IsInvalidIndex(id));

			var addresses = map.GetTypeAddresses(id);
			Assert.IsNotNull(addresses, "Map.GetTypeAddresses returned null.");
		}


		//[TestMethod]
		//public void TestFilteredTypeParents()
		//{
		//	//const string typeName = "System.Threading.CancellationTokenRegistration[]";
		//	string typeName = "System.String";
		//	string content = "Caption";
		//	var map = OpenMap(@"C:\WinDbgStuff\Dumps\DumpSearch\DumpSearch.exe_160813_062931.map");

		//	var id = map.GetTypeId(typeName);
		//	string name = map.GetTypeName(id);

		//	Assert.IsTrue(IndexValue.IsIndex(id));
		//	Assert.IsFalse(IndexValue.IsInvalidIndex(id));

		//	var addresses = map.GetTypeAddresses(id);
		//	Assert.IsNotNull(addresses, "Map.GetTypeAddresses returned null.");
		//	string error;
		//	List<ulong> addrLst = new List<ulong>(1024);
		//	List<InstanceTypeNode> nodeLst = new List<InstanceTypeNode>(1024);
		//	var heap = map.Dump.Runtime.GetHeap();

		//	for (int i = 0, icnt = addresses.Length; i < icnt;  ++i)
		//	{
		//		var addr = addresses[i];
		//		ClrType clrType = heap.GetObjectType(addresses[i]);
		//		if (clrType == null) continue;
		//		var str = ValueExtractor.GetStringAtAddress(addr, heap);
		//		if (str == content)
		//		{
		//			addrLst.Add(addr);
		//			var node = map.GetParentReferences(addr, out error, 3);
		//			nodeLst.Add(node);
		//		}
		//	}
		//}


		private ulong[] addressesToCheck = new ulong[]
		{
			 0x000000307e3948
,0x000000349b2c48
,0x00000048164730
,0x000000482419a8
,0x0000004824bce0
,0x00000048250030
,0x00000048254378
,0x000000482586c0
,0x0000004825ca08
,0x00000048260d30
,0x00000048265050
,0x00000048269380
,0x0000004826d688
,0x00000048271988
,0x00000048277cd0
,0x0000004827c018
,0x00000048280360
,0x000000482846b0
,0x0000004828c938
,0x00000048290e98
,0x000000482951e0
,0x00000048299508
,0x0000004829f870
,0x000000482a3bc0
,0x000000482a7ef0
,0x000000482ac238
,0x000000482b0580
,0x000000482b48a0
,0x000000482b8be8
,0x000000482bcf18
,0x000000482c1268
,0x000000482c55b0
,0x000000482c98f8
,0x000000482cdc40
,0x000000482d1f88
,0x000000482d62d8
,0x00000048411a50
,0x000000484178f0
,0x0000004841e970
,0x00000048422ac0
,0x00000048426c20
,0x0000004842add8
,0x000000484353f0
,0x00000048439540
,0x0000004843d6a8
,0x000000484418a0
,0x00000048445a08
,0x00000048449b70
,0x0000004844e6b0
,0x000000484527d0
,0x00000048463c88
,0x00000048467db0
,0x0000004846c058
,0x000000484704c8
,0x000000484747d0
,0x00000048478ad8
,0x0000004847ce28
,0x00000048481130
,0x00000048485460
,0x000000484897b0
,0x0000004848daf0
,0x00000048491df0
,0x00000048496150
,0x0000004849a498
,0x0000004849e7c8
,0x000000484a2ac8
,0x000000484a6e18
,0x000000484ab148
,0x000000484b33b0
,0x000000484b78f0
,0x000000484bbc20
,0x000000484bff20
,0x000000484c4228
,0x000000484c8550
,0x000000484cc8a0
,0x000000484d0bf0
,0x000000484d4f20
,0x000000484d9238
,0x000000484dd580
,0x000000484e18c8
,0x000000484e5c10
,0x000000484e9f58
,0x000000484ee2a0
,0x000000484f25c8
,0x000000484f6910
,0x000000484fac38
		};

		[TestMethod]
		public void TestAddressesParents()
		{
			string typeName =
				"System.Collections.Concurrent.ConcurrentDictionary+Node<System.String,ECS.Common.Collections.Common.triple<System.String,System.Int32,System.Int32>>";
			string error = null;
			int parentsPresentCnt = 0;
			KeyValuePair<ulong, int>[][] parents = new KeyValuePair<ulong, int>[addressesToCheck.Length][];
			//using (var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\afterJerzy.map"))
			//using (var map = OpenMap(@"D:\Jerzy\ezeprojects\OPAMS\1008 - Remove Duplicate Strings\CacheTest.vshost.exe_160823_110843.map"))
			//using (var map = OpenMap(@"D:\Jerzy\ezeprojects\OPAMS\1008 - Remove Duplicate Strings\CacheTest.vshost.exe_160823_113610.map"))
			//using (var map = OpenMap(@"D:\Jerzy\ezeprojects\OPAMS\1008 - Remove Duplicate Strings\CacheTest.vshost.exe_160823_161542.map"))
			//using (var map = OpenMap(@"D:\Jerzy\ezeprojects\OPAMS\1008 - Remove Duplicate Strings\CacheTest.vshost.exe_160823_165954.map"))
			using (var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\Eze.Analytics.O971_160824_095212.FC.dupcache8a.map"))
			{
				var fldDnpd = map.FieldDependencies;
				//for (int i = 0, icnt = addressesToCheck.Length; i < icnt; ++i)
				//{
				//	parents[i] = fldDnpd.GetFieldParents(addressesToCheck[i], out error);
				//	if (error != null && error[0] == Constants.InformationSymbol) error = null;
				//	if (parents[i].Length > 0)
				//	{
				//		++parentsPresentCnt;
				//	}

				//}
				var dct = new SortedDictionary<string, KeyValuePair<List<ulong>,int>>(StringComparer.Ordinal);
				int typeId = map.GetTypeId(typeName);
				ulong[] taddresses = map.GetTypeAddresses(typeId);
				var heap = map.Dump.GetFreshHeap();
				for (int i = 0, icnt = taddresses.Length; i < icnt; ++i)
				{
					var paddrs = fldDnpd.GetFieldParents(taddresses[i], out error);
					if (error != null && error[0] == Constants.InformationSymbol) error = null;
					var clrType = heap.GetObjectType(taddresses[i]);
					Assert.IsNotNull(clrType);
					ClrInstanceField fld1 = clrType.GetFieldByName("m_key");
					var keyVal = (ulong)fld1.GetValue(taddresses[i], clrType.IsValueClass);
					var keyStr = ValueExtractor.GetStringAtAddress(keyVal, heap);
					KeyValuePair<List<ulong>, int> kv;
					if (dct.TryGetValue(keyStr, out kv))
					{
						var lst = kv.Key;
						for (int j = 0, jcnt = paddrs.Length; j < jcnt; ++j)
							lst.Add(paddrs[j].Key);
						var cnt = kv.Value + 1;
						dct[keyStr] = new KeyValuePair<List<ulong>, int>(lst,cnt);
					}
					else
					{
						var lst = new List<ulong>(paddrs.Length);
						for (int j = 0, jcnt = paddrs.Length; j < jcnt; ++j)
							lst.Add(paddrs[j].Key);
						dct.Add(keyStr,new KeyValuePair<List<ulong>, int>(lst,1));
					}
				}

				List<KeyValuePair<string, int>> freeList = new List<KeyValuePair<string, int>>(128);
				List<KeyValuePair<string, int>> orphanList = new List<KeyValuePair<string, int>>(128);
				List<KeyValuePair<string, int>> onelingList = new List<KeyValuePair<string, int>>(128);
				int totalFreeCount = 0;
				foreach (var ent in dct)
				{
					var kvVal = ent.Value;
					if (kvVal.Key.Count == 0)
					{
						orphanList.Add(new KeyValuePair<string, int>(ent.Key, kvVal.Value));
					}
					else if (kvVal.Key.Count < kvVal.Value)
					{
						int cnt = kvVal.Value - kvVal.Key.Count;
						totalFreeCount += cnt;
						freeList.Add(new KeyValuePair<string, int>(ent.Key,cnt));
					}
					else if (kvVal.Key.Count == 1 && kvVal.Value == 1)
					{
						onelingList.Add(new KeyValuePair<string, int>(ent.Key, 1));
					}
				}



				Assert.IsNull(error);
			}
		}

		[TestMethod]
	    public void TestGetTypeWithStringField2()
	    {
            var fldRefList = new List<KeyValuePair<ulong, int>>(64);
			const string str = "SomeClassName_SomeClassA_0";
			string error = null;
			using (var map = OpenMap(@"C:\WinDbgStuff\Dumps\DumpSearch\DumpSearch.exe_160813_062931.map"))
			{
				//var dct = map.GetTypesWithSpecificStringField(str, out error);
                //Assert.IsNotNull(dct);
                var strStats =map.GetCurrentStringStats(out error);
                var addresses = strStats.GetStringAddresses(str, out error);
			    var fldDpnds = map.FieldDependencies;
			    List<string> lst=new List<string>();
			    var parents = fldDpnds.GetMultiFieldParents(addresses, lst);

			}
        }


		[TestMethod]
		public void TestGetTypeGroupedContent()
		{
			const string str = "Eze.Server.Common.Pulse.Common.Types.CachedValue+DefaultOnly<System.Decimal>";
			string error = null;
			SortedDictionary<string,int> dct = new SortedDictionary<string, int>(StringComparer.Ordinal);
			using (var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\RealPositionCmp\Eze.Analytics.Svc.RPSMALL0_160913_153220.map"))
			{
				try
				{
					ClrType clrType = null;
					ClrInstanceField bitwiseCurrentPositionToggleState = null;
					ClrInstanceField indexIntoPositionIndexToFieldNumberConversionSet = null;
					int typeId = map.GetTypeId(str);
					ulong[] addresses = map.GetTypeAddresses(typeId);
					var heap = map.Dump.GetFreshHeap();
					for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
					{
						var addr = addresses[i];
						if (clrType == null)
						{
							clrType = heap.GetObjectType(addr);
							bitwiseCurrentPositionToggleState = clrType.GetFieldByName("bitwiseCurrentPositionToggleState");
							indexIntoPositionIndexToFieldNumberConversionSet = clrType.GetFieldByName("indexIntoPositionIndexToFieldNumberConversionSet");
							Assert.IsNotNull(bitwiseCurrentPositionToggleState.Type);
							Assert.IsNotNull(indexIntoPositionIndexToFieldNumberConversionSet.Type);
						}

						object fld1ValObj = bitwiseCurrentPositionToggleState.GetValue(addr);
						string fld1Val = ValueExtractor.GetPrimitiveValue(fld1ValObj, bitwiseCurrentPositionToggleState.Type);
						object fld2ValObj = indexIntoPositionIndexToFieldNumberConversionSet.GetValue(addr);
						string fld2Val = ValueExtractor.GetPrimitiveValue(fld2ValObj, indexIntoPositionIndexToFieldNumberConversionSet.Type);
						var key = fld1Val + "_" + fld2Val;
						int count;
						if (dct.TryGetValue(key, out count))
						{
							dct[key] = count + 1;
						}
						else
						{
							dct.Add(key,1);
						}
					}

				}
				catch (Exception ex)
				{
					Assert.IsTrue(false,ex.ToString());
				}
			}
		}


		[TestMethod]
		public void TestGetTypeGroupedContent2()
		{
			const string str = "ECS.Common.Collections.Common.EzeBitVector";
			string error = null;
			SortedDictionary<string, int> dct = new SortedDictionary<string, int>(StringComparer.Ordinal);
			int nullFieldCount = 0;
			StringBuilder sb = new StringBuilder(256);
			//using (var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\RealPositionCmp\Eze.Analytics.Svc.RPBIG_160913_151123.map"))
			//using (var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Baly\analytics9_1512161604.good.map"))
			//using (var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\Eze.Analytics.Svc.exe.EzeBitVector.map"))
			using (var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\Eze.Analytics.Svc.exe.EzeBitVector.PadOff.map"))
			{
				try
				{
					ClrType clrType = null;
					ClrType aryType = null;
					ClrInstanceField bits = null;
					ulong aryAddr = 0;
					int typeId = map.GetTypeId(str);
					ulong[] addresses = map.GetTypeAddresses(typeId);
					var heap = map.Dump.GetFreshHeap();
					for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
					{
						var addr = addresses[i];
						if (clrType == null)
						{
							clrType = heap.GetObjectType(addr);
							bits = clrType.GetFieldByName("bits");
							Assert.IsNotNull(bits.Type);
							Assert.IsNotNull(bits.Type);
							aryType = bits.Type;
						}
						var aryAddrObj = bits.GetValue(addr);
						if (aryAddrObj == null)
						{
							++nullFieldCount;
							continue;
						}
						aryAddr = (ulong) aryAddrObj;
						int aryCount = aryType.GetArrayLength(aryAddr);
						sb.Clear();
						sb.Append(aryCount.ToString()).Append("_");
						for (int j = 0; j < aryCount; ++j)
						{
							var elemVal = aryType.GetArrayElementValue(aryAddr, j);
							if (elemVal == null)
								continue;
							var valStr = elemVal.ToString();
							sb.Append(valStr).Append("_");
						}
						if (sb.Length > 0) sb.Remove(sb.Length - 1, 1);
						var key = sb.ToString();
						int dctCnt;
						if (dct.TryGetValue(key, out dctCnt))
						{
							dct[key] = dctCnt + 1;
						}
						else
						{
							dct.Add(key, 1);
						}
					}

					StreamWriter sw = null;
					try
					{

						sw = new StreamWriter(map.ReportPath + Path.DirectorySeparatorChar + "EzeBitVector.txt");
						sw.WriteLine("#### Total EzeBitVector Instance Count: " + Utils.SizeString(addresses.Length));
						sw.WriteLine("#### Total EzeBitVector Unique Count: " + Utils.SizeString(dct.Count));
						sw.WriteLine("#### Columns: duplicate count, ulong[] size, vector content");
						foreach (var kv in dct)
						{
							var pos = kv.Key.IndexOf('_');
							int aryCount = Int32.Parse(kv.Key.Substring(0, pos));
							sw.WriteLine(Utils.CountStringHeader(kv.Value) + Utils.CountStringHeader(aryCount) + kv.Key.Substring(pos+1));
						}

					}
					catch (Exception ex)
					{
						Assert.IsTrue(false, ex.ToString());
					}
					finally { sw?.Close();}

				}
				catch (Exception ex)
				{
					Assert.IsTrue(false, ex.ToString());
				}
			}
		}
		[TestMethod]
		public void TestGetTypeSizeDetails()
		{
			string reportPath = null;
			StreamWriter sw = null;
			const string typeName = "ECS.Common.HierarchyCache.Structure.RealPosition";
			string error = null;
			Tuple<ulong, ulong[], SortedDictionary<string, KeyValuePair<int, ulong>>, SortedDictionary<string, List<int>>, triple<int, ulong,string>[]> result = null;

			try
			{
				using (
					var map =
						OpenMap(
							@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\RealPositionCmp\Eze.Analytics.Svc.RPSMALL0_160913_153220.map")
					)
				{
					int typeId = map.GetTypeId(typeName);
					result = map.GetTypeSizeDetails(typeId, out error);
					reportPath = map.ReportPath + Path.DirectorySeparatorChar + Utils.BaseTypeName(typeName) + ".SIZE.DETAILS.txt";
				}

				Assert.IsNotNull(result,error);

				sw = new StreamWriter(reportPath);
				sw.WriteLine("### TOTAL SIZE: " + Utils.LargeNumberString(result.Item1));
				sw.WriteLine("### INVALID ADDRESSES COUNT: " + Utils.LargeNumberString(result.Item2.Length));
				var typeDct = result.Item3;
				sw.WriteLine("### TYPE COUNT: " + Utils.LargeNumberString(typeDct.Count));
				foreach (var kv in typeDct)
				{
					var name = kv.Key;
					var cnt = kv.Value.Key;
					var sz = kv.Value.Value;
					sw.WriteLine(Utils.SortableSizeStringHeader(cnt) + Utils.SortableLengthStringHeader(sz) + name);
				}
				var aryDct = result.Item4;
				sw.WriteLine("### ARRAYS AND THEIR COUNTS");
				sw.WriteLine("### ARRAY COUNT: " + Utils.LargeNumberString(aryDct.Count));
				sw.WriteLine("### Columns: array count, min elem count, max elem count, avg elem count, total elem count, type name");
				foreach (var kv in aryDct)
				{
					var name = kv.Key;
					var lst = kv.Value;
					var acnt = lst.Count;
					var totalElemCount = 0;
					var minElemCount = Int32.MaxValue;
					var maxElemCount = 0;
					var avgElemCount = 0;
					for (int i = 0, icnt = lst.Count; i < icnt; ++i)
					{
						var val = lst[i];
						totalElemCount += val;
						if (val < minElemCount) minElemCount = val;
						if (val > maxElemCount) maxElemCount = val;
					}
					avgElemCount = (int) Math.Round((double) totalElemCount/(double) acnt);
					sw.Write(Utils.CountStringHeader(acnt));
					sw.Write(Utils.CountStringHeader(minElemCount));
					sw.Write(Utils.CountStringHeader(maxElemCount));
					sw.Write(Utils.CountStringHeader(avgElemCount));
					sw.Write(Utils.SortableSizeStringHeader(totalElemCount));
					sw.WriteLine(name);

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
		public void TestGetProportionsOfFromThreeFiles()
		{
			StreamWriter sw = null;
			string path1 = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\RealPositionCmp\Eze.Analytics.Svc.RPBIG_160913_151123.map\ad-hoc.queries\RealPosition.SIZE.DETAILS.TYPES.txt";
			string path2 = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\RealPositionCmp\Eze.Analytics.Svc.RPSMALL0_160913_153220.map\ad-hoc.queries\RealPosition.SIZE.DETAILS.TYPES.txt";
			string path3 = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\RealPositionCmp\Eze.Analytics.Svc.RPSMALL1_160913_153641.map\ad-hoc.queries\RealPosition.SIZE.DETAILS.TYPES.txt";

			SortedDictionary<string,long[]> resDct = new SortedDictionary<string, long[]>(StringComparer.Ordinal);
			string[] seps = new[] {Constants.HeavyGreekCrossPadded};

			try
			{
				ReadProportionsFromFile(path1, 0, 6, resDct, seps);
				ReadProportionsFromFile(path2, 2, 6, resDct, seps);
				ReadProportionsFromFile(path3, 4, 6, resDct, seps);
				var path = path1 + ".PROPORTIONS.txt";
				sw = new StreamWriter(path);
				sw.WriteLine("### MDRDESK REPORT: SIZE DETAILS TYPES PROPORTIONS");
				sw.WriteLine("### TITLE: PROPORTIONS");
				sw.WriteLine("### COUNT: " + Utils.LargeNumberString(resDct.Count));
				sw.WriteLine("### SEPARATOR: " + Constants.HeavyGreekCrossPadded);
				sw.WriteLine("### COLUMNS: Count1 Prop|int|150 "
					+ Constants.HeavyGreekCrossPadded + "Count2 Prop|int|150"
					+ Constants.HeavyGreekCrossPadded + "Size1 Prop|int|150"
					+ Constants.HeavyGreekCrossPadded + "Size2 Prop|int|150"
					+ Constants.HeavyGreekCrossPadded + "Type|string|500");

				sw.WriteLine(ReportFile.DescrPrefix + path1);
				sw.WriteLine(ReportFile.DescrPrefix + path2);
				sw.WriteLine(ReportFile.DescrPrefix + path3);

				foreach (var kv in resDct)
				{
					long[] vals = kv.Value;
					int c1Prop = (int)Math.Round((double)vals[0] / (double)(vals[2] == 0 ? 1 : vals[2]));
					int s1Prop = (int)Math.Round((double)vals[1] / (double)(vals[3] == 0 ? 1 : vals[3]));
					int c2Prop = (int)Math.Round((double)vals[0] / (double)(vals[4] == 0 ? 1 : vals[4]));
					int s2Prop = (int)Math.Round((double)vals[1] / (double)(vals[5] == 0 ? 1 : vals[5]));

					sw.Write(Utils.LargeNumberString(c1Prop) + Constants.HeavyGreekCrossPadded);
					sw.Write(Utils.LargeNumberString(s1Prop) + Constants.HeavyGreekCrossPadded);
					sw.Write(Utils.LargeNumberString(c2Prop) + Constants.HeavyGreekCrossPadded);
					sw.Write(Utils.LargeNumberString(s1Prop) + Constants.HeavyGreekCrossPadded);
					sw.WriteLine(kv.Key);
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
		public void TestGetProportionsOfFromTwoFiles()
		{
			StreamWriter sw = null;
			string path1 = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\RealPositionCmp\Eze.Analytics.Svc.BIG.map\ad-hoc.queries\RealPosition.SIZE.DETAILS.TYPES.txt";
			string path2 = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\RealPositionCmp\Eze.Analytics.Svc.SMALL.map\ad-hoc.queries\RealPosition.SIZE.DETAILS.TYPES.txt";

			SortedDictionary<string, long[]> resDct = new SortedDictionary<string, long[]>(StringComparer.Ordinal);
			string[] seps = new[] { Constants.HeavyGreekCrossPadded };

			try
			{
				ReadProportionsFromFile(path1, 0, 4, resDct, seps);
				ReadProportionsFromFile(path2, 2, 4, resDct, seps);
				var path = path1 + ".PROPORTIONS.txt";
				sw = new StreamWriter(path);
				sw.WriteLine("### MDRDESK REPORT: SIZE DETAILS TYPES PROPORTIONS");
				sw.WriteLine("### TITLE: PROPORTIONS");
				sw.WriteLine("### COUNT: " + Utils.LargeNumberString(resDct.Count));
				sw.WriteLine("### SEPARATOR: " + Constants.HeavyGreekCrossPadded);
				sw.WriteLine("### COLUMNS: Count1 Prop|int|200 "
					+ Constants.HeavyGreekCrossPadded + "Size1 Prop|int|200"
					+ Constants.HeavyGreekCrossPadded + "Type|string|500");

				sw.WriteLine(ReportFile.DescrPrefix + path1);
				sw.WriteLine(ReportFile.DescrPrefix + path2);

				foreach (var kv in resDct)
				{
					long[] vals = kv.Value;
					int c1Prop = (int)Math.Round((double)vals[0] / (double)(vals[2] == 0 ? 1 : vals[2]));
					int s1Prop = (int)Math.Round((double)vals[1] / (double)(vals[3] == 0 ? 1 : vals[3]));

					sw.Write(Utils.LargeNumberString(c1Prop) + Constants.HeavyGreekCrossPadded);
					sw.Write(Utils.LargeNumberString(s1Prop) + Constants.HeavyGreekCrossPadded);
					sw.WriteLine(kv.Key);
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



		private bool ReadProportionsFromFile(string path, int saveNdx, int aryLen, SortedDictionary<string, long[]> resDct, string[] seps)
		{
			StreamReader sr = null;
			string ln = null;
			try
			{
				sr = new StreamReader(path);

				int nextNdx = saveNdx + 1;
				while ((ln = sr.ReadLine()) != null)
				{
					if (string.IsNullOrWhiteSpace(ln) || !Char.IsDigit(ln[0])) continue;
					var parts = ln.Split(seps, StringSplitOptions.None);
					long val1 = Int64.Parse(parts[0],NumberStyles.AllowThousands);
					long val2 = Int64.Parse(parts[1],NumberStyles.AllowThousands);
					long[] ary;
					if(resDct.TryGetValue(parts[2], out ary))
					{
						ary[saveNdx] = val1;
						ary[nextNdx] = val2;
					}
					else
					{
						ary = new long[aryLen];
						ary[saveNdx] = val1;
						ary[nextNdx] = val2;
						resDct.Add(parts[2],ary);
					}
				}

				return true;
			}
			catch (Exception ex)
			{
				Assert.IsTrue(false, ex.ToString());
				return false;
			}
			finally
			{
				sr?.Close();
			}
		}

		[TestMethod]
        public void TestDumpTypesFields()
        {
            var fldRefList = new List<KeyValuePair<ulong, int>>(64);
            const string str = "SomeClassName_SomeClassA_0";
            string error = null;
//            using (var map = OpenMap(@"C:\WinDbgStuff\Dumps\DumpSearch\DumpSearch.exe_160813_062931.map"))
            {
                BinaryReader offRd = null;
                BinaryReader parRd = null;
                StreamWriter sw = null;

                try
                {
  //                  var fldDpnds = map.FieldDependencies;
                    offRd = new BinaryReader(File.Open(@"C:\WinDbgStuff\Dumps\DumpSearch\DumpSearch.exe_160813_062931.map\DumpSearch.exe_160813_062931.FIELDPARENTOFFSETS[0].map", FileMode.Open,FileAccess.Read));
                    parRd = new BinaryReader(File.Open(@"C:\WinDbgStuff\Dumps\DumpSearch\DumpSearch.exe_160813_062931.map\DumpSearch.exe_160813_062931.FIELDPARENTINSTANCES[0].map", FileMode.Open, FileAccess.Read));
                    sw = new StreamWriter(@"C:\WinDbgStuff\Dumps\DumpSearch\DumpSearch.exe_160813_062931.map\ad-hoc.queries\FieldsAndParents.txt");

                    int offCnt = offRd.ReadInt32();
                    int parCnt = parRd.ReadInt32();

                    var offsets = new KeyValuePair<ulong,long>[offCnt];
                    var parents = new KeyValuePair<long, ulong>[parCnt];
                    for (int i = 0; i < offCnt; ++i)
                    {
                        var fld = offRd.ReadUInt64();
                        var off = offRd.ReadInt64();
                        offsets[i] = new KeyValuePair<ulong, long>(fld,off);
                    }
                    long poff = sizeof(int);
                    for (int i = 0; i < parCnt; ++i)
                    {
                        var par = parRd.ReadUInt64();
                        parents[i] = new KeyValuePair<long, ulong>(poff, par);
                        poff += sizeof(ulong);
                    }

                    var pfld = offsets[0].Key;
                    poff = offsets[0].Value;
                    int pndx = 0;
                    for (int i = 1; i < offCnt; ++i)
                    {
                        var fld = offsets[i].Key;
                        var off = offsets[i].Value;
                        var cnt = (off - poff)/sizeof(long);

                        sw.WriteLine(Utils.AddressStringHeader(pfld) + Utils.AddressString((ulong) poff) + " [" + cnt + "]");
                        //for (int j = 0; j < cnt && pndx < parCnt; ++j)
                        for (int j = 0; j < cnt; ++j)
                        {
                            sw.WriteLine("   " + Utils.AddressStringHeader((ulong)parents[pndx].Key) + Utils.AddressString(parents[pndx].Value));
                            ++pndx;
                        }
                        pfld = fld;
                        poff = off;
                    }



                }
                finally 
                {
                    offRd?.Close();
                    parRd?.Close();
                    sw?.Close();
                }


            }
        }

        [TestMethod]
		public void TestGetTypeWithStringField()
		{
			var fldRefList = new List<KeyValuePair<ulong, int>>(64);
			const string str = "sec_realtime.ASK";

			SortedDictionary<string, KeyValuePair<string, int>> dct = new SortedDictionary<string, KeyValuePair<string, int>>(StringComparer.Ordinal);
			List<KeyValuePair<string, string>> additional = new List<KeyValuePair<string, string>>(512);
			SortedDictionary<string, int> typeDct = new SortedDictionary<string, int>(StringComparer.Ordinal);

			string error = null;
			using (var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\Eze.Analytics.O971_160721_163520.FC.7030MB.map"))
			{
				try
				{
					var heap = map.Dump.Runtime.GetHeap();

					var strTypeId = map.GetTypeId("System.String");
					Assert.IsTrue(IndexValue.IsIndex(strTypeId));
					Assert.IsFalse(IndexValue.IsInvalidIndex(strTypeId));

					ulong[] strAddresses = map.GetTypeAddresses(strTypeId);
					Assert.IsNotNull(strAddresses);
					Assert.IsTrue(Utils.IsSorted(strAddresses));
					ulong[] myStrAddrs = Map.GetStringAddresses(heap, str, strAddresses, out error);
					Assert.IsNull(error);
					Assert.IsNotNull(myStrAddrs);
					Assert.IsTrue(Utils.IsSorted(myStrAddrs));

					var genStrHistogram = map.GetGenerationHistogram(strAddresses);
					var genMyHistogram = map.GetGenerationHistogram(myStrAddrs);

					//HashSet<ulong> strAddrSet = new HashSet<ulong>(myStrAddrs);

					List<ulong> arrays = new List<ulong>(10*1024);
					ulong[] instances = map.Instances;
					for (int i = 0, icnt = instances.Length; i < icnt; ++i)
					{
						ulong addr = instances[i];
						if (Array.BinarySearch(strAddresses,addr) >= 0) continue;

						var clrType = heap.GetObjectType(addr);
						if (clrType == null || clrType.IsString) continue;


						fldRefList.Clear();
						clrType.EnumerateRefsOfObjectCarefully(addr, (address, off) =>
						{
							fldRefList.Add(new KeyValuePair<ulong, int>(address, off));
						});

						bool found = false;
						for (int j = 0, jcnt = fldRefList.Count; j < jcnt; ++j)
						{
							var fldAddr = fldRefList[j].Key;
							if (Array.BinarySearch(myStrAddrs, fldAddr) < 0) continue; // not my string
							found = true;
							int cnt;
							if (typeDct.TryGetValue(clrType.Name, out cnt))
							{
								typeDct[clrType.Name] = cnt + 1;
							}
							else
							{
								typeDct.Add(clrType.Name, 1);
							}
						}
						if (clrType.IsArray)
						{
							arrays.Add(addr);
							if (found)
							{
								int a = 0;
							}
						}
						else if (found)
						{
							int b = 0;
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

		//[TestMethod]
		//public void TestNonrootedFinalizationObjects()
		//{
		//	var map = OpenMap0();
		//	Stopwatch stopWatch = new Stopwatch();
		//	stopWatch.Start();
		//	var finalizerQue = map.FinalizerQueue;
		//	Assert.IsNotNull(finalizerQue, "Map.FinalizerQueue property returned null.");
		//	var notRootedInfo = map.GetNotRooted(finalizerQue);
		//	Assert.IsNotNull(notRootedInfo, "Map.GetNotRooted() method returned null.");
		//	stopWatch.Stop();

		//	TestContext.WriteLine("TEST DURATION: " + Utils.DurationString(stopWatch.Elapsed));
		//	TestContext.WriteLine("Finalizer Queue Count: " + finalizerQue.Length);
		//	TestContext.WriteLine("Not Rooted Count: " + notRootedInfo.Item1.Length);
		//	TestContext.WriteLine("Not Found Count: " + notRootedInfo.Item2.Length);
		//}

		[TestMethod]
		public void CompareDumpsTypeInstances()
		{
			var map0 = OpenMap(@"");
			var map1 = OpenMap(@"");
			Map.MapsDiff(map0,map1);
		}

		[TestMethod]
		public void ArraysScanTest()
		{
			var map = OpenMap(@"");
			using (map)
			{
				var stopWatch = new Stopwatch();
				stopWatch.Start();

				var lst = map.GetElementTypeTypes(ClrElementType.SZArray);

				int instanceCount = 0;
				int maxTypeInstanceCount = 0;
				int minTypeInstanceCount = Int32.MaxValue;
				int maxInstanceId = -1;
				int minInstanceId = -1;

				for (int i = 0, icnt = lst.Count; i < icnt; ++i)
				{
					try
					{
						var addresses = map.GetTypeAddresses(lst[i]);
						Assert.IsNotNull(addresses, "Map.GetTypeAddresses returned null.");
						instanceCount += addresses.Length;
						if (maxTypeInstanceCount < addresses.Length)
						{
							maxTypeInstanceCount = addresses.Length;
							maxInstanceId = lst[i];
						}
						if (minTypeInstanceCount > addresses.Length)
						{
							minTypeInstanceCount = addresses.Length;
							minInstanceId = lst[i];
						}
					}
					catch (Exception ex)
					{
						var id = lst[i];
						var typeName = "Type id is outside id range.";
						if (id >= 0 && id < map.TypeCount())
							typeName = map.GetTypeName(id);
						Assert.Fail(
							Environment.NewLine + "Map.GetTypeAddresses failed, for type id: " + id + Environment.NewLine
							+ "Type: " + typeName + Environment.NewLine
							+ ex.ToString()
							);
					}
				}

				stopWatch.Stop();
				var duration = Utils.DurationString(stopWatch.Elapsed);

				var maxInstanceName = map.GetTypeName(maxInstanceId);
				var minInstanceName = map.GetTypeName(minInstanceId);
				TestContext.WriteLine("Scanning arrays duration: " + duration + Environment.NewLine
										+ "Type count: " + Utils.LargeNumberString(lst.Count) + Environment.NewLine
										+ "Instance count: " + Utils.LargeNumberString(instanceCount) + Environment.NewLine
										+ "Max instances type: [" + Utils.LargeNumberString(maxTypeInstanceCount) + "] " + maxInstanceName + Environment.NewLine
										+ "Min instances type: [" + Utils.LargeNumberString(minTypeInstanceCount) + "] " + minInstanceName);

			}
		}

		#region Field Parents / Dependencies

		[TestMethod]
		public void TestFieldsMapfiles()
		{
			string error;
			const string parentsPath = @"D:\Jerzy\WinDbgStuff\dumps\DumpSearch\DumpSearch.exe_160711_121816.map\DumpSearch.exe_160711_121816.FIELDPARENTINSTANCES[0].map";
			const string offsetsPath = @"D:\Jerzy\WinDbgStuff\dumps\DumpSearch\DumpSearch.exe_160711_121816.map\DumpSearch.exe_160711_121816.FIELDPARENTOFFSETS[0].map";
			ulong fldAddr = 0x00000002488cc8;

			var fieldInfos = FieldDependency.GetFieldOffsets(offsetsPath, out error);
			ulong[] fldAddresses = fieldInfos.Item1;
			long[] parentOffsets = fieldInfos.Item2;

			var ndx = Array.BinarySearch(fldAddresses, fldAddr);
			Assert.IsTrue(ndx >= 0);

			Assert.IsNull(error);
			using (var mmf = FieldDependency.GetFieldParentMap(parentsPath, "ParentInstances", out error))
			{
				KeyValuePair<ulong,int>[] parents = FieldDependency.GetFieldParents(mmf, parentOffsets[ndx], parentOffsets[ndx + 1], out error);
				Assert.IsNull(error);
			}

		}

		#endregion Field Parents / Dependencies

		#region Type Value Reports

		[TestMethod]
		public void TestTypeValueReports()
		{
			string error = null;
			using (var map = OpenMap(_mapPath))
			{
				try
				{
					var typeId = map.GetTypeId(_typeName);
					var addresses = map.GetTypeAddresses(typeId);
					Assert.IsTrue(addresses != null && addresses.Length > 0);
					var dispType = DmpNdxQueries.FQry.getDisplayableType(map.CurrentInfo, map.GetFreshHeap(), addresses[0]);

				}
				catch (Exception ex)
				{
					Assert.IsTrue(false,ex.ToString());
				}
			}
		}


		#endregion Type Value Reports

		#region Object Sizes

		[TestMethod]
	    public void SelectedBaseObjectSizesTest()
	    {
            var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Baly\AnalyticsLatencyDump06062016 03354291.map");

	        var requestedTypeNames = new string[]
	        {
                "ECS.Common.HierarchyCache.Structure.CashEffectPosition",
                "ECS.Common.HierarchyCache.Structure.RealPosition",
                "ECS.Common.HierarchyCache.Structure.WhatIfPosition",
                "ECS.Common.HierarchyCache.Structure.CashPosition"

                //"ECS.Common.HierarchyCache.Structure.RealtimePrice",
                //"ECS.Common.HierarchyCache.Structure.IndirectRealtimePrice",

            };
		    string[] typeNames;
			List<ulong[]> addressesLst;

			ulong[][] addresses;
            ulong[] allAddress;
	        Tuple<ulong, ulong[]> result;
	        int usedCnt = 0;
	        int fraction = 2;

            Assert.IsNotNull(map);
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            using (map)
	        {
                var dmp = map.Dump;
                Assert.IsNotNull(dmp, "Map property 'Dump (ClrtDump)' is null.");
                string error;
				int len = 0;
				var typeNameLst = new List<string>(requestedTypeNames.Length);
				addressesLst = new List<ulong[]>(requestedTypeNames.Length);
				for (int i = 0, icnt = requestedTypeNames.Length; i < icnt; ++i)
				{
					var id = map.GetTypeId(requestedTypeNames[i]);
					if (id != Constants.InvalidIndex)
					{
						typeNameLst.Add(requestedTypeNames[i]);
						var addrAry = map.GetTypeAddresses(id);
						addressesLst.Add(addrAry);
						len += addrAry.Length;
					}
				}

		        typeNames = typeNameLst.ToArray();
				typeNameLst.Clear();
		        typeNameLst = null;
				int[] typeIds = new int[typeNames.Length];

	            allAddress = new ulong[len];
	            int off = 0;
	            for (int i = 0, icnt = typeNames.Length; i < icnt; ++i)
	            {
	                Array.Copy(addressesLst[i],0,allAddress,off,addressesLst[i].Length);
	                off += addressesLst[i].Length;
	            }
                Array.Sort(allAddress);

				HashSet<ulong> done = new HashSet<ulong>();

				usedCnt = allAddress.Length/fraction;
	            ulong[] ary;
	            if (fraction > 1)
	            {
	                ary = new ulong[usedCnt];
	                int ndx = 0;
	                for (int i = 0, icnt = allAddress.Length; i < icnt && ndx < usedCnt; ++i)
	                {
						if ((i%fraction) == 0)
							ary[ndx++] = allAddress[i];
						else
						{
							done.Add(allAddress[i]);
						}
	                }
	            }
	            else
	            {
	                ary = allAddress;
	            }
				result = ClrtDump.GetTotalSize(map.GetFreshHeap(), ary, done, out error);
                Assert.IsNotNull(result);

	        }

	        double dpct = (double) usedCnt/(double) allAddress.Length*100.0;

            stopWatch.Stop();
            TestContext.WriteLine("TEST DURATION: " + Utils.DurationString(stopWatch.Elapsed));
            TestContext.WriteLine("Instance count: " + Utils.SizeString(allAddress.Length));
            TestContext.WriteLine("Used instances count: " + Utils.SizeString(usedCnt));
            TestContext.WriteLine("Total size: " + Utils.SizeString(result.Item1));
            TestContext.WriteLine("Percent considered: " + Utils.SizeString((int)dpct));
            for (int i = 0, icnt = typeNames.Length; i < icnt; ++i)
            {
                TestContext.WriteLine(Utils.SizeStringHeader(addressesLst[i].Length) + typeNames[i]);
            }
        }


        [TestMethod]
		public void SelectedObjectSizesTest()
		{
			ulong[] addresses = new ulong[]
			{
0x0000a12f531808,
0x0000a12f531848,
0x0000a12f531888,
0x0000a12f531908,
0x0000a12f531948,
0x0000a12f531988,
0x0000a12f5319c8,

			};

			var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Baly\AnalyticsLatencyDump06062016 03354291.map");
			var dmp = map.Dump;
			Assert.IsNotNull(dmp, "Map property 'Dump (ClrtDump)' is null.");
			string error;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();

			var typeId = map.GetTypeId("ECS.Common.HierarchyCache.MarketData.FastSmartWeakEvent<System.EventHandler>");
			Assert.AreNotEqual(Constants.InvalidIndex,typeId);
			var allAddresses = map.GetTypeAddresses(typeId);
			addresses = allAddresses;
			//addresses = new ulong[1000];
			//Array.Copy(allAddresses,addresses,addresses.Length);

			HashSet<ulong> done = new HashSet<ulong>();
			var result = ClrtDump.GetTotalSize(map.GetFreshHeap(), addresses, done, out error);
			var typeName = map.GetTypeNameAtAddr(addresses[0]);
			var totalSize = result.Item1;
			var notFoundTypes = result.Item2;

			stopWatch.Stop();

			TestContext.WriteLine("TEST DURATION: " + Utils.DurationString(stopWatch.Elapsed));
			TestContext.WriteLine("Type name: " + typeName);
			TestContext.WriteLine("Address count: " + Utils.SizeString(addresses.Length));
			TestContext.WriteLine("Total size: " + Utils.SizeString(totalSize));
			TestContext.WriteLine("Types not found count: " + Utils.SizeString(notFoundTypes.Length));
			TestContext.WriteLine("  ");
			var maxCnt = Math.Min(12, notFoundTypes.Length);
			for (int i = 0; i < maxCnt; ++i)
			{
				TestContext.WriteLine("  " + Utils.AddressString(notFoundTypes[i]));
			}

		}

		[TestMethod]
		public void TestArrayCounts()
		{
			var map = OpenMap(@"");
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();

			string error;
			var result = map.GetArrayCounts(true, out error);
			Assert.IsNull(error,error);

			var totalAryCount = result.Item1;
			//  triple<string, int, pair<ulong, int>[]>[]
			var aryInfos = result.Item2;

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

			var binHeap = new BinaryHeap<triple<int,string,ulong>>(new Utils.IntTripleCmp());
			bool cntDone = false;
			const int TopCnt = 50;

			for (int i = 0, icnt = aryInfos.Length; i < icnt; ++i)
			{
				var aryInfo = aryInfos[i];
				for (int j = 0, jcnt = aryInfo.Third.Length; j < jcnt; ++j)
				{
					var ary = aryInfo.Third[j];
					var cnt = ary.Second;

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
						var heapItem = new triple<int,string,ulong>(cnt,aryInfo.First,ary.First);
						if (hcnt == 0) binHeap.Insert(heapItem);
						else
						{
							if (binHeap.Peek().First < cnt) binHeap.Insert(heapItem);
						}
					}
					else
					{
						if (binHeap.Peek().First < cnt)
						{
							binHeap.RemoveRoot();
							var heapItem = new triple<int, string, ulong>(cnt, aryInfo.First, ary.First);
							binHeap.Insert(heapItem);
						}
					}
				}
			}

			var topCnt = binHeap.ToArray();
			Array.Sort(topCnt,(a,b)=>a.First < b.First ? 1 : ( a.First > b.First ? -1 : string.Compare(a.Second,b.Second,StringComparison.Ordinal)));

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
				TestContext.WriteLine(Utils.SizeStringHeader(a.First) + Utils.AddressString(a.Third) + "  " + a.Second);
			}
		}




		#endregion Object Sizes

		#region Open Map

		public static Map OpenMap(string mapPath)
		{
			string error;
			var map = Map.OpenMap(new Version(1,0,0), mapPath, out error);
			Assert.IsTrue(map != null, error);
			return map;
		}

		#endregion OpenMap

	}
}
