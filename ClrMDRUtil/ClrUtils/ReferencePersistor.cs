using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using ClrMDRUtil;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	public class ReferencePersistor
	{
		private string _error;
		public string Error => _error;
		private const int MemoryOrDiskTreshold = 30000000; // 30,000,000
		private const int FileBufferSize = 8192;
		private readonly BlockingCollection<KeyValuePair<int, int[]>> _dataQue;
		private readonly int _totalCount;
		private Thread _thread;
		private readonly string _rToFPath;
		private readonly string _fToRPath;

		public ReferencePersistor(string rToFPath, string fToRPath, int count, BlockingCollection<KeyValuePair<int, int[]>> dataQue)
		{
			_dataQue = dataQue;
			_totalCount = count;
			_rToFPath = rToFPath;
			_fToRPath = fToRPath;
		}

		public void Start()
		{
			if (_totalCount <= MemoryOrDiskTreshold)
			{
				_thread = new Thread(InMemory) { IsBackground = true, Name = "ReferencePersistor" };
				_thread.Start();
			}
		}

		public void Wait()
		{
			_dataQue.Add(new KeyValuePair<int, int[]>(-1, null));
			_thread?.Join();
		}

		private void InMemory()
		{
			FileWriter fw = null;

			try
			{
				IntArrayStore fToR = new IntArrayStore(_totalCount);
				fw = new FileWriter(_rToFPath);
				byte[] fwBuffer = new byte[FileBufferSize];
				fw.Write(0);
				int recCount = 0;
				while (true)
				{
					var kv = _dataQue.Take();
					if (kv.Key < 0) break; // end of data

					fw.Write(kv.Key, kv.Value, fwBuffer);
					++recCount;
					for (int i = 0, icnt = kv.Value.Length; i < icnt; ++i)
					{
						fToR.Add(kv.Value[i], kv.Key);
					}
				}
				fw.GotoBegin();
				fw.Write(recCount);
				fToR.Dump(_fToRPath, out _error);
			}
			catch (Exception ex)
			{
				_error = Utils.GetExceptionErrorString(ex);
			}
			finally
			{
				fw?.Dispose();
			}
		}

		private void OnDisk()
		{
			FileWriter fw = null;
			FileReader fr = null;

			try
			{
				fw = new FileWriter(_rToFPath);
				var fcounts = new int[_totalCount];
				long totSize = sizeof(int); // entries size
				byte[] buffer = new byte[FileBufferSize];
				fw.Write(0);
				int recCount = 0;
				while (true)
				{
					var kv = _dataQue.Take();
					if (kv.Key < 0) break; // end of data

					fw.Write(kv.Key, kv.Value, buffer);
					++recCount;
					for (int i = 0, icnt = kv.Value.Length; i < icnt; ++i)
					{
						var fndx = kv.Value[i];
						var fcnt = fcounts[fndx];
						if (fcnt == 0)
						{
							totSize += sizeof(int) * 2;
						}
						totSize += sizeof(int);
						fcounts[fndx] = fcnt + 1;
						fcounts[fndx] = fcnt;
					}
				}
				fw.GotoBegin();
				fw.Write(recCount);
				fw.Dispose();
				fw = null;

				long[] offsets = new long[_totalCount];
				long curOff = sizeof(int); // first record offset is after record count entry
				for (int i = 0, icnt = _totalCount; i < icnt; ++i)
				{
					if (fcounts[i] == 0) continue;
					offsets[i] = curOff;
					curOff += (fcounts[i] + 2) * sizeof(int); // head, list count, and list items
				}

				fw = new FileWriter(_fToRPath);
				fw.SetLength(totSize);
				fw.GotoBegin();
				fw.Write(0);

				fr = new FileReader(_rToFPath, FileBufferSize, FileOptions.SequentialScan);
				var totRcnt = fr.ReadInt32();
				int totfcnt = 0;
				for (int i = 0; i < totRcnt; ++i)
				{
					fr.ReadInt32Bytes(buffer, 0);
					var rcnt = fr.ReadInt32();
					for (int j = 0; j < rcnt; ++j)
					{
						var n = fr.ReadInt32();
						var off = offsets[n];
						fw.Seek(off, SeekOrigin.Begin);
						var fcnt = fcounts[n];
						if (fcnt > 0)
						{
							fw.Write(n);
							fw.Write(fcounts[n]);
							fw.WriteBytes(buffer, 0, 4);
							offsets[n] = off + sizeof(int) * 3;
							fcounts[n] = 0; // so we know that this entry's head and count are already written
							++totfcnt;
							continue;
						}
						fw.WriteBytes(buffer, 0, 4);
						offsets[n] = off + sizeof(int);
						fcounts[n] = fcnt + 1;
					}
				}
				fw.GotoBegin();
				fw.Write(totfcnt);
			}
			catch (Exception ex)
			{
				_error = Utils.GetExceptionErrorString(ex);
			}
			finally
			{
				fw?.Dispose();
				fr?.Dispose();
			}
		}
	}
}
