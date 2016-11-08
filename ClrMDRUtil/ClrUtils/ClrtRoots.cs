using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using ClrMDRUtil.Utils;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	public class ClrtRoots
	{
		#region Fields/Properties

		private readonly ClrtRoot[] _clrtRoots; // this array is sorted by kind and ClrtRoot.Object
		private readonly int[] _rootKindOffsets;// map to _clrtRoots array by root kind

		private readonly ulong[] _finalizerQue; // sorted sorted finalizer queue addresses
		public ulong[] FinalizerQue => _finalizerQue; // sorted sorted finalizer queue addresses
		private readonly int[] _finalizerQueInstanceIds; // map to heap instances

		private readonly ulong[] _rootAddresses;// sorted root addresses
		private readonly int[] _rootAddressMap;// sorted root addresses map to _clrtRoots array

		private readonly ulong[] _objectAddresses;// sorted object addresses
		private readonly int[] _objectAddressMap;// sorted object addresses map to _clrtRoots array

		public int FinalizerQueueCount => _finalizerQue.Length;

		public int RootsCount => _clrtRoots.Length;

		#endregion Fields/Properties

		#region Constructors/Initialization

		private ClrtRoots(ClrtRoot[] clrtRoots, int[] rootKindOffsets, ulong[] rootAddresses, int[] rootAddressMap, ulong[] objectAddresses, int[] objectAddressMap, ulong[] finalizerQue, int[] finalizeQueInstIds)
		{
			_rootAddresses = rootAddresses;
			_rootAddressMap = rootAddressMap;
			_objectAddresses = objectAddresses;
			_objectAddressMap = objectAddressMap;
			_clrtRoots = clrtRoots;
			_rootKindOffsets = rootKindOffsets;
			_finalizerQue = finalizerQue;
			_finalizerQueInstanceIds = finalizeQueInstIds;
		}

		public static ulong[] GetRootAddresses(ClrHeap heap)
		{
			var roots = heap.EnumerateRoots(true);
			var dct = new Dictionary<ulong, ulong>();

			foreach (var root in roots)
			{
				ulong rootAddr = root.Address;
				ulong objAddr = root.Object;
				if (rootAddr != 0Ul)
				{
					rootAddr = Utils.SetAsRoot(root.Address);
					ulong addr;
					if (dct.TryGetValue(root.Address, out addr))
					{
						dct[root.Address] = rootAddr | addr;
					}
					else
					{
						dct.Add(root.Address,rootAddr);
					}
				}
				if (objAddr != 0UL)
				{
					if (root.Kind == GCRootKind.Finalizer)
					{
						objAddr = Utils.SetAsFinalizer(root.Object);
					}
					objAddr = Utils.SetAsRoot(objAddr);
					ulong addr;
					if (dct.TryGetValue(root.Object, out addr))
					{
						dct[root.Object] = addr | objAddr;
					}
					else
					{
						dct.Add(root.Object,objAddr);
					}
				}
			}

			var addrAry = dct.Values.ToArray();
			var cmp = new Utils.AddressCmpAcs();
			Array.Sort(addrAry,cmp);
			return addrAry;
		}

		// TODO JRD -- delete later
		public static ClrtRoots GetRoots(ClrHeap heap, ulong[] instances, int[] types, StringIdAsyncDct idDct)
		{
			var rootDct = new SortedDictionary<ulong, List<ClrtRoot>>();
			var finalizeQue = new List<ulong>(1024 * 1024);
			var finalizeQueInstIds = new List<int>(1024 * 1024);

			List<ClrRoot> lstNotFoundObject = new List<ClrRoot>();
			List<ClrtRoot> lstRoots = new List<ClrtRoot>(1024 * 64);

			foreach (var root in heap.EnumerateRoots())
			{
				var rootObj = root.Object;
				if (root.Kind == GCRootKind.Finalizer)
				{
					finalizeQue.Add(rootObj);
					var obj = Array.BinarySearch(instances, rootObj);
					if (obj < 0) obj = Constants.InvalidIndex;
					finalizeQueInstIds.Add(obj);
					continue;
				}
				var rootAddr = root.Address;
				List<ClrtRoot> lst;
				if (rootDct.TryGetValue(rootAddr, out lst))
				{
					int i = 0;
					int icnt = lst.Count;
					var rkind = root.Kind;
					for (; i < icnt; ++i)
					{
						if (rootObj == lst[i].Object && rkind == ClrtRoot.GetGCRootKind(lst[i].RootKind)) break;
					}
					if (i == icnt)
					{
						var rname = root.Name ?? Constants.NullName;
						var rnameId = idDct.JustGetId(rname);
						var obj = Array.BinarySearch(instances, rootObj);
						if (obj < 0) { lstNotFoundObject.Add(root); }
						var typeId = obj < 0 ? Constants.InvalidIndex : types[obj];
						var clrtRoot = new ClrtRoot(root, typeId, rnameId, Constants.InvalidIndex, Constants.InvalidThreadId, Constants.InvalidIndex);
						lst.Add(clrtRoot);
						lstRoots.Add(clrtRoot);
					}
				}
				else
				{
					var rname = root.Name ?? Constants.NullName;
					var rnameId = idDct.JustGetId(rname);
					var obj = Array.BinarySearch(instances, rootObj);
					if (obj < 0) { lstNotFoundObject.Add(root); }
					var typeId = obj < 0 ? Constants.InvalidIndex : types[obj];
					var clrtRoot = new ClrtRoot(root, typeId, rnameId, Constants.InvalidIndex, Constants.InvalidThreadId, Constants.InvalidIndex);
					rootDct.Add(rootAddr, new List<ClrtRoot>() { clrtRoot });
					lstRoots.Add(clrtRoot);
				}
			}

			var rootAry = lstRoots.ToArray();
			lstRoots.Clear();
			lstRoots = null;
			ulong[] sortedObjs;
			int[] kindOffsets;
			int[] objMap;
			ulong[] sortedAddrs;
			int[] addrMap;
			SortRoots(rootAry, out kindOffsets, out sortedAddrs, out addrMap, out sortedObjs, out objMap);

			var finQueAry = finalizeQue.ToArray();
			var finQueInstIdsAry = finalizeQueInstIds.ToArray();
			Array.Sort(finQueAry, finQueInstIdsAry);

			return new ClrtRoots(rootAry, kindOffsets, sortedAddrs, addrMap, sortedObjs, objMap, finQueAry, finQueInstIdsAry);
		}


		/// <summary>
		/// Collect roots information.
		/// </summary>
		/// <param name="heap">Clr heap of current runtime.</param>
		/// <param name="instances">Instance addresses.</param>
		/// <param name="types">Instance types.</param>
		/// <param name="idDct">String id dictionary.</param>
		/// <returns>Roots info, <see cref="ClrtRoots"/></returns>
		public static ClrtRoots GetRootInfos(ClrHeap heap, ulong[] instances, int[] types, ulong[] rootAddresses, StringIdDct idDct)
		{
			var rootDct = new SortedDictionary<ulong, List<ClrtRoot>>();
			var finalizeQue = new List<ulong>(1024 * 1024);
			var finalizeQueInstIds = new List<int>(1024 * 1024);

			List<ClrRoot> lstNotFoundObject = new List<ClrRoot>();
			List<ClrtRoot> lstRoots = new List<ClrtRoot>(1024 * 64);

			foreach (var root in heap.EnumerateRoots(true))
			{
				var rootObj = root.Object;
				if (root.Kind == GCRootKind.Finalizer)
				{
					finalizeQue.Add(rootObj);
					var obj = Utils.AddressSearch(instances, rootObj);
					finalizeQueInstIds.Add(obj);
					continue;
				}
				var rootAddr = root.Address;
				List<ClrtRoot> lst;
				if (rootDct.TryGetValue(rootAddr, out lst))
				{
					int i = 0;
					int icnt = lst.Count;
					var rkind = root.Kind;
					for (; i < icnt; ++i)
					{
						if (rootObj == lst[i].Object && rkind == ClrtRoot.GetGCRootKind(lst[i].RootKind)) break;
					}
					if (i == icnt)
					{
						var rname = root.Name ?? Constants.NullName;
						var rnameId = idDct.JustGetId(rname);
						var obj = Utils.AddressSearch(instances, rootObj);
						if (obj < 0) { lstNotFoundObject.Add(root); }
						var typeId = obj < 0 ? Constants.InvalidIndex : types[obj];
						var clrtRoot = new ClrtRoot(root, typeId, rnameId, Constants.InvalidIndex, Constants.InvalidThreadId, Constants.InvalidIndex);
						lst.Add(clrtRoot);
						lstRoots.Add(clrtRoot);
					}
				}
				else
				{
					var rname = root.Name ?? Constants.NullName;
					var rnameId = idDct.JustGetId(rname);
					var obj = Utils.AddressSearch(instances, rootObj);
					if (obj < 0) { lstNotFoundObject.Add(root); }
					var typeId = obj < 0 ? Constants.InvalidIndex : types[obj];
					var clrtRoot = new ClrtRoot(root, typeId, rnameId, Constants.InvalidIndex, Constants.InvalidThreadId, Constants.InvalidIndex);
					rootDct.Add(rootAddr, new List<ClrtRoot>() { clrtRoot });
					lstRoots.Add(clrtRoot);
				}
			}

			var rootAry = lstRoots.ToArray();
			lstRoots.Clear();
			lstRoots = null;
			ulong[] sortedObjs;
			int[] kindOffsets;
			int[] objMap;
			ulong[] sortedAddrs;
			int[] addrMap;
			SortRoots(rootAry, out kindOffsets, out sortedAddrs, out addrMap, out sortedObjs, out objMap);

			var finQueAry = finalizeQue.ToArray();
			var finQueInstIdsAry = finalizeQueInstIds.ToArray();
			Array.Sort(finQueAry, finQueInstIdsAry);

			return new ClrtRoots(rootAry, kindOffsets, sortedAddrs, addrMap, sortedObjs, objMap, finQueAry, finQueInstIdsAry);
		}

		public static void SortRoots(ClrtRoot[] roots, out int[] kindOffsets, out ulong[] addresses, out int[] addressMap, out ulong[] objects, out int[] objectMap)
		{
			objects = Utils.EmptyArray<ulong>.Value;
			objectMap = Utils.EmptyArray<int>.Value;
			kindOffsets = Utils.EmptyArray<int>.Value;
			addresses = objects;
			addressMap = objectMap;
			if (roots == null || roots.Length < 1) return;
			Array.Sort(roots, new ClrtRootKindCmp());

			List<int> offs = new List<int>(16) { 0 };
			ClrtRoot.Kinds pkind = ClrtRoot.GetGCRootKindPart(roots[0].RootKind);
			objects = new ulong[roots.Length];
			objectMap = new int[roots.Length];
			objects[0] = roots[0].Object;
			objectMap[0] = 0;
			addresses = new ulong[roots.Length];
			addressMap = new int[roots.Length];
			addresses[0] = roots[0].Address;
			addressMap[0] = 0;
			for (int i = 1, icnt = roots.Length; i < icnt; ++i)
			{
				objects[i] = roots[i].Object;
				objectMap[i] = i;
				addresses[i] = roots[i].Address;
				addressMap[i] = i;
				var kind = ClrtRoot.GetGCRootKindPart(roots[0].RootKind);
				if (pkind != kind)
				{
					offs.Add(i);
					pkind = kind;
				}
			}
			offs.Add(roots.Length);
			kindOffsets = offs.ToArray();
			Array.Sort(objects, objectMap);
			Array.Sort(addresses, addressMap);
		}

		#endregion Constructors/Initialization

		#region Dump/Load


		// TODO JRD -- delete later
		public bool Dump(int r, string indexFolder, string dumpName, out string error)
		{
			error = null;
			BinaryWriter bw = null;
			try
			{
				// dump ClrtRoot array
				//
				var path = DumpFileMoniker.GetFilePath(r, indexFolder, dumpName, Constants.MapRootsFilePostfix);
				bw = new BinaryWriter(File.Open(path, FileMode.Create));
				int cnt = _rootKindOffsets.Length; // offsets first
				bw.Write(cnt);
				for (int i = 0; i < cnt; ++i)
				{
					bw.Write(_rootKindOffsets[i]);
				}
				cnt = _clrtRoots.Length; // root infos next
				bw.Write(cnt);
				for (int i = 0; i < cnt; ++i)
				{
					_clrtRoots[i].Dump(bw);
				}
				bw.Close();
				bw = null;

				// dump finalizer array
				//
				path = DumpFileMoniker.GetFilePath(r, indexFolder, dumpName, Constants.MapFinalizerFilePostfix);
				if (!Utils.WriteUlongIntArrays(path, _finalizerQue, _finalizerQueInstanceIds, out error)) return false;

				// dump root addresses array and its map
				//
				path = DumpFileMoniker.GetFilePath(r, indexFolder, dumpName, Constants.MapRootAddressesFilePostfix);
				if (!Utils.WriteUlongIntArrays(path, _rootAddresses, _rootAddressMap, out error)) return false;

				// dump object addresses array and its map
				//
				path = DumpFileMoniker.GetFilePath(r, indexFolder, dumpName, Constants.MapRootObjectsFilePostfix);
				if (!Utils.WriteUlongIntArrays(path, _objectAddresses, _objectAddressMap, out error)) return false;

				return true;

			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				bw?.Close();
			}
		}

		// TODO JRD -- delete later
		public static ClrtRoots Load(int r, string indexFolder, string dumpName, out string error)
		{
			error = null;
			BinaryReader br = null;
			try
			{
				// get root infos
				//
				var path = DumpFileMoniker.GetFilePath(r, indexFolder, dumpName, Constants.MapRootsFilePostfix);
				br = new BinaryReader(File.Open(path, FileMode.Open));
				int cnt = br.ReadInt32(); // kind offsets first
				int[] kindOffsets = new int[cnt];
				for (int i = 0; i < cnt; ++i)
				{
					kindOffsets[i] = br.ReadInt32();
				}
				cnt = br.ReadInt32(); // root infos
				ClrtRoot[] rootInfos = new ClrtRoot[cnt];
				for (int i = 0; i < cnt; ++i)
				{
					rootInfos[i] = ClrtRoot.Load(br);
				}

				// get finalizer queue
				//
				path = DumpFileMoniker.GetFilePath(r, indexFolder, dumpName, Constants.MapFinalizerFilePostfix);
				ulong[] finQue;
				int[] finQueInstIds;
				if (!Utils.ReadUlongIntArrays(path, out finQue, out finQueInstIds, out error)) return null;

				// get root addresses array and its map
				//
				path = DumpFileMoniker.GetFilePath(r, indexFolder, dumpName, Constants.MapRootAddressesFilePostfix);
				ulong[] addresses;
				int[] addrMap;
				if (!Utils.ReadUlongIntArrays(path, out addresses, out addrMap, out error)) return null;

				// get root addresses array and its map
				//
				path = DumpFileMoniker.GetFilePath(r, indexFolder, dumpName, Constants.MapRootObjectsFilePostfix);
				ulong[] objects;
				int[] objMap;
				if (!Utils.ReadUlongIntArrays(path, out objects, out objMap, out error)) return null;

				return new ClrtRoots(rootInfos, kindOffsets, addresses, addrMap, objects, objMap, finQue, finQueInstIds);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				br?.Close();
			}
		}

		public static ClrtRoots LoadRoots(int r, DumpFileMoniker fileMoniker, out string error)
		{
			error = null;
			BinaryReader br = null;
			try
			{
				// get root infos
				//
				var path = fileMoniker.GetFilePath(r, Constants.MapRootsInfoFilePostfix);
				br = new BinaryReader(File.Open(path, FileMode.Open));
				int cnt = br.ReadInt32(); // kind offsets first
				int[] kindOffsets = new int[cnt];
				for (int i = 0; i < cnt; ++i)
				{
					kindOffsets[i] = br.ReadInt32();
				}
				cnt = br.ReadInt32(); // root infos
				ClrtRoot[] rootInfos = new ClrtRoot[cnt];
				for (int i = 0; i < cnt; ++i)
				{
					rootInfos[i] = ClrtRoot.Load(br);
				}

				// get finalizer queue
				//
				path = fileMoniker.GetFilePath(r, Constants.MapRootsFinalizerFilePostfix);
				ulong[] finQue;
				int[] finQueInstIds;
				if (!Utils.ReadUlongIntArrays(path, out finQue, out finQueInstIds, out error)) return null;

				// get root addresses array and its map
				//
				path = fileMoniker.GetFilePath(r, Constants.MapRootsAddressesFilePostfix);
				ulong[] addresses;
				int[] addrMap;
				if (!Utils.ReadUlongIntArrays(path, out addresses, out addrMap, out error)) return null;

				// get root addresses array and its map
				//
				path = fileMoniker.GetFilePath(r, Constants.MapRootsObjectsFilePostfix);
				ulong[] objects;
				int[] objMap;
				if (!Utils.ReadUlongIntArrays(path, out objects, out objMap, out error)) return null;

				return new ClrtRoots(rootInfos, kindOffsets, addresses, addrMap, objects, objMap, finQue, finQueInstIds);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				br?.Close();
			}
		}

		/// <summary>
		/// Save root indexing data.
		/// </summary>
		/// <param name="r">Current runtime index.</param>
		/// <param name="fileMoniker">Dump path information.</param>
		/// <param name="error">Error message if any.</param>
		/// <returns>False if failed, true otherwise.</returns>
		public bool PersitRootInfos(int r, DumpFileMoniker fileMoniker, out string error)
		{
			error = null;
			BinaryWriter bw = null;
			try
			{
				// dump ClrtRoot array
				//
				var path = fileMoniker.GetFilePath(r, Constants.MapRootsInfoFilePostfix);
				bw = new BinaryWriter(File.Open(path, FileMode.Create));
				int cnt = _rootKindOffsets.Length; // offsets first
				bw.Write(cnt);
				for (int i = 0; i < cnt; ++i)
				{
					bw.Write(_rootKindOffsets[i]);
				}
				cnt = _clrtRoots.Length; // root infos next
				bw.Write(cnt);
				for (int i = 0; i < cnt; ++i)
				{
					_clrtRoots[i].Dump(bw);
				}
				bw.Close();
				bw = null;

				// dump finalizer array
				//
				path = fileMoniker.GetFilePath(r, Constants.MapRootsFinalizerFilePostfix);
				if (!Utils.WriteUlongIntArrays(path, _finalizerQue, _finalizerQueInstanceIds, out error)) return false;

				// dump root addresses array and its map
				//
				path = fileMoniker.GetFilePath(r, Constants.MapRootsAddressesFilePostfix);
				if (!Utils.WriteUlongIntArrays(path, _rootAddresses, _rootAddressMap, out error)) return false;

				// dump object addresses array and its map
				//
				path = fileMoniker.GetFilePath(r, Constants.MapRootsObjectsFilePostfix);
				if (!Utils.WriteUlongIntArrays(path, _objectAddresses, _objectAddressMap, out error)) return false;

				return true;

			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				bw?.Close();
			}
		}


		#endregion Dump/Load

		#region Queries

		public bool IsRootedOutsideFinalization(ulong addr)
		{
			var ndx = Array.BinarySearch(_objectAddresses, addr);
			if (ndx >= 0) return true;
			return Array.BinarySearch(_rootAddresses, addr) >= 0;
		}

		public bool GetRootInfoByObject(ulong addr, out ClrtRoot info)
		{
			var ndx = Array.BinarySearch(_objectAddresses, addr);
			if (ndx < 0)
			{
				info = ClrtRoot.EmptyClrtRoot;
				return false;
			}

			info = _clrtRoots[_objectAddressMap[ndx]];
			return true;
		}

		public bool GetRootInfoByAddress(ulong addr, out ClrtRoot info)
		{
			var ndx = Array.BinarySearch(_rootAddresses, addr);
			if (ndx < 0)
			{
				info = ClrtRoot.EmptyClrtRoot;
				return false;
			}

			info = _clrtRoots[_rootAddressMap[ndx]];
			return true;
		}

		public bool GetRootInfo(ulong addr, out ClrtRoot info, out bool inFinalizerQueue)
		{
			inFinalizerQueue = Array.BinarySearch(_finalizerQue, addr) >= 0;
			var ndx = Array.BinarySearch(_objectAddresses, addr);
			if (ndx >= 0)
			{
				info = _clrtRoots[_objectAddressMap[ndx]];
				return true;
			}
			return GetRootInfoByAddress(addr, out info);
		}

		public ClrtRoot[] GetRootInfoByType(int typeId)
		{
			List<ClrtRoot> lst = new List<ClrtRoot>(64);
			for (int i = 0, icnt = _clrtRoots.Length; i < icnt; ++i)
			{
				if (_clrtRoots[i].TypeId == typeId)
					lst.Add(_clrtRoots[i]);
			}
			return lst.ToArray();
		}

		public bool GetFinilizerItemInstanceId(ulong addr, out int instId)
		{
			var ndx = Array.BinarySearch(_finalizerQue, addr);
			instId = ndx < 0 ? Constants.InvalidIndex : _finalizerQueInstanceIds[ndx];
			return ndx >= 0;
		}

		public triple<string, string, string>[] GetDisplayableFinalizationQueue(int[] instTypes, string[] typeNames, ulong[] unrooted)
		{
			var que = new triple<string, string, string>[_finalizerQue.Length];
			for (int i = 0, icnt = _finalizerQue.Length; i < icnt; ++i)
			{
				var addrStr = Utils.AddressString(_finalizerQue[i]);
				var typeId = _finalizerQueInstanceIds[i];
				var notRooted = IsRooted(_finalizerQue[i], unrooted) ? string.Empty : Constants.HeavyCheckMarkHeader;
				string typeName = typeId != Constants.InvalidIndex ? typeNames[instTypes[typeId]] : Constants.UnknownName;
				que[i] = new triple<string, string, string>(addrStr, notRooted, typeName);


			}
			return que;
		}

		public bool IsRooted(ulong addr, ulong[] unrooted)
		{
			return Array.BinarySearch(unrooted, addr) < 0;
		}

		#endregion Queries
	}

	public class RootInfo
	{
		private ClrtRoot _root;
		private bool _inFinalizerQueue;
		public ClrtRoot Root => _root;
		public bool InFinalizerQueue => _inFinalizerQueue;

		public RootInfo(ClrtRoot root, bool inFinalizerQueue)
		{
			_root = root;
			_inFinalizerQueue = inFinalizerQueue;
		}

		public override string ToString()
		{
			return Utils.AddressString(_root.Address) + "->" + Utils.AddressString(_root.Object) + " " + _root.GetKindString() +
				   (_inFinalizerQueue ? " FQ" : String.Empty);
		}
	}



	public class ClrtRootKindCmp : IComparer<ClrtRoot>
	{
		public int Compare(ClrtRoot a, ClrtRoot b)
		{
			var cmp = a.RootKind < b.RootKind ? -1 : (a.RootKind > b.RootKind ? 1 : 0);
			return cmp != 0
				? cmp
				: (a.Object < b.Object ? -1 : (a.Object > b.Object ? 1 : 0));
		}
	}

}
