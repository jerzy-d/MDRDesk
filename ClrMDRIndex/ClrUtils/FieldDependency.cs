using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.Serialization.Formatters;

namespace ClrMDRIndex
{
	public class FieldDependency
	{
		#region Fields/Properties

		private string _lastError;
		public string LastError => _lastError;
		private string _offsetsPath;
	    public string OffsetsPath => _offsetsPath;
		private string _parentsPath;
	    public string ParentsPath => _parentsPath;
		private string _mappedFileName;

		private WeakReference<Tuple<ulong[], long[]>> _fieldParents;

		public Tuple<ulong[], long[]> ParentOffsets => GetParentOffsets();

		private MemoryMappedFile _mappedFile;

		public MemoryMappedFile MappedFile => _mappedFile;

		private object _accessorLock;

		#endregion Fields/Properties

		#region Building/Ctors/Initialization

		public FieldDependency(string offsetPath, string parentPath, string mappedFileName)
		{
			_offsetsPath = offsetPath;
			_parentsPath = parentPath;
			_mappedFileName = mappedFileName;
			_fieldParents = null;
			_mappedFile = null;
			_accessorLock = new object();
		}

		public static string MappedFileName(string postfix)
		{
			return "FldDpndMappedFile" + postfix;
		}

		public bool Init(out string error)
		{
			error = null;
			try
			{
				var fieldParents = GetFieldOffsets(_offsetsPath, out error);
				if (error != null) return false;
				_mappedFile = GetFieldParentMap(_parentsPath, _mappedFileName, out error);
				if (error != null) return false;
				_fieldParents = new WeakReference<Tuple<ulong[], long[]>>(fieldParents);
				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}

		public static Tuple<ulong[], long[]> GetFieldOffsets(string path, out string error)
		{
			error = null;
			BinaryReader reader = null;
			try
			{
				reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read));
				int cnt = reader.ReadInt32();
				ulong[] fields = new ulong[cnt];
				long[] offsets = new long[cnt];
				for (int i = 0; i < cnt; ++i)
				{
					fields[i] = reader.ReadUInt64();
					offsets[i] = reader.ReadInt64();
				}
				reader.Close();
				reader = null;
				return new Tuple<ulong[], long[]>(fields, offsets);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				reader?.Close();
			}
		}

		public static void WriteFieldsDependencies(object data)
		{
			// string offsetPath, string dependPath, BlockingCollection<KeyValuePair<int, int[]>> queue, List<string> errors
			var info = data as Tuple<string, string, BlockingCollection<KeyValuePair<ulong, KeyValuePair<ulong, int>[]>>, List<string>>;
			Debug.Assert(info != null);

			var que = info.Item3;
			var errors = info.Item4;

			BinaryWriter offsbr = null;
			BinaryWriter dpndbr = null;
			int offsCnt = 0;
			int dpndCnt = 0;
			try
			{
				offsbr = new BinaryWriter(File.Open(info.Item1, FileMode.Create));
				dpndbr = new BinaryWriter(File.Open(info.Item2, FileMode.Create));
				offsbr.Write(offsCnt);
				dpndbr.Write(dpndCnt);

				while (true)
				{
					var kv = que.Take();
					if (kv.Key == Constants.InvalidAddress) break; // means quit

					var curOff = dpndbr.Seek(0, SeekOrigin.Current);
					offsbr.Write(curOff);
					++offsCnt;
					if (kv.Value.Length > 0)
					{
						for (int i = 0, icnt = kv.Value.Length; i < icnt; ++i)
						{
                            var fldAddr = kv.Value[i].Key;
                            var fldNameNdx = kv.Value[i].Value;
                            dpndbr.Write(fldAddr);
                            dpndbr.Write(kv.Key); // parent address
                            dpndbr.Write(fldNameNdx); // field name index
                            ++dpndCnt;
						}
					}
					//else
					//{
					//	dpndbr.Write(Constants.InvalidAddress);
     //                   dpndbr.Write(kv.Key);
     //                   dpndbr.Write(Constants.InvalidIndex);
     //                   ++dpndCnt;
					//}
				}
				offsbr.Seek(0, SeekOrigin.Begin);
				offsbr.Write(offsCnt);
				offsbr.Close();
				offsbr = null;
				dpndbr.Seek(0, SeekOrigin.Begin);
				dpndbr.Write(dpndCnt);
				dpndbr.Close();
				dpndbr = null;

			}
			catch (Exception ex)
			{
				errors.Add(Utils.GetExceptionErrorString(ex));
				return;
			}
			finally
			{
				offsbr?.Close();
				dpndbr?.Close();
			}
		}

