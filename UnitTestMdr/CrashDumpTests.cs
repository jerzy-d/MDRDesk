using System;
using System.Text;
using System.Collections.Generic;
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
			ulong dctAddr = 0x0002189f5b2ec0;
			var dmp = OpenDump(1);
			using (dmp)
			{
				var heap = dmp.Heap;
				var clrType = heap.GetObjectType(dctAddr);
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
			ulong dctAddr = 0x0001e7e5273000;
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



		#region open dump

		public static ClrtDump OpenDump(int indexNdx = 0)
		{
			string error;
			var path = Setup.GetRecentDumpPath(indexNdx);
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
