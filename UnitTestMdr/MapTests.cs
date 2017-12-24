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
				ClrElementKind[] elems = map.TypeKinds;
				Assert.IsNull(error, error);
				Assert.IsTrue(sizeInfo.Key.Length == sizeInfo.Value.Length);
				Assert.IsTrue(map.Instances.Length == sizeInfo.Key.Length);
				HashSet<string> set = new HashSet<string>(StringComparer.Ordinal);
				int totalCount = sizeInfo.Key.Length;
				int freeId = map.GetTypeId("Free");
				int equalCnt = 0;
				uint maxDiff = 0;
				ulong maxAddr = 0UL;
				string maxTypeName = string.Empty;
				ClrElementKind maxElem = ClrElementKind.Unknown;
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
					ClrElementKind elem = elems[typeId];
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
			}
		}

		#endregion instance sizes

		[TestMethod]
		public void TestTypeNamespaceDisplay()
		{
			using (var map = OpenMap(@"D:\Jerzy\WinDbgStuff\Dumps\DumpTest\DumpTest.exe_160820_073947.map"))
			{
				var dispInfo = map.GetNamespaceDisplay();
			}
		}


		#region Types

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
					int unrootedCount;
					var typeInstances = map.GetTypeInstances(typeId,out unrootedCount);
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
			var map = OpenMap(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\ConvergEx\Analytics.map");
			using (map)
			{
				int totalCount, unrootedCount;
				var result = map.GetTypeWithPrefixAddresses(typeName, true, out totalCount, out unrootedCount);

				var id = map.GetTypeId(typeName);
				var tname = map.GetTypeName(id);
				Debug.Assert(tname == typeName);
				var addresses = map.GetTypeRealAddresses(id);
			}

		}

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

		[TestMethod]
		public void TestGetTypeGroupedContent()
		{
			const string str = "Eze.Server.Common.Pulse.Common.Types.CachedValue+DefaultOnly<System.Decimal>";
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
			SortedDictionary<string, int> dct = new SortedDictionary<string, int>(StringComparer.Ordinal);
			int nullFieldCount = 0;
			StringBuilder sb = new StringBuilder(256);
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
		public void TestGetTypeSizeDetails()
		{
			string reportPath = null;
			StreamWriter sw = null;
			const string typeName = "ECS.Common.HierarchyCache.Structure.RealPosition";
			string error = null;
            Tuple<ulong, ulong[], SortedDictionary<string, KeyValuePair<int, ulong>>, SortedDictionary<string, List<int>>, ValueTuple<int, ulong, string>[]> result = null;

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
