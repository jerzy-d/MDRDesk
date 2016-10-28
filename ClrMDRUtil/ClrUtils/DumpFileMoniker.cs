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
		private string _path;

		public string Path => _path;

		public string FileName => System.IO.Path.GetFileName(_path);

		public string FileNameNoExt => System.IO.Path.GetFileNameWithoutExtension(_path);
		 
		public string OutputFolder => System.IO.Path.GetDirectoryName(_path) // dump file directory
		                              + System.IO.Path.DirectorySeparatorChar
		                              + System.IO.Path.GetFileNameWithoutExtension(_path) + ".map" // dummp file name with extension changed tp ".map"
		                              + System.IO.Path.DirectorySeparatorChar
		                              + "ad-hoc.queries";

		public DumpFileMoniker(string path)
		{
			_path = path;
		}

		public static Tuple<string, string> GetAndCreateMapFolders(string dmpFilePath, out string error)
		{
			error = null;
			try
			{
				var dumpFile = System.IO.Path.GetFileNameWithoutExtension(dmpFilePath);
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
				var dumpFile = System.IO.Path.GetFileNameWithoutExtension(dmpFilePath);
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
			return System.IO.Path.GetFileNameWithoutExtension(dumpPath) + postfix;
		}


		public static string GetMapFolder(string dmpFilePath)
		{
			var dumpFile = System.IO.Path.GetFileNameWithoutExtension(dmpFilePath);
			var dumpDir = System.IO.Path.GetDirectoryName(dmpFilePath);
			return dumpDir + System.IO.Path.DirectorySeparatorChar + dumpFile + ".map";
		}


		public static string GetOuputFolder(string dmpPath)
		{
		 return System.IO.Path.GetDirectoryName(dmpPath) // dump file directory
									  + System.IO.Path.DirectorySeparatorChar
									  + System.IO.Path.GetFileNameWithoutExtension(dmpPath) + ".map" // dummp file name with extension changed tp ".map"
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
			return System.IO.Path.GetFileNameWithoutExtension(path) + ".map";
		}
	}
}
