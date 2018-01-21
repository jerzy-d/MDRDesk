using System;
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
using System.Net;

namespace MDRDeskInstaller
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string _myFolder;
        public MainWindow()
        {
            InitializeComponent();
            string error;
            _myFolder = AppDomain.CurrentDomain.BaseDirectory;


            var lockedFiles = CheckFiles(_myFolder, out error);


            string tempFolder = _myFolder + @"/Temp";
            Directory.CreateDirectory(tempFolder);
            string mdrDeskZip = tempFolder + @"/MDRDesk.zip";

            WebClient client = new WebClient();
            client.DownloadFile(@"https://github.com/jerzy-d/MDRDesk/releases/download/v1.0-test.0/MDRDesk.zip", mdrDeskZip);
            System.IO.Compression.ZipFile.ExtractToDirectory(mdrDeskZip, tempFolder);



        }

        public string GetTemporaryDirectory()
        {
            string tempDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
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
                    if (file.Name.StartsWith("MDRDeskInstaller")) continue;
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

    }
}
