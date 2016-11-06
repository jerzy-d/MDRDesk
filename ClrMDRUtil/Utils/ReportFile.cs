using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Instrumentation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class ReportFile
	{
		[Flags]
		public enum ColumnType
		{
			Unknown = 0,
			Int32 = 1,
			Int64 = 2,
			UInt32 = 4,
			UInt64 = 8,
			String = 16,
            Address = 32
		}

		public static ListingStrCmp1Asc ListingStrCmp1AscInstance = new ListingStrCmp1Asc();
		public static ListingStrCmp2Asc ListingStrCmp2AscInstance = new ListingStrCmp2Asc();
		public static ListingStrCmp3Asc ListingStrCmp3AscInstance = new ListingStrCmp3Asc();
		public static ListingStrCmp4Asc ListingStrCmp4AscInstance = new ListingStrCmp4Asc();
		public static ListingStrCmp5Asc ListingStrCmp5AscInstance = new ListingStrCmp5Asc();
		public static ListingStrCmp6Asc ListingStrCmp6AscInstance = new ListingStrCmp6Asc();
		public static ListingStrCmp7Asc ListingStrCmp7AscInstance = new ListingStrCmp7Asc();
		public static ListingStrCmp8Asc ListingStrCmp8AscInstance = new ListingStrCmp8Asc();

		public static ListingStrCmp1Desc ListingStrCmp1DescInstance = new ListingStrCmp1Desc();
		public static ListingStrCmp2Desc ListingStrCmp2DescInstance = new ListingStrCmp2Desc();
		public static ListingStrCmp3Desc ListingStrCmp3DescInstance = new ListingStrCmp3Desc();
		public static ListingStrCmp4Desc ListingStrCmp4DescInstance = new ListingStrCmp4Desc();
		public static ListingStrCmp5Desc ListingStrCmp5DescInstance = new ListingStrCmp5Desc();
		public static ListingStrCmp6Desc ListingStrCmp6DescInstance = new ListingStrCmp6Desc();
		public static ListingStrCmp7Desc ListingStrCmp7DescInstance = new ListingStrCmp7Desc();
		public static ListingStrCmp8Desc ListingStrCmp8DescInstance = new ListingStrCmp8Desc();

		public static ListingNumCmp1Asc ListingNumCmp1AscInstance = new ListingNumCmp1Asc();
		public static ListingNumCmp2Asc ListingNumCmp2AscInstance = new ListingNumCmp2Asc();
		public static ListingNumCmp3Asc ListingNumCmp3AscInstance = new ListingNumCmp3Asc();
		public static ListingNumCmp4Asc ListingNumCmp4AscInstance = new ListingNumCmp4Asc();
		public static ListingNumCmp5Asc ListingNumCmp5AscInstance = new ListingNumCmp5Asc();
		public static ListingNumCmp6Asc ListingNumCmp6AscInstance = new ListingNumCmp6Asc();
		public static ListingNumCmp7Asc ListingNumCmp7AscInstance = new ListingNumCmp7Asc();
		public static ListingNumCmp8Asc ListingNumCmp8AscInstance = new ListingNumCmp8Asc();

		public static ListingNumCmp1Desc ListingNumCmp1DescInstance = new ListingNumCmp1Desc();
		public static ListingNumCmp2Desc ListingNumCmp2DescInstance = new ListingNumCmp2Desc();
		public static ListingNumCmp3Desc ListingNumCmp3DescInstance = new ListingNumCmp3Desc();
		public static ListingNumCmp4Desc ListingNumCmp4DescInstance = new ListingNumCmp4Desc();
		public static ListingNumCmp5Desc ListingNumCmp5DescInstance = new ListingNumCmp5Desc();
		public static ListingNumCmp6Desc ListingNumCmp6DescInstance = new ListingNumCmp6Desc();
		public static ListingNumCmp7Desc ListingNumCmp7DescInstance = new ListingNumCmp7Desc();
		public static ListingNumCmp8Desc ListingNumCmp8DescInstance = new ListingNumCmp8Desc();

		public static IComparer<listing<string>> GetComparer(ColumnType colType, int colIndex, bool asc)
		{
			var cmpId = GetComparerId(colType, colIndex, asc);
			IComparer<listing<string>> cmp;
			if (_comparerDct.TryGetValue(cmpId, out cmp))
			{
				return cmp;
			}
			return null;
		}

		public static IComparer<listing<string>> GetComparer(ColumnInfo colInfo)
		{
			var cmpId = GetComparerId(colInfo.ColumnType, colInfo.ColumnIndex, colInfo.Ascending);
			IComparer<listing<string>> cmp;
			if (_comparerDct.TryGetValue(cmpId, out cmp))
			{
				return cmp;
			}
			return null;
		}

		private static int GetComparerId(ColumnType colType, int colIndex, bool asc)
		{
			int mask = asc ? 1 : 2;
			mask <<= 8;
			mask |= colType == ColumnType.String ? 1 : 2;
			mask <<= 8;
			mask |= colIndex;
			return mask;
		}

		private static Dictionary<int, IComparer<listing<string>>> _comparerDct = new Dictionary<int, IComparer<listing<string>>>()
		{
			{ GetComparerId(ColumnType.String,1,true),ListingStrCmp1AscInstance },
			{ GetComparerId(ColumnType.String,2,true),ListingStrCmp2AscInstance },
			{ GetComparerId(ColumnType.String,3,true),ListingStrCmp3AscInstance },
			{ GetComparerId(ColumnType.String,4,true),ListingStrCmp4AscInstance },
			{ GetComparerId(ColumnType.String,5,true),ListingStrCmp5AscInstance },
			{ GetComparerId(ColumnType.String,6,true),ListingStrCmp6AscInstance },
			{ GetComparerId(ColumnType.String,7,true),ListingStrCmp7AscInstance },
			{ GetComparerId(ColumnType.String,8,true),ListingStrCmp8AscInstance },

			{ GetComparerId(ColumnType.String,1,false),ListingStrCmp1DescInstance },
			{ GetComparerId(ColumnType.String,2,false),ListingStrCmp2DescInstance },
			{ GetComparerId(ColumnType.String,3,false),ListingStrCmp3DescInstance },
			{ GetComparerId(ColumnType.String,4,false),ListingStrCmp4DescInstance },
			{ GetComparerId(ColumnType.String,5,false),ListingStrCmp5DescInstance },
			{ GetComparerId(ColumnType.String,6,false),ListingStrCmp6DescInstance },
			{ GetComparerId(ColumnType.String,7,false),ListingStrCmp7DescInstance },
			{ GetComparerId(ColumnType.String,8,false),ListingStrCmp8DescInstance },

			{ GetComparerId(ColumnType.Int32,1,true),ListingNumCmp1AscInstance},
			{ GetComparerId(ColumnType.Int32,2,true),ListingNumCmp2AscInstance},
			{ GetComparerId(ColumnType.Int32,3,true),ListingNumCmp3AscInstance},
			{ GetComparerId(ColumnType.Int32,4,true),ListingNumCmp4AscInstance},
			{ GetComparerId(ColumnType.Int32,5,true),ListingNumCmp5AscInstance},
			{ GetComparerId(ColumnType.Int32,6,true),ListingNumCmp6AscInstance},
			{ GetComparerId(ColumnType.Int32,7,true),ListingNumCmp7AscInstance},
			{ GetComparerId(ColumnType.Int32,8,true),ListingNumCmp8AscInstance},

			{ GetComparerId(ColumnType.Int32,1,false),ListingNumCmp1DescInstance},
			{ GetComparerId(ColumnType.Int32,2,false),ListingNumCmp2DescInstance},
			{ GetComparerId(ColumnType.Int32,3,false),ListingNumCmp3DescInstance},
			{ GetComparerId(ColumnType.Int32,4,false),ListingNumCmp4DescInstance},
			{ GetComparerId(ColumnType.Int32,5,false),ListingNumCmp5DescInstance},
			{ GetComparerId(ColumnType.Int32,6,false),ListingNumCmp6DescInstance},
			{ GetComparerId(ColumnType.Int32,7,false),ListingNumCmp7DescInstance},
			{ GetComparerId(ColumnType.Int32,8,false),ListingNumCmp8DescInstance},
		};

		public static ColumnType GetColumnType(string colType)
		{
			switch (colType.ToUpper())
			{
				case "INT":
				case "INT32":
					return ColumnType.Int32;
				case "LONG":
				case "INT64":
					return ColumnType.Int64;
				case "ULONG":
				case "UINT64":
					return ColumnType.UInt64;
				case "UINT":
				case "UINT32":
					return ColumnType.UInt32;
				case "STRING":
					return ColumnType.String;
				default:
					return ColumnType.Unknown;
			}
		}

		public const string InfoPrefix = "### ";
		public const string ReportPrefix = "### MDRDESK REPORT: ";
		public const string SeparatorPrefix = "### SEPARATOR: ";
		public const string CountPrefix = "### COUNT: ";
		public const string ColumnPrefix = "### COLUMNS: ";
		public const string TitlePrefix = "### TITLE: ";
		public const string DescrPrefix = "#### ";
		public const string ItemSeparator = Constants.HeavyAsteriskPadded;

		public static ListingInfo ReadReportFile(string path, out string title, out string error)
		{
			error = null;
			title = String.Empty;
			StreamReader sr = null;
			StringBuilder sbDescr = StringBuilderCache.Acquire();
			try
			{
				int itemCnt = 0;
				string colSpecs = string.Empty;
				string[] separators = new string[] { Constants.HeavyAsteriskPadded };
				string report = null;
				sr = new StreamReader(path);
				string ln = sr.ReadLine();
				if (!ln.StartsWith(ReportFile.ReportPrefix))
				{
					error = "This is not a mdrdesk report file";
					return null;
				}
				report = ln.Substring(ReportPrefix.Length);
				if (!string.IsNullOrWhiteSpace(report))
					sbDescr.Append("Report: ").Append(report).AppendLine();
				while (ln.StartsWith(InfoPrefix))
				{
					if (ln.StartsWith(CountPrefix))
					{
						var pos = Utils.SkipWhites(ln, CountPrefix.Length);
						itemCnt = Int32.Parse(ln.Substring(pos, ln.Length - pos), NumberStyles.AllowThousands);
						ln = sr.ReadLine();
					}
					else if (ln.StartsWith(ColumnPrefix))
					{
						var pos = Utils.SkipWhites(ln, ColumnPrefix.Length);
						colSpecs = ln.Substring(pos);
						ln = sr.ReadLine();
					}
					else if (ln.StartsWith(TitlePrefix))
					{
						title = ln.Substring(TitlePrefix.Length);
						ln = sr.ReadLine();
					}
					else if (ln.StartsWith(SeparatorPrefix))
					{
						var sep = ln.Substring(SeparatorPrefix.Length);
						separators = new[] { sep };
						ln = sr.ReadLine();
					}
					else
					{
						ln = sr.ReadLine();
					}
				}

				while (ln.StartsWith(DescrPrefix))
				{
					sbDescr.Append(ln.Substring(DescrPrefix.Length)).AppendLine();
					ln = sr.ReadLine();
				}

				ColumnType[] colTypes;
				triple<string, ColumnType, int>[] columns = GetColumnInfo(colSpecs, separators, out colTypes);

				var itemLst = new List<string>(itemCnt * columns.Length);

				while (ln != null)
				{
					if (ln.Length == 0 || ln[0] != '#')
					{
						ParseReportLine(ln, separators, colTypes, itemLst);
					}
					else
					{
						if (ln.StartsWith(DescrPrefix))
						{
							sbDescr.Append(ln.Substring(DescrPrefix.Length)).AppendLine();
						}
						else if (ln.StartsWith(CountPrefix))
						{
							var pos = Utils.SkipWhites(ln, CountPrefix.Length);
							itemCnt = Int32.Parse(ln.Substring(pos, ln.Length - pos), NumberStyles.AllowThousands);
						}
					}
					ln = sr.ReadLine();
				}

				int colCnt = columns.Length;
				Debug.Assert((itemLst.Count % colCnt) == 0);
				var dataAry = itemLst.ToArray();
				var itemAry = new listing<string>[dataAry.Length / colCnt];
				var dataNdx = 0;
				for (int i = 0, icnt = dataAry.Length; i < icnt; i += colCnt)
				{
					itemAry[dataNdx++] = new listing<string>(dataAry, i, colCnt);
				}

				var colInfos = new ColumnInfo[columns.Length];
				for (int i = 0, icnt = colInfos.Length; i < icnt; ++i)
				{
					colInfos[i] = new ColumnInfo(columns[i].First, columns[i].Second, columns[i].Third, i + 1, true);
				}
				Array.Sort(itemAry, ReportFile.GetComparer(colInfos[0]));

				var result = new ListingInfo(null, itemAry, colInfos, sbDescr.ToString(), title);
				return result;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				StringBuilderCache.Release(sbDescr);
				sr?.Close();
			}
		}

		public static void SortListingStringArray(ColumnInfo colInfo, listing<string>[] ary)
		{
			//Array.Sort(ary, ReportFile.GetComparer(colInfo));
		    if (colInfo.Ascending)
		    {
		        switch (colInfo.ColumnType)
		        {
                    case ColumnType.String:
                        var ascCmpStr = new ListingStrCmpAsc(colInfo.ColumnIndex - 1);
                        Array.Sort(ary, ascCmpStr);
                        return;
                    default:
		                var ascCmpNum = new ListingNumCmpAsc(colInfo.ColumnIndex - 1);
                        Array.Sort(ary,ascCmpNum);
                        return;
                }
            }
		    else
		    {
                switch (colInfo.ColumnType)
                {
                    case ColumnType.String:
                        var descCmpStr = new ListingStrCmpDesc(colInfo.ColumnIndex - 1);
                        Array.Sort(ary, descCmpStr);
                        return;
                    default:
                        var descCmpNum = new ListingNumCmpDesc(colInfo.ColumnIndex - 1);
                        Array.Sort(ary, descCmpNum);
                        return;
                }
            }
		}

		public static bool ParseReportLine(string ln, string[] sep, ColumnType[] cols, List<string> lst)
		{
			if (string.IsNullOrWhiteSpace(ln)) return false;
			string[] items = ln.Split(sep, StringSplitOptions.None);
			if (items.Length < cols.Length) return false;
			for (int i = 0, icnt = cols.Length; i < icnt; ++i)
			{
				if (cols[i] == ColumnType.String)
					lst.Add(items[i]);
				else
					lst.Add(items[i].Trim());
			}
			return true;
		}

		public static string GetReportLineString(string str)
		{
			if (str == null) return "{{null string}}";
			if (str.Length == 0) return "{{empty string}}";
			bool allwhite = true;
			bool replaceNewLine = false;
			for (int i = 0, icnt = str.Length; i < icnt; ++i)
			{
				if (str[i] == '\n' || str[i] == '\r')
				{
					replaceNewLine = true;
					continue;
				}
				if (!Char.IsWhiteSpace(str[i]))
				{
					allwhite = false;
					if (replaceNewLine) break;
				}
			}
			if (allwhite)
			{
				return "{{blank string of length " + str.Length + "}}";
			}
			if (replaceNewLine)
			{
				StringBuilder sb = new StringBuilder(str);
				sb.Replace("\r\n", Constants.WindowsNewLine);
				sb.Replace("\n", Constants.UnixNewLine);
				return sb.ToString();
			}
			return str;
		}

		public static string RecoverReportLineString(string str)
		{
			if (Utils.SameStrings(str, "{{null string}}")) return null;
			if (Utils.SameStrings(str, "{{empty string}}")) return string.Empty;
			if (str.StartsWith("{{blank string of length ", StringComparison.Ordinal))
			{
				var prefLen = "{{blank string of length ".Length;
				var end = str.IndexOf("}}", prefLen, StringComparison.Ordinal);
				if (end > 0)
				{
					int cnt;
					Int32.TryParse(str.Substring(prefLen, end - prefLen), out cnt);
					if (cnt > 0)
					{
						var chars = new char[cnt];
						Utils.InitArray(chars, ' ');
						return new string(chars);
					}
				}
				return string.Empty;
			}
			bool replaceNewLine = false;
			if (str.Contains(Constants.WindowsNewLine) || str.Contains(Constants.UnixNewLine))
			{
				StringBuilder sb = new StringBuilder(str);
				sb.Replace(Constants.WindowsNewLine, "\r\n");
				sb.Replace(Constants.UnixNewLine, "\n");
				return sb.ToString();
			}
			return str;
		}

		private static triple<string, ColumnType, int>[] GetColumnInfo(string colSpec, string[] seps, out ColumnType[] colTypes)
		{
			var charSep = new char[] { '|' };
			var cols = colSpec.Split(seps, StringSplitOptions.None);
			var colInfos = new triple<string, ColumnType, int>[cols.Length];
			colTypes = new ColumnType[cols.Length];

			for (int i = 0, icnt = cols.Length; i < icnt; ++i)
			{
				var ary = cols[i].Split(charSep);
				var colType = GetColumnType(ary[1]);
				int width = -1;
				if (ary.Length > 2) // we might have width of column for display
				{
					int w;
					if (Int32.TryParse(ary[2], out w))
					{
						width = w;
					}
				}
				colInfos[i] = new triple<string, ColumnType, int>(ary[0], colType, width <= 0 ? 200 : width);
				colTypes[i] = colType;
			}
			return colInfos;
		}

		//public const string InfoPrefix = "### ";
		//public const string ReportPrefix = "### MDRDESK REPORT: ";
		//public const string SeparatorPrefix = "### SEPARATOR: ";
		//public const string CountPrefix = "### COUNT: ";
		//public const string ColumnPrefix = "### COLUMNS: ";
		//public const string TitlePrefix = "### TITLE: ";
		//public const string DescrPrefix = "#### ";
		//public const string ItemSeparator = Constants.HeavyAsteriskPadded;

		public static bool WriteReport(string path, string report, string title, ColumnInfo[] cols, string[] notes, listing<string>[] lines, out string error)
		{
			error = null;
			StreamWriter sr = null;
			StringBuilder sb = null;
			try
			{
				sr = new StreamWriter(path);
				sr.WriteLine(ReportPrefix + report ?? string.Empty);
				sr.WriteLine(TitlePrefix + title ?? string.Empty);
				sr.WriteLine(SeparatorPrefix + Constants.ReportSeparator);
				sr.WriteLine(CountPrefix + Utils.LargeNumberString(lines.Length));
				sr.WriteLine(ColumnPrefix + ColumnInfo.ToString(cols));
				for (int i = 0, icnt = notes.Length; i < icnt; ++i)
				{
					sr.WriteLine(DescrPrefix + notes[i]);
				}
				Debug.Assert(lines != null && lines.Length > 0);
				sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
				var itemCount = lines[0].Count;
				var lastItemNdx = itemCount - 1;
				for (int i = 0, icnt = lines.Length; i < icnt; ++i)
				{
					sb.Clear();
					var ln = lines[i];
					for (int j = 0; j < itemCount; ++j)
					{
						sb.Append(ln.GetItem(j));
						if (j < lastItemNdx)
							sb.Append(Constants.ReportSeparator);
					}
					sr.WriteLine(sb.ToString());
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
				if (sb != null)
					StringBuilderCache.Release(sb);
				sr?.Close();
			}
		}

	}

	public class ColumnInfo
	{
		private string _name;
		private ReportFile.ColumnType _type;
		private int _width;
		private int _columnIndex; // starts with 1
		private bool _ascending;

		public string Name => _name;
		public ReportFile.ColumnType ColumnType => _type;
		public int Width => _width;
		public int ColumnIndex => _columnIndex;
		public bool Ascending => _ascending;

		public ColumnInfo(string name, ReportFile.ColumnType colType, int width, int index, bool sortAsc)
		{
			_name = name;
			_type = colType;
			_width = width;
			_columnIndex = index;
			_ascending = sortAsc;
		}

		public static string ToString(IList<ColumnInfo> cols)
		{
			Debug.Assert(cols != null && cols.Count > 0);
			var sb = StringBuilderCache.Acquire(512);
			var lastNdx = cols.Count - 1;
			for (int i = 0, icnt = cols.Count; i < icnt; ++i)
			{
				var col = cols[i];
				sb.Append(col._name).Append('|').Append(col._type).Append('|').Append(col._width.ToString());
				if (i < lastNdx)
					sb.Append(Constants.ReportSeparator);
			}
			return StringBuilderCache.GetStringAndRelease(sb);
		}

		public void ReverseOrder()
		{
			_ascending = !_ascending;
		}

	}


	public class ListingInfo
	{
		private string _error;
		private listing<string>[] _items;
		private ColumnInfo[] _colInfos;
		private string _notes;
		private object _data;

		public string Error => _error;
		public listing<string>[] Items => _items;
		public ColumnInfo[] ColInfos => _colInfos;
		public string Notes => _notes;
		public object Data => _data;


		public const string EmptyList = "Empty Listing";

		public ListingInfo(string error, listing<string>[] items, ColumnInfo[] colInfos, string notes, object data = null)
		{
			_error = error;
			_items = items;
			_colInfos = colInfos;
			_notes = notes;
			_data = data;
		}

		public ListingInfo(string error)
		{
			_error = error;
			_items = null;
			_colInfos = null;
			_notes = null;
			_data = null;
		}

		public ListingInfo()
		{
			_error = EmptyList;
			_items = Utils.EmptyArray<listing<string>>.Value;
			_colInfos = Utils.EmptyArray<ColumnInfo>.Value;
			_notes = null;
			_data = null;
		}

		public static bool DumpListing(string path, ListingInfo info, string title, out string error, int count = Int32.MaxValue)
		{
			error = null;
			StreamWriter sw = null;
			try
			{
				int writeCnt = Math.Min(info.Items.Length, count);
				sw = new StreamWriter(path);
				sw.WriteLine(info.Notes);
				var cols = info.ColInfos;
				sw.WriteLine("### MDRDESK REPORT: ");
				sw.WriteLine("### TITLE: " + title);
				sw.WriteLine("### COUNT: " + Utils.LargeNumberString(writeCnt));
				sw.WriteLine("### SEPARATOR: " + Constants.HeavyGreekCrossPadded);
				//sw.WriteLine("### COLUMNS: Access Count|int \u271A Last Tick|int \u271A Last Access|string \u271A String|string");
				sw.Write("### COLUMNS: ");
				sw.Write(cols[0].Name + "|" + cols[0].ColumnType);
				for (int i = 1, icnt = cols.Length; i < icnt; ++i)
				{
					sw.Write(Constants.HeavyGreekCrossPadded);
					sw.Write(cols[i].Name + "|" + cols[i].ColumnType);
				}
				var items = info.Items;
				sw.WriteLine();
				int itemCount = items[0].Count;
				for (int i = 0, icnt = writeCnt; i < icnt; ++i)
				{
					sw.Write(items[i].GetItem(0));
					for (int j = 1, jcnt = itemCount; j < jcnt; ++j)
					{
						sw.Write(Constants.HeavyGreekCrossPadded);
						sw.Write(items[i].GetItem(j));
					}
					sw.WriteLine();
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
				sw?.Close();
			}
		}
	}

    public class ListingStrCmpAsc : IComparer<listing<string>>
    {
        private int _ndx;

        public ListingStrCmpAsc(int ndx)
        {
            _ndx = ndx;
        }
        public int Compare(listing<string> a, listing<string> b)
        {
            return string.Compare(a.Item(_ndx), b.Item(_ndx), StringComparison.Ordinal);
        }
    }

    public class ListingStrCmpDesc : IComparer<listing<string>>
    {
        private int _ndx;

        public ListingStrCmpDesc(int ndx)
        {
            _ndx = ndx;
        }
        public int Compare(listing<string> a, listing<string> b)
        {
            return string.Compare(b.Item(_ndx), a.Item(_ndx), StringComparison.Ordinal);
        }
    }

    public class ListingNumCmpAsc : IComparer<listing<string>>
    {
        private int _ndx;

        public ListingNumCmpAsc(int ndx)
        {
            _ndx = ndx;
        }

        public int Compare(listing<string> a, listing<string> b)
        {
            return Utils.NumStrAscComparer.Compare(a.Item(_ndx), b.Item(_ndx));
        }
    }

    public class ListingNumCmpDesc : IComparer<listing<string>>
    {
        private int _ndx;

        public ListingNumCmpDesc(int ndx)
        {
            _ndx = ndx;
        }

        public int Compare(listing<string> a, listing<string> b)
        {
            return Utils.NumStrAscComparer.Compare(b.Item(_ndx), a.Item(_ndx));
        }
    }


    public class ListingStrCmp1Asc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return string.Compare(a.First, b.First, StringComparison.Ordinal);
		}
	}

	public class ListingStrCmp2Asc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return string.Compare(a.Second, b.Second, StringComparison.Ordinal);
		}
	}
	public class ListingStrCmp3Asc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return string.Compare(a.Third, b.Third, StringComparison.Ordinal);
		}
	}
	public class ListingStrCmp4Asc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return string.Compare(a.Forth, b.Forth, StringComparison.Ordinal);
		}
	}

	public class ListingStrCmp5Asc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return string.Compare(a.Fifth, b.Fifth, StringComparison.Ordinal);
		}
	}

	public class ListingStrCmp6Asc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return string.Compare(a.Sixth, b.Sixth, StringComparison.Ordinal);
		}
	}

	public class ListingStrCmp7Asc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return string.Compare(a.Seventh, b.Seventh, StringComparison.Ordinal);
		}
	}

	public class ListingStrCmp8Asc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return string.Compare(a.Eighth, b.Eighth, StringComparison.Ordinal);
		}
	}

	public class ListingStrCmp1Desc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return string.Compare(b.First, a.First, StringComparison.Ordinal);
		}
	}

	public class ListingStrCmp2Desc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return string.Compare(b.Second, a.Second, StringComparison.Ordinal);
		}
	}
	public class ListingStrCmp3Desc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return string.Compare(b.Third, a.Third, StringComparison.Ordinal);
		}
	}
	public class ListingStrCmp4Desc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return string.Compare(b.Forth, a.Forth, StringComparison.Ordinal);
		}
	}

	public class ListingStrCmp5Desc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return string.Compare(b.Fifth, a.Fifth, StringComparison.Ordinal);
		}
	}

	public class ListingStrCmp6Desc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return string.Compare(b.Sixth, a.Sixth, StringComparison.Ordinal);
		}
	}

	public class ListingStrCmp7Desc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return string.Compare(b.Seventh, a.Seventh, StringComparison.Ordinal);
		}
	}

	public class ListingStrCmp8Desc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return string.Compare(b.Eighth, a.Eighth, StringComparison.Ordinal);
		}
	}

	public class ListingNumCmp1Asc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return Utils.NumStrAscComparer.Compare(a.First, b.First);
		}
	}

	public class ListingNumCmp2Asc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return Utils.NumStrAscComparer.Compare(a.Second, b.Second);
		}
	}

	public class ListingNumCmp3Asc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return Utils.NumStrAscComparer.Compare(a.Third, b.Third);
		}
	}

	public class ListingNumCmp4Asc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return Utils.NumStrAscComparer.Compare(a.Forth, b.Forth);
		}
	}

	public class ListingNumCmp5Asc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return Utils.NumStrAscComparer.Compare(a.Fifth, b.Fifth);
		}
	}

	public class ListingNumCmp6Asc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return Utils.NumStrAscComparer.Compare(a.Sixth, b.Sixth);
		}
	}

	public class ListingNumCmp7Asc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return Utils.NumStrAscComparer.Compare(a.Seventh, b.Seventh);
		}
	}

	public class ListingNumCmp8Asc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return Utils.NumStrAscComparer.Compare(a.Eighth, b.Eighth);
		}
	}

	public class ListingNumCmp1Desc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return Utils.NumStrDescComparer.Compare(a.First, b.First);
		}
	}

	public class ListingNumCmp2Desc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return Utils.NumStrDescComparer.Compare(a.Second, b.Second);
		}
	}

	public class ListingNumCmp3Desc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return Utils.NumStrDescComparer.Compare(a.Third, b.Third);
		}
	}

	public class ListingNumCmp4Desc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return Utils.NumStrDescComparer.Compare(a.Forth, b.Forth);
		}
	}

	public class ListingNumCmp5Desc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return Utils.NumStrDescComparer.Compare(a.Fifth, b.Fifth);
		}
	}

	public class ListingNumCmp6Desc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return Utils.NumStrDescComparer.Compare(a.Sixth, b.Sixth);
		}
	}

	public class ListingNumCmp7Desc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return Utils.NumStrDescComparer.Compare(a.Seventh, b.Seventh);
		}
	}

	public class ListingNumCmp8Desc : IComparer<listing<string>>
	{
		public int Compare(listing<string> a, listing<string> b)
		{
			return Utils.NumStrDescComparer.Compare(a.Eighth, b.Eighth);
		}
	}
}
