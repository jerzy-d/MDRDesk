using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClrMDRUtil.Utils;
using DmpNdxQueries;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.ICorDebug;

namespace ClrMDRIndex
{
	public sealed class DumpIndex : IDisposable
	{
		[Flags]
		public enum IndexType
		{
			InstanceRefrences = 1,

			All = 0xFFFFFFF,
		}

		#region fields/properties

		public static KvIntIntKeyCmp kvIntIntKeyCmp = new KvIntIntKeyCmp();

		const int MaxNodes = 10000;

		public IndexType Type { get; private set; }
		public static bool Is64Bit;

		private DumpFileMoniker _fileMoniker;
		public string AdhocFolder => _fileMoniker.OutputFolder;
		public string OutputFolder => AdhocFolder;
		public string IndexFolder => _fileMoniker.MapFolder;
		public string DumpPath => _fileMoniker.Path;
		public string DumpFileName => _fileMoniker.FileName;

		private string _dumpInfo;
		public string DumpInfo => _dumpInfo;

		private int _runtimeCount;
		private int _currentRuntimeIndex;
		public int CurrentRuntimeIndex => _currentRuntimeIndex;

		private ulong[] _instances;
		public ulong[] Instances => _instances;
		private int[] _instanceTypes;
		public int[] InstanceTypes => _instanceTypes;
		private int[] _typeInstanceMap;
		private KeyValuePair<int, int>[] _typeInstanceOffsets;

		private string[] _typeNames;
		public string[] TypeNames;
		private KeyValuePair<string, int>[] _displayableTypeNames;
		public KeyValuePair<string, int>[] DisplayableTypeNames => _displayableTypeNames;
		private KeyValuePair<string, int>[] _reversedTypeNames;
		public KeyValuePair<string, int>[] ReversedTypeNames => _reversedTypeNames;
		private KeyValuePair<string, KeyValuePair<string, int>[]>[] _typeNamespaces;
		public KeyValuePair<string, KeyValuePair<string, int>[]>[] TypeNamespaces => _typeNamespaces;
		public int UsedTypeCount => _displayableTypeNames.Length;

		private InstanceReferences _instanceReferences;

		public WeakReference<StringStats> _stringStats;
		public WeakReference<uint[]> _sizes;
		public WeakReference<uint[]> _baseSizes;
		public WeakReference<ClrElementType[]> _elementTypes;
		public WeakReference<Tuple<int[], int[]>> _arraySizes;

		private ClrtSegment[] _segments; // segment infos, for instances generation histograms
		private bool _segmentInfoUnrooted;

		private ClrtDump _clrtDump;
		public ClrtDump Dump => _clrtDump;
		public ClrRuntime Runtime => _clrtDump.Runtime;

		private string[] _stringIds; // ordered by string ids TODO JRD
		public string[] StringIds => _stringIds;

		private ClrtRootInfo _roots;

		private IndexProxy _indexProxy;
		public IndexProxy IndexProxy => _indexProxy;

		private int[] _deadlock;
		public int[] Deadlock => _deadlock;
		private int[] _threadBlockingMap;
		private int[] _blockBlockingMap;
		private Digraph _threadBlockgraph;
		public Digraph ThreadBlockgraph => _threadBlockgraph;
		public bool DeadlockFound => _deadlock.Length > 0;
		private ClrtThread[] _threads;
		private ClrtBlkObject[] _blocks;

		#endregion fields/properties

		#region ctors/initialization

		private DumpIndex(string dumpOrIndexPath, int runtimeIndex, IndexType type = IndexType.All)
		{
			Type = IndexType.All;
			if (dumpOrIndexPath.EndsWith(".map", StringComparison.OrdinalIgnoreCase))
			{
				dumpOrIndexPath = dumpOrIndexPath.Substring(0, dumpOrIndexPath.Length - 4);
			}
			_fileMoniker = new DumpFileMoniker(dumpOrIndexPath);
			Is64Bit = Environment.Is64BitOperatingSystem;
			_currentRuntimeIndex = runtimeIndex;
		}

		public static DumpIndex OpenIndexInstanceReferences(Version version, string dumpPath, int runtimeNdx,
			out string error, IProgress<string> progress = null)
		{
			error = null;
			try
			{
				var index = new DumpIndex(dumpPath, runtimeNdx, IndexType.InstanceRefrences);
				index._dumpInfo = index.LoadDumpInfo(out error);
				if (!Utils.IsIndexVersionCompatible(version, index.DumpInfo))
				{
					error = Utils.GetErrorString("Failed to Open Index", index._fileMoniker.MapFolder,
						"Index version is not compatible with this application's version."
						+ Environment.NewLine
						+ "Please reindex the corresponding crash dump.");
					index.Dispose();
					return null;

				}
				if (!index.LoadInstanceReferences(out error)) return null;
				if (!index.InitDump(out error, progress)) return null;
				index._indexProxy = new IndexProxy(index.Dump, index._instances, index._instanceTypes, index._typeNames);
				return index;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		private bool LoadInstanceReferences(out string error)
		{
			error = null;
			try
			{
				string path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapInstancesFilePostfix);
				_instances = Utils.ReadUlongArray(path, out error);
				if (error != null) return false;
				path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapInstanceTypesFilePostfix);
				_instanceTypes = Utils.ReadIntArray(path, out error);
				if (error != null) return false;

				// types/instances map
				//
				path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapTypeInstanceMapFilePostfix);
				_typeInstanceMap = Utils.ReadIntArray(path, out error);
				if (error != null) return false;
				path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapTypeInstanceOffsetsFilePostfix);
				_typeInstanceOffsets = Utils.ReadKvIntIntArray(path, out error);
				if (error != null) return false;

				// segments -- generation info
				//
				path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapSegmentInfoFilePostfix);

				_segments = ClrtSegment.ReadSegments(path, out _segmentInfoUnrooted, out error);

				// roots
				//
				_roots = ClrtRootInfo.Load(_currentRuntimeIndex, _fileMoniker, out error);
				if (error != null) return false;

