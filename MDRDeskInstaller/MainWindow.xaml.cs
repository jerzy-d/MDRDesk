﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Net;
using System.Configuration;
using System.Threading;

namespace MDRDeskInstaller
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const char InformationSymbol = '\u2110'; // ℐ SCRIPT CAPITAL I
        public static bool Install = false;

        enum CmpSet
        {
            LT = -1,
            EQ = 0,
            GT = 1,
            ER = 10
        }

        enum UpdtNdx
        {
            Version,
            MDRDeskZip,
            DacVersion,
            DacZip,
            Count
        }

        string _myFolder;
        public string MyFolder => _myFolder;
        string _myVersion;
        string _myDacVersion;
        string _localServerPath;
        public FileInfo[] _lockedFiles;

        IProgress<string> _progress;
        DateTime _time;
        object _progressLock;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {

            string error;
            _myFolder = AppDomain.CurrentDomain.BaseDirectory;
            if (!ReadConfig(out error))
            {
                MessageBox.Show(error, "Cannot Read App Config", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _progressLock = new object();
            _time = DateTime.Now;
            _progress = new Progress<string>(ShowMessage);
            _progress.Report("CURRENT VERSION: " + _myVersion + ", DACS VERSION " + _myDacVersion);

            if (_localServerPath == null || !Directory.Exists(_localServerPath))
            {
                _localServerPath = null;
            }

            Dispatcher.CurrentDispatcher.InvokeAsync(() => Upgrade());

        }

        private async void Upgrade()
        {
            MainStatusLabel.Content = "Checking if update is needed.";
            _progress.Report("Checking available updates.");

            string tempFolder = _myFolder + @"Temp";
            if (Directory.Exists(tempFolder))
            {
                CleanDir(tempFolder);
            }
            else
            {
                Directory.CreateDirectory(tempFolder);
            }

            (string error, bool updateMdrDesk, bool updateDacs, string[] updateInfo) =
            await Task.Factory.StartNew(() =>
            {
                return CheckUpdates(tempFolder);
            });

            if (error != null)
            {

            }

            if(!updateMdrDesk && !updateDacs)
            {
                _progress.Report("Current MDRDesk and dacs are the latest. No need to upgrade.");
                return;
            }

            if (true)
//          if (updateMdrDesk)
            {
                    _progress.Report("Checking if MDRDesk files can be overwritten.");

                _lockedFiles = CheckFiles(_myFolder, out error);
                if (_lockedFiles.Length > 1)
                {
                    FileUsage fileUsage = new FileUsage() { Owner = this, Title = "Locked Files" };
                    fileUsage.ShowDialog();
                }

               

                // TODO JRD temp remove
                return;

                string mdrDeskZip = tempFolder + @"\MDRDesk.zip";
                WebClient client = new WebClient();
                client.DownloadFile(updateInfo[1].Substring("mdrdeskzip::".Length), mdrDeskZip);
                System.IO.Compression.ZipFile.ExtractToDirectory(mdrDeskZip, tempFolder);
            }
        }

        private ValueTuple<string, bool, bool, string[]> CheckUpdates(string folder)
        {
            string error = null;
            List<string> updateInfoLst = GetUpdateInfo(folder, out error);
            if (updateInfoLst == null || error != null)
            {
                return (error, false, false, null);
            }

            string[] updateInfo = new string[(int)UpdtNdx.Count];
            string[] prefixes = new string[]
            {
                "version::",
                "mdrdeskzip::",
                "dacversion::",
                "mscordacwks::"
            };

            for(int i = 0, icnt = updateInfoLst.Count; i < icnt; ++i)
            {
                var item = updateInfoLst[i];
                if (item.StartsWith(prefixes[(int)UpdtNdx.Version]))
                    updateInfo[(int)UpdtNdx.Version] = item.Substring(prefixes[(int)UpdtNdx.Version].Length);
                else if (item.StartsWith(prefixes[(int)UpdtNdx.MDRDeskZip]))
                    updateInfo[(int)UpdtNdx.MDRDeskZip] = item.Substring(prefixes[(int)UpdtNdx.MDRDeskZip].Length);
                else if (item.StartsWith(prefixes[(int)UpdtNdx.DacVersion]))
                    updateInfo[(int)UpdtNdx.DacVersion] = item.Substring(prefixes[(int)UpdtNdx.DacVersion].Length);
                else if (item.StartsWith(prefixes[(int)UpdtNdx.DacZip]))
                    updateInfo[(int)UpdtNdx.DacZip] = item.Substring(prefixes[(int)UpdtNdx.DacZip].Length);
            }
            bool updateMdrDesk = false, updateDacs = false;
            CmpSet cmp = CompareVersions(_myVersion, updateInfo[(int)UpdtNdx.Version], out error);
            if (cmp == CmpSet.ER)
            {
                return (error, false, false, updateInfoLst.ToArray());
            }
            updateMdrDesk = cmp == CmpSet.LT;

            cmp = CompareIntVersion(_myDacVersion, updateInfo[(int)UpdtNdx.DacVersion], out error);
            if (cmp == CmpSet.ER)
            {
                return (error, false, false, updateInfoLst.ToArray());
            }
            updateDacs = cmp == CmpSet.LT;

            return (null, updateMdrDesk, updateDacs, updateInfo);
        }

        public List<string> GetUpdateInfo(string folder, out string error)
        {
            error = null;
            //StreamReader sr = null;
            var updateInfo = new List<string>();
            try
            {
                string ln;
                if (_localServerPath != null) // we have release on our local server
                {
                    string filePath = _localServerPath + @"\CurrentRelease.txt";
                    if (File.Exists(filePath))
                    {
                        if (!ReadUpdateInfo(filePath, updateInfo, out error)) return null;
                        return updateInfo;
                    }
                }

                var path = folder + @"\CurrentRelease.txt";
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                // Skip validation of SSL/TLS certificate
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                WebClient client = new WebClient();
                
                using (client)
                {
                    client.DownloadFile(@"https://github.com/jerzy-d/MDRDesk/releases/download/v.0.0-info.0/CurrentRelease.txt", path);

                }
                client = null;

                if (!ReadUpdateInfo(path, updateInfo, out error)) return null;
                return updateInfo;
            }
            catch(Exception ex)
            {
                error = ex.ToString();
                return null;
            }
        }

        private static bool ReadUpdateInfo(string path, List<string> updList, out string error)
        {
            error = null;
            StreamReader sr = null;
            try
            {
                sr = new StreamReader(path);
                string ln = sr.ReadLine();
                while (ln != null)
                {
                    if (!(string.IsNullOrWhiteSpace(ln) || ln[0] == '#'))
                        updList.Add(ln);
                    ln = sr.ReadLine();
                }
                return true;
            }
            catch(Exception ex)
            {
                error = ex.ToString();
                return false;
            }
            finally
            {
                sr?.Close();
            }
        }

        public string GetTemporaryDirectory()
        {
            string tempDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        public void CheckFiles(out string error)
        {
            _lockedFiles = CheckFiles(_myFolder, out error);
        }

        public static FileInfo[] CheckFiles(string folder, out string error)
        {
            error = null;
            try
            {
                var directory = new DirectoryInfo(folder);
                List<FileInfo> lst = new List<FileInfo>();
                foreach (FileInfo file in directory.GetFiles())
                {
                    if (file.Name.StartsWith("MDRDeskUpdate")) continue;
                    if (IsFileLocked(file)) lst.Add(file);
                }
                return lst.ToArray();
            }
            catch(Exception ex)
            {
                error = ex.ToString();
                return null;
            }
        }

        /// <summary>
        /// This function is used to check specified file being used or not
        /// </summary>
        /// <param name="file">FileInfo of required file</param>
        /// <returns>If that specified file is being processed 
        /// or not found is return true</returns>
        public static Boolean IsFileLocked(FileInfo file)
        {
            FileStream stream = null;
            if (file.IsReadOnly) return true;
            try
            {
                //Don't change FileAccess to ReadWrite, 
                //because if a file is in readOnly, it fails.
                if (file.IsReadOnly)
                {
                    stream = file.Open
                    (
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.None
                    );
                }
                else
                {
                    stream = file.Open
                    (
                        FileMode.Open,
                        FileAccess.ReadWrite,
                        FileShare.None
                    );
                }
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            //file is not locked
            return false;
        }

        void CleanDir(string path)
        {
            System.IO.DirectoryInfo di = new DirectoryInfo(path);
            var stack = new Stack<DirectoryInfo>();
            stack.Push(di);
            List<DirectoryInfo> lst = new List<DirectoryInfo>();
            do
            {
                di = stack.Pop();
                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete();
                }
                foreach (DirectoryInfo dir in di.GetDirectories())
                {
                    stack.Push(dir);
                    lst.Add(dir);
                }
            } while (stack.Count > 0);
            lst.Reverse();
            foreach(var d in lst)
            {
                d.Delete();
            }
        }

        /// <summary>
        /// Append message to the appropriate text box.
        /// Messages are coming from an indexing progess delegete.
        /// </summary>
        /// <param name="msg">Text to display.</param>
        public void ShowMessage(string msg)
        {
            if (string.IsNullOrWhiteSpace(msg) || msg.Length < 1) return;
            lock (_progressLock)
            {
                var dt = DateTime.Now;
                var tmStr = dt.ToLongTimeString();
                // get duration of previous action
                //
                if (!string.IsNullOrEmpty(UpdateProgressText.Text))
                {
                    TimeSpan ts = dt - _time;
                    var duration = DurationString(ts);
                    _time = dt;
                    // append duration to the previous action
                    //
                    UpdateProgressText.AppendText("  DURATION: " + duration + Environment.NewLine);
                }
                // display current message
                //
                msg = tmStr + " : " + msg;
                UpdateProgressText.AppendText(msg);
                UpdateProgressText.ScrollToEnd();
                UpdateProgressText.UpdateLayout();
            }
        }

        public static string DurationString(TimeSpan ts)
        {
            return string.Format(" {0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
        }


        private bool ReadConfig(out string error)
        {
            error = null;
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var appSettings = (AppSettingsSection)config.GetSection("appSettings");
                _localServerPath = appSettings.Settings["localserver"].Value.Trim();
                _myVersion = appSettings.Settings["version"].Value.Trim();
                _myDacVersion = appSettings.Settings["dacversion"].Value.Trim();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
        }

        private void ButtonCancelClicked(object sender, RoutedEventArgs e)
        {

        }


        private static CmpSet CompareVersions(string v1, string v2, out string error)
        {
            error = null;
            int[] v1Vals = GetVersionValues(v1, out error);
            if (error != null) return CmpSet.ER;
            int[] v2Vals = GetVersionValues(v2, out error);
            if (error != null) return CmpSet.ER;
            CmpSet cmp = CmpSet.EQ;
            for (int i = 0; i < 4; ++i)
            {
                cmp = v1Vals[i] < v2Vals[i] ? CmpSet.LT : (v1Vals[i] > v2Vals[i] ? CmpSet.GT : CmpSet.EQ);
                if (cmp != CmpSet.EQ) return cmp;
            }
            return CmpSet.EQ;
        }

        private static int[] GetVersionValues(string ver, out string error)
        {
            error = null;
            string[] valStr= ver.Split(new char[] { '.' });
            if (valStr.Length != 4)
            {
                error = "Version string is corrupted: " + ver;
                return null;
            }
            int[] vals = new int[4];
            for (int i = 0; i < 4; ++i)
            {
                vals[0] = ParseIntString(valStr[0], out error);
                if (error != null) return null;
            }
            return vals;
        }

        private static int ParseIntString(string s, out string error)
        {
            error = null;
            int val;
            if (!Int32.TryParse(s, out val))
            {
                error = "[ParseIntString] Failed to parse: " + s;
                return Int32.MinValue;
            }
            return val;
        }

        private static CmpSet CompareIntVersion(string s1, string s2, out string error)
        {
            error = null;
            int s1Val;
            if (!Int32.TryParse(s1,out s1Val))
            {
                error = "Int32.TryParse failed on the first argument: " + s1;
                return CmpSet.ER;
            }
            int s2Val;
            if (!Int32.TryParse(s2, out s2Val))
            {
                error = "Int32.TryParse failed on the second argument: " + s2;
                return CmpSet.ER;
            }

            return s1Val < s2Val ? CmpSet.LT : (s1Val > s2Val ? CmpSet.GT : CmpSet.EQ);
        }

    }
}
