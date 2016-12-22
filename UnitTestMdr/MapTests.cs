using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
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

		private const string _typeName = @"Eze.Server.Common.Pulse.Common.ServerColumn";
		private const string _indexPath = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Baly\analytics_160107_122809.Baly.crash.dmp.map";


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

		#region (new) types and instances

		[TestMethod]
		public void TypeInstancesMapTest()
		{
			string path = @"D:\Jerzy\WinDbgStuff\dumps\TestApp\TestApp.exe_161031_093521.map";
			string typeName = "System.Text.EncoderReplacementFallback";

			DumpIndex index = OpenIndex(path);
			Assert.IsNotNull(index);

			var typeId = index.GetTypeId(typeName);
			Assert.AreNotEqual(Constants.InvalidIndex, typeId);
			var ndxTypeName = index.GetTypeName(typeId);
			Assert.IsTrue(Utils.SameStrings(typeName, ndxTypeName));
			var addresses = index.GetTypeInstances(typeId);


		}

		#endregion (new) types and instances



		#region Instance Dependencies

		//[TestMethod]
		//public void TestFieldDependencyFields()
		//{
		//    string error;
		//          //var map = OpenMap(@"D:\Jerzy\WinDbgStuff\Dumps\DumpTest\DumpTest.exe_160820_073947.map");
		//          var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\DupCacheIssue\A2_noDF.map");
		//          var outPath = map.ReportPath + Path.DirectorySeparatorChar + "FIELD_DEPENDENCIES.txt";
		//    using (map)
		//    {
		//              map.DebugFieldDependencyDump(outPath, out error);
		//          }

		//          Assert.IsNull(error,error);
		//}

		//     [TestMethod]
		//     public void TestDependencyTree()
		//     {
		//         const string typeName = @"ECS.Common.Collections.Common.EzeBitVector";
		//         string error;
		////var map = OpenMap(@"D:\Jerzy\WinDbgStuff\Dumps\DumpTest\DumpTest.exe_160820_073947.map");
		////var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\DupCacheIssue\A2_noDF.map");
		//var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\DupCacheIssue\A2_noDF.map");
		//using (map)
		//         {
		//             int typeId = map.GetTypeId(typeName);
		//             ulong[] typeAddresses = map.GetTypeRealAddresses(typeId);
		//	Tuple<DependencyNode, int> result = map.GetAddressesDescendants(typeId, typeAddresses, 6, out error);
		//          var count = result.Item2;
		//         }

		//         Assert.IsNull(error, error);
		//     }


//		[TestMethod]
//		public void TestFieldDependencyNonrooted()
//		{
//			string error;
////			var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\TestApp\TestApp.exe_161021_104608.map");
//			var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\ConvergEx\Analytics_Post.map");
//			Assert.IsNotNull(map);
//			List<string> errors = new List<string>();
//			using (map)
//			{
//				BinaryReader fieldOffsets = null;
//				try
//				{
//					var path = DumpFileMoniker.GetFilePath(map.CurrentRuntimeIndex, map.IndexFolder, map.DumpFileName,
//						Constants.MapFieldParentOffsetsFilePostfix);
//					fieldOffsets = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read));
//					var cnt = fieldOffsets.ReadInt32()-1; // last offset address is invalid
//					ulong[] fieldAddresses = new ulong[cnt];
//					for (int i = 0; i < cnt; ++i)
//					{
//						fieldAddresses[i] = fieldOffsets.ReadUInt64();
//						var offs = fieldOffsets.ReadInt64();
//					}



//					Assert.IsTrue(Utils.IsSorted(fieldAddresses));
//					var instances = map.Instances;
//					var roots = map.Roots;
//					int offNdx = 0;
//					int instNdx = 0;
//					int offCnt = fieldAddresses.Length;
//					int instCnt = instances.Length;
//					Assert.IsTrue(instCnt>=offCnt);
//					List<ulong> unrooted = new List<ulong>(instCnt - offCnt);
//					List<ulong> rooted = new List<ulong>(1024);
//					while (instNdx < instCnt && offNdx < offCnt)
//					{
//						var instAddr = instances[instNdx];
//						var fldAddr = fieldAddresses[offNdx];
//						if (instAddr == fldAddr)
//						{
//							++instNdx;
//							++offNdx;
//							continue;
//						}
//						if (instAddr < fldAddr)
//						{
//							if (roots.IsRootedOutsideFinalization(instAddr))
//								rooted.Add(instAddr);
//							else
//								unrooted.Add(instAddr);
//							++instNdx;
//							continue;
//						}
//						if (instAddr > fldAddr) // should not happen
//						{
//							Assert.Fail("Shoul not happen: instAddr > fldAddr");
//						}


//					}
//					for (; instNdx < instCnt; ++instNdx)
//						unrooted.Add(instances[instNdx]);
//					Assert.IsTrue(unrooted.Count == instCnt - offCnt - rooted.Count);
//				}
//				catch (Exception ex)
//				{
//					Assert.IsTrue(false, ex.ToString());
//				}
//				finally
//				{
//					fieldOffsets?.Close();
//				}
//			}

