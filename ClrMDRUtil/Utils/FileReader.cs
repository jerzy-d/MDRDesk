using System.Collections.Generic;
using System.IO;

namespace ClrMDRIndex
{

	public sealed class FileReader
	{
		FileStream _file;
		byte[] _buffer;

		public FileReader(string path)
		{
			_file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			_buffer = new byte[8];
		}

		public FileReader(string path,int bufSize, FileOptions fopt)
		{
			_file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufSize, fopt);
			_buffer = new byte[8];
		}

		// public void Write(int value)
		//       {
		//           _buffer[0] = (byte) value;
		//           _buffer[1] = (byte) (value >> 8);
		//           _buffer[2] = (byte) (value >> 16);
		//           _buffer[3] = (byte) (value >> 24);
		//           _file.Write(_buffer, 0, 4);
		//       }

		// public void Write(uint value)
		//       {
		//           _buffer[0] = (byte) value;
		//           _buffer[1] = (byte) (value >> 8);
		//           _buffer[2] = (byte) (value >> 16);
		//           _buffer[3] = (byte) (value >> 24);
		//           _file.Write(_buffer, 0, 4);
		//       }

		// public void Write(long value)
		//       {
		//           _buffer[0] = (byte) value;
		//           _buffer[1] = (byte) (value >> 8);
		//           _buffer[2] = (byte) (value >> 16);
		//           _buffer[3] = (byte) (value >> 24);
		//           _buffer[4] = (byte) (value >> 32);
		//           _buffer[5] = (byte) (value >> 40);
		//           _buffer[6] = (byte) (value >> 48);
		//           _buffer[7] = (byte) (value >> 56);
		//           _file.Write(_buffer, 0, 8);
		//       }

		// public void Write(ulong value)
		//       {
		//           _buffer[0] = (byte) value;
		//           _buffer[1] = (byte) (value >> 8);
		//           _buffer[2] = (byte) (value >> 16);
		//           _buffer[3] = (byte) (value >> 24);
		//           _buffer[4] = (byte) (value >> 32);
		//           _buffer[5] = (byte) (value >> 40);
		//           _buffer[6] = (byte) (value >> 48);
		//           _buffer[7] = (byte) (value >> 56);
		//           _file.Write(_buffer, 0, 8);
		//       }

		//       public void Write(int head, int[] data, byte[] buffer)
		//       {
		//       	int totalLen = sizeof(int)*2 + sizeof(int)*data.Lenght;
		//       	if ( buffer.Lenght < totalLen)
		//       		buffer = new byte[totalLen];
		//       	int off = 0;
		//       	FillBuffer(head,buffer,off);
		//       	off += 4;
		//       	int len = data.Lenght;
		//       	FillBuffer(len,buffer,off);
		//       	off += 4;
		//       	for (int i = 0; i < len; ++i)
		//       	{
		//       		FillBuffer(data[i],buffer,off);
		//       		off += 4;
		//       	}
		//           _file.Write(buffer, 0, totalLen);
		//       }

		public int ReadInt32()
        {
        	_file.Read(_buffer,0,4);
        	return (int)(_buffer[0] | _buffer[1] << 8 | _buffer[2] << 16 | _buffer[3] << 24);
        }

		public void ReadInt32Bytes(byte[] buffer, int start)
		{
			_file.Read(buffer, start, 4);
		}

		public uint ReadUInt32()
        {
        	_file.Read(_buffer,0,4);
        	return (uint)(_buffer[0] | _buffer[1] << 8 | _buffer[2] << 16 | _buffer[3] << 24);
        }

        public long ReadInt64() {
        	_file.Read(_buffer,0,8);
            uint lo = (uint)(_buffer[0] | _buffer[1] << 8 | _buffer[2] << 16 | _buffer[3] << 24);
            uint hi = (uint)(_buffer[4] | _buffer[5] << 8 | _buffer[6] << 16 | _buffer[7] << 24);
            return (long) ((ulong)hi) << 32 | lo;
        }

        public ulong ReadUInt64() {
        	_file.Read(_buffer,0,8);
            uint lo = (uint)(_buffer[0] | _buffer[1] << 8 | _buffer[2] << 16 | _buffer[3] << 24);
            uint hi = (uint)(_buffer[4] | _buffer[5] << 8 | _buffer[6] << 16 | _buffer[7] << 24);
            return ((ulong)hi) << 32 | lo;
        }

		public int[] ReadList(long offset, byte[] buffer)
		{
			Seek(offset, SeekOrigin.Begin);
			int cnt = ReadInt32();
			int bufsize = cnt*sizeof(int);
			if (buffer.Length < bufsize)
				buffer = new byte[bufsize];
			_file.Read(buffer, 0, bufsize);
			int[] lst = new int[cnt];
			int off = 0;
			for (int i = 0; i < cnt; ++i)
			{
				lst[i] = GetInt32(buffer, off);
				off += 4;
			}
			return lst;
		}

		public KeyValuePair<int,int[]> ReadList(long offset, int[] ibuf, byte[] buffer)
		{
			Seek(offset, SeekOrigin.Begin);
			int cnt = ReadInt32();
			int bufsize = cnt * sizeof(int);
			if (buffer.Length < bufsize)
			{
				ibuf = new int[cnt];
				buffer = new byte[bufsize];
			}
			_file.Read(buffer, 0, bufsize);
			int off = 0;
			for (int i = 0; i < cnt; ++i)
			{
				ibuf[i] = GetInt32(buffer, off);
				off += 4;
			}
			return new KeyValuePair<int, int[]>(cnt,ibuf);
		}

		public int[] ReadHeadAndList(byte[] buffer, out int head)
		{
			head = ReadInt32();
			int cnt = ReadInt32();
			int bufsize = cnt * sizeof(int);
			if (buffer.Length < bufsize)
				buffer = new byte[bufsize];
			_file.Read(buffer, 0, bufsize);
			int[] lst = new int[cnt];
			int off = 0;
			for (int i = 0; i < cnt; ++i)
			{
				lst[i] = GetInt32(buffer, off);
				off += 4;
			}
			return lst;
		}

		private int GetInt32(byte[] buffer, int offset)
		{
			return (int)(buffer[offset] | buffer[offset+1] << 8 | buffer[offset+2] << 16 | buffer[offset+3] << 24);
		}

		private void FillBuffer(int val, byte[] buffer, int off)
        {
            buffer[off+0] = (byte) val;
            buffer[off+1] = (byte) (val >> 8);
            buffer[off+2] = (byte) (val >> 16);
            buffer[off+3] = (byte) (val >> 24);
        }

        public void Flush()
        {
        	_file.Flush(true);
        }

        public void SetLength(long value)
        {
        	_file.SetLength(value);
        }

		public long Seek(long offset, SeekOrigin origin)
		{
			return _file.Seek(offset, origin);
		}

		public long GotoBegin()
		{
			return _file.Seek(0L, SeekOrigin.Begin);
		}

		public void Close()
		{
			_file.Close();
		}

		public void Dispose()
		{
			_file.Dispose();
		}
	}
}