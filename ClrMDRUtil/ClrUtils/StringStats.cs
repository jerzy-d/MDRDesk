using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class StringStats
	{
		private string[] _strings;
		public string[] Strings => _strings;
		private uint[] _sizes;
		public uint[] Sizes => _sizes;
		private int[] _counts;
		public int[] Counts => _counts;
		public int Count => _counts.Length;

		private ulong[][] _adddresses;
		//private long[] _addroffsets;

		private long _totalSize;
		public long TotalSize => _totalSize;
		private long _totalUniqueSize;
		public long TotalUniqueSize => _totalUniqueSize;
		private int _totalCount;
		public int TotalCount => _totalCount;

		private StringStats(SortedDictionary<string, KeyValuePair<uint, List<ulong>>> infoDct)
		{
			int cnt = infoDct.Count;
			_strings = new string[cnt];
			_sizes = new uint[cnt];
			_counts = new int[cnt];
			_adddresses = new ulong[cnt][];
			_totalSize = 0L;
			_totalUniqueSize = 0L;
			_totalCount = 0;

			int ndx = 0;
			foreach (var kv in infoDct)
			{
				int strCnt = kv.Value.Value.Count;
				uint sz = kv.Value.Key;
				_strings[ndx] = kv.Key;
				_counts[ndx] = strCnt;
				_totalCount += strCnt;
				_sizes[ndx] = sz;
				_totalSize += (long)sz * (long)strCnt;
				_totalUniqueSize += (long)sz;
				_adddresses[ndx] = kv.Value.Value.ToArray();
				++ndx;
			}
		}

		private StringStats()
		{

		}

		/// <summary>
		/// TODO JRD -- switch to memory mapped file
		/// </summary>
		/// <param name="ndx"></param>
		/// <returns></returns>
		public ulong[] GetStringAddressesAtIndex(int ndx)
		{
			return _adddresses[ndx];
		}

        public long GetSizeOfStringsWithPrefix(string str)
		{
			Debug.Assert(str.Length > 1 && str[str.Length - 1] == Constants.FancyKleeneStar);
			str = str.Substring(0, str.Length - 1);
			var ndx = Array.BinarySearch(_strings, str, StringComparer.Ordinal);
			if (ndx < 0) ndx = ~ndx;
			var list = new List<ulong>(4 * 1024);
			long totalSize = 0L;
			for (int i = ndx, icnt = _strings.Length; i < icnt; ++i)
			{
				if (_strings[i].StartsWith(str))
					totalSize += _sizes[i] * _counts[i];
				else
					break;
			}
			return totalSize;
		}

		public ulong[] GetStringAddresses(string str, out string error)
		{
			error = null;
#if DEBUG
			KeyValuePair<string, string>[] bad;
			var result = Utils.IsSorted(_strings, out bad);
			if (!result)
				Debug.Assert(false, "StringStats _strings are not sorted: [" + bad.Length + "]");
#endif
			if (str == null)
			{
				error = Utils.GetErrorString("Get Specific String Addresses", "Search failed, string argument is null.",
					"The string to search for cannot be empty");
				return null;
			}

			bool matchPrefix = false;
			if (str.Length > 2 && str[str.Length - 1] == Constants.FancyKleeneStar)
			{
				matchPrefix = true;
				str = str.Substring(0, str.Length - 1);
			}
			var ndx = Array.BinarySearch(_strings, str, StringComparer.Ordinal);
			if (!matchPrefix)
			{
				if (ndx < 0)
				{
					error = Utils.GetErrorString("Get Specific String Addresses", "Search failed.",
						"Cannot find string: " + str + ".");
					return null;
				}
				return GetStringAddressesAtIndex(ndx);
			}
			if (ndx >= 0)
			{
				var lst = new List<ulong>(4 * 1024);
				lst.AddRange(GetStringAddressesAtIndex(ndx));
				for (int i = ndx + 1, icnt = _strings.Length; i < icnt; ++i)
				{
					if (_strings[i].StartsWith(str))
						lst.AddRange(GetStringAddressesAtIndex(i));
					else
						break;
				}
				lst.Sort();
				return lst.ToArray();
			}

			ndx = ~ndx;
			var list = new List<ulong>(4 * 1024);
			for (int i = ndx, icnt = _strings.Length; i < icnt; ++i)
			{
				if (_strings[i].StartsWith(str))
					list.AddRange(GetStringAddressesAtIndex(i));
				else
					break;
			}
			if (list.Count > 0)
			{
				list.Sort();
				return list.ToArray();
			}
			return Utils.EmptyArray<ulong>.Value;
		}


		public static StringStats GetStringStats(SortedDictionary<string, KeyValuePair<uint, List<ulong>>> infoDct, string strFilePath, string dataFilePath, out string error)
		{
			error = null;
			var stats = new StringStats(infoDct);

			DumpStringsInfo(strFilePath, dataFilePath, infoDct, stats._totalCount, stats._totalSize, stats._totalUniqueSize, out error);

			return stats;
		}

		public ListingInfo GetGridData(int minReferenceCount, out string error)
		{
			error = null;
			try
			{
				string[] dataAry;
				listing<string>[] itemAry;
				if (minReferenceCount < 1) minReferenceCount = 1;
				if (minReferenceCount < 2)
				{
					dataAry = new string[_strings.Length * 4];
					itemAry = new listing<string>[dataAry.Length / 4];
					int dataNdx = 0;
					for (int i = 0, icnt = _strings.Length; i < icnt; ++i)
					{
						var count = _counts[i];
						var countStr = Utils.LargeNumberString(count);
						var size = _sizes[i];
						var sizeStr = Utils.LargeNumberString(size);
						long totSize = (long)count * (long)size;
						var totSizeStr = Utils.LargeNumberString(totSize);
						var str = ReportFile.GetReportLineString(_strings[i]);

						itemAry[i] = new listing<string>(dataAry, dataNdx, 4);
						dataAry[dataNdx++] = countStr;
						dataAry[dataNdx++] = sizeStr;
						dataAry[dataNdx++] = totSizeStr;
						dataAry[dataNdx++] = str;
					}
				}
				else
				{
					List<string> lst = new List<string>(_strings.Length);
					for (int i = 0, icnt = _strings.Length; i < icnt; ++i)
					{
						var count = _counts[i];
						if (count < minReferenceCount) continue;
						var countStr = Utils.LargeNumberString(count);
						var size = _sizes[i];
						var sizeStr = Utils.LargeNumberString(size);
						long totSize = (long)count * (long)size;
						var totSizeStr = Utils.LargeNumberString(totSize);
						var str = ReportFile.GetReportLineString(_strings[i]);
						lst.Add(countStr);
						lst.Add(sizeStr);
						lst.Add(totSizeStr);
						lst.Add(str);
					}
					dataAry = lst.ToArray();
					itemAry = new listing<string>[dataAry.Length / 4];
					int dataNdx = 0;
					for (int i = 0, icnt = itemAry.Length; i < icnt; ++i)
					{
						itemAry[i] = new listing<string>(dataAry, dataNdx, 4);
						dataNdx += 4;
					}
				}

				StringBuilder sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
				sb.Append(ReportFile.DescrPrefix);
				sb.Append("String instance count: ")
					.Append(Utils.LargeNumberString(_totalCount))
					.Append(",   unique string count: ")
					.Append(Utils.LargeNumberString(_strings.Length))
					.AppendLine();
				sb.Append(ReportFile.DescrPrefix);
				sb.Append("Total size: ")
					.Append(Utils.LargeNumberString(_totalSize))
					.Append(",  total unique size: ")
					.Append(Utils.LargeNumberString(_totalUniqueSize))
					.AppendLine();
				sb.Append(ReportFile.DescrPrefix);
				sb.Append("Possible memory savings: ")
				   .Append(Utils.LargeNumberString(_totalSize - _totalUniqueSize))
				   .AppendLine();
				sb.Append(ReportFile.DescrPrefix);
				sb.Append("LISTING CONTAINS STRINGS WITH REFERENCE COUNT AT LEAST " + minReferenceCount)
				   .Append(", DISPLAYED COUNT: ").Append(Utils.LargeNumberString(itemAry.Length))
				   .AppendLine();

				ColumnInfo[] columns =
				{
					new ColumnInfo("Count", ReportFile.ColumnType.Int32, 200,1,true),
					new ColumnInfo("Size", ReportFile.ColumnType.Int32, 200,2,true),
					new ColumnInfo("Total Size", ReportFile.ColumnType.Int64, 200,3,true),
					new ColumnInfo("String Content", ReportFile.ColumnType.String, 600,4,true),
				};
				return new ListingInfo(null, itemAry, columns, StringBuilderCache.GetStringAndRelease(sb));
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public ListingInfo GetGridData(int minReferenceCount, ClrtSegment[] segments, out string error)
		{
			const int COLUMN_COUNT = 8;
			error = null;
			try
			{
				string[] dataAry;
				listing<string>[] itemAry;
				if (minReferenceCount < 1) minReferenceCount = 1;
				if (minReferenceCount < 2)
				{
					dataAry = new string[_strings.Length * COLUMN_COUNT];
					itemAry = new listing<string>[dataAry.Length / COLUMN_COUNT];
					int dataNdx = 0;
					for (int i = 0, icnt = _strings.Length; i < icnt; ++i)
					{
						var count = _counts[i];
						var countStr = Utils.LargeNumberString(count);
						var size = _sizes[i];
						var sizeStr = Utils.LargeNumberString(size);
						long totSize = (long)count * (long)size;
						var totSizeStr = Utils.LargeNumberString(totSize);
						var str = ReportFile.GetReportLineString(_strings[i]);
						var generations = ClrtSegment.GetGenerationHistogram(segments, _adddresses[i]);

						itemAry[i] = new listing<string>(dataAry, dataNdx, COLUMN_COUNT);
						dataAry[dataNdx++] = countStr;
						dataAry[dataNdx++] = sizeStr;
						dataAry[dataNdx++] = totSizeStr;
						dataAry[dataNdx++] = Utils.LargeNumberString(generations[0]);
						dataAry[dataNdx++] = Utils.LargeNumberString(generations[1]);
						dataAry[dataNdx++] = Utils.LargeNumberString(generations[2]);
						dataAry[dataNdx++] = Utils.LargeNumberString(generations[3]);
						dataAry[dataNdx++] = str;
					}
				}
				else
				{
					List<string> lst = new List<string>(_strings.Length);
					for (int i = 0, icnt = _strings.Length; i < icnt; ++i)
					{
						var count = _counts[i];
						if (count < minReferenceCount) continue;
						var countStr = Utils.LargeNumberString(count);
						var size = _sizes[i];
						var sizeStr = Utils.LargeNumberString(size);
						long totSize = (long)count * (long)size;
						var totSizeStr = Utils.LargeNumberString(totSize);
						var str = ReportFile.GetReportLineString(_strings[i]);
						var generations = ClrtSegment.GetGenerationHistogram(segments, _adddresses[i]);
						lst.Add(countStr);
						lst.Add(sizeStr);
						lst.Add(totSizeStr);
						lst.Add(Utils.LargeNumberString(generations[0]));
						lst.Add(Utils.LargeNumberString(generations[1]));
						lst.Add(Utils.LargeNumberString(generations[2]));
						lst.Add(Utils.LargeNumberString(generations[3]));
						lst.Add(str);
					}
					dataAry = lst.ToArray();
					itemAry = new listing<string>[dataAry.Length / COLUMN_COUNT];
					int dataNdx = 0;
					for (int i = 0, icnt = itemAry.Length; i < icnt; ++i)
					{
						itemAry[i] = new listing<string>(dataAry, dataNdx, COLUMN_COUNT);
						dataNdx += COLUMN_COUNT;
					}
				}

				StringBuilder sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
				sb.Append(ReportFile.DescrPrefix);
				sb.Append("String instance count: ")
					.Append(Utils.LargeNumberString(_totalCount))
					.Append(",   unique string count: ")
					.Append(Utils.LargeNumberString(_strings.Length))
					.AppendLine();
				sb.Append(ReportFile.DescrPrefix);
				sb.Append("Total size: ")
					.Append(Utils.LargeNumberString(_totalSize))
					.Append(",  total unique size: ")
					.Append(Utils.LargeNumberString(_totalUniqueSize))
					.AppendLine();
				sb.Append(ReportFile.DescrPrefix);
				sb.Append("Possible memory savings: ")
				   .Append(Utils.LargeNumberString(_totalSize - _totalUniqueSize))
				   .AppendLine();
				sb.Append(ReportFile.DescrPrefix);
				sb.Append("LISTING CONTAINS STRINGS WITH REFERENCE COUNT AT LEAST " + minReferenceCount)
				   .Append(", DISPLAYED COUNT: ").Append(Utils.LargeNumberString(itemAry.Length))
				   .AppendLine();

				ColumnInfo[] columns =
				{
					new ColumnInfo("Count", ReportFile.ColumnType.Int32, 100,1,true),
					new ColumnInfo("Size", ReportFile.ColumnType.Int32, 100,2,true),
					new ColumnInfo("Total Size", ReportFile.ColumnType.Int64, 100,3,true),

					new ColumnInfo("Gen 0", ReportFile.ColumnType.Int64, 100,4,true),
					new ColumnInfo("Gen 1", ReportFile.ColumnType.Int64, 100,5,true),
					new ColumnInfo("Gen 2", ReportFile.ColumnType.Int64, 100,6,true),
					new ColumnInfo("LOH", ReportFile.ColumnType.Int64, 100,7,true),

					new ColumnInfo("String Content", ReportFile.ColumnType.String, 600,8,true),
				};
				return new ListingInfo(null, itemAry, columns, StringBuilderCache.GetStringAndRelease(sb));
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		/// <summary>
		/// We are persisting string statistics (System.String) when they are asked for,
		/// so second request might be served faster.
		/// </summary>
		/// <param name="runtimeIndex">Mostly we have just one runtime [0].</param>
		/// <param name="dumpPath">Process dump file.</param>
		/// <returns>True if we have files with string information.</returns>
		public static bool StringStatsFilesExist(int runtimeIndex, string dumpPath)
		{
			var strDatPath = DumpFileMoniker.GetRuntimeFilePath(runtimeIndex, dumpPath, Constants.MapDumpStringsInfoPostfix);
			if (!File.Exists(strDatPath)) return false;
			return true;
		}


		public static StringStats GetStringsInfoFromFiles(int runtimeIndex, string dumpPath, out string error)
		{
			error = null;
			string[] strings = null;
			StringStats strStats = null;
			BinaryReader br = null;

			try
			{
				string dataPath = DumpFileMoniker.GetRuntimeFilePath(runtimeIndex, dumpPath, Constants.MapDumpStringsInfoPostfix);
				br = new BinaryReader(File.Open(dataPath, FileMode.Open));
				int totalStringCount = br.ReadInt32();
				long totalStringSize = br.ReadInt64();
				long totalUniqueSize = br.ReadInt64();
				long strOffsetOffset = br.ReadInt64();
				int uniqueStrCount = br.ReadInt32();

				uint[] sizes = new uint[uniqueStrCount];
				int[] counts = new int[uniqueStrCount];
				ulong[][] adddresses = new ulong[uniqueStrCount][];

				for (int i = 0; i < uniqueStrCount; ++i)
				{
					int strInstCount = br.ReadInt32();
					uint strSize = br.ReadUInt32();
					counts[i] = strInstCount;
					sizes[i] = strSize;
					adddresses[i] = new ulong[strInstCount];
					for (int j = 0; j < strInstCount; ++j)
					{
						adddresses[i][j] = br.ReadUInt64();
					}
				}
				strings = new string[uniqueStrCount];
				long[] addroffs = new long[uniqueStrCount];
				for (int i = 0; i < uniqueStrCount; ++i)
				{
					addroffs[i] = br.ReadInt64();
					strings[i] = br.ReadString();
				}
				br.Close();
				br = null;

				strStats = new StringStats()
				{
					_strings = strings,
					_sizes = sizes,
					_counts = counts,
					_adddresses = adddresses,
					_totalSize = totalStringSize,
					_totalUniqueSize = totalUniqueSize,
					_totalCount = totalStringCount
				};
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				br?.Close();
			}

			return strStats;
		}


		public static bool DumpStringsInfo(string strPath, string dataPath, SortedDictionary<string, KeyValuePair<uint, List<ulong>>> dct, int totalStringCount, long totalStringSize, long totalUniqueSize, out string error)
		{
			error = null;
			BinaryWriter bw = null;

			try
			{
				bw = new BinaryWriter(File.Open(dataPath, FileMode.Create));
				bw.Write(totalStringCount);
				bw.Write(totalStringSize);
				bw.Write(totalUniqueSize);
				int strOffsetOffset = (int)bw.Seek(0, SeekOrigin.Current);
				bw.Write(0L);

				List<long> offsetLst = new List<long>(dct.Count);
				bw.Write(dct.Count); // unique string count
				foreach (var kv in dct)
				{
					long curOffset = bw.Seek(0, SeekOrigin.Current);
					offsetLst.Add(curOffset);
					List<ulong> lst = kv.Value.Value;
					bw.Write(lst.Count); // instances/addresses count
					bw.Write(kv.Value.Key); // dump string size, not its length
					for (int i = 0, icnt = lst.Count; i < icnt; ++i)
					{
						bw.Write(lst[i]);
					}
				}
				long strOffset = bw.Seek(0, SeekOrigin.Current);
				int offNdx = 0;
				foreach (var kv in dct)
				{
					bw.Write(offsetLst[offNdx++]); // string addresses offset
					bw.Write(kv.Key);
				}

				long off = bw.Seek(strOffsetOffset, SeekOrigin.Begin);
				bw.Write((long)strOffset);

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
	}

	public struct StringStatsDispEntry
	{
		public string StrContent { get; private set; }
		public int Count { get; private set; }
		public int Size { get; private set; }
		public long TotSize { get; private set; }

		public StringStatsDispEntry(string content, int count, int size, long totSize)
		{
			StrContent = content;
			Count = count;
			Size = size;
			TotSize = totSize;
		}

		public static bool WriteShortReport(StringStatsDispEntry[] entries, string reportPath, string title, int topItemCount, string[] sorts, string footer, out string error)
		{
			error = null;
			StreamWriter wr = null;
			try
			{
				wr = new StreamWriter(reportPath);

				var entriesCpy = new StringStatsDispEntry[entries.Length];
				Array.Copy(entries, entriesCpy, entries.Length);

				wr.WriteLine(title);
				if (sorts.Contains("Count"))
				{
					wr.WriteLine();
					wr.WriteLine(topItemCount + " top items by count, columns: [Count] | Size | Total Size | String Content");
					Sort(entriesCpy, "Count");
					for (int i = 0; i < topItemCount; ++i)
					{
						wr.Write(Utils.SizeStringHeader(entriesCpy[i].Count));
						wr.Write(Utils.SizeString(entriesCpy[i].Size));
						wr.Write("   " + Utils.SizeString(entriesCpy[i].TotSize) + "   ");
						wr.WriteLine(ReportFile.GetReportLineString(entriesCpy[i].StrContent));
					}
				}
				if (sorts.Contains("TotalSize"))
				{
					wr.WriteLine();
					wr.WriteLine(topItemCount + " top items by total size, columns: Count | Size | [Total Size] | String Content");
					Sort(entriesCpy, "TotalSize");
					for (int i = 0; i < topItemCount; ++i)
					{
						wr.Write(Utils.SizeString(entriesCpy[i].Count) + "   ");
						wr.Write(Utils.SizeString(entriesCpy[i].Size));
						wr.Write("   " + Utils.SizeStringHeader(entriesCpy[i].TotSize) + " ");
						wr.WriteLine(ReportFile.GetReportLineString(entriesCpy[i].StrContent));
					}
				}
				if (sorts.Contains("Size"))
				{
					wr.WriteLine();
					wr.WriteLine(topItemCount + " top items by individual sizes, columns: Count | [Size] | Total Size | String Content");
					Sort(entriesCpy, "Size");
					for (int i = 0; i < topItemCount; ++i)
					{
						wr.Write(Utils.SizeString(entriesCpy[i].Count) + "   ");
						wr.Write(Utils.SizeStringHeader(entriesCpy[i].Size));
						wr.Write(" " + Utils.SizeString(entriesCpy[i].TotSize) + "   ");
						wr.WriteLine(ReportFile.GetReportLineString(entriesCpy[i].StrContent));
					}
				}

				if (string.IsNullOrWhiteSpace(footer)) return true;

				wr.WriteLine();
				wr.WriteLine(footer);

				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				wr?.Close();
			}
		}

		public static void Sort(StringStatsDispEntry[] entries, string sortBy)
		{
			switch (sortBy)
			{
				case "String":
					Array.Sort(entries, new StringStatsStrCmp());
					break;
				case "Count":
					Array.Sort(entries, new StringStatsCountCmp());
					break;
				case "Size":
					Array.Sort(entries, new StringStatsSizeCmp());
					break;
				case "TotalSize":
					Array.Sort(entries, new StringStatsTotSizeCmp());
					break;
			}
		}
	}

	public class StringStatsStrCmp : IComparer<StringStatsDispEntry>
	{
		public int Compare(StringStatsDispEntry a, StringStatsDispEntry b)
		{
			return string.Compare(a.StrContent, b.StrContent, StringComparison.Ordinal);
		}
	}

	public class StringStatsCountCmp : IComparer<StringStatsDispEntry>
	{
		public int Compare(StringStatsDispEntry a, StringStatsDispEntry b)
		{
			return a.Count < b.Count ? 1 : (a.Count > b.Count ? -1 : 0);
		}
	}

	public class StringStatsSizeCmp : IComparer<StringStatsDispEntry>
	{
		public int Compare(StringStatsDispEntry a, StringStatsDispEntry b)
		{
			return a.Size < b.Size ? 1 : (a.Size > b.Size ? -1 : 0);
		}
	}

	public class StringStatsTotSizeCmp : IComparer<StringStatsDispEntry>
	{
		public int Compare(StringStatsDispEntry a, StringStatsDispEntry b)
		{
			return a.TotSize < b.TotSize ? 1 : (a.TotSize > b.TotSize ? -1 : 0);
		}
	}
}