		/// <summary>
		/// Given a file of parent -> field addresses sorted by parent addresses generate
		/// two files: field -> parents offset, and parent addresses, sorted by field addresses.
		/// </summary>
		/// <param name="data">
		/// Tuple&lt;string,string,string,string,List&lt;string&gt;&gt;.
		/// (input: parent -> child addresses file, output: parents offsets file, output: parents addresses, list of errors send to a caller)
		/// </param>
		public static void SortFieldDependencies(object data)
		{
			var info = data as Tuple<string, string, string, List<string>, IProgress<string>>;
			Debug.Assert(info != null);

			BinaryReader dpndbrSource = null;
			BinaryWriter dpndbrTarget = null;
			BinaryWriter offsetTarget = null;
			KeyValuePair<BinaryReader, int>[] readers = null;
			List<string> tempFiles = new List<string>();
			var progress = info.Item5;

			progress?.Report("Generating fields dependency files...");
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			int maxRead = ((1024 * 1024) / (sizeof(ulong) * 2)) * (IntPtr.Size==4 ? 50 : 100); // ~50 for 32-bit, ~100 MB otherwise
			int read = 0;
			var records = new List<triple<ulong, ulong,int>>(maxRead);

			try
			{
				dpndbrSource = new BinaryReader(File.Open(info.Item1, FileMode.Open));
				int srcCnt = dpndbrSource.ReadInt32();
				TimeSpan tm = stopWatch.Elapsed;
				while (read < srcCnt)
				{
					records.Clear();
					int toRead = Math.Min(maxRead, srcCnt - read);
					for (int i = 0, icnt = toRead; i < icnt; ++i)
					{
						var fld = dpndbrSource.ReadUInt64();
                        var par = dpndbrSource.ReadUInt64();
                        var fldNameNdx = dpndbrSource.ReadInt32();
                        if (fld == Constants.InvalidAddress) continue;
						records.Add(new triple<ulong, ulong, int>(fld, par,fldNameNdx));
					}

					// we like to have stable sort here
					//
					IEnumerable<triple<ulong, ulong,int>> sorted = records.OrderBy(record => record.First);

					// write to temp file
					//
					string path = info.Item2 + tempFiles.Count.ToString() + ".tmp";
					tempFiles.Add(path);
					dpndbrTarget = new BinaryWriter(File.Open(path, FileMode.Create));
					dpndbrTarget.Write(sorted.Count());
				    var prevKey = 0UL;
					foreach (triple<ulong, ulong,int> kv in sorted)
					{
                        Debug.Assert(kv.First>=prevKey);
						dpndbrTarget.Write(kv.First);
                        dpndbrTarget.Write(kv.Second);
                        dpndbrTarget.Write(kv.Third);
                        prevKey = kv.First;
					}
					dpndbrTarget.Close();
					dpndbrTarget = null;

					read += toRead;

					progress?.Report("Generating fields dependency, sorted file: " + tempFiles.Count + ", duration: " + Utils.DurationString(stopWatch.Elapsed - tm));
				}
				progress?.Report("Fields dependency, temp files count: " + tempFiles.Count + ", sorting files took: " + Utils.DurationString(stopWatch.Elapsed) + ", now merging...");
				tm = stopWatch.Elapsed;
				// merge sorted files
				//
				BinaryHeap<quadruple<ulong, ulong, int, int>> heap =
					new BinaryHeap<quadruple<ulong, ulong, int, int>>(new Utils.QuadrupleUlongUlongIntKeyCmp());
				readers = new KeyValuePair<BinaryReader, int>[tempFiles.Count];
				for (int i = 0, icnt = readers.Length; i < icnt; ++i)
				{
					var reader = new BinaryReader(File.Open(tempFiles[i], FileMode.Open));
					var cnt = reader.ReadInt32();
				    if (cnt > 0)
				    {
				        readers[i] = new KeyValuePair<BinaryReader, int>(reader, cnt - 1); // we read firstrecord below
				        var fld = reader.ReadUInt64();
				        var par = reader.ReadUInt64();
				        var id = reader.ReadInt32();
				        heap.Insert(new quadruple<ulong, ulong, int, int>(fld, par, id, i));
				    }
				    else
				    {
                        readers[i] = new KeyValuePair<BinaryReader, int>(reader, 0); // we read firstrecord below
                        heap.Insert(new quadruple<ulong, ulong, int, int>(UInt64.MaxValue, UInt64.MaxValue, Constants.InvalidIndex, i));
                    }
                }

				offsetTarget = new BinaryWriter(File.Open(info.Item2, FileMode.Create));
				dpndbrTarget = new BinaryWriter(File.Open(info.Item3, FileMode.Create));
				int dpnCnt = 0;
				int offCnt = 0;
				offsetTarget.Write(offCnt);
				dpndbrTarget.Write(dpnCnt);

				ulong prevFld = Constants.InvalidAddress;
				while (heap.Count > 0)
				{
					var item = heap.RemoveRoot();
					ReadNextRecord(readers, item.Forth, heap);
					if (item.First != prevFld)
					{
						offsetTarget.Write(item.First); // field address
						offsetTarget.Write(dpndbrTarget.Seek(0, SeekOrigin.Current));
						++offCnt;
                        Debug.Assert(prevFld<=item.First);
						prevFld = item.First;
					}
                    dpndbrTarget.Write(item.Second); // parent address
                    dpndbrTarget.Write(item.Third); // fld name index
                    ++dpnCnt;
				}
				// add extra entry at the end
				offsetTarget.Write(UInt64.MaxValue); // dummy address
				offsetTarget.Write(dpndbrTarget.Seek(0, SeekOrigin.Current));
				++offCnt;

				offsetTarget.Seek(0, SeekOrigin.Begin);
				offsetTarget.Write(offCnt);
				offsetTarget.Close();
				offsetTarget = null;
				dpndbrTarget.Seek(0, SeekOrigin.Begin);
				dpndbrTarget.Write(dpnCnt);
				dpndbrTarget.Close();
				dpndbrTarget = null;

				progress?.Report("Fields dependency, merge done: " + Utils.DurationString(stopWatch.Elapsed - tm));


				// remove temp files
				//
				for (int i = 0, icnt = readers.Length; i < icnt; ++i)
				{
					readers[i].Key.Close();
					readers[i].Key.Dispose();
					readers[i] = new KeyValuePair<BinaryReader, int>(null, 0);
				}

				Utils.ForceGcWithCompaction();

				for (int i = 0, icnt = readers.Length; i < icnt; ++i)
				{
					File.Delete(tempFiles[i]);
				}
			}
			catch (Exception ex)
			{
				info.Item4.Add(Utils.GetExceptionErrorString(ex));
				return;
			}
			finally
			{
				dpndbrSource?.Close();
				dpndbrTarget?.Close();
				offsetTarget?.Close();
				if (readers != null)
				{
					for (int i = 0, icnt = readers.Length; i < icnt; ++i)
						readers[i].Key?.Close();
				}
			}
		}

