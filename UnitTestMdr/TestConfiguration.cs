using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClrMDRIndex;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestMdr
{
	public static class TestConfiguration
	{
		public static string Error { get; private set; }
		public static string DumpPath0 = null; // @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Baly\analytics9_1512161604.good.dmp";
		public static string OutPath0 = null; //@"D:\Jerzy\WinDbgStuff\Dumps\Analytics\Baly\analytics9_1512161604.good.map\ad-hoc.queries\";
		public static string MapPath0 = null; //@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Baly\analytics9_1512161604.good.map";
		public static string DumpPath1 = null; // @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Baly\analytics9_1512161604.good.dmp";
		public static string OutPath1 = null; //@"D:\Jerzy\WinDbgStuff\Dumps\Analytics\Baly\analytics9_1512161604.good.map\ad-hoc.queries\";
		public static string MapPath1 = null; //@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Baly\analytics9_1512161604.good.map";
		public static bool SettingsConfigured { get; private set; }
		private static readonly object _lock = new object();


		public static bool ConfigureSettings()
		{
			string error = null;
			if (SettingsConfigured) return true;
			lock (_lock)
			{
				if (!SettingsConfigured)
				{
					if (!Setup.GetConfigSettings(out error))
					{
						Error = error;
						return false;
					}
					if (!GetConfigTestData()) return false;
					SettingsConfigured = true;
					return true;
				}
			}
			return true;
		}

		private static bool GetConfigTestData()
		{
			try
			{
				var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
				var appSettings = (AppSettingsSection)config.GetSection("appSettings");
				int testSettingCount = 0;
				if (appSettings.Settings.Count != 0)
				{
					foreach (string key in appSettings.Settings.AllKeys)
					{
						var ky = key.ToLower();
						if (ky == "test_mapfolder0")
						{
							MapPath0 = appSettings.Settings[key].Value.Trim();
							++testSettingCount;
						}
						else if (ky == "test_dumppath0")
						{
							DumpPath0 = appSettings.Settings[key].Value.Trim();
							++testSettingCount;
						}
						else if (ky == "test_outfolder0")
						{
							OutPath0 = appSettings.Settings[key].Value.Trim();
							++testSettingCount;
						}

						if (ky == "test_mapfolder1")
						{
							MapPath1 = appSettings.Settings[key].Value.Trim();
						}
						else if (ky == "test_dumppath1")
						{
							DumpPath1 = appSettings.Settings[key].Value.Trim();
						}
						else if (ky == "test_outfolder1")
						{
							OutPath1 = appSettings.Settings[key].Value.Trim();
						}
					}
				}
				if (testSettingCount < 3)
				{
					Error = "App.config issue, not all test settings are available.";
				}
				if (MapPath0 != null && !Directory.Exists(MapPath0))
				{
					Directory.CreateDirectory(MapPath0);
				}
				if (OutPath0 != null && !Directory.Exists(OutPath0))
				{
					Directory.CreateDirectory(OutPath0);
				}

				return true;
			}
			catch (Exception ex)
			{
				Error = ex.ToString();
				return false;
			}
		}
	}
}
