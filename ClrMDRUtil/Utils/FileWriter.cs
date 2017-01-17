using System.IO;

namespace ClrMDRIndex
{

	public class FileWriter
	{
		FileStream _file;
		byte[] _buffer;

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

        public void Write(int head, int[] data, byte[] buffer)
        {
        	int totalLen = sizeof(int)*2 + sizeof(int)*data.Length;
        	if ( buffer.Length < totalLen)
        		buffer = new byte[totalLen];
        	int off = 0;
        	FillBuffer(head,buffer,off);
        	off += 4;
        	int len = data.Length;
        	FillBuffer(len,buffer,off);
        	off += 4;
        	for (int i = 0; i < len; ++i)
        	{
        		FillBuffer(data[i],buffer,off);
        		off += 4;
        	}
            _file.Write(buffer, 0, totalLen);
        }

		public void Write(int head, int dcnt, int[] data, byte[] buffer)
		{
			int totalLen = sizeof(int) * 2 + sizeof(int) * dcnt;
			if (buffer.Length < totalLen)
				buffer = new byte[totalLen];
			int off = 0;
			FillBuffer(head, buffer, off);
			off += 4;
			FillBuffer(dcnt, buffer, off);
			off += 4;
			for (int i = 0; i < dcnt; ++i)
			{
				FillBuffer(data[i], buffer, off);
				off += 4;
			}
			_file.Write(buffer, 0, totalLen);
		}

		public int Write(int[] data, byte[] buffer)
		{
			int totalLen = sizeof(int) + sizeof(int) * data.Length;
			if (buffer.Length < totalLen)
				buffer = new byte[totalLen];
			int off = 0;
			int len = data.Length;
			FillBuffer(len, buffer, off);
			off += 4;
			FillBuffer(len, buffer, off);
			for (int i = 0; i < len; ++i)
			{
				FillBuffer(data[i], buffer, off);
				off += 4;
			}
			_file.Write(buffer, 0, totalLen);
			return totalLen;
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