using System;
using System.Collections.Generic;
using System.IO;
using System.Configuration;
using System.Text;

namespace ClrMDRIndex
{
	public class Setup
	{
	    public enum RecentFiles : int
	    {
	        Dump,
            Adhoc,
            Map,
            MaxCount = 5
	    }

		public static string Error { get; private set; }
		public static string DacPathFolder { get; private set; }

		static string[] _dacPaths;
		static string[] _dumpPaths;

		public static string PrivateDacFolder { get; private set; }
		public static string LastDump { get; private set; }

        public static List<string> RecentDumpList { get; private set; }
        public static List<string> RecentIndexList { get; private set; }
        public static List<string> RecentAdhocList { get; private set; }

        public static string MapFolder { get; private set; }
		public static string DacFilePath { get; private set; }
		public static string GraphDbJar { get; private set; }
		public static int GraphPort { get; private set; }

        public static string TypesDisplayMode { get; private set; }

        public static void SetDacPathFolder(string folder)
		{
			DacPathFolder = folder;
		}

	    public static void SetTypesDisplayMode(string mode)
	    {
	        TypesDisplayMode = mode;
	    }

	    public static void AddRecentFileList(string path, RecentFiles files )
	    {
	        if (files == RecentFiles.Dump)
	        {
	            if (RecentDumpList.Count > (int)RecentFiles.MaxCount)
                    RecentDumpList.RemoveAt(RecentDumpList.Count-1);
                RecentDumpList.Insert(0, path);
            }
            else if (files == RecentFiles.Adhoc)
            {
                if (RecentAdhocList.Count > (int)RecentFiles.MaxCount)
                    RecentAdhocList.RemoveAt(RecentAdhocList.Count - 1);
                RecentAdhocList.Insert(0, path);
            }
            else if (files == RecentFiles.Map)
            {
                if (RecentIndexList.Count > (int)RecentFiles.MaxCount)
                    RecentIndexList.RemoveAt(RecentIndexList.Count - 1);
                RecentIndexList.Insert(0, path);
            }
        }

        public static void ResetRecentFileList(IList<string> paths, RecentFiles files)
        {
            if (files == RecentFiles.Dump)
            {
                RecentDumpList.Clear();
                RecentDumpList.AddRange(paths);
            }
            else if (files == RecentFiles.Adhoc)
            {
                RecentAdhocList.Clear();
                RecentAdhocList.AddRange(paths);
            }
            else if (files == RecentFiles.Map)
            {
                RecentIndexList.Clear();
                RecentIndexList.AddRange(paths);
            }
        }


        public static bool GetConfigSettings(out string error)
		{
			error = null;
			PrivateDacFolder = string.Empty;
			LastDump = string.Empty;
            RecentDumpList = new List<string>();
            RecentIndexList = new List<string>();
            RecentAdhocList = new List<string>();
            StringBuilder errors = StringBuilderCache.Acquire(256);

            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var appSettings = (AppSettingsSection) config.GetSection("appSettings");
                if (appSettings.Settings.Count != 0)
                {
                    foreach (string key in appSettings.Settings.AllKeys)
                    {
                        var ky = key.ToLower();
                        if (Utils.SameStrings(ky, "dacfolder"))
                        {
                            PrivateDacFolder = appSettings.Settings[key].Value.Trim();
                        }
                        else if (Utils.SameStrings(ky, "mapfolder"))
                        {
                            MapFolder = appSettings.Settings[key].Value.Trim();
                            if (!Directory.Exists(MapFolder))
                                errors.AppendLine("MapFolder does not exist: " + MapFolder);
                        }
                        else if (Utils.SameStrings(ky, "graphproxy"))
                        {
                            GraphDbJar = appSettings.Settings[key].Value.Trim();
                        }
                        else if (Utils.SameStrings(ky, "graphport"))
                        {
                            GraphPort = Int32.Parse(appSettings.Settings[key].Value.Trim());
                        }
                        else if (Utils.SameStrings(ky, "recentdumps"))
                        {
                            GetSemicolonDelimitedFilePaths(RecentDumpList, appSettings.Settings[key].Value);
                        }
                        else if (Utils.SameStrings(ky, "recentindices"))
                        {
							GetSemicolonDelimitedFolderPaths(RecentIndexList, appSettings.Settings[key].Value);
                        }
                        else if (Utils.SameStrings(ky, "recentadhocs"))
                        {
                            GetSemicolonDelimitedFilePaths(RecentAdhocList, appSettings.Settings[key].Value);
                        }
                        else if (Utils.SameStrings(ky, "typedisplaymode"))
                        {
                            TypesDisplayMode = appSettings.Settings[key].Value.Trim();
                        }
                    }
                }
                else
                {
                    error = "The appSettings section is empty.";
                }

                DacPathFolder = PrivateDacFolder;
                if (!Directory.Exists(DacPathFolder))
                    errors.AppendLine("DacPathFolder does not exist: " + DacPathFolder);
                if (errors.Length > 0) return false;
                return true;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return false;
            }
            finally
            {
                if (errors.Length > 0)
                {
                    error = "Initialization Failed" + Constants.HeavyGreekCrossPadded
                            + "Setup.GetConfigSettings()" + Constants.HeavyGreekCrossPadded
                            + "MDRDesk application config file is invalid." + Environment.NewLine + "See details." +
                            Constants.HeavyGreekCrossPadded
                            + StringBuilderCache.GetStringAndRelease(errors);
                }
                else
                {
                    StringBuilderCache.Release(errors);
                }
            }
		}

