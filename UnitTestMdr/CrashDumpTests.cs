using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using ClrMDRIndex;
using ClrMDRUtil;
using ClrMDRUtil.Utils;
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
			get { return testContextInstance; }
			set { testContextInstance = value; }
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

		#region misc

		[TestMethod]
		public void TestMisc()
		{
			ulong x = Utils.RootBits.Finalizer;
			ulong y = Utils.RootBits.Rooted;



			List<int[]> lst = new List<int[]>();
			int[] ary = new[] {1, 2, 3, 16, 17};
			Utils.GetPermutations(ary,0,ary.Length-1,lst);
			IntArrayStore rToF = new IntArrayStore(lst.Count+2);
			for (int i = 0, icnt = lst.Count; i < icnt; ++i)
			{
				var lary = lst[i];
				for (int j = 0, jcnt = lary.Length; j < jcnt; ++j)
				{
					rToF.Add(i, lary[j]);
				}
			}

			for (int i = 0, icnt = lst.Count; i < icnt; ++i)
			{
				var lary = lst[i];
				var sary = rToF.GetEntry(i);
				Assert.IsTrue(Utils.IsSorted(sary));
			}

			//rToF.Add(1, 16);
			//rToF.Add(1, 2);
			//rToF.Add(1, 3);
			//rToF.Add(1, 1);
			//rToF.Add(1, 0);

			//rToF.Add(1, 16);

		}

		#endregion misc

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
				Assert.IsTrue(Utils.SameStringArrays(strings, aryresult.Item5));
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
			ulong[] dctAddrs = new ulong[]
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
					if (valType.Name == "ERROR" || valType.Name == "System.__Canon")
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

		#region roots/instances/references

		//[TestMethod]
		//public void TestIndexing()
		//{
		//	string error;
		//	string dumpPath = Setup.RecentAdhocList[0];
		//	var dmp = OpenDump(dumpPath);
		//	var fileMoniker = new DumpFileMoniker(dumpPath);
		//	var strIds = new StringIdDct();

		//	using (dmp)
		//	{
		//		var heap = dmp.Heap;
		//		ulong[] instances = DumpIndexer.GetHeapAddressesCount(heap);
		//		string[] typeNames = DumpIndexer.GetTypeNames(heap, out error);
		//		var rootAddrInfo = ClrtRootInfo.GetRootAddresses(0, dmp.Runtimes[0], heap, typeNames, strIds, fileMoniker, out error);

		//		Assert.IsNull(error);
		//		Assert.IsTrue(Utils.IsSorted(rootAddrInfo.Item1));
		//		Assert.IsTrue(Utils.IsSorted(rootAddrInfo.Item2));

		//		var rootAddresses = Utils.MergeAddressesRemove0s(rootAddrInfo.Item1, rootAddrInfo.Item2);

		//		Assert.IsTrue(Utils.AreAllInExcept0(rootAddresses, rootAddrInfo.Item1));
		//		Assert.IsTrue(Utils.AreAllInExcept0(rootAddresses, rootAddrInfo.Item2));
		//		Assert.IsTrue(Utils.AreAllDistinct(rootAddresses));
		//		Assert.IsTrue(Utils.IsSorted(rootAddresses));

		//		rootAddresses = Utils.GetRealAddressesInPlace(rootAddresses);

		//		HashSet<ulong> done = new HashSet<ulong>();
		//		SortedDictionary<ulong, List<ulong>> objRefs = new SortedDictionary<ulong, List<ulong>>();
		//		SortedDictionary<ulong, List<ulong>> fldRefs = new SortedDictionary<ulong, List<ulong>>();

		//		bool result = References.GetRefrences(heap, rootAddresses, objRefs, fldRefs, done, out error);

		//		Assert.IsTrue(result);
		//		Assert.IsNull(error);

		//		string path = fileMoniker.GetFilePath(0, Constants.MapParentFieldsRootedPostfix);
		//		result = DumpReferences(path, objRefs, instances, out error);
		//		Assert.IsTrue(result);
		//		Assert.IsNull(error);
		//		path = fileMoniker.GetFilePath(0, Constants.MapFieldParentsRootedPostfix);
		//		result = DumpReferences(path, fldRefs, instances, out error);
		//		Assert.IsTrue(result);
		//		Assert.IsNull(error);

		//		var rootedAry = done.ToArray(); // for later
		//		Array.Sort(rootedAry);
		//		done.Clear();
		//		objRefs.Clear();
		//		fldRefs.Clear();
		//		var remaining = Utils.Difference(instances, rootedAry);
		//		result = References.GetRefrences(heap, remaining, objRefs, fldRefs, done, out error);
		//		Assert.IsTrue(result);
		//		Assert.IsNull(error);
		//		path = fileMoniker.GetFilePath(0, Constants.MapParentFieldsNotRootedPostfix);
		//		result = DumpReferences(path, objRefs, instances, out error);
		//		Assert.IsTrue(result);
		//		Assert.IsNull(error);
		//		path = fileMoniker.GetFilePath(0, Constants.MapFieldParentsNotRootedPostfix);
		//		result = DumpReferences(path, fldRefs, instances, out error);
		//		Assert.IsTrue(result);
		//		Assert.IsNull(error);

		//		var roots = ClrtRootInfo.Load(0, fileMoniker, out error);
		//		Assert.IsNull(error);
		//		Assert.IsNotNull(roots);

		//		ulong[] finalizer = Utils.GetRealAddressesInPlace(roots.FinalizerAddresses);
		//		Assert.IsTrue(Utils.IsSorted(finalizer));

		//		int fcnt = 0;
		//		for (int i = 0, icnt = finalizer.Length; i < icnt; ++i)
		//		{
		//			if (Utils.AddressSearch(instances,finalizer[i]) < 0) continue;
		//			++fcnt;
		//		}

		//		Utils.SetAddressBit(rootedAry, instances, Utils.RootBits.Rooted);
		//		Utils.SetAddressBit(finalizer, instances, Utils.RootBits.Finalizer);

		//		TestContext.WriteLine("INSTANCE COUNT: " + Utils.LargeNumberString(instances.Length));
		//		TestContext.WriteLine("ROOTED ARY COUNT: " + Utils.LargeNumberString(rootedAry.Length));
		//		TestContext.WriteLine("UNROOTED ARY COUNT: " + Utils.LargeNumberString(done.Count));
		//		TestContext.WriteLine("FINALIZER COUNT: " + Utils.LargeNumberString(finalizer.Length));

		//		var markedRoooted = 0;
		//		var markedFinalizer = 0;
		//		for (int i = 0, icnt = instances.Length; i < icnt; ++i)
		//		{
		//			var addr = instances[i];
		//			if (Utils.IsRooted(addr)) ++markedRoooted;
		//			if (Utils.IsFinalizer(addr)) ++markedFinalizer;
		//		}
		//		TestContext.WriteLine("MARKED ROOTED COUNT: " + Utils.LargeNumberString(markedRoooted));
		//		TestContext.WriteLine("MARKED FINALIZER COUNT: " + Utils.LargeNumberString(markedFinalizer));



		//	} // using dump
		//}

		//[TestMethod]
		//public void TestIndexing2()
		//{
		//	string error;
		//	string dumpPath = Setup.RecentAdhocList[0];
		//	var dmp = OpenDump(dumpPath);
		//	var fileMoniker = new DumpFileMoniker(dumpPath);
		//	var strIds = new StringIdDct();

		//	using (dmp)
		//	{
		//		var heap = dmp.Heap;
		//		var runtm = dmp.Runtimes[0];
		//		ulong[] instances = DumpIndexer.GetHeapAddressesCount(heap);
		//		string[] typeNames = DumpIndexer.GetTypeNames(heap, out error);
		//		var rootAddrInfo = ClrtRootInfo.GetRootAddresses(0, runtm, heap, typeNames, strIds, fileMoniker, out error);
		//		Utils.GetRealAddressesInPlace(rootAddrInfo.Item1);
		//		Utils.GetRealAddressesInPlace(rootAddrInfo.Item2);

		//		Assert.IsNull(error);
		//		Assert.IsTrue(Utils.AreAddressesSorted(rootAddrInfo.Item1));
		//		Assert.IsTrue(Utils.AreAddressesSorted(rootAddrInfo.Item2));

		//		var rootAddresses = rootAddrInfo.Item1;

		//		//Assert.IsTrue(Utils.AreAllInExcept0(rootAddresses, rootAddrInfo.Item1));
		//		//Assert.IsTrue(Utils.AreAllInExcept0(rootAddresses, rootAddrInfo.Item2));
		//		Assert.IsTrue(Utils.AreAllDistinct(rootAddresses));
		//		Assert.IsTrue(Utils.IsSorted(rootAddresses));

		//		rootAddresses = Utils.GetRealAddressesInPlace(rootAddresses);
		//		Bitset bitset = new Bitset(instances.Length);
		//		var rootAddressNdxs = Utils.GetAddressIndices(rootAddresses, instances);

		//		string path1 = fileMoniker.GetFilePath(0, Constants.MapParentFieldsRootedPostfix);
		//		string path2 = fileMoniker.GetFilePath(0, Constants.MapFieldParentsRootedPostfix);
		//		//bool result = References.GetRefrences(heap, rootAddressNdxs, instances, bitset, path1, path2, out error);
		//		bool result = References.CreateReferences2(0, heap, rootAddrInfo.Item1, instances, bitset, fileMoniker, null, out error);

		//		int markRootCount = Utils.SetAddressBitIfSet(instances, rootAddrInfo.Item2, Utils.RootBits.Rooted);
		//		int markFnlzCount = Utils.SetAddressBit(rootAddrInfo.Item2, instances, Utils.RootBits.Finalizer);

		//		Assert.IsTrue(result);
		//		Assert.IsNull(error);

		//		int[] head1;
		//		int[][] lists1;
		//		result = References.LoadReferences(path1, out head1, out lists1, out error);
		//		Assert.IsTrue(result);
		//		Assert.IsNull(error);
		//		Assert.IsTrue(Utils.IsSorted(head1));

		//		for (int i = 0, icnt = lists1.Length; i < icnt; ++i)
		//		{
		//			var lst = lists1[i];
		//			Assert.IsTrue(Utils.IsSorted(lst));
		//			Assert.IsTrue(Utils.AreAllDistinct(lst));
		//			Assert.IsTrue(Utils.DoesNotContain(lst,Int32.MaxValue));
		//		}

		//		string path1a = fileMoniker.GetFilePath(0, ".`PARENTFIELDSROOTED[0].BEFORE.NOFIN.bin");
		//		int[] head1a;
		//		int[][] lists1a;
		//		result = References.LoadReferences(path1a, out head1a, out lists1a, out error);
		//		Assert.IsTrue(result);
		//		Assert.IsNull(error);

		//		for (int i = 0, icnt = lists1a.Length; i < icnt; ++i)
		//		{
		//			var lst = lists1a[i];
		//			Assert.IsTrue(Utils.IsSorted(lst));
		//			//Assert.IsTrue(Utils.AreAllDistinct(lst));
		//			Assert.IsTrue(Utils.DoesNotContain(lst, Int32.MaxValue));
		//			Assert.IsTrue(Utils.Contains(lst,lists1[i]));
		//		}

		//		int[] head2;
		//		int[][] lists2;
		//		result = References.LoadReferences(path2, out head2, out lists2, out error);
		//		Assert.IsTrue(result);
		//		Assert.IsNull(error);
		//		Assert.IsTrue(Utils.IsSorted(head2));

		//		for (int i = 0, icnt = lists2.Length; i < icnt; ++i)
		//		{
		//			var lst = lists2[i];
		//			Assert.IsTrue(Utils.IsSorted(lst));
		//			Assert.IsTrue(Utils.AreAllDistinct(lst));
		//			Assert.IsTrue(Utils.DoesNotContain(lst, Int32.MaxValue));
		//		}


		//		string path2a = fileMoniker.GetFilePath(0, ".`FIELDPARENTSROOTED[0].BEFORE.NOFIN.bin");
		//		int[] head2a;
		//		int[][] lists2a;
		//		result = References.LoadReferences(path2a, out head2a, out lists2a, out error);
		//		Assert.IsTrue(result);
		//		Assert.IsNull(error);

		//		for (int i = 0, icnt = lists2a.Length; i < icnt; ++i)
		//		{
		//			var lst = lists2a[i];
		//			Assert.IsTrue(Utils.IsSorted(lst));
		//			Assert.IsTrue(Utils.DoesNotContain(lst, Int32.MaxValue));
		//			Assert.IsTrue(Utils.Contains(lst, lists2[i]));
		//		}

		//		TestContext.WriteLine("INSTANCE COUNT: " + Utils.LargeNumberString(instances.Length));
		//		//TestContext.WriteLine("ROOTED ARY COUNT: " + Utils.LargeNumberString(rootedAry.Length));
		//		//TestContext.WriteLine("UNROOTED ARY COUNT: " + Utils.LargeNumberString(done.Count));
		//		//TestContext.WriteLine("FINALIZER COUNT: " + Utils.LargeNumberString(finalizer.Length));

		//		var markedRoooted = 0;
		//		var markedFinalizer = 0;
		//		for (int i = 0, icnt = instances.Length; i < icnt; ++i)
		//		{
		//			var addr = instances[i];
		//			if (Utils.IsRooted(addr)) ++markedRoooted;
		//			if (Utils.IsFinalizer(addr)) ++markedFinalizer;
		//		}
		//		TestContext.WriteLine("MARKED ROOTED COUNT: " + Utils.LargeNumberString(markedRoooted));
		//		TestContext.WriteLine("MARKED FINALIZER COUNT: " + Utils.LargeNumberString(markedFinalizer));

		//	} // using dump
		//}

		[TestMethod]
		public void TestIndexingBig()
		{
			string error;
			string dumpPath = @"C:\WinDbgStuff\Dumps\Analytics\BigOne\Analytics11_042015_2.Big.dmp";
			var dmp = OpenDump(dumpPath);
			var fileMoniker = new DumpFileMoniker(dumpPath);
			var strIds = new StringIdDct();

			using (dmp)
			{
				var heap = dmp.Heap;
				var runtm = dmp.Runtimes[0];
				ulong[] instances = DumpIndexer.GetHeapAddressesCount(heap);
				string[] typeNames = DumpIndexer.GetTypeNames(heap, out error);
				var rootAddrInfo = ClrtRootInfo.GetRootAddresses(0, runtm, heap, typeNames, strIds, fileMoniker, out error);
				Utils.GetRealAddressesInPlace(rootAddrInfo.Item1);
				Utils.GetRealAddressesInPlace(rootAddrInfo.Item2);

				Assert.IsNull(error);
				Assert.IsTrue(Utils.AreAddressesSorted(rootAddrInfo.Item1));
				Assert.IsTrue(Utils.AreAddressesSorted(rootAddrInfo.Item2));

				var rootAddresses = rootAddrInfo.Item1;

				//Assert.IsTrue(Utils.AreAllInExcept0(rootAddresses, rootAddrInfo.Item1));
				//Assert.IsTrue(Utils.AreAllInExcept0(rootAddresses, rootAddrInfo.Item2));
				Assert.IsTrue(Utils.AreAllDistinct(rootAddresses));
				Assert.IsTrue(Utils.IsSorted(rootAddresses));

				rootAddresses = Utils.GetRealAddressesInPlace(rootAddresses);
				Bitset bitset = new Bitset(instances.Length);
				var rootAddressNdxs = Utils.GetAddressIndices(rootAddresses, instances);

				string path1 = fileMoniker.GetFilePath(0, Constants.MapParentFieldsRootedPostfix);
				string path2 = fileMoniker.GetFilePath(0, Constants.MapFieldParentsRootedPostfix);

				//bool result = References.CreateReferences2(0, heap, rootAddrInfo.Item1, instances, bitset, fileMoniker, null, out error);
				//Assert.IsTrue(result);
				Assert.IsNull(error);
			} // using dump
		}

		[TestMethod]
		public void TestRootInfo()
		{
			string error;
			string dumpPath = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.dmp";
			var dmp = OpenDump(dumpPath);

			using (dmp)
			{
				var heap = dmp.Heap;
				var runtm = dmp.Runtimes[0];
				ulong[] instances = DumpIndexer.GetHeapAddressesCount(heap);
				var result = ClrtRootInfo.DumpRootInfo(dumpPath, runtm, heap, instances, out error);
				Assert.IsTrue(result);
				Assert.IsNull(error);
			} // using dump
		}

		private bool DumpReferences(string path, SortedDictionary<ulong, List<ulong>> refs, ulong[] instances,
			out string error)
		{
			error = null;
			BinaryWriter bw = null;
			try
			{
				bw = new BinaryWriter(File.Open(path, FileMode.Create));
				bw.Write(refs.Count);
				foreach (var kv in refs)
				{
					var ndx = Array.BinarySearch(instances, kv.Key);
					Debug.Assert(ndx >= 0);
					var lst = kv.Value;
					bw.Write(ndx);
					bw.Write(lst.Count);
					for (int i = 0, icnt = lst.Count; i < icnt; ++i)
					{
						ndx = Array.BinarySearch(instances, lst[i]);
						Debug.Assert(ndx >= 0);
						bw.Write(ndx);
					}
				}
				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				bw?.Close();
			}
		}

		#endregion roots/instances/references

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

		#region type references

		[TestMethod]
		public void TestTypeReferences()
		{
			string error;
			string dumpPath = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.dmp";
			//string typeName = "System.String";
			string typeName = "System.Collections.Concurrent.ConcurrentDictionary<";

			var dmp = OpenDump(dumpPath);

			using (dmp)
			{
				var heap = dmp.Heap;
				var segs = heap.Segments;
				var fieldAddrOffsetList = new List<KeyValuePair<ulong, int>>(64);
				for (int i = 0, icnt = segs.Count; i < icnt; ++i)
				{
					var seg = segs[i];
					ulong addr = seg.FirstObject;
					while (addr != 0ul)
					{
						var clrType = heap.GetObjectType(addr);
						if (clrType == null) goto NEXT_OBJECT;
						if (clrType.Name.StartsWith(typeName,StringComparison.Ordinal))
						{
							fieldAddrOffsetList.Clear();
							clrType.EnumerateRefsOfObjectCarefully(addr, (address, off) =>
							{
								fieldAddrOffsetList.Add(new KeyValuePair<ulong, int>(address, off));
							});
						}

						int fieldCount = clrType.Fields.Count;

						NEXT_OBJECT:
						addr = seg.NextObject(addr);
					}
				}
			} // using dump
		}

		[TestMethod]
		public void TestArrayReferences()
		{
			string error;
			string dumpPath = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.dmp";
			//string typeName = "System.String";
			string typeName = "System.Collections.Concurrent.ConcurrentDictionary<";

			var dmp = OpenDump(dumpPath);

			using (dmp)
			{
				var heap = dmp.Heap;
				var segs = heap.Segments;
				var fieldAddrOffsetList = new List<KeyValuePair<ulong, int>>(64);
				for (int i = 0, icnt = segs.Count; i < icnt; ++i)
				{
					var seg = segs[i];
					ulong addr = seg.FirstObject;
					while (addr != 0ul)
					{
						var clrType = heap.GetObjectType(addr);
						if (clrType == null || !clrType.IsArray) goto NEXT_OBJECT;
						fieldAddrOffsetList.Clear();
						clrType.EnumerateRefsOfObjectCarefully(addr, (address, off) =>
						{
							fieldAddrOffsetList.Add(new KeyValuePair<ulong, int>(address, off));
						});

						NEXT_OBJECT:
						addr = seg.NextObject(addr);
					}
				}
			} // using dump
		}

		[TestMethod]
		public void TestEnumerateTypeReferences()
		{
			string error;
			string dumpPath = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.dmp";
			//ulong addr = 0x00000000800a8d48;
			ulong addr = 0x00000000800a8c30;

			var dmp = OpenDump(dumpPath);

			using (dmp)
			{
				var heap = dmp.Heap;
				var segs = heap.Segments;
				var fieldAddrOffsetList = new List<KeyValuePair<ulong, int>>(64);
				fieldAddrOffsetList.Clear();
				var clrType = heap.GetObjectType(addr);
				Assert.IsNotNull(clrType);
				clrType.EnumerateRefsOfObjectCarefully(addr, (address, off) =>
				{
					fieldAddrOffsetList.Add(new KeyValuePair<ulong, int>(address, off));
				});

				int fieldCount = clrType.Fields.Count;

			} // using dump
		}

		#endregion type references

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

		#region template

		[TestMethod]
		public void TestTemplate()
		{
			var dmp = OpenDump();
			using (dmp)
			{
				var heap = dmp.Heap;

			}
		}

		#endregion template
	}
}
