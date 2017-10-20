using ClrMDRIndex;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace dbgdeng
{
    public class DbgEng
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hReservedNull, int dwFlags);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr AddDllDirectory(string lpNewDirectory);

        private const int LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x00000100;
        private const int LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;
        private const int LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400;

        public static bool LoadDebugEngine(string path, out string error)
        {
            error = null;
            var dllPath = Path.Combine(path, "dbgeng.dll");
            try
            {
                var les = AddDllDirectory(path);
                if (les == IntPtr.Zero)
                {
                    int sysError = Marshal.GetLastWin32Error();
                    error = "[DbgEng.LoadDebugEngine]" + Constants.HeavyGreekCrossPadded // caption
                           + "Failed to add dbeng.dll dir: " + Constants.HeavyGreekCrossPadded // heading
                           + "Path: " + path + Environment.NewLine + "Win32 Error: " + sysError + " (" + sysError.ToString("x") + ")" + Constants.HeavyGreekCrossPadded; // text
                    return false;
                }
                var res = LoadLibraryEx(dllPath, IntPtr.Zero, LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
                if (res == IntPtr.Zero)
                {
                    int sysError = Marshal.GetLastWin32Error();
                    error = "[DbgEng.LoadDebugEngine]" + Constants.HeavyGreekCrossPadded // caption
                           + "Failed to load dbeng.dll: " + Constants.HeavyGreekCrossPadded // heading
                           + "Path: " + dllPath + Environment.NewLine + "Win32 Error: " + sysError + " (" + sysError.ToString("x") + ")" + Constants.HeavyGreekCrossPadded; // text
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return false;
            }
        }
    }
}