        public static bool SaveConfigSettings(out string error)
        {
            error = null;
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings["typedisplaymode"].Value = TypesDisplayMode;
                config.AppSettings.Settings["recentdumps"].Value = JoinSemicolonDelimitedList(RecentDumpList);
                config.AppSettings.Settings["recentindices"].Value = JoinSemicolonDelimitedList(RecentIndexList);
                config.AppSettings.Settings["recentadhocs"].Value = JoinSemicolonDelimitedList(RecentAdhocList);
                config.Save(ConfigurationSaveMode.Modified);
                return true;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return false;
            }
        }

        private static void GetSemicolonDelimitedFilePaths(List<string> lst, string str)
	    {
	        if (string.IsNullOrWhiteSpace(str)) return;
	        str = str.Trim();
            var parts = str.Split(';');
            for (var i = 0; i < parts.Length; ++i)
            {
                if (!string.IsNullOrWhiteSpace(parts[i]))
                {
                    var path = parts[i].Trim();
                    if (File.Exists(path))
                        lst.Add(path);
                }
            }
        }

		private static void GetSemicolonDelimitedFolderPaths(List<string> lst, string str)
		{
			if (string.IsNullOrWhiteSpace(str)) return;
			str = str.Trim();
			var parts = str.Split(';');
			for (var i = 0; i < parts.Length; ++i)
			{
				if (!string.IsNullOrWhiteSpace(parts[i]))
				{
					var path = parts[i].Trim();
					if (Directory.Exists(path))
						lst.Add(path);
				}
			}
		}

		private static string JoinSemicolonDelimitedList(List<string> lst)
        {
            if (lst.Count < 1) return string.Empty;
            return  string.Join(";", lst);
        }

        //public static bool ReadAppSettings()
        //{
        //	try
        //	{
        //		var appSettings = ConfigurationManager.AppSettings;

        //		if (appSettings.Count == 0)
        //		{
        //			_dacPaths = Utils.EmptyArray<string>.Value;
        //			_dumpPaths = Utils.EmptyArray<string>.Value;
        //		}
        //		else
        //		{
        //			List<string> dacs = new List<string>();
        //			List<string> dumps = new List<string>();
        //			foreach (var key in appSettings.AllKeys)
        //			{
        //				if (key.StartsWith("dac")) dacs.Add(appSettings[key]);
        //				else if (key.StartsWith("dump")) dumps.Add(appSettings[key]);
        //			}
        //			_dacPaths = dacs.ToArray();
        //			_dumpPaths = dumps.ToArray();
        //		}
        //		return true;
        //	}
        //	catch (ConfigurationErrorsException ex)
        //	{
        //		Error = "Error reading app settings" + Environment.NewLine + ex.ToString();
        //		return false;
        //	}

        //}
    }
}
