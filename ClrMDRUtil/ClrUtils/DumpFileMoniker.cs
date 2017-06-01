using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class DumpFileMoniker
	{
		private readonly string _path;

		public string Path => _path;

		public string FileName => System.IO.Path.GetFileName(_path);

		public string DumpFileName => System.IO.Path.GetFileName(_path);

		public string OutputFolder => System.IO.Path.GetDirectoryName(_path) // dump file directory
		                              + System.IO.Path.DirectorySeparatorChar
		                              + System.IO.Path.GetFileName(_path) + ".map" // dummp file name
		                              + System.IO.Path.DirectorySeparatorChar
		                              + "ad-hoc.queries";

		public string MapFolder => System.IO.Path.GetDirectoryName(_path) // dump file directory
		                           + System.IO.Path.DirectorySeparatorChar
		                           + System.IO.Path.GetFileName(_path) + ".map";

		public DumpFileMoniker(string path)
		{
			_path = path;
		}

		public string GetFilePath(int runtimeIndex, string pathPostfix)
		{
			string postfix = runtimeIndex > 0 && pathPostfix.IndexOf("[0]", StringComparison.Ordinal) > 0
				? pathPostfix.Replace("[0]", "[" + runtimeIndex + "]")
				: pathPostfix;
			return MapFolder + System.IO.Path.DirectorySeparatorChar + DumpFileName + postfix;
		}

		public static string GetFilePath(int runtimeIndex, string outFolder, string dmpName, string pathPostfix)
		{
			string postfix = runtimeIndex > 0 && pathPostfix.IndexOf("[0]", StringComparison.Ordinal) > 0
				? pathPostfix.Replace("[0]", "[" + runtimeIndex + "]")
				: pathPostfix;
			return outFolder + System.IO.Path.DirectorySeparatorChar + dmpName + postfix;
		}

		public static string GetFilePath(int runtimeIndex, string dmpFilePath, string pathPostfix)
		{
			var dmpName = System.IO.Path.GetFileName(dmpFilePath);
			var indexDir = System.IO.Path.GetDirectoryName(dmpFilePath) // dump file directory
								   + System.IO.Path.DirectorySeparatorChar
								   + System.IO.Path.GetFileName(dmpFilePath) + ".map";

			string postfix = runtimeIndex > 0 && pathPostfix.IndexOf("[0]", StringComparison.Ordinal) > 0
				? pathPostfix.Replace("[0]", "[" + runtimeIndex + "]")
				: pathPostfix;
			return indexDir + System.IO.Path.DirectorySeparatorChar + dmpName + postfix;
		}

		public static Tuple<string, string> GetAndCreateMapFolders(string dmpFilePath, out string error)
		{
			error = null;
			try
			{
				var dumpFile = System.IO.Path.GetFileName(dmpFilePath);
				var dumpDir = System.IO.Path.GetDirectoryName(dmpFilePath);
				var mapDir = dumpDir + System.IO.Path.DirectorySeparatorChar + dumpFile + ".map";
				if (!Directory.Exists(mapDir)) Directory.CreateDirectory(mapDir);
				var adHocQueryDir = mapDir + System.IO.Path.DirectorySeparatorChar + "ad-hoc.queries";
				if (!Directory.Exists(adHocQueryDir)) Directory.CreateDirectory(adHocQueryDir);
				return new Tuple<string, string>(mapDir, adHocQueryDir);
			}
			catch (Exception ex)
			{
				Utils.GetExceptionErrorString(ex);
				return null;
			}
		}


		public static string GetAndCreateOutFolder(string dmpFilePath, out string error)
		{
			error = null;
			try
			{
				var dumpFile = System.IO.Path.GetFileName(dmpFilePath);
				var dumpDir = System.IO.Path.GetDirectoryName(dmpFilePath);
				var mapDir = dumpDir + System.IO.Path.DirectorySeparatorChar + dumpFile + ".map";
				if (!Directory.Exists(mapDir)) Directory.CreateDirectory(mapDir);
				var adHocQueryDir = mapDir + System.IO.Path.DirectorySeparatorChar + "ad-hoc.queries";
				if (!Directory.Exists(adHocQueryDir)) Directory.CreateDirectory(adHocQueryDir);
				return adHocQueryDir;
			}
			catch (Exception ex)
			{
				Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public static string GetDumpBaseName(string mapFolder)
		{
			if (string.IsNullOrWhiteSpace(mapFolder)) return null;
			var pos = mapFolder.LastIndexOf(System.IO.Path.DirectorySeparatorChar);
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

		public string GetPathReplacingFileName(string newFileName)
		{
			var folder = System.IO.Path.GetDirectoryName(_path);
			return folder + System.IO.Path.DirectorySeparatorChar + newFileName;
		}

		public string GetPathAppendingToFileName(string postfix)
		{
			return _path + postfix;
		}

		public static string GetRuntimeFilePath(int runtimeIndex, string dumpPath, string pathPostfix)
		{
			return GetMapFolder(dumpPath) + System.IO.Path.DirectorySeparatorChar + GetRuntimeFileName(runtimeIndex,dumpPath,pathPostfix);
		}

		public static string GetRuntimeFileName(int runtimeIndex, string dumpPath, string pathPostfix)
		{
			string postfix = runtimeIndex > 0 && pathPostfix.IndexOf("[0]", StringComparison.Ordinal) > 0
				? pathPostfix.Replace("[0]", "[" + runtimeIndex + "]")
				: pathPostfix;
			return System.IO.Path.GetFileName(dumpPath) + postfix;
		}


		public static string GetMapFolder(string dmpFilePath)
		{
			var dumpFile = System.IO.Path.GetFileName(dmpFilePath);
			var dumpDir = System.IO.Path.GetDirectoryName(dmpFilePath);
			return dumpDir + System.IO.Path.DirectorySeparatorChar + dumpFile + ".map";
		}


		public static string GetOuputFolder(string dmpPath)
		{
		 return System.IO.Path.GetDirectoryName(dmpPath) // dump file directory
									  + System.IO.Path.DirectorySeparatorChar
									  + System.IO.Path.GetFileName(dmpPath) + ".map" // dummp file name with extension changed tp ".map"
									  + System.IO.Path.DirectorySeparatorChar
									  + "ad-hoc.queries";
		}

		public static string GetOutputPath(string dmpPath, string fileName)
		{
			return GetOuputFolder(dmpPath) + System.IO.Path.DirectorySeparatorChar + fileName;
		}

		public static string GetOutputPath(DumpFileMoniker monkr, string fileName)
		{
			return GetOuputFolder(monkr.Path) + System.IO.Path.DirectorySeparatorChar + fileName;
		}

		public static string GetOutputPathWithDumpPrefix(DumpFileMoniker monkr, string fileName)
		{
			return GetOuputFolder(monkr.Path) + System.IO.Path.DirectorySeparatorChar + monkr.FileName + "." + fileName;
		}

        public static string GetFileDistinctPath(string folderPath, string fileName)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath)) return null;
            fileName = DumpFileMoniker.GetValidFileName(fileName);
            string path = folderPath;
            if (folderPath[folderPath.Length - 1] != System.IO.Path.DirectorySeparatorChar) path = path + System.IO.Path.DirectorySeparatorChar;
            path = path + fileName;
            int ndx = 1;
            while (File.Exists(path))
            {
                string num = "(" + ndx + ")";
                ++ndx;
                int pos = path.LastIndexOf('.');
                path = path.Insert(pos - 1, num);
            }
            return path;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string GetFileName(string path)
		{
			if (string.IsNullOrWhiteSpace(path)) return string.Empty;
			return System.IO.Path.GetFileName(path);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string GetMapName(string path)
		{
			if (string.IsNullOrWhiteSpace(path)) return string.Empty;
			return System.IO.Path.GetFileName(path) + ".map";
		}

        public static HashSet<char> InvalidPathChars = new HashSet<char>(System.IO.Path.GetInvalidPathChars());

        public static string GetValidFileName(string name)
        {
            bool found = false;
            for (int i = 0, icnt = name.Length; i < icnt; ++i)
            {
                if (InvalidPathChars.Contains(name[i]))
                {
                    found = true;
                    break;
                }
            }
            if (!found) return name;
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
            sb.Append(name);
            for (int i = 0, icnt = name.Length; i < icnt; ++i)
            {
                if (InvalidPathChars.Contains(name[i]))
                {
                    sb[i] = '_';
                }
            }
            return StringBuilderCache.GetStringAndRelease(sb);
        }
    }
}
