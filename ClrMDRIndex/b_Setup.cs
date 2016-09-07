using System;
using System.Collections.Generic;
using System.Configuration;

namespace ClrMDRIndex
{
	public class Setup
	{
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

		public static bool GetConfigSettings(out string error)
		{
			error = null;
			PrivateDacFolder = string.Empty;
			LastDump = string.Empty;
            RecentDumpList = new List<string>();
            RecentIndexList = new List<string>();
            RecentAdhocList = new List<string>();

            try
			{
				var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
				var appSettings = (AppSettingsSection)config.GetSection("appSettings");
				if (appSettings.Settings.Count != 0)
				{
					foreach (string key in appSettings.Settings.AllKeys)
					{
						var ky = key.ToLower();
						if (Utils.SameStrings(ky,"dacfolder"))
						{
							PrivateDacFolder = appSettings.Settings[key].Value.Trim();
						}
						else if (Utils.SameStrings(ky,"lastdump"))
						{
							LastDump = appSettings.Settings[key].Value.Trim();
						}
						else if (Utils.SameStrings(ky,"mapfolder"))
						{
							MapFolder = appSettings.Settings[key].Value.Trim();
						}
						else if (Utils.SameStrings(ky,"graphproxy"))
						{
							GraphDbJar = appSettings.Settings[key].Value.Trim();
						}
						else if (Utils.SameStrings(ky,"graphport"))
						{
							GraphPort = Int32.Parse(appSettings.Settings[key].Value.Trim());
						}
                        else if (Utils.SameStrings(ky, "recentdumps"))
                        {
                            SetSemicolonDelimitedList(RecentDumpList, appSettings.Settings[key].Value);
                        }
                        else if (Utils.SameStrings(ky, "recentindices"))
                        {
                            SetSemicolonDelimitedList(RecentIndexList, appSettings.Settings[key].Value);
                        }
                        else if (Utils.SameStrings(ky, "recentadhocs"))
                        {
                            SetSemicolonDelimitedList(RecentAdhocList, appSettings.Settings[key].Value);
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
				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}

	    private static void SetSemicolonDelimitedList(List<string> lst, string str)
	    {
	        if (string.IsNullOrWhiteSpace(str)) return;
	        str = str.Trim();
            var parts = str.Split(';');
            for (var i = 0; i < parts.Length; ++i)
            {
                if (!string.IsNullOrWhiteSpace(parts[i]))
                {
                    lst.Add(parts[i].Trim());
                }
            }
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
