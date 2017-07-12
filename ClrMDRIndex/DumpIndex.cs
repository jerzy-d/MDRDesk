using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using DmpNdxQueries;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;

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

        public static KvIntIntKeyCmp KvIntIntKeyCmparer = new KvIntIntKeyCmp();

        public IndexType Type { get; private set; }
        public static bool Is64Bit;
        public static uint WordSize => Is64Bit ? 8u : 4u;

        private readonly DumpFileMoniker _fileMoniker;
        public string AdhocFolder => _fileMoniker.OutputFolder;
        public string OutputFolder => AdhocFolder;
        public string IndexFolder => _fileMoniker.MapFolder;
        public string DumpPath => _fileMoniker.Path;
        public string DumpFileName => _fileMoniker.FileName;

        private string _dumpInfo;
        public string DumpInfo => _dumpInfo;

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

        //private References _references;
        private InstanceReferences _instanceReferences;
        public bool HasInstanceReferences => _instanceReferences != null;

        private WeakReference<StringStats> _stringStats;
        public WeakReference<StringStats> StringStatitics => _stringStats;

        private WeakReference<uint[]> _sizes;
        public WeakReference<uint[]> Sizes => _sizes;

        private WeakReference<uint[]> _baseSizes;
        public WeakReference<uint[]> BaseSizes => _baseSizes;

        private WeakReference<ClrElementType[]> _elementTypes;
        public WeakReference<ClrElementType[]> ElementTypes => _elementTypes;

        private WeakReference<Tuple<int[], int[]>> _arraySizes;
        public WeakReference<Tuple<int[], int[]>> ArraySizes => _arraySizes;

        private ClrtSegment[] _segments; // segment infos, for instances generation histograms
        private bool _segmentInfoUnrooted;

        private ClrtDump _clrtDump;
        public ClrtDump Dump => _clrtDump;
        public ClrRuntime Runtime => _clrtDump.Runtime;

        private string[] _stringIds; // ordered by string ids TODO JRD
        public string[] StringIds => _stringIds;

        private ClrtRootInfo _roots;

        public ulong[] RootObjects => _roots.RootObjects;

        public ulong[] FinalizerAddresses => _roots.FinalizerAddresses;

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
            Is64Bit = Environment.Is64BitProcess;
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

                if (!index.LoadInstanceData(out error)) return null;
                if (InstanceReferences.InstanceReferenceFilesAvailable(runtimeNdx, index._fileMoniker, out error))
                {
                    index._instanceReferences = new InstanceReferences(index._instances, runtimeNdx, index._fileMoniker);
                }

                if (!index.InitDump(out error, progress)) return null;
                index._indexProxy = new IndexProxy(index.Dump, index._instances, index._instanceTypes, index._typeNames,
                    index._roots, index._fileMoniker);
                //if (index._references != null)
                //	index._references.SetIndexProxy(index.IndexProxy);
                return index;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
        }

        private bool LoadInstanceData(out string error)
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

                //// check if we indexed referrences, and load them if we did
                ////
                //{
                //	string refsFilePath = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapRefsObjectFieldPostfix);
                //	if (File.Exists(refsFilePath))
                //	{
                //		_references = new References(_currentRuntimeIndex, _fileMoniker);
                //		_references.Init(out error);
                //	}
                //}

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

                //if (DeadlockFound)
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
            int i;
            try
            {
                var path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapThreadsAndBlocksFilePostfix);
                br = new BinaryReader(File.Open(path, FileMode.Open));
                int threadCnt = br.ReadInt32();
                _threads = new ClrtThread[threadCnt];
                for (i = 0; i < threadCnt; ++i)
                {
                    _threads[i] = ClrtThread.Load(br);
                }
                int blockCnt = br.ReadInt32();
                _blocks = new ClrtBlkObject[blockCnt];
                for (i = 0; i < blockCnt; ++i)
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
                int count;
                if (!Int32.TryParse(ln, out count)) count = 0;

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
            string[] splitter = new[] { Constants.NamespaceSepPadded };
            Dictionary<string, string> strCache = new Dictionary<string, string>(StringComparer.Ordinal);
            var resultDct = new SortedDictionary<string, List<KeyValuePair<string, int>>>(StringComparer.Ordinal);

            string[] items = reversedNames[0].Key.Split(splitter, StringSplitOptions.RemoveEmptyEntries);
            string name;
            if (items.Length > 0)
                name = Utils.GetCachedString(items[0], strCache);
            else
                name = Utils.GetCachedString(string.Empty, strCache);

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
                    new List<KeyValuePair<string, int>>() { new KeyValuePair<string, int>(name, reversedNameInfo.Value) });
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

        public int GetInstanceIndex(ulong address)
        {
            return Utils.AddressSearch(_instances, address);
        }

        private int[] GetInstanceIndices(ulong[] addresses)
        {
            List<int> indices = new List<int>(addresses.Length);
            for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
            {
                var ndx = Utils.AddressSearch(_instances, addresses[i]);
                if (ndx < 0) continue;
                indices.Add(ndx);
            }
            return indices.ToArray();
        }

        #endregion utils

        #region queries

        #region io

        /// <summary>
        /// Get full path of one of the index files.
        /// </summary>
        /// <param name="r">Index of a runtime.</param>
        /// <param name="postfix">Canned file name postix.</param>
        /// <returns>Full path to a index file.</returns>
        public string GetFilePath(int r, string postfix)
        {
            return _fileMoniker.GetFilePath(r, postfix);
        }

        public string GetAdHocPath(string fileName)
        {
            return _fileMoniker.OutputFolder + Path.DirectorySeparatorChar + fileName;
        }

        #endregion io

        #region heap

        public ClrHeap GetFreshHeap()
        {
            return Dump.GetFreshHeap();
        }

        public ClrHeap Heap => Runtime.Heap;

        public ulong[] GetInstancesAddresses(int[] instIds)
        {
            ulong[] addresses = new ulong[instIds.Length];
            for (int i = 0, icnt = instIds.Length; i < icnt; ++i)
            {
                addresses[i] = _instances[instIds[i]];
            }
            Utils.SortAddresses(addresses);
            return addresses;
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

        public ulong[] GetTypeInstances(int typeId, out int unrootedCount)
        {
            unrootedCount = 0;
            var offNdx = Array.BinarySearch(_typeInstanceOffsets, new KeyValuePair<int, int>(typeId, 0), KvIntIntKeyCmparer);
            if (offNdx < 0) return Utils.EmptyArray<ulong>.Value;
            var count = _typeInstanceOffsets[offNdx + 1].Value - _typeInstanceOffsets[offNdx].Value;
            ulong[] addresses = new ulong[count];
            int mapIndex = _typeInstanceOffsets[offNdx].Value;
            for (int i = 0; i < count; ++i)
            {
                var addr = _instances[_typeInstanceMap[mapIndex++]];
                addresses[i] = addr;
                if (Utils.IsUnrooted(addr))
                    ++unrootedCount;
            }
            Utils.SortAddresses(addresses);
            return addresses;
        }


        public ulong[] GetTypeRealAddresses(int typeId)
        {
            var offNdx = Array.BinarySearch(_typeInstanceOffsets, new KeyValuePair<int, int>(typeId, 0), KvIntIntKeyCmparer);
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

        public int[] GetTypeInstanceIndices(int typeId, InstanceReferences.ReferenceType refType)
        {
            var offNdx = Array.BinarySearch(_typeInstanceOffsets, new KeyValuePair<int, int>(typeId, 0), KvIntIntKeyCmparer);
            if (offNdx < 0) return Utils.EmptyArray<int>.Value;
            var count = _typeInstanceOffsets[offNdx + 1].Value - _typeInstanceOffsets[offNdx].Value;
            List<int> indices = new List<int>(count);
            int mapIndex = _typeInstanceOffsets[offNdx].Value;
            bool all = (refType & InstanceReferences.ReferenceType.All) > 0;
            bool rooted = (refType & InstanceReferences.ReferenceType.Rooted) > 0;
            bool unrooted = (refType & InstanceReferences.ReferenceType.Unrooted) > 0;
            bool fnlzer = (refType & InstanceReferences.ReferenceType.Finalizer) > 0;

            for (int i = 0; i < count; ++i)
            {
                int ndx = _typeInstanceMap[mapIndex++];
                if (all) indices.Add(ndx);
                else if (rooted)
                {
                    if (Utils.IsRooted(_instances[ndx]))
                        indices.Add(ndx);
                }
                else if (unrooted)
                {
                    if (!Utils.IsRooted(_instances[ndx]))
                        indices.Add(ndx);
                }
                else
                {
                    Debug.Assert(fnlzer);
                    if (Utils.IsFinalizer(_instances[ndx]))
                        indices.Add(ndx);
                }
            }
            return indices.ToArray();
        }

        public int[] GetTypeAllInstanceIndices(int typeId)
        {
            var offNdx = Array.BinarySearch(_typeInstanceOffsets, new KeyValuePair<int, int>(typeId, 0), KvIntIntKeyCmparer);
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


        public int[] GetRealAddressIndices(ulong[] addresses)
        {
            Debug.Assert(Utils.IsSorted(addresses));
            int i = 0, _i = 0, icnt = addresses.Length, _icnt = _instances.Length;
            int[] indices = new int[icnt];
            while (i < icnt && _i < _icnt)
            {
                if (Utils.RealAddress(addresses[i]) == Utils.RealAddress(_instances[_i]))
                {
                    indices[i++] = _i;
                }
                ++_i;
            }
            return indices;
        }

        public int GetTypeInstanceCount(int typeId)
        {
            var offNdx = Array.BinarySearch(_typeInstanceOffsets, new KeyValuePair<int, int>(typeId, 0), KvIntIntKeyCmparer);
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
                    if (!includeArrays && typeName.EndsWith("[]", StringComparison.Ordinal))
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

        public KeyValuePair<int, ulong[]>[] GetTypeAddresses(int[] typeIds, out int totalCount, out int unrootedCount)
        {
            totalCount = 0;
            unrootedCount = 0;
            KeyValuePair<int, ulong[]>[] result = new KeyValuePair<int, ulong[]>[typeIds.Length];
            for (int i = 0, icnt = typeIds.Length; i < icnt; ++i)
            {
                var addrAry = GetTypeInstances(typeIds[i], out unrootedCount);
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

        public string[] GetTypesImplementingInterface(string interfaceList, out string error)
        {
            return _clrtDump.GetTypesImplementingInterface(interfaceList, out error);
        }

        #endregion types

        #region instance references

        public ValueTuple<string, AncestorNode> GetParentTree(ulong address, int levelMax)
        {
            string error = null;
            try
            {
                var instanceId = Utils.AddressSearch(_instances, address);
                if (instanceId < 0)
                {
                    error = "Cannot find instance at address: " + Utils.AddressString(address);
                    return (error, null);
                }
                var typeId = _instanceTypes[instanceId];
                var typeName = _typeNames[typeId];
                AncestorNode rootNode = new AncestorNode(null, 0, 0, typeId, typeName, new[] { instanceId });

                return GetParentTree(rootNode, levelMax);
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return (error, null);
            }
        }

        public ValueTuple<string, AncestorNode> GetParentTree(int typeId, int levelMax, InstanceReferences.ReferenceType refType = InstanceReferences.ReferenceType.All | InstanceReferences.ReferenceType.Ancestors)
        {
            try
            {
                string typeName = GetTypeName(typeId);
                int[] instances = GetTypeInstanceIndices(typeId, refType);
                if (instances.Length < 1)
                    return ("Cannot find find instances of type: " + GetTypeName(typeId), null);
                var rootNode = new AncestorNode(null, 0, 0, typeId, typeName, instances);
                return GetParentTree(rootNode, levelMax);
            }
            catch (Exception ex)
            {
                return (Utils.GetExceptionErrorString(ex), null);
            }
        }

        public ValueTuple<string, AncestorNode> GetParentTree(int typeId, int[] instIds, int levelMax)
        {
            try
            {
                string typeName = GetTypeName(typeId);
                var rootNode = new AncestorNode(null, 0, 0, typeId, typeName, instIds);
                return GetParentTree(rootNode, levelMax);
            }
            catch (Exception ex)
            {
                return (Utils.GetExceptionErrorString(ex), null);
            }
        }

        public ValueTuple<string, AncestorNode> GetParentTree(AncestorNode rootNode, int levelMax)
        {
            string error = null;
            try
            {
                HashSet<int> set = new HashSet<int>();
                Queue<AncestorNode> que = new Queue<AncestorNode>(Math.Min(1000, rootNode.Instances.Length));
                var dct = new SortedDictionary<int, quadruple<string, List<int>, int, int>>();
                que.Enqueue(rootNode);
                while (que.Count > 0)
                {
                    AncestorNode currentNode = que.Dequeue();
                    dct.Clear();
                    int currentNodeLevel = currentNode.Level + 1;
                    if (currentNodeLevel >= levelMax) continue;
                    var instances = currentNode.Instances;
                    for (int i = 0, icnt = instances.Length; i < icnt; ++i)
                    {
                        var inst = instances[i];
                        if (!set.Add(inst)) continue;

                        var ancestors = //_references.GetReferences(inst, References.Direction.FieldParent, References.DataSource.All, out error);
                        _instanceReferences.GetReferences(inst, out error, InstanceReferences.ReferenceType.Ancestors | InstanceReferences.ReferenceType.All);


                        for (int j = 0, jcnt = ancestors.Length; j < jcnt; ++j)
                        {
                            var ancestor = ancestors[j];
                            var typeid = _instanceTypes[ancestor];
                            var typename = _typeNames[typeid];
                            quadruple<string, List<int>, int, int> quad;
                            if (dct.TryGetValue(typeid, out quad))
                            {
                                quad.Second.Add(ancestor);
                                var childNdx = i + 1;
                                if (quad.Third != childNdx)
                                {
                                    var childCount = quad.Forth + 1;
                                    dct[typeid] = new quadruple<string, List<int>, int, int>(quad.First, quad.Second, childNdx, childCount);
                                }
                                continue;
                            }
                            dct.Add(typeid, new quadruple<string, List<int>, int, int>(typename, new List<int>(16) { ancestor }, i + 1, 1));
                        }
                    }
                    var nodes = new AncestorNode[dct.Count];
                    int n = 0;
                    foreach (var kv in dct)
                    {
                        var ancestors = Utils.RemoveDuplicates(kv.Value.Second);
                        nodes[n] = new AncestorNode(currentNode, currentNodeLevel, kv.Value.Forth, kv.Key, kv.Value.First, ancestors);
                        nodes[n].Sort(AncestorNode.SortAncestors.ByteInstanceCountDesc);
                        que.Enqueue(nodes[n]);
                        ++n;
                    }
                    currentNode.AddNodes(nodes);
                    currentNode.Sort(AncestorNode.SortAncestors.ByteInstanceCountDesc);
                }
                return (null, rootNode);
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return (error, null);
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

        //        DependencyNode[] nodes = DependencyNode.BuildBranches(Types, _instances, InstanceTypeIds, StringIds, FieldDependencies, root, addresses, out error);
        //        for (int i = 0, icnt = nodes.Length; i < icnt; ++i)
        //        {
        //            que.Enqueue(nodes[i]);
        //            ++nodeCount;
        //        }

        //        while (que.Count > 0)
        //        {
        //            var node = que.Dequeue();
        //            if (node.Level == maxLevel) continue;

        //            nodes = DependencyNode.BuildBranches(Types, _instances, InstanceTypeIds, StringIds, FieldDependencies, node, node.Addresses, out error);
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


        //public KeyValuePair<IndexNode, int> GetReferenceNodes(int instNdx, References.Direction dir, References.DataSource source, out string error, int level)
        //{
        //    return _references.GetReferenceNodes(instNdx, dir, source, out error, level);
        //}


        public ListingInfo GetParentReferencesReport(ulong addr, int level = Int32.MaxValue)
        {
            string error;
            var instNdx = GetInstanceIndex(addr);
            if (instNdx < 0)
            {
                error = "No object found at: " + Utils.AddressString(addr);
                return new ListingInfo(error);
            }

            KeyValuePair<IndexNode, int> result = //_references.GetReferenceNodes(instNdx, References.Direction.FieldParent,References.DataSource.All, out error, level);

            _instanceReferences.GetAncestors(instNdx, level, out error, InstanceReferences.ReferenceType.Ancestors | InstanceReferences.ReferenceType.All);


            if (!string.IsNullOrEmpty(error) && error[0] != Constants.InformationSymbol)
            {
                return new ListingInfo(error);
            }
            if (result.Key == null)
            {
                return
                    new ListingInfo("Could not get any information for: " + Utils.RealAddress(addr) +
                                    ", this looks like a bug. Please let know development about it.");
            }

            return OneInstanceParentsReport(result.Key, result.Value);
        }

        public int[] GetAllReferences(ulong addr, InstanceReferences.ReferenceType type, out string error)
        {
            error = null;
            var instNdx = GetInstanceIndex(addr);
            if (instNdx < 0)
            {
                error = "No object found at: " + Utils.AddressString(addr);
                return null;
            }

            KeyValuePair<IndexNode, int> result = _instanceReferences.GetAncestors(instNdx, int.MaxValue, out error, type);
            if (error != null) return null;
            List<int> lst = new List<int>(result.Value);
            Queue<IndexNode> que = new Queue<IndexNode>(1024);
            que.Enqueue(result.Key);
            lst.Add(result.Key.Index);
            HashSet<int> set = new HashSet<int>();
            set.Add(result.Key.Index);
            while (que.Count > 0)
            {
                var node = que.Dequeue();
                if (node.Nodes == null || node.Nodes.Length < 1) continue;
                for (int i = 0, icnt = node.Nodes.Length; i < icnt; ++i)
                {
                    var n = node.Nodes[i];
                    if (set.Contains(n.Index)) continue;
                    lst.Add(n.Index);
                    set.Add(n.Index);
                    que.Enqueue(n);
                }
            }
            return lst.ToArray();
        }


        //public KeyValuePair<IndexNode, int>[] GetReferenceNodes(ulong[] typeInstances, References.Direction  dir, References.DataSource dataSource, out string error, int level)
        //{
        //    int[] indices = GetRealAddressIndices(typeInstances);
        //    return _references.GetReferenceNodes(indices, References.Direction.FieldParent, References.DataSource.All, out error, level);
        //}


        /// <summary>
        /// Get references for all instances of a given type.
        /// </summary>
        /// <param name="typeId">Type id.</param>
        /// <param name="refType">Search flag restrinctions and directions. Rooted, unrooted, finalizer and forward or backward references.</param>
        /// <param name="level">How deep reference tree should be.</param>
        /// <returns></returns>
        public ListingInfo GetTypeReferenceReport(int typeId, InstanceReferences.ReferenceType refType, int level = Int32.MaxValue)
        {
            string error;
            int[] typeInstances = GetTypeInstanceIndices(typeId, refType);
            if (typeInstances.Length < 1)
                return new ListingInfo(Constants.InformationSymbolHeader + "No " + InstanceReferences.InstanceTypeString(refType) + " instances found for this type");
            KeyValuePair<IndexNode, int>[] result = _instanceReferences.GetReferenceNodes(typeInstances, level, _instances, out error, refType);
            if (!string.IsNullOrEmpty(error) && error[0] != Constants.InformationSymbol)
            {
                return new ListingInfo(error);
            }
            return MultiInstanceParentsReport(result);
        }

        public ListingInfo OneInstanceParentsReport(IndexNode rootNode, int nodeCnt)
        {
            const int columnCount = 4;
            string[] data = new string[nodeCnt * columnCount];
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

                items[itemNdx++] = new listing<string>(data, dataNdx, columnCount);
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
            string[] data = new string[totalNodes * ColumnCount];
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
                    string level = Utils.SortableCountStringHeader(node.Level);

                    items[itemNdx++] = new listing<string>(data, dataNdx, ColumnCount);
                    data[dataNdx++] = Utils.AddressString(rootAddr);
                    data[dataNdx++] = Utils.AddressString(address);
                    data[dataNdx++] = level;
                    data[dataNdx++] = typeName;
                    for (int j = 0, jcnt = node.Nodes.Length; j < jcnt; ++j)
                    {
                        que.Enqueue(node.Nodes[j]);
                    }
                }
            }

            ColumnInfo[] colInfos = new[]
            {
                new ColumnInfo("Type _instances", ReportFile.ColumnType.Address, 150, 1, true),
                new ColumnInfo("Parents", ReportFile.ColumnType.Address, 150, 2, true),
                new ColumnInfo("Tree Level", ReportFile.ColumnType.Int32, 100, 3, true),
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


        public AncestorNode[] GetGcRootPath(AncestorNode gcRootNode, AncestorNode targetNode)
        {
            List<AncestorNode> lst = new List<AncestorNode>(gcRootNode.Level + 1);
            lst.Add(gcRootNode);
            var curNode = gcRootNode;
            while (!Object.ReferenceEquals(curNode, targetNode))
            {
                curNode = curNode.Parent;
                lst.Add(curNode);
            }
            return lst.ToArray();
        }

        #endregion instance references

        #region instance value

        public Tuple<InstanceValue, string> GetInstanceValue(ulong addr, out string error)
        {
            error = null;
            StringBuilder sb;
            try
            {
                InstanceValue inst;
                var realAddr = Utils.RealAddress(addr);
                var typeId = GetTypeId(addr);
                var heap = Dump.Heap;
                var clrType = heap.GetObjectType(realAddr);
                if (clrType == null)
                {
                    error = "DumpIndex.GetInstanceValue" + Constants.HeavyGreekCrossPadded + "Object at address: " +
                            Utils.AddressString(addr) + " is null.";
                    return null;
                }

                if (clrType.Name == "Free" || clrType.Name == "Error")
                {
                    error = "DumpIndex.GetInstanceValue" + Constants.HeavyGreekCrossPadded + "Invalid object at address: " +
                            Utils.AddressString(addr) + ", type name is: " + clrType.Name + ".";
                    return null;
                }

                var kind = TypeKinds.GetTypeKind(clrType);

                var newkind = TypeExtractor.GetElementKind(clrType);


                if (TypeKinds.IsArray(kind))
                {
                    var aryResult = CollectionContent.getAryContent(heap, realAddr);
                    if (aryResult.Item1 != null)
                    {
                        error = aryResult.Item1;
                        return null;
                    }
                    sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
                    sb.Append("Type:      ").AppendLine(aryResult.Item2.Name);
                    sb.Append("Item Type: ").AppendLine(aryResult.Item3.Name);
                    sb.Append("Address:   ").AppendLine(Utils.AddressString(addr));
                    sb.Append("Lenght:    ").AppendLine(aryResult.Item4.ToString());
                    inst = new InstanceValue(typeId, newkind, addr, aryResult.Item2.Name, aryResult.Item3.Name,
                        Utils.BaseArrayName(aryResult.Item2.Name, aryResult.Item4));
                    inst.AddArrayValues(aryResult.Item5);
                    return new Tuple<InstanceValue, string>(inst, StringBuilderCache.GetStringAndRelease(sb));
                }

                if (TypeKinds.IsClassStruct(kind))
                {
                    var result = Types.getClassStructValue(this.IndexProxy, heap, addr, clrType, kind, -1);
                    error = result.Item1;
                    string info = string.Empty;
                    if (result.Item2 != null)
                    {
                        sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
                        sb.Append("Type:      ").AppendLine(result.Item2.TypeName);
                        sb.Append("Address:   ").AppendLine(Utils.AddressString(addr));
                        info = StringBuilderCache.GetStringAndRelease(sb);
                        result.Item2?.SortByFieldName();
                    }
                    return new Tuple<InstanceValue, string>(result.Item2, info);
                }

                var val = Types.getTypeValue(heap, realAddr, clrType, kind);
                var typeName = GetTypeName(typeId);
                sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
                sb.Append("Type:      ").AppendLine(typeName);
                sb.Append("Address:   ").AppendLine(Utils.AddressString(addr));
                var newKind = TypeExtractor.GetElementKind(clrType);
                inst = new InstanceValue(typeId, newKind, addr, typeName, string.Empty, val);
                return new Tuple<InstanceValue, string>(inst, StringBuilderCache.GetStringAndRelease(sb));
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
        }

        public (string, InstanceValue) GetInstanceValue(ulong addr, InstanceValue parent)
        {
            try
            {
                int instId = GetInstanceIndex(addr);
                if (instId < 0)
                {
                    string err = Constants.InformationSymbolHeader + "Cannot find address on the heap: " + Utils.RealAddressString(addr);
                    return (err, null);
                }

                (string error, InstanceValue inst) = ValueExtractor.GetInstanceValue(IndexProxy, Dump.Heap, addr, Constants.InvalidIndex, parent);
                if (inst != null) inst.SortByFieldName();
                return (error, inst);
            }
            catch (Exception ex)
            {
                return (Utils.GetExceptionErrorString(ex), null);
            }
        }

        public (string, InstanceValue[]) GetInstanceValueFields(ulong addr, InstanceValue parent)
        {
            try
            {
                (string error, InstanceValue[] fields) = ValueExtractor.GetInstanceValueFields(IndexProxy, Dump.Heap, addr, parent);
                if (fields != parent.Fields) parent.SetFields(fields);
                parent.SortByFieldName();
                return (error, fields);
            }
            catch (Exception ex)
            {
                return (Utils.GetExceptionErrorString(ex), null);
            }
        }

        #endregion instance value


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
                AncestorDispRecord[] ancestorInfos = Utils.EmptyArray<AncestorDispRecord>.Value;
                var instNdx = GetInstanceIndex(addr);
                if (instNdx >= 0)
                {
                    var ancestors = //_references.GetReferences(instNdx, References.Direction.FieldParent, References.DataSource.All,out error);
                    _instanceReferences.GetReferences(instNdx, out error, InstanceReferences.ReferenceType.Ancestors | InstanceReferences.ReferenceType.All);

                    if (error != null && !Utils.IsInformation(error)) return null;
                    if (ancestors != null && ancestors.Length > 0)
                        ancestorInfos = GroupAddressesByTypesForDisplay(ancestors);
                }

                // get instance info: fields and values
                //
                //var heap = GetFreshHeap();
                //var result = FQry.getInstanceValue(_indexProxy, heap, addr, fldNdx);
                var instValue = GetInstanceValue(addr, out error);
                instValue.Item1?.SortByFieldName();
                return new InstanceValueAndAncestors(instValue.Item1, ancestorInfos);
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
        }

        public AncestorDispRecord[] GroupAddressesByTypesForDisplay(int[] parents)
        {
            var dct = new SortedDictionary<KeyValuePair<int, string>, List<ulong>>(new Utils.KvIntStr());
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

        public ValueTuple<string, ClrtDisplayableType, ulong[]> GetTypeDisplayableRecord(int typeId)
        {
            string error = null;
            try
            {
                ulong[] instances = GetTypeRealAddresses(typeId);
                if (instances == null || instances.Length < 1)
                    return new ValueTuple<string, ClrtDisplayableType, ulong[]>("Type instances not found.", null, null);
                ClrtDisplayableType cdt = TypeExtractor.GetClrtDisplayableType(_indexProxy, Dump.Heap, typeId, instances, out error);
                if (cdt != null) cdt.SetAddresses(instances);
                return new ValueTuple<string, ClrtDisplayableType, ulong[]>(error, cdt, instances); ;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return new ValueTuple<string, ClrtDisplayableType, ulong[]>(error, null, null);
            }

        }


        public ValueTuple<string, ClrtDisplayableType, ulong[]> GetTypeDisplayableRecord(int typeId, ClrtDisplayableType parent, ClrtDisplayableType[] fields = null, ulong[] rootInstances = null)
        {
            string error = null;
            try
            {
                ulong[] instances = rootInstances != null ? rootInstances : GetTypeRealAddresses(typeId);
                if (instances == null || instances.Length < 1)
                    return new ValueTuple<string, ClrtDisplayableType, ulong[]>("Type instances not found.", null, null);
                if (fields != null)
                {
                    Debug.Assert(fields.Length > 1);
                    instances = GetInstanceFieldAddresses(instances, fields, out error);
                    if (error != null)
                        return new ValueTuple<string, ClrtDisplayableType, ulong[]>(error, null, null);
                }
                ClrtDisplayableType cdt = TypeExtractor.GetClrtDisplayableType(_indexProxy, Dump.Heap, parent, typeId, instances, out error);
                if (cdt != null) cdt.SetAddresses(instances);
                return new ValueTuple<string, ClrtDisplayableType, ulong[]>(error, parent, instances); ;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return new ValueTuple<string, ClrtDisplayableType, ulong[]>(error, null, null);
            }

        }

        private ulong[] GetInstanceFieldAddresses(ulong[] instances, ClrtDisplayableType[] types, out string error)
        {
            error = null;
            try
            {
                return GetInstanceFieldAddresses2(instances, types, out error);

                //bool hasAmbiguousField = Array.FindIndex(types, t => t.IsAlternative) != -1;
                //if (!hasAmbiguousField)
                //{
                //    List<int> fieldIndices = new List<int>(types.Length - 1);
                //    // we need to get addresses of parent, starting at root instances, first item in the list is root type
                //    for (int j = 1, jcnt = types.Length; j < jcnt; ++j)
                //    {
                //        if (types[j].IsDummy) continue;
                //        fieldIndices.Add(types[j].FieldIndex);
                //    }
                //    return GetInstanceFieldAddresses2(instances, fieldIndices.ToArray(), out error);
                //}

                //var heap = Heap;
                //ClrType rootType = null;
                //ClrInstanceField[] fields = new ClrInstanceField[types.Length];
                //int icount = instances.Length;
                //List<ulong> addresses = new List<ulong>(instances.Length);

                //for (int i = 0; i < icount; ++i)
                //{
                //    var found = true;
                //    var addr = instances[i];
                //    rootType = heap.GetObjectType(addr);
                //    if (rootType == null) continue;

                //    var curType = rootType;
                //    var curAddr = addr;
                //    for (int j = 1, jcnt = types.Length; j < jcnt; ++j)
                //    {
                //        var fld = curType.Fields[types[j].FieldIndex];
                //        curAddr = ValueExtractor.GetReferenceFieldAddress(curAddr, fld, false);
                //        curType = heap.GetObjectType(curAddr);
                //        if (!Utils.SameStrings(curType.Name, types[j].TypeName)) { found = false; break; }
                //    }

                //    if (found)
                //    {
                //        addresses.Add(curAddr);
                //    }
                //}
                //return addresses.ToArray();
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }

        }

        private ulong[] GetInstanceFieldAddresses(ulong[] instances, int[] fieldIndices, out string error)
        {
            error = null;
            List<ulong> addresses = new List<ulong>(instances.Length);
            try
            {
                var heap = Heap;
                int i = 0;
                ClrType rootType = null;
                ClrInstanceField[] fields = new ClrInstanceField[fieldIndices.Length];
                int icount = instances.Length;

                bool done = true;
                for (; i < icount; ++i)
                {
                    done = true;
                    var addr = instances[i];
                    rootType = heap.GetObjectType(addr);
                    if (rootType == null) continue;
                    var curType = rootType;
                    var curAddr = addr;
                    for (int j = 0, jcnt = fieldIndices.Length; j < jcnt; ++j)
                    {
                        if (fields[j] != null) continue;
                        var fld = curType.Fields[fieldIndices[j]];
                        curAddr = ValueExtractor.GetReferenceFieldAddress(curAddr, fld, false);
                        curType = heap.GetObjectType(curAddr);
                        if (curType == null) { done = false; break; }
                        fields[j] = fld;
                    }

                    if (done) break;
                }
                if (!done)
                {
                    error = Utils.GetErrorString("Type Report", "Cannot resolve field chain.", rootType != null ? rootType.Name : "Getting root type failed.");
                    return null;
                }
                for (; i < icount; ++i)
                {
                    var addr = instances[i];
                    for (int j = 0, jcnt = fieldIndices.Length; j < jcnt; ++j)
                    {
                        var fld = fields[j];
                        addr = ValueExtractor.GetReferenceFieldAddress(addr, fld, false);
                    }
                    if (addr != Constants.InvalidAddress)
                        addresses.Add(addr);
                }

                return addresses.ToArray();
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }

        }


        private ulong[] GetInstanceFieldAddresses2(ulong[] instances, int[] fieldIndices, out string error)
        {
            error = null;
            ulong[] addresses = instances;
            try
            {
                for (int i = 0, icnt = fieldIndices.Length; i < icnt; ++i)
                {
                    addresses = GetInstanceFieldAddresses(addresses, fieldIndices[i], out error);
                    if (addresses == null) return null;
                }
                return addresses;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }

        }

        private ulong[] GetInstanceFieldAddresses2(ulong[] instances, ClrtDisplayableType[] fields, out string error)
        {
            error = null;
            ulong[] addresses = instances;
            try
            {
                for (int i = 1, icnt = fields.Length; i < icnt; ++i)
                {
                    var fld = fields[i];
                    if (fld.IsDummy) continue;
                    addresses = fld.IsAlternative
                        ? GetInstanceFieldAddresses(addresses, fld.FieldIndex, fld.TypeName, out error)
                        : GetInstanceFieldAddresses(addresses, fld.FieldIndex, out error);
                    if (addresses == null) return null;
                    fld.SetAddresses(addresses);
                }
                return addresses;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }

        }

        private ulong[] GetInstanceFieldAddresses(ulong[] instances, int fldIndex, out string error)
        {
            error = null;
            List<ulong> addresses = new List<ulong>(instances.Length);
            try
            {
                var heap = Heap;
                int icount = instances.Length;
                ClrType clrType = heap.GetObjectType(instances[0]);
                Debug.Assert(clrType != null);
                ClrInstanceField fld = clrType.Fields[fldIndex];

                for (int i = 0; i < icount; ++i)
                {
                    var fldAddr = ValueExtractor.GetReferenceFieldAddress(instances[i], fld, false);
                    if (fldAddr != Constants.InvalidAddress)
                        addresses.Add(fldAddr);
                }
                return addresses.ToArray();
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }

        }

        private ulong[] GetInstanceFieldAddresses(ulong[] instances, int fldIndex, string typeName, out string error)
        {
            error = null;
            List<ulong> addresses = new List<ulong>(instances.Length);
            try
            {
                var heap = Heap;
                int icount = instances.Length;

                for (int i = 0; i < icount; ++i)
                {
                    ClrType clrType = heap.GetObjectType(instances[i]);
                    Debug.Assert(clrType != null);
                    ClrInstanceField fld = clrType.Fields[fldIndex];
                    var fldAddr = ValueExtractor.GetReferenceFieldAddress(instances[i], fld, false);
                    if (fldAddr == Constants.InvalidAddress) continue;
                    ClrType fldType = heap.GetObjectType(fldAddr);
                    if (fldType == null || !Utils.SameStrings(typeName, fldType.Name)) continue;
                    addresses.Add(fldAddr);
                }
                return addresses.ToArray();
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }

        }


        //public ClrtDisplayableType GetTypeDisplayableRecord(int typeId, ClrtDisplayableType parent, out string error)
        //{
        //	error = null;
        //	try
        //	{
        //		ulong[] instances = GetTypeRealAddresses(typeId);
        //		if (instances == null || instances.Length < 1)
        //		{
        //			error = "Type instances not found.";
        //			return null;
        //		}
        //		return TypeExtractor.GetClrtDisplayableType(_indexProxy, Dump.Heap, parent, typeId, instances, out error);
        //	}
        //	catch (Exception ex)
        //	{
        //		error = Utils.GetExceptionErrorString(ex);
        //		return null;
        //	}

        //}

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="dispTypeParent"></param>
        ///// <param name="dispTypeField"></param>
        ///// <param name="error"></param>
        ///// <returns></returns>
        //public ClrtDisplayableType GetTypeDisplayableRecord(ClrtDisplayableType dispTypeParent, out string error)
        //{
        //	error = null;
        //	try
        //	{
        //		ulong[] parentInstances = null;
        //		if (dispTypeParent != null)
        //		{
        //			parentInstances = GetTypeRealAddresses(dispTypeParent.TypeId);
        //			if (parentInstances == null || parentInstances.Length < 1)
        //			{
        //				error = Constants.InformationSymbolHeader + "Parent type instances not found (should not happen)." + Environment.NewLine + dispTypeParent.TypeName;
        //				return null;
        //			}
        //		}

        //		return TypeExtractor.GetClrtDisplayableTypeFields(_indexProxy, Dump.Heap, dispTypeParent, parentInstances, out error);
        //	}
        //	catch (Exception ex)
        //	{
        //		error = Utils.GetExceptionErrorString(ex);
        //		return null;
        //	}

        //}

        private bool GetValues(ClrHeap heap, TypeValueQuery parentQry, ulong addr, TypeValueQuery qry)
        {
            bool accepted = true;
            if (addr == Constants.InvalidAddress)
            {
                if (qry.GetValue) qry.AddValue(Constants.NotApplicaleValue);
                if (qry.HasChildren)
                {
                    for (int i = 0, icnt = qry.Children.Length; i < icnt; ++i)
                    {
                        if (!GetValues(null, qry, Constants.InvalidAddress, qry.Children[i]))
                            accepted = false;
                    }
                }
                return accepted;
            }

            if (qry.Field == null)
            {
                qry.SetField(parentQry.Type);
                if (qry.HasChildren)
                {
                    ulong faddr = ValueExtractor.GetReferenceFieldAddress(addr, qry.Field, qry.IsInternal);
                    var kv = TypeExtractor.TryGetRealType(heap, faddr);
                    qry.SetTypeAndKind(kv.Key, kv.Value);
                }
            }

            //if (qry.IsAlternative)
            //{
            //    if (qry.Type == null)
            //    {
            //        ulong fldaddr = ValueExtractor.GetReferenceFieldAddress(addr, qry.Field, qry.IsInternal);
            //        var typeAndKind = TypeExtractor.TryGetRealType(heap, fldaddr);
            //        qry.SetTypeAndKind(typeAndKind.Key, typeAndKind.Value);



            //    }
            //    if (parentQry.IsCompatibleField(qry.FieldIndex,qry.FieldName,qry.TypeName))
            //    {

            //    }
            //    var kv = TypeExtractor.TryGetRealType(heap, addr);
            //    qry.SetTypeAndKind(kv.Key, kv.Value);

            //    var fld = parentQry.Type.Fields[qry.FieldIndex];
            //    ulong faddr = ValueExtractor.GetReferenceFieldAddress(addr, fld, qry.IsInternal);

            //    if (qry.GetValue) qry.AddValue(Utils.RealAddressString(faddr));
            //    if (qry.HasChildren)
            //    {
            //        for (int i = 0, icnt = qry.Children.Length; i < icnt; ++i)
            //        {
            //            if (!GetValues(heap, qry, faddr, qry.Children[i]))
            //                accepted = false;
            //        }
            //    }
            //    return accepted;
            //}

            object val = ValueExtractor.GetFieldValue(heap, addr, qry.Field, qry.Type, qry.Kind, parentQry.IsInternal, true);
            if (qry.GetValue)
            {
                if (!qry.Accept(val))
                {
                    qry.AddValue(Constants.NotApplicaleValue);
                    if (qry.HasChildren)
                    {
                        for (int i = 0, icnt = qry.Children.Length; i < icnt; ++i)
                        {
                            GetValues(null, qry, Constants.InvalidAddress, qry.Children[i]);
                        }
                    }
                    return false;
                }
                else
                    qry.AddValue(ValueExtractor.ValueToString(val, qry.Kind));
            }
            if (qry.HasChildren)
            {
                ulong qaddr = (ulong)val;
                for (int i = 0, icnt = qry.Children.Length; i < icnt; ++i)
                {
                    if (!GetValues(heap, qry, qaddr, qry.Children[i]))
                        accepted = false;
                }
            }

            return accepted;
        }


        public ListingInfo GetTypeValuesReport(TypeValueQuery query, ulong[] instances, out string error)
        {
            error = null;
            try
            {
                Debug.Assert(query.HasChildren);
                // get type addresses
                //
                Debug.Assert(instances != null && instances.Length > 0);
                if (instances == null || instances.Length < 1)
                {
                    error = Constants.InformationSymbolHeader + "Type instances not found? Should not happen!" + Environment.NewLine + query.TypeName;
                    return null;
                }

                // prepare query items types and their fields
                //
                var heap = Heap;
                bool[] accepted = Enumerable.Repeat(true, instances.Length).ToArray();
                int nonAcceptedCount = 0;
                for (int i = 0, icnt = instances.Length; i < icnt; ++i)
                {
                    var addr = Utils.RealAddress(instances[i]);
                    var curAddr = addr;
                    if (query.Type == null)
                    {
                        var kv = TypeExtractor.TryGetRealType(heap, addr);
                        query.SetTypeAndKind(kv.Key, kv.Value);
                    }
                    query.AddValue(Utils.AddressString(instances[i]));

                    bool acceptedRow = true;
                    for (int j = 0, jcnt = query.Children.Length; j < jcnt; ++j)
                    {
                        var child = query.Children[j];
                        if (!GetValues(heap, query, addr, child))
                        {
                            acceptedRow = false;
                        }
                    }
                    if (!acceptedRow)
                    {
                        accepted[i] = false;
                        ++nonAcceptedCount;
                    }
                }
 
                // prepare listing output
                //
                int qryGetValueCnt = query.RowValueCount;
                int valCnt = query.ValueCount;

                string[] data = nonAcceptedCount > 0 ? new string[(valCnt - nonAcceptedCount)* qryGetValueCnt] : query.Data; // data is already populated
                string[] qryData = query.Data;
                listing<string>[] items = new listing<string>[valCnt-nonAcceptedCount];

                ColumnInfo[] colInfos = GetTypeValueReportColumns(query);
                int dataNdx = 0;
                int ndx = 0;
                for (int i = 0, icnt = valCnt; i < icnt; ++i)
                {
                    if (!accepted[i]) continue;
                    items[ndx++] = new listing<string>(data, dataNdx, qryGetValueCnt);
                    if (nonAcceptedCount > 0)
                    {
                        for(int j =0, jcnt= qryGetValueCnt; j < jcnt; ++j)
                        {
                            data[dataNdx++] = qryData[i + j];
                        }
                    }
                    else
                        dataNdx += qryGetValueCnt;
                }

                var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
                sb.AppendLine(query.TypeName + "  COUNT: " + valCnt);
                if (query.HasChildren)
                {
                    for (int i = 0, icnt = query.Children.Length; i < icnt; ++i)
                    {
                        BuildTypeValueReportInfo(sb, query.Children[i], "   ");
                    }
                }
                return new ListingInfo(null, items, colInfos, StringBuilderCache.GetStringAndRelease(sb));
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }

        }

        private void BuildTypeValueReportInfo(StringBuilder sb, TypeValueQuery qry, string indent)
        {
            sb.AppendLine(indent + qry.FieldName + "   " + qry.TypeName);
            if (qry.HasChildren)
            {
                for (int i = 0, icnt = qry.Children.Length; i < icnt; ++i)
                {
                    BuildTypeValueReportInfo(sb, qry.Children[i], indent + "   ");
                }
            }
        }

        private void GetTypeValueReportColumns(TypeValueQuery qry, List<ColumnInfo> colInfos, ref int ndx)
        {
            if (qry.GetValue)
            {
                ++ndx;
                colInfos.Add(new ColumnInfo(qry.Field.Name, ReportFile.ColumnType.String, 150, ndx, true));
            }
            if (qry.HasChildren)
            {
                foreach (var child in qry.Children)
                {
                    GetTypeValueReportColumns(child, colInfos, ref ndx);
                }
            }
        }

        private ColumnInfo[] GetTypeValueReportColumns(TypeValueQuery qry)
        {
            List<ColumnInfo> colInfos = new List<ColumnInfo>();
            int ndx = 1;
            colInfos.Add(new ColumnInfo("ADDRESS", ReportFile.ColumnType.UInt64, 150, ndx, true));
            if (qry.HasChildren)
            {
                foreach(var child in qry.Children)
                {
                    GetTypeValueReportColumns(child, colInfos, ref ndx);
                }
            }
            return colInfos.ToArray();
        }



///// <summary>
///// Get type report values for one instance.
///// </summary>
///// <param name="heap">Current dump's heap (from MDR).</param>
///// <param name="qrys">Info about the values to be collected.</param>
///// <param name="addresses">Buffer for parent addresses.</param>
///// <param name="values">Buffer for values as strings.</param>
///// <param name="addr">An instance decorated address.</param>
///// <returns>True if values were accepted by filter(s).</returns>
//private bool GetTypeValues(ClrHeap heap, TypeValueQuery[] qrys, ulong[] addresses, string[] values, ulong addr)
//        {
//            // first item collects the object address
//            //
//            values[0] = Utils.RealAddressString(addr);
//            addr = Utils.RealAddress(addr); // switch to real address
//            addresses[0] = addr;
//            for (int i = 1, icnt = qrys.Length; i < icnt; ++i)
//            {
//                values[i] = null;
//                addresses[i] = Constants.InvalidAddress;
//                var qry = qrys[i];
//                var qparent = qrys[qry.ParentIndex];
//                var parentAddr = addresses[qry.ParentIndex];
//                if (parentAddr == Constants.InvalidAddress)
//                {
//                    if (qry.HasFilter)
//                    {
//                        if (!qry.Filter.AcceptNull()) return false;
//                    }
//                    values[i] = Constants.NullValue;
//                    continue;
//                }
//                if (qry.HasFilter)
//                {
//                    object val = ValueExtractor.GetFieldValue(heap, parentAddr, qry.Field, qry.Type, qry.Kind, TypeExtractor.IsStruct(qparent.Kind), true);
//                    if (!qry.Filter.Accept(val)) return false;
//                    if (qry.GetValue)
//                        values[i] = ValueExtractor.ValueToString(val, qry.Kind);
//                    if (TypeExtractor.IsNonStringObjectReference(qry.Kind))
//                        addresses[i] = (ulong)val;
//                    continue;
//                }
//                if (qry.GetValue)
//                {
//                    values[i] = (string)ValueExtractor.GetFieldValue(heap, parentAddr, qry.Field, qry.Type, qry.Kind, TypeExtractor.IsStruct(qparent.Kind), false);
//                }
//                if (TypeExtractor.IsNonStringObjectReference(qry.Kind))
//                    addresses[i] = (ulong)qry.Field.GetValue(parentAddr, TypeExtractor.IsStruct(qparent.Kind), false);
//            }
//            return true;
//        }


        #endregion Type Value Reports

        #region disassemble

        private Tuple<ClrMethod, MethodCompilationType, ulong, ulong> GetMethodInfo(ClrType clrType, string methodName,
            out string error)
        {
            error = null;
            try
            {
                ClrMethod method = clrType.Methods.Single(m => Utils.SameStrings(m.Name, methodName));
                MethodCompilationType compilationType = method.CompilationType;
                ulong startAddr = method.NativeCode;
                ulong endAddr = method.ILOffsetMap.Select(entry => entry.EndAddress).Max();
                return new Tuple<ClrMethod, MethodCompilationType, ulong, ulong>(method, compilationType, startAddr, endAddr);
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
        }

        public string[] Disassemble(ulong addr, string methodName, out string error)
        {
            error = null;
            try
            {
                var clrType = Runtime.Heap.GetObjectType(addr);
                Tuple<ClrMethod, MethodCompilationType, ulong, ulong> methodInfo =
                    GetMethodInfo(clrType, methodName, out error);
                var lineOfAssembly = new StringBuilder(512);
                var control = (IDebugControl)Dump.DataTarget.DebuggerInterface;
                //var control = (IDebugControl)DataTarget.CreateFromDebuggerInterface(client);
                ulong startOffset = methodInfo.Item3, endOffset;
                ulong endAddress = methodInfo.Item4;
                uint disassemblySize;
                List<string> lst = new List<string>(128);
                do
                {
                    var flags = DEBUG_DISASM.EFFECTIVE_ADDRESS; // DEBUG_DISASM.SOURCE_FILE_NAME | DEBUG_DISASM.SOURCE_LINE_NUMBER;
                    var result = control.Disassemble(startOffset, flags, lineOfAssembly, 512, out disassemblySize, out endOffset);
                    startOffset = endOffset;
                    lst.Add(lineOfAssembly.ToString());
                } while (disassemblySize > 0 && endOffset <= endAddress);
                return lst.ToArray();
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
        }

        public string[] GetMethodNames(ulong addr)
        {
            var clrType = Runtime.Heap.GetObjectType(addr);
            string[] methods = new string[clrType.Methods.Count];
            for (int i = 0, icnt = methods.Length; i < icnt; ++i)
            {
                methods[i] = clrType.Methods[i].Name;
            }
            return methods;
        }

        #endregion disassemble

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

        public Tuple<string, DisplayableFinalizerQueue> GetDisplayableFinalizationQueue()
        {
            string error = null;
            try
            {
                var dispFinlQue = FinalizerQueueDisplayableItem.GetDisplayableFinalizerQueue(_roots.GetFinalizerItems(), _roots.FinalizerAddresses, _typeNames,
                    _fileMoniker);
                return new Tuple<string, DisplayableFinalizerQueue>(error, dispFinlQue);
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
        }

        public ValueTuple<ulong[], ulong[]> GetRootAddresses(out string error)
        {
            error = null;
            try
            {
                return (_roots.RootAddresses, _roots.RootObjects);
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return (null, null);
            }
        }

        #endregion roots

        #region weakreferences

        public ListingInfo GetWeakReferenceInfo(out string error)
        {
            error = null;
            try
            {
                // ReSharper disable InconsistentNaming
                int totalWeakRefCount = 0;
                var ids = GetTypeIds("System.WeakReference", false);
                if (ids.Length < 1) return new ListingInfo(Constants.InformationSymbolHeader + "No System.WeakReference instances found.");
                var indices = new KeyValuePair<int, int[]>[ids.Length];
                var dct = new SortedDictionary<int, Dictionary<int, List<int>>>();
                var heap = Dump.GetFreshHeap();
                for (int i = 0, icnt = ids.Length; i < icnt; ++i)
                {
                    var refs = GetTypeAllInstanceIndices(ids[i]);
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
                        var m_handleVal = m_handleObj == null ? 0L : (long)m_handleObj;
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
                                lst.Add(instNdx, new List<int>() { wkRefInstNdx });
                            }
                        }
                        else
                        {
                            lst = new Dictionary<int, List<int>>();
                            lst.Add(instNdx, new List<int>() { wkRefInstNdx });
                            dct.Add(instType, lst);
                        }
                    }
                }

                // format data and prepare report listing
                //
                var dataAry = new string[dct.Count * 3];
                var infoAry = new listing<string>[dct.Count];
                var addrData = new KeyValuePair<ulong, ulong[]>[dct.Count][];
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
                        instAry[instNdx++] = new KeyValuePair<ulong, ulong[]>(instAddr, wkRefAry);
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

                return new ListingInfo(null, infoAry, colInfos, descr, addrData);

                // ReSharper restore InconsistentNaming
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
        }

        public KeyValuePair<int, ulong[]>[] GetTypeWithPrefixAddresses(string prefix, bool includeArrays, out int totalCount, out int unrootedCount)
        {
            int[] ids = GetTypeIds(prefix, includeArrays);
            return GetTypeAddresses(ids, out totalCount, out unrootedCount);
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
            // ReSharper disable RedundantAssignment
            string error;

            const int columnCount = 7;

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
            listing<string>[] listing = new listing<string>[dataAry.Length / columnCount];
            Debug.Assert((dataAry.Length % columnCount) == 0);

            data.Clear();
            data = null;
            int dataNdx = 0;
            for (int i = 0, icnt = listing.Length; i < icnt; ++i)
            {
                listing[i] = new listing<string>(dataAry, dataNdx, 6);
                dataNdx += columnCount;
            }

            ColumnInfo[] colInfos = {
                new ColumnInfo(Constants.BlackDiamond + " Count", ReportFile.ColumnType.Int32,150,1,true),
                new ColumnInfo(Constants.AdhocQuerySymbol + " Count", ReportFile.ColumnType.Int32,150,2,true),
                new ColumnInfo(Constants.BlackDiamond + " Count Diff", ReportFile.ColumnType.Int32,150,3,true),
                new ColumnInfo(Constants.BlackDiamond + " Total Size", ReportFile.ColumnType.UInt64,150,4,true),
                new ColumnInfo(Constants.AdhocQuerySymbol + " Total Size", ReportFile.ColumnType.UInt64,150,5,true),
                new ColumnInfo("Size", ReportFile.ColumnType.UInt32,150,6,true),
                new ColumnInfo("Type", ReportFile.ColumnType.String,500,7,true),
            };

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
            // ReSharper restore RedundantAssignment
        }

        private void GetStringsDiffLine(string str, int myCount, int otCount, uint size, List<string> data)
        {

            var myCountStr = Utils.LargeNumberString(myCount);
            var otCntStr = Utils.LargeNumberString(otCount);
            var cntDiffStr = Utils.LargeNumberString(myCount - otCount);
            var myTotSizeStr = Utils.LargeNumberString((ulong)myCount * size);
            var otTotSizeStr = Utils.LargeNumberString((ulong)otCount * size);
            var mySizeStr = Utils.LargeNumberString(size);
            data.Add(myCountStr);
            data.Add(otCntStr);
            data.Add(cntDiffStr);
            data.Add(myTotSizeStr);
            data.Add(otTotSizeStr);
            data.Add(mySizeStr);
            data.Add(str);
        }


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
                    ListingInfo data;
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
                var heap = runtime.Heap;

                var stats = ClrtDump.GetStringStats(heap, addresses, DumpPath, out error);
                if (stats == null) return new ListingInfo(error);
                ListingInfo lstData;
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
                int unrootedCount;
                ulong[] addresses = GetTypeInstances(strTypeId, out unrootedCount);
                var runtime = Dump.Runtime;
                runtime.Flush();
                var heap = runtime.Heap;


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
                StringStats strStats;
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
            var strStats = GetCurrentStringStats(out error);
            if (error != null) return -1L;
            return strStats.GetSizeOfStringsWithPrefix(str);
        }

        public ValueTuple<string, AncestorNode> GetTypesWithSpecificStringFieldListing(string strContent)
        {
            return GetTypesWithSpecificStringField(strContent);
        }

        public ValueTuple<string, AncestorNode> GetTypesWithSpecificStringField(string strContent)
        {
            try
            {
                string error;
                var strStats = GetCurrentStringStats(out error);
                var addresses = strStats.GetStringAddresses(strContent, out error);
                if (error != null)
                {
                    error = Utils.GetErrorString("Types with Specific String Field", "StringStats.GetStringAddresses failed.",
                        error);
                    return (error, null);
                }

                List<string> errors = new List<string>();

                var indices = GetInstanceIndices(addresses);
                int typeId = GetTypeId("System.String");
                (string err, AncestorNode node) = GetParentTree(typeId, indices, 2);
                if (err != null)
                {
                    return (err, null);
                }
                node.AddData(new Tuple<string, int>(strContent, node.Ancestors.Length));
                return (null, node);
            }
            catch (Exception ex)
            {
                return (Utils.GetExceptionErrorString(ex), null);
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
                return ClrtDump.GetStringCountsAndSizes(dump.Runtime.Heap, out totalSize, out totalUniqueSize, out totalCount, out error);
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
                return new KeyValuePair<uint, uint>(0, 0);
            }
            var sizes = GetSizeArray(false, out error);
            if (error != null) return new KeyValuePair<uint, uint>(0, 0);
            var totalSize = sizes[instNdx];
            var baseSizes = GetSizeArray(true, out error);
            if (error != null) return new KeyValuePair<uint, uint>(0, 0);
            var baseSize = baseSizes[instNdx];
            return new KeyValuePair<uint, uint>(baseSize, totalSize);

        }

        public Tuple<int[], ulong[], int[], ulong[], int[], ulong[]> GetTotalGenerationDistributions(out string error)
        {
            var sizes = GetSizeArray(false, out error);
            return ClrtSegment.GetTotalGenerationDistributions(_currentRuntimeIndex, _segments, _instances, sizes, _fileMoniker, _segmentInfoUnrooted);
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
            var typeId = Array.BinarySearch(_typeNames, typeName, StringComparer.Ordinal);
            if (typeId < 0)
            {
                error = "cannot find type: " + typeName;
                return null;
            }
            return GetTypeGcGenerationHistogram(typeId);
        }

        public int[] GetTypeGcGenerationHistogram(int typeId)
        {
            int unrootedCount;
            ulong[] addresses = GetTypeInstances(typeId, out unrootedCount);
            return ClrtSegment.GetGenerationHistogram(_segments, addresses);
        }

        public static bool GetLohInfo(ClrRuntime runtime, out string error)
        {
            error = null;
            try
            {
                runtime.Flush();
                var heap = runtime.Heap;

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

        private Tuple<int[], int[]> GetArraySizes(out string error)
        {
            error = null;
            try
            {
                Tuple<int[], int[]> arySizes = null;
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
                int[] instIndices = GetTypeAllInstanceIndices(typeId);
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
                return new KeyValuePair<ulong, ulong>(totalBaseSize, totalSize);
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return new KeyValuePair<ulong, ulong>(0UL, 0UL);
            }
        }

        //public Tuple<ulong, KeyValuePair<uint, ulong>[]> GetTypeBaseSizes(int typeId, out string error)
        //{
        //	error = null;
        //	try
        //	{
        //		uint[] sizes = null;
        //		if (_baseSizes == null || !_baseSizes.TryGetTarget(out sizes))
        //		{
        //			sizes = Utils.ReadUintArray(_fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapInstanceBaseSizesFilePostfix),
        //				out error);
        //			if (sizes == null) return null;
        //			if (_baseSizes == null)
        //				_baseSizes = new WeakReference<uint[]>(sizes);
        //			else
        //				_baseSizes.SetTarget(sizes);
        //		}

        //		int[] instIndices = GetTypeInstanceIndices(typeId);
        //		ulong totalSize = 0UL;
        //		var sizeInfos = new KeyValuePair<uint, ulong>[instIndices.Length];
        //		for (int i = 0, icnt = instIndices.Length; i < icnt; ++i)
        //		{
        //			var ndx = instIndices[i];
        //			var sz = sizes[ndx];
        //			totalSize += sz;
        //			sizeInfos[i] = new KeyValuePair<uint, ulong>(sz, _instances[ndx]);
        //		}
        //		return new Tuple<ulong, KeyValuePair<uint, ulong>[]>(totalSize, sizeInfos);
        //	}
        //	catch (Exception ex)
        //	{
        //		error = Utils.GetExceptionErrorString(ex);
        //		return null;
        //	}
        //}

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
            KeyValuePair<string, string>[] fieldDefValues = null;
            int[] counts = null;
            ClrType[] fldTypes = null;
            TypeKind[] fldKinds = null;
            ClrInstanceField[] fields = null;
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
                        fieldDefValues[f] = new KeyValuePair<string, string>(clrType.Fields[f].Name, defValue);
                        fields[f] = clrType.Fields[f];
                        fldKinds[f] = Types.typeKind(fldType);
                    }
                    break;
                }
            }

            var minDt = new DateTime(1800, 1, 1);

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
                                    var dec = ValueExtractor.GetDecimal(addr, fields[j], intrnl);
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
                                    var ts = ValueExtractor.GetTimeSpan(addr, fields[j], intrnl);
                                    if (ts.TotalMilliseconds == 0.0)
                                    {
                                        counts[j] = counts[j] + 1;
                                        ++totalDefValues;
                                    }
                                    break;
                                case TypeKind.Guid:
                                    if (ValueExtractor.IsGuidEmpty(addr, fields[j], intrnl))
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
                int per = (int)Math.Round(((double)counts[i] * 100.0) / (double)addresses.Length);
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
            var totPercent = (int)Math.Round(((double)totalDefValues * 100.0) / (double)(addresses.Length * fldCount));
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
            var heap = runtime.Heap;
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
                    return ClrtDump.GetTypeSizesInfo(dump.Runtime.Heap, out error);
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
            Debug.Assert(id >= _threads.Length);
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
            if (blk.BlkReason != BlockingReason.None)
                return Utils.BaseTypeName(GetTypeName(blk.TypeId)) + "/" + blk.BlkReason;
            return Utils.BaseTypeName(GetTypeName(blk.TypeId));
        }

        public Tuple<ClrtThread[], string[], KeyValuePair<int, ulong>[]> GetThreads(out string error)
        {
            error = null;
            try
            {
                var path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.TxtThreadFrameDescriptionFilePostfix);
                var frameDescrs = Utils.GetStringListFromFile(path, out error);
                if (error != null) return null;
                path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapThreadFramesFilePostfix);
                var stackVars = Utils.ReadKvIntUInt64Array(path, out error);

                return new Tuple<ClrtThread[], string[], KeyValuePair<int, ulong>[]>(_threads, frameDescrs, stackVars);
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
        }

        public KeyValuePair<string[], string[]> GetThreadStackVarsStrings(int[] alive, int[] dead, KeyValuePair<int, ulong>[] stackVars)
        {
            var aliveStrs = new string[alive.Length];
            for (int i = 0, icnt = alive.Length; i < icnt; ++i)
            {
                var kv = stackVars[alive[i]];
                var addrStr = Utils.RealAddressStringHeader(kv.Value);
                var type = GetTypeName(kv.Key);
                aliveStrs[i] = addrStr + type;
            }
            var deadStrs = new string[dead.Length];
            for (int i = 0, icnt = dead.Length; i < icnt; ++i)
            {
                var kv = stackVars[dead[i]];
                var addrStr = Utils.RealAddressStringHeader(kv.Value);
                var type = GetTypeName(kv.Key);
                deadStrs[i] = addrStr + type;
            }
            return new KeyValuePair<string[], string[]>(aliveStrs, deadStrs);
        }

        #endregion threads/blocking objects

        #region dump

        private bool InitDump(out string error, IProgress<string> progress)
        {
            error = null;
            try
            {
                _clrtDump = new ClrtDump(DumpPath);
                if (_clrtDump.Init(out error))
                {
                    _clrtDump.WarmupHeap();
                    return true;
                }
                return false;
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

        public void Dispose(bool disposing)
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