//			//Assert.IsNull(error, error);
//		}


		#endregion Instance Dependencies

		[TestMethod]
		public void Conversions()
		{
			//TimeSpan ts1 = new TimeSpan(0, 1, 34, 57);
			//TimeSpan ts2 = new TimeSpan(0, 0, 3, 29);
			TimeSpan ts1 = new TimeSpan(0, 2, 38, 19);
			TimeSpan ts2 = new TimeSpan(0, 0, 3, 02);

			double gain = 100*((ts1.TotalMilliseconds - ts2.TotalMilliseconds)/ts2.TotalMilliseconds);


		}


		#region instance sizes

		[TestMethod]
		public void TestInstanceSizes()
		{
			var map = OpenMap(_indexPath);
			using (map)
			{
				string error;
				var sizeInfo = map.GetSizeArrays(out error);
				Assert.IsNull(error, error);
				ClrElementType[] elems = map.GetElementTypeList(out error);
				Assert.IsNull(error, error);
				Assert.IsTrue(sizeInfo.Key.Length == sizeInfo.Value.Length);
				Assert.IsTrue(map.Instances.Length == sizeInfo.Key.Length);
				HashSet<string> set = new HashSet<string>(StringComparer.Ordinal);
				int totalCount = sizeInfo.Key.Length;
				int freeId = map.GetTypeId("Free");
				int equalCnt = 0;
				uint maxDiff = 0;
				int maxTypeId = 0;
				ulong maxAddr = 0UL;
				string maxTypeName = string.Empty;
				ClrElementType maxElem = ClrElementType.Unknown;
				for (int i = 0; i < totalCount; ++i)
				{
					var typeId = map.GetTypeId(i);
					if (typeId == freeId) continue;

					var bsize = sizeInfo.Key[i];
					var tsize = sizeInfo.Value[i];
					if (bsize == tsize)
					{
						++equalCnt;
						continue;
					}
					string typeName = map.GetTypeName(typeId);
					set.Add(typeName);
					ClrElementType elem = elems[i];
					Assert.IsTrue(tsize >= bsize);
					uint diff = tsize - bsize;
					if (diff > maxDiff)
					{
						maxDiff = diff;
						maxTypeName = typeName;
						maxElem = elem;
						maxAddr = map.GetInstanceAddress(i);
					}


				}
				int a = 1;
			}
		}

		#endregion instance sizes

		[TestMethod]
		public void TestBuildTestUnitIssues()
		{
			const string typeName = @"Microsoft.VisualStudio.TestTools.TestTypes.Unit.UnitTestResult";
			string error = null;
			//var map = OpenMap(@"D:\Jerzy\WinDbgStuff\Dumps\DumpTest\DumpTest.exe_160820_073947.map");
			//var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\DupCacheIssue\A2_noDF.map");
			var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\mainlinebuildissue\LouDump.map");
			using (map)
			{
				int typeId = map.GetTypeId(typeName);
				ulong[] typeAddresses = map.GetTypeRealAddresses(typeId);
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


		//[TestMethod]
		//public void TestWeakReferenceFields()
		//{
		//	string error;
		//	ulong address = 0x0000008000c0d8;
		//	var map =
		//		OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\Eze.Analytics.Svc.exe.EzeBitVector.PadOff.map");
		//	Assert.IsNotNull(map);
		//	List<string> errors = new List<string>();
		//	using (map)
		//	{
		//		int totalCount;
		//		KeyValuePair<int, ulong[]>[] prefaddresses = map.GetTypeWithPrefixAddresses("System.WeakReference", false,
		//			out totalCount);

		//		int typeId = map.GetTypeId("System.WeakReference");
		//		ulong[] addresses = map.GetTypeRealAddresses(typeId);

		//		var heap = map.GetFreshHeap();
		//		ClrType weakReferenceType = heap.GetObjectType(address); // System.WeakReference
		//		ClrInstanceField m_handleField = weakReferenceType.Fields[0];
		//		object m_handleValue = m_handleField.GetValue(address, false, false);
		//		ClrType m_handleType = m_handleField.Type; //  System.IntPtr
		//		ClrInstanceField m_valueField = m_handleType.Fields[0];
		//		ulong m_valueValue = (ulong) m_valueField.GetValue((ulong) (long) m_handleValue, true, false);
		//		ClrType eeferencedType = heap.GetObjectType(m_valueValue); // type this WeakReference points to

		//		//ulong[] addrTest = new[] {address};
		//		var result = DmpNdxQueries.SpecializedQueries.getWeakReferenceInfos(heap, addresses, m_handleField, m_valueField);
		//		if (result.Item1 != null)
		//		{
		//			Assert.IsTrue(false, result.Item1);
		//		}



		//		string repPath = map.AdhocFolder + @"\WeakReferenceObjects.txt";
		//		StreamWriter sw = null;
		//		try
		//		{
		//			sw = new StreamWriter(repPath);
		//			var infos = result.Item2;
		//			Array.Sort(infos, new Utils.TripleUlUlStrByStrUl2Cmp());

		//			sw.WriteLine("### MDRDESK REPORT: WeakReference");
		//			sw.WriteLine("### TITLE: WeakReference");
		//			sw.WriteLine("### COUNT: " + Utils.LargeNumberString(infos.Length));
		//			sw.WriteLine("### COLUMNS: Address|ulong \u271A Object Address|string \u271A Object Type|string");
		//			sw.WriteLine("### SEPARATOR:  \u271A ");
		//			sw.WriteLine("#### WeakReference count: " + Utils.LargeNumberString(infos.Length));

		//			sw.WriteLine("#### Total WeakReference instance count: " + infos.Length);
		//			sw.WriteLine("#### Clumns: address of WeakReference, address of type pointed to, type name");
		//			sw.WriteLine("####");
		//			for (int i = 0, icnt = infos.Length; i < icnt; ++i)
		//			{
		//				sw.Write(Utils.AddressStringHeader(infos[i].First) + Constants.HeavyGreekCrossPadded);
		//				sw.Write(Utils.AddressStringHeader(infos[i].Second) + Constants.HeavyGreekCrossPadded);
		//				sw.WriteLine(infos[i].Third);
		//			}
		//		}
		//		catch (Exception ex)
		//		{
		//			Assert.IsTrue(false, ex.ToString());
		//		}
		//		finally
		//		{
		//			sw?.Close();
		//		}

		//	}

		//	//Assert.IsNull(error, error);
		//}






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

		//[TestMethod]
		//public void TestConcurrentDictionaryContent()
		//{
		//	var sbTypeName =
		//		@"System.Collections.Concurrent.ConcurrentDictionary<System.String,System.Collections.Generic.KeyValuePair<System.String,System.String>>";
		//	var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\TestApp\TestApp.exe_160928_083345.map");
		//	bool found = false;
		//	using (map)
		//	{
		//		var typeId = map.GetTypeId(sbTypeName);
		//		Assert.IsFalse(typeId == Constants.InvalidIndex);
		//		ulong[] typeAddresses = map.GetTypeRealAddresses(typeId);
		//		Assert.IsTrue(typeAddresses != null && typeAddresses.Length > 0);
		//		for (int i = 0, icnt = typeAddresses.Length; i < icnt; ++i)
		//		{
		//			SpecializedQueries.getConcurrentDictionaryContent(map.GetFreshHeap(), typeAddresses[i]);
		//		}
		//	}
		//}

		#endregion Specialized Queries

		[TestMethod]
		public void TestTypeNamespaceDisplay()
		{
			using (var map = OpenMap(@"D:\Jerzy\WinDbgStuff\Dumps\DumpTest\DumpTest.exe_160820_073947.map"))
			{
				var dispInfo = map.GetNamespaceDisplay();
			}
		}


		//[TestMethod]
		//public void TestDictionaries()
		//{
		//	using (var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Lou\Eze.Analytics.App_161018_173251.map"))
		//	{

		//		var typeName = "System.Collections.Generic.Dictionary<System.String,System.Object>";
		//		var typeId = map.GetTypeId(typeName);
		//		ulong[] addresses = map.GetTypeRealAddresses(typeId);
		//		int[] counts = new int[addresses.Length];
		//		var heap = map.GetFreshHeap();
		//		for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
		//		{
		//			var result = CollectionContent.getDictionaryCount(heap, addresses[i]);
		//			counts[i] = result.Item2;
		//		}
		//		Array.Sort(counts, addresses);
		//		List<string> keyLst = new List<string>();
		//		for (int i = addresses.Length - 4; i > 0; --i)
		//		{
		//			if (counts[i] < 1) break;
		//			var eresult = CollectionContent.getDictionaryEntries(heap, addresses[i]);
		//			var keys = CollectionContent.getDictionaryStringKeys(heap, eresult.Item2, eresult.Item3);

		//			string str = string.Join(Constants.HeavyGreekCrossPadded, keys);
		//			keyLst.Add(str);
		//		}

		//		keyLst.Sort();


		//		List<string[]> keyAry = new List<string[]>();
		//		for (int i = addresses.Length - 1, icnt = addresses.Length - 4; i > icnt; --i)
		//		{
		//			if (counts[i] < 1) break;
		//			var eresult = CollectionContent.getDictionaryEntries(heap, addresses[i]);
		//			var keys = CollectionContent.getDictionaryStringKeys(heap, eresult.Item2, eresult.Item3);
		//			keyAry.Add(keys);
		//		}

		//	}
		//}


		//[TestMethod]
		//public void TestDictionaryEntries()
		//{
		//	using (var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Lou\Eze.Analytics.App_161018_173251.map"))
		//	{

		//		var typeName = "System.Collections.Generic.Dictionary+Entry<System.String,System.Object>[]";
		//		var typeId = map.GetTypeId(typeName);
		//		ulong[] addresses = map.GetTypeRealAddresses(typeId);
		//		int[] counts = new int[addresses.Length];
		//		var heap = map.GetFreshHeap();
		//		var keyLst = new List<KeyValuePair<ulong, string[]>>();
		//		var specKeysLst = new List<KeyValuePair<ulong, string[]>>();
		//		var specKeyAddresses = new List<ulong>();
		//		for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
		//		{
		//			var keys = CollectionContent.getEntryStringKeys(heap, addresses[i]);
		//			keyLst.Add(new KeyValuePair<ulong, string[]>(addresses[i], keys));
		//			for (int j = 0; j < 5 && j < keys.Length; ++j)
		//			{
		//				if (keys[j] == "1")
		//				{
		//					specKeysLst.Add(new KeyValuePair<ulong, string[]>(addresses[i], keys));
		//					specKeyAddresses.Add(addresses[i]);
		//					break;
		//				}
		//			}
		//		}
		//		var keyAry = keyLst.ToArray();
		//		keyLst = null;
		//		var specKeyAry = specKeysLst.ToArray();
		//		specKeysLst = null;

		//		var specKeyAryAddr = specKeyAddresses.ToArray();

		//		Array.Sort(keyAry, (a, b) => a.Value.Length < b.Value.Length ? 1 : (a.Value.Length > b.Value.Length ? -1 : 0));
		//		Array.Sort(specKeyAry, (a, b) => a.Value.Length < b.Value.Length ? 1 : (a.Value.Length > b.Value.Length ? -1 : 0));

		//		var hist1 = map.GetGenerationHistogram(addresses);
		//		var hist2 = map.GetGenerationHistogram(specKeyAryAddr);

		//		string error;

		//		//HashSet<ulong> parents = new HashSet<ulong>();
		//		//for (int i = 0, icnt = specKeyAryAddr.Length; i < icnt; ++i)
		//		//{
		//		//	var pars = map.GetParents(specKeyAryAddr[i], out error);
		//		//	for (int j = 0, jcnt = pars.Length; j < jcnt; ++j)
		//		//	{
		//		//		parents.Add(pars[j].Key);
		//		//	}
		//		//}

		//	}
		//}

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

				var typeAddresses = map.GetTypeRealAddresses(typeId);

				var heap = map.GetFreshHeap();
				ClrInstanceField[] fields = null;
				string[] values = null;
				StreamWriter sw = null;
				try
				{
					sw = new StreamWriter(map.AdhocFolder + Path.DirectorySeparatorChar + typeName + ".txt");
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

		//		var addresses = map.GetTypeRealAddresses(id);
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

		//[TestMethod]
		//public void TestTypeElemnts()
		//{
		//	string error;
		//	var map = OpenMap(@"");

		//	var ets0 = map.GetElementTypes(ClrElementType.Struct);

		//	var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
		//	List<string> lst = new List<string>(ets0.Length);
		//	for (int i = 0, icnt = ets0.Length; i < icnt; ++i)
		//	{
		//		var tp = ets0[i];
		//		var fldInfo = map.GetFieldNamesAndTypes(tp.Key);
		//		sb.Clear();
		//		sb.Append(tp.Value).Append("  ").AppendLine(Utils.SmallIdHeader(tp.Key));

		//		for (int j = 0, jcnt = fldInfo.Length; j < jcnt; ++j)
		//		{
		//			var fld = fldInfo[j];
		//			sb.Append("   ").Append(Utils.SmallIdHeader(fld.First)).Append(fld.Second).Append("  ").AppendLine(fld.Third);
		//		}
		//		lst.Add(sb.ToString());
		//	}
		//	StringBuilderCache.Release(sb);

		//	lst.Sort(StringComparer.Ordinal);
		//	var path = TestConfiguration.OutPath0 + map.DumpBaseName + ".STRUCTURES.txt";
		//	Utils.WriteStringList(path, lst, out error);
		//	Assert.IsNull(error,error);

		//	var ets1 = map.GetElementTypes(ClrElementType.Unknown);
		//	Assert.IsTrue(ets1.Length==0);
		//	var nonEmptys = map.GetNonemptyElementTypes();
		//	var ets2 = map.GetElementTypes(ClrElementType.Array);
		//}

		[TestMethod]
		public void TestTypeInstanceSearch()
		{
			var map = OpenMap(_indexPath);
			Assert.IsNotNull(map);
			List<string> columns = new List<string>();
			using (map)
			{
				try
				{
					var typeId = map.GetTypeId(_typeName);
					var typeInstances = map.GetTypeInstances(typeId);
					var heap = map.Dump.Heap;
					for (int i = 0, icnt = typeInstances.Length; i < icnt; ++i)
					{
						var addr = Utils.RealAddress(typeInstances[i]);
						var clrType = heap.GetObjectType(addr);
						var fldCalc = clrType.GetFieldByName("calc");
						Assert.IsNotNull(fldCalc);
						var calcObj = fldCalc.GetValue(addr, false, false);
						if (calcObj == null || (ulong)calcObj == 0UL)
						{
							var fldColumnName = clrType.GetFieldByName("ColumnName");
							Assert.IsNotNull(fldColumnName);
							var colName = (string)fldColumnName.GetValue(addr, false, true);
							columns.Add(colName);
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
				var addresses = map.GetTypeRealAddresses(id);
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
			var daddresses = map.GetTypeRealAddresses(did);

			var rid = map.GetTypeId("Eze.Server.Common.Pulse.CalculationCache.RowCache");
			Assert.IsTrue(IndexValue.IsIndex(rid));
			Assert.IsFalse(IndexValue.IsInvalidIndex(rid));
			var raddresses = map.GetTypeRealAddresses(rid);



			var heap = map.GetFreshHeap();
			List<ulong> adrLst = new List<ulong>(daddresses.Length);
			HashSet<ulong> adrSet = new HashSet<ulong>();
			var bitVecDct = new SortedDictionary<ulong, List<ulong[]>>();
			for (int i = 0, icnt = daddresses.Length; i < icnt; ++i)
			{
				var addr = daddresses[i];
				var clrType = heap.GetObjectType(addr);
				Assert.IsNotNull(clrType);
				var faddr = (ulong) clrType.Fields[0].GetValue(addr);
				adrLst.Add(faddr);
				adrSet.Add(faddr);

				var bvAddr = (ulong) clrType.Fields[1].GetValue(addr);
				var bvType = heap.GetObjectType(bvAddr);
				if (bvType == null)
				{
					continue;
				}
				var bitsFldAddr = (ulong) bvType.Fields[0].GetValue(bvAddr);
				var bitsType = heap.GetObjectType(bitsFldAddr);
				if (bitsType == null)
				{
					continue;
				}
				var bitsCnt = bitsType.GetArrayLength(bitsFldAddr);
				var bitsAry = new ulong[bitsCnt];
				for (int j = 0; j < bitsCnt; ++j)
				{
					bitsAry[j] = (ulong) bitsType.GetArrayElementValue(bitsFldAddr, j);
				}
				Array.Sort(bitsAry);
				List<ulong[]> lst;
				if (bitVecDct.TryGetValue(faddr, out lst))
				{
					lst.Add(bitsAry);
				}
				else
				{
					bitVecDct.Add(faddr, new List<ulong[]>() {bitsAry});
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

			Assert.IsNotNull(daddresses, "Map.GetTypeRealAddresses returned null.");
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

		//[TestMethod]
		//public void TestDumpReversedTypeName()
		//{
		//	var map = OpenMap(@"");
		//	Assert.IsNotNull(map);
		//	var outFile = DumpFileMoniker.GetOuputFolder(map.DumpPath) + Path.DirectorySeparatorChar + "REVERSEDNAMES.txt";
		//	string error;
		//	var ok = map.DumpReversedNames(outFile, out error);
		//	Assert.IsTrue(ok,error);
		//}

		#endregion Types

		[TestMethod]
		public void TextFinalizationQueue()
		{
			string error = null;
			var index = OpenIndex(_indexPath);
			Assert.IsNotNull(index);
			using (index)
			{
				var roots = index.GetRoots(out error);
				Assert.IsNull(error, error);
			}
		}

		#region Instance References

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
		//			var addresses = map.GetTypeRealAddresses(id);
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

			var addresses = map.GetTypeRealAddresses(id);
			Assert.IsNotNull(addresses, "Map.GetTypeRealAddresses returned null.");
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
			Array.Sort(nnames, StringComparer.Ordinal);
			Tuple<string, string> nbadCouple;
			var nisSorted = Utils.IsSorted(nnames, out nbadCouple);


			var id = map.GetTypeId(typeName);
			string name = map.GetTypeName(id);

			Assert.IsTrue(IndexValue.IsIndex(id));
			Assert.IsFalse(IndexValue.IsInvalidIndex(id));

			var addresses = map.GetTypeRealAddresses(id);
			Assert.IsNotNull(addresses, "Map.GetTypeRealAddresses returned null.");
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

		//	var addresses = map.GetTypeRealAddresses(id);
		//	Assert.IsNotNull(addresses, "Map.GetTypeRealAddresses returned null.");
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

		[TestMethod]
		public void TestGetTypeGroupedContent()
		{
			const string str = "Eze.Server.Common.Pulse.Common.Types.CachedValue+DefaultOnly<System.Decimal>";
			string error = null;
			SortedDictionary<string, int> dct = new SortedDictionary<string, int>(StringComparer.Ordinal);
			using (
				var map =
					OpenMap(
						@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\RealPositionCmp\Eze.Analytics.Svc.RPSMALL0_160913_153220.map")
			)
			{
				try
				{
					ClrType clrType = null;
					ClrInstanceField bitwiseCurrentPositionToggleState = null;
					ClrInstanceField indexIntoPositionIndexToFieldNumberConversionSet = null;
					int typeId = map.GetTypeId(str);
					ulong[] addresses = map.GetTypeRealAddresses(typeId);
					var heap = map.Dump.GetFreshHeap();
					for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
					{
						var addr = addresses[i];
						if (clrType == null)
						{
							clrType = heap.GetObjectType(addr);
							bitwiseCurrentPositionToggleState = clrType.GetFieldByName("bitwiseCurrentPositionToggleState");
							indexIntoPositionIndexToFieldNumberConversionSet =
								clrType.GetFieldByName("indexIntoPositionIndexToFieldNumberConversionSet");
							Assert.IsNotNull(bitwiseCurrentPositionToggleState.Type);
							Assert.IsNotNull(indexIntoPositionIndexToFieldNumberConversionSet.Type);
						}

						object fld1ValObj = bitwiseCurrentPositionToggleState.GetValue(addr);
						string fld1Val = ValueExtractor.GetPrimitiveValue(fld1ValObj, bitwiseCurrentPositionToggleState.Type);
						object fld2ValObj = indexIntoPositionIndexToFieldNumberConversionSet.GetValue(addr);
						string fld2Val = ValueExtractor.GetPrimitiveValue(fld2ValObj,
							indexIntoPositionIndexToFieldNumberConversionSet.Type);
						var key = fld1Val + "_" + fld2Val;
						int count;
						if (dct.TryGetValue(key, out count))
						{
							dct[key] = count + 1;
						}
						else
						{
							dct.Add(key, 1);
						}
					}

				}
				catch (Exception ex)
				{
					Assert.IsTrue(false, ex.ToString());
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
			using (var index = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Lou\Analytics3.dmp.map"))
			{
				try
				{
					ClrType clrType = null;
					ClrType aryType = null;
					ClrInstanceField bits = null;
					ulong aryAddr = 0;
					int typeId = index.GetTypeId(str);
					ulong[] addresses = index.GetTypeRealAddresses(typeId);
					var heap = index.Dump.GetFreshHeap();
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

						sw = new StreamWriter(index.AdhocFolder + Path.DirectorySeparatorChar + "EzeBitVector.txt");
						sw.WriteLine("#### Total EzeBitVector Instance Count: " + Utils.SizeString(addresses.Length));
						sw.WriteLine("#### Total EzeBitVector Unique Count: " + Utils.SizeString(dct.Count));
						sw.WriteLine("#### Columns: duplicate count, ulong[] size, vector content");
						foreach (var kv in dct)
						{
							var pos = kv.Key.IndexOf('_');
							int aryCount = Int32.Parse(kv.Key.Substring(0, pos));
							sw.WriteLine(Utils.CountStringHeader(kv.Value) + Utils.CountStringHeader(aryCount) + kv.Key.Substring(pos + 1));
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
				catch (Exception ex)
				{
					Assert.IsTrue(false, ex.ToString());
				}
			}
		}

		[TestMethod]
		public void TestDictionaryCounts()
		{
			const string typeName0 = "System.Decimal";
			const string typeName1 = "System.Collections.Generic.Dictionary+Entry<System.String,System.Object>[]";
			const string typeName2 = "System.Collections.Generic.Dictionary<System.String,System.Object>";

			string error = null;
			SortedDictionary<string, int> dct = new SortedDictionary<string, int>(StringComparer.Ordinal);
			int nullFieldCount = 0;
			StringBuilder sb = new StringBuilder(256);
			using (var index = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Lou\Analytics1.dmp.map"))
			{
				try
				{
					int typeId0 = index.GetTypeId(typeName0);
					int typeId1 = index.GetTypeId(typeName1);
					int typeId2 = index.GetTypeId(typeName2);

					var instNdxs = index.GetTypeInstanceIndices(typeId0);
					KeyValuePair<IndexNode, int>[] parentTree = index.GetParentReferences(instNdxs, out error, 3);

					int notRootedCount = 0;
					var set = new HashSet<ulong>();
					for (int i = 0, icnt = parentTree.Length; i < icnt; ++i)
					{
						var node = parentTree[i].Key;
						if (node.Nodes.Length < 1)
						{
							++notRootedCount;
							continue;
						}
						for (int j = 0, jcnt = node.Nodes.Length; j < jcnt; ++j)
						{
							int typeId = index.GetTypeId(node.Nodes[j].Index);
							if (typeId == typeId1)
							{
								var nodes = node.Nodes[j].Nodes;
								for (int k = 0, kcnt = nodes.Length; k < kcnt; ++k)
								{
									int tpId = index.GetTypeId(nodes[k].Index);
									if (tpId == typeId2)
									{
										var addr = index.GetInstanceAddress(nodes[k].Index);
										set.Add(Utils.RealAddress(addr));
									}
								}
							}
						}
					}

					ulong[] addresses = set.ToArray();
					Array.Sort(addresses);
					var heap = index.GetFreshHeap();

					int maxCount = 0;
					int minCount = Int32.MaxValue;
					int emptyCount = 0;
					long countSum = 0L;

					for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
					{
						var result = CollectionContent.getDictionaryCount(heap, addresses[i]);
						var count = result.Item2;
						if (count > maxCount) maxCount = count;
						if (count < minCount) minCount = count;
						countSum += count;
					}

					double avgCount = (double) countSum/addresses.Length;

					int a = 0;

					//StreamWriter sw = null;
					//try
					//{

					//	sw = new StreamWriter(index.AdhocFolder + Path.DirectorySeparatorChar + "EzeBitVector.txt");
					//	sw.WriteLine("#### Total EzeBitVector Instance Count: " + Utils.SizeString(addresses.Length));
					//	sw.WriteLine("#### Total EzeBitVector Unique Count: " + Utils.SizeString(dct.Count));
					//	sw.WriteLine("#### Columns: duplicate count, ulong[] size, vector content");
					//	foreach (var kv in dct)
					//	{
					//		var pos = kv.Key.IndexOf('_');
					//		int aryCount = Int32.Parse(kv.Key.Substring(0, pos));
					//		sw.WriteLine(Utils.CountStringHeader(kv.Value) + Utils.CountStringHeader(aryCount) + kv.Key.Substring(pos + 1));
					//	}

					//}
					//catch (Exception ex)
					//{
					//	Assert.IsTrue(false, ex.ToString());
					//}
					//finally { sw?.Close(); }

				}
				catch (Exception ex)
				{
					Assert.IsTrue(false, ex.ToString());
				}
			}
		}


		[TestMethod]
		public void TestDictionaryCounts2()
		{
			const string typeName0 = "ECS.Common.HierarchyCache.Structure.PositionIndexGroup";
			const string typeName1 = "System.Collections.Generic.Dictionary+Entry<System.String,System.Object>[]";
			const string typeName2 = "System.Collections.Generic.Dictionary<System.String,System.Object>";

			string error = null;
			SortedDictionary<string, int> dct = new SortedDictionary<string, int>(StringComparer.Ordinal);
			int nullFieldCount = 0;
			int recalcRequiredPoisonPositionsIfTtzSignChangesEmpty = 0;
			int recalcRequiredPositionsInducingRecalcEmpty = 0;

			StringBuilder sb = new StringBuilder(256);
			using (var index = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Lou\Analytics3.dmp.map"))
			{
				try
				{
					int typeId0 = index.GetTypeId(typeName0);
					var addresses = index.GetTypeRealAddresses(typeId0);
					var heap = index.GetFreshHeap();
					int clrTypeNullCount = 0;
					for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
					{
						var clrType = heap.GetObjectType(addresses[i]);
						if (clrType == null)
						{
							++clrTypeNullCount;
							continue;
						}

						var addr = addresses[i];
						var fld1 = clrType.GetFieldByName("recalcRequiredPoisonPositionsIfTtzSignChanges");
						var addr1 = Auxiliaries.getReferenceFieldAddress(addr, fld1, false);
						var fld2 = clrType.GetFieldByName("recalcRequiredPositionsInducingRecalc");
						var addr2 = Auxiliaries.getReferenceFieldAddress(addr, fld2, false);
						int cnt1 = 0;
						if (addr1 != Constants.InvalidAddress)
						{
							cnt1 = CollectionContent.getDictionaryCount(heap, addr1).Item2;
							if (cnt1 == 0)
								++recalcRequiredPoisonPositionsIfTtzSignChangesEmpty;
						}
						int cnt2 = 0;
						if (addr2 != Constants.InvalidAddress)
						{
							cnt2 = CollectionContent.getDictionaryCount(heap, addr2).Item2;
							if (cnt2 == 0)
								++recalcRequiredPositionsInducingRecalcEmpty;
						}

					}


				}
				catch (Exception ex)
				{
					Assert.IsTrue(false, ex.ToString());
				}
			}
		}

		//[TestMethod]
		//public void TestDictionaryCounts3()
		//{
		//	const string typeName0 = "ECS.Common.HierarchyCache.Structure.AggregationTracksClosedPositions";

		//	string error = null;
		//	SortedDictionary<string, int> dct = new SortedDictionary<string, int>(StringComparer.Ordinal);
		//	int nullFieldCount = 0;
		//	int recalcRequiredPoisonPositionsIfTtzSignChangesEmpty = 0;
		//	int recalcRequiredPositionsInducingRecalcEmpty = 0;

		//	StringBuilder sb = new StringBuilder(256);
		//	using (var index = OpenMap(@"C:\WinDbgStuff\dumps\Analytics\Lou\Analytics3.dmp.map"))
		//	{
		//		try
		//		{
		//			int typeId0 = index.GetTypeId(typeName0);
		//			var addresses = index.GetTypeRealAddresses(typeId0);
		//			var heap = index.GetFreshHeap();
		//			int dupCount = 0;
		//			int nullCount = 0;
		//			List<KeyValuePair<string[], string[]>> lst = new List<KeyValuePair<string[], string[]>>();
		//			HashSet<string> set = new HashSet<string>();
		//			SortedDictionary<string, int> dups = new SortedDictionary<string, int>();
		//			for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
		//			{
		//				var clrType = heap.GetObjectType(addresses[i]);
		//				if (clrType == null)
		//				{
		//					++nullCount;
		//					continue;
		//				}

		//				var addr = addresses[i];
		//				var fldKeyValue = clrType.GetFieldByName("keyValue");
		//				var fldKeyValueAddr = Auxiliaries.getReferenceFieldAddress(addr, fldKeyValue, false);
		//				var fldKeyValueArray = clrType.GetFieldByName("keyValueArray");
		//				var fldKeyValueArrayAddr = Auxiliaries.getReferenceFieldAddress(addr, fldKeyValueArray, false);
		//				var fldKeyValueObj = heap.GetObjectType(fldKeyValueAddr);
		//				var fldData = fldKeyValueObj.GetFieldByName("data");
		//				var fldDataStr = fldData.GetValue(fldKeyValueAddr, false, true) as string;
		//				if (!set.Add(fldDataStr))
		//				{
		//					++dupCount;
		//					int cnt;
		//					if (dups.TryGetValue(fldDataStr, out cnt))
		//					{
		//						dups[fldDataStr] = cnt + 1;
		//					}
		//					else
		//					{
		//						dups.Add(fldDataStr, 1);
		//					}
		//				}
		//				var fldDataStrAry = fldDataStr.Split('\u001e');
		//				var aryInfo = CollectionContent.getArrayContent(heap, fldKeyValueArrayAddr);

		//				lst.Add(new KeyValuePair<string[], string[]>(fldDataStrAry, aryInfo.Item4));


		//			}

		//			int notTheSameCount = 0;
		//			Dictionary<int, int> countDct = new Dictionary<int, int>();
		//			for (int i = 0, icnt = lst.Count; i < icnt; ++i)
		//			{
		//				var ary1 = lst[i].Key;
		//				var ary2 = lst[i].Value;
		//				int len;
		//				if (countDct.TryGetValue(ary1.Length, out len))
		//				{
		//					countDct[ary1.Length] = len + 1;
		//				}
		//				else
		//				{
		//					countDct.Add(ary1.Length,1);
		//				}
		//				if (ary1.Length != ary2.Length)
		//				{
		//					++notTheSameCount;
		//				}
		//				for (int j = 0, jcnt = ary1.Length; j < jcnt; ++j)
		//				{
		//					if (ary1[j] == ary2[j]) continue;
		//					++notTheSameCount;
		//				}

		//			}

		//			StreamWriter sw = null;
		//			try
		//			{
		//				var path = index.AdhocFolder + @"\KeyDups.txt";
		//				sw = new StreamWriter(path);
		//				foreach (var kv in dups)
		//				{
		//					sw.WriteLine("[" + (kv.Value + 1) + "] " + kv.Key);
		//				}
		//			}
		//			catch (Exception ex)
		//			{
		//				Assert.IsTrue(false, ex.ToString());
		//			}
		//			finally
		//			{
		//				sw?.Close();
		//			}

		//			Assert.IsTrue(true);
		//		}
		//		catch (Exception ex)
		//		{
		//			Assert.IsTrue(false, ex.ToString());
		//		}
		//	}
		//}

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
					reportPath = map.AdhocFolder + Path.DirectorySeparatorChar + Utils.BaseTypeName(typeName) + ".SIZE.DETAILS.txt";
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

					ulong[] strAddresses = map.GetTypeRealAddresses(strTypeId);
					Assert.IsNotNull(strAddresses);
					Assert.IsTrue(Utils.IsSorted(strAddresses));
					ulong[] myStrAddrs = DumpIndex.GetStringAddresses(heap, str, strAddresses, out error);
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

		//[TestMethod]
		//public void CompareDumpsTypeInstances()
		//{
		//	var map0 = OpenMap(@"");
		//	var map1 = OpenMap(@"");
		//	Map.MapsDiff(map0,map1);
		//}

		//[TestMethod]
		//public void ArraysScanTest()
		//{
		//	var map = OpenMap(@"");
		//	using (map)
		//	{
		//		var stopWatch = new Stopwatch();
		//		stopWatch.Start();

		//		var lst = map.GetElementTypeTypes(ClrElementType.SZArray);

		//		int instanceCount = 0;
		//		int maxTypeInstanceCount = 0;
		//		int minTypeInstanceCount = Int32.MaxValue;
		//		int maxInstanceId = -1;
		//		int minInstanceId = -1;

		//		for (int i = 0, icnt = lst.Count; i < icnt; ++i)
		//		{
		//			try
		//			{
		//				var addresses = map.GetTypeRealAddresses(lst[i]);
		//				Assert.IsNotNull(addresses, "Map.GetTypeRealAddresses returned null.");
		//				instanceCount += addresses.Length;
		//				if (maxTypeInstanceCount < addresses.Length)
		//				{
		//					maxTypeInstanceCount = addresses.Length;
		//					maxInstanceId = lst[i];
		//				}
		//				if (minTypeInstanceCount > addresses.Length)
		//				{
		//					minTypeInstanceCount = addresses.Length;
		//					minInstanceId = lst[i];
		//				}
		//			}
		//			catch (Exception ex)
		//			{
		//				var id = lst[i];
		//				var typeName = "Type id is outside id range.";
		//				if (id >= 0 && id < map.TypeCount())
		//					typeName = map.GetTypeName(id);
		//				Assert.Fail(
		//					Environment.NewLine + "Map.GetTypeRealAddresses failed, for type id: " + id + Environment.NewLine
		//					+ "Type: " + typeName + Environment.NewLine
		//					+ ex.ToString()
		//					);
		//			}
		//		}

		//		stopWatch.Stop();
		//		var duration = Utils.DurationString(stopWatch.Elapsed);

		//		var maxInstanceName = map.GetTypeName(maxInstanceId);
		//		var minInstanceName = map.GetTypeName(minInstanceId);
		//		TestContext.WriteLine("Scanning arrays duration: " + duration + Environment.NewLine
		//								+ "Type count: " + Utils.LargeNumberString(lst.Count) + Environment.NewLine
		//								+ "Instance count: " + Utils.LargeNumberString(instanceCount) + Environment.NewLine
		//								+ "Max instances type: [" + Utils.LargeNumberString(maxTypeInstanceCount) + "] " + maxInstanceName + Environment.NewLine
		//								+ "Min instances type: [" + Utils.LargeNumberString(minTypeInstanceCount) + "] " + minInstanceName);

		//	}
		//}

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
						var addrAry = map.GetTypeRealAddresses(id);
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
			var allAddresses = map.GetTypeRealAddresses(typeId);
			addresses = allAddresses;
			//addresses = new ulong[1000];
			//Array.Copy(allAddresses,addresses,addresses.Length);

			HashSet<ulong> done = new HashSet<ulong>();
			var result = ClrtDump.GetTotalSize(map.GetFreshHeap(), addresses, done, out error);
			var typeName = map.GetTypeName(addresses[0]);
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

		public static DumpIndex OpenMap(string mapPath)
		{
			string error;
			var map = DumpIndex.OpenIndexInstanceReferences(new Version(0,2,0,1), mapPath, 0, out error);
			Assert.IsTrue(map != null, error);
			return map;
		}

		public static DumpIndex OpenIndex(string mapPath)
		{
			string error;
			var version = Assembly.GetExecutingAssembly().GetName().Version;
			var index = DumpIndex.OpenIndexInstanceReferences(version, mapPath, 0, out error);
			Assert.IsTrue(index != null, error);
			return index;
		}

		#endregion OpenMap

	}
}
