using System;
using System.Runtime.InteropServices;

namespace ClrMDRIndex.Unsafe
{
    public class FileWriter
    {
        const uint GENERIC_WRITE = 0x40000000;
        const uint CREATE_ALWAYS = 2;
        const uint FILE_BEGIN = 0;
        const uint FILE_CURRENT = 1;
        const uint FILE_END = 2;

        IntPtr handle;

        [DllImport("kernel32", SetLastError = true)]
        static extern unsafe IntPtr CreateFile(
              string FileName,                    // file name
              uint DesiredAccess,                 // access mode
              uint ShareMode,                     // share mode
              uint SecurityAttributes,            // Security Attributes
              uint CreationDisposition,           // how to create
              uint FlagsAndAttributes,            // file attributes
              int hTemplateFile                   // handle to template file
              );

        [DllImport("kernel32", SetLastError = true)]
        static extern unsafe bool WriteFile(
             IntPtr hFile,                       // handle to file
             void* pBuffer,                      // data buffer
             int NumberOfBytesToWrite,           // number of bytes to write
             int* pNumberOfBytesWritten,         // number of bytes read
             int Overlapped                      // overlapped buffer
             );

        [DllImport("kernel32", SetLastError = true)]
        static extern unsafe bool CloseHandle(
              IntPtr hObject   // handle to object
              );

        [DllImport("kernel32", SetLastError = true)]
        static extern unsafe bool SetFilePointerEx(
            IntPtr hFile,
            long liDistanceToMove,
            long* lpNewFilePointer,
            uint dwMoveMethod
            );

        [DllImport("kernel32", SetLastError = true)]
        static extern unsafe bool SetEndOfFile(
            IntPtr hFile
            );



        public bool Open(string FileName)
        {
            // open the existing file for reading          
            handle = CreateFile(
                  FileName,
                  GENERIC_WRITE,
                  0,
                  0,
                  CREATE_ALWAYS,
                  0,
                  0);

            if (handle != IntPtr.Zero)
                return true;
            else
                return false;
        }

        public unsafe int Write(byte[] buffer, int index, int count)
        {
            int n = 0;
            fixed (byte* p = buffer)
            {
                if (!WriteFile(handle, p + index, count, &n, 0))
                    return 0;
            }
            return n;
        }

        public static unsafe bool CreateFileWithSize(string path, long size, out string error)
        {
            error = null;
            FileWriter file = null;
            try
            {
                file = new FileWriter();
                file.Open(path);
                SetFilePointerEx(file.handle, size, null, FILE_BEGIN);
                SetEndOfFile(file.handle);
                file.Close();
                file = null;
                return true;
            }
            catch(Exception ex)
            {
                error = ClrMDRIndex.Utils.GetExceptionErrorString(ex);
                return false;
            }
            finally
            {
                file?.Close();
            }
        }

        public bool Close()
        {
            // close file handle
            return CloseHandle(handle);
        }
    }
}
