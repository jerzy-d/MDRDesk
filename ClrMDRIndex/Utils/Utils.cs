using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace ClrMDRIndex
{
	public class Utils
	{
		#region IO

		public static char[] DirSeps = new char[] {Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar};
		public static string GetPathLastFolder(string path)
		{
			if (string.IsNullOrWhiteSpace(path) || path.Length < 1) return string.Empty;
			if (path[path.Length - 1] == Path.DirectorySeparatorChar || path[path.Length - 1] == Path.DirectorySeparatorChar)
			{
				var ary = path.Split(DirSeps, StringSplitOptions.RemoveEmptyEntries);
				if (ary.Length < 1) return string.Empty;
				return ary[ary.Length - 1];
			}
			return Path.GetFileName(path);
		}

		public static string GetMapFolder(string dmpFilePath)
		{
			var dumpFile = Path.GetFileNameWithoutExtension(dmpFilePath);
			var dumpDir = Path.GetDirectoryName(dmpFilePath);
			return dumpDir + Path.DirectorySeparatorChar + dumpFile + ".map";
		}


		public static string GetFilePath(int runtimeIndex, string outFolder, string dmpName, string pathPostfix)
		{
			string postfix = runtimeIndex > 0 && pathPostfix.IndexOf("[0]", StringComparison.Ordinal) > 0
				? pathPostfix.Replace("[0]", "[" + runtimeIndex + "]")
				: pathPostfix;
			return outFolder + Path.DirectorySeparatorChar + dmpName + postfix;
		}

		public static string GetDumpBaseName(string mapFolder)
		{
			if (string.IsNullOrWhiteSpace(mapFolder)) return null;
			var pos = mapFolder.LastIndexOf(Path.DirectorySeparatorChar);
			if (pos >= 0 && (pos + 2) < mapFolder.Length)
			{
				var s = mapFolder.Substring(pos + 1);
				if (s.EndsWith(".map"))
				{
					s = s.Substring(0, s.Length - ".map".Length);
				}
				return s;
			}
			return null;
		}

		public static string[] GetStringListFromFile(string filePath, out string error)
		{
			error = null;
			StreamReader rd = null;
			try
			{
				rd = new StreamReader(filePath);
				var ln = rd.ReadLine();
				var count = Int32.Parse(ln);
				var ary = new string[count];
				for (var i = 0; i < count; ++i)
				{
					ln = rd.ReadLine();
					ary[i] = ln;
				}
				return ary;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				rd?.Close();
			}
		}

		public static bool WriteStringListToFile(string filePath, IList<string> lst, out string error)
		{
			error = null;
			StreamWriter wr = null;
			try
			{
				wr = new StreamWriter(filePath);
				int cnt = lst.Count;
				wr.WriteLine(cnt);
				for (var i = 0; i < cnt; ++i)
				{
					wr.WriteLine(lst[i]);
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
				wr?.Close();
			}
		}

		public static ulong[] ReadUlongArray(string path, out string error)
		{
			error = null;
			BinaryReader br = null;
			try
			{
				br = new BinaryReader(File.Open(path,FileMode.Open));
				var cnt = br.ReadInt32();
				var ary = new ulong[cnt];
				for (int i = 0; i < cnt; ++i)
				{
					ary[i] = br.ReadUInt64();
				}
				return ary;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				br?.Close();
			}
		}

		public static uint[] ReadUintArray(string path, out string error)
		{
			error = null;
			BinaryReader br = null;
			try
			{
				br = new BinaryReader(File.Open(path, FileMode.Open));
				var cnt = br.ReadInt32();
				var ary = new uint[cnt];
				for (int i = 0; i < cnt; ++i)
				{
					ary[i] = br.ReadUInt32();
				}
				return ary;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				br?.Close();
			}
		}

		public static int[] ReadIntArray(string path, out string error)
		{
			error = null;
			BinaryReader br = null;
			try
			{
				br = new BinaryReader(File.Open(path, FileMode.Open));
				var cnt = br.ReadInt32();
				var ary = new int[cnt];
				for (int i = 0; i < cnt; ++i)
				{
					ary[i] = br.ReadInt32();
				}
				return ary;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				br?.Close();
			}
		}

		public static bool WriteStringList(string filePath, IList<string> lst,out string error)
		{
			error = null;
			StreamWriter rd = null;
			try
			{
				rd = new StreamWriter(filePath);
				rd.WriteLine(lst.Count);
				for (int i = 0, icnt = lst.Count; i < icnt; ++i)
				{
					rd.WriteLine(lst[i]);
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
				rd?.Close();
			}
		}

		public static bool WriteUlongArray(string path, IList<ulong> lst, out string error)
		{
			error = null;
			BinaryWriter bw = null;
			try
			{
				bw = new BinaryWriter(File.Open(path, FileMode.Create));
				var cnt = lst.Count;
				bw.Write(cnt);
				for (int i = 0; i < cnt; ++i)
				{
					bw.Write(lst[i]);
				}
				return true;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				bw?.Close();
			}
		}

        public static bool WriteUlongIntArrays(string path, IList<ulong> lst1, IList<int> lst2, out string error)
        {
            Debug.Assert(lst1.Count == lst2.Count);
            error = null;
            BinaryWriter bw = null;
            try
            {
                bw = new BinaryWriter(File.Open(path, FileMode.Create));
                var cnt = lst1.Count;
                bw.Write(cnt);
                for (int i = 0; i < cnt; ++i)
                {
                    bw.Write(lst1[i]);
                    bw.Write(lst2[i]);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = GetExceptionErrorString(ex);
                return false;
            }
            finally
            {
                bw?.Close();
            }
        }

		public static bool WriteUlongUintIntArrays(string path, IList<ulong> lst1, IList<uint> lst2, IList<int> lst3, out string error)
		{
			Debug.Assert(lst1.Count == lst2.Count && lst1.Count == lst3.Count);
			error = null;
			BinaryWriter bw = null;
			try
			{
				bw = new BinaryWriter(File.Open(path, FileMode.Create));
				var cnt = lst1.Count;
				bw.Write(cnt);
				for (int i = 0; i < cnt; ++i)
				{
					bw.Write(lst1[i]);
					bw.Write(lst2[i]);
					bw.Write(lst3[i]);
				}
				return true;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				bw?.Close();
			}
		}

		public static bool ReadUlongIntArrays(string path, out ulong[] lst1, out int[] lst2, out string error)
		{
			error = null;
			lst1 = null;
			lst2 = null;
			BinaryReader br = null;
			try
			{
				br = new BinaryReader(File.Open(path, FileMode.Open));
				var cnt = br.ReadInt32();
				lst1 = new ulong[cnt];
				lst2 = new int[cnt];
				for (int i = 0; i < cnt; ++i)
				{
					lst1[i] = br.ReadUInt64();
					lst2[i] = br.ReadInt32();
				}
				return true;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				br?.Close();
			}
		}

		public static bool ReadUlongUintIntArrays(string path, out ulong[] lst1, out uint[] lst2, out int[] lst3, out string error)
		{
			error = null;
			lst1 = null;
			lst2 = null;
			lst3 = null;
			BinaryReader br = null;
			try
			{
				br = new BinaryReader(File.Open(path, FileMode.Open));
				var cnt = br.ReadInt32();
				lst1 = new ulong[cnt];
				lst2 = new uint[cnt];
				lst3 = new int[cnt];
				for (int i = 0; i < cnt; ++i)
				{
					lst1[i] = br.ReadUInt64();
					lst2[i] = br.ReadUInt32();
					lst3[i] = br.ReadInt32();
				}
				return true;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				br?.Close();
			}
		}

		public static bool WriteIntArrays(string path, IList<int> lst1, IList<int> lst2, out string error)
        {
            error = null;
            BinaryWriter bw = null;
            try
            {
                bw = new BinaryWriter(File.Open(path, FileMode.Create));
	            var cnt1 = lst1.Count;
	            var cnt2 = lst2.Count;
				bw.Write(cnt1);
				bw.Write(cnt2);
				if (cnt1 == cnt2)
                {
                    for (int i = 0; i < cnt1; ++i)
                    {
                        bw.Write(lst1[i]);
                        bw.Write(lst2[i]);
                    }
                }
                else
                {
                    for (int i = 0; i < cnt1; ++i)
                    {
                        bw.Write(lst1[i]);
                    }
                    for (int i = 0; i < cnt2; ++i)
                    {
                        bw.Write(lst2[i]);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = GetExceptionErrorString(ex);
                return false;
            }
            finally
            {
                bw?.Close();
            }
        }

		public static bool ReadIntArrays(string path, out int[] lst1, out int[] lst2, out string error)
		{
			error = null;
			BinaryReader br = null;
			lst1 = lst2 = null;
			try
			{
				br = new BinaryReader(File.Open(path, FileMode.Open));
				var cnt1 = br.ReadInt32();
				var cnt2 = br.ReadInt32();
				lst1 = new int[cnt1];
				lst2 = new int[cnt2];
				if (cnt1 == cnt2)
				{
					for (int i = 0; i < cnt1; ++i)
					{
						lst1[i] = br.ReadInt32();
						lst2[i] = br.ReadInt32();
					}
				}
				else
				{
					for (int i = 0; i < cnt1; ++i)
					{
						lst1[i] = br.ReadInt32();
					}
					for (int i = 0; i < cnt2; ++i)
					{
						lst2[i] = br.ReadInt32();
					}
				}
				return true;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				br?.Close();
			}
		}


		public static bool WriteUintArray(string path, IList<uint> lst, out string error)
		{
			error = null;
			BinaryWriter bw = null;
			try
			{
				bw = new BinaryWriter(File.Open(path, FileMode.Create));
				var cnt = lst.Count;
				bw.Write(cnt);
				for (int i = 0; i < cnt; ++i)
				{
					bw.Write(lst[i]);
				}
				return true;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				bw?.Close();
			}
		}

		public static bool WriteIntArray(string path, IList<int> lst, out string error)
		{
			error = null;
			BinaryWriter bw = null;
			try
			{
				bw = new BinaryWriter(File.Open(path, FileMode.Create));
				var cnt = lst.Count;
				bw.Write(cnt);
				for (int i = 0; i < cnt; ++i)
				{
					bw.Write(lst[i]);
				}
				return true;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				bw?.Close();
			}
		}

		public static void CloseStream(ref StreamWriter s)
		{
			s?.Close();
			s = null;
		}

		public static void CloseStream(ref StreamReader s)
		{
			s?.Close();
			s = null;
		}

		public static void CloseStream(ref BinaryWriter s)
		{
			s?.Close();
			s = null;
		}
		public static void CloseStream(ref BinaryReader s)
		{
			s?.Close();
			s = null;
		}

		#endregion IO

		#region Dac File Search

		public static string SearchDacFolder(string dacFileName, string dacFileFolder)
		{

			var folder = new DirectoryInfo(dacFileFolder);
			foreach (var dir in folder.EnumerateDirectories())
			{
				var pathName = dir.Name;
				var dirName = Path.GetFileName(pathName);
				if (string.Compare(dirName, dacFileName, StringComparison.OrdinalIgnoreCase) == 0)
				{
					return LookForDacDll(dir);
				}
			}
			return null;
		}

		private static string LookForDacDll(DirectoryInfo dir)
		{
			Queue<DirectoryInfo> que = new Queue<DirectoryInfo>();
			que.Enqueue(dir);
			while (que.Count > 0)
			{
				dir = que.Dequeue();
				foreach (var file in dir.EnumerateFiles())
				{
					var fname = Path.GetFileName(file.Name);
					if (fname.StartsWith("mscordacwks", StringComparison.OrdinalIgnoreCase)
						&& fname.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
					{
						return file.FullName;
					}
				}
				foreach (var d in dir.EnumerateDirectories())
				{
					que.Enqueue(d);
				}
			}
			return null;
		}

		#endregion Dac File Search

		#region String Utils

		public static string ReverseTypeName(string name)
		{
			var plusPos = name.IndexOf('+');
			var bracketPos = name.IndexOf('<');
			var lastDotPos = name.LastIndexOf('.');
			if (lastDotPos < 0 && bracketPos < 0 && plusPos < 0)
				return name;
			if (bracketPos == 0)
			{
				return name;
			}
			if (plusPos < 0 && bracketPos < 0)
			{
				return name.Substring(lastDotPos + 1) + Constants.NamespaceSepPadded + name.Substring(0, lastDotPos);
			}
			else if (bracketPos < 0)
			{
				return name.Substring(plusPos + 1) + "+" + Constants.NamespaceSepPadded + name.Substring(0, plusPos);
			}
			else if (plusPos < 0)
			{
				var pos = name.LastIndexOf('.', bracketPos, bracketPos - 1);
				return name.Substring(pos + 1) + Constants.NamespaceSepPadded + name.Substring(0, pos);

			}
			else if (plusPos < bracketPos)
			{
				return name.Substring(plusPos + 1) + "+" + Constants.NamespaceSepPadded + name.Substring(0, plusPos);
			}
			else
			{
				var pos = name.LastIndexOf('.', bracketPos, bracketPos - 1);
				return name.Substring(pos + 1) + Constants.NamespaceSepPadded + name.Substring(0, pos);
			}
		}

        public static string BaseTypeName(string name)
        {
            var plusPos = name.IndexOf('+');
            var bracketPos = name.IndexOf('<');
            var lastDotPos = name.LastIndexOf('.');
            if (lastDotPos < 0 && bracketPos < 0 && plusPos < 0)
                return name;
            if (bracketPos == 0)
            {
                return name;
            }
            if (plusPos < 0 && bracketPos < 0)
            {
                return name.Substring(lastDotPos + 1);
            }
            else if (bracketPos < 0)
            {
                return name.Substring(plusPos + 1) + "+";
            }
            else if (plusPos < 0)
            {
                var pos = name.LastIndexOf('.', bracketPos, bracketPos - 1);
                return name.Substring(pos + 1);

            }
            else if (plusPos < bracketPos)
            {
                return name.Substring(plusPos + 1) + "+";
            }
            else
            {
                var pos = name.LastIndexOf('.', bracketPos, bracketPos - 1);
                return name.Substring(pos + 1);
            }
        }

        public static int[] SortAndGetMap(string[] ary)
		{
			int cnt = ary.Length;
			var map = new int[cnt];
			for (var i = 0; i < cnt; ++i)
			{
				map[i] = i;
			}
			Array.Sort(ary, map,StringComparer.Ordinal);
			return map;
		}

		public static string[] CloneIntArray(string[] ary)
		{
			var cnt = ary.Length;
			var nary = new string[cnt];
			for (int i = 0; i < cnt; ++i)
			{
				nary[i] = ary[i];
			}
			return nary;
		}

		public static string ReplaceNewlines(string str)
		{
			if (string.IsNullOrEmpty(str)) return str;
			for (int i = 0, icnt = str.Length; i < icnt; ++i)
			{
				if (str[i] == '\n')
				{
					var sb = new StringBuilder(str);
					sb.Replace("\r\n", Constants.WindowsNewLine);
					sb.Replace("\n", Constants.UnixNewLine);
					return sb.ToString();
				}
			}
			return str;
		}

		public static string RemoveWhites(string str)
		{
			bool replaceWhites = false;
			for (int i = 0, icnt = str.Length; i < icnt; ++i)
			{
				if (char.IsWhiteSpace(str[i]))
				{
					replaceWhites = true;
					break;
				}
			}
			if (replaceWhites)
			{
				StringBuilder sb = new StringBuilder(str);
				sb.Replace("\r\n", "_");
				for (int i = 0, icnt = sb.Length; i < icnt; ++i)
				{
					if (char.IsWhiteSpace(sb[i]))
					{
						sb[i] = '_';
					}
				}
				return sb.ToString();
			}
			return str;
		}

		public static string RestoreNewlines(string str)
		{
			if (string.IsNullOrEmpty(str)) return str;
			for (int i = 0, icnt = str.Length; i < icnt; ++i)
			{
				if (str[i] == Constants.WindowsNewLineChar || str[i] == Constants.UnixNewLineChar)
				{
					var sb = new StringBuilder(str);
					sb.Replace(Constants.WindowsNewLine, "\r\n");
					sb.Replace(Constants.UnixNewLine, "\n");
					return sb.ToString();
				}
			}
			return str;
		}

		public static int SkipWhites(string str, int pos)
		{
			for (; pos < str.Length && Char.IsWhiteSpace(str[pos]); ++pos) ;
			return pos;
		}

		public static int SkipNonWhites(string str, int pos)
		{
			for (; pos < str.Length && !Char.IsWhiteSpace(str[pos]); ++pos) ;
			return pos;
		}

		private static bool IsBracket(char c)
		{
			return c == '['
			       || c == ']'
			       || c == '{'
			       || c == '}'
			       || c == '('
			       || c == ')';
		}

		private static char GetOpposingBracket(char c)
		{
			if (c == '[') return ']';
			if (c == '{') return '}';
			if (c == '(') return ')';
			if (c == ']') return '[';
			if (c == '}') return '{';
			if (c == ')') return '(';
			throw new ArgumentException("Utils.GetOpposingBracket -- not handled bracket: " + c);
		}

		private static int FindChar(string s, char c, int pos)
		{
			for (int i = pos, icnt = s.Length; i < icnt; ++i)
			{
				if (s[i] == c) return i;
			}
			return -1;
		}

		//public static bool ParseReportLine(string ln, ReportFile.ColumnType[] columns, List<string> lst)
		//{
		//	if (string.IsNullOrWhiteSpace(ln)) return false;
		//	int pos = 0, epos = 0;
		//	int lastItem = columns.Length - 1;
		//	for (int i = 0, icount = columns.Length; i < icount && epos < ln.Length; ++i)
		//	{
		//		pos = Utils.SkipWhites(ln, epos);
		//		if (i == lastItem && columns[lastItem] == ReportFile.ColumnType.String)
		//		{
		//			var substr = ln.Substring(pos);
		//			lst.Add(string.IsNullOrEmpty(substr) ? string.Empty : substr);
		//			return true;
		//		}

		//		if (IsBracket(ln[pos]))
		//		{
		//			char b = GetOpposingBracket(ln[pos]);
		//			++pos;
		//			epos = FindChar(ln, b, pos);
		//			if (epos < 0) return false;
		//			if (pos < epos) lst.Add(ln.Substring(pos, epos - pos).Trim());
		//			else lst.Add(string.Empty);
		//		}
		//		else
		//		{
		//			epos = SkipNonWhites(ln, pos);
		//			if (pos < epos) lst.Add(ln.Substring(pos, epos - pos).Trim());
		//			else lst.Add(string.Empty);
		//		}
		//		++epos;
		//	}

		//	return true;
		//}

		public static bool StartsWithPrefix(string str, IList<string> prefs)
		{
			for (int i = 0, icnt = prefs.Count; i < icnt; ++i)
			{
				if (str.StartsWith(prefs[i]))
					return true;
			}
			return false;
		}

		public static string GetValidName(string name)
		{
			if (string.IsNullOrEmpty(name)) return name;
			bool firstLetterOk = char.IsLetter(name[0]) || name[0] == '_';
			bool needChange = false;
			for (int i = 0,icnt=name.Length; i < icnt; ++i)
			{
				if (char.IsLetterOrDigit(name[i]) || name[i] == '_') continue;
				needChange = true;
				break;
			}
			if (firstLetterOk && !needChange) return name;
			var sb = StringBuilderCache.Acquire(name.Length + 1);
			if (!firstLetterOk)
			{
				sb.Append('_');
			}
			for (int i = 0, icnt = name.Length; i < icnt; ++i)
			{
				if (char.IsWhiteSpace(name[i])) continue;
				if (!(char.IsLetterOrDigit(name[i]) || name[i] == '_'))
				{
					sb.Append('_');
					continue;
				}
				sb.Append(name[i]);
			}
			return StringBuilderCache.GetStringAndRelease(sb);
		}

		#endregion String Utils

		#region Comparers

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool SameStrings(string s1, string s2)
		{
			return string.Compare(s1, s2, StringComparison.Ordinal) == 0;
		}

		public class LongCmpDesc : IComparer<long>
		{
			public int Compare(long a, long b)
			{
				return a < b ? 1 : (a > b ? -1 : 0);
			}
		}

		public class IntCmpDesc : IComparer<int>
		{
			public int Compare(int a, int b)
			{
				return a < b ? 1 : (a > b ? -1 : 0);
			}
		}

		public class UIntCmpDesc : IComparer<uint>
		{
			public int Compare(uint a, uint b)
			{
				return a < b ? 1 : (a > b ? -1 : 0);
			}
		}

		public class IntPairCmp : IComparer<pair<int, int>>
		{
			public int Compare(pair<int, int> a, pair<int, int> b)
			{
				return a.First < b.First ? -1 : (a.First > b.First ? 1 : 0);
			}
		}

		public class IntTripleCmp : IComparer<triple<int, String, ulong>>
		{
			public int Compare(triple<int, String, ulong> a, triple<int, String, ulong> b)
			{
				return a.First < b.First ? -1 : (a.First > b.First ? 1 : 0);
			}
		}

		public class KVStrStrCmp : IComparer<KeyValuePair<string, string>>
		{
			public int Compare(KeyValuePair<string, string> a, KeyValuePair<string, string> b)
			{
				return string.Compare(a.Value, b.Value, StringComparison.Ordinal);
			}
		}

        public class KVIntIntCmp : IComparer<KeyValuePair<int, int>>
        {
            public int Compare(KeyValuePair<int, int> a, KeyValuePair<int, int> b)
            {
                if (a.Key == b.Key)
                    return a.Value < b.Value ? -1 : (a.Value > b.Value ? 1 : 0);
                return a.Key < b.Key ? -1 : (a.Key > b.Key ? 1 : 0);
            }
        }

        public class KVUlongUlongKeyCmp : IComparer<KeyValuePair<ulong, ulong>>
		{
			public int Compare(KeyValuePair<ulong, ulong> a, KeyValuePair<ulong, ulong> b)
			{
				if (a.Key == b.Key)
					return a.Value < b.Value ? -1 : (a.Value > b.Value ? 1 : 0);
				return a.Key < b.Key ? -1 : (a.Key > b.Key ? 1 : 0);
			}
		}


		public class TripleUlongUlongIntKeyCmp : IComparer<triple<ulong, ulong,int>>
		{
			public int Compare(triple<ulong, ulong,int> a, triple<ulong, ulong,int> b)
			{
				if (a.First == b.First)
				{
					if (a.Second == b.Second)
						return a.Third < b.Third ? -1 : (a.Third > b.Third ? 1 : 0);
					return a.Second < b.Second ? -1 : (a.Second > b.Second ? 1 : 0);
				}
				return a.First < b.First ? -1 : (a.First > b.First ? 1 : 0);
			}
		}

        public class QuadrupleUlongUlongIntKeyCmp : IComparer<quadruple<ulong, ulong, int, int>>
        {
            public int Compare(quadruple<ulong, ulong, int, int> a, quadruple<ulong, ulong, int, int> b)
            {
                if (a.First == b.First)
                {
                    if (a.Second == b.Second)
                    {
                        if (a.Third == b.Third)
                        {
                            return a.Forth < b.Forth ? -1 : (a.Forth > b.Forth ? 1 : 0);
                        }
                        return a.Third < b.Third ? -1 : (a.Third > b.Third ? 1 : 0);
                    }
                    return a.Second < b.Second ? -1 : (a.Second > b.Second ? 1 : 0);
                }
                return a.First < b.First ? -1 : (a.First > b.First ? 1 : 0);
            }
        }

        public class StrListCmp : IComparer<IList<string>>
		{
			public int Compare(IList<string> a, IList<string> b)
			{
				var aLstCnt = a.Count;
				var bLstCnt = b.Count;
				for (int i = 0; i < aLstCnt && i < bLstCnt; ++i)
				{
					var cmp = string.Compare(a[i], b[i], StringComparison.Ordinal);
					if (cmp != 0) return cmp;
				}
				return aLstCnt < bLstCnt ? -1 : (aLstCnt > bLstCnt ? 1 : 0);
			}
		}

		public static NumStrCmpAsc NumStrAscComparer = new NumStrCmpAsc();

		public class NumStrCmpAsc : IComparer<string>
		{
			public int Compare(string a, string b)
			{
				bool aMinusSign = a.Length > 0 && a[0] == '-';
				bool bMinusSign = b.Length > 0 && b[0] == '-';
				if (aMinusSign && bMinusSign)
					return CompareNegatives(a, b);
				if (aMinusSign && !bMinusSign) return -1;
				if (!aMinusSign && bMinusSign) return 1;

				if (a.Length == b.Length)
				{
					for (int i = 0, icnt = a.Length; i < icnt; ++i)
					{
						if (a[i] < b[i]) return -1;
						if (a[i] > b[i]) return 1;
					}
					return 0;
				}
				return a.Length < b.Length ? -1 : 1;
			}

			private int CompareNegatives(string a, string b)
			{
				if (a.Length == b.Length)
				{
					for (int i = 1, icnt = a.Length; i < icnt; ++i)
					{
						if (a[i] > b[i]) return -1;
						if (a[i] < b[i]) return 1;
					}
					return 0;
				}
				return a.Length < b.Length ? 1 : -1;
			}
		}

		public static NumStrCmpDesc NumStrDescComparer = new NumStrCmpDesc();

		public class NumStrCmpDesc : IComparer<string>
		{
			public int Compare(string a, string b)
			{
				bool aMinusSign = a.Length > 0 && a[0] == '-';
				bool bMinusSign = b.Length > 0 && b[0] == '-';
				if (aMinusSign && bMinusSign)
					return CompareNegatives(a, b);
				if (aMinusSign && !bMinusSign) return 1;
				if (!aMinusSign && bMinusSign) return -1;

				if (a.Length == b.Length)
				{
					for (int i = 0, icnt = a.Length; i < icnt; ++i)
					{
						if (a[i] < b[i]) return 1;
						if (a[i] > b[i]) return -1;
					}
					return 0;
				}
				return a.Length > b.Length ? -1 : 1;
			}

			private int CompareNegatives(string a, string b)
			{
				if (a.Length == b.Length)
				{
					for (int i = 1, icnt = a.Length; i < icnt; ++i)
					{
						if (a[i] > b[i]) return 1;
						if (a[i] < b[i]) return -1;
					}
					return 0;
				}
				return a.Length < b.Length ? -1 : 1;
			}
		}



	    public class KvStrKvStrInt : IComparer<KeyValuePair<string, KeyValuePair<string, int>[]>>
	    {
	        public int Compare(KeyValuePair<string, KeyValuePair<string, int>[]> a,
                KeyValuePair<string, KeyValuePair<string, int>[]>  b)
	        {
	            return string.Compare(a.Key, b.Key,StringComparison.Ordinal);
	        }
	    }

        public class KvStrInt : IComparer<KeyValuePair<string, int>>
        {
            public int Compare(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
            {
                var cmp = string.Compare(a.Key, b.Key, StringComparison.Ordinal);
                if (cmp == 0)
                {
                    cmp = a.Value < b.Value ? -1 : (a.Value > b.Value ? 1 : 0);
                }
                return cmp;
            }
        }
		#endregion Comparers

		#region Misc

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsIndexInvalid(int ndx)
		{
			return Constants.InvalidIndex == ndx;
		}

		private static int _id = 0;

		public static int GetNewID()
		{
			var id = Interlocked.Increment(ref _id);
			return id;
		}

		public static int NumberOfDigits(ulong val)
		{
			if (val >= 1000000000UL) return 10;
			int n = 1;
			if (val >= 100000000) { val /= 100000000; n += 8; }
			if (val >= 10000) { val /= 10000; n += 4; }
			if (val >= 100) { val /= 100; n += 2; }
			if (val >= 10) { val /= 10; n += 1; }
			return n;
		}

		public static string GetNameWithoutId(string name)
		{
			if (string.IsNullOrWhiteSpace(name)) return name;
			int pos = name.IndexOf("__");
			if (pos <= 0) return name;
			return name.Substring(0, pos);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int RoundupToPowerOf2Boundary(int number, int powerOf2)
		{
			return (number + powerOf2 - 1) & ~(powerOf2 - 1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong RoundupToPowerOf2Boundary(ulong number, ulong powerOf2)
		{
			return (number + powerOf2 - 1) & ~(powerOf2 - 1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string AddressString(ulong addr)
		{
			return string.Format("0x{0:x14}", addr);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string AddressStringHeader(ulong addr)
		{
			return string.Format("[0x{0:x14}] ", addr);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string UintHexStringHeader(uint num)
		{
			return string.Format("[0x{0:x8}] ", num);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string DurationString(TimeSpan ts)
		{
			return string.Format(" {0:00}:{1:00}:{2:00}.{3:00}",ts.Hours, ts.Minutes, ts.Seconds,ts.Milliseconds / 10);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string SortableLengthString(ulong len)
		{
			return len == 0 ? "             O" : string.Format("{0,14:0#,###,###,###}", len);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string SortableLengthStringHeader(ulong len)
		{
			return len == 0 ? "             O" : string.Format("[{0,14:0#,###,###,###}] ", len);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string SortableSizeString(int sz)
		{
			return sz == 0 ? "           O" : string.Format("{0,12:0##,###,###}", sz);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string SortableSizeStringHeader(int sz)
		{
			return sz == 0 ? "           O" : string.Format("[{0,12:0#,###,###}] ", sz);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string SizeString(int sz)
		{
			return sz == 0 ? "           O" : string.Format("{0,12:#,###,###}", sz);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string SizeString(long sz)
		{
			return sz == 0 ? "           O" : string.Format("{0,12:#,###,###}", sz);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string SizeString(ulong sz)
		{
			return sz == 0 ? "           O" : string.Format("{0,12:#,###,###}", sz);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string SizeStringHeader(int sz)
		{
			return sz == 0 ? "[           0] " : string.Format("[{0,12:#,###,###}] ", sz);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string SizeStringHeader(long sz)
		{
			return sz == 0 ? "[           0] " : string.Format("[{0,12:#,###,###}] ", sz);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string SmallIdHeader(int id)
		{
			return string.Format("[{0,06}] ", id);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string SmallNumberHeader(int num)
		{
			return string.Format("[{0,03}] ", num);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string LargeNumberString(int num)
		{
			return num == 0 ? Constants.ZeroStr : string.Format("{0:#,###,###,###}", num);
		}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string LargeNumberString(long num)
        {
            return num == 0 ? Constants.ZeroStr : string.Format("{0:#,###,###,###}", num);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string LargeNumberString(ulong num)
        {
            return num == 0 ? Constants.ZeroStr : string.Format("{0:#,###,###,###}", num);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string TimeString(DateTime dt)
		{
			return dt.ToString("hh:mm:ss:fff");
		}

		public static string FormatBytes(long bytes)
		{
			const int scale = 1024;
			string[] orders = new string[] { "GB", "MB", "KB", "Bytes" };
			long max = (long)Math.Pow(scale, orders.Length - 1);
			foreach (string order in orders)
			{
				if (bytes > max)
					return string.Format("{0:##.##} {1}", decimal.Divide(bytes, max), order);

				max /= scale;
			}
			return "0 Bytes";
		}

		public static bool IsWhiteSpace(string str)
		{
			if (str == null || str.Length == 0) return false;
			for (int i = 0, icnt = str.Length; i < icnt; ++i)
			{
				if (!Char.IsWhiteSpace(str[i])) return false;
			}
			return true;
		}

		public static bool IsSpaces(string str)
		{
			if (str == null || str.Length == 0) return false;
			for (int i = 0, icnt = str.Length; i < icnt; ++i)
			{
				if (str[i] != ' ') return false;
			}
			return true;
		}


		private const int scanSize = 256;

		/// <summary>
		/// Find index of the first entry with value = val.
		/// The array is grouped by values.
		/// </summary>
		public static int GetFirstIndex(int[] ary, int ndx, int count, int val)
		{
			if (ndx == 0) return 0;
			if (ary[ndx - 1] != val) return ndx;
			if (ndx == ary.Length - 1 || ary[ndx + 1] != val) return ndx - count + 1;

			int dist = scanSize;
			int fst = Math.Max(0, ndx - dist);
			while (fst > 0 && ary[fst] == val)
			{
				dist *= 2;
				fst = Math.Max(0, ndx - dist);
			}
			if (fst == 0 && ary[fst] == val) return 0;
			if (ndx - fst <= scanSize)
			{
				for (; fst < ndx; ++fst)
				{
					if (ary[fst] == val)
						return fst;
				}
				Debug.Assert(false, "Utils. GetFirstIndex -- cannot find the value?");
			}

			return -1; // TODO JRD
		}

	    public static int GetFirstLastValueIndices(int[] ary, int ndx, out int end)
	    {
	        int val = ary[ndx];
	        int first = ndx;
	        while (first > 0 && ary[first] == val) --first;
	        first = ary[first] == val ? first : --first;
	        end = ndx;
	        int aryEnd = ary.Length;
	        while (end < aryEnd && ary[end] == val) ++end;
	        return first;
	    }

		public static int[] CloneIntArray(int[] ary)
		{
			const int INT_SIZE = 4;

			var cnt = ary.Length;
			var nary = new int[cnt];
			Buffer.BlockCopy(ary, 0, nary, 0, cnt * INT_SIZE);
			return nary;
		}

		public static T[] CloneArray<T>(T[] ary)
		{
			var cnt = ary.Length;
			var nary = new T[cnt];
			Array.Copy(ary,nary,cnt);
			return nary;
		}

		public static int[] SortAndGetMap(int[] ary)
		{
			int cnt = ary.Length;
			var map = new int[cnt];
			for (var i = 0; i < cnt; ++i)
			{
				map[i] = i;
			}
			Array.Sort(ary, map);
			return map;
		}

		public static void InitArray<T>(T[] ary, T value)
		{
			for (int i = 0, icnt = ary.Length; i < icnt; ++i)
				ary[i] = value;
		}

		public static void Iota(int[] ary)
		{
			for (int i = 0, icnt = ary.Length; i < icnt; ++i)
				ary[i] = i;
		}

		public static int[] Iota(int cnt)
		{
			var ary = new int[cnt];
			for (int i = 0, icnt = cnt; i < icnt; ++i)
				ary[i] = i;
			return ary;
		}

		public static bool IsSorted(IList<string> lst, out Tuple<string,string> badCouple)
		{
			badCouple = null;
			for (int i = 1, icnt = lst.Count; i < icnt; ++i)
			{
				if (string.Compare(lst[i - 1], lst[i], StringComparison.Ordinal) > 0)
				{
					badCouple = new Tuple<string, string>(lst[i-1],lst[i]);
					return false;
				}
			}
			badCouple = new Tuple<string, string>(string.Empty, string.Empty);
			return true;
		}

        public static bool IsSorted(IList<string> lst, out KeyValuePair<string, string>[] bad)
        {
            bad = null;
            var badlst = new List<KeyValuePair<string, string>>();
            for (int i = 1, icnt = lst.Count; i < icnt; ++i)
            {
                if (string.Compare(lst[i - 1], lst[i], StringComparison.Ordinal) > 0)
                {
                    badlst.Add(new KeyValuePair<string, string>(lst[i - 1], lst[i]));
                 }
            }
            bad = badlst.ToArray();
            return bad.Length < 1 ? true : false;
        }

        public static bool IsSorted<T>(IList<T> lst) where T : System.IComparable<T>
		{
			for (int i = 1, icnt = lst.Count; i < icnt; ++i)
			{
				if (lst[i - 1].CompareTo(lst[i]) > 0) return false;
			}
			return true;
		}

		public static int[] GetIntArrayMapping(int[] ary, out int[] offs)
		{
			var cnt = ary.Length;
			var arySorted = new int[cnt];
			Array.Copy(ary,arySorted,cnt);
			var indices = Iota(cnt);
			Array.Sort(arySorted,indices);
			var offsets = new List<int>(Math.Min(cnt,1024*8));
			var curTypeId = 0;
			offsets.Add(0);
			for (int i = 0; i < cnt; ++i)
			{
				while (arySorted[i] != curTypeId)
				{
					offsets.Add(i);
					++curTypeId;
				}
			}
			offsets.Add(cnt);
			offs = offsets.ToArray();
			return indices;
		}

		public static int[] GetIdArray(int id, int[] ary, int[] map, int[] offsets)
		{
			var cnt = offsets[id + 1] - offsets[id];
			var outAry = new int[cnt];
			var offset = offsets[id];
			for (int i = 0; i < cnt; ++i)
			{
				outAry[i] = ary[map[offset]];
				++offset;
			}
			return outAry;
		}

		public static KeyValuePair<int, string>[] GetHistogram<T>(SortedDictionary<string, List<T>> dct)
		{
			KeyValuePair<int, string>[] hist =new KeyValuePair<int, string>[dct.Count];
			int ndx = 0;
			foreach (var kv in dct)
			{
				hist[ndx++] = new KeyValuePair<int, string>(
						kv.Value?.Count ?? 0,
						kv.Key
					);
			}
			// sort in descending order
			var cmp = Comparer<KeyValuePair<int,string>>.Create((a, b) =>
			{
				return a.Key < b.Key ? 1 : (a.Key > b.Key ? -1 : string.Compare(a.Value,b.Value,StringComparison.Ordinal));
			});
			Array.Sort(hist,cmp);
			return hist;
		}


		public static KeyValuePair<int, string>[] GetHistogram(SortedDictionary<string, int> dct)
		{
			KeyValuePair<int, string>[] hist = new KeyValuePair<int, string>[dct.Count];
			int ndx = 0;
			foreach (var kv in dct)
			{
				hist[ndx++] = new KeyValuePair<int, string>(
						kv.Value,
						kv.Key
					);
			}
			// sort in descending order
			var cmp = Comparer<KeyValuePair<int, string>>.Create((a, b) =>
			{
				return a.Key < b.Key ? 1 : (a.Key > b.Key ? -1 : string.Compare(a.Value, b.Value, StringComparison.Ordinal));
			});
			Array.Sort(hist, cmp);
			return hist;
		}

		public static int ConvertToInt(string str, int begin, int end)
		{
			int val = 0;
			for (; begin < end; ++begin)
				val = val*10 + (str[begin] - '0');
			return val;
		}

		public static long ConvertToLong(string str, int begin, int end)
		{
			long val = 0;
			for (; begin < end; ++begin)
				val = val * 10 + (str[begin] - '0');
			return val;
		}

		#region Errors Formatting

		public static string GetErrorString(string caption, string heading, string text, string details=null)
		{
			return (caption ?? string.Empty) + Constants.HeavyGreekCrossPadded
					+ (heading ?? string.Empty) + Constants.HeavyGreekCrossPadded
					+ (text ?? string.Empty) + Constants.HeavyGreekCrossPadded
					+ details ?? string.Empty;
		}

		public static string GetExceptionErrorString(Exception ex)
		{

			return ex.GetType().Name + Constants.HeavyGreekCrossPadded // caption
			       + ex.Source + Constants.HeavyGreekCrossPadded // heading
			       + ex.Message + Constants.HeavyGreekCrossPadded // text
			       + ex.StackTrace; // details;
		}


		//public static string ToRoman(int number)
		//{
		//	if ((number < 0) || (number > 3999)) throw new ArgumentOutOfRangeException("insert value betwheen 1 and 3999");
		//	var sb = StringBuilderCache.Acquire(32);
		//	while (number > 0)
		//	{
		//		if (number >= 1000) return "M" + ToRoman(number - 1000);  // 2160 Ⅰ ROMAN NUMERAL ONE
		//		if (number >= 900) return "CM" + ToRoman(number - 900); //EDIT: i've typed 400 instead 900
		//		if (number >= 500) return "D" + ToRoman(number - 500);
		//		if (number >= 400) return "CD" + ToRoman(number - 400);
		//		if (number >= 100) return "C" + ToRoman(number - 100);
		//		if (number >= 90) return "XC" + ToRoman(number - 90);
		//		if (number >= 50) return "L" + ToRoman(number - 50);
		//		if (number >= 40) return "XL" + ToRoman(number - 40);
		//		if (number >= 10) return "X" + ToRoman(number - 10);
		//		if (number >= 9) return "IX" + ToRoman(number - 9);
		//		if (number >= 5) return "V" + ToRoman(number - 5);
		//		if (number >= 4) return "IV" + ToRoman(number - 4);
		//		if (number >= 1) return "I" + ToRoman(number - 1); // 2170 ⅰ SMALL ROMAN NUMERAL ONE
		//	}
		//}
		#endregion Errors Formatting


		public static int VersionValue(Version version)
		{
			return version.Major*1000 + version.Minor*100 + (version.Build%10);
		}


		private const string verPrefix = "MDR Version: [";
		public static bool IsIndexVersionCompatible(Version version, string verStr)
		{
			if (!verStr.StartsWith(verPrefix)) return false;
			var pos = verStr.IndexOf(']');
			if (pos <= verPrefix.Length) return false;
			int ndxVerValue;
			if (Int32.TryParse(verStr.Substring(verPrefix.Length, pos - verPrefix.Length),out ndxVerValue))
			{
				if (VersionValue(version) <= ndxVerValue) return true; // it should be equal but less is added for unit tests
			}
			return false;
		}

		/// <summary>
		/// Return an empty array to avoid unnecessary memory allocation.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		public static class EmptyArray<T>
		{
			/// <summary>
			/// Static empty array of some type.
			/// </summary>
			public static readonly T[] Value = new T[0];
		}

        /// <summary>
        /// Return an empty list to avoid unnecessary memory allocation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static class EmptyList<T>
		{
			/// <summary>
			/// Static empty list of some type.
			/// </summary>
			public static readonly List<T> Value = new List<T>(0);
		}

		/// <summary>
		/// Force gc collection, and compact LOH.
		/// </summary>
		public static void ForceGcWithCompaction()
		{
			GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
			GC.Collect();
			GC.Collect();
		}

		#endregion Misc
	}
}