				// typenames
				//
				path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.TxtTypeNamesFilePostfix);
				_typeNames = Utils.GetStringListFromFile(path, out error);
				if (error != null) return false;
				{
					path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.TxtReversedTypeNamesFilePostfix);
					var dispNames = GetDisplayableTypeNames(path, _typeNames, _typeInstanceOffsets, out error);
					_displayableTypeNames = dispNames.Item1;
					_reversedTypeNames = dispNames.Item2;
					if (error != null) return false;
					Array.Sort(_reversedTypeNames, new KvStrIntKeyCmp());
					_typeNamespaces = GetTypeNamespaceOrdering(_reversedTypeNames);
				}

				_instanceReferences = new InstanceReferences(_fileMoniker.Path, _currentRuntimeIndex, _instances,
					_instanceTypes, _typeNames);
				_instanceReferences.Init(out error);
				if (error != null) return false;

				// threads and blocks
				//
				if (!LoadThreadBlockGraph(out error))
				{
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}

		private bool LoadThreadBlockGraph(out string error)
		{
			error = null;
			BinaryReader br = null;
			try
			{
				var path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapThreadsAndBlocksGraphFilePostfix);
				br = new BinaryReader(File.Open(path, FileMode.Open));
				int cycleCount = br.ReadInt32();
				_deadlock = cycleCount > 0 ? new int[cycleCount] : Utils.EmptyArray<int>.Value;
				for (int i = 0; i < cycleCount; ++i)
					_deadlock[i] = br.ReadInt32();
				int count = br.ReadInt32();
				_threadBlockingMap = count > 0 ? new int[count] : Utils.EmptyArray<int>.Value;
				for (int i = 0; i < count; ++i)
					_threadBlockingMap[i] = br.ReadInt32();

				count = br.ReadInt32();
				_blockBlockingMap = count > 0 ? new int[count] : Utils.EmptyArray<int>.Value;
				for (int i = 0; i < count; ++i)
					_blockBlockingMap[i] = br.ReadInt32();

				_threadBlockgraph = Digraph.Load(br, out error);

				if (DeadlockFound)
				{
					LoadThreadsAndBlocks(out error);
				}
				return error == null;
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


		private bool LoadThreadsAndBlocks(out string error)
		{
			error = null;
			BinaryReader br = null;
			try
			{
				var path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapThreadsAndBlocksFilePostfix);
				br = new BinaryReader(File.Open(path, FileMode.Open));
				int threadCnt = br.ReadInt32();
				_threads = new ClrtThread[threadCnt];
				for (int i = 0; i < threadCnt; ++i)
				{
					_threads[i] = ClrtThread.Load(br);
				}
				int blockCnt = br.ReadInt32();
				_blocks = new ClrtBlkObject[blockCnt];
				for (int i = 0; i < blockCnt; ++i)
				{
					_blocks[i] = ClrtBlkObject.Load(br);
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

		private Tuple<KeyValuePair<string, int>[], KeyValuePair<string, int>[]> GetDisplayableTypeNames(string path,
			string[] allTypeNames, KeyValuePair<int, int>[] knownTypes, out string error)
		{
			error = null;
			StreamReader rd = null;
			try
			{
				int knownTypesCount = knownTypes.Length - 1; // skip last dummy type
				KeyValuePair<string, int>[] revtTypeNames = new KeyValuePair<string, int>[knownTypesCount];
				KeyValuePair<string, int>[] dispTypeNames = new KeyValuePair<string, int>[knownTypesCount];
				rd = new StreamReader(path);
				var ln = rd.ReadLine();
				var count = Int32.Parse(ln);
				int curKnownTypeNdx = 0;
				int curKnownTypeId = knownTypes[0].Key;
				for (int i = 0; i < count && curKnownTypeNdx < knownTypesCount; ++i)
				{
					ln = rd.ReadLine();
					if (i < curKnownTypeId) continue;
					Debug.Assert(i == curKnownTypeId);
					revtTypeNames[curKnownTypeNdx] = new KeyValuePair<string, int>(ln, i);
					dispTypeNames[curKnownTypeNdx] = new KeyValuePair<string, int>(allTypeNames[i], i);

					curKnownTypeId = knownTypes[++curKnownTypeNdx].Key;
				}
				Debug.Assert(curKnownTypeNdx == knownTypesCount);
				return new Tuple<KeyValuePair<string, int>[], KeyValuePair<string, int>[]>(dispTypeNames, revtTypeNames);

			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				rd?.Close();
			}
		}

		private KeyValuePair<string, KeyValuePair<string, int>[]>[] GetTypeNamespaceOrdering(
			KeyValuePair<string, int>[] reversedNames)
		{
			Debug.Assert(reversedNames != null && reversedNames.Length > 0);
			string[] splitter = new[] {Constants.NamespaceSepPadded};
			Dictionary<string, string> strCache = new Dictionary<string, string>(StringComparer.Ordinal);
			var resultDct = new SortedDictionary<string, List<KeyValuePair<string, int>>>(StringComparer.Ordinal);

			string[] items = reversedNames[0].Key.Split(splitter, StringSplitOptions.RemoveEmptyEntries);
			Debug.Assert(items.Length > 0);
			string name = Utils.GetCachedString(items[0], strCache);
			string prevNamesp = items.Length > 1 ? items[1] : string.Empty;
			var names = new List<KeyValuePair<string, int>>(128)
			{
				new KeyValuePair<string, int>(name, reversedNames[0].Value)
			};
			var namespInfos = new List<KeyValuePair<string, KeyValuePair<string, int>[]>>();
			for (int i = 1, icnt = reversedNames.Length; i < icnt; ++i)
			{
				var reversedNameInfo = reversedNames[i];
				items = reversedNameInfo.Key.Split(splitter, StringSplitOptions.RemoveEmptyEntries);
				Debug.Assert(items.Length > 0);
				name = Utils.GetCachedString(items[0], strCache);
				string namesp = items.Length > 1 ? Utils.GetCachedString(items[1], strCache) : string.Empty;
				List<KeyValuePair<string, int>> lst;
				if (resultDct.TryGetValue(namesp, out lst))
				{
					lst.Add(new KeyValuePair<string, int>(name, reversedNameInfo.Value));
					continue;
				}
				resultDct.Add(namesp,
					new List<KeyValuePair<string, int>>() {new KeyValuePair<string, int>(name, reversedNameInfo.Value)});
			}
			var result = new KeyValuePair<string, KeyValuePair<string, int>[]>[resultDct.Count];
			int ndx = 0;
			foreach (var kv in resultDct)
			{
				result[ndx++] = new KeyValuePair<string, KeyValuePair<string, int>[]>(
					kv.Key,
					kv.Value.ToArray()
				);
			}

			return result;
		}


		/// <summary>
		/// Read a text file with general dump information.
		/// </summary>
		/// <param name="error">Error message if io operation fails.</param>
		/// <returns>General dump information as string.</returns>
		private string LoadDumpInfo(out string error)
		{
			var path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.TxtIndexInfoFilePostfix);

			error = null;
			StreamReader rd = null;
			try
			{
				rd = new StreamReader(path);
				return rd.ReadToEnd();
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				rd?.Close();
			}
		}

		#endregion ctors/initialization

		#region utils

		private int GetInstanceIndex(ulong address)
		{
			return Utils.AddressSearch(_instances, address);
		}

		private string GetAddressString(IList<int> instIndices)
		{
			int cnt = instIndices != null ? 0 : instIndices.Count;
			return cnt == 1
				? Utils.AddressString(GetInstanceAddress(instIndices[0]))
				: (instIndices.Count == 0 ? Utils.AddressString(0UL) : Utils.MultiAddressString(instIndices.Count));
		}

		#endregion utils

		#region queries

		#region io

		public string GetFilePath(int r, string postfix)
		{
			return _fileMoniker.GetFilePath(r, postfix);
		}

		#endregion io

		#region heap

		public ClrHeap GetFreshHeap()
		{
			return Dump.GetFreshHeap();
		}

		#endregion heap

		#region types

		public string GetTypeName(ulong address)
		{
			var instanceIndex = GetInstanceIndex(address);
			return instanceIndex == Constants.InvalidIndex
				? Constants.UnknownTypeName
				: _typeNames[_instanceTypes[instanceIndex]];
		}

		public int GetTypeId(ulong addr)
		{
			var instanceIndex = GetInstanceIndex(addr);
			return instanceIndex == Constants.InvalidIndex 
				? Constants.InvalidIndex
				: _instanceTypes[instanceIndex];
		}

		public string GetTypeName(int id)
		{
			return (id >= 0 && id < _typeNames.Length) ? _typeNames[id] : Constants.UnknownTypeName;
		}

		public int GetTypeId(int instanceId)
		{
			return (instanceId >= 0 && instanceId < _instanceTypes.Length) ? _instanceTypes[instanceId] : Constants.InvalidIndex;
		}

		public ulong GetInstanceAddress(int instanceId)
		{
			return (instanceId >= 0 && instanceId < _instances.Length) ? _instances[instanceId] : Constants.InvalidAddress;
		}

		public int GetTypeId(string typeName)
		{
			var id = Array.BinarySearch(_typeNames, typeName, StringComparer.Ordinal);
			return id < 0 ? Constants.InvalidIndex : id;
		}

		public ulong[] GetTypeInstances(int typeId)
		{
			var offNdx = Array.BinarySearch(_typeInstanceOffsets, new KeyValuePair<int, int>(typeId, 0), kvIntIntKeyCmp);
			if (offNdx < 0) return Utils.EmptyArray<ulong>.Value;
			var count = _typeInstanceOffsets[offNdx + 1].Value - _typeInstanceOffsets[offNdx].Value;
			ulong[] addresses = new ulong[count];
			int mapIndex = _typeInstanceOffsets[offNdx].Value;
			for (int i = 0; i < count; ++i)
			{
				addresses[i] = _instances[_typeInstanceMap[mapIndex++]];
			}
			Utils.SortAddresses(addresses);
			return addresses;
		}


		public ulong[] GetTypeRealAddresses(int typeId)
		{
			var offNdx = Array.BinarySearch(_typeInstanceOffsets, new KeyValuePair<int, int>(typeId, 0), kvIntIntKeyCmp);
			if (offNdx < 0) return Utils.EmptyArray<ulong>.Value;
			var count = _typeInstanceOffsets[offNdx + 1].Value - _typeInstanceOffsets[offNdx].Value;
			ulong[] addresses = new ulong[count];
			int mapIndex = _typeInstanceOffsets[offNdx].Value;
			for (int i = 0; i < count; ++i)
			{
				addresses[i] = Utils.RealAddress(_instances[_typeInstanceMap[mapIndex++]]);
			}
			Utils.SortAddresses(addresses);
			return addresses;
		}

		public int[] GetTypeInstanceIndices(int typeId)
		{
			var offNdx = Array.BinarySearch(_typeInstanceOffsets, new KeyValuePair<int, int>(typeId, 0), kvIntIntKeyCmp);
			if (offNdx < 0) return Utils.EmptyArray<int>.Value;
			var count = _typeInstanceOffsets[offNdx + 1].Value - _typeInstanceOffsets[offNdx].Value;
			int[] indices = new int[count];
			int mapIndex = _typeInstanceOffsets[offNdx].Value;
			for (int i = 0; i < count; ++i)
			{
				indices[i] = _typeInstanceMap[mapIndex++];
			}
			return indices;
		}

		public int GetTypeInstanceCount(int typeId)
		{
			var offNdx = Array.BinarySearch(_typeInstanceOffsets, new KeyValuePair<int, int>(typeId, 0), kvIntIntKeyCmp);
			if (offNdx < 0) return 0;
			var count = _typeInstanceOffsets[offNdx + 1].Value - _typeInstanceOffsets[offNdx].Value;
			return count;
		}

		public KeyValuePair<string, KeyValuePair<string, int>[]>[] GetNamespaceDisplay()
		{
			return _typeNamespaces;
		}

		public int[] GetTypeIds(string prefix, bool includeArrays)
		{
			List<int> lst = new List<int>();
			int id = Array.BinarySearch(_typeNames, prefix, StringComparer.Ordinal);
			if (id < 0) id = ~id;
			for (int i = id; i >= 0 && i < _typeNames.Length; --i)
			{
				string typeName = _typeNames[i];
				if (typeName.StartsWith(prefix, StringComparison.Ordinal))
				{
					if (!includeArrays && typeName.EndsWith("[]",StringComparison.Ordinal))
						continue;
					lst.Add(i);
				}
				else break;
			}
			for (int i = id + 1; i < _typeNames.Length; ++i)
			{
				string typeName = _typeNames[i];
				if (typeName.StartsWith(prefix, StringComparison.Ordinal))
				{
					if (!includeArrays && typeName.EndsWith("[]", StringComparison.Ordinal))
						continue;
					lst.Add(i);
				}
				else break;
			}
			lst.Sort();
			return lst.Count > 0 ? lst.ToArray() : Utils.EmptyArray<int>.Value;
		}

		public KeyValuePair<int, ulong[]>[] GetTypeAddresses(int[] typeIds, out int totalCount)
		{
			totalCount = 0;
			KeyValuePair<int, ulong[]>[] result = new KeyValuePair<int, ulong[]>[typeIds.Length];
			for (int i = 0, icnt = typeIds.Length; i < icnt; ++i)
			{
				var addrAry = GetTypeInstances(typeIds[i]);
				totalCount += addrAry.Length;
				result[i] = new KeyValuePair<int, ulong[]>(typeIds[i], addrAry);
			}
			return result;
		}

		public KeyValuePair<int, ulong[]>[] GetTypeRealAddresses(int[] typeIds, out int totalCount)
		{
			totalCount = 0;
			KeyValuePair<int, ulong[]>[] result = new KeyValuePair<int, ulong[]>[typeIds.Length];
			for (int i = 0, icnt = typeIds.Length; i < icnt; ++i)
			{
				var addrAry = GetTypeRealAddresses(typeIds[i]);
				totalCount += addrAry.Length;
				result[i] = new KeyValuePair<int, ulong[]>(typeIds[i], addrAry);
			}
			return result;
		}
		//private int[] RemoveArrayType(int[] typeIds)
		//{
		//	int cntToRemove = 0;
		//	for (int i = 0, icnt = typeIds.Length; i < icnt; ++i)
		//	{
		//		if (Types.IsArray(typeIds[i]))
		//		{
		//			typeIds[i] = Int32.MaxValue;
		//			++cntToRemove;
		//		}
		//	}
		//	if (cntToRemove > 0)
		//	{
		//		int[] newAry = new int[typeIds.Length - cntToRemove];
		//		int ndx = 0;
		//		for (int i = 0, icnt = typeIds.Length; i < icnt; ++i)
		//		{
		//			if (typeIds[i] != Int32.MaxValue)
		//				newAry[ndx++] = typeIds[i];
		//		}
		//		return newAry;
		//	}
		//	return typeIds;
		//}

		public ListingInfo GetAllTypesSizesInfo(bool baseSize)
		{
			string error = null;
			try
			{
				uint[] sizes = GetSizeArray(baseSize, out error);
				if (error != null) return new ListingInfo(error);
				Debug.Assert(sizes.Length == _instances.Length);

				ulong grandTotal = 0UL;
				var typeDct = new SortedDictionary<string, quadruple<int, ulong, ulong, ulong>>(StringComparer.Ordinal);

				for (int i = 0, icnt = sizes.Length; i < icnt; ++i)
				{
					var sz = sizes[i];
					var typeName = _typeNames[_instanceTypes[i]];
					grandTotal += sz;
					quadruple<int, ulong, ulong, ulong> info;
					if (typeDct.TryGetValue(typeName, out info))
					{
						var maxSz = info.Third < sz ? sz : info.Third;
						var minSz = info.Third > sz ? sz : info.Third;
						typeDct[typeName] = new quadruple<int, ulong, ulong, ulong>(
							info.First + 1,
							info.Second + sz,
							maxSz,
							minSz
						);
					}
					else
					{
						typeDct.Add(typeName, new quadruple<int, ulong, ulong, ulong>(1, sz, sz, sz));
					}

				}
				var dataAry = new string[typeDct.Count * 6];
				var infoAry = new listing<string>[typeDct.Count];
				int off = 0;
				int ndx = 0;
				foreach (var kv in typeDct)
				{
					infoAry[ndx++] = new listing<string>(dataAry, off, 6);
					int cnt = kv.Value.First;
					ulong totSz = kv.Value.Second;
					ulong maxSz = kv.Value.Third;
					ulong minSz = kv.Value.Forth;
					double avg = (double)totSz / (double)(cnt);
					ulong uavg = Convert.ToUInt64(avg);

					dataAry[off++] = Utils.LargeNumberString(cnt);
					dataAry[off++] = Utils.LargeNumberString(totSz);
					dataAry[off++] = Utils.LargeNumberString(maxSz);
					dataAry[off++] = Utils.LargeNumberString(minSz);
					dataAry[off++] = Utils.LargeNumberString(uavg);
					dataAry[off++] = kv.Key;
				}

				ColumnInfo[] colInfos = new[]
				{
					new ColumnInfo("Count", ReportFile.ColumnType.Int32, 150, 1, true),
					new ColumnInfo("Total Size", ReportFile.ColumnType.UInt64, 150, 2, true),
					new ColumnInfo("Max Size", ReportFile.ColumnType.UInt64, 150, 3, true),
					new ColumnInfo("Min Size", ReportFile.ColumnType.UInt64, 150, 4, true),
					new ColumnInfo("Avg Size", ReportFile.ColumnType.UInt64, 150, 5, true),
					new ColumnInfo("Type", ReportFile.ColumnType.String, 500, 6, true),
				};
				Array.Sort(infoAry, new ListingNumCmpDesc(0));

				string descr = "Type Count: " + Utils.CountString(typeDct.Count) + Environment.NewLine
							   + "Instance Count: " + Utils.CountString(_instances.Length) + Environment.NewLine
							   + "Grand Total Size: " + Utils.LargeNumberString(grandTotal) + Environment.NewLine;

				return new ListingInfo(null, infoAry, colInfos, descr);


			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return new ListingInfo(error);
			}
		}

		public int GetArraySize(int instanceId, Tuple<int[], int[]> arySizes)
		{
			var ndx = Array.BinarySearch(arySizes.Item1, instanceId);
			if (ndx < 0) return 0;
			return arySizes.Item2[ndx];
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="error"></param>
		/// <returns>
		/// Tuple (total array count, list of 
		/// </returns>
		public Tuple<int, triple<string, int, pair<ulong, int>[]>[]> GetArrayCounts(bool skipFree, out string error)
		{
			error = null;
			try
			{
				ClrElementType[] elems = GetElementTypeList(out error);
				if (error != null) return null;
				Tuple<int[], int[]> arySizes = GetArraySizes(out error);
				if (error != null) return null;
				Debug.Assert(elems.Length == _instances.Length);
				SortedDictionary<int, List<pair<ulong, int>>> dct = new SortedDictionary<int, List<pair<ulong, int>>>();

				for (int i = 0, icnt = _instances.Length; i < icnt; ++i)
				{
					var elemType = elems[i];
					if (elemType != ClrElementType.SZArray && elemType != ClrElementType.Array) continue;
					var typeId = _instanceTypes[i];
					var addr = _instances[i];
					var typeName = _typeNames[typeId];
					if (skipFree && Utils.SameStrings(typeName, "Free")) continue;

					var aryCnt = GetArraySize(i, arySizes);
					List<pair<ulong, int>> lst;
					if (dct.TryGetValue(typeId, out lst))
					{
						lst.Add(new pair<ulong, int>(addr, aryCnt));
					}
					else
					{
						dct.Add(typeId, new List<pair<ulong, int>>(32) { new pair<ulong, int>(addr, aryCnt) });
					}
				}

				var result = new triple<string, int, pair<ulong, int>[]>[dct.Count];
				int ndx = 0;
				int totalAryCnt = 0;
				foreach (var kv in dct)
				{
					var typeName = _typeNames[kv.Key];
					var ary = kv.Value.ToArray();
					totalAryCnt += ary.Length;
					result[ndx++] = new triple<string, int, pair<ulong, int>[]>(typeName, kv.Key, ary);
				}
				return Tuple.Create(totalAryCnt, result);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		#endregion types

		#region instance references

		public KeyValuePair<string, int>[] GetParents(ulong address, out string error)
		{
			var parents = _instanceReferences.GetFieldParents(address, out error);
			if (error != null) return Utils.EmptyArray<KeyValuePair<string, int>>.Value;
			SortedDictionary<string, int> dct = new SortedDictionary<string, int>();
			for (int i = 0, icnt = parents.Length; i < icnt; ++i)
			{
				var typeName = _typeNames[_instanceTypes[parents[i]]];
				int cnt;
				if (dct.TryGetValue(typeName, out cnt))
				{
					dct[typeName] = cnt + 1;
				}
				else
				{
					dct.Add(typeName, 1);
				}
			}
			return dct.ToArray();
		}

		public KeyValuePair<string, ulong>[] GetParentDetails(ulong address, out string error)
		{
			var parents = _instanceReferences.GetFieldParents(address, out error);
			if (error != null) return Utils.EmptyArray<KeyValuePair<string, ulong>>.Value;
			var result = new List<KeyValuePair<string, ulong>>(32);
			for (int i = 0, icnt = parents.Length; i < icnt; ++i)
			{
				var typeName = _typeNames[_instanceTypes[parents[i]]];
				var paddr = _instances[parents[i]];
				result.Add(new KeyValuePair<string, ulong>(typeName, paddr));
			}
			return result.ToArray();
		}


		/// <summary>
		/// Get parents of an instance, all or up to given maxlevel.
		/// </summary>
		/// <param name="address">Instance address</param>
		/// <param name="error">Error message if any.</param>
		/// <param name="maxLevel">Output tree height limit. </param>
		/// <returns>Tuple of: (IndexNode tree, node count, back reference list).</returns>
		public Tuple<IndexNode, int, int[]> GetParentReferences(ulong address, out string error,
			int maxLevel = Int32.MaxValue)
		{
			error = null;
			try
			{
				var addrNdx = GetInstanceIndex(address);
				if (addrNdx < 0)
				{
					error = "No object found at: " + Utils.AddressString(address);
					return null;
				}
				var rootNode = new IndexNode(addrNdx, 0); // we at level 0
				var uniqueSet = new HashSet<int>();
				var que = new Queue<IndexNode>(256);
				var backReferences = new List<int>();
				que.Enqueue(rootNode);
				uniqueSet.Add(addrNdx);
				int nodeCount = 0; // do not count root node
				while (que.Count > 0)
				{
					var curNode = que.Dequeue();
					++nodeCount;
					if (curNode.Level >= maxLevel || nodeCount > MaxNodes) continue;
					var parents = _instanceReferences.GetFieldParents(curNode.Index, out error);
					if (parents.Length > 0)
					{
						var nodes = new IndexNode[parents.Length];
						curNode.AddNodes(nodes);
						for (int i = 0, icnt = parents.Length; i < icnt; ++i)
						{
							var parentNdx = parents[i];
							var cnode = new IndexNode(parentNdx, curNode.Level + 1);
							nodes[i] = cnode;
							if (!uniqueSet.Add(parentNdx))
							{
								backReferences.Add(parentNdx);
								++nodeCount;
								continue;
							}
							que.Enqueue(cnode);
						}
					}
				}
				return Tuple.Create(rootNode, nodeCount, backReferences.ToArray());
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public KeyValuePair<IndexNode, int>[] GetParentReferences(int[] instIndices, out string error,
			int maxLevel = Int32.MaxValue)
		{
			error = null;
			try
			{
				var uniqueSet = new HashSet<int>();
				var que = new Queue<IndexNode>(256);
				List<KeyValuePair<IndexNode, int>> lst = new List<KeyValuePair<IndexNode, int>>(instIndices.Length);
				for (int inst = 0, instCnt = instIndices.Length; inst < instCnt; ++inst)
				{
					var instNdx = instIndices[inst];
					var rootNode = new IndexNode(instNdx, 0); // we at level 0
					uniqueSet.Clear();
					que.Clear();
					que.Enqueue(rootNode);
					uniqueSet.Add(instNdx);
					int nodeCount = 0; // do not count root node
					while (que.Count > 0)
					{
						var curNode = que.Dequeue();
						++nodeCount;
						if (curNode.Level >= maxLevel || nodeCount > MaxNodes) continue;
						var parents = _instanceReferences.GetFieldParents(curNode.Index, out error);
						if (parents.Length > 0)
						{
							var nodes = new IndexNode[parents.Length];
							curNode.AddNodes(nodes);
							for (int i = 0, icnt = parents.Length; i < icnt; ++i)
							{
								var parentNdx = parents[i];
								var cnode = new IndexNode(parentNdx, curNode.Level + 1);
								nodes[i] = cnode;
								if (!uniqueSet.Add(parentNdx))
								{
									++nodeCount;
									continue;
								}
								que.Enqueue(cnode);
							}
						}
					}
					lst.Add(new KeyValuePair<IndexNode, int>(rootNode, nodeCount));
				}

				return lst.ToArray();
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public Tuple<string, AncestorNode> GetParentTree(ulong address, int levelMax)
		{
			string error = null;
			try
			{
				var instanceId = Utils.AddressSearch(_instances, address);
				if (instanceId < 0)
				{
					error = "Cannot find instance at address: " + Utils.AddressString(address);
					return new Tuple<string, AncestorNode>(error, null);
				}
				var typeId = _instanceTypes[instanceId];
				var typeName = _typeNames[typeId];
				AncestorNode rootNode = new AncestorNode(0, typeId, typeName, new int[] {instanceId});

				return GetParentTree(rootNode, levelMax);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return new Tuple<string, AncestorNode>(error, null);
			}
		}

		public Tuple<string, AncestorNode> GetParentTree(int typeId, int levelMax)
		{
			string error = null;
			try
			{
				string typeName = GetTypeName(typeId);
				var rootNode = new AncestorNode(0, typeId, typeName, GetTypeInstanceIndices(typeId));
				return GetParentTree(rootNode, levelMax);

				//HashSet<int> set = new HashSet<int>();
				//Queue<AncestorNode> que = new Queue<AncestorNode>(Math.Min(1000,rootNode.Instances.Length));
				//var dct = new SortedDictionary<int, KeyValuePair<string, List<int>>>();
				//que.Enqueue(rootNode);
				//while (que.Count > 0)
				//{
				//    AncestorNode node = que.Dequeue();
				//    dct.Clear();
				//    int currentNodeLevel = node.Level + 1;
				//    if (currentNodeLevel >= levelMax) continue;
				//    var instances = node.Instances;
				//    for (int i = 0, icnt = instances.Length; i < icnt; ++i)
				//    {
				//        var inst = instances[i];
				//        if (!set.Add(inst)) continue;
				//        var ancestors = _instanceReferences.GetFieldParents(inst, out error);
				//        for (int j = 0, jcnt = ancestors.Length; j < jcnt; ++j)
				//        {
				//            var ancestor = ancestors[j];
				//            var typeid = _instanceTypes[ancestor];
				//            var typename = _typeNames[typeid];
				//            KeyValuePair<string, List<int>> kv;
				//            if (dct.TryGetValue(typeid, out kv))
				//            {
				//                kv.Value.Add(ancestor);
				//                continue;
				//            }
				//            dct.Add(typeid, new KeyValuePair<string, List<int>>(typename, new List<int>(16) { ancestor }));
				//        }
				//    }
				//    var nodes = new AncestorNode[dct.Count];
				//    int n = 0;
				//    foreach (var kv in dct)
				//    {
				//        nodes[n] = new AncestorNode(currentNodeLevel,kv.Key,kv.Value.Key,kv.Value.Value.ToArray());
				//        que.Enqueue(nodes[n]);
				//        ++n;
				//    }
				//    node.AddNodes(nodes);
				//}
				//return new Tuple<string, AncestorNode>(null,rootNode);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return new Tuple<string, AncestorNode>(error, null);
			}
		}

		public Tuple<string, AncestorNode> GetParentTree(AncestorNode rootNode, int levelMax)
		{
			string error = null;
			try
			{
				HashSet<int> set = new HashSet<int>();
				Queue<AncestorNode> que = new Queue<AncestorNode>(Math.Min(1000, rootNode.Instances.Length));
				var dct = new SortedDictionary<int, KeyValuePair<string, List<int>>>();
				que.Enqueue(rootNode);
				while (que.Count > 0)
				{
					AncestorNode node = que.Dequeue();
					dct.Clear();
					int currentNodeLevel = node.Level + 1;
					if (currentNodeLevel >= levelMax) continue;
					var instances = node.Instances;
					for (int i = 0, icnt = instances.Length; i < icnt; ++i)
					{
						var inst = instances[i];
						if (!set.Add(inst)) continue;
						var ancestors = _instanceReferences.GetFieldParents(inst, out error);
						for (int j = 0, jcnt = ancestors.Length; j < jcnt; ++j)
						{
							var ancestor = ancestors[j];
							var typeid = _instanceTypes[ancestor];
							var typename = _typeNames[typeid];
							KeyValuePair<string, List<int>> kv;
							if (dct.TryGetValue(typeid, out kv))
							{
								kv.Value.Add(ancestor);
								continue;
							}
							dct.Add(typeid, new KeyValuePair<string, List<int>>(typename, new List<int>(16) {ancestor}));
						}
					}
					var nodes = new AncestorNode[dct.Count];
					int n = 0;
					foreach (var kv in dct)
					{
						nodes[n] = new AncestorNode(currentNodeLevel, kv.Key, kv.Value.Key, kv.Value.Value.ToArray());
						que.Enqueue(nodes[n]);
						++n;
					}
					node.AddNodes(nodes);
				}
				return new Tuple<string, AncestorNode>(null, rootNode);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return new Tuple<string, AncestorNode>(error, null);
			}
		}

		//public Tuple<DependencyNode, int> GetAddressesDescendants(int typeId, ulong[] addresses, int maxLevel, out string error)
		//{
		//    error = null;
		//    int nodeCount = 0;
		//    try
		//    {
		//        string typeName = GetTypeName(typeId);
		//        DependencyNode root = new DependencyNode(0, typeId, typeName, string.Empty, addresses);
		//        ++nodeCount;
		//        HashSet<ulong> addrSet = new HashSet<ulong>();
		//        Queue<DependencyNode> que = new Queue<DependencyNode>(1024);

		//        DependencyNode[] nodes = DependencyNode.BuildBranches(Types, Instances, InstanceTypeIds, StringIds, FieldDependencies, root, addresses, out error);
		//        for (int i = 0, icnt = nodes.Length; i < icnt; ++i)
		//        {
		//            que.Enqueue(nodes[i]);
		//            ++nodeCount;
		//        }

		//        while (que.Count > 0)
		//        {
		//            var node = que.Dequeue();
		//            if (node.Level == maxLevel) continue;

		//            nodes = DependencyNode.BuildBranches(Types, Instances, InstanceTypeIds, StringIds, FieldDependencies, node, node.Addresses, out error);
		//            for (int i = 0, icnt = nodes.Length; i < icnt; ++i)
		//            {
		//                que.Enqueue(nodes[i]);
		//                ++nodeCount;
		//            }
		//        }

		//        return Tuple.Create(root, nodeCount);
		//    }
		//    catch (Exception ex)
		//    {
		//        error = Utils.GetExceptionErrorString(ex);
		//        return null;
		//    }
		//}

		private void GetInstanceNodeExtraInfo(object data)
		{
			var args = data as Tuple<ulong[], ClrtRoots, BlockingCollection<InstanceNode>>;
			var instances = args.Item1;
			var roots = args.Item2;
			var que = args.Item3;

			while (true)
			{
				InstanceNode node = que.Take();
				if (node == null) break;
				var instNdx = Array.BinarySearch(instances, node.Address);
				if (instNdx >= 0) node.SetInstanceIndex(instNdx);
				ClrtRoot root;
				bool inFinalizerQueue;
				if (roots.GetRootInfo(node.Address, out root, out inFinalizerQueue))
				{
					node.SetRootInfo(new RootInfo(root, inFinalizerQueue));
				}
			}
		}

		public ListingInfo GetParentReferencesReport(ulong addr, int level = Int32.MaxValue)
		{
			string error;
			Tuple<IndexNode, int, int[]> result = GetParentReferences(addr, out error, level);
			if (!string.IsNullOrEmpty(error) && error[0] != Constants.InformationSymbol)
			{
				return new ListingInfo(error);
			}
			return OneInstanceParentsReport(result.Item1, result.Item2);
		}

		public ListingInfo GetParentReferencesReport(int typeId, int level = Int32.MaxValue)
		{
			string error;

			int[] typeInstances = GetTypeInstanceIndices(typeId);

			KeyValuePair<IndexNode, int>[] result = GetParentReferences(typeInstances, out error, level);
			if (!string.IsNullOrEmpty(error) && error[0] != Constants.InformationSymbol)
			{
				return new ListingInfo(error);
			}
			return MultiInstanceParentsReport(result);
		}

		public ListingInfo OneInstanceParentsReport(IndexNode rootNode, int nodeCnt)
		{
			const int ColumnCount = 4;
			string[] data = new string[nodeCnt*ColumnCount];
			listing<string>[] items = new listing<string>[nodeCnt];
			var que = new Queue<IndexNode>();
			que.Enqueue(rootNode);
			int dataNdx = 0;
			int itemNdx = 0;
			while (que.Count > 0)
			{
				var node = que.Dequeue();
				int instNdx = node.Index;
				ulong address = _instances[instNdx];
				string typeName = _typeNames[_instanceTypes[instNdx]];
				string rootInfo = Utils.IsRooted(address) ? string.Empty : "not rooted";

				items[itemNdx++] = new listing<string>(data, dataNdx, ColumnCount);
				data[dataNdx++] = node.Level.ToString();
				data[dataNdx++] = Utils.AddressString(address);
				data[dataNdx++] = rootInfo;
				data[dataNdx++] = typeName;
				for (int i = 0, icnt = node.Nodes.Length; i < icnt; ++i)
				{
					que.Enqueue(node.Nodes[i]);
				}
			}

			ColumnInfo[] colInfos = new[]
			{
				new ColumnInfo("Tree Level", ReportFile.ColumnType.Int32, 150, 1, true),
				new ColumnInfo("Address", ReportFile.ColumnType.String, 150, 2, true),
				new ColumnInfo("Root Info", ReportFile.ColumnType.String, 300, 3, true),
				new ColumnInfo("Type", ReportFile.ColumnType.String, 700, 4, true),
			};

			var sb = new StringBuilder(256);
			sb.Append(ReportFile.DescrPrefix).Append("Parents of ").Append(items[0].Forth).AppendLine();
			sb.Append(ReportFile.DescrPrefix).Append("Instance at address: ").Append(items[0].Second).AppendLine();
			sb.Append(ReportFile.DescrPrefix)
				.Append("Total reference count: ")
				.Append(Utils.LargeNumberString(nodeCnt))
				.AppendLine();
			sb.Append(ReportFile.DescrPrefix)
				.Append("NOTE. The queried instance is displayed in the row where Tree Level is '0'")
				.AppendLine();

			return new ListingInfo(null, items, colInfos, sb.ToString());
		}

		public ListingInfo MultiInstanceParentsReport(KeyValuePair<IndexNode, int>[] nodes)
		{
			const int ColumnCount = 4;
			int totalNodes = nodes.Sum(kv => kv.Value);
			string[] data = new string[totalNodes*ColumnCount];
			listing<string>[] items = new listing<string>[totalNodes];
			var que = new Queue<IndexNode>();
			int dataNdx = 0;
			int itemNdx = 0;
			for (int i = 0, icnt = nodes.Length; i < icnt; ++i)
			{
				var rootNode = nodes[i].Key;
				que.Enqueue(rootNode);
				ulong rootAddr = _instances[rootNode.Index];
				while (que.Count > 0)
				{
					var node = que.Dequeue();
					int instNdx = node.Index;
					ulong address = _instances[instNdx];
					string typeName = _typeNames[_instanceTypes[instNdx]];
					string rootInfo = Utils.IsRooted(address) ? string.Empty : "not rooted";

					items[itemNdx++] = new listing<string>(data, dataNdx, ColumnCount);
					data[dataNdx++] = Utils.AddressString(rootAddr);
					data[dataNdx++] = Utils.AddressString(address);
					data[dataNdx++] = rootInfo;
					data[dataNdx++] = typeName;
					for (int j = 0, jcnt = node.Nodes.Length; j < jcnt; ++j)
					{
						que.Enqueue(node.Nodes[j]);
					}
				}
			}




			ColumnInfo[] colInfos = new[]
			{
				new ColumnInfo("Type Instances", ReportFile.ColumnType.Int32, 150, 1, true),
				new ColumnInfo("Parents", ReportFile.ColumnType.String, 150, 2, true),
				new ColumnInfo("Root Info", ReportFile.ColumnType.String, 300, 3, true),
				new ColumnInfo("Type", ReportFile.ColumnType.String, 700, 4, true),
			};

			var sb = new StringBuilder(256);
			sb.Append(ReportFile.DescrPrefix).Append("Parents of ").Append(items[0].Forth).AppendLine();
			sb.Append(ReportFile.DescrPrefix).Append("Instance at address: ").Append(items[0].Second).AppendLine();
			sb.Append(ReportFile.DescrPrefix)
				.Append("Total reference count: ")
				.Append(Utils.LargeNumberString(items.Length))
				.AppendLine();

			return new ListingInfo(null, items, colInfos, sb.ToString());
		}

		#endregion instance references

		#region instance hierarchy 

		/// <summary>
		/// Get instance information for hierarchy walk.
		/// </summary>
		/// <param name="addr">Instance address.</param>
		/// <param name="fldNdx">Field index, this is used for struct types, in this case addr is of the parent.</param>
		/// <param name="error">Output error.</param>
		/// <returns>Instance information, and list of its parents.</returns>
		public InstanceValueAndAncestors GetInstanceInfo(ulong addr, int fldNdx, out string error)
		{
			try
			{
				// get ancestors
				//
				var ancestors = _instanceReferences.GetFieldParents(addr, out error);
				if (error != null && !Utils.IsInformation(error)) return null;
				AncestorDispRecord[] ancestorInfos = GroupAddressesByTypesForDisplay(ancestors);

				// get instance info: fields and values
				//
				var heap = GetFreshHeap();
				var result = DmpNdxQueries.FQry.getInstanceValue(_indexProxy, heap, addr, fldNdx);
				error = result.Item1;
				result.Item2?.SortByFieldName();
				return new InstanceValueAndAncestors(result.Item2, ancestorInfos);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public AncestorDispRecord[] GroupAddressesByTypesForDisplay(int[] parents)
		{
			var dct = new SortedDictionary<KeyValuePair<int,string>, List<ulong>>(new Utils.KvIntStr());
			for (int i = 0, icnt = parents.Length; i < icnt; ++i)
			{
				var addr = _instances[parents[i]];
				var typeId = _instanceTypes[parents[i]];
				var typeName = _typeNames[typeId];
				var key = new KeyValuePair<int, string>(typeId, typeName);
				List<ulong> lst;
				if (dct.TryGetValue(key, out lst))
				{
					lst.Add(addr);
				}
				else
				{
					dct.Add(key, new List<ulong>(8) { addr });
				}
			}
			var ary = new AncestorDispRecord[dct.Count];
			int ndx = 0;
			foreach (var kv in dct)
			{
				ary[ndx++] = new AncestorDispRecord(kv.Key.Key, kv.Key.Value, kv.Value.ToArray());
			}
			Array.Sort(ary, new AncestorDispRecordCmp());
			return ary;
		}

		#endregion instance hierarchy 

		#region Type Value Reports

		public ClrtDisplayableType GetTypeDisplayableRecord(int typeId, out string error)
		{
			error = null;
			try
			{
				ulong[] instances = GetTypeRealAddresses(typeId);
				if (instances == null || instances.Length < 1)
				{
					error = "Type instances not found.";
					return null;
				}
				return DmpNdxQueries.FQry.getDisplayableType(_indexProxy, Dump.Heap, instances[0]);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}

		}

		public ClrtDisplayableType GetTypeDisplayableRecord(ClrtDisplayableType dispType, ClrtDisplayableType dispTypeField, out string error)
		{
			error = null;
			try
			{
				ulong[] instances = GetTypeRealAddresses(dispTypeField.TypeId);
				if (instances != null && instances.Length > 0)
					return DmpNdxQueries.FQry.getDisplayableType(_indexProxy, Dump.Heap, instances[0]);
				instances = GetTypeRealAddresses(dispType.TypeId);
				if (instances == null || instances.Length < 1)
				{
					error = "Type instances not found.";
					return null;
				}

				var result = DmpNdxQueries.FQry.getDisplayableFieldType(_indexProxy, Dump.Heap, instances[0], dispTypeField.FieldIndex);
				if (result.Item1 != null)
				{
					error = Constants.InformationSymbolHeader + result.Item1;
					return null;
				}
				dispType.AddFields(result.Item2);
				return dispType;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}

		}
		#endregion Type Value Reports

		#endregion queries

		#region roots

		public ClrtRootInfo GetRoots(out string error)
		{
			error = null;
			if (_roots == null)
			{
				try
				{
					_roots = ClrtRootInfo.Load(_currentRuntimeIndex, _fileMoniker, out error);
					if (error != null) return null;
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					return null;
				}
			}
			return _roots;
		}

		public Tuple<string,DisplayableFinalizerQueue> GetDisplayableFinalizationQueue()
		{
			string error = null;
			try
			{
				var dispFinlQue = FinalizerQueueDisplayableItem.GetDisplayableFinalizerQueue(_roots.GetFinalizerItems(), _instances, _typeNames,
					_fileMoniker);
				return new Tuple<string, DisplayableFinalizerQueue>(error,dispFinlQue);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		#endregion roots

		#region weakreferences

		public ListingInfo GetWeakReferenceInfo(out string error)
		{
			Stopwatch stopWatch = new Stopwatch();
			error = null;
			try
			{
				stopWatch.Start();
				int totalWeakRefCount = 0;
				var ids = GetTypeIds("System.WeakReference", false);
				if (ids.Length < 1) return new ListingInfo(Constants.InformationSymbolHeader + "No System.WeakReference instances found.");
				var indices = new KeyValuePair<int,int[]>[ids.Length];
				var dct = new SortedDictionary<int, Dictionary<int,List<int>>>();
				var heap = Dump.GetFreshHeap();
				for (int i = 0, icnt = ids.Length; i < icnt; ++i)
				{
					var refs = GetTypeInstanceIndices(ids[i]);
					if (refs.Length < 1) continue;
					totalWeakRefCount += refs.Length;
					indices[i] = new KeyValuePair<int, int[]>(ids[i], refs);

					var firstAddr = Utils.RealAddress(_instances[refs[0]]);
					ClrType weakReferenceType = heap.GetObjectType(firstAddr); // System.WeakReference or System.WeakReference<T>
					ClrInstanceField m_handleField = weakReferenceType.Fields[0];
					ClrType m_handleType = m_handleField.Type; //  System.IntPtr
					ClrInstanceField m_valueField = m_handleType.Fields[0];

					for (int j = 0, jcnt = refs.Length; j < jcnt; ++j)
					{
						var wkRefInstNdx = refs[j];
						var addr = Utils.RealAddress(_instances[wkRefInstNdx]);
						var m_handleObj = m_handleField.GetValue(addr, false, false);
						var m_handleVal = m_handleObj == null ? 0L : (long) m_handleObj;
						var m_valueObj = m_handleVal == 0L ? null : m_valueField.GetValue(Convert.ToUInt64(m_handleVal), true, false);
						var m_valueVal = m_valueObj == null ? 0UL : (ulong)m_valueObj;
						var instNdx = GetInstanceIndex(m_valueVal);
						var instType = GetTypeId(instNdx);
						Dictionary<int, List<int>> lst;
						if (dct.TryGetValue(instType, out lst))
						{
							List<int> l;
							if (lst.TryGetValue(instNdx, out l))
							{
								l.Add(wkRefInstNdx);
							}
							else
							{
								lst.Add(instNdx,new List<int>() {wkRefInstNdx});
							}
						}
						else
						{
							lst = new Dictionary<int, List<int>>();
							lst.Add(instNdx,new List<int>() {wkRefInstNdx});
							dct.Add(instType,lst);
						}
					}
				}

				var durationGetDataFromHeap = Utils.StopAndGetDurationString(stopWatch);
				stopWatch.Restart();

				// format data and prepare report listing
				//
				var dataAry = new string[dct.Count * 3];
				var infoAry = new listing<string>[dct.Count];
				var addrData = new KeyValuePair<ulong,ulong[]>[dct.Count][];
				int ndx = 0;
				int off = 0;
				foreach (var kv in dct)
				{
					var typeName = GetTypeName(kv.Key);
					var objDct = kv.Value;
					var objCount = objDct.Count;
					int wkRefCount = 0;

					var instAry = new KeyValuePair<ulong, ulong[]>[objCount];
					int instNdx = 0;
					foreach (var instKv in objDct)
					{
						wkRefCount += instKv.Value.Count;
						ulong[] wkRefAry = new ulong[instKv.Value.Count];
						for (int i = 0, icnt = instKv.Value.Count; i < icnt; ++i)
						{
							wkRefAry[i] = GetInstanceAddress(instKv.Value[i]);
						}
						Utils.SortAddresses(wkRefAry);
						var instAddr = GetInstanceAddress(instKv.Key);
						instAry[instNdx++] = new KeyValuePair<ulong, ulong[]>(instAddr,wkRefAry);
					}

					var objCountStr = Utils.LargeNumberString(objCount);
					var wkRefCountStr = Utils.LargeNumberString(wkRefCount);

					infoAry[ndx] = new listing<string>(dataAry, off, 3);
					dataAry[off++] = objCountStr;
					dataAry[off++] = wkRefCountStr;
					dataAry[off++] = typeName;
					addrData[ndx] = instAry;
					++ndx;
				}

				ColumnInfo[] colInfos = new[]
					{
						new ColumnInfo("Object Count", ReportFile.ColumnType.Int32,150,1,true),
						new ColumnInfo("WeakReference Count", ReportFile.ColumnType.Int32,150,2,true),
						new ColumnInfo("Object Type", ReportFile.ColumnType.String,400,3,true),
					};

				StringBuilder sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
				sb.AppendLine("WeakReference type: " + string.Empty)
					.AppendLine("WeakReference Count: " + Utils.LargeNumberString(totalWeakRefCount))
					.AppendLine("Pointed instances Count: " + Utils.LargeNumberString(0));
				string descr = StringBuilderCache.GetStringAndRelease(sb);

				var durationFomattingData = Utils.StopAndGetDurationString(stopWatch);

				return new ListingInfo(null, infoAry, colInfos, descr, addrData);

			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public KeyValuePair<int, ulong[]>[] GetTypeWithPrefixAddresses(string prefix, bool includeArrays, out int totalCount)
		{
			int[] ids = GetTypeIds(prefix, includeArrays);
			return GetTypeAddresses(ids, out totalCount);
		}

		//public KeyValuePair<int, int[]>[] GetTypeWithPrefixIndices(string prefix, bool includeArrays, out int totalCount)
		//{
		//	int[] ids = GetTypeIds(prefix, includeArrays);
		//	return GetTypeAddresses(ids, out totalCount);
		//}

		#endregion weakreferences

		#region strings

		public string GetString(int id)
		{
			if (id < 0 || id >= _stringIds.Length) return Constants.Unknown;
			return _stringIds[id];
		}

		public bool AreStringDataFilesAvailable()
		{
			var strDatPath = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapDumpStringsInfoPostfix);
			return File.Exists(strDatPath);
		}

		public static ulong[] GetStringAddresses(ClrHeap heap, string str, ulong[] addresses, out string error)
		{
			error = null;
			try
			{
				List<ulong> lst = new List<ulong>(10 * 1024);
				for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
				{
					var heapStr = ValueExtractor.GetStringAtAddress(addresses[i], heap);
					if (Utils.SameStrings(str, heapStr))
						lst.Add(addresses[i]);
				}
				return lst.ToArray();
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public ListingInfo CompareStringsWithOther(string otherDumpPath)
		{
			string error;

			const int ColumnCount = 7;

			StringStats myStrStats = GetStringStats(out error);
			if (error != null) return new ListingInfo(error);

			long otherTotalSize, otherTotalUniqueSize;
			int otherTotalCount;
			SortedDictionary<string, KeyValuePair<int, uint>> otherInfo = GetStringsSizesInfo(otherDumpPath, out otherTotalSize, out otherTotalUniqueSize, out otherTotalCount, out error);
			if (error != null) return new ListingInfo(error);

			string[] otStrings = new string[otherInfo.Count];
			int[] otCounts = new int[otherInfo.Count];
			uint[] otSizes = new uint[otherInfo.Count];

			int ndx = 0;
			foreach (var kv in otherInfo)
			{
				otStrings[ndx] = kv.Key;
				otCounts[ndx] = kv.Value.Key;
				otSizes[ndx] = kv.Value.Value;
				++ndx;
			}
			otherInfo.Clear();
			otherInfo = null;

			var maxCnt = Math.Max(otStrings.Length, myStrStats.Count);
			int myNdx = 0;
			int myCnt = myStrStats.Count;
			int otherNdx = 0;
			int otherCnt = otStrings.Length;

			var myStrings = myStrStats.Strings;
			var myCounts = myStrStats.Counts;
			var mySizes = myStrStats.Sizes;


			List<string> data = new List<string>(100000);
			while (true)
			{
				if (myNdx < myCnt && otherNdx < otherCnt)
				{
					var cmp = string.Compare(myStrings[myNdx], otStrings[otherNdx], StringComparison.Ordinal);
					if (cmp == 0)
					{
						var myCount = myCounts[myNdx];
						var otCount = otCounts[otherNdx];
						var mySize = mySizes[myNdx];
						GetStringsDiffLine(myStrings[myNdx], myCount, otCount, mySize, data);
						++myNdx;
						++otherNdx;
					}
					else if (cmp < 0)
					{
						var myCount = myCounts[myNdx];
						var mySize = mySizes[myNdx];
						GetStringsDiffLine(myStrings[myNdx], myCount, 0, mySize, data);
						++myNdx;
					}
					else
					{
						Debug.Assert(cmp > 0);
						var otCount = otCounts[otherNdx];
						var otSize = otSizes[otherNdx];
						GetStringsDiffLine(otStrings[otherNdx], 0, otCount, otSize, data);
						++otherNdx;
					}
				}
				else if (myNdx < myCnt)
				{
					var myCount = myCounts[myNdx];
					var mySize = mySizes[myNdx];
					GetStringsDiffLine(myStrings[myNdx], myCount, 0, mySize, data);
					++myNdx;
				}
				else if (otherNdx < otherCnt)
				{
					var otCount = otCounts[otherNdx];
					var otSize = otSizes[otherNdx];
					GetStringsDiffLine(otStrings[otherNdx], 0, otCount, otSize, data);
					++otherNdx;
				}
				else
				{
					break;
				}
			}

			string[] dataAry = data.ToArray();
			listing<string>[] listing = new listing<string>[dataAry.Length / ColumnCount];
			Debug.Assert((dataAry.Length % ColumnCount) == 0);

			data.Clear();
			data = null;
			int dataNdx = 0;
			for (int i = 0, icnt = listing.Length; i < icnt; ++i)
			{
				listing[i] = new listing<string>(dataAry, dataNdx, 6);
				dataNdx += ColumnCount;
			}

			ColumnInfo[] colInfos = new[]
			{
				new ColumnInfo(Constants.BlackDiamond + " Count", ReportFile.ColumnType.Int32,150,1,true),
				new ColumnInfo(Constants.AdhocQuerySymbol + " Count", ReportFile.ColumnType.Int32,150,2,true),
				new ColumnInfo(Constants.BlackDiamond + " Count Diff", ReportFile.ColumnType.Int32,150,3,true),
				new ColumnInfo(Constants.BlackDiamond + " Total Size", ReportFile.ColumnType.UInt64,150,4,true),
				new ColumnInfo(Constants.AdhocQuerySymbol + " Total Size", ReportFile.ColumnType.UInt64,150,5,true),
				new ColumnInfo("Size", ReportFile.ColumnType.UInt32,150,6,true),
				new ColumnInfo("Type", ReportFile.ColumnType.String,500,7,true),
			};

			//Array.Sort(dataListing, ReportFile.GetComparer(colInfos[6]));

			var otherDmpName = Path.GetFileName(otherDumpPath);
			StringBuilder sb = new StringBuilder(512);
			sb.Append(Constants.BlackDiamond).Append(" Index Dump: ").Append(_fileMoniker.DumpFileName).AppendLine();
			sb.Append(Constants.AdhocQuerySymbol).Append(" Adhoc Dump: ").Append(otherDmpName).AppendLine();

			sb.Append(Constants.BlackDiamond).Append(" Total String Count: ").Append(Utils.LargeNumberString(myStrStats.TotalCount))
				.Append(", Unique String Count: ").Append(Utils.LargeNumberString(myStrings.Length)).AppendLine();
			sb.Append(Constants.AdhocQuerySymbol).Append(" Total String Count: ").Append(Utils.LargeNumberString(otherTotalCount))
				.Append(", Unique String Count: ").Append(otherCnt).AppendLine();

			sb.Append(Constants.BlackDiamond).Append(" Total Size: ").Append(Utils.LargeNumberString(myStrStats.TotalSize))
				.Append(" Unique Total Size: ").Append(Utils.LargeNumberString(myStrStats.TotalUniqueSize)).AppendLine();
			sb.Append(Constants.AdhocQuerySymbol).Append(" Total Size: ").Append(Utils.LargeNumberString(otherTotalSize))
				.Append(" Unique Total Size: ").Append(Utils.LargeNumberString(otherTotalUniqueSize)).AppendLine();

			return new ListingInfo(null, listing, colInfos, sb.ToString());
		}

		private void GetStringsDiffLine(string str, int myCount, int otCount, uint size, List<string> data)
		{

			var myCountStr = Utils.LargeNumberString(myCount);
			var otCntStr = Utils.LargeNumberString(otCount);
			var cntDiffStr = Utils.LargeNumberString(myCount - otCount);
			var myTotSizeStr = Utils.LargeNumberString((ulong)myCount * (ulong)size);
			var otTotSizeStr = Utils.LargeNumberString((ulong)otCount * (ulong)size);
			var mySizeStr = Utils.LargeNumberString(size);
			data.Add(myCountStr);
			data.Add(otCntStr);
			data.Add(cntDiffStr);
			data.Add(myTotSizeStr);
			data.Add(otTotSizeStr);
			data.Add(mySizeStr);
			data.Add(str);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="error"></param>
		public ListingInfo GetStringStats(int minReferenceCount, bool includeGenerations = false)
		{
			string error = null;
			try
			{
				if (AreStringDataFilesAvailable())
				{
					StringStats strStats = null;
					if (_stringStats == null || !_stringStats.TryGetTarget(out strStats))
					{
						strStats = StringStats.GetStringsInfoFromFiles(_currentRuntimeIndex, DumpPath, out error);
						if (strStats == null)
							return new ListingInfo(error);
						if (_stringStats == null)
							_stringStats = new WeakReference<StringStats>(strStats);
						else
							_stringStats.SetTarget(strStats);
					}
					ListingInfo data = null;
					if (includeGenerations)
						data = strStats.GetGridData(minReferenceCount, _segments, out error);
					else
						data = strStats.GetGridData(minReferenceCount, out error);

					return data ?? new ListingInfo(error);
				}

				var strTypeId = GetTypeId("System.String");
				Debug.Assert(strTypeId != Constants.InvalidIndex);
				ulong[] addresses = GetTypeRealAddresses(strTypeId);
				var runtime = Dump.Runtime;
				runtime.Flush();
				var heap = runtime.GetHeap();


				var stats = ClrtDump.GetStringStats(heap, addresses, DumpPath, out error);
				if (stats == null) return new ListingInfo(error);
				ListingInfo lstData = null;
				if (includeGenerations)
					lstData = stats.GetGridData(minReferenceCount, _segments, out error);
				else
					lstData = stats.GetGridData(minReferenceCount, out error);

				return lstData ?? new ListingInfo(error);
			}
			catch (Exception ex)
			{
				return new ListingInfo(Utils.GetExceptionErrorString(ex));
			}
		}

		public StringStats GetStringStats(out string error)
		{
			error = null;
			try
			{
				if (AreStringDataFilesAvailable())
				{
					StringStats strStats = null;
					if (_stringStats == null || !_stringStats.TryGetTarget(out strStats))
					{
						strStats = StringStats.GetStringsInfoFromFiles(_currentRuntimeIndex, DumpPath, out error);
						if (strStats == null)
							return null;
						if (_stringStats == null)
							_stringStats = new WeakReference<StringStats>(strStats);
						else
							_stringStats.SetTarget(strStats);
					}
					return strStats;
				}

				var strTypeId = GetTypeId("System.String");
				Debug.Assert(strTypeId != Constants.InvalidIndex);
				ulong[] addresses = GetTypeInstances(strTypeId);
				var runtime = Dump.Runtime;
				runtime.Flush();
				var heap = runtime.GetHeap();


				var stats = ClrtDump.GetStringStats(heap, addresses, DumpPath, out error);
				if (stats == null) return null;

				if (_stringStats == null)
					_stringStats = new WeakReference<StringStats>(stats);
				else
					_stringStats.SetTarget(stats);

				return stats;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}
		
		public StringStats GetCurrentStringStats(out string error)
		{
			error = null;
			try
			{
				StringStats strStats = null;
				if (_stringStats == null || !_stringStats.TryGetTarget(out strStats))
				{
					strStats = StringStats.GetStringsInfoFromFiles(_currentRuntimeIndex, DumpPath, out error);
					if (strStats == null) return null;
					if (_stringStats == null)
						_stringStats = new WeakReference<StringStats>(strStats);
					else
						_stringStats.SetTarget(strStats);
				}
				return strStats;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public long GetSizeOfStringsWithPrefix(string str, out string error)
		{
			error = null;
			var strStats = GetCurrentStringStats(out error);
			if (error != null) return -1L;
			return strStats.GetSizeOfStringsWithPrefix(str);
		}

		public ListingInfo GetTypesWithSpecificStringFieldListing(string strContent)
		{
			string error;
			var result = GetTypesWithSpecificStringField(strContent, out error);
			if (error != null)
				return new ListingInfo(error);
			var dct = result.Item1;
			if (dct.Count < 1)
			{
				return new ListingInfo();
			}
			var itemCount = result.Item2;
			ColumnInfo[] columns = new ColumnInfo[]
			{
				new ColumnInfo("String Addr", ReportFile.ColumnType.UInt64, 200,1,true),
				new ColumnInfo("Parent Addr", ReportFile.ColumnType.UInt64, 200,2,true),
				new ColumnInfo("Parent Count", ReportFile.ColumnType.Int32, 200,3,true),
				new ColumnInfo("Field", ReportFile.ColumnType.String, 200,4,true),
				new ColumnInfo("Type", ReportFile.ColumnType.String, 500,5,true)
			};
			listing<string>[] listAry = new listing<string>[itemCount];
			string[] dataAry = new string[itemCount * columns.Length];
			int lisAryNdx = 0;
			int dataAryNdx = 0;
			foreach (var kv in dct)
			{
				var parts = kv.Key.Split(new string[] { Constants.FieldSymbolPadded }, StringSplitOptions.None);
				Debug.Assert(parts.Length == 2);
				var addresses = kv.Value;
				for (int i = 0, icnt = addresses.Count; i < icnt; ++i)
				{
					listAry[lisAryNdx++] = new listing<string>(dataAry, dataAryNdx, columns.Length);
					dataAry[dataAryNdx++] = Utils.AddressString(addresses[i].Key);
					dataAry[dataAryNdx++] = Utils.AddressString(addresses[i].Value);
					dataAry[dataAryNdx++] = Utils.LargeNumberString(icnt);
					dataAry[dataAryNdx++] = parts[1];
					dataAry[dataAryNdx++] = parts[0];
				}
			}
			StringBuilder sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
			sb.Append(ReportFile.DescrPrefix).Append("Type instances containing with string field: ").Append(strContent).AppendLine();
			sb.Append(ReportFile.DescrPrefix).Append("Total reference count: ").Append(itemCount).AppendLine();
			return new ListingInfo(null, listAry, columns, StringBuilderCache.GetStringAndRelease(sb), dct);
		}

		public Tuple<SortedDictionary<string, List<KeyValuePair<ulong, ulong>>>, int> GetTypesWithSpecificStringField(string strContent, out string error)
		{
			error = null;
			try
			{
				var strStats = GetCurrentStringStats(out error);
				var addresses = strStats.GetStringAddresses(strContent, out error);
				if (error != null)
				{
					error = Utils.GetErrorString("Types with Specific String Field", "StringStats.GetStringAddresses failed.",
						error);
					return null;
				}

				List<string> errors = new List<string>();
				KeyValuePair<int, int[]>[] parentInfos = _instanceReferences.GetMultiFieldParents(addresses, errors);


				var dct = new SortedDictionary<string, List<KeyValuePair<ulong, ulong>>>(StringComparer.Ordinal);
				int addrCount = 0;
				for (int i = 0, icnt = parentInfos.Length; i < icnt; ++i)
				{
					var childAddr = _instances[parentInfos[i].Key];
					var parentIds = parentInfos[i].Value;
					for (int j = 0, jcnt = parentIds.Length; j < jcnt; ++j)
					{
						var parentId = parentIds[j];
						var parentAddr = _instances[parentId];
						var typeName = _typeNames[_instanceTypes[parentId]];
						List<KeyValuePair<ulong, ulong>> lst;
						if (dct.TryGetValue(typeName, out lst))
						{
							lst.Add(new KeyValuePair<ulong, ulong>(childAddr, parentAddr));
						}
						else
						{
							dct.Add(typeName, new List<KeyValuePair<ulong, ulong>>() { new KeyValuePair<ulong, ulong>(childAddr, parentAddr) });
						}

						++addrCount;
					}

				}
				return new Tuple<SortedDictionary<string, List<KeyValuePair<ulong, ulong>>>, int>(dct, addrCount);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public SortedDictionary<string, KeyValuePair<int, uint>> GetStringsSizesInfo(string path, out long totalSize, out long totalUniqueSize, out int totalCount, out string error)
		{
			totalSize = totalUniqueSize = 0;
			totalCount = 0;
			try
			{
				var dump = ClrtDump.OpenDump(path, out error);
				if (dump == null) return null;
				return ClrtDump.GetStringCountsAndSizes(dump.Runtime.GetHeap(), out totalSize, out totalUniqueSize, out totalCount, out error);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				Dump?.Dispose();
			}
		}

		//public static ulong[] GetStringAddresses(ClrHeap heap, string str, ulong[] addresses, out string error)
		//{
		//	error = null;
		//	try
		//	{
		//		List<ulong> lst = new List<ulong>(10 * 1024);
		//		for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
		//		{
		//			var heapStr = ValueExtractor.GetStringAtAddress(addresses[i], heap);
		//			if (Utils.SameStrings(str, heapStr))
		//				lst.Add(addresses[i]);
		//		}
		//		return lst.ToArray();
		//	}
		//	catch (Exception ex)
		//	{
		//		error = Utils.GetExceptionErrorString(ex);
		//		return null;
		//	}
		//}

		#endregion strings

		#region segments/generations/sizes

		public KeyValuePair<uint[], uint[]> GetSizeArrays(out string error)
		{
			var sizes = GetSizeArray(false, out error);
			if (error != null) return new KeyValuePair<uint[], uint[]>(null, null);
			var baseSizes = GetSizeArray(true, out error);
			if (error != null) return new KeyValuePair<uint[], uint[]>(null, null);
			return new KeyValuePair<uint[], uint[]>(baseSizes, sizes);
		}

		public KeyValuePair<uint, uint> GetInstanceSizes(ulong address, out string error)
		{
			error = null;
			var instNdx = Utils.AddressSearch(_instances, address);
			if (instNdx < 0)
			{
				error = "Cannot find an instance at address: " + Utils.RealAddressString(address);
				return new KeyValuePair<uint, uint>(0,0);
			}
			var sizes = GetSizeArray(false, out error);
			if (error != null) return new KeyValuePair<uint, uint>(0, 0);
			var totalSize = sizes[instNdx];
			var baseSizes = GetSizeArray(true, out error);
			if (error != null) return new KeyValuePair<uint, uint>(0, 0);
			var baseSize = baseSizes[instNdx];
			return new KeyValuePair<uint, uint>(baseSize,totalSize);

		}

		public Tuple<int[], ulong[], int[], ulong[],int[],ulong[]> GetTotalGenerationDistributions(out string error)
		{
			var sizes = GetSizeArray(false, out error);
			return ClrtSegment.GetTotalGenerationDistributions(_currentRuntimeIndex,_segments,_instances,sizes,_fileMoniker,_segmentInfoUnrooted);
		}


		//public Tuple<string, long>[][] GetGenerationTotals()
		//{
		//	Tuple<int[], ulong[], int[], ulong[]> histograms =
		//		ClrtSegment.GetTotalGenerationDistributions(_segments);

		//	Tuple<string, long>[] ary0 = new Tuple<string, long>[4];
		//	ary0[0] = new Tuple<string, long>("G0", histograms.Item1[0]);
		//	ary0[1] = new Tuple<string, long>("G1", histograms.Item1[1]);
		//	ary0[2] = new Tuple<string, long>("G2", histograms.Item1[2]);
		//	ary0[3] = new Tuple<string, long>("LOH", histograms.Item1[3]);

		//	Tuple<string, long>[] ary1 = new Tuple<string, long>[4];
		//	ary1[0] = new Tuple<string, long>("G0", (long)histograms.Item2[0]);
		//	ary1[1] = new Tuple<string, long>("G1", (long)histograms.Item2[1]);
		//	ary1[2] = new Tuple<string, long>("G2", (long)histograms.Item2[2]);
		//	ary1[3] = new Tuple<string, long>("LOH", (long)histograms.Item2[3]);

		//	Tuple<string, long>[] ary2 = new Tuple<string, long>[4];
		//	ary2[0] = new Tuple<string, long>("G0", histograms.Item3[0]);
		//	ary2[1] = new Tuple<string, long>("G1", histograms.Item3[1]);
		//	ary2[2] = new Tuple<string, long>("G2", histograms.Item3[2]);
		//	ary2[3] = new Tuple<string, long>("LOH", histograms.Item3[3]);

		//	Tuple<string, long>[] ary3 = new Tuple<string, long>[4];
		//	ary3[0] = new Tuple<string, long>("G0", (long)histograms.Item4[0]);
		//	ary3[1] = new Tuple<string, long>("G1", (long)histograms.Item4[1]);
		//	ary3[2] = new Tuple<string, long>("G2", (long)histograms.Item4[2]);
		//	ary3[3] = new Tuple<string, long>("LOH", (long)histograms.Item4[3]);

		//	return new Tuple<string, long>[][]
		//	{
		//		ary0,
		//		ary1,
		//		ary2,
		//		ary3,
		//	};
		//}

		public int[] GetGenerationHistogram(ulong[] addresses)
		{
			return ClrtSegment.GetGenerationHistogram(_segments, addresses);
		}

		public int[] GetStringGcGenerationHistogram(string strContent, out string error)
		{
			error = null;
			var strStats = GetCurrentStringStats(out error);
			var addresses = strStats.GetStringAddresses(strContent, out error);
			if (error != null) return null;
			return ClrtSegment.GetGenerationHistogram(_segments, addresses);
		}

		public int[] GetTypeGcGenerationHistogram(string typeName, out string error)
		{
			error = null;
			var typeId = Array.BinarySearch(_typeNames,typeName,StringComparer.Ordinal);
			if (typeId < 0)
			{
				error = "cannot find type: " + typeName;
				return null;
			}
			return GetTypeGcGenerationHistogram(typeId);
		}

		public int[] GetTypeGcGenerationHistogram(int typeId)
		{
			ulong[] addresses = GetTypeInstances(typeId);
			return ClrtSegment.GetGenerationHistogram(_segments, addresses);
		}

		public static bool GetLohInfo(ClrRuntime runtime, out string error)
		{
			error = null;
			try
			{
				runtime.Flush();
				var heap = runtime.GetHeap();

				var segs = heap.Segments;
				var segMemFragments = new List<triple<bool, ulong, ulong>[]>();
				var stringsLsts = new List<string[]>(segs.Count);
				var typeInfoLst = new List<SortedDictionary<string, List<KeyValuePair<ulong, ulong>>>>(segs.Count);
				var countAndSize = new List<KeyValuePair<ulong, int>>(segs.Count);

				for (int i = 0, icnt = segs.Count; i < icnt; ++i)
				{
					var seg = segs[i];
					if (!seg.IsLarge) continue;

					int cnt = 0;
					var dct = new SortedDictionary<string, List<KeyValuePair<ulong, ulong>>>(StringComparer.Ordinal);
					var strings = new List<string>(128);
					var fragments = new List<triple<bool, ulong, ulong>>(1024);
					fragments.Add(new triple<bool, ulong, ulong>(true, seg.Start, 0));

					ulong addr = seg.FirstObject;
					while (addr != 0ul)
					{
						var clrType = heap.GetObjectType(addr);
						if (clrType == null) goto NEXT_OBJECT;
						var sz = clrType.GetSize(addr);
						if (!clrType.IsFree)
						{
							++cnt;
							if (clrType.IsString)
							{
								strings.Add(ClrMDRIndex.ValueExtractor.GetStringAtAddress(addr, heap));
							}
							List<KeyValuePair<ulong, ulong>> lst;
							if (dct.TryGetValue(clrType.Name, out lst))
							{
								lst.Add(new KeyValuePair<ulong, ulong>(addr, sz));
							}
							else
							{
								dct.Add(clrType.Name, new List<KeyValuePair<ulong, ulong>>(128) { new KeyValuePair<ulong, ulong>(addr, sz) });
							}
						}
						ClrMDRIndex.ValueExtractor.SetSegmentInterval(fragments, addr, sz, clrType.IsFree);
						NEXT_OBJECT:
						addr = seg.NextObject(addr);
					}
					var lastFragment = fragments[fragments.Count - 1];
					var lastAddr = lastFragment.Second + lastFragment.Third;
					if (seg.End > lastAddr)
						ClrMDRIndex.ValueExtractor.SetSegmentInterval(fragments, lastAddr, seg.End - lastAddr, true);

					// collect segment info
					//
					countAndSize.Add(new KeyValuePair<ulong, int>(seg.End + 1ul - seg.Start, cnt));
					typeInfoLst.Add(dct);
					stringsLsts.Add(strings.ToArray());
					segMemFragments.Add(fragments.ToArray());
				}

				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}

		private uint[] GetSizeArray(bool baseSize, out string error)
		{
			error = null;
			try
			{
				uint[] sizes = null;
				if (baseSize)
				{
					if (_baseSizes == null || !_baseSizes.TryGetTarget(out sizes))
					{
						sizes = Utils.ReadUintArray(_fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapInstanceBaseSizesFilePostfix),
							out error);
						if (sizes == null) return null;
						if (_baseSizes == null)
							_baseSizes = new WeakReference<uint[]>(sizes);
						else
							_baseSizes.SetTarget(sizes);
					}
				}
				else
				{
					if (_sizes == null || !_sizes.TryGetTarget(out sizes))
					{
						sizes = Utils.ReadUintArray(_fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapInstanceSizesFilePostfix),
							out error);
						if (sizes == null) return null;
						if (_sizes == null)
							_sizes = new WeakReference<uint[]>(sizes);
						else
							_sizes.SetTarget(sizes);
					}
				}
				return sizes;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public ClrElementType[] GetElementTypeList(out string error)
		{
			error = null;
			try
			{
				ClrElementType[] elems = null;
				if (_elementTypes == null || !_elementTypes.TryGetTarget(out elems))
					{
						elems = Utils.ReadClrElementTypeArray(_fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapInstanceElemTypesFilePostfix),
							out error);
						if (elems == null) return null;
						if (_elementTypes == null)
							_elementTypes = new WeakReference<ClrElementType[]>(elems);
						else
							_elementTypes.SetTarget(elems);
					}
				return elems;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		private Tuple<int[],int[]> GetArraySizes(out string error)
		{
			error = null;
			try
			{
				Tuple <int[],int[]> arySizes = null;
				if (_arraySizes == null || !_arraySizes.TryGetTarget(out arySizes))
				{
					arySizes = Utils.ReadKvIntIntArrayAsTwoArrays(_fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapInstanceBaseSizesFilePostfix),
						out error);
					if (arySizes == null) return null;
					if (_arraySizes == null)
						_arraySizes = new WeakReference<Tuple<int[], int[]>>(arySizes);
					else
						_arraySizes.SetTarget(arySizes);
				}
				return arySizes;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}
		public KeyValuePair<ulong, ulong> GetTypeSizes(int typeId, out string error)
		{
			error = null;
			try
			{
				uint[] sizes = GetSizeArray(false, out error);
				uint[] baseSz = GetSizeArray(true, out error);
				int[] instIndices = GetTypeInstanceIndices(typeId);
				ulong totalSize = 0UL;
				ulong totalBaseSize = 0UL;
				for (int i = 0, icnt = instIndices.Length; i < icnt; ++i)
				{
					var ndx = instIndices[i];
					var sz = sizes[ndx];
					totalSize += sz;
					var bsz = baseSz[ndx];
					totalBaseSize += bsz;
				}
				return new KeyValuePair<ulong, ulong>(totalBaseSize,totalSize);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return new KeyValuePair<ulong, ulong>(0UL,0UL);
			}
		}

		public Tuple<ulong, KeyValuePair<uint, ulong>[]> GetTypeBaseSizes(int typeId, out string error)
		{
			error = null;
			try
			{
				uint[] sizes = null;
				if (_baseSizes == null || !_baseSizes.TryGetTarget(out sizes))
				{
					sizes = Utils.ReadUintArray(_fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapInstanceBaseSizesFilePostfix),
						out error);
					if (sizes == null) return null;
					if (_baseSizes == null)
						_baseSizes = new WeakReference<uint[]>(sizes);
					else
						_baseSizes.SetTarget(sizes);
				}

				int[] instIndices = GetTypeInstanceIndices(typeId);
				ulong totalSize = 0UL;
				var sizeInfos = new KeyValuePair<uint, ulong>[instIndices.Length];
				for (int i = 0, icnt = instIndices.Length; i < icnt; ++i)
				{
					var ndx = instIndices[i];
					var sz = sizes[ndx];
					totalSize += sz;
					sizeInfos[i] = new KeyValuePair<uint, ulong>(sz, _instances[ndx]);
				}
				return new Tuple<ulong, KeyValuePair<uint, ulong>[]>(totalSize, sizeInfos);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public SortedDictionary<string, quadruple<int, ulong, ulong, ulong>> GetTypeSizesInfo(out string error)
		{
			error = null;
			try
			{
				ulong grandTotal = 0ul;
				int instanceCount = 0;
				var typeDct = new SortedDictionary<string, quadruple<int, ulong, ulong, ulong>>(StringComparer.Ordinal);
				uint[] sizes = GetSizeArray(false, out error);

				for (int i = 0, icnt = sizes.Length; i < icnt; ++i)
				{
					++instanceCount;
					var sz = sizes[i];
					grandTotal += sz;
					var typeName = _typeNames[_instanceTypes[i]];
					quadruple<int, ulong, ulong, ulong> info;
					if (typeDct.TryGetValue(typeName, out info))
					{
						var maxSz = info.Third < sz ? sz : info.Third;
						var minSz = info.Third > sz ? sz : info.Third;
						typeDct[typeName] = new quadruple<int, ulong, ulong, ulong>(
							info.First + 1,
							info.Second + sz,
							maxSz,
							minSz
						);
					}
					else
					{
						typeDct.Add(typeName, new quadruple<int, ulong, ulong, ulong>(1, sz, sz, sz));
					}
				}
				return typeDct;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		/// <summary>
		/// TODO JRD -- redo this
		/// </summary>
		/// <param name="typeId"></param>
		/// <param name="error"></param>
		/// <returns></returns>
		public Tuple<ulong, ulong[], SortedDictionary<string, KeyValuePair<int, ulong>>, SortedDictionary<string, List<int>>, triple<int, ulong, string>[]> GetTypeSizeDetails(int typeId, out string error)
		{
			ulong[] addresses = GetTypeRealAddresses(typeId);
			return ClrtDump.GetTotalSizeDetail(Dump, addresses, out error);
		}

		public ListingInfo GetTypeFieldDefaultValues(int typeId)
		{
			ulong[] addresses = GetTypeRealAddresses(typeId);
			var heap = Dump.Heap;
			ClrType clrType = null;
			int fldCount = 0;
			KeyValuePair<string, string>[] fieldDefValues=null;
			int[] counts = null;
			ClrType[] fldTypes = null;
			TypeKind[] fldKinds = null;
			ClrInstanceField[] fields=null;
			string[] fieldTypeNames = null;
			int ndx = 0;
			bool intrnl = false;
			bool isArray = false;
			int totalDefValues = 0;
			while (clrType == null) // get type from heap
			{
				clrType = heap.GetObjectType(addresses[ndx]);
				if (clrType != null)
				{
					intrnl = clrType.IsValueClass;
					isArray = clrType.IsArray;
					fldCount = isArray ? 1 : clrType.Fields.Count;

					fieldDefValues = new KeyValuePair<string, string>[fldCount];
					counts = new int[fldCount];
					fldTypes = new ClrType[fldCount];
					fields = new ClrInstanceField[fldCount];
					fldKinds = new TypeKind[fldCount];
					fieldTypeNames = new string[fldCount];

					if (isArray)
					{
						
					}
					for (int f = 0; f < fldCount; ++f)
					{
						var fldType = clrType.Fields[f].Type;
						fieldTypeNames[f] = fldType.Name;
						fldTypes[f] = fldType;
						var defValue = Types.typeDefaultValue(fldType);
						fieldDefValues[f] = new KeyValuePair<string, string>(clrType.Fields[f].Name,defValue);
						fields[f] = clrType.Fields[f];
						fldKinds[f] = Types.typeKind(fldType);
					}
					break;
				}
			}

			var minDt = new DateTime(1800,1,1);

			for (int i = ndx, icnt = addresses.Length; i < icnt; ++i)
			{
				var addr = addresses[i];
				ulong fldAddr;
				for (int j = 0; j < fldCount; ++j)
				{
					switch (fldTypes[j].ElementType)
					{
						case ClrElementType.Unknown:
							break;
						case ClrElementType.SZArray:
						case ClrElementType.Array:
							fldAddr = fields[j].GetAddress(addr);
							if (fldAddr == 0UL)
								counts[j] = counts[j] + 1;
							else if (fldTypes[j].GetArrayLength(fldAddr) == 0)
							{
								counts[j] = counts[j] + 1;
								++totalDefValues;
							}
							break;
						case ClrElementType.String:
							var str = (string)fields[j].GetValue(addr, false, true);
							if (str == null || str.Length < 1)
							{
								counts[j] = counts[j] + 1;
								++totalDefValues;
							}
							break;
						case ClrElementType.Class:
						case ClrElementType.Object:
							fldAddr = fields[j].GetAddress(addr);
							if (fldAddr == 0UL)
							{
								counts[j] = counts[j] + 1;
								++totalDefValues;
							}
							break;
						case ClrElementType.FunctionPointer:
							fldAddr = fields[j].GetAddress(addr);
							if (fldAddr == 0UL)
							{
								counts[j] = counts[j] + 1;
								++totalDefValues;
							}
							break;
						case ClrElementType.Struct:
							switch (TypeKinds.GetParticularTypeKind(fldKinds[j]))
							{
								case TypeKind.Decimal:
									var dec = ValueExtractor.GetDecimal(addr, fields[j]);
									if (dec == 0m)
									{
										counts[j] = counts[j] + 1;
										++totalDefValues;
									}
									break;
								case TypeKind.DateTime:
									var dt = ValueExtractor.GetDateTime(addr, fields[j], intrnl);
									if (dt < minDt)
									{
										counts[j] = counts[j] + 1;
										++totalDefValues;
									}
									break;
								case TypeKind.TimeSpan:
									var ts = ValueExtractor.GetTimeSpan(addr, fields[j]);
									if (ts.TotalMilliseconds == 0.0)
									{
										counts[j] = counts[j] + 1;
										++totalDefValues;
									}
									break;
								case TypeKind.Guid:
									if (ValueExtractor.IsGuidEmpty(addr, fields[j]))
									{
										counts[j] = counts[j] + 1;
										++totalDefValues;
									}
									break;
							}
							break;
						default:
							if (ValueExtractor.IsPrimitiveValueDefault(addr, fields[j]))
							{
								counts[j] = counts[j] + 1;
								++totalDefValues;
							}
							break;
					}
				}
			}

			const int ColumnCount = 5;
			int listNdx = 0;
			int dataNdx = 0;
			listing<string>[] dataListing = new listing<string>[fldCount];
			string[] data = new string[fldCount * ColumnCount];
			for (int i = 0; i < fldCount; ++i)
			{
				dataListing[listNdx++] = new listing<string>(data, dataNdx, ColumnCount);
				data[dataNdx++] = Utils.LargeNumberString(counts[i]);
				int per = (int)Math.Round(((double)counts[i]*100.0)/(double)addresses.Length);
				data[dataNdx++] = Utils.LargeNumberString(per);
				data[dataNdx++] = fieldDefValues[i].Value;
				data[dataNdx++] = fieldDefValues[i].Key;
				data[dataNdx++] = fieldTypeNames[i];
			}

			ColumnInfo[] colInfos = new[]
{
				new ColumnInfo("count", ReportFile.ColumnType.Int32,100,1,true),
				new ColumnInfo("% rounded", ReportFile.ColumnType.Int32,100,2,true),
				new ColumnInfo("def value", ReportFile.ColumnType.String,100,3,true),
				new ColumnInfo("field name", ReportFile.ColumnType.String,200,4,true),
				new ColumnInfo("field type", ReportFile.ColumnType.String,400,5,true),
			};

			StringBuilder sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
			sb.Append("Default field values stats of: ").Append(clrType.Name).AppendLine();
			sb.Append("Instance count: ").Append(Utils.LargeNumberString(addresses.Length));
			sb.Append(", field count: ").Append(Utils.LargeNumberString(fldCount)).AppendLine();
			var totPercent = (int)Math.Round(((double)totalDefValues * 100.0) / (double)(addresses.Length*fldCount));
			sb.Append("Total default values percent: ").Append(Utils.LargeNumberString(totPercent)).Append("%").AppendLine();
			sb.Append("NOTE: Boolean false value is counted as default one.").AppendLine();

			return new ListingInfo(null, dataListing, colInfos, sb.ToString());

		}

		public ListingInfo CompareTypesWithOther(string otherDumpPath)
		{
			const int ColumnCount = 6;
			string error;
			var runtime = Dump.Runtime;
			runtime.Flush();
			var heap = runtime.GetHeap();
			var myTypeDCT = GetTypeSizesInfo(out error);
			if (error != null) return new ListingInfo(error);
			var otherTypeDct = GetTypeSizesInfo(otherDumpPath, out error);
			if (error != null) return new ListingInfo(error);
			// merge dictionaries
			HashSet<string> set = new HashSet<string>(myTypeDCT.Keys);
			set.UnionWith(otherTypeDct.Keys);
			listing<string>[] dataListing = new listing<string>[set.Count];
			string[] data = new string[set.Count * ColumnCount];
			int totalCount0 = 0;
			ulong grandTotalSize0 = 0UL;
			int totalCount1 = 0;
			ulong grandTotalSize1 = 0UL;

			int listNdx = 0;
			int dataNdx = 0;
			foreach (var str in set)
			{
				int count0 = 0;
				int count1 = 0;
				ulong totSize0 = 0UL;
				ulong totSize1 = 0UL;
				quadruple<int, ulong, ulong, ulong> info0;
				if (myTypeDCT.TryGetValue(str, out info0))
				{
					count0 = info0.First;
					totSize0 = info0.Second;
					totalCount0 += count0;
					grandTotalSize0 += totSize0;
				}
				quadruple<int, ulong, ulong, ulong> info1;
				if (otherTypeDct.TryGetValue(str, out info1))
				{
					count1 = info1.First;
					totSize1 = info1.Second;
					totalCount1 += count1;
					grandTotalSize1 += totSize1;
				}
				dataListing[listNdx++] = new listing<string>(data, dataNdx, ColumnCount);
				data[dataNdx++] = Utils.LargeNumberString(count0);
				data[dataNdx++] = Utils.LargeNumberString(count1);
				data[dataNdx++] = Utils.LargeNumberString(count0 - count1);
				data[dataNdx++] = Utils.LargeNumberString(totSize0);
				data[dataNdx++] = Utils.LargeNumberString(totSize1);
				data[dataNdx++] = str;
			}

			myTypeDCT.Clear();
			myTypeDCT = null;
			otherTypeDct.Clear();
			otherTypeDct = null;
			set.Clear();
			set = null;
			Utils.ForceGcWithCompaction();

			ColumnInfo[] colInfos = new[]
			{
				new ColumnInfo(Constants.BlackDiamond + " Count", ReportFile.ColumnType.Int32,150,1,true),
				new ColumnInfo(Constants.AdhocQuerySymbol + " Count", ReportFile.ColumnType.Int32,150,2,true),
				new ColumnInfo(Constants.BlackDiamond + " Count Diff", ReportFile.ColumnType.Int32,150,3,true),
				new ColumnInfo(Constants.BlackDiamond + " Total Size", ReportFile.ColumnType.UInt64,150,4,true),
				new ColumnInfo(Constants.AdhocQuerySymbol + " Total Size", ReportFile.ColumnType.UInt64,150,5,true),
				new ColumnInfo("Type", ReportFile.ColumnType.String,500,6,true),
			};

			Array.Sort(dataListing, new ListingNumCmpDesc(4));

			var otherDmpName = Path.GetFileName(otherDumpPath);
			StringBuilder sb = new StringBuilder(512);
			sb.Append(Constants.BlackDiamond).Append(" Index Dump: ").Append(_fileMoniker.DumpFileName).AppendLine();
			sb.Append(Constants.AdhocQuerySymbol).Append(" Adhoc Dump: ").Append(otherDmpName).AppendLine();
			sb.Append(Constants.BlackDiamond).Append(" Total Instance Count: ").Append(Utils.LargeNumberString(totalCount0)).AppendLine();
			sb.Append(Constants.AdhocQuerySymbol).Append(" Total Instance Count: ").Append(Utils.LargeNumberString(totalCount1)).AppendLine();
			sb.Append(Constants.BlackDiamond).Append(" Total Instance Size: ").Append(Utils.LargeNumberString(grandTotalSize0)).AppendLine();
			sb.Append(Constants.AdhocQuerySymbol).Append(" Total Instance Size: ").Append(Utils.LargeNumberString(grandTotalSize1)).AppendLine();

			return new ListingInfo(null, dataListing, colInfos, sb.ToString());
		}

		public SortedDictionary<string, quadruple<int, ulong, ulong, ulong>> GetTypeSizesInfo(string path, out string error)
		{
			try
			{

				var dump = ClrtDump.OpenDump(path, out error);
				if (dump == null) return null;
				using (dump)
				{
					return ClrtDump.GetTypeSizesInfo(dump.Runtime.GetHeap(), out error);
				}
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				Dump?.Dispose();
			}
		}

		#endregion segments/generations/sizes

		#region threads/blocking objects

		public bool LoadThreadBlockInfo(out string error)
		{
			error = null;
			if (_threads == null)
				return LoadThreadsAndBlocks(out error);
			return true;
		}

		public bool IsThreadId(int id)
		{
			return id < _threadBlockingMap.Length;
		}

		public int GetIdFromGraph(int id)
		{
			return IsThreadId(id) ? id : id - _threadBlockingMap.Length;
		}

		public int GetBlockingId(int id)
		{
			if (_threads == null) return Constants.InvalidIndex;
			Debug.Assert(id>=_threads.Length);
			return _threads.Length - id;
		}

		public string GetThreadLabel(int id)
		{
			if (_threads == null) return Constants.Unknown;
			ClrtThread thread = _threads[_threadBlockingMap[id]];
			return thread.OSThreadId + "/" + thread.ManagedThreadId;
		}

		public string GetThreadOrBlkLabel(int id, out bool isThread)
		{
			isThread = false;
			if (_threads == null) return Constants.Unknown;
			isThread = IsThreadId(id);
			if (isThread)
			{
				ClrtThread thread = _threads[_threadBlockingMap[id]];
				return thread.OSThreadId + "/" + thread.ManagedThreadId;
			}
			ClrtBlkObject blk = _blocks[GetIdFromGraph(id)];
			return Utils.BaseTypeName(GetTypeName(blk.TypeId)) + "/" + blk.BlkReason;
		}


		#endregion threads/blocking objects

		#region dump

		private bool InitDump(out string error, IProgress<string> progress)
		{
			error = null;
			try
			{
				_clrtDump = new ClrtDump(DumpPath);
				return _clrtDump.Init(out error);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}

		#endregion dump

		#region dispose

		volatile
		bool _disposed = false;

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected void Dispose(bool disposing)
		{
			if (_disposed)
				return;

			if (disposing)
			{
				// Free any other managed objects here.
				//
				_clrtDump?.Dispose();
			}

			// Free any unmanaged objects here.
			//
			_disposed = true;
		}

		~DumpIndex()
		{
			Dispose(false);
		}

		#endregion dispose

	}

	public class KvIntIntKeyCmp : IComparer<KeyValuePair<int, int>>
	{
		public int Compare(KeyValuePair<int, int> a, KeyValuePair<int, int> b)
		{
			return a.Key < b.Key ? -1 : (a.Key > b.Key ? 1 : 0);
		}
	}

	public class KvStrIntKeyCmp : IComparer<KeyValuePair<string, int>>
	{
		public int Compare(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
		{
			return string.Compare(a.Key, b.Key, StringComparison.Ordinal);
		}
	}
}
