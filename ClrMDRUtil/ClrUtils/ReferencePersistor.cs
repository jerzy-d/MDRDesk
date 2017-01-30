﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using ClrMDRUtil;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	public class ReferencePersistor
	{
		private string _error;
		public string Error => _error;
		private const int MemoryOrDiskTreshold = 20000000; // = 30000000; // 30,000,000 TODO JRD -- get better heuristics
		private const int FileBufferSize = 1024*8;
		private readonly BlockingCollection<KeyValuePair<int, int[]>> _dataQue;
		private readonly int _totalCount;
		private Thread _thread;
		private readonly string _rToFPath;
		private readonly string _fToRPath;
		private IProgress<string> _progress;

		private RecordCounter _counter;


		public ReferencePersistor(string rToFPath, string fToRPath, int count,
			BlockingCollection<KeyValuePair<int, int[]>> dataQue, IProgress<string> progress = null)
		{
			_dataQue = dataQue;
			_totalCount = count;
			_rToFPath = rToFPath;
			_fToRPath = fToRPath;
			_progress = progress;
		}

		public ReferencePersistor(string rToFPath, string fToRPath, RecordCounter counter, IProgress<string> progress = null)
		{
			_rToFPath = rToFPath;
			_fToRPath = fToRPath;
			_progress = progress;
			_counter = counter;
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
				while (true)
				{
					var kv = _dataQue.Take();
					if (kv.Key < 0) break; // end of data
					rToF.Add(kv.Key, kv.Value);
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
				fw = new FileWriter(_rToFPath);
				byte[] buffer = new byte[FileBufferSize];
				int recCount = 0;
				fw.Write(recCount);
				int[] fieldRefCounts = new int[_totalCount];
				int rndx, rcnt;
				while (true)
				{
					var kv = _dataQue.Take();
					if (kv.Key < 0) break; // end of data
					++recCount;
					rndx = kv.Key;
					int[] fary = kv.Value;
					Debug.Assert(Utils.IsSorted(fary));
					Debug.Assert(Utils.AreAllDistinct(fary));
					fw.Write(rndx, fary, buffer);
					for (int i = 0, icnt = fary.Length; i < icnt; ++i)
					{
						var fndx = fary[i];
						var fcnt = fieldRefCounts[fndx];
						fieldRefCounts[fndx] = fcnt + 1;
					}
				}
				fw.GotoBegin();
				fw.Write(-recCount); // mark as not sorted
				fw.Dispose();
				fw = null;

				// revert rToF file

				_progress?.Report("Reverting object to fields reference file.");

				IntArrayStore fToR = new IntArrayStore(_totalCount);
				fToR.InitSlots(fieldRefCounts);
				int[] ibuffer = new int[FileBufferSize/sizeof(int)];

				fr = new FileReader(_rToFPath, FileBufferSize*4, FileOptions.SequentialScan);
				var totRcnt = fr.ReadInt32();

				totRcnt = Math.Abs(totRcnt);
				int reportInterval = totRcnt/10; // 10 progress messages
				string totRecordStr = ", out of " + Utils.CountString(totRcnt);
				for (int i = 0; i < totRcnt; ++i)
				{
					int read = fr.ReadRecord(buffer, ibuffer, out rndx, out rcnt);
					for (int j = 0; j < read; ++j)
					{
						fToR.Add(ibuffer[j], rndx);
					}
					rcnt -= read;
					while (rcnt > 0)
					{
						read = fr.ReadInts(buffer, ibuffer, rcnt);
						for (int j = 0; j < read; ++j)
						{
							fToR.Add(ibuffer[j], rndx);
						}
						rcnt -= read;
					}
					if (i > 0 && (i%reportInterval) == 0)
						_progress.Report("Reverting file, processed: " + Utils.CountString(i) + totRecordStr);
				}
				fr.Dispose();
				fr = null;

				fToR.Dump(_fToRPath, out _error);
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

	public static class ReferenceFileRevertor
	{
		public static Thread RevertFile(string rToFPath, string fToRPath, RecordCounter counter, List<string> errors,
			IProgress<string> progress = null)
		{
			Thread thread = new Thread(RevertFileWork) {IsBackground = true, Name = "ReferenceRevertor"};
			thread.Start(new Tuple<string, string, RecordCounter, List<string>, IProgress<string>>(rToFPath, fToRPath, counter,
				errors, progress));
			return thread;
		}

		private static void RevertFileWork(object data)
		{
			FileReader fr = null;
			List<string> errors = null;
			try
			{
				var info = data as Tuple<string, string, RecordCounter, List<string>, IProgress<string>>;
				Debug.Assert(info != null);
				string rToFPath = info.Item1;
				string fToRPath = info.Item2;
				RecordCounter counter = info.Item3;
				errors = info.Item4;
				IProgress<string> progress = info.Item5;
				const int fileBufferSize = 1024*8;

				IntStore store = new IntStore(counter);
				fr = new FileReader(rToFPath, fileBufferSize, FileOptions.SequentialScan);
				var totRcnt = fr.ReadInt32();

				totRcnt = Math.Abs(totRcnt);
				int[] ibuffer = new int[fileBufferSize/sizeof(int)];
				byte[] buffer = new byte[fileBufferSize];
				int rndx, rcnt;

				for (int i = 0; i < totRcnt; ++i)
				{
					int read = fr.ReadRecord(buffer, ibuffer, out rndx, out rcnt);
					if (rndx == 8098)
					{
						int a = 1;
					}
					int count = rcnt;
					for (int j = 0; j < read; ++j)
					{
						store.AddItem(ibuffer[j], rndx);
					}
					//store.AddItems(rndx, read, ibuffer);
					rcnt -= read;
					while (rcnt > 0)
					{
						read = fr.ReadInts(buffer, ibuffer, rcnt);
						//store.AddItems(rndx, read, ibuffer);
						for (int j = 0; j < read; ++j)
						{
							if (j == 1953)
							{
								int a = 1;
							}
							try
							{
								store.AddItem(ibuffer[j], rndx);
							}
							catch (Exception ex)
							{
								int a = 0;
							}
						}
						rcnt -= read;
					}
					//if (i > 0 && (i % reportInterval) == 0)
					//	_progress.Report("Reverting file, processed: " + Utils.CountString(i) + totRecordStr);
				}

				// restore offsets
				store.RestoreOffsets();
				Debug.Assert(store.CheckOffsets());
				string error;
				store.Dump(fToRPath, out error);
				if (error!=null) errors?.Add(error);
			}
			catch (Exception ex)
			{
				errors?.Add(Utils.GetExceptionErrorString(ex));
			}
			finally
			{
				fr?.Dispose();
			}
		}
	}
}
