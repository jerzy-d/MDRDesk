using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ClrMDRUtil;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
    public class References_old
	{
		public enum DataSource
		{
			Unknown,
			Rooted,
			Unrooted,
			All,
		}

		public enum Direction
		{
			Unknown,
			FieldParent,
			ParentField
		}

		#region fields/properties

		const int MaxNodes = 10000;

		const int FileBufferSize = 1024 * 32;

		private readonly int _runtimeIndex;

		private readonly DumpFileMoniker _fileMoniker;

		// TODO JRD -- remove all below
		//private int[] _rootedParents;
		//private WeakReference<int[][]> _rootedParentReferences;
		//private string _rootedParentsPath;
		//private int[] _rootedFields;
		//private string _rootedFieldsPath;
		//private WeakReference<int[][]> _rootedFiledsReferences;
		//private int[] _nonrootedParents;
		//private string _nonrootedParentsPath;
		//private WeakReference<int[][]> _nonrootedParentReferences;
		//private int[] _nonrootedFields;
		//private string _nonrootedFieldsPath;
		//private WeakReference<int[][]> _nonrootedFiledsReferences;
		// TODO -- end of remove

		private string _refsObjFldPath;
		private int[] _objects;
		private WeakReference<int[][]> _objectReferences;
		private string _refsFldObjPath;
		private int[] _fields;
		private WeakReference<int[][]> _fieldReferences;

		private IndexProxy _index;

		private readonly object _accessorLock;

		#endregion fields/properties

		#region ctors/initialization

		public References_old(int runtimeIndex, DumpFileMoniker fileMoniker)
		{
			_runtimeIndex = runtimeIndex;
			_fileMoniker = fileMoniker;
			_accessorLock = new object();
		}

		public bool Init(out string error)
		{
			error = null;
			try
			{

				_refsObjFldPath = _fileMoniker.GetFilePath(_runtimeIndex, Constants.MapRefsObjectFieldPostfix);
				int[][] lists;
				LoadReferences(_refsObjFldPath, out _objects, out lists, out error);
				_objectReferences = new WeakReference<int[][]>(lists);

				_refsFldObjPath = _fileMoniker.GetFilePath(_runtimeIndex, Constants.MapRefsFieldObjectPostfix);
				LoadReferences(_refsFldObjPath, out _fields, out lists, out error);
				_fieldReferences = new WeakReference<int[][]>(lists);

				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}

		public void SetIndexProxy(IndexProxy index)
		{
			_index = index;
		}




		public static bool CreateReferences2(int runNdx, ClrHeap heap, ulong[] roots, ulong[] instances, int[] typeIds, string[] typeNames,
			Bitset bitset, DumpFileMoniker fileMoniker, IProgress<string> progress, out string error)
		{
			try
			{
				int freTypeId;
				int[] excludedTypes = GetExcludedTypes(typeNames, out freTypeId);
				roots = Utils.GetRealAddressesInPlace(roots);
				instances = Utils.GetRealAddressesInPlace(instances);
				var rootAddressNdxs = Utils.GetAddressIndices(roots, instances);
				string rToFPath = fileMoniker.GetFilePath(runNdx, Constants.MapRefsObjectFieldPostfix);
				string fToRPath = fileMoniker.GetFilePath(runNdx, Constants.MapRefsFieldObjectPostfix);
				return GetRefrences3(heap, rootAddressNdxs, instances, typeIds, excludedTypes, freTypeId, bitset, rToFPath, fToRPath, progress, out error);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}


		private static List<KeyValuePair<ulong, int>> _refObjects = new List<KeyValuePair<ulong, int>>();

		public static void CollectRefs(ulong addr, int off)
		{
			_refObjects.Add(new KeyValuePair<ulong, int>(addr, off));
		}

		public static bool GetRefrences3(ClrHeap heap, int[] indices, ulong[] instances, int[] typeIds, int[] excludedTypes, int freeTypeId,
								Bitset bitset, string refsObjFldPath, string refsFldObjPath, IProgress<string> progress, out string error)
		{
			error = null;
			int reportInterval = instances.Length / 12;
			int reportCount = reportInterval;
			FileWriter fw = null;
			try
			{
				Debug.Assert(freeTypeId >= 0);
				List<int> ndxList = new List<int>(128);
				List<ulong> addrList = new List<ulong>(128);
				var fieldAddrOffsetList = new List<KeyValuePair<ulong, int>>(64);
				var que = new Queue<KeyValuePair<int, ulong>>(1024 * 1024);
				Utils.KVUlongIntKCmp kvCmp = new Utils.KVUlongIntKCmp();

				progress?.Report("Building rooted references, root/object counts: " + Utils.CountString(indices.Length) + "/" +
									Utils.CountString(instances.Length));
				fw = new FileWriter(refsObjFldPath, FileBufferSize, FileOptions.SequentialScan);

				fw.Write(0);
				RecordCounter counter = new RecordCounter(instances.Length);
				List<ulong> notFound = new List<ulong>();

				int recCount = 0;

				for (int i = 0, icnt = indices.Length; i < icnt; ++i)
				{
					var rootNdx = indices[i];
					if (bitset.IsSet(rootNdx)) continue;
					var excluded = IsExcludedType(excludedTypes, typeIds[rootNdx]);
					if (excluded >= 0)
					{
						if (excluded != freeTypeId) bitset.Set(rootNdx); // mark it
						continue;
					}
					var rootAddr = Utils.RealAddress(instances[rootNdx]);
					que.Enqueue(new KeyValuePair<int, ulong>(rootNdx, rootAddr));

					// go down the hierarchy
					while (que.Count > 0)
					{
						var kv = que.Dequeue();
						var refNdx = kv.Key;
						if (bitset.IsSet(refNdx)) continue;
						Debug.Assert(IsExcludedType(excludedTypes, typeIds[refNdx]) < 0);
						bitset.Set(refNdx); // mark it

						if (bitset.SetCount > reportCount)
						{
							progress?.Report("Rooted references, processed: " + Utils.CountString(bitset.SetCount)
												+ ", current instance [" + Utils.CountString(rootNdx)
												+ "] " + Utils.RealAddressString(rootAddr));
							reportCount += reportInterval;
						}

						// get references from m.d.r
						//
						var refAddr = kv.Value;
						var clrType = heap.GetObjectType(refAddr);
						if (clrType == null) continue;
						fieldAddrOffsetList.Clear();
						clrType.EnumerateRefsOfObjectCarefully(refAddr, (address, off) =>
						{
							fieldAddrOffsetList.Add(new KeyValuePair<ulong, int>(address, off));
						});
						if (fieldAddrOffsetList.Count < 1) continue;

						// clean and process address list
						CleanupReferencesList(fieldAddrOffsetList, kvCmp, instances, ndxList, addrList, notFound);
						if (ndxList.Count > 0)
						{
							for (int j = 0, jcnt = ndxList.Count; j < jcnt; ++j)
							{
								var faddr = addrList[j];
								var ndx = ndxList[j];
								counter.Add(ndx);
								if (bitset.IsSet(ndx)) continue;
								excluded = IsExcludedType(excludedTypes, typeIds[ndx]);
								if (excluded >= 0)
								{
									if (excluded != freeTypeId) bitset.Set(ndx); // mark it
									continue;
								}
								que.Enqueue(new KeyValuePair<int, ulong>(ndx, faddr));
							}
							ndxList.Sort();
							fw.WriteReferenceRecord(refNdx, ndxList);
							++recCount;
						}
					}
				}

				progress?.Report("Building rooted references done, rooted/object counts: " +
									Utils.CountString(bitset.SetCount) + "/" + Utils.CountString(instances.Length));

				//
				// continue with the rest
				//
				var bitset2 = bitset.Clone();
				progress?.Report("Building unrooted references, unrooted/object counts: " +
									Utils.CountString(instances.Length - bitset2.SetCount) + "/" + Utils.CountString(instances.Length));

				for (int i = 0, icnt = instances.Length; i < icnt; ++i)
				{
					if (bitset2.IsSet(i)) continue;
					var excluded = IsExcludedType(excludedTypes, typeIds[i]);
					if (excluded >= 0)
					{
						if (excluded != freeTypeId) bitset2.Set(i);
						continue;
					}
					var rootAddr = Utils.RealAddress(instances[i]);
					que.Enqueue(new KeyValuePair<int, ulong>(i, rootAddr));

					// go down the hierarchy
					while (que.Count > 0)
					{
						var kv = que.Dequeue();
						var refNdx = kv.Key;
						if (bitset2.IsSet(refNdx)) continue;
						Debug.Assert(IsExcludedType(excludedTypes, typeIds[refNdx]) < 0);
						bitset2.Set(refNdx); // mark it
						if (bitset2.SetCount > reportCount)
						{
							progress?.Report("Unrooted references, processed: " + Utils.CountString(bitset2.SetCount - bitset.SetCount)
												+ ", current instance [" + Utils.CountString(i)
												+ "] " + Utils.RealAddressString(rootAddr));
							reportCount += reportInterval;
						}
						var raddr = kv.Value;
						var clrType = heap.GetObjectType(raddr);
						if (clrType == null) continue;
						fieldAddrOffsetList.Clear();
						clrType.EnumerateRefsOfObjectCarefully(raddr, (address, off) =>
						{
							fieldAddrOffsetList.Add(new KeyValuePair<ulong, int>(address, off));
						});
						if (fieldAddrOffsetList.Count < 1) continue;

						// clean and process address list
						CleanupReferencesList(fieldAddrOffsetList, kvCmp, instances, ndxList, addrList, notFound);
						if (ndxList.Count > 0)
						{
							for (int j = 0, jcnt = ndxList.Count; j < jcnt; ++j)
							{
								var faddr = addrList[j];
								var ndx = ndxList[j];
								counter.Add(ndx);
								if (bitset2.IsSet(ndx)) continue;
								excluded = IsExcludedType(excludedTypes, typeIds[ndx]);
								if (excluded >= 0)
								{
									if (excluded != freeTypeId) bitset2.Set(ndx); // mark it
									continue;
								}
								que.Enqueue(new KeyValuePair<int, ulong>(ndx, faddr));
							}
							ndxList.Sort();
							fw.WriteReferenceRecord(refNdx, ndxList);
							++recCount;
						}
					}
				}

				fw.Flush();
				fw.Seek(0, SeekOrigin.Begin);
				fw.Write(-recCount);
				fw.Flush();
				fw.Dispose();

				fw = null;
				que = null;
				fieldAddrOffsetList = null;
				ndxList = null;
				addrList = null;

				var errors = new List<string>();
				var revertThread = ReferenceFileRevertor.RevertFile(refsObjFldPath, refsFldObjPath, counter, errors, progress);

				progress?.Report("The references processing done, rooted/unrooted counts: " + Utils.CountString(bitset.SetCount) +
									"/" + Utils.CountString(bitset2.SetCount - bitset.SetCount));
				revertThread.Join();
				if (errors.Count > 0) error = errors[0];

				Utils.ForceGcWithCompaction();

				return error == null;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				fw?.Dispose();
			}

		}

		private static void CleanupReferencesList(List<KeyValuePair<ulong, int>> fieldAddrOffsetList, Utils.KVUlongIntKCmp kvCmp, ulong[] instances, List<int> indices, List<ulong> addresses, List<ulong> notFound)
		{
			indices.Clear();
			addresses.Clear();
			fieldAddrOffsetList.Sort(kvCmp);
			Utils.RemoveDuplicates(fieldAddrOffsetList, kvCmp);
			for (int j = 0, jcnt = fieldAddrOffsetList.Count; j < jcnt; ++j)
			{
				var faddr = fieldAddrOffsetList[j].Key;
				var ndx = Utils.AddressSearch(instances, faddr);
				if (ndx < 0)
				{
					notFound.Add(faddr);
					continue;
				}
				indices.Add(ndx);
				addresses.Add(faddr);
			}
		}

		#endregion ctors/initialization

		#region queries


        // TODO JRD replace 
		public int[] GetReferences(int instNdx, Direction direction, DataSource dataSource, out string error)
		{
			int[] heads;
			int[][] refs;
			if (!SelectArrays(direction, out heads, out refs, out error)) return null;

			var hNdx = Array.BinarySearch(heads, instNdx);
			if (hNdx < 0)
			{
				return Utils.EmptyArray<int>.Value;
			}
			return refs[hNdx];
		}

        //public int[] GetReferences(int instNdx, int[] heads, int[][] refs, out string error)
        //{
        //	error = null;
        //	var hNdx = Array.BinarySearch(heads, instNdx);
        //	if (hNdx < 0)
        //	{
        //		return Utils.EmptyArray<int>.Value;
        //	}
        //	return refs[hNdx];
        //}


        // TODO JRD replace 
        public KeyValuePair<IndexNode, int> GetReferenceNodes(int addrNdx, Direction direction, DataSource dataSource, out string error, int maxLevel = Int32.MaxValue)
		{
			error = null;
			try
			{
				int[] heads;
				int[][] refs;
				if (!SelectArrays(direction, out heads, out refs, out error)) return new KeyValuePair<IndexNode, int>(null, 0);

				var rootNode = new IndexNode(addrNdx, 0); // we at level 0
				var uniqueSet = new HashSet<int>();
				var que = new Queue<IndexNode>(256);
				que.Enqueue(rootNode);
				uniqueSet.Add(addrNdx);
				int nodeCount = 0; // do not count root node
				while (que.Count > 0)
				{
					var curNode = que.Dequeue();
					++nodeCount;
					if (curNode.Level >= maxLevel || nodeCount > MaxNodes) continue;
					var hNdx = Array.BinarySearch(heads, curNode.Index);
					if (hNdx < 0) continue;

					var references = refs[hNdx];
					if (references.Length < 1) continue;

					var nodes = new IndexNode[references.Length];
					curNode.AddNodes(nodes);
					for (int i = 0, icnt = references.Length; i < icnt; ++i)
					{
						var rNdx = references[i];
						var cnode = new IndexNode(rNdx, curNode.Level + 1);
						nodes[i] = cnode;
						if (!uniqueSet.Add(rNdx))
						{
							++nodeCount;
							continue;
						}
						que.Enqueue(cnode);
					}
				}
				return new KeyValuePair<IndexNode, int>(rootNode, nodeCount);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return new KeyValuePair<IndexNode, int>(null, 0);
			}
		}

		/// <summary>
		/// Get references of the given set of instances.
		/// </summary>
		/// <param name="addrNdxs">Ids of instances we want references of.</param>
		/// <param name="direction">Parents or children.</param>
		/// <param name="dataSource">Rooted, unrooted or all.</param>
		/// <param name="error">Failure message.</param>
		/// <param name="maxLevel">How deep the reference tree should be.</param>
		/// <returns></returns>
        /// TODO JRD replace
		public KeyValuePair<IndexNode, int>[] GetReferenceNodes(int[] addrNdxs, Direction direction, DataSource dataSource, out string error, int maxLevel = Int32.MaxValue)
		{
			error = null;
			try
			{
				// select desired graph... parents or children
				int[] heads;
				int[][] refs;
				if (!SelectArrays(direction, out heads, out refs, out error)) return null;


				var results = new List<KeyValuePair<IndexNode, int>>(addrNdxs.Length);
				var uniqueSet = new HashSet<int>();
				var que = new Queue<IndexNode>(256);
				for (int ndx = 0, ndxCnt = addrNdxs.Length; ndx < ndxCnt; ++ndx)
				{
					uniqueSet.Clear();
					que.Clear();
					var addrNdx = addrNdxs[ndx];
					var rootNode = new IndexNode(addrNdx, 0); // we at level 0
					que.Enqueue(rootNode);
					uniqueSet.Add(addrNdx);
					int nodeCount = 0; // do not count root node
					while (que.Count > 0)
					{
						var curNode = que.Dequeue();
						++nodeCount;
						if (curNode.Level >= maxLevel || nodeCount > MaxNodes) continue;
						var hNdx = Array.BinarySearch(heads, curNode.Index);
						if (hNdx < 0) continue;

						var references = refs[hNdx];
						if (references.Length < 1) continue;

						var nodes = new IndexNode[references.Length];
						curNode.AddNodes(nodes);
						for (int i = 0, icnt = references.Length; i < icnt; ++i)
						{
							var rNdx = references[i];
							var cnode = new IndexNode(rNdx, curNode.Level + 1);
							nodes[i] = cnode;
							if (!uniqueSet.Add(rNdx))
							{
								++nodeCount;
								continue;
							}
							que.Enqueue(cnode);
						}
					}
					results.Add(new KeyValuePair<IndexNode, int>(rootNode, nodeCount));

				}
				return results.ToArray();
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}


		private bool SelectArrays(Direction direction, out int[] heads, out int[][] refs, out string error)
		{
			error = null;
			switch (direction)
			{
				case Direction.ParentField:
					heads = _objects;
					refs = GetReferenceLists(_refsObjFldPath, _objectReferences, out error);
					return error == null;
				case Direction.FieldParent:
					heads = _fields;
					refs = GetReferenceLists(_refsFldObjPath, _fieldReferences, out error);
					return error == null;
				default:
					heads = null;
					refs = null;
					error = "Unknown direction.";
					return false;
			}
		}

		#endregion queries

		#region io

		private int[][] GetReferenceLists(string path, WeakReference<int[][]> references, out string error)
		{
			error = null;
			lock (_accessorLock)
			{
				try
				{
					int[][] refs = null;
					if (!references.TryGetTarget(out refs))
					{
						if (!LoadReferenceLists(path, out refs, out error))
						{
							return null;
						}
						references.SetTarget(refs);
					}
					return refs;
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					return null;
				}
			}
		}

		public static bool LoadReferences(string path, out int[] headAry, out int[][] lists, out string error)
		{
			error = null;
			BinaryReader br = null;
			headAry = null;
			lists = null;
			try
			{
				br = new BinaryReader(File.Open(path, FileMode.Open));

				int cnt = br.ReadInt32();
				bool sortAfter = cnt < 0;
				cnt = Math.Abs(cnt);
				headAry = new int[cnt];
				lists = new int[cnt][];
				for (int i = 0; i < cnt; ++i)
				{
					headAry[i] = br.ReadInt32();
					var lstCnt = br.ReadInt32();
					var lstAry = new int[lstCnt];
					for (int j = 0; j < lstCnt; ++j)
					{
						lstAry[j] = br.ReadInt32();
					}
					lists[i] = lstAry;
				}
				if (sortAfter)
					Array.Sort(headAry, lists);
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
			}
		}

		public static bool LoadReferenceLists(string path, out int[][] lists, out string error)
		{
			error = null;
			BinaryReader br = null;
			lists = null;
			try
			{
				br = new BinaryReader(File.Open(path, FileMode.Open));

				int cnt = br.ReadInt32();
				lists = new int[cnt][];
				for (int i = 0; i < cnt; ++i)
				{
					br.ReadInt32();
					var lstCnt = br.ReadInt32();
					var lstAry = new int[lstCnt];
					for (int j = 0; j < lstCnt; ++j)
					{
						lstAry[j] = br.ReadInt32();
					}
					lists[i] = lstAry;
				}
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
			}
		}

		#endregion io

		#region utils

		public static int[] GetExcludedTypes(string[] typeNames, out int freeType)
		{
			freeType = -1;
			Debug.Assert(Utils.IsSorted<string>(typeNames, StringComparer.Ordinal));
			string[] excludedTypeNames = new[]
			{
				"Free",
				"System.DateTime",
				"System.Decimal",
				"System.Guid",
				"System.String",
				"System.TimeSpan",

			};

			List<int> lst = new List<int>(excludedTypeNames.Length);
			for (int i = 0, icnt = excludedTypeNames.Length; i < icnt; ++i)
			{
				var ndx = Array.BinarySearch(typeNames, excludedTypeNames[i], StringComparer.Ordinal);
				if (ndx >= 0)
				{
					if (freeType < 0 && Utils.SameStrings(excludedTypeNames[i], "Free"))
						freeType = ndx;
					lst.Add(ndx);
				}
			}
			return lst.ToArray();
		}

		public static int IsExcludedType(int[] typeIds, int id)
		{
			int ndx = Array.IndexOf(typeIds, id);
			return ndx < 0 ? Constants.InvalidIndex : typeIds[ndx];
		}

		public static class ReferenceFileRevertor
		{
			public static Thread RevertFile(string rToFPath, string fToRPath, RecordCounter counter, List<string> errors,
				IProgress<string> progress = null)
			{
				Thread thread = new Thread(RevertFileWork) { IsBackground = true, Name = "ReferenceRevertor" };
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
					const int fileBufferSize = 1024 * 8;

					IntStore store = new IntStore(counter);
					fr = new FileReader(rToFPath, fileBufferSize, FileOptions.SequentialScan);
					var totRcnt = fr.ReadInt32();

					totRcnt = Math.Abs(totRcnt);
					int[] ibuffer = new int[fileBufferSize / sizeof(int)];
					byte[] buffer = new byte[fileBufferSize];
					int rndx, rcnt;

					for (int i = 0; i < totRcnt; ++i)
					{
						int read = fr.ReadRecord(buffer, ibuffer, out rndx, out rcnt);
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
							for (int j = 0; j < read; ++j)
							{
								try
								{
									store.AddItem(ibuffer[j], rndx);
								}
								catch (Exception)
								{
									// TODO JRD ???
								}
							}
							rcnt -= read;
						}
					}

					// restore offsets
					store.RestoreOffsets();
					Debug.Assert(store.CheckOffsets());
					string error;
					store.Dump(fToRPath, out error);
					if (error != null) errors?.Add(error);
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


			#endregion utils

		}
	}
}
