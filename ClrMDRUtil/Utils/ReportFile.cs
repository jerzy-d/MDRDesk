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

		public static ColumnType GetColumnType(string colType)
		{
			switch (colType.ToUpper())
			{
				case "ADDRESS":
					return ColumnType.Address;
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

		public static bool IsColumnTypeString(ColumnType colType)
		{
			return colType == ColumnType.String;
		}

		public static bool IsColumnTypeAddress(ColumnType colType)
		{
			return colType == ColumnType.Address;
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

				if (IsColumnTypeString(colInfos[0].ColumnType))
				{
					Array.Sort(itemAry, new ListingStrCmpAsc(0));
				}
				else
				{
					Array.Sort(itemAry, new ListingNumCmpAsc(0));
				}

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
						Array.Sort(ary, ascCmpNum);
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
			if (str.Contains(Constants.WindowsNewLine) || str.Contains(Constants.UnixNewLine))
			{
				StringBuilder sb = new StringBuilder(str);
				sb.Replace(Constants.WindowsNewLine, "\r\n");
				sb.Replace(Constants.UnixNewLine, "\n");
				return sb.ToString();
			}
			return str;
		}

		private static triple<string, ColumnType, int>[] GetColumnInfo(string colSpec, string[] seps,
			out ColumnType[] colTypes)
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

		public static bool WriteReport(string path, string report, string title, ColumnInfo[] cols, string[] notes,
			listing<string>[] lines, out string error)
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

		public static bool DumpListing(string path, ListingInfo listingInfo, string title, out string error, int count = Int32.MaxValue)
		{
			error = null;
			StreamWriter sw = null;
			try
			{
				int writeCnt = Math.Min(listingInfo.Items.Length, count);
				sw = new StreamWriter(path);
				var cols = listingInfo.ColInfos;
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

                if (!string.IsNullOrWhiteSpace(listingInfo.Notes))
                {
                    var noteLines = listingInfo.Notes.Split("\r\n".ToCharArray(),StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0, icnt = noteLines.Length; i < icnt; ++i)
                    {
                        if (noteLines[i].StartsWith(ReportFile.DescrPrefix))
                            sw.WriteLine(noteLines[i]);
                        else
                            sw.WriteLine(ReportFile.DescrPrefix + noteLines[i]);
                    }
                }

                var items = listingInfo.Items;
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

	/// <summary>
	/// Multicolumn sorting
	/// </summary>

	public class MultipleListingCmp : IComparer<listing<string>>
	{
		private IComparer<listing<string>>[] _cmps;
		private int[] _indices;

		public MultipleListingCmp(int[] indices, IComparer<listing<string>>[] cmps)
		{
			_indices = indices;
			_cmps = cmps;
		}

		public int Compare(listing<string> a, listing<string> b)
		{
			int cmp = _cmps[0].Compare(a, b);
			int ndx = 1;
			while (ndx < _cmps.Length && cmp == 0)
			{
				cmp = _cmps[ndx++].Compare(a, b);
			}
			return cmp;
		}
	}
}
