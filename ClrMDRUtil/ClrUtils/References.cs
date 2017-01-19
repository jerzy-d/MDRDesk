﻿using System;
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
	public class References
	{
		public enum DataSource
		{
			Unknown,
			RootedParents,
			RootedFields,
			UnrootedParents,
			UnrootedFields,
			AllParents,
			AllFields
		}

		#region fields/properties

		const int MaxNodes = 10000;

		private int _runtimeIndex;

		private DumpFileMoniker _fileMoniker;

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

		private object _accessorLock;

		#endregion fields/properties

		#region ctors/initialization

		public References(int runtimeIndex, DumpFileMoniker fileMoniker)
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
				//_rootedParentsPath = _fileMoniker.GetFilePath(_runtimeIndex, Constants.MapParentFieldsRootedPostfix);
				//int[][] lists;
				//LoadReferences(_rootedParentsPath, out _rootedParents, out lists, out error);
				//_rootedParentReferences = new WeakReference<int[][]>(lists);

				//_rootedFieldsPath = _fileMoniker.GetFilePath(_runtimeIndex, Constants.MapFieldParentsRootedPostfix);
				//LoadReferences(_rootedFieldsPath, out _rootedFields, out lists, out error);
				//_rootedFiledsReferences = new WeakReference<int[][]>(lists);

				//_nonrootedParentsPath = _fileMoniker.GetFilePath(_runtimeIndex, Constants.MapParentFieldsNotRootedPostfix);
				//LoadReferences(_nonrootedParentsPath, out _nonrootedParents, out lists, out error);
				//_nonrootedParentReferences = new WeakReference<int[][]>(lists);

				//_nonrootedFieldsPath = _fileMoniker.GetFilePath(_runtimeIndex, Constants.MapFieldParentsNotRootedPostfix);
				//LoadReferences(_rootedFieldsPath, out _rootedFields, out lists, out error);
				//_rootedFiledsReferences = new WeakReference<int[][]>(lists);

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

		//public static bool CreateReferences(ClrHeap heap, DumpFileMoniker fileMoniker, Tuple<ulong[],ulong[]> rootAddrInfo, ulong[] instances, out ulong[] rootedAry, out string error)
		//{
		//	error = null;
		//	rootedAry = null;
		//	try
		//	{
		//		var rootAddresses = Utils.Remove0sFromSorted(rootAddrInfo.Item1);
		//		rootAddresses = Utils.GetRealAddressesInPlace(rootAddresses);
		//		Debug.Assert(Utils.AreAddressesSorted(rootAddresses));

		//		HashSet<ulong> done = new HashSet<ulong>();
		//		SortedDictionary<ulong, List<ulong>> objRefs = new SortedDictionary<ulong, List<ulong>>();
		//		SortedDictionary<ulong, List<ulong>> fldRefs = new SortedDictionary<ulong, List<ulong>>();

		//		bool result = GetRefrences(heap, rootAddresses, objRefs, fldRefs, done, out error);
		//		if (!result) return false;
		//		string path = fileMoniker.GetFilePath(0, Constants.MapParentFieldsRootedPostfix);
		//		result = DumpReferences(path, objRefs, instances, out error);
		//		path = fileMoniker.GetFilePath(0, Constants.MapFieldParentsRootedPostfix);
		//		result = DumpReferences(path, fldRefs, instances, out error);

		//		rootedAry = done.ToArray(); // for later
		//		Array.Sort(rootedAry);
		//		done.Clear();
		//		objRefs.Clear();
		//		fldRefs.Clear();
		//		var remaining = Utils.Difference(instances, rootedAry);
		//		result = GetRefrences(heap, remaining, objRefs, fldRefs, done, out error);
		//		path = fileMoniker.GetFilePath(0, Constants.MapParentFieldsNotRootedPostfix);
		//		result = DumpReferences(path, objRefs, instances, out error);
		//		path = fileMoniker.GetFilePath(0, Constants.MapFieldParentsNotRootedPostfix);
		//		result = DumpReferences(path, fldRefs, instances, out error);

		//		return true;
		//	}
		//	catch (Exception ex)
		//	{
		//		error = Utils.GetExceptionErrorString(ex);
		//		return false;
		//	}
		//}

		//public static bool DumpReferences(string path, SortedDictionary<ulong, List<ulong>> refs, ulong[] instances, out string error)
		//{
		//	error = null;
		//	BinaryWriter bw = null;
		//	try
		//	{
		//		bw = new BinaryWriter(File.Open(path, FileMode.Create));
		//		bw.Write(refs.Count);
		//		foreach (var kv in refs)
		//		{
		//			var ndx = Array.BinarySearch(instances, kv.Key);
		//			Debug.Assert(ndx >= 0);
		//			var lst = kv.Value;
		//			bw.Write(ndx);
		//			bw.Write(lst.Count);
		//			for (int i = 0, icnt = lst.Count; i < icnt; ++i)
		//			{
		//				ndx = Array.BinarySearch(instances, lst[i]);
		//				Debug.Assert(ndx >= 0);
		//				bw.Write(ndx);
		//			}
		//		}
		//		return true;
		//	}
		//	catch (Exception ex)
		//	{
		//		error = Utils.GetExceptionErrorString(ex);
		//		return false;
		//	}
		//	finally
		//	{
		//		bw?.Close();
		//	}
		//}

		//public static bool GetRefrences(ClrHeap heap, ulong[] addresses, SortedDictionary<ulong, List<ulong>> objRefs, SortedDictionary<ulong, List<ulong>> fldRefs, HashSet<ulong> done,out string error)
		//{
		//	error = null;
		//	try
		//	{
		//		var fldRefList = new List<KeyValuePair<ulong, int>>(64);
		//		Queue<ulong> que = new Queue<ulong>(1024 * 1024 * 10);

		//		for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
		//		{
		//			var raddr = addresses[i];
		//			var clrType = heap.GetObjectType(raddr);
		//			if (clrType == null) continue;
		//			fldRefList.Clear();
		//			clrType.EnumerateRefsOfObjectCarefully(raddr, (address, off) =>
		//			{
		//				fldRefList.Add(new KeyValuePair<ulong, int>(address, off));
		//			});
		//			if (done.Add(raddr))
		//			{
		//				if (fldRefList.Count < 1)
		//				{
		//					continue;
		//				}
		//				var fldAry = fldRefList.Select(kv => kv.Key).ToArray();
		//				Array.Sort(fldAry);
		//				objRefs.Add(raddr, new List<ulong>(fldAry));
		//				for (int j = 0; j < fldAry.Length; ++j)
		//				{
		//					var faddr = fldAry[j];
		//					que.Enqueue(faddr);
		//					List<ulong> lst;
		//					if (fldRefs.TryGetValue(faddr, out lst))
		//					{
		//						var fndx = lst.BinarySearch(raddr);
		//						if (fndx < 0)
		//						{
		//							lst.Insert(~fndx, raddr);
		//						}
		//					}
		//					else
		//					{
		//						fldRefs.Add(faddr, new List<ulong>() { raddr });
		//					}
		//				}
		//			}
		//		}
		//		while (que.Count > 0)
		//		{
		//			var addr = que.Dequeue();
		//			if (!done.Add(addr)) continue;

		//			var clrType = heap.GetObjectType(addr);
		//			if (clrType == null) continue;
		//			fldRefList.Clear();
		//			clrType.EnumerateRefsOfObjectCarefully(addr, (address, off) =>
		//			{
		//				fldRefList.Add(new KeyValuePair<ulong, int>(address, off));
		//			});
		//			if (fldRefList.Count < 1)
		//			{
		//				continue;
		//			}
		//			var fldAry = fldRefList.Select(kv => kv.Key).ToArray();
		//			Array.Sort(fldAry);
		//			if (!objRefs.ContainsKey(addr))
		//				objRefs.Add(addr, new List<ulong>(fldAry));

		//			for (int j = 0; j < fldAry.Length; ++j)
		//			{
		//				var faddr = fldAry[j];
		//				que.Enqueue(faddr);
		//				List<ulong> lst;
		//				if (fldRefs.TryGetValue(faddr, out lst))
		//				{
		//					var fndx = lst.BinarySearch(addr);
		//					if (fndx < 0)
		//					{
		//						lst.Insert(~fndx, addr);
		//					}
		//				}
		//				else
		//				{
		//					fldRefs.Add(faddr, new List<ulong>() { addr });
		//				}
		//			}
		//		}

		//		return true;
		//	}
		//	catch (Exception ex)
		//	{
		//		error = Utils.GetExceptionErrorString(ex);
		//		return false;
		//	}

		//}

		//public static bool CreateReferences(int runNdx, ClrHeap heap, ulong[] roots, ulong[] instances, Bitset bitset, DumpFileMoniker fileMoniker, IProgress<string> progress, out string error)
		//{
		//	error = null;
		//	try
		//	{
		//		roots = Utils.GetRealAddressesInPlace(roots);
		//		var rootAddressNdxs = Utils.GetAddressIndices(roots, instances);
		//		string rToFPath = fileMoniker.GetFilePath(runNdx, Constants.MapParentFieldsRootedPostfix);
		//		string fToRPath = fileMoniker.GetFilePath(runNdx, Constants.MapFieldParentsRootedPostfix);
		//		progress?.Report("REFS BUILD ROOTED inst count: " + rootAddressNdxs.Length);
		//		if (!GetRefrences(heap, rootAddressNdxs, instances, bitset, rToFPath, fToRPath, out error))
		//			return false;
		//		int[] unrootedNdxs = bitset.GetUnsetIndices();
		//		bitset = null;
		//		Utils.ForceGcWithCompaction();
		//		rToFPath = fileMoniker.GetFilePath(runNdx, Constants.MapParentFieldsNotRootedPostfix);
		//		fToRPath = fileMoniker.GetFilePath(runNdx, Constants.MapFieldParentsNotRootedPostfix);
		//		Bitset bs = new Bitset(instances.Length);
		//		progress?.Report("REFS BUILD UNROOTED inst count: " + rootAddressNdxs.Length);
		//		if (!GetRefrences(heap, unrootedNdxs, instances, bs, rToFPath, fToRPath, out error))
		//			return false;
		//		return true;
		//	}
		//	catch (Exception ex)
		//	{
		//		error = Utils.GetExceptionErrorString(ex);
		//		return false;
		//	}
		//}

		public static bool CreateReferences2(int runNdx, ClrHeap heap, ulong[] roots, ulong[] instances, Bitset bitset, DumpFileMoniker fileMoniker, IProgress<string> progress, out string error)
		{
			try
			{
				roots = Utils.GetRealAddressesInPlace(roots);
				var rootAddressNdxs = Utils.GetAddressIndices(roots, instances);
				string rToFPath = fileMoniker.GetFilePath(runNdx, Constants.MapRefsObjectFieldPostfix);
				string fToRPath = fileMoniker.GetFilePath(runNdx, Constants.MapRefsFieldObjectPostfix);
				return GetRefrences2(heap, rootAddressNdxs, instances, bitset, rToFPath, fToRPath, progress, out error);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}


		public static bool GetRefrences(ClrHeap heap, int[] indices, ulong[] instances, Bitset bitset, string rToFPath, string fToRPath, out string error)
		{
			error = null;
			try
			{
				var fldRefList = new List<KeyValuePair<ulong, int>>(64);
				var fldRefIndices = new List<int>(64);
				var que = new Queue<KeyValuePair<int,ulong>>(1024 * 1024 * 10);
				Utils.KVUlongIntKCmp kvCmp = new Utils.KVUlongIntKCmp();

				BlockingCollection<KeyValuePair<int,int[]>> dataToPersist = new BlockingCollection<KeyValuePair<int, int[]>>();
				ReferencePersistor persistor = new ReferencePersistor(rToFPath,fToRPath,instances.Length,dataToPersist);
				persistor.Start();

				for (int i = 0, icnt = indices.Length; i < icnt; ++i)
				{
					var rndx = indices[i];
					if (bitset.IsSet(rndx)) continue;
					var raddr = Utils.RealAddress(instances[rndx]);
					var clrType = heap.GetObjectType(raddr);
					if (clrType == null) continue;
					bitset.Set(rndx); // mark it

					fldRefList.Clear();
					clrType.EnumerateRefsOfObjectCarefully(raddr, (address, off) =>
					{
						fldRefList.Add(new KeyValuePair<ulong, int>(address, off));
					});
					if (fldRefList.Count < 1) continue;
					fldRefList.Sort(kvCmp);
					Utils.RemoveDuplicates(fldRefList, kvCmp);
					fldRefIndices.Clear();
					for (int j = 0, jcnt = fldRefList.Count; j < jcnt; ++j)
					{
						var faddr = fldRefList[j].Key;
						var ndx = Utils.AddressSearch(instances, faddr);
						if (ndx < 0) continue;
						fldRefIndices.Add(ndx);
						que.Enqueue(new KeyValuePair<int, ulong>(ndx,faddr)); 
						// save ndx -> rndx pair
					}
					if (fldRefIndices.Count < 1) continue;

					// save rndx -> ndx[] relation
					dataToPersist.Add(new KeyValuePair<int, int[]>(rndx,fldRefIndices.ToArray()));

					// go down the hierarchy
					while (que.Count > 0)
					{
						var kv = que.Dequeue();
						if (bitset.IsSet(kv.Key)) continue;
						rndx = kv.Key;
						bitset.Set(rndx);  // mark it
						//instances[rndx] |= Utils.RootBits.Rooted;
						raddr = kv.Value;
						clrType = heap.GetObjectType(raddr);
						if (clrType == null) continue;
						fldRefList.Clear();
						clrType.EnumerateRefsOfObjectCarefully(raddr, (address, off) =>
						{
							fldRefList.Add(new KeyValuePair<ulong, int>(address, off));
						});
						if (fldRefList.Count < 1) continue;
						fldRefList.Sort(kvCmp);
						Utils.RemoveDuplicates(fldRefList, kvCmp);
						fldRefIndices.Clear();
						for (int j = 0, jcnt = fldRefList.Count; j < jcnt; ++j)
						{
							var faddr = fldRefList[j].Key;
							var ndx = Utils.AddressSearch(instances, faddr);
							if (ndx < 0) continue;
							fldRefIndices.Add(ndx);
							que.Enqueue(new KeyValuePair<int, ulong>(ndx, faddr));
							// save ndx -> rndx pair
						}
						if (fldRefIndices.Count < 1) continue;

						// save rndx -> ndx[] relation
						dataToPersist.Add(new KeyValuePair<int, int[]>(rndx, fldRefIndices.ToArray()));
					}
				}

				// save results in files
				dataToPersist.Add(new KeyValuePair<int, int[]>(-1,null));
				que.Clear();
				que = null;
				fldRefList.Clear();
				fldRefList = null;
				fldRefIndices.Clear();
				fldRefIndices = null;

				Utils.ForceGcWithCompaction();

				persistor.Wait();
				error = persistor.Error;

				return error == null;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}

		}


		public static bool GetRefrences2(ClrHeap heap, int[] indices, ulong[] instances, Bitset bitset, string refsObjFldPath, string refsFldObjPath, IProgress<string> progress, out string error)
		{
			error = null;
			try
			{
				var fldRefList = new List<KeyValuePair<ulong, int>>(64);
				var fldRefIndices = new List<int>(64);
				var que = new Queue<KeyValuePair<int, ulong>>(1024 * 1024 * 10);
				Utils.KVUlongIntKCmp kvCmp = new Utils.KVUlongIntKCmp();

				progress?.Report("REFS Building rooted references, root count: " + indices.Length);

				BlockingCollection<KeyValuePair<int, int[]>> dataToPersist = new BlockingCollection<KeyValuePair<int, int[]>>();
				ReferencePersistor persistor = new ReferencePersistor(refsObjFldPath, refsFldObjPath, instances.Length, dataToPersist, progress);
				persistor.Start();

				for (int i = 0, icnt = indices.Length; i < icnt; ++i)
				{
					var rndx = indices[i];
					if (bitset.IsSet(rndx)) continue;
					var raddr = Utils.RealAddress(instances[rndx]);
					var clrType = heap.GetObjectType(raddr);
					if (clrType == null) continue;
					bitset.Set(rndx); // mark it

					fldRefList.Clear();
					clrType.EnumerateRefsOfObjectCarefully(raddr, (address, off) =>
					{
						fldRefList.Add(new KeyValuePair<ulong, int>(address, off));
					});
					if (fldRefList.Count < 1) continue;
					fldRefList.Sort(kvCmp);
					Utils.RemoveDuplicates(fldRefList, kvCmp);
					fldRefIndices.Clear();
					for (int j = 0, jcnt = fldRefList.Count; j < jcnt; ++j)
					{
						var faddr = fldRefList[j].Key;
						var ndx = Utils.AddressSearch(instances, faddr);
						if (ndx < 0) continue;
						fldRefIndices.Add(ndx);
						que.Enqueue(new KeyValuePair<int, ulong>(ndx, faddr));
						// save ndx -> rndx pair
					}
					if (fldRefIndices.Count < 1) continue;

					// save rndx -> ndx[] relation
					dataToPersist.Add(new KeyValuePair<int, int[]>(rndx, fldRefIndices.ToArray()));

					// go down the hierarchy
					while (que.Count > 0)
					{
						var kv = que.Dequeue();
						rndx = kv.Key;
						if (bitset.IsSet(rndx)) continue;
						bitset.Set(rndx);  // mark it
										   //instances[rndx] |= Utils.RootBits.Rooted;
						raddr = kv.Value;
						clrType = heap.GetObjectType(raddr);
						if (clrType == null) continue;
						fldRefList.Clear();
						clrType.EnumerateRefsOfObjectCarefully(raddr, (address, off) =>
						{
							fldRefList.Add(new KeyValuePair<ulong, int>(address, off));
						});
						if (fldRefList.Count < 1) continue;
						fldRefList.Sort(kvCmp);
						Utils.RemoveDuplicates(fldRefList, kvCmp);
						fldRefIndices.Clear();
						for (int j = 0, jcnt = fldRefList.Count; j < jcnt; ++j)
						{
							var faddr = fldRefList[j].Key;
							var ndx = Utils.AddressSearch(instances, faddr);
							if (ndx < 0) continue;
							fldRefIndices.Add(ndx);
							que.Enqueue(new KeyValuePair<int, ulong>(ndx, faddr));
							// save ndx -> rndx pair
						}
						if (fldRefIndices.Count < 1) continue;

						// save rndx -> ndx[] relation
						dataToPersist.Add(new KeyValuePair<int, int[]>(rndx, fldRefIndices.ToArray()));
					}
				}


				//
				// continue with the rest
				//
				var bitset2 = bitset.Clone();
				progress?.Report("REFS Building unrooted references, root count: " + (instances.Length - bitset2.SetCount));

				for (int i = 0, icnt = instances.Length; i < icnt; ++i)
				{
					if (bitset2.IsSet(i)) continue;
					bitset2.Set(i);
					var rndx = i;
					var raddr = Utils.RealAddress(instances[rndx]);
					var clrType = heap.GetObjectType(raddr);
					if (clrType == null) continue;

					fldRefList.Clear();
					clrType.EnumerateRefsOfObjectCarefully(raddr, (address, off) =>
					{
						fldRefList.Add(new KeyValuePair<ulong, int>(address, off));
					});
					if (fldRefList.Count < 1) continue;
					fldRefList.Sort(kvCmp);
					Utils.RemoveDuplicates(fldRefList, kvCmp);
					fldRefIndices.Clear();
					for (int j = 0, jcnt = fldRefList.Count; j < jcnt; ++j)
					{
						var faddr = fldRefList[j].Key;
						var ndx = Utils.AddressSearch(instances, faddr);
						if (ndx < 0) continue;
						fldRefIndices.Add(ndx);
						que.Enqueue(new KeyValuePair<int, ulong>(ndx, faddr));
						// save ndx -> rndx pair
					}
					if (fldRefIndices.Count < 1) continue;

					// save rndx -> ndx[] relation
					dataToPersist.Add(new KeyValuePair<int, int[]>(rndx, fldRefIndices.ToArray()));

					// go down the hierarchy
					while (que.Count > 0)
					{
						var kv = que.Dequeue();
						rndx = kv.Key;
						if (bitset2.IsSet(rndx)) continue;
						bitset.Set(rndx);  // mark it
										   //instances[rndx] |= Utils.RootBits.Rooted;
						raddr = kv.Value;
						clrType = heap.GetObjectType(raddr);
						if (clrType == null) continue;
						fldRefList.Clear();
						clrType.EnumerateRefsOfObjectCarefully(raddr, (address, off) =>
						{
							fldRefList.Add(new KeyValuePair<ulong, int>(address, off));
						});
						if (fldRefList.Count < 1) continue;
						fldRefList.Sort(kvCmp);
						Utils.RemoveDuplicates(fldRefList, kvCmp);
						fldRefIndices.Clear();
						for (int j = 0, jcnt = fldRefList.Count; j < jcnt; ++j)
						{
							var faddr = fldRefList[j].Key;
							var ndx = Utils.AddressSearch(instances, faddr);
							if (ndx < 0) continue;
							fldRefIndices.Add(ndx);
							que.Enqueue(new KeyValuePair<int, ulong>(ndx, faddr));
							// save ndx -> rndx pair
						}
						if (fldRefIndices.Count < 1) continue;

						// save rndx -> ndx[] relation
						dataToPersist.Add(new KeyValuePair<int, int[]>(rndx, fldRefIndices.ToArray()));
					}
				}



				// save results in files
				dataToPersist.Add(new KeyValuePair<int, int[]>(-1, null));
				que.Clear();
				que = null;
				fldRefList.Clear();
				fldRefList = null;
				fldRefIndices.Clear();
				fldRefIndices = null;

				Utils.ForceGcWithCompaction();

				persistor.Wait();
				error = persistor.Error;

				return error == null;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}

		}


		#endregion ctors/initialization

		#region queries

		public int[] GetFieldParents(int instNdx, out string error)
		{
			var hNdx = Array.BinarySearch(_fields, instNdx);
			if (hNdx < 0)
			{
				error = Constants.InformationSymbolHeader + "Parents , not found.";
				return Utils.EmptyArray<int>.Value;
			}

			var refs = GetReferenceLists(_refsFldObjPath, _fieldReferences, out error);
			if (refs == null) return Utils.EmptyArray<int>.Value;
			return refs[hNdx];
		}

		public int[] GetParents(int instNdx, DataSource dataSource, out string error)
		{
			int[] heads;
			int[][] refs;
			if (!SelectArrays(dataSource, out heads, out refs, out error)) return null;

			var hNdx = Array.BinarySearch(heads, instNdx);
			if (hNdx < 0)
			{
				error = Constants.InformationSymbolHeader + "Parents , not found.";
				return null;
			}
			return refs[hNdx];
		}

		public KeyValuePair<IndexNode, int> GetReferences(int addrNdx, DataSource dataSource, out string error, int maxLevel = Int32.MaxValue)
		{
			error = null;
			try
			{
				int[] heads;
				int[][] refs;
				if (!SelectArrays(dataSource, out heads, out refs, out error)) return new KeyValuePair<IndexNode, int>(null, 0);

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

		public KeyValuePair<IndexNode, int>[] GetReferences(int[] addrNdxs, DataSource dataSource, out string error, int maxLevel = Int32.MaxValue)
		{
			error = null;
			try
			{
				int[] heads;
				int[][] refs;
				if (!SelectArrays(dataSource, out heads, out refs, out error)) return null;


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

		public KeyValuePair<int, int[]>[] GetMultiFieldParents(int[] indices, DataSource dataSource, List<string> errors)
		{
			try
			{
				string error;
				var lst = new List<KeyValuePair<int, int[]>>(indices.Length);

				for (int i = 0, icnt = indices.Length; i < icnt; ++i)
				{
					int[] refs = GetParents(indices[i], dataSource, out error);
					if (refs != null && refs.Length > 0)
					{
						lst.Add(new KeyValuePair<int, int[]>(indices[i], refs));
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

		private bool SelectArrays(DataSource ds, out int[] heads, out int[][] refs, out string error)
		{
			error = null;
			switch (ds)
			{
				case DataSource.AllParents:
				case DataSource.UnrootedParents:
				case DataSource.RootedParents:
					heads = _objects;
					refs = GetReferenceLists(_refsObjFldPath, _objectReferences, out error);
					return error == null;
				case DataSource.AllFields:
				case DataSource.UnrootedFields:
				case DataSource.RootedFields:
					heads = _fields;
					refs = GetReferenceLists(_refsFldObjPath, _fieldReferences, out error);
					return error == null;
				default:
					heads = null;
					refs = null;
					error = "Unknown data source.";
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
					Array.Sort(headAry,lists);
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

	}

}
