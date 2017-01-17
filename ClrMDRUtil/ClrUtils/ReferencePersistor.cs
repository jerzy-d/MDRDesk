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
		private const int MemoryOrDiskTreshold = 5000; // 30000000; // 30,000,000 TODO JRD
		private const int FileBufferSize = 8192;
		private readonly BlockingCollection<KeyValuePair<int, int[]>> _dataQue;
		private readonly int _totalCount;
		private Thread _thread;
		private readonly string _rToFPath;
		private readonly string _fToRPath;

		public ReferencePersistor(string rToFPath, string fToRPath, int count,
			BlockingCollection<KeyValuePair<int, int[]>> dataQue)
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
				_thread = new Thread(InMemory) {IsBackground = true, Name = "MemReferencePersistor"};
				_thread.Start();
			}
			else
			{
				_thread = new Thread(OnDisk) {IsBackground = true, Name = "DiskReferencePersistor"};
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
			try
			{
				IntArrayStore fToR = new IntArrayStore(_totalCount);
				IntArrayStore rToF = new IntArrayStore(_totalCount);
				int recCount = 0;
				while (true)
				{
					var kv = _dataQue.Take();
					if (kv.Key < 0) break; // end of data
					rToF.Add(kv.Key, kv.Value);
					++recCount;
					for (int i = 0, icnt = kv.Value.Length; i < icnt; ++i)
					{
						fToR.Add(kv.Value[i], kv.Key);
					}
				}
				Debug.Assert(rToF.ListHaveNoDuplicates());
				rToF.Dump(_rToFPath, out _error);
				Debug.Assert(fToR.ListHaveNoDuplicates());
				fToR.Dump(_fToRPath, out _error);
			}
			catch (Exception ex)
			{
				_error = Utils.GetExceptionErrorString(ex);
			}
		}

		private void OnDisk()
		{
			FileWriter fw = null;
			FileReader fr = null;


			try
			{
				fw = new FileWriter(_rToFPath + ".data.tmp");
				var fcounts = new int[_totalCount];
				long[] offsets = new long[_totalCount];
				long totSize = sizeof(int); // total size for inverted file, WITH DUPLICATES
				byte[] buffer = new byte[FileBufferSize];
				int recCount = 0;
				long hoff = 0;
				while (true)
				{
					var kv = _dataQue.Take();
					if (kv.Key < 0) break; // end of data
					offsets[kv.Key] = hoff;
					//fw.Seek(kv.Key*sizeof(long) + sizeof(int), SeekOrigin.Begin);
					//fw.Write(hoff);
					++recCount;
					totSize += sizeof(int)*(2 + kv.Value.Length);
					for (int i = 0, icnt = kv.Value.Length; i < icnt; ++i) // saving counts for each field, IT MIGHT CONTAIN DUPLICATES
					{
						var fnx = kv.Value[i];
						var val = fcounts[fnx];
						fcounts[fnx] = val + 1;
					}
					hoff += fw.Write(kv.Value, buffer);
				}
				fw.GotoBegin();
				fw.Write(recCount);
				fw.Dispose();
				fw = null;

				// generate clean _rToFPath file
				//
				int[] ibuf = new int[FileBufferSize/sizeof(int)];

				fr = new FileReader(_rToFPath + ".data.tmp", FileBufferSize, FileOptions.RandomAccess);
				fw = new FileWriter(_rToFPath);
				fw.Write(recCount);
				for (int i = 0, icnt = offsets.Length; i < icnt; ++i)
				{
					if (offsets[i] == 0L) continue;
					var kv = fr.ReadList(offsets[i], ibuf, buffer);
					fw.Write(i, kv.Key, kv.Value, buffer);
				}
				fr.Dispose();
				fr = null;
				fw.Dispose();
				fw = null;
				File.Delete(_rToFPath + ".data.tmp");

				IntArrayStore fToR = new IntArrayStore(_totalCount);
				fToR.InitSlots(fcounts);

				fr = new FileReader(_rToFPath, FileBufferSize, FileOptions.SequentialScan);
				var totRcnt = fr.ReadInt32();
				int totfcnt = 0;
				for (int i = 0; i < totRcnt; ++i)
				{
					var rndx = fr.ReadInt32();
					var rcnt = fr.ReadInt32();
					for (int j = 0; j < rcnt; ++j)
					{
						var n = fr.ReadInt32();
						fToR.Add(n,rndx);
					}
				}
				fr.Dispose();
				fr = null;

				fToR.Dump(_fToRPath, out _error);


#if DEBUG
				//{
				//	StreamWriter sw = null;
				//	try
				//	{
				//		sw = new StreamWriter(_fToRPath + ".counts.tmp.txt");
				//		for (int i = 0, icnt = _totalCount; i < icnt; ++i)
				//		{
				//			if (fcounts[i] == 0) continue;
				//			sw.WriteLine(Utils.SizeStringHeader(i) + Utils.LargeNumberString(fcounts[i]));
				//		}
				//		sw.Close();
				//		sw = null;
				//	}
				//	catch (Exception ex)
				//	{
				//		_error = Utils.GetExceptionErrorString(ex);
				//	}
				//	finally
				//	{
				//		sw?.Close();
				//	}
				//}
#endif

				// revert rToF file
				//
				//fw = new FileWriter(_fToRPath + ".data.tmp",FileBufferSize*4,FileOptions.RandomAccess);
				//fw.SetLength(totSize);
				//fw.GotoBegin();
				//fw.Write(0);

				//hoff = sizeof(int);
				//for (int i = 0, icnt = offsets.Length; i < icnt; ++i)
				//{
				//	if (fcounts[i]==0) continue;
				//	offsets[i] = hoff;
				//	hoff += (fcounts[i] + 2)*sizeof(int);
				//}

				//int[] xcounts = new int[offsets.Length];
				//Array.Copy(fcounts,xcounts,offsets.Length);

				//fr = new FileReader(_rToFPath, FileBufferSize, FileOptions.SequentialScan);
				//var totRcnt = fr.ReadInt32();
				//int totfcnt = 0;
				//for (int i = 0; i < totRcnt; ++i)
				//{
				//	fr.ReadInt32Bytes(buffer, 0);
				//	var rcnt = fr.ReadInt32();
				//	if (rcnt > 1000000)
				//	{
				//		int a = 1;
				//	}
				//	for (int j = 0; j < rcnt; ++j)
				//	{
				//		var n = fr.ReadInt32();
				//		if (n == 0)
				//		{
				//			int a = 1;
				//		}
				//		var off = offsets[n];
				//		fw.Flush();
				//		fw.Seek(off, SeekOrigin.Begin);
				//		var fcnt = fcounts[n];
				//		if (fcnt > 0)
				//		{
				//			fw.Write(n);
				//			fw.Write(fcnt);
				//			fw.WriteBytes(buffer, 0, 4);
				//			offsets[n] = off + sizeof(int) * 3;
				//			fcounts[n] = 0; // so we know that this entry's head and count are already written
				//			++totfcnt;
				//			continue;
				//		}
				//		fw.WriteBytes(buffer, 0, 4);
				//		offsets[n] = off + sizeof(int);
				//	}
				//}
				//fw.GotoBegin();
				//fw.Write(totfcnt);
				//fw.Dispose();
				//fw = null;

//#if DEBUG
//				{
//					StreamWriter sw = null;
//					try
//					{
//						fr = new FileReader(_fToRPath + ".data.tmp", FileBufferSize, FileOptions.SequentialScan);
//						sw = new StreamWriter(_fToRPath + ".data.tmp.txt");
//						int cnt = fr.ReadInt32();
//						int h;
//						var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
//						for (int i = 0, icnt = 150; i < icnt; ++i)
//						{
//							h = fr.ReadInt32();
//							var c = fr.ReadInt32();
//							int[] refs = new int[c];
//							for (int k = 0; k < c; ++k)
//							{
//								refs[k]=fr.ReadInt32();
//							}

//							sw.Write(Utils.SizeStringHeader(i) + Utils.SizeStringHeader(h) + Utils.LargeNumberString(refs.Length));
//							sb.Clear();
//							sb.Append(" | ");
//							for (int j = 0, jcnt = refs.Length; j < jcnt; ++j)
//							{
//								sb.Append(" ").Append(refs[j]);
//							}
//							sw.WriteLine(sb.ToString());
//						}
//						sw.Close();
//						sw = null;
//					}
//					catch (Exception ex)
//					{
//						_error = Utils.GetExceptionErrorString(ex);
//					}
//					finally
//					{
//						sw?.Close();
//						fr?.Dispose();
//					}
//				}
//#endif


//				// generate clean _fToRPath file
//				//
//				fr = new FileReader(_fToRPath + ".data.tmp", FileBufferSize, FileOptions.SequentialScan);
//				fw = new FileWriter(_fToRPath);
//				fw.Write(recCount);
//				recCount = fr.ReadInt32();
//				int head = 0;
//				for (int i = 0; i < recCount; ++i)
//				{
//					int[] refs = fr.ReadHeadAndList(buffer, out head);
//					if (xcounts[head] != refs.Length || head == 0)
//					{
//						int a = 1;
//					}
//					Array.Sort(refs);
//					if (!Utils.AreAllDistinct(refs))
//					{
//						refs = Utils.RemoveDuplicates(refs);
//					}
//					fw.Write(head, refs, buffer);
//				}
//				fr.Dispose();
//				fr = null;
//				fw.Dispose();
//				fw = null;
//				File.Delete(_fToRPath + ".data.tmp");

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
