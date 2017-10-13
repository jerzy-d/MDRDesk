﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Configuration;
using System.Text;

namespace ClrMDRIndex
{
    public class Setup
    {
        public enum RecentFiles
        {
            Unknown,
            Adhoc,
            Map,
            MaxCount = 5
        }

        public static string DacFolder { get; private set; }
        public static string ProcDumpFolder { get; private set; }
        public static string DumpsFolder { get; private set; }
        public static string HelpFolder { get; private set; }
        public static string WndDbgFolder { get; private set; }

        public static bool HasWndDbgFolder => !string.IsNullOrEmpty(WndDbgFolder);

        public static List<string> RecentIndexList { get; private set; }
        public static List<string> RecentAdhocList { get; private set; }

        public static string GraphDbJar { get; private set; }
        public static int GraphPort { get; private set; }

        public static string TypesDisplayMode { get; private set; }
         public static bool SkipReferences { get; private set; }

         public static void SetDacFolder(string folder)
        {
            DacFolder = folder;
        }

        public static void SetDumpsFolder(string folder)
        {
            DumpsFolder = folder;
        }
        public static void SetProcdumpFolder(string folder)
        {
            ProcDumpFolder = folder;
        }

        public static void SetHelpFolder(string folder)
        {
            HelpFolder = folder;
        }

        public static void SetRecentIndexList(List<string> lst)
        {
            RecentIndexList = lst;
        }

        public static void SetRecentAdhocList(List<string> lst)
        {
            RecentAdhocList = lst;
        }

        public static void SetTypesDisplayMode(string mode)
        {
            TypesDisplayMode = mode;
        }

        public static void SetSkipIndexingRefs(bool skip)
        {
            SkipReferences = skip;
        }

        public static void AddRecentFileList(string path, RecentFiles files)
        {
            if (files == RecentFiles.Adhoc)
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
            if (files == RecentFiles.Adhoc)
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
            DacFolder = string.Empty;
            DumpsFolder = string.Empty;
            ProcDumpFolder = string.Empty;
            RecentIndexList = new List<string>();
            RecentAdhocList = new List<string>();
            StringBuilder errors = StringBuilderCache.Acquire(256);

            try
            {
#if DEBUG
                string myfolder = @"D:\Jerzy\WinDbgStuff\MDRDesk\";
#else
                string myfolder = DumpFileMoniker.MyFolder;
#endif
                string installFolder = DumpFileMoniker.GetParentFolder(myfolder);
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var appSettings = (AppSettingsSection)config.GetSection("appSettings");
                if (appSettings.Settings.Count != 0)
                {
                    foreach (string key in appSettings.Settings.AllKeys)
                    {
                        var ky = key.ToLower();
                        if (Utils.SameStrings(ky, "dacfolder"))
                        {
                            var folder = appSettings.Settings[key].Value.Trim();
                            if (Directory.Exists(folder)) DacFolder = folder;
                            else errors.AppendLine("Dac folder does not exist: " + folder);
                        }
                        else if (Utils.SameStrings(ky, "mapfolder"))
                        {
                            var folder = appSettings.Settings[key].Value.Trim();
                            if (Directory.Exists(folder)) DumpsFolder = folder;
                            else
                            {
                                // if dumps folder exists, add it to config
                                string path = installFolder + @"dumps";
                                if (Directory.Exists(path))
                                {
                                    DumpsFolder = folder;
                                }
                                else
                                {
                                    errors.AppendLine("Dumps folder does not exist: " + folder);
                                }
                            }
                        }
                        else if (Utils.SameStrings(ky, "procdumpfolder"))
                        {
                            var folder = appSettings.Settings[key].Value.Trim();
                            if (Directory.Exists(folder)) ProcDumpFolder = folder;
                            else errors.AppendLine("procdum.exe folder does not exist: " + folder);
                        }
                        else if (Utils.SameStrings(ky, "helpfolder"))
                        {
                            var folder = appSettings.Settings[key].Value.Trim();
                            if (Directory.Exists(folder)) HelpFolder = folder;
                            else errors.AppendLine("help folder does not exist: " + folder);
                        }
                        else if (Utils.SameStrings(ky, "wnddbgfolder"))
                        {
                            var folder = appSettings.Settings[key].Value.Trim();
                            if (Directory.Exists(folder)) WndDbgFolder = folder;
                            //else errors.AppendLine("help folder does not exist: " + folder);
                        }
                        else if (Utils.SameStrings(ky, "graphproxy"))
                        {
                            GraphDbJar = appSettings.Settings[key].Value.Trim();
                        }
                        else if (Utils.SameStrings(ky, "graphport"))
                        {
                            GraphPort = Int32.Parse(appSettings.Settings[key].Value.Trim());
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
                        else if (Utils.SameStrings(ky, "skipreferences"))
                        {
                            var intStr = appSettings.Settings[key].Value.Trim();
                            if (!string.IsNullOrWhiteSpace(intStr))
                            {
                                if (string.Compare(intStr, "true", StringComparison.OrdinalIgnoreCase) == 0)
                                    SkipReferences = true;
                            }
                        }
                    }
                }
                else
                {
                    error = "The appSettings section is empty.";
                }
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
                var settings = config.AppSettings.Settings;
                settings["typedisplaymode"].Value = TypesDisplayMode;
                settings["recentindices"].Value = JoinSemicolonDelimitedList(RecentIndexList);
                settings["recentadhocs"].Value = JoinSemicolonDelimitedList(RecentAdhocList);
                settings["dacfolder"].Value = DacFolder;
                settings["mapfolder"].Value = DumpsFolder;
                settings["procdumpfolder"].Value = ProcDumpFolder;
                settings["helpfolder"].Value = HelpFolder;
                var skip = SkipReferences ? "true" : "false";
                if (settings["skipreferences"] == null)
                    settings.Add("skipreferences", skip);
                else
                    settings["skipreferences"].Value = skip;
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
            return string.Join(";", lst);
        }

        public static string GetRecentIndexPath(int ndx = 0)
        {
            if (RecentIndexList != null && RecentIndexList.Count > ndx)
            {
                return RecentIndexList[ndx];
            }
            return null;
        }


        public static string GetRecentAdhocPath(int ndx = 0)
        {
            if (RecentAdhocList != null && RecentAdhocList.Count > ndx)
            {
                return RecentAdhocList[ndx];
            }
            return null;
        }
    }
}
