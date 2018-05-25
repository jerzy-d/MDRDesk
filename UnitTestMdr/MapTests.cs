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
			int unrootedCount;
			var addresses = index.GetTypeInstances(typeId,out unrootedCount);
		}

		#endregion (new) types and instances

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

        // TODO JRD - will be usefull ?
        //[TestMethod]
        //public void TestGetTypeGroupedContent()
        //{
        //	const string str = "Eze.Server.Common.Pulse.Common.Types.CachedValue+DefaultOnly<System.Decimal>";
        //	SortedDictionary<string, int> dct = new SortedDictionary<string, int>(StringComparer.Ordinal);
        //	using (
        //		var map =
        //			OpenMap(
        //				@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\RealPositionCmp\Eze.Analytics.Svc.RPSMALL0_160913_153220.map")
        //	)
        //	{
        //		try
        //		{
        //			ClrType clrType = null;
        //			ClrInstanceField bitwiseCurrentPositionToggleState = null;
        //			ClrInstanceField indexIntoPositionIndexToFieldNumberConversionSet = null;
        //			int typeId = map.GetTypeId(str);
        //			ulong[] addresses = map.GetTypeRealAddresses(typeId);
        //			var heap = map.Dump.GetFreshHeap();
        //			for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
        //			{
        //				var addr = addresses[i];
        //				if (clrType == null)
        //				{
        //					clrType = heap.GetObjectType(addr);
        //					bitwiseCurrentPositionToggleState = clrType.GetFieldByName("bitwiseCurrentPositionToggleState");
        //					indexIntoPositionIndexToFieldNumberConversionSet =
        //						clrType.GetFieldByName("indexIntoPositionIndexToFieldNumberConversionSet");
        //					Assert.IsNotNull(bitwiseCurrentPositionToggleState.Type);
        //					Assert.IsNotNull(indexIntoPositionIndexToFieldNumberConversionSet.Type);
        //				}

        //				object fld1ValObj = bitwiseCurrentPositionToggleState.GetValue(addr);
        //				string fld1Val = ValueExtractor.GetPrimitiveValue(fld1ValObj, bitwiseCurrentPositionToggleState.Type);
        //				object fld2ValObj = indexIntoPositionIndexToFieldNumberConversionSet.GetValue(addr);
        //				string fld2Val = ValueExtractor.GetPrimitiveValue(fld2ValObj,
        //					indexIntoPositionIndexToFieldNumberConversionSet.Type);
        //				var key = fld1Val + "_" + fld2Val;
        //				int count;
        //				if (dct.TryGetValue(key, out count))
        //				{
        //					dct[key] = count + 1;
        //				}
        //				else
        //				{
        //					dct.Add(key, 1);
        //				}
        //			}

        //		}
        //		catch (Exception ex)
        //		{
        //			Assert.IsTrue(false, ex.ToString());
        //		}
        //	}
        //}

        // TODO JRD - will be usefull ?
        //[TestMethod]
        //public void TestGetTypeGroupedContent2()
        //{
        //	const string str = "ECS.Common.Collections.Common.EzeBitVector";
        //	SortedDictionary<string, int> dct = new SortedDictionary<string, int>(StringComparer.Ordinal);
        //	int nullFieldCount = 0;
        //	StringBuilder sb = new StringBuilder(256);
        //	using (var index = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Lou\Analytics3.dmp.map"))
        //	{
        //		try
        //		{
        //			ClrType clrType = null;
        //			ClrType aryType = null;
        //			ClrInstanceField bits = null;
        //			ulong aryAddr = 0;
        //			int typeId = index.GetTypeId(str);
        //			ulong[] addresses = index.GetTypeRealAddresses(typeId);
        //			var heap = index.Dump.GetFreshHeap();
        //			for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
        //			{
        //				var addr = addresses[i];
        //				if (clrType == null)
        //				{
        //					clrType = heap.GetObjectType(addr);
        //					bits = clrType.GetFieldByName("bits");
        //					Assert.IsNotNull(bits.Type);
        //					Assert.IsNotNull(bits.Type);
        //					aryType = bits.Type;
        //				}
        //				var aryAddrObj = bits.GetValue(addr);
        //				if (aryAddrObj == null)
        //				{
        //					++nullFieldCount;
        //					continue;
        //				}
        //				aryAddr = (ulong) aryAddrObj;
        //				int aryCount = aryType.GetArrayLength(aryAddr);
        //				sb.Clear();
        //				sb.Append(aryCount.ToString()).Append("_");
        //				for (int j = 0; j < aryCount; ++j)
        //				{
        //					var elemVal = aryType.GetArrayElementValue(aryAddr, j);
        //					if (elemVal == null)
        //						continue;
        //					var valStr = elemVal.ToString();
        //					sb.Append(valStr).Append("_");
        //				}
        //				if (sb.Length > 0) sb.Remove(sb.Length - 1, 1);
        //				var key = sb.ToString();
        //				int dctCnt;
        //				if (dct.TryGetValue(key, out dctCnt))
        //				{
        //					dct[key] = dctCnt + 1;
        //				}
        //				else
        //				{
        //					dct.Add(key, 1);
        //				}
        //			}

        //			StreamWriter sw = null;
        //			try
        //			{

        //				sw = new StreamWriter(index.AdhocFolder + Path.DirectorySeparatorChar + "EzeBitVector.txt");
        //				sw.WriteLine("#### Total EzeBitVector Instance Count: " + Utils.SizeString(addresses.Length));
        //				sw.WriteLine("#### Total EzeBitVector Unique Count: " + Utils.SizeString(dct.Count));
        //				sw.WriteLine("#### Columns: duplicate count, ulong[] size, vector content");
        //				foreach (var kv in dct)
        //				{
        //					var pos = kv.Key.IndexOf('_');
        //					int aryCount = Int32.Parse(kv.Key.Substring(0, pos));
        //					sw.WriteLine(Utils.CountStringHeader(kv.Value) + Utils.CountStringHeader(aryCount) + kv.Key.Substring(pos + 1));
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

        //		}
        //		catch (Exception ex)
        //		{
        //			Assert.IsTrue(false, ex.ToString());
        //		}
        //	}
        //}

  //      [TestMethod]
		//public void TestGetTypeSizeDetails()
		//{
		//	string reportPath = null;
		//	StreamWriter sw = null;
		//	const string typeName = "ECS.Common.HierarchyCache.Structure.RealPosition";
		//	string error = null;
  //          Tuple<ulong, ulong[], SortedDictionary<string, KeyValuePair<int, ulong>>, SortedDictionary<string, List<int>>, ValueTuple<int, ulong, string>[]> result = null;

		//	try
		//	{
		//		using (
		//			var map =
		//				OpenMap(
		//					@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Memory.Usage.OPAM.971\RealPositionCmp\Eze.Analytics.Svc.RPSMALL0_160913_153220.map")
		//			)
		//		{
		//			int typeId = map.GetTypeId(typeName);
		//			result = map.GetTypeSizeDetails(typeId, out error);
		//			reportPath = map.AdhocFolder + Path.DirectorySeparatorChar + Utils.BaseTypeName(typeName) + ".SIZE.DETAILS.txt";
		//		}

		//		Assert.IsNotNull(result,error);

		//		sw = new StreamWriter(reportPath);
		//		sw.WriteLine("### TOTAL SIZE: " + Utils.LargeNumberString(result.Item1));
		//		sw.WriteLine("### INVALID ADDRESSES COUNT: " + Utils.LargeNumberString(result.Item2.Length));
		//		var typeDct = result.Item3;
		//		sw.WriteLine("### TYPE COUNT: " + Utils.LargeNumberString(typeDct.Count));
		//		foreach (var kv in typeDct)
		//		{
		//			var name = kv.Key;
		//			var cnt = kv.Value.Key;
		//			var sz = kv.Value.Value;
		//			sw.WriteLine(Utils.SortableSizeStringHeader(cnt) + Utils.SortableLengthStringHeader(sz) + name);
		//		}
		//		var aryDct = result.Item4;
		//		sw.WriteLine("### ARRAYS AND THEIR COUNTS");
		//		sw.WriteLine("### ARRAY COUNT: " + Utils.LargeNumberString(aryDct.Count));
		//		sw.WriteLine("### Columns: array count, min elem count, max elem count, avg elem count, total elem count, type name");
		//		foreach (var kv in aryDct)
		//		{
		//			var name = kv.Key;
		//			var lst = kv.Value;
		//			var acnt = lst.Count;
		//			var totalElemCount = 0;
		//			var minElemCount = Int32.MaxValue;
		//			var maxElemCount = 0;
		//			var avgElemCount = 0;
		//			for (int i = 0, icnt = lst.Count; i < icnt; ++i)
		//			{
		//				var val = lst[i];
		//				totalElemCount += val;
		//				if (val < minElemCount) minElemCount = val;
		//				if (val > maxElemCount) maxElemCount = val;
		//			}
		//			avgElemCount = (int) Math.Round((double) totalElemCount/(double) acnt);
		//			sw.Write(Utils.CountStringHeader(acnt));
		//			sw.Write(Utils.CountStringHeader(minElemCount));
		//			sw.Write(Utils.CountStringHeader(maxElemCount));
		//			sw.Write(Utils.CountStringHeader(avgElemCount));
		//			sw.Write(Utils.SortableSizeStringHeader(totalElemCount));
		//			sw.WriteLine(name);

		//		}

		//	}
		//	catch (Exception ex)
		//	{
		//		Assert.IsTrue(false, ex.ToString());
		//	}
		//	finally
		//	{
		//		sw?.Close();
		//	}
		//}

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
             {
                BinaryReader offRd = null;
                BinaryReader parRd = null;
                StreamWriter sw = null;

                try
                {
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


        #region open index

		public DumpIndex OpenIndex(string indexPath)
		{
			string error = null;
			var version = Assembly.GetExecutingAssembly().GetName().Version;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var index = DumpIndex.OpenIndexInstanceReferences(version, indexPath, 0, out error);
            bool ok = index != null;
            stopWatch.Stop();
            TestContext.WriteLine(indexPath);
            TestContext.WriteLine((ok?"":"FAILED TO OPEN... ") + "OPENING DURATION: " + Utils.DurationString(stopWatch.Elapsed));
            Assert.IsTrue(ok, error);
			return index;
		}

		#endregion open index
	}
}
