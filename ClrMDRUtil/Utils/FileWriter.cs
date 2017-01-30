using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace ClrMDRIndex
{

	public class FileWriter
	{
		private FileStream _file;
		private byte[] _buffer;
		private byte[] _wbuf;
		private int _wmax;
		private int _woff;

		public FileWriter(string path)
		{
			_file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
			_buffer = new byte[8];
		}

		public FileWriter(string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
		{
			_file = new FileStream(path, fileMode, fileAccess, fileShare);
			_buffer = new byte[8];
		}

		public FileWriter(string path, int bufSize, FileOptions fopt)
		{
			_file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, bufSize, fopt);
			_buffer = new byte[8];
		}

		public FileWriter(string path, int bufSize)
		{
			_file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufSize*2, FileOptions.SequentialScan);
			_buffer = new byte[8];
			_wbuf = new byte[bufSize];
			_wmax = bufSize;
			_woff = 0;
		}

		public void WriteIntArray(int[] lst, int count)
		{
			Debug.Assert(_woff<=_wmax);
			if (_woff == _wmax)
			{
				_file.Write(_wbuf, 0, _woff);
				_woff = 0;
			}
			int toWrite = count * sizeof(int);
			int ndx = 0;
			while (toWrite > 0)
			{
				if (_woff == _wmax)
				{
					_file.Write(_wbuf, 0, _woff);
					_woff = 0;
				}
				FillBuffer(lst[ndx++], _wbuf, _woff);
				_woff += sizeof(int);
				toWrite -= sizeof(int);
			}
		}

		public void WriteReferenceRecord(int head, IList<int> lst)
		{
			Write(head);
			Write(lst.Count);
			for (int i = 0, icnt = lst.Count; i < icnt; ++i)
			{
				Write(lst[i]);
			}
		}

		public void WriteReferenceRecord(int head, int count, int off, int[] buf)
		{
			Write(head);
			Write(count);
			for (int i = 0; i < count; ++i,++off)
			{
				Write(buf[off]);
			}
		}

		public void FlushBuffer()
		{
			if (_woff > 0)
			{
				_file.Write(_wbuf,0,_woff);
				_woff = 0;
			}
			_file.Flush();
		}

		public void Write(int value)
        {
            _buffer[0] = (byte) value;
            _buffer[1] = (byte) (value >> 8);
            _buffer[2] = (byte) (value >> 16);
            _buffer[3] = (byte) (value >> 24);
            _file.Write(_buffer, 0, 4);
        }

		public void Write(uint value)
        {
            _buffer[0] = (byte) value;
            _buffer[1] = (byte) (value >> 8);
            _buffer[2] = (byte) (value >> 16);
            _buffer[3] = (byte) (value >> 24);
            _file.Write(_buffer, 0, 4);
        }

		public void Write(long value)
        {
            _buffer[0] = (byte) value;
            _buffer[1] = (byte) (value >> 8);
            _buffer[2] = (byte) (value >> 16);
            _buffer[3] = (byte) (value >> 24);
            _buffer[4] = (byte) (value >> 32);
            _buffer[5] = (byte) (value >> 40);
            _buffer[6] = (byte) (value >> 48);
            _buffer[7] = (byte) (value >> 56);
            _file.Write(_buffer, 0, 8);
        }
 
		public void Write(ulong value)
        {
            _buffer[0] = (byte) value;
            _buffer[1] = (byte) (value >> 8);
            _buffer[2] = (byte) (value >> 16);
            _buffer[3] = (byte) (value >> 24);
            _buffer[4] = (byte) (value >> 32);
            _buffer[5] = (byte) (value >> 40);
            _buffer[6] = (byte) (value >> 48);
            _buffer[7] = (byte) (value >> 56);
            _file.Write(_buffer, 0, 8);
        }

        public int Write(int head, int[] data, byte[] buffer)
        {
			Debug.Assert(buffer.Length > 12);
        	int bufLen = buffer.Length;
        	int off = 0;
        	FillBuffer(head,buffer,off);
        	off += 4;
        	int len = data.Length;
        	FillBuffer(len,buffer,off);
        	off += 4;
        	for (int i = 0; i < len; ++i)
        	{
				if (off >= bufLen)
				{
					_file.Write(buffer, 0, off);
					off = 0;
				}
				FillBuffer(data[i],buffer,off);
        		off += 4;
        	}
			if (off > 0)
				_file.Write(buffer, 0, off);
			return sizeof(int) * (len + 2);
		}

		public int Write(int head, int dcnt, int[] data, byte[] buffer)
		{
			Debug.Assert(buffer.Length > 12);
			int bufLen = buffer.Length;
			int off = 0;
			FillBuffer(head, buffer, off);
			off += 4;
			FillBuffer(dcnt, buffer, off);
			off += 4;
			for (int i = 0; i < dcnt; ++i)
			{
				if (off >= bufLen)
				{
					_file.Write(buffer, 0, off);
					off = 0;
				}
				FillBuffer(data[i], buffer, off);
				off += 4;
			}
			if (off > 0)
				_file.Write(buffer, 0, off);
			return sizeof(int) * (dcnt + 2);
		}

		public int Write(int[] data, byte[] buffer)
		{
			int len = data.Length;
			int bufLen = buffer.Length;
			FillBuffer(len, buffer, 0);
			int off = 4;
			for (int i = 0; i < len; ++i)
			{
				if (off >= bufLen)
				{
					_file.Write(buffer, 0, off);
					off = 0;
				}
				FillBuffer(data[i], buffer, off);
				off += 4;
			}
			if (off > 0)
				_file.Write(buffer, 0, off);
			return sizeof(int) * (len+1);
		}

		public void WriteBytes(byte[] buffer, int start, int len)
		{
			_file.Write(buffer,start,len);
		}

        public int ReadInt32()
        {
        	_file.Read(_buffer,0,4);
        	return (int)(_buffer[0] | _buffer[1] << 8 | _buffer[2] << 16 | _buffer[3] << 24);
        }

        public uint ReadUInt32()
        {
        	_file.Read(_buffer,0,4);
        	return (uint)(_buffer[0] | _buffer[1] << 8 | _buffer[2] << 16 | _buffer[3] << 24);
        }

        public virtual long ReadInt64() {
        	_file.Read(_buffer,0,8);
            uint lo = (uint)(_buffer[0] | _buffer[1] << 8 | _buffer[2] << 16 | _buffer[3] << 24);
            uint hi = (uint)(_buffer[4] | _buffer[5] << 8 | _buffer[6] << 16 | _buffer[7] << 24);
            return (long) ((ulong)hi) << 32 | lo;
        }

        public virtual ulong ReadUInt64() {
        	_file.Read(_buffer,0,8);
            uint lo = (uint)(_buffer[0] | _buffer[1] << 8 | _buffer[2] << 16 | _buffer[3] << 24);
            uint hi = (uint)(_buffer[4] | _buffer[5] << 8 | _buffer[6] << 16 | _buffer[7] << 24);
            return ((ulong)hi) << 32 | lo;
        }

        public void FillBuffer(int val, byte[] buffer, int off)
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