		private static void ReadNextRecord(KeyValuePair<BinaryReader, int>[] readers, int readerNdx, BinaryHeap<quadruple<ulong, ulong, int, int>> heap)
		{
			var reader = readers[readerNdx].Key;
			var cnt = readers[readerNdx].Value;
			if (cnt == 0) return;
			var fld = reader.ReadUInt64();
            var par = reader.ReadUInt64();
            var id = reader.ReadInt32();
            readers[readerNdx] = new KeyValuePair<BinaryReader, int>(reader, cnt - 1);
			heap.Insert(new quadruple<ulong, ulong, int, int>(fld, par, id, readerNdx));
		}

		public static MemoryMappedFile GetFieldParentMap(string path, string mapName, out string error)
		{
			Debug.Assert(!string.IsNullOrWhiteSpace(mapName));
			error = null;
			try
			{
				FileStream fstream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
				return MemoryMappedFile.CreateFromFile(fstream,
														mapName,
														0,
														MemoryMappedFileAccess.Read,
														HandleInheritability.None, 
														true);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		private Tuple<ulong[], long[]> GetParentOffsets()
		{
			Tuple<ulong[], long[]> offsets;
			if (_fieldParents.TryGetTarget(out offsets)) // get stuff from WeakReference<Tuple<ulong[], long[]>>
			{
				return offsets;
			}
			string error;
			offsets = GetFieldOffsets(_offsetsPath, out error);
			if (error != null) _lastError = error;
			else _fieldParents.SetTarget(offsets);
			return offsets; // might be mull if GetFieldOffsets method fails
		}

		#endregion Building/Ctors/Initialization

		#region Queries

		public KeyValuePair<ulong, KeyValuePair<ulong, int>[]>[] GetMultiFieldParents(ulong[] addresses, List<string> errors)
		{
			try
			{
				//var procCnt = Math.Min(Environment.ProcessorCount,addresses.Length);
				//procCnt = 1; // TODO JRD -- REMOVE
				//Task[] tasks = new Task[procCnt];
				//var requests = new BlockingCollection<ulong>(addresses.Length);
				//var responses = new BlockingCollection<Tuple<ulong, KeyValuePair<ulong, int>[], string>>(addresses.Length);
				//for (int i = 0, icnt = addresses.Length; i < icnt; ++i) requests.Add(addresses[i]);
				//for (int i = 0; i < procCnt; ++i)
				//{
				//	tasks[i] = new Task(() =>
				//	{
				//		string error;
				//		while (requests.Count > 0)
				//		{
				//			ulong addr;
				//			if (!requests.TryTake(out addr))
				//			{
				//				break;
				//			}
    //                        KeyValuePair<ulong, int>[] parents = GetFieldParents(addr, out error);
				//			responses.Add(new Tuple<ulong, KeyValuePair<ulong, int>[], string>(addr, parents, error));
				//		}
    //                },TaskCreationOptions.LongRunning);
    //                tasks[i].Start();
    //            }
				//Task.WaitAll(tasks);

                string error;
                List<KeyValuePair<ulong, KeyValuePair<ulong, int>[]>> lst = new List<KeyValuePair<ulong, KeyValuePair<ulong, int>[]>>(addresses.Length);
                for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
			    {
                    KeyValuePair<ulong, int>[] parents = GetFieldParents(addresses[i], out error);
			        if (parents != null && parents.Length > 0)
			        {
			            lst.Add(new KeyValuePair<ulong, KeyValuePair<ulong, int>[]>(addresses[i],parents));
			        }
                    if (error != null && error[0] != Constants.InformationSymbol) errors.Add(error);
                }

    //            var result = new KeyValuePair<ulong, KeyValuePair<ulong, int>[]>[responses.Count];
				//int ndx=0;
				//foreach (var val in responses)
				//{
				//	result[ndx++] = new KeyValuePair<ulong, KeyValuePair<ulong, int>[]>(val.Item1,val.Item2??Utils.EmptyArray<KeyValuePair<ulong,int>>.Value);
				//	if (val.Item3 != null)
				//		errors.Add(val.Item3);
				//}

				return lst.ToArray();
			}
			catch (Exception ex)
			{
				errors.Add(Utils.GetExceptionErrorString(ex));
				return null;
			}
		}

		public KeyValuePair<ulong, int>[] GetFieldParents(ulong fldAddr, out string error)
		{
			var parentOffsets = GetParentOffsets();
			if (parentOffsets == null)
			{
				error = LastError;
				return null;
			}
			var ndx = Array.BinarySearch(parentOffsets.Item1, fldAddr);
			if (ndx < 0)
			{
				error = Constants.InformationSymbolHeader + "Parents of: " + Utils.AddressString(fldAddr) + ", not found.";
				return Utils.EmptyArray<KeyValuePair<ulong, int>>.Value;
			}
			return ReadFieldParents(parentOffsets.Item2[ndx], parentOffsets.Item2[ndx+1], out error);
		}

		/// <summary>
		/// Get parent addresses from mapped file..
		/// </summary>
		/// <param name="mmf">Parent list mapped file.</param>
		/// <param name="offBegin">Starting offset of parents.</param>
		/// <param name="offEnd">First offset past parents data.</param>
		/// <param name="error"></param>
		/// <returns>Array of parent addresses, or null.</returns>
		public KeyValuePair<ulong, int>[] ReadFieldParents(long offBegin, long offEnd, out string error)
		{

			error = null;
            KeyValuePair<ulong, int>[] addresses = null;
			long length = offEnd - offBegin;
            long recordSize = sizeof(ulong) + sizeof(int);
            try
            {
				lock (_accessorLock)
				{
					using (var acssr = _mappedFile.CreateViewAccessor(offBegin, length, MemoryMappedFileAccess.Read))
					{
						int count = (int)(length / recordSize);
						addresses = new KeyValuePair<ulong, int>[count];
					    long off = 0;
						for (int i = 0; i < count; ++i)
						{
							addresses[i] = new KeyValuePair<ulong, int>(
                                acssr.ReadUInt64(off),
                                acssr.ReadInt32(off + sizeof(long))
                                );
							off += recordSize;
						}
					}
				}
				return addresses;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}


		/// <summary>
		/// Mostly for testing.
		/// </summary>
		/// <param name="mmf">Parent list mapped file.</param>
		/// <param name="offBegin">Starting offset of parents.</param>
		/// <param name="offEnd">First offset past parents data.</param>
		/// <param name="error"></param>
		/// <returns>Array of parent addresses, or null.</returns>
		public static KeyValuePair<ulong, int>[] GetFieldParents(MemoryMappedFile mmf, long offBegin, long offEnd, out string error)
		{
			error = null;
			KeyValuePair<ulong,int>[] addresses = null;
			long length = offEnd - offBegin;
		    long recordSize = sizeof(ulong) + sizeof(int);

            try
			{
				using (var acssr = mmf.CreateViewAccessor(offBegin,length,MemoryMappedFileAccess.Read))
				{
					int count = (int)(length / recordSize);
					addresses = new KeyValuePair<ulong, int>[count];
				    long off = 0;
					for (int i = 0; i < count; ++i)
					{
					    addresses[i] = new KeyValuePair<ulong, int>(
					        acssr.ReadUInt64(off),
                            acssr.ReadInt32(off+sizeof(long))
					        );
					    off += recordSize;
					}
				}

				return addresses;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		#endregion Queries

	}
}
