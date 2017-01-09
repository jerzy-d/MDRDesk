using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class InstanceReferencesOld
	{
		#region fields/properties

		private int _runtimeIndex;
		private string _dumpPath;
		private ulong[] _instances;
		private int[] _instanceTypes;
		private string[] _typeNames;

		private int[] _fieldInstances;
		private long[] _parentOffsets;

		private MemoryMappedFile _referencesMappedFile;
		public MemoryMappedFile ReferencesMappedFile => _referencesMappedFile;
		private MemoryMappedViewAccessor _mappedViewAccessor;
		private long _mappedViewBegin;
		private long _mappedViewEnd;
		private object _accessorLock;


		#endregion fields/properties

		#region ctors/initialization

		public InstanceReferencesOld(string dumpPath, int runtimeIndex, ulong[] instances, int[] instanceTypes, string[] typeNames)
		{
			_dumpPath = dumpPath;
			_runtimeIndex = runtimeIndex;
			_instances = instances;
			_instanceTypes = instanceTypes;
			_typeNames = typeNames;
			_accessorLock = new object();
		}

		public bool Init(out string error)
		{
			error = null;
			DumpFileMoniker fileMoniker = new DumpFileMoniker(_dumpPath);
			BinaryReader reader = null;
			try
			{
				var offsetTargetPath = fileMoniker.GetFilePath(_runtimeIndex, Constants.MapFieldRefOffsetsFilePostfix);
				reader = new BinaryReader(File.Open(offsetTargetPath, FileMode.Open));
				int offCnt = reader.ReadInt32();
				_fieldInstances = new int[offCnt];
				_parentOffsets = new long[offCnt];
				for (int i = 0; i < offCnt; ++i)
				{
					_fieldInstances[i] = reader.ReadInt32();
					_parentOffsets[i] = reader.ReadInt64();
				}
				Utils.CloseStream(ref reader);

				var refTargetPath = fileMoniker.GetFilePath(_runtimeIndex, Constants.MapFieldParentsPostfix);
				_referencesMappedFile = GetFieldParentMap(refTargetPath, out error);
				return _referencesMappedFile != null;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				reader?.Close();
			}
		}

		#endregion ctors/initialization


		public static MemoryMappedFile GetFieldParentMap(string path, out string error)
		{
			error = null;
			try
			{
				FileStream fstream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
				return MemoryMappedFile.CreateFromFile(fstream,
														null,
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

		#region queries

		public int[] GetFieldParents(ulong fldAddr, out string error)
		{
			var instanceNdx = Utils.AddressSearch(_instances,fldAddr,0, _instances.Length-1);
			if (instanceNdx < 0)
			{
				error = "Instance at address: " + Utils.AddressString(fldAddr) + ", not found.";
				return Utils.EmptyArray<int>.Value;
			}
			var offsetNdx = Array.BinarySearch(_fieldInstances, instanceNdx);
			if (offsetNdx < 0)
			{
				error = Constants.InformationSymbolHeader + "Parents of: " + Utils.AddressString(fldAddr) + ", not found.";
				return Utils.EmptyArray<int>.Value;
			}

			//int parentCnt = (int)((_parentOffsets[offsetNdx + 1] - _parentOffsets[offsetNdx])/sizeof(long));

			return ReadFieldParents(_parentOffsets[offsetNdx], _parentOffsets[offsetNdx + 1], out error);
		}

        public int[] GetFieldParents(int instanceNdx, out string error)
		{
			Debug.Assert(instanceNdx >= 0);
			var offsetNdx = Array.BinarySearch(_fieldInstances, instanceNdx);
			if (offsetNdx < 0)
			{
				error = Constants.InformationSymbolHeader + "Parents of object at index: [" + instanceNdx + "], not found.";
				return Utils.EmptyArray<int>.Value;
			}
			return ReadFieldParents(_parentOffsets[offsetNdx], _parentOffsets[offsetNdx + 1], out error);
		}


		public KeyValuePair<int, int[]>[] GetMultiFieldParents(ulong[] addresses, List<string> errors)
		{
			try
			{
				string error;
				var lst = new List<KeyValuePair<int, int[]>>(addresses.Length);
				for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
				{
					int[] parents = GetFieldParents(addresses[i], out error);
					if (parents != null && parents.Length > 0)
					{
						var addrIndex = Utils.AddressSearch(_instances, addresses[i]);
						lst.Add(new KeyValuePair<int, int[]>(addrIndex, parents));
					}
					if (error != null && error[0] != Constants.InformationSymbol) errors.Add(error);
				}

				return lst.ToArray();
			}
			catch (Exception ex)
			{
				errors.Add(Utils.GetExceptionErrorString(ex));
				return null;
			}
		}
		/// <summary>
		/// Get parent addresses from mapped file..
		/// </summary>
		/// <param name="offBegin">Starting offset of parents.</param>
		/// <param name="offEnd">First offset past parents data.</param>
		/// <param name="error"></param>
		/// <returns>Array of parent addresses, or null.</returns>
		public int[] ReadFieldParents(long offBegin, long offEnd, out string error)
		{

			error = null;
			int count = (int)((offEnd - offBegin) / sizeof(int));
			var parents = new int[count];

			long length = offEnd - offBegin;

			try
			{
				lock (_accessorLock)
				{
					using (var acssr = _referencesMappedFile.CreateViewAccessor(offBegin, length, MemoryMappedFileAccess.Read))
					{
						long off = 0;
						for (int i = 0; i < count; ++i)
						{
							parents[i] = acssr.ReadInt32(off);
							off += sizeof(int);
						}
					}
				}
				return parents;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}
		#endregion queries


		#region persists field references

		public static void WriteFieldReferences(object data)
		{
			var info = data as Tuple<string, BlockingCollection<KeyValuePair<int, int[]>>, List<string>>;
			Debug.Assert(info != null);

			var que = info.Item2;
			var errors = info.Item3;

			BinaryWriter dpndbr = null;
			int dpndCnt = 0;
			try
			{
				dpndbr = new BinaryWriter(File.Open(info.Item1, FileMode.Create));
				dpndbr.Write(dpndCnt);

				while (true)
				{
					var kv = que.Take();
					if (kv.Key < 0) break; // signals end of data
					Debug.Assert(kv.Value.Length > 0);
					dpndbr.Write(kv.Key);
					dpndbr.Write(kv.Value.Length);
					for (int i = 0, icnt = kv.Value.Length; i < icnt; ++i)
					{
						dpndbr.Write(kv.Value[i]);
					}
					++dpndCnt;
				}
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
				dpndbr?.Close();
			}
		}

		private static bool WriteFldRefTempFile(string basePath, List<string> tempFiles, List<ulong> items, out string error)
		{
			error = null;
			items.Sort();
			string path = basePath + tempFiles.Count.ToString() + ".tmp";
			BinaryWriter br = null;
			try
			{
				br = new BinaryWriter(File.Open(path, FileMode.Create));
				var icnt = items.Count;
				br.Write(icnt);
				for (int i = 0; i < icnt; ++i)
				{
					br.Write(items[i]);
				}
				tempFiles.Add(path);
				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				br?.Close();
				items.Clear();
			}
		}

		/// <summary>
		/// Creates inverted file for field references.
		/// </summary>
		/// <param name="data">
		/// Tuple:
		///		path to file with references, 
		///		dmpPath to create temp files,
		///		error list (out),
		///		IProgress instance, can be null 
		/// </param>
		public static void InvertFieldRefs(object data)
		{
			string error = null;
			var info = data as Tuple<int, DumpFileMoniker, ConcurrentBag<string>, IProgress<string>>;
			Debug.Assert(info != null);

			var runtmIndex = info.Item1;
			var fileMoniker = info.Item2;
			var errors = info.Item3;
			var progress = info.Item4;
			var pathToRefFile = fileMoniker.GetFilePath(runtmIndex, Constants.MapFieldRefInstancesPostfix);

			BinaryReader brFldRefInstances = null;
			int maxItemCount = (1024 * 1024) * (IntPtr.Size == 4 ? 50 : 100) / sizeof(int);
			List<string> tempFiles = new List<string>();
			List<ulong> items = new List<ulong>(maxItemCount / sizeof(int) + 128);

			try
			{
				brFldRefInstances = new BinaryReader(File.Open(pathToRefFile, FileMode.Open));
				var brFldRefInstancesCnt = brFldRefInstances.ReadInt32();
				int readItems = 0;
				while (readItems < brFldRefInstancesCnt)
				{
					if (items.Count > maxItemCount)
					{
						WriteFldRefTempFile(pathToRefFile, tempFiles, items, out error);
					}
					var instIndex = brFldRefInstances.ReadInt32();
					var instFldeCnt = brFldRefInstances.ReadInt32();
					for (int i = 0; i < instFldeCnt; ++i)
					{
						var fldNdx = brFldRefInstances.ReadInt32();
#pragma warning disable CS0675 // Bitwise-or operator used on a sign-extended operand
						var item = ((ulong)fldNdx << 32) | (ulong)instIndex;
#pragma warning restore CS0675 // Bitwise-or operator used on a sign-extended operand
						items.Add(item);
					}
					++readItems;
				}
				if (items.Count > 0)
				{
					WriteFldRefTempFile(pathToRefFile, tempFiles, items, out error);
				}
				brFldRefInstances?.Close();

				MergeInvertedFieldRefs(runtmIndex, fileMoniker, tempFiles, out error);

				Utils.DeleteFiles(tempFiles, out error);

			}
			catch (Exception ex)
			{
				errors.Add(Utils.GetExceptionErrorString(ex));
			}
			finally
			{
				brFldRefInstances?.Close();
			}
		}

		public static void MergeInvertedFieldRefs(int runtmIndex, DumpFileMoniker fileMoniker, List<string> tempFiles, out string error)
		{
			error = null;
			KeyValuePair<BinaryReader, int>[] readers = new KeyValuePair<BinaryReader, int>[tempFiles.Count];
			var binHeap = new BinaryHeap<KeyValuePair<ulong, int>>(new Utils.NumericKvCmp<ulong, int>());
			BinaryWriter offsetTarget = null;
			BinaryWriter refTarget = null;

			try
			{
				for (int i = 0, icnt = readers.Length; i < icnt; ++i)
				{
					var reader = new BinaryReader(File.Open(tempFiles[i], FileMode.Open));
					var cnt = reader.ReadInt32();
					if (cnt > 0)
					{
						readers[i] = new KeyValuePair<BinaryReader, int>(reader, cnt - 1); // we read firstrecord below
						var fld = reader.ReadUInt64();
						binHeap.Insert(new KeyValuePair<ulong, int>(fld, i));
					}
					else
					{
						readers[i] = new KeyValuePair<BinaryReader, int>(reader, 0); // we read firstrecord below
						binHeap.Insert(new KeyValuePair<ulong, int>(UInt64.MaxValue, i));
					}
				}

				var offsetTargetPath = fileMoniker.GetFilePath(runtmIndex, Constants.MapFieldRefOffsetsFilePostfix);
				var refTargetPath = fileMoniker.GetFilePath(runtmIndex, Constants.MapFieldParentsPostfix);

				offsetTarget = new BinaryWriter(File.Open(offsetTargetPath, FileMode.Create));
				refTarget = new BinaryWriter(File.Open(refTargetPath, FileMode.Create));
				int offCnt = 0;
				int refCnt = 0;
				offsetTarget.Write(offCnt);
				refTarget.Write(refCnt);
				int prevFld = int.MinValue;
				while (binHeap.Count > 0)
				{
					var item = binHeap.RemoveRoot();
					ReadNextRecord(readers, item.Value, binHeap);
					var fldNdx = (int)(item.Key >> 32);
					var parentNdx = (int)item.Key;
					if (fldNdx != prevFld)
					{
						offsetTarget.Write(fldNdx); // field address
						offsetTarget.Write(refTarget.Seek(0, SeekOrigin.Current));
						Debug.Assert(prevFld <= fldNdx);
						prevFld = fldNdx;
						++offCnt;
					}
					refTarget.Write(parentNdx); // parent address
					++refCnt;
				}
				// add extra entry at the end
				offsetTarget.Write(int.MaxValue); // dummy ndx
				offsetTarget.Write(refTarget.Seek(0, SeekOrigin.Current));
				++offCnt;

				offsetTarget.Seek(0, SeekOrigin.Begin);
				offsetTarget.Write(offCnt);
				refTarget.Seek(0, SeekOrigin.Begin);
				refTarget.Write(refCnt);

			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
			}
			finally
			{
				offsetTarget?.Close();
				refTarget?.Close();
				for (int i = 0, icnt = readers.Length; i < icnt; ++i)
				{
					readers[i].Key?.Close();
				}
			}

		}

		private static void ReadNextRecord(KeyValuePair<BinaryReader, int>[] readers, int readerNdx, BinaryHeap<KeyValuePair<ulong, int>> heap)
		{
			var reader = readers[readerNdx].Key;
			var cnt = readers[readerNdx].Value;
			if (cnt == 0) return;
			var fld = reader.ReadUInt64();
			readers[readerNdx] = new KeyValuePair<BinaryReader, int>(reader, cnt - 1);
			heap.Insert(new KeyValuePair<ulong, int>(fld, readerNdx));
		}

		#endregion persists field references
	}
}
