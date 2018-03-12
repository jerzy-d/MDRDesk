//#define USE_REFBUILDER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Threading;
using ClrMDRUtil.Utils;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
    public sealed class DumpIndexer
    {
        [Flags]
        public enum IndexingArguments
        {
            All = 0xFFFFFFF,
            JustInstanceRefs = 1,
        }

        /// <summary>
        /// Files and directories helper.
        /// </summary>
        private DumpFileMoniker _fileMoniker;
        public string AdhocFolder => _fileMoniker.OutputFolder;
        public string OutputFolder => AdhocFolder;
        public string IndexFolder => _fileMoniker.MapFolder;
        public string DumpPath => _fileMoniker.Path;
        public string DumpFileName => _fileMoniker.FileName;

        private int[] _instanceCount;
        private int[] _typeCount;
        private int[] _finalizerCount;
        private int[] _rootCount;

        /// <summary>
        /// Indexing errors, they all are written to the error file in the index folder.
        /// </summary>
        private ConcurrentBag<string>[] _errors;

        /// <summary>
        /// List of the unique string ids.
        /// </summary>
        private StringIdDct[] _stringIdDcts;

        /// <summary>
        /// Index of a runtime currently proccessed.
        /// </summary>
        private int _currentRuntimeIndex;

        /// <summary>
        /// The only ctor, it needs path to a crash dump.
        /// </summary>
        public DumpIndexer(string dmpPath)
        {
            _fileMoniker = new DumpFileMoniker(dmpPath);
        }

        public bool CreateDumpIndex2(Version version, IProgress<string> progress, out string indexPath, out string error)
        {
            error = null;
            indexPath = _fileMoniker.MapFolder;
            var clrtDump = new ClrtDump(DumpPath);
            if (!clrtDump.Init(out error)) return false;
            _errors = new ConcurrentBag<string>[clrtDump.RuntimeCount];
            DateTime indexingStart = DateTime.UtcNow;
            InstanceReferences builder = null;

            using (clrtDump)
            {
                try
                {
                    if (DumpFileMoniker.GetAndCreateMapFolders(DumpPath, out error) == null) return false;
                    indexPath = IndexFolder;

                    // indexing
                    //
                    if (!GetPrerequisites(clrtDump, progress, out _stringIdDcts, out error)) return false;
                    if (!GetTargetModuleInfos(clrtDump, progress, out error)) return false;

                    _instanceCount = new int[clrtDump.RuntimeCount];
                    _typeCount = new int[clrtDump.RuntimeCount];
                    _finalizerCount = new int[clrtDump.RuntimeCount];
                    _rootCount = new int[clrtDump.RuntimeCount];

                    for (int r = 0, rcnt = clrtDump.RuntimeCount; r < rcnt; ++r)
                    {
                        _currentRuntimeIndex = r;
                        string runtimeIndexHeader = Utils.RuntimeStringHeader(r);
                        clrtDump.SetRuntime(r);
                        ClrRuntime runtime = clrtDump.Runtime;
                        ClrHeap heap = runtime.Heap;
                        ConcurrentBag<string> errors = new ConcurrentBag<string>();
                        _errors[r] = errors;
                        var strIds = _stringIdDcts[r];

                        string[] typeNames = null;
                        ulong[] addresses = null;
                        int[] typeIds = null;

                        // get heap address count
                        //
                        progress?.Report(runtimeIndexHeader + "Getting instance count...");
                        var addrCount = DumpIndexer.GetHeapAddressCount(heap);
                        progress?.Report(runtimeIndexHeader + "Instance count: " + Utils.CountString(addrCount));
                        _instanceCount[r] = addrCount; // for general info dump

                        // get type names
                        //
                        progress?.Report(runtimeIndexHeader + "Getting type names...");
                        typeNames = DumpIndexer.GetTypeNames(heap, out error);
                        Debug.Assert(error == null);
                        _typeCount[r] = typeNames.Length; // for general info dump

                        // get roots
                        //
                        progress?.Report(runtimeIndexHeader + "Getting roots...");
                        (ulong[] rootObjectAddrs, ulong[] finalizerAddrs) = ClrtRootInfo.SetupRootAddresses(r, heap, typeNames, strIds, _fileMoniker, out error);
                        Debug.Assert(error == null);
                        // for general info dump
                        _finalizerCount[r] = finalizerAddrs.Length;
                        _rootCount[r] = rootObjectAddrs.Length;

                        // get addresses and types
                        //
                        progress?.Report(runtimeIndexHeader + "Getting addresses and types...");

                        addresses = new ulong[addrCount];
                        typeIds = new int[addrCount];
                        if (!GetAddressesAndTypes(heap, addresses, typeIds, typeNames, out error))
                        {
                            return false;
                        }
                        Debug.Assert(Utils.AreAddressesSorted(addresses));

                        // threads and blocking objects
                        //
                        progress?.Report(runtimeIndexHeader + "Getting threads, blocking objecks information...");
                        GetThreadsInfos2(clrtDump.Runtime, addresses, typeIds, typeNames, progress);

                        // setting root information
                        //
                        Debug.Assert(Utils.AreAddressesSorted(addresses));
                        progress?.Report(runtimeIndexHeader + "Setting root information...");
                        for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
                        {
                            ulong addr = addresses[i];
                            var isroot = Utils.AddressSearch(rootObjectAddrs, addr) >= 0;
                            var isflnz = Utils.AddressSearch(finalizerAddrs, addr) >= 0;
                            if (!isroot && !isflnz) continue;
                            if (isroot && isflnz)
                            {
                                addresses[i] = addr | Utils.RootBits.Root | Utils.RootBits.Rooted | Utils.RootBits.Finalizer;
                                continue;
                            }
                            if (isroot)
                            {
                                addresses[i] = addr | Utils.RootBits.Root | Utils.RootBits.Rooted;
                                continue;
                            }
                            addresses[i] = addr | Utils.RootBits.Finalizer;
                        }
                        rootObjectAddrs = null;
                        finalizerAddrs = null;

                        // field dependencies
                        //
                        if (!Setup.SkipReferences)
                        {
#if FALSE
                            progress?.Report(runtimeIndexHeader + "Creating instance reference data...");
                            Scullion bld = new Scullion(addressesCopy,
								_fileMoniker.GetFilePath(r, Constants.MapRefFwdDataFilePostfix),
								_fileMoniker.GetFilePath(r, Constants.MapRefFwdOffsetsFilePostfix),
								_fileMoniker.GetFilePath(r, Constants.MapFwdRefsFilePostfix),
								_fileMoniker.GetFilePath(r, Constants.MapRefBwdOffsetsFilePostfix),
								_fileMoniker.GetFilePath(r, Constants.MapBwdRefsFilePostfix),
                                _fileMoniker.GetFilePath(r, Constants.MapInstancesFilePostfix),
                            progress);
                            addressesCopy = null;
                            progress?.Report(runtimeIndexHeader + "Dumping forward instance reference heap data...");
                            bld.CreateForwardReferences(heap, out error);

                            progress?.Report(runtimeIndexHeader + "Starting instance reference builder...");


                            referenceBuilderWorker = new Thread(new ThreadStart(bld.BuildReferences));
                            referenceBuilderWorker.Start();
#else
                            builder = new InstanceReferences(addresses,
                                                                    new string[]
                                                                    {
                                                                    _fileMoniker.GetFilePath(r, Constants.MapRefFwdOffsetsFilePostfix),
                                                                    _fileMoniker.GetFilePath(r, Constants.MapFwdRefsFilePostfix),
                                                                    _fileMoniker.GetFilePath(r, Constants.MapRefBwdOffsetsFilePostfix),
                                                                    _fileMoniker.GetFilePath(r, Constants.MapBwdRefsFilePostfix),
                                                                    },
                                                                    progress,
                                                                    _fileMoniker.GetFilePath(r, Constants.MapInstancesFilePostfix)
                                                                    );
                            builder.CreateForwardReferences(heap, out error);
                            if (error == null)
                            {
                                if (!builder.BuildReveresedReferences())
                                {
                                    AddError(r, "CreateBackwardReferences failed." + Environment.NewLine + builder.Error);
                                }
                            }
                            else
                            {
                                AddError(r, "CreateForwardReferences failed." + Environment.NewLine + error);
                            }
#endif
                        }
                        else
                        {
                            progress?.Report(runtimeIndexHeader + "Skipping generation of instance references...");
                            InstanceReferences.DeleteInstanceReferenceFiles(r, _fileMoniker, out error);
                            progress?.Report(runtimeIndexHeader + "Savings instances addresses...");
                            Utils.WriteUlongArray(_fileMoniker.GetFilePath(r, Constants.MapInstancesFilePostfix), addresses, out error);
                        }

                        progress?.Report(runtimeIndexHeader + "Building type instance map...");
                        if (!BuildTypeInstanceMap(r, typeIds, out error))
                        {
                            AddError(r, "BuildTypeInstanceMap failed." + Environment.NewLine + error);
                            return false;
                        }

                        // save index data
                        //
                        progress?.Report(runtimeIndexHeader + "Saving data...");

                        var path = _fileMoniker.GetFilePath(r, Constants.MapInstanceTypesFilePostfix);
                        Utils.WriteIntArray(path, typeIds, out error);
                        // save type names
                        path = _fileMoniker.GetFilePath(r, Constants.TxtTypeNamesFilePostfix);
                        Utils.WriteStringList(path, typeNames, out error);
                        {
                            var reversedNames = Utils.ReverseTypeNames(typeNames);
                            path = _fileMoniker.GetFilePath(r, Constants.TxtReversedTypeNamesFilePostfix);
                            Utils.WriteStringList(path, reversedNames, out error);
                        }
                        // save string ids
                        //
                        path = _fileMoniker.GetFilePath(r, Constants.TxtCommonStringIdsPostfix);
                        if (!strIds.DumpInIdOrder(path, out error))
                        {
                            AddError(_currentRuntimeIndex, "StringIdDct.DumpInIdOrder failed." + Environment.NewLine + error);
                        }

                        error = builder?.Error;
                        if (error != null) AddError(_currentRuntimeIndex, "InstanceReferences building failed." + Environment.NewLine + error);
 
                        runtime.Flush();
                        heap = null;
                        progress?.Report(runtimeIndexHeader + "Runtime indexing done...");
                    }

                    var durationStr = Utils.DurationString(DateTime.UtcNow - indexingStart);
                    DumpIndexInfo(version, clrtDump, durationStr);
                    progress?.Report("Indexing done, total duration: " + durationStr);
                    return true;
                }
                catch (Exception ex)
                {
                    error = Utils.GetExceptionErrorString(ex);
                    AddError(_currentRuntimeIndex, "Exception in CreateDumpIndex." + Environment.NewLine + error);
                    return false;
                }
                finally
                {
                    builder?.Dispose();
                    DumpErrors();
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect();
                }
            }
        }

        public bool CreateDumpIndex3(Version version, IProgress<string> progress, out string indexPath, out string error)
        {
            error = null;
            indexPath = _fileMoniker.MapFolder;
            var clrtDump = new ClrtDump(DumpPath);
            if (!clrtDump.Init(out error)) return false;
            _errors = new ConcurrentBag<string>[clrtDump.RuntimeCount];
            DateTime indexingStart = DateTime.UtcNow;
            InstanceReferences builder = null;

            using (clrtDump)
            {
                try
                {
                    if (DumpFileMoniker.GetAndCreateMapFolders(DumpPath, out error) == null) return false;
                    indexPath = IndexFolder;

                    // indexing
                    //
                    if (!GetPrerequisites(clrtDump, progress, out _stringIdDcts, out error)) return false;
                    if (!GetTargetModuleInfos(clrtDump, progress, out error)) return false;

                    _instanceCount = new int[clrtDump.RuntimeCount];
                    _typeCount = new int[clrtDump.RuntimeCount];
                    _finalizerCount = new int[clrtDump.RuntimeCount];
                    _rootCount = new int[clrtDump.RuntimeCount];

                    for (int r = 0, rcnt = clrtDump.RuntimeCount; r < rcnt; ++r)
                    {
                        _currentRuntimeIndex = r;
                        string runtimeIndexHeader = Utils.RuntimeStringHeader(r);
                        clrtDump.SetRuntime(r);
                        ClrRuntime runtime = clrtDump.Runtime;
                        ClrHeap heap = runtime.Heap;
                        ConcurrentBag<string> errors = new ConcurrentBag<string>();
                        _errors[r] = errors;
                        var strIds = _stringIdDcts[r];

                        string[] typeNames = null;
                        ulong[] addresses = null;
                        int[] typeIds = null;

                        // get heap address count
                        //
                        progress?.Report(runtimeIndexHeader + "Getting instance count...");
                        var addrCount = DumpIndexer.GetHeapAddressCount(heap);
                        progress?.Report(runtimeIndexHeader + "Instance count: " + Utils.CountString(addrCount));
                        _instanceCount[r] = addrCount; // for general info dump

                        // get type names
                        //
                        progress?.Report(runtimeIndexHeader + "Getting type names...");
                        typeNames = DumpIndexer.GetTypeNames(heap, out error);
                        Debug.Assert(error == null);
                        _typeCount[r] = typeNames.Length; // for general info dump

                        // get roots
                        //
                        progress?.Report(runtimeIndexHeader + "Getting roots...");
                        (ulong[] rootObjectAddrs, ulong[] finalizerAddrs) = ClrtRootInfo.SetupRootAddresses(r, heap, typeNames, strIds, _fileMoniker, out error);
                        Debug.Assert(error == null);
                        // for general info dump
                        _finalizerCount[r] = finalizerAddrs.Length;
                        _rootCount[r] = rootObjectAddrs.Length;

                        // get addresses and types
                        //
                        progress?.Report(runtimeIndexHeader + "Getting addresses and types...");
#if USE_REFBUILDER
#else
                        addresses = new ulong[addrCount];
                        typeIds = new int[addrCount];
                        if (!GetAddressesAndTypes(heap, addresses, typeIds, typeNames, out error))
                        {
                            return false;
                        }
#endif
                        Debug.Assert(Utils.AreAddressesSorted(addresses));

                        // threads and blocking objects
                        //
                        progress?.Report(runtimeIndexHeader + "Getting threads, blocking objecks information...");
                        GetThreadsInfos2(clrtDump.Runtime, addresses, typeIds, typeNames, progress);

                        // setting root information
                        //
                        Debug.Assert(Utils.AreAddressesSorted(addresses));
                        progress?.Report(runtimeIndexHeader + "Setting root information...");
                        for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
                        {
                            ulong addr = addresses[i];
                            var isroot = Utils.AddressSearch(rootObjectAddrs, addr) >= 0;
                            var isflnz = Utils.AddressSearch(finalizerAddrs, addr) >= 0;
                            if (!isroot && !isflnz) continue;
                            if (isroot && isflnz)
                            {
                                addresses[i] = addr | Utils.RootBits.Root | Utils.RootBits.Rooted | Utils.RootBits.Finalizer;
                                continue;
                            }
                            if (isroot)
                            {
                                addresses[i] = addr | Utils.RootBits.Root | Utils.RootBits.Rooted;
                                continue;
                            }
                            addresses[i] = addr | Utils.RootBits.Finalizer;
                        }
                        rootObjectAddrs = null;
                        finalizerAddrs = null;

                        // field dependencies
                        //
                        if (!Setup.SkipReferences)
                        {
#if FALSE
                            progress?.Report(runtimeIndexHeader + "Creating instance reference data...");
                            Scullion bld = new Scullion(addressesCopy,
								_fileMoniker.GetFilePath(r, Constants.MapRefFwdDataFilePostfix),
								_fileMoniker.GetFilePath(r, Constants.MapRefFwdOffsetsFilePostfix),
								_fileMoniker.GetFilePath(r, Constants.MapFwdRefsFilePostfix),
								_fileMoniker.GetFilePath(r, Constants.MapRefBwdOffsetsFilePostfix),
								_fileMoniker.GetFilePath(r, Constants.MapBwdRefsFilePostfix),
                                _fileMoniker.GetFilePath(r, Constants.MapInstancesFilePostfix),
                            progress);
                            addressesCopy = null;
                            progress?.Report(runtimeIndexHeader + "Dumping forward instance reference heap data...");
                            bld.CreateForwardReferences(heap, out error);

                            progress?.Report(runtimeIndexHeader + "Starting instance reference builder...");


                            referenceBuilderWorker = new Thread(new ThreadStart(bld.BuildReferences));
                            referenceBuilderWorker.Start();
#else
                            builder = new InstanceReferences(addresses,
                                                                    new string[]
                                                                    {
                                                                    _fileMoniker.GetFilePath(r, Constants.MapRefFwdOffsetsFilePostfix),
                                                                    _fileMoniker.GetFilePath(r, Constants.MapFwdRefsFilePostfix),
                                                                    _fileMoniker.GetFilePath(r, Constants.MapRefBwdOffsetsFilePostfix),
                                                                    _fileMoniker.GetFilePath(r, Constants.MapBwdRefsFilePostfix),
                                                                    },
                                                                    progress,
                                                                    _fileMoniker.GetFilePath(r, Constants.MapInstancesFilePostfix)
                                                                    );
                            builder.CreateForwardReferences(heap, out error);
                            if (error == null)
                            {
                                if (!builder.BuildReveresedReferences())
                                {
                                    AddError(r, "CreateBackwardReferences failed." + Environment.NewLine + builder.Error);
                                }
                            }
                            else
                            {
                                AddError(r, "CreateForwardReferences failed." + Environment.NewLine + error);
                            }
#endif
                        }
                        else
                        {
                            progress?.Report(runtimeIndexHeader + "Skipping generation of instance references...");
                            InstanceReferences.DeleteInstanceReferenceFiles(r, _fileMoniker, out error);
                            progress?.Report(runtimeIndexHeader + "Savings instances addresses...");
                            Utils.WriteUlongArray(_fileMoniker.GetFilePath(r, Constants.MapInstancesFilePostfix), addresses, out error);
                        }

                        progress?.Report(runtimeIndexHeader + "Building type instance map...");
                        if (!BuildTypeInstanceMap(r, typeIds, out error))
                        {
                            AddError(r, "BuildTypeInstanceMap failed." + Environment.NewLine + error);
                            return false;
                        }

                        // save index data
                        //
                        progress?.Report(runtimeIndexHeader + "Saving data...");

                        var path = _fileMoniker.GetFilePath(r, Constants.MapInstanceTypesFilePostfix);
                        Utils.WriteIntArray(path, typeIds, out error);
                        // save type names
                        path = _fileMoniker.GetFilePath(r, Constants.TxtTypeNamesFilePostfix);
                        Utils.WriteStringList(path, typeNames, out error);
                        {
                            var reversedNames = Utils.ReverseTypeNames(typeNames);
                            path = _fileMoniker.GetFilePath(r, Constants.TxtReversedTypeNamesFilePostfix);
                            Utils.WriteStringList(path, reversedNames, out error);
                        }
                        // save string ids
                        //
                        path = _fileMoniker.GetFilePath(r, Constants.TxtCommonStringIdsPostfix);
                        if (!strIds.DumpInIdOrder(path, out error))
                        {
                            AddError(_currentRuntimeIndex, "StringIdDct.DumpInIdOrder failed." + Environment.NewLine + error);
                        }

                        error = builder?.Error;
                        if (error != null) AddError(_currentRuntimeIndex, "InstanceReferences building failed." + Environment.NewLine + error);

                        runtime.Flush();
                        heap = null;
                        progress?.Report(runtimeIndexHeader + "Runtime indexing done...");
                    }

                    var durationStr = Utils.DurationString(DateTime.UtcNow - indexingStart);
                    DumpIndexInfo(version, clrtDump, durationStr);
                    progress?.Report("Indexing done, total duration: " + durationStr);
                    return true;
                }
                catch (Exception ex)
                {
                    error = Utils.GetExceptionErrorString(ex);
                    AddError(_currentRuntimeIndex, "Exception in CreateDumpIndex." + Environment.NewLine + error);
                    return false;
                }
                finally
                {
                    builder?.Dispose();
                    DumpErrors();
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect();
                }
            }
        }

        /// <summary>
        /// Save instances by type lookup.
        /// </summary>
        /// <param name="rtNdx">Current runtime index.</param>
        /// <param name="typeIds">Big array of type ids correspondig to instances.</param>
        /// <param name="error">Error message, out.</param>
        /// <returns>True if no exception.</returns>
        private bool BuildTypeInstanceMap(int rtNdx, int[] typeIds, out string error)
        {
            error = null;
            BinaryWriter brOffsets = null;
            try
            {
                brOffsets = new BinaryWriter(File.Open(_fileMoniker.GetFilePath(rtNdx, Constants.MapTypeInstanceOffsetsFilePostfix), FileMode.Create));

                Utils.ForceGcWithCompaction(); // x86 version gives me out of memory exception here

                var indices = Utils.Iota(typeIds.Length);
                var ids = new int[typeIds.Length];
                Array.Copy(typeIds, 0, ids, 0, typeIds.Length);
                Array.Sort(ids, indices);
                brOffsets.Write((int)0);
                int prev = ids[0];
                brOffsets.Write(prev);
                brOffsets.Write((int)0);
                int usedTypeCnt = 1;
                for (int i = 1, icnt = ids.Length; i < icnt; ++i)
                {
                    int cur = ids[i];
                    if (cur != prev)
                    {
                        brOffsets.Write(cur);
                        brOffsets.Write(i);
                        prev = cur;
                        ++usedTypeCnt;
                    }
                }
                brOffsets.Write(int.MaxValue);
                brOffsets.Write(ids.Length);
                ++usedTypeCnt;
                brOffsets.Seek(0, SeekOrigin.Begin);
                brOffsets.Write(usedTypeCnt);
                Utils.CloseStream(ref brOffsets);

                Utils.WriteIntArray(_fileMoniker.GetFilePath(rtNdx, Constants.MapTypeInstanceMapFilePostfix), indices, out error);

                return error == null;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return false;
            }
            finally
            {
                brOffsets?.Close();
            }
        }

        public static int GetHeapAddressCount(ClrHeap heap)
        {
            int count = 0;
            var segs = heap.Segments;
            for (int i = 0, icnt = segs.Count; i < icnt; ++i)
            {
                var seg = segs[i];
                ulong addr = seg.FirstObject;
                while (addr != 0ul)
                {
                    ++count;
                    addr = seg.NextObject(addr);
                }
            }
            return count;
        }

        public static ulong[] GetHeapAddressesCount(ClrHeap heap)
        {
            int count = 0;
            var segs = heap.Segments;
            for (int i = 0, icnt = segs.Count; i < icnt; ++i)
            {
                var seg = segs[i];
                ulong addr = seg.FirstObject;
                while (addr != 0ul)
                {
                    ++count;
                    addr = seg.NextObject(addr);
                }
            }
            ulong[] ary = new ulong[count];
            int aryNdx = 0;
            for (int i = 0, icnt = segs.Count; i < icnt; ++i)
            {
                var seg = segs[i];
                ulong addr = seg.FirstObject;
                while (addr != 0ul)
                {
                    ary[aryNdx++] = addr;
                    addr = seg.NextObject(addr);
                }
            }

            return ary;
        }

        public static Instances GetHeapAddresses(ClrHeap heap, out ClrtSegment[] segments, out SortedDictionary<string, List<ClrType>> typeDct)
        {
            typeDct = new SortedDictionary<string, List<ClrType>>();
            int count = 0;
            ulong pointerSize = (ulong)heap.PointerSize;
            var segs = heap.Segments;
            segments = new ClrtSegment[segs.Count];
            ulong[][] segAddrs = new ulong[segs.Count][];
            List<ulong> segAddrLst = new List<ulong>(1024 * 1024 * 10);
            bool canWalkHeap = heap.CanWalkHeap;
            if (canWalkHeap)
            {
                for (int i = 0, icnt = segs.Count; i < icnt; ++i)
                {
                    var seg = segs[i];
                    ulong addr = seg.FirstObject;
                    ulong end = seg.CommittedEnd;
                    ulong segFirst = addr;
                    ulong segLast = addr;
                    int segFirstInst = count;
                    int segLastInst = count;
                    if (addr == 0) ++count;
                    bool firstbad = addr == 0 ? true : false;
                    int badcount = (addr == 0) ? 1 : 0;
                    if (addr != 0)
                    {
                        ClrType tp = heap.GetObjectType(addr);
                        segAddrLst.Add(addr);
                        AddClrType(typeDct, tp);
                    }

                    while (addr != 0ul)
                    {
                        ClrType tp;
                        addr = seg.NextObject(addr, out tp);
                        if (addr != 0)
                        {
                            segAddrLst.Add(addr);
                            segLastInst = count;
                            segLast = addr;
                            ++count;
                            AddClrType(typeDct, tp);
                        }
                    }
                    segments[i] = new ClrtSegment(seg, segFirst, segLast, segFirstInst, segLastInst);
                    segAddrs[i] = segAddrLst.ToArray();
                    segAddrLst.Clear();
                }

            }
            else
            {
                for (int i = 0, icnt = segs.Count; i < icnt; ++i)
                {
                    var seg = segs[i];

                    ulong first = seg.FirstObject;
                    ulong end = seg.CommittedEnd;
                    ulong addr = first;
                    addr = TryFindNextValidAddress(heap, addr, end, pointerSize);
                    // save some segment info
                    ulong segFirst = addr;
                    ulong segLast = addr;
                    int segFirstInst = count;
                    int segLastInst = count;
                    if (addr == 0) ++count;
                    bool firstbad = addr == 0 ? true : false;
                    int badcount = (addr == 0) ? 1 : 0;
                    if (addr != 0) segAddrLst.Add(addr);

                    while (addr != 0ul)
                    {
                        var newAddr = seg.NextObject(addr);
                        if (newAddr == 0)
                        {
                            newAddr = TryFindNextValidAddress(heap, addr + pointerSize, end, pointerSize);
                        }
                        addr = newAddr;
                        if (addr != 0)
                        {
                            segAddrLst.Add(addr);
                            segLastInst = count;
                            segLast = addr;
                            ++count;
                         }
                    }
                    segments[i] = new ClrtSegment(seg, segFirst, segLast, segFirstInst, segLastInst);
                    segAddrs[i] = segAddrLst.ToArray();
                    segAddrLst.Clear();
                }
            }
            segAddrLst = null;
            return new Instances(segAddrs);
        }

        private static void AddClrType(SortedDictionary<string, List<ClrType>> typeDc, ClrType tp)
        {
            string tpName = tp.Name;
            List<ClrType> lst;
            if (typeDc.TryGetValue(tpName,out lst))
            {
                bool found = false;
                for (int i = 0, icnt = lst.Count; i < icnt; ++i)
                {
                    if (tp == lst[i]) { found = true; break; }
                }
                if (!found) lst.Add(tp);
            }
            else
            {
                typeDc.Add(tpName, new List<ClrType>() { tp });
            }
        }



        public static Instances GetHeapAddresses(ClrHeap heap, out ClrtSegment[] segments)
        {
            int count = 0;
            ulong pointerSize = (ulong)heap.PointerSize;
            var segs = heap.Segments;
            segments = new ClrtSegment[segs.Count];
            ulong[][] segAddrs = new ulong[segs.Count][];
            List<ulong> segAddrLst = new List<ulong>(1024 * 1024 * 10);
            bool canWalkHeap = heap.CanWalkHeap;
            if (canWalkHeap)
            {
                for (int i = 0, icnt = segs.Count; i < icnt; ++i)
                {
                    var seg = segs[i];
                    ulong addr = seg.FirstObject;
                    ulong end = seg.CommittedEnd;
                    ulong segFirst = addr;
                    ulong segLast = addr;
                    int segFirstInst = count;
                    int segLastInst = count;
                    if (addr == 0) ++count;
                    bool firstbad = addr == 0 ? true : false;
                    int badcount = (addr == 0) ? 1 : 0;
                    if (addr != 0) segAddrLst.Add(addr);

                    while (addr != 0ul)
                    {
                        addr = seg.NextObject(addr);
                        if (addr != 0)
                        {
                            segAddrLst.Add(addr);
                            segLastInst = count;
                            segLast = addr;
                            ++count;
                        }
                    }
                    segments[i] = new ClrtSegment(seg, segFirst, segLast, segFirstInst, segLastInst);
                    segAddrs[i] = segAddrLst.ToArray();
                    segAddrLst.Clear();
                }

            }
            else
            {
                for (int i = 0, icnt = segs.Count; i < icnt; ++i)
                {
                    var seg = segs[i];

                    ulong first = seg.FirstObject;
                    ulong end = seg.CommittedEnd;
                    ulong addr = first;
                    addr = TryFindNextValidAddress(heap, addr, end, pointerSize);
                    // save some segment info
                    ulong segFirst = addr;
                    ulong segLast = addr;
                    int segFirstInst = count;
                    int segLastInst = count;
                    if (addr == 0) ++count;
                    bool firstbad = addr == 0 ? true : false;
                    int badcount = (addr == 0) ? 1 : 0;
                    if (addr != 0) segAddrLst.Add(addr);

                    while (addr != 0ul)
                    {
                        var newAddr = seg.NextObject(addr);
                        if (newAddr == 0)
                        {
                            newAddr = TryFindNextValidAddress(heap, addr + pointerSize, end, pointerSize);
                        }
                        addr = newAddr;
                        if (addr != 0)
                        {
                            segAddrLst.Add(addr);
                            segLastInst = count;
                            segLast = addr;
                            ++count;
                        }
                    }
                    segments[i] = new ClrtSegment(seg, segFirst, segLast, segFirstInst, segLastInst);
                    segAddrs[i] = segAddrLst.ToArray();
                    segAddrLst.Clear();
                }
            }
            segAddrLst = null;
            return new Instances(segAddrs);
        }

        private static ulong TryFindNextValidAddress(ClrHeap heap, ulong addr, ulong end, ulong pointerSize)
        {
            const int TryCount = 10000;
            var clrType = heap.GetObjectType(addr);
            if (clrType != null) return addr;
            int count = 0;
            while (clrType == null && ++count < TryCount)
            {
                addr += pointerSize;
                if (addr > end)
                {
                    return 0;
                }
                clrType = heap.GetObjectType(addr);
                if (clrType != null)
                {
                    return addr;
                }
            }
            return 0;
        }

#region type fields

        private void RetainFieldTypes(ClrType clrType, int typeId, HashSet<int> doneTypeFields, HashSet<int> tofixTypeFields, string[] typeNames, SortedDictionary<int, long[]> typeFields, StringIdDct idDct)
        {
            if (doneTypeFields.Add(typeId))
            {
                int fldCnt = clrType.Fields == null ? 0 : clrType.Fields.Count;
                if (fldCnt < 1) return;
                long[] fields = new long[fldCnt];
                bool fixit = false;
                for (int i = 0; i < fldCnt; ++i)
                {
                    ClrType fldType = clrType.Fields[i].Type;
                    if (fldType == null)
                    {
                        fixit = true;
                        fields[i] = Constants.InvalidIndex | (Constants.InvalidIndex<<32);
                    }
                    else
                    {
                        int fldTypeId = Array.BinarySearch(typeNames, fldType.Name, StringComparer.Ordinal);
                        if (fldTypeId < 0)
                        {
                            fields[i] = Constants.InvalidIndex | (Constants.InvalidIndex << 32);
                            fixit = true;
                        }
                        else
                        {
                            int id = idDct.JustGetId(clrType.Fields[i].Name);
                            fields[i] = (long)fldTypeId | ((long)id << 32);
                        }
                    }
                }
                if (fixit)
                {
                    tofixTypeFields.Add(typeId);
                }
                else
                {
                    if (tofixTypeFields.Contains(typeId))
                        tofixTypeFields.Remove(typeId);
                }
                long[] old;
                if (typeFields.TryGetValue(typeId,out old))
                {
                    typeFields[typeId] = fields;
                }
                else
                {
                    typeFields.Add(typeId, fields);
                }
            }
        }

        private void SaveFieldTypes(SortedDictionary<int, long[]> typeFields)
        {
            var fieldTypeParents = new SortedDictionary<int, List<long>>();
            foreach(var kv in typeFields)
            {
                var flds = kv.Value;
                var parent = kv.Key;
                for (int i = 0, icnt = flds.Length; i < icnt; ++i)
                {
                    var fld = flds[i];
                    int fldTypeId = (int)(fld & 0x00000000FFFFFFFF);
                    int fldNameId = (int)(((ulong)fld & 0xFFFFFFFF00000000)>> 32);
                    long entry = (long)parent | (long)((ulong)fld & 0xFFFFFFFF00000000);
                    List<long> lst;
                    if (fieldTypeParents.TryGetValue(fldTypeId,out lst))
                    {
                        lst.Add(entry);
                    }
                    else
                    {
                        fieldTypeParents.Add(fldTypeId, new List<long>() { entry });
                    }
                }
            }
            var path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapTypeFieldTypesPostfix);
            string error;
            if (!IdReferences.SaveIndicesReferences(path, typeFields, out error))
            {
                _errors[_currentRuntimeIndex].Add("Dumping field types failed." + Environment.NewLine + error);
            }
            typeFields.Clear();
            path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapFieldTypeParentTypesPostfix);
            if (!IdReferences.SaveIndicesReferences(path, fieldTypeParents, out error))
            {
                _errors[_currentRuntimeIndex].Add("Dumping field parent types failed." + Environment.NewLine + error);
            }
            fieldTypeParents.Clear();
        }

#endregion type fields

        public bool GetAddressesAndTypes(ClrHeap heap, ulong[] addresses, int[] typeIds, string[] typeNames, out string error)
        {
            error = null;
            try
            {
                var segs = heap.Segments;
                ClrtSegment[] mysegs = new ClrtSegment[segs.Count];
                int addrNdx = 0;
                int segIndex = 0;
                uint[] sizes = new uint[addresses.Length];
                uint[] baseSizes = new uint[addresses.Length];
                int[] typeKinds = new int[typeNames.Length];
                var arraySizes = new List<ValueTuple<int, int,ClrElementKind>>(addresses.Length / 25);
                HashSet<int> doneTypeFields = new HashSet<int>();
                HashSet<int> tofixTypeFields = new HashSet<int>();
                var typeFields = new SortedDictionary<int, long[]>();

                for (int segNdx = 0, icnt = segs.Count; segNdx < icnt; ++segNdx)
                {
                    var seg = segs[segNdx];
                    var genCounts = new int[3];
                    var genSizes = new ulong[3];
                    var genFreeCounts = new int[3];
                    var genFreeSizes = new ulong[3];

                    ulong addr = seg.FirstObject;
                    ulong firstAddr = addr, lastAddr = addr;
                    while (addr != 0ul)
                    {
                        var clrType = heap.GetObjectType(addr);

                        var typeNameKey = clrType == null ? Constants.NullTypeName : clrType.Name;
                        int typeId = Array.BinarySearch(typeNames, typeNameKey, StringComparer.Ordinal);
                        typeIds[addrNdx] = typeId;
                        if (typeKinds[typeId] == 0) typeKinds[typeId] = (int)TypeExtractor.GetElementKind(clrType);
                        if (clrType == null) goto NEXT_OBJECT;
                        addresses[addrNdx] = addr;
                        var isFree = Utils.SameStrings(clrType.Name, Constants.FreeTypeName);
                        var baseSize = clrType.BaseSize;
                        baseSizes[addrNdx] = (uint)baseSize;
                        var size = clrType.GetSize(addr);
                        if (size > (ulong)UInt32.MaxValue) size = (ulong)UInt32.MaxValue;
                        sizes[addrNdx] = (uint)size;

                        RetainFieldTypes(clrType, typeId, doneTypeFields, tofixTypeFields, typeNames, typeFields, _stringIdDcts[_currentRuntimeIndex]);

                        // get generation stats
                        //
                        if (isFree)
                            ClrtSegment.SetGenerationStats(seg, addr, size, genFreeCounts, genFreeSizes);
                        else
                            ClrtSegment.SetGenerationStats(seg, addr, size, genCounts, genSizes);
                        if (clrType.IsArray)
                        {
                            int asz = clrType.GetArrayLength(addr);
                            ClrElementKind aryElKind = TypeExtractor.GetElementKind(clrType.ComponentType);
                            arraySizes.Add((addrNdx, asz, aryElKind));
                        }

                        NEXT_OBJECT:
                        lastAddr = addr;
                        addr = seg.NextObject(addr);
                        ++addrNdx;
                    }
                    // set segment info
                    //
                    mysegs[segNdx] = new ClrtSegment(heap.Segments[segNdx], firstAddr, lastAddr, segIndex, addrNdx - 1);
                    segIndex = addrNdx;
                    mysegs[segNdx].SetGenerationStats(genCounts, genSizes, genFreeCounts, genFreeSizes);
                }

                // dump segments info
                //
                if (
                    !ClrtSegment.DumpSegments(_fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapSegmentInfoFilePostfix),
                        mysegs, out error))
                {
                    _errors[_currentRuntimeIndex].Add("DumpSegments failed." + Environment.NewLine + error);
                }

                SaveFieldTypes(typeFields);

                // dump sizes
                //
                if (!Utils.WriteUintArray(_fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapInstanceSizesFilePostfix), sizes, out error))
                {
                    _errors[_currentRuntimeIndex].Add("Dumping sizes failed." + Environment.NewLine + error);
                }
                if (!Utils.WriteUintArray(_fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapInstanceBaseSizesFilePostfix), baseSizes, out error))
                {
                    _errors[_currentRuntimeIndex].Add("Dumping base sizes failed." + Environment.NewLine + error);
                }
                if (!Utils.WriteIntArray(_fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapKindsFilePostfix), typeKinds, out error))
                {
                    _errors[_currentRuntimeIndex].Add("Dumping type kinds failed." + Environment.NewLine + error);
                }
                if (!Utils.WriteArrayInfos(_fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapArraySizesFilePostfix), arraySizes, out error))
                {
                    _errors[_currentRuntimeIndex].Add("Dumping array sizes failed." + Environment.NewLine + error);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return false;
            }
        }

        string GetIndexingDurationString(Stopwatch sw, TimeSpan prevTimeSpan, out TimeSpan newPrevTimespan)
        {
            newPrevTimespan = sw.Elapsed;
            var curDiff = newPrevTimespan - prevTimeSpan;
            return Utils.DurationString(curDiff) + "/" + Utils.DurationString(newPrevTimespan);
        }

        //private void GetThreadsInfos(object param)
        //{
        //    string error;
        //    var parameters = param as Tuple<string, ulong[], int[], string[]>;
        //    Debug.Assert(parameters != null);
        //    var clrtDump = new ClrtDump(parameters.Item1);
        //    if (!clrtDump.Init(out error))
        //    {
        //        _errors[_currentRuntimeIndex].Add("GetThreadsInfos failed." + Environment.NewLine + error);
        //        return;
        //    }
        //    var instances = parameters.Item2;
        //    var typeIds = parameters.Item3;
        //    var typeNames = parameters.Item4;

        //    BinaryWriter bw = null;
        //    StreamWriter sw = null;
        //    try
        //    {
        //        var heap = clrtDump.Runtimes[_currentRuntimeIndex].Heap;
        //        var threads = DumpIndexer.GetThreads(clrtDump.Runtimes[_currentRuntimeIndex]);
        //        var blocks = DumpIndexer.GetBlockingObjects(heap);


        //        var threadSet = new HashSet<ClrThread>(new ClrThreadEqualityCmp());
        //        var blkGraph = new List<Tuple<BlockingObject, ClrThread[], ClrThread[]>>();
        //        var allBlkList = new List<BlockingObject>();
        //        var owners = new List<ClrThread>();
        //        var waiters = new List<ClrThread>();

        //        for (int i = 0, icnt = blocks.Length; i < icnt; ++i)
        //        {
        //            var blk = blocks[i];
        //            owners.Clear();
        //            waiters.Clear();
        //            ClrThread owner = null;
        //            if (blk.Taken && blk.HasSingleOwner)
        //            {
        //                owner = blk.Owner;
        //                if (owner != null)
        //                {
        //                    threadSet.Add(owner);
        //                    owners.Add(owner);
        //                }
        //            }

        //            if (blk.Owners != null && blk.Owners.Count > 0)
        //            {
        //                for (int j = 0, jcnt = blk.Owners.Count; j < jcnt; ++j)
        //                {
        //                    var th = blk.Owners[j];
        //                    if (th != null)
        //                    {
        //                        threadSet.Add(th);
        //                        if (owner == null || owner.Address != th.Address)
        //                            owners.Add(th);
        //                    }
        //                }
        //            }

        //            if (blk.Waiters != null && blk.Waiters.Count > 0)
        //            {
        //                for (int j = 0, jcnt = blk.Waiters.Count; j < jcnt; ++j)
        //                {
        //                    var th = blk.Waiters[j];
        //                    if (th != null)
        //                    {
        //                        threadSet.Add(th);
        //                        waiters.Add(th);
        //                    }
        //                }
        //            }

        //            if (owners.Count > 0)
        //            {
        //                var ownerAry = owners.ToArray();
        //                var waiterAry = waiters.ToArray();
        //                blkGraph.Add(new Tuple<BlockingObject, ClrThread[], ClrThread[]>(blk, ownerAry, waiterAry));
        //                allBlkList.Add(blk);
        //            }
        //            else if (waiters.Count > 0)
        //            {
        //                blkGraph.Add(new Tuple<BlockingObject, ClrThread[], ClrThread[]>(blk, Utils.EmptyArray<ClrThread>.Value,
        //                    waiters.ToArray()));
        //                allBlkList.Add(blk);
        //            }
        //        }

        //        var thrCmp = new ClrThreadCmp();
        //        var blkCmp = new BlockingObjectCmp();
        //        var blkInfoCmp = new BlkObjInfoCmp();

        //        // blocks and threads found in blocking objects
        //        //
        //        var blkThreadAry = threadSet.ToArray();
        //        Array.Sort(blkThreadAry, thrCmp);
        //        var blkBlockAry = allBlkList.ToArray();
        //        Array.Sort(blkBlockAry, blkCmp);
        //        var threadBlocksAry = blkGraph.ToArray();
        //        Array.Sort(threadBlocksAry, blkInfoCmp);

        //        // create maps
        //        //
        //        int[] blkMap = new int[blkBlockAry.Length];
        //        int[] thrMap = new int[blkThreadAry.Length];
        //        for (int i = 0, icnt = blkMap.Length; i < icnt; ++i)
        //        {
        //            blkMap[i] = Array.BinarySearch(blocks, blkBlockAry[i], blkCmp);
        //            Debug.Assert(blkMap[i] >= 0);
        //        }

        //        for (int i = 0, icnt = thrMap.Length; i < icnt; ++i)
        //        {
        //            thrMap[i] = Array.BinarySearch(threads, blkThreadAry[i], thrCmp);
        //            Debug.Assert(thrMap[i] >= 0);
        //        }

        //        int blkThreadCount = blkThreadAry.Length;
        //        Digraph graph = new Digraph(blkThreadCount + blkBlockAry.Length);

        //        for (int i = 0, cnt = threadBlocksAry.Length; i < cnt; ++i)
        //        {
        //            var blkInfo = threadBlocksAry[i];
        //            for (int j = 0, tcnt = blkInfo.Item2.Length; j < tcnt; ++j)
        //            {
        //                var ndx = Array.BinarySearch(blkThreadAry, blkInfo.Item2[j], thrCmp);
        //                Debug.Assert(ndx >= 0);
        //                graph.AddDistinctEdge(blkThreadCount + i, ndx);
        //            }
        //            for (int j = 0, tcnt = blkInfo.Item3.Length; j < tcnt; ++j)
        //            {
        //                var ndx = Array.BinarySearch(blkThreadAry, blkInfo.Item3[j], thrCmp);
        //                Debug.Assert(ndx >= 0);
        //                graph.AddDistinctEdge(ndx, blkThreadCount + i);
        //            }
        //        }

        //        var cycle = new DirectedCycle(graph);
        //        var cycles = cycle.GetCycle();

        //        // save graph
        //        //
        //        var path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapThreadsAndBlocksGraphFilePostfix);
        //        bw = new BinaryWriter(File.Open(path, FileMode.Create));
        //        bw.Write(cycles.Length);
        //        for (int i = 0, icnt = cycles.Length; i < icnt; ++i)
        //        {
        //            bw.Write(cycles[i]);
        //        }
        //        bw.Write(thrMap.Length);
        //        for (int i = 0, icnt = thrMap.Length; i < icnt; ++i)
        //        {
        //            bw.Write(thrMap[i]);
        //        }
        //        bw.Write(blkMap.Length);
        //        for (int i = 0, icnt = blkMap.Length; i < icnt; ++i)
        //        {
        //            bw.Write(blkMap[i]);
        //        }
        //        graph.Dump(bw, out error);
        //        bw.Close();
        //        bw = null;
        //        if (error != null)
        //            AddError(_currentRuntimeIndex, "Exception in GetThreadsInfos." + Environment.NewLine + error);

        //        // get frames info
        //        //
        //        var frames = new StringIdDct();
        //        var stObjCmp = new Utils.KVIntUlongCmpAsc();
        //        var stackObject = new SortedDictionary<KeyValuePair<int, ulong>, int>(stObjCmp);
        //        var stackTraceLst = new List<ClrStackFrame>();
        //        var rootEqCmp = new ClrRootEqualityComparer();
        //        var frameIds = new List<int>();
        //        var frameStackPtrs = new List<ulong>();
        //        var aliveIds = new List<int>();
        //        var deadIds = new List<int>();

        //        // save threads and blocks, generate stack info
        //        //
        //        path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapThreadsAndBlocksFilePostfix);
        //        bw = new BinaryWriter(File.Open(path, FileMode.Create));
        //        bw.Write(threads.Length);
        //        for (int i = 0, icnt = threads.Length; i < icnt; ++i)
        //        {
        //            var thread = threads[i];
        //            stackTraceLst.Clear();
        //            foreach (var st in thread.EnumerateStackTrace())
        //            {
        //                stackTraceLst.Add(st);
        //                if (stackTraceLst.Count > 100) break;
        //            }

        //            var threadLocalAliveVars = thread.EnumerateStackObjects(false).ToArray();
        //            var all = thread.EnumerateStackObjects(true).ToArray();
        //            var threadLocalDeadVars = all.Except(threadLocalAliveVars, rootEqCmp).ToArray();
        //            var threadFrames = stackTraceLst.ToArray();

        //            aliveIds.Clear();
        //            for (int j = 0, jcnt = threadLocalAliveVars.Length; j < jcnt; ++j)
        //            {
        //                ClrRoot root = threadLocalAliveVars[j];
        //                ClrType clrType = heap.GetObjectType(root.Object);
        //                var typeName = clrType == null ? Constants.NullTypeName : clrType.Name;
        //                var typeId = Array.BinarySearch(typeNames, typeName, StringComparer.Ordinal);
        //                if (typeId < 0) typeId = Constants.InvalidIndex;
        //                int stackId;
        //                if (!stackObject.TryGetValue(new KeyValuePair<int, ulong>(typeId, root.Object), out stackId))
        //                {
        //                    stackId = stackObject.Count;
        //                    stackObject.Add(new KeyValuePair<int, ulong>(typeId, root.Object), stackId);
        //                }
        //                aliveIds.Add(stackId);
        //            }

        //            deadIds.Clear();
        //            for (int j = 0, jcnt = threadLocalDeadVars.Length; j < jcnt; ++j)
        //            {
        //                ClrRoot root = threadLocalDeadVars[j];
        //                ClrType clrType = heap.GetObjectType(root.Object);
        //                var typeName = clrType == null ? Constants.NullTypeName : clrType.Name;
        //                var typeId = Array.BinarySearch(typeNames, typeName, StringComparer.Ordinal);
        //                if (typeId < 0) typeId = Constants.InvalidIndex;
        //                int stackId;
        //                if (!stackObject.TryGetValue(new KeyValuePair<int, ulong>(typeId, root.Object), out stackId))
        //                {
        //                    stackId = stackObject.Count;
        //                    stackObject.Add(new KeyValuePair<int, ulong>(typeId, root.Object), stackId);
        //                }
        //                deadIds.Add(stackId);
        //            }

        //            frameIds.Clear();
        //            frameStackPtrs.Clear();
        //            for (int j = 0, jcnt = threadFrames.Length; j < jcnt; ++j)
        //            {
        //                ClrStackFrame fr = threadFrames[j];
        //                frameStackPtrs.Add(fr.StackPointer);
        //                if (fr.Method != null)
        //                {
        //                    string fullSig = fr.Method.GetFullSignature();
        //                    if (fullSig == null)
        //                        fullSig = fr.Method.Name;
        //                    if (fullSig == null) fullSig = "UKNOWN METHOD";
        //                    var frameStr = Utils.RealAddressStringHeader(fr.InstructionPointer) + fullSig;
        //                    var frId = frames.JustGetId(frameStr);
        //                    frameIds.Add(frId);
        //                }
        //                else
        //                {
        //                    string sig = string.IsNullOrEmpty(fr.DisplayString) ? "UKNOWN METHOD" : fr.DisplayString;
        //                    var frameStr = Utils.RealAddressStringHeader(fr.InstructionPointer) + sig;
        //                    var frId = frames.JustGetId(frameStr);
        //                    frameIds.Add(frId);
        //                }
        //            }

        //            var clrtThread = new ClrtThread(thread, blocks, blkCmp, frameIds.ToArray(), frameStackPtrs.ToArray(), aliveIds.ToArray(), deadIds.ToArray());
        //            Debug.Assert(clrtThread != null);
        //            clrtThread.Dump(bw);
        //        }

        //        bw.Write(blocks.Length);
        //        for (int i = 0, icnt = blocks.Length; i < icnt; ++i)
        //        {
        //            var blk = blocks[i];
        //            var ndx = Array.BinarySearch(blkBlockAry, blk, blkCmp);
        //            var typeId = GetTypeId(blk.Object, instances, typeIds);
        //            var clrtBlock = new ClrtBlkObject(blk, ndx, typeId);
        //            Debug.Assert(clrtBlock != null);
        //            clrtBlock.Dump(bw);
        //        }
        //        Utils.CloseStream(ref bw);

        //        path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapThreadFramesFilePostfix);
        //        bw = new BinaryWriter(File.Open(path, FileMode.Create));
        //        var ary = stackObject.ToArray();
        //        Array.Sort(ary, (a, b) => a.Value < b.Value ? -1 : (a.Value > b.Value ? 1 : 0));
        //        bw.Write(ary.Length);
        //        for (int i = 0, icnt = ary.Length; i < icnt; ++i)
        //        {
        //            bw.Write(ary[i].Key.Key);
        //            bw.Write(ary[i].Key.Value);
        //        }
        //        Utils.CloseStream(ref bw);
        //        path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.TxtThreadFrameDescriptionFilePostfix);
        //        frames.DumpInIdOrder(path, out error);
        //    }
        //    catch (Exception ex)
        //    {
        //        AddError(_currentRuntimeIndex,
        //            "Exception in GetThreadsInfos." + Environment.NewLine + Utils.GetExceptionErrorString(ex));
        //    }
        //    finally
        //    {
        //        bw?.Close();
        //        sw?.Close();
        //        clrtDump?.Dispose();
        //    }
        //}


        private void GetThreadsInfos(ClrRuntime runtm, ulong[] instances, int[] typeIds, string[] typeNames, IProgress<string> progress = null)
        {
            string error;
            BinaryWriter bw = null;
            StreamWriter sw = null;
            string progressHeader = Constants.HeavyCheckMarkHeader + "[Threads] ";
            try
            {
                var heap = runtm.Heap;
                progress?.Report(progressHeader + "Getting thread list...");
                var threads = DumpIndexer.GetThreads(runtm);
                progress?.Report(progressHeader + "Getting blocking object list...");
                var blocks = DumpIndexer.GetBlockingObjects(heap);

                progress?.Report(progressHeader + "Generating thread and blocking object references...");
                var threadSet = new HashSet<ClrThread>(new ClrThreadEqualityCmp());
                var blkGraph = new List<Tuple<BlockingObject, ClrThread[], ClrThread[]>>();
                var allBlkList = new List<BlockingObject>();
                var owners = new List<ClrThread>();
                var waiters = new List<ClrThread>();

                for (int i = 0, icnt = blocks.Length; i < icnt; ++i)
                {
                    var blk = blocks[i];
                    owners.Clear();
                    waiters.Clear();
                    ClrThread owner = null;
                    if (blk.Taken && blk.HasSingleOwner)
                    {
                        owner = blk.Owner;
                        if (owner != null)
                        {
                            threadSet.Add(owner);
                            owners.Add(owner);
                        }
                    }

                    if (blk.Owners != null && blk.Owners.Count > 0)
                    {
                        for (int j = 0, jcnt = blk.Owners.Count; j < jcnt; ++j)
                        {
                            var th = blk.Owners[j];
                            if (th != null)
                            {
                                threadSet.Add(th);
                                if (owner == null || owner.Address != th.Address)
                                    owners.Add(th);
                            }
                        }
                    }

                    if (blk.Waiters != null && blk.Waiters.Count > 0)
                    {
                        for (int j = 0, jcnt = blk.Waiters.Count; j < jcnt; ++j)
                        {
                            var th = blk.Waiters[j];
                            if (th != null)
                            {
                                threadSet.Add(th);
                                waiters.Add(th);
                            }
                        }
                    }

                    if (owners.Count > 0)
                    {
                        var ownerAry = owners.ToArray();
                        var waiterAry = waiters.ToArray();
                        blkGraph.Add(new Tuple<BlockingObject, ClrThread[], ClrThread[]>(blk, ownerAry, waiterAry));
                        allBlkList.Add(blk);
                    }
                    else if (waiters.Count > 0)
                    {
                        blkGraph.Add(new Tuple<BlockingObject, ClrThread[], ClrThread[]>(blk, Utils.EmptyArray<ClrThread>.Value,
                            waiters.ToArray()));
                        allBlkList.Add(blk);
                    }
                }

                var thrCmp = new ClrThreadCmp();
                var blkCmp = new BlockingObjectCmp();
                var blkInfoCmp = new BlkObjInfoCmp();

                // blocks and threads found in blocking objects
                //
                var blkThreadAry = threadSet.ToArray();
                Array.Sort(blkThreadAry, thrCmp);
                var blkBlockAry = allBlkList.ToArray();
                Array.Sort(blkBlockAry, blkCmp);
                var threadBlocksAry = blkGraph.ToArray();
                Array.Sort(threadBlocksAry, blkInfoCmp);

                progress?.Report(progressHeader + "Building thread and blocking object graph...");
                // create maps
                //
                int[] blkMap = new int[blkBlockAry.Length];
                int[] thrMap = new int[blkThreadAry.Length];
                for (int i = 0, icnt = blkMap.Length; i < icnt; ++i)
                {
                    blkMap[i] = Array.BinarySearch(blocks, blkBlockAry[i], blkCmp);
                    Debug.Assert(blkMap[i] >= 0);
                }

                for (int i = 0, icnt = thrMap.Length; i < icnt; ++i)
                {
                    thrMap[i] = Array.BinarySearch(threads, blkThreadAry[i], thrCmp);
                    Debug.Assert(thrMap[i] >= 0);
                }

                int blkThreadCount = blkThreadAry.Length;

#if FALSE
                Digraph graph = new Digraph(blkThreadCount + blkBlockAry.Length);

                for (int i = 0, cnt = threadBlocksAry.Length; i < cnt; ++i)
                {
                    var blkInfo = threadBlocksAry[i];
                    for (int j = 0, tcnt = blkInfo.Item2.Length; j < tcnt; ++j)
                    {
                        var ndx = Array.BinarySearch(blkThreadAry, blkInfo.Item2[j], thrCmp);
                        Debug.Assert(ndx >= 0);
                        graph.AddDistinctEdge(blkThreadCount + i, ndx);
                    }
                    for (int j = 0, tcnt = blkInfo.Item3.Length; j < tcnt; ++j)
                    {
                        var ndx = Array.BinarySearch(blkThreadAry, blkInfo.Item3[j], thrCmp);
                        Debug.Assert(ndx >= 0);
                        graph.AddDistinctEdge(ndx, blkThreadCount + i);
                    }
                }
#endif
                List<int>[] adjLists = new List<int>[blkThreadCount + blkBlockAry.Length];
                int edgeCount = 0;
                for (int i = 0, cnt = threadBlocksAry.Length; i < cnt; ++i)
                {
                    var blkInfo = threadBlocksAry[i];
                    for (int j = 0, tcnt = blkInfo.Item2.Length; j < tcnt; ++j)
                    {
                        var ndx = Array.BinarySearch(blkThreadAry, blkInfo.Item2[j], thrCmp);
                        Debug.Assert(ndx >= 0);
                        DGraph.AddDistinctEdge(adjLists, blkThreadCount + i, ndx,ref edgeCount);
                    }
                    for (int j = 0, tcnt = blkInfo.Item3.Length; j < tcnt; ++j)
                    {
                        var ndx = Array.BinarySearch(blkThreadAry, blkInfo.Item3[j], thrCmp);
                        Debug.Assert(ndx >= 0);
                        DGraph.AddDistinctEdge(adjLists, ndx, blkThreadCount + i, ref edgeCount);
                    }
                }
 
                progress?.Report(progressHeader + "Searching for thread and blocking object cycles...");

                int[][] allCycles = null;
                DGraph dgraph = new DGraph(adjLists, edgeCount);
                if (DGraph.HasCycle(dgraph.Graph))
                {
                    progress?.Report(progressHeader + "Found one cycle, searching for more, possible deadlock(s)...");
                    allCycles = Circuits.GetCycles(dgraph.Graph, out error);
                    // TODO JRD -- handle error 
                }
#if FALSE
                var cycle = new DirectedCycle(graph);
                var cycles = cycle.GetCycle();
                int[][] allCycles = null;
                if (cycles != null && cycles.Length > 0)
                {
                    progress?.Report(progressHeader + "Found one cycle, searching for more, possible deadlock(s)...");
                    var g = graph.GetJaggedArrayGraph();
                    allCycles = Circuits.GetCycles(g);
                }
#endif

                // save graph
                //
                progress?.Report(progressHeader + "Saving thread and blocking object graph...");
                var path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapThreadsAndBlocksGraphFilePostfix);
                bw = new BinaryWriter(File.Open(path, FileMode.Create));
                if (allCycles != null)
                {
                    bw.Write(allCycles.Length);
                    for (int i = 0, icnt = allCycles.Length; i < icnt; ++i)
                    {
                        bw.Write(allCycles[i].Length);
                        for(int j = 0, jcnt=allCycles[i].Length; j < jcnt; ++j)
                        {
                            bw.Write(allCycles[i][j]);
                        }
                    }
                }
                else
                {
                    bw.Write((int)0);
                }
                bw.Write(thrMap.Length);
                for (int i = 0, icnt = thrMap.Length; i < icnt; ++i)
                {
                    bw.Write(thrMap[i]);
                }
                bw.Write(blkMap.Length);
                for (int i = 0, icnt = blkMap.Length; i < icnt; ++i)
                {
                    bw.Write(blkMap[i]);
                }

#if FALSE
                graph.Dump(bw, out error);
#endif
                DGraph.Dump(bw, dgraph, out error);

                bw.Close();
                bw = null;
                if (error != null)
                    AddError(_currentRuntimeIndex, "Exception in GetThreadsInfos." + Environment.NewLine + error);

                // get frames info
                //
                var frames = new StringIdDct();
                var stObjCmp = new Utils.KVIntUlongCmpAsc();
                var stackObject = new SortedDictionary<KeyValuePair<int, ulong>, int>(stObjCmp);
                var stackTraceLst = new List<ClrStackFrame>();
                var rootEqCmp = new ClrRootEqualityComparer();
                var frameIds = new List<int>();
                var frameStackPtrs = new List<ulong>();
                var aliveIds = new List<int>();
                var deadIds = new List<int>();

                // save threads and blocks, generate stack info
                //
                path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapThreadsAndBlocksFilePostfix);
                bw = new BinaryWriter(File.Open(path, FileMode.Create));
                bw.Write(threads.Length);
                progress?.Report(progressHeader + "Getting frames information...");
                List<KeyValuePair<int,ulong>> stackZeros = new List<KeyValuePair<int, ulong>>();
                for (int i = 0, icnt = threads.Length; i < icnt; ++i)
                {
                    if (threads[i].StackBase == 0) stackZeros.Add(new KeyValuePair<int, ulong>(i,threads[i].StackLimit));
                }

                for (int i = 0, icnt = threads.Length; i < icnt; ++i)
                {
                    var thread = threads[i];
                    stackTraceLst.Clear();
                    foreach (var st in thread.EnumerateStackTrace())
                    {
                        stackTraceLst.Add(st);
                        if (stackTraceLst.Count > 100) break;
                    }

                    ClrRoot[] threadLocalAliveVars = Utils.GetArray<ClrRoot>(thread.EnumerateStackObjects(false));
                    ClrRoot[] threadLocalDeadVars;
                    if (!Setup.SkipDeadStackObjects)
                    {
                        ClrRoot[] all;
                        long stackSz = (thread.StackLimit > thread.StackBase) ? (long)(thread.StackLimit - thread.StackBase) : (long)(thread.StackBase - thread.StackLimit);
                        if (thread.StackBase != 0 && (stackSz < 10 * 1024 * 1024))
                        {
                            all = Utils.GetArray<ClrRoot>(thread.EnumerateStackObjects(true));
                        }
                        else
                        {
                            all = Utils.EmptyArray<ClrRoot>.Value;
                        }
                        threadLocalDeadVars = all.Except(threadLocalAliveVars, rootEqCmp).ToArray();
                    }
                    else
                    {
                        threadLocalDeadVars = Utils.EmptyArray<ClrRoot>.Value;
                    }
                    var threadFrames = stackTraceLst.ToArray();

                    aliveIds.Clear();
                    for (int j = 0, jcnt = threadLocalAliveVars.Length; j < jcnt; ++j)
                    {
                        ClrRoot root = threadLocalAliveVars[j];
                        ClrType clrType = heap.GetObjectType(root.Object);
                        var typeName = clrType == null ? Constants.NullTypeName : clrType.Name;
                        var typeId = Array.BinarySearch(typeNames, typeName, StringComparer.Ordinal);
                        if (typeId < 0) typeId = Constants.InvalidIndex;
                        int stackId;
                        if (!stackObject.TryGetValue(new KeyValuePair<int, ulong>(typeId, root.Object), out stackId))
                        {
                            stackId = stackObject.Count;
                            stackObject.Add(new KeyValuePair<int, ulong>(typeId, root.Object), stackId);
                        }
                        aliveIds.Add(stackId);
                    }

                    deadIds.Clear();
                    for (int j = 0, jcnt = threadLocalDeadVars.Length; j < jcnt; ++j)
                    {
                        ClrRoot root = threadLocalDeadVars[j];
                        ClrType clrType = heap.GetObjectType(root.Object);
                        var typeName = clrType == null ? Constants.NullTypeName : clrType.Name;
                        var typeId = Array.BinarySearch(typeNames, typeName, StringComparer.Ordinal);
                        if (typeId < 0) typeId = Constants.InvalidIndex;
                        int stackId;
                        if (!stackObject.TryGetValue(new KeyValuePair<int, ulong>(typeId, root.Object), out stackId))
                        {
                            stackId = stackObject.Count;
                            stackObject.Add(new KeyValuePair<int, ulong>(typeId, root.Object), stackId);
                        }
                        deadIds.Add(stackId);
                    }

                    frameIds.Clear();
                    frameStackPtrs.Clear();
                    for (int j = 0, jcnt = threadFrames.Length; j < jcnt; ++j)
                    {
                        ClrStackFrame fr = threadFrames[j];
                        frameStackPtrs.Add(fr.StackPointer);
                        if (fr.Method != null)
                        {
                            string fullSig = fr.Method.GetFullSignature();
                            if (fullSig == null)
                                fullSig = fr.Method.Name;
                            if (fullSig == null) fullSig = "UKNOWN METHOD";
                            var frameStr = Utils.RealAddressStringHeader(fr.InstructionPointer) + fullSig;
                            var frId = frames.JustGetId(frameStr);
                            frameIds.Add(frId);
                        }
                        else
                        {
                            string sig = string.IsNullOrEmpty(fr.DisplayString) ? "UKNOWN METHOD" : fr.DisplayString;
                            var frameStr = Utils.RealAddressStringHeader(fr.InstructionPointer) + sig;
                            var frId = frames.JustGetId(frameStr);
                            frameIds.Add(frId);
                        }
                    }

                    var clrtThread = new ClrtThread(thread, blocks, blkCmp, frameIds.ToArray(), frameStackPtrs.ToArray(), aliveIds.ToArray(), deadIds.ToArray());
                    Debug.Assert(clrtThread != null);
                    clrtThread.Dump(bw);
                }

                progress?.Report(progressHeader + "Saving data...");
                bw.Write(blocks.Length);
                for (int i = 0, icnt = blocks.Length; i < icnt; ++i)
                {
                    var blk = blocks[i];
                    var typeId = GetTypeId(blk.Object, instances, typeIds);
                    var clrtBlock = new ClrtBlkObject(blk, i, typeId);
                    Debug.Assert(clrtBlock != null);
                    clrtBlock.Dump(bw);
                }
                Utils.CloseStream(ref bw);

                path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapThreadFramesFilePostfix);
                bw = new BinaryWriter(File.Open(path, FileMode.Create));
                var ary = stackObject.ToArray();
                Array.Sort(ary, (a, b) => a.Value < b.Value ? -1 : (a.Value > b.Value ? 1 : 0));
                bw.Write(ary.Length);
                for (int i = 0, icnt = ary.Length; i < icnt; ++i)
                {
                    bw.Write(ary[i].Key.Key);
                    bw.Write(ary[i].Key.Value);
                }
                Utils.CloseStream(ref bw);
                path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.TxtThreadFrameDescriptionFilePostfix);
                frames.DumpInIdOrder(path, out error);
                progress?.Report(progressHeader + "Thread processing done...");
            }
            catch (Exception ex)
            {
                AddError(_currentRuntimeIndex,
                    "Exception in GetThreadsInfos." + Environment.NewLine + Utils.GetExceptionErrorString(ex));
            }
            finally
            {
                bw?.Close();
                sw?.Close();
            }
        }

        private void GetThreadsInfos2(ClrRuntime runtm, ulong[] instances, int[] typeIds, string[] typeNames, IProgress<string> progress = null)
        {
            string error;
            BinaryWriter bw = null;
            StreamWriter sw = null;
            string progressHeader = Constants.HeavyCheckMarkHeader + "[Threads] ";
            try
            {
                var heap = runtm.Heap;
                progress?.Report(progressHeader + "Getting thread list...");
                var threads = DumpIndexer.GetThreads(runtm);
                progress?.Report(progressHeader + "Getting blocking object list...");
                var blocks = DumpIndexer.GetBlockingObjects(heap);

                // build thread - block graph
                //
                progress?.Report(progressHeader + "Building thread and blocking object graph...");
                var tbgraph = ThreadBlockGraph.BuildThreadBlockGraph(threads, blocks, out error);
                if (error != null)
                    AddError(_currentRuntimeIndex, "Exception in GetThreadsInfos." + Environment.NewLine + error);
                // save graph
                //
                progress?.Report(progressHeader + "Saving thread and blocking object graph...");
                var path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapThreadsAndBlocksGraphFilePostfix);
                ThreadBlockGraph.Dump(path, tbgraph, out error);
//                bw = new BinaryWriter(File.Open(path, FileMode.Create));
//                if (allCycles != null)
//                {
//                    bw.Write(allCycles.Length);
//                    for (int i = 0, icnt = allCycles.Length; i < icnt; ++i)
//                    {
//                        bw.Write(allCycles[i].Length);
//                        for (int j = 0, jcnt = allCycles[i].Length; j < jcnt; ++j)
//                        {
//                            bw.Write(allCycles[i][j]);
//                        }
//                    }
//                }
//                else
//                {
//                    bw.Write((int)0);
//                }
//                bw.Write(thrMap.Length);
//                for (int i = 0, icnt = thrMap.Length; i < icnt; ++i)
//                {
//                    bw.Write(thrMap[i]);
//                }
//                bw.Write(blkMap.Length);
//                for (int i = 0, icnt = blkMap.Length; i < icnt; ++i)
//                {
//                    bw.Write(blkMap[i]);
//                }

//#if FALSE
//                graph.Dump(bw, out error);
//#endif
//                DGraph.Dump(bw, dgraph, out error);

//                bw.Close();
//                bw = null;
                if (error != null)
                    AddError(_currentRuntimeIndex, "Exception in GetThreadsInfos." + Environment.NewLine + error);

                // get frames info
                //
                var frames = new StringIdDct();
                var stObjCmp = new Utils.KVIntUlongCmpAsc();
                var stackObject = new SortedDictionary<KeyValuePair<int, ulong>, int>(stObjCmp);
                var stackTraceLst = new List<ClrStackFrame>();
                var rootEqCmp = new ClrRootEqualityComparer();
                var frameIds = new List<int>();
                var frameStackPtrs = new List<ulong>();
                var aliveIds = new List<int>();
                var deadIds = new List<int>();

                // save threads and blocks, generate stack info
                //
                path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapThreadsAndBlocksFilePostfix);
                bw = new BinaryWriter(File.Open(path, FileMode.Create));
                bw.Write(threads.Length);
                progress?.Report(progressHeader + "Getting frames information...");
                List<KeyValuePair<int, ulong>> stackZeros = new List<KeyValuePair<int, ulong>>();
                for (int i = 0, icnt = threads.Length; i < icnt; ++i)
                {
                    if (threads[i].StackBase == 0) stackZeros.Add(new KeyValuePair<int, ulong>(i, threads[i].StackLimit));
                }

                for (int i = 0, icnt = threads.Length; i < icnt; ++i)
                {
                    var thread = threads[i];
                    stackTraceLst.Clear();
                    foreach (var st in thread.EnumerateStackTrace())
                    {
                        stackTraceLst.Add(st);
                        if (stackTraceLst.Count > 100) break;
                    }

                    ClrRoot[] threadLocalAliveVars = Utils.GetArray<ClrRoot>(thread.EnumerateStackObjects(false));
                    ClrRoot[] threadLocalDeadVars;
                    if (!Setup.SkipDeadStackObjects)
                    {
                        ClrRoot[] all;
                        long stackSz = (thread.StackLimit > thread.StackBase) ? (long)(thread.StackLimit - thread.StackBase) : (long)(thread.StackBase - thread.StackLimit);
                        if (thread.StackBase != 0 && (stackSz < 10 * 1024 * 1024))
                        {
                            all = Utils.GetArray<ClrRoot>(thread.EnumerateStackObjects(true));
                        }
                        else
                        {
                            all = Utils.EmptyArray<ClrRoot>.Value;
                        }
                        threadLocalDeadVars = all.Except(threadLocalAliveVars, rootEqCmp).ToArray();
                    }
                    else
                    {
                        threadLocalDeadVars = Utils.EmptyArray<ClrRoot>.Value;
                    }
                    var threadFrames = stackTraceLst.ToArray();

                    aliveIds.Clear();
                    for (int j = 0, jcnt = threadLocalAliveVars.Length; j < jcnt; ++j)
                    {
                        ClrRoot root = threadLocalAliveVars[j];
                        ClrType clrType = heap.GetObjectType(root.Object);
                        var typeName = clrType == null ? Constants.NullTypeName : clrType.Name;
                        var typeId = Array.BinarySearch(typeNames, typeName, StringComparer.Ordinal);
                        if (typeId < 0) typeId = Constants.InvalidIndex;
                        int stackId;
                        if (!stackObject.TryGetValue(new KeyValuePair<int, ulong>(typeId, root.Object), out stackId))
                        {
                            stackId = stackObject.Count;
                            stackObject.Add(new KeyValuePair<int, ulong>(typeId, root.Object), stackId);
                        }
                        aliveIds.Add(stackId);
                    }

                    deadIds.Clear();
                    for (int j = 0, jcnt = threadLocalDeadVars.Length; j < jcnt; ++j)
                    {
                        ClrRoot root = threadLocalDeadVars[j];
                        ClrType clrType = heap.GetObjectType(root.Object);
                        var typeName = clrType == null ? Constants.NullTypeName : clrType.Name;
                        var typeId = Array.BinarySearch(typeNames, typeName, StringComparer.Ordinal);
                        if (typeId < 0) typeId = Constants.InvalidIndex;
                        int stackId;
                        if (!stackObject.TryGetValue(new KeyValuePair<int, ulong>(typeId, root.Object), out stackId))
                        {
                            stackId = stackObject.Count;
                            stackObject.Add(new KeyValuePair<int, ulong>(typeId, root.Object), stackId);
                        }
                        deadIds.Add(stackId);
                    }

                    frameIds.Clear();
                    frameStackPtrs.Clear();
                    for (int j = 0, jcnt = threadFrames.Length; j < jcnt; ++j)
                    {
                        ClrStackFrame fr = threadFrames[j];
                        frameStackPtrs.Add(fr.StackPointer);
                        if (fr.Method != null)
                        {
                            string fullSig = fr.Method.GetFullSignature();
                            if (fullSig == null)
                                fullSig = fr.Method.Name;
                            if (fullSig == null) fullSig = "UKNOWN METHOD";
                            var frameStr = Utils.RealAddressStringHeader(fr.InstructionPointer) + fullSig;
                            var frId = frames.JustGetId(frameStr);
                            frameIds.Add(frId);
                        }
                        else
                        {
                            string sig = string.IsNullOrEmpty(fr.DisplayString) ? "UKNOWN METHOD" : fr.DisplayString;
                            var frameStr = Utils.RealAddressStringHeader(fr.InstructionPointer) + sig;
                            var frId = frames.JustGetId(frameStr);
                            frameIds.Add(frId);
                        }
                    }
                    var blkCmp = new BlockingObjectCmp();
                    var clrtThread = new ClrtThread(thread, blocks, blkCmp, frameIds.ToArray(), frameStackPtrs.ToArray(), aliveIds.ToArray(), deadIds.ToArray());
                    Debug.Assert(clrtThread != null);
                    clrtThread.Dump(bw);
                }

                progress?.Report(progressHeader + "Saving data...");
                bw.Write(blocks.Length);
                for (int i = 0, icnt = blocks.Length; i < icnt; ++i)
                {
                    var blk = blocks[i];
                    var typeId = GetTypeId(blk.Object, instances, typeIds);
                    var clrtBlock = new ClrtBlkObject(blk, i, typeId);
                    Debug.Assert(clrtBlock != null);
                    clrtBlock.Dump(bw);
                }
                Utils.CloseStream(ref bw);

                path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapThreadFramesFilePostfix);
                bw = new BinaryWriter(File.Open(path, FileMode.Create));
                var ary = stackObject.ToArray();
                Array.Sort(ary, (a, b) => a.Value < b.Value ? -1 : (a.Value > b.Value ? 1 : 0));
                bw.Write(ary.Length);
                for (int i = 0, icnt = ary.Length; i < icnt; ++i)
                {
                    bw.Write(ary[i].Key.Key);
                    bw.Write(ary[i].Key.Value);
                }
                Utils.CloseStream(ref bw);
                path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.TxtThreadFrameDescriptionFilePostfix);
                frames.DumpInIdOrder(path, out error);
                progress?.Report(progressHeader + "Thread processing done...");
            }
            catch (Exception ex)
            {
                AddError(_currentRuntimeIndex,
                    "Exception in GetThreadsInfos." + Environment.NewLine + Utils.GetExceptionErrorString(ex));
            }
            finally
            {
                bw?.Close();
                sw?.Close();
            }
        }

        public int GetTypeId(ulong addr, ulong[] instances, int[] typeIds)
        {
            int inst = Utils.AddressSearch(instances, addr);
            return inst < 0 ? Constants.InvalidIndex : typeIds[inst];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clrtDump">Our dump wrapper.</param>
        /// <param name="progress">Report progress if this is not null.</param>
        /// <param name="error">Output exception error.</param>
        /// <returns>True if successful.</returns>
        public bool GetPrerequisites(ClrtDump clrtDump, IProgress<string> progress, out StringIdDct[] strIds,
            out string error)
        {
            error = null;
            strIds = null;
            progress?.Report(Utils.TimeString(DateTime.Now) + " Getting prerequisites...");
            try
            {
                strIds = new StringIdDct[clrtDump.RuntimeCount];
                //_clrtAppDomains = new ClrtAppDomains[clrtDump.RuntimeCount];
                for (int r = 0, rcnt = clrtDump.Runtimes.Length; r < rcnt; ++r)
                {
                    _errors[r] = new ConcurrentBag<string>();
                    _stringIdDcts[r] = new StringIdDct();
                    _stringIdDcts[r].AddKey("[]");
                    _stringIdDcts[r].AddKey(Constants.Unknown);
                    var clrRuntime = clrtDump.Runtimes[r];
                    //_clrtAppDomains[r] = GetAppDomains(clrRuntime, _stringIdDcts[r]);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return false;
            }
        }

        public bool GetTargetModuleInfos(ClrtDump clrtDump, IProgress<string> progress, out string error)
        {
            error = null;
            progress?.Report(Utils.TimeString(DateTime.Now) + " Getting target module infos...");
            try
            {
                var target = clrtDump.DataTarget;
                var modules = target.EnumerateModules().ToArray();
                var dct = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var lst = new string[3];
                for (int i = 0, icnt = modules.Length; i < icnt; ++i)
                {
                    var module = modules[i];
                    var moduleName = Path.GetFileName(module.FileName);
                    var key = moduleName + Constants.HeavyGreekCrossPadded + module.FileName;
                    int nameNdx = 1;
                    while (dct.ContainsKey(key))
                    {
                        moduleName = moduleName + "(" + nameNdx + ")";
                        key = moduleName + Constants.HeavyGreekCrossPadded + module.FileName;
                        ++nameNdx;
                    }
                    lst[0] = Utils.RealAddressString(module.ImageBase);
                    lst[1] = Utils.SizeString(module.FileSize);
                    lst[2] = module.Version.ToString();
                    var entry = string.Join(Constants.HeavyGreekCrossPadded, lst);
                    if (!dct.ContainsKey(key)) // just in case
                        dct.Add(key, entry);
                    else
                        AddError(0, "DataTarget.EnumerateModules return duplicate modules: " + key);
                }
                string[] extraInfos = new[]
                {
                    _fileMoniker.Path,
                    "Module Count: " + Utils.CountString(dct.Count)
                };
                DumpModuleInfos(dct, extraInfos);
                return true;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return false;
            }
        }

        private void DumpModuleInfos(SortedDictionary<string, string> dct, IList<string> extraInfos)
        {
            var path = _fileMoniker.GetFilePath(-1, Constants.TxtTargetModulesPostfix);
            StreamWriter txtWriter = null;
            try
            {
                ResetReadOnlyAttribute(path);
                txtWriter = new StreamWriter(path);
                txtWriter.WriteLine("### MDRDESK REPORT: Target Module Infos");
                txtWriter.WriteLine("### TITLE: Target Modules");
                txtWriter.WriteLine("### COUNT: " + Utils.LargeNumberString(dct.Count));
                txtWriter.WriteLine("### COLUMNS: Image Base|uint64"
                                    + Constants.HeavyGreekCrossPadded + "File Size|uint32"
                                    + Constants.HeavyGreekCrossPadded + "Version|string"
                                    + Constants.HeavyGreekCrossPadded + "File Name|string" +
                                    Constants.HeavyGreekCrossPadded + "Path|string");
                txtWriter.WriteLine("### SEPARATOR: " + Constants.HeavyGreekCrossPadded);

                for (int i = 0, icnt = extraInfos.Count; i < icnt; ++i)
                {
                    txtWriter.WriteLine(ReportFile.DescrPrefix + extraInfos[i]);
                }
                foreach (var kv in dct)
                {
                    txtWriter.Write(kv.Value);
                    txtWriter.Write(Constants.HeavyGreekCrossPadded);
                    txtWriter.WriteLine(kv.Key);
                }
            }
            catch (Exception ex)
            {
                AddError(-1, "[Indexer.DumpModuleInfos]" + Environment.NewLine + Utils.GetExceptionErrorString(ex));
            }
            finally
            {
                if (txtWriter != null)
                {
                    txtWriter.Close();
                    File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
                }
            }
        }


        private void DumpErrors()
        {
            StreamWriter errWriter = null;
            try
            {
                for (int r = 0; r < _errors.Length; ++r)
                {
                    var path = _fileMoniker.GetFilePath(r, Constants.TxtIndexErrorsFilePostfix);
                    errWriter = new StreamWriter(path);
                    errWriter.WriteLine("ERROR COUNT: " + _errors[r].Count);
                    errWriter.WriteLine();
                    while (!_errors[r].IsEmpty)
                    {
                        string error;
                        if (_errors[r].TryTake(out error))
                        {
                            errWriter.WriteLine(error);
                        }
                    }
                    _errors[r] = null;
                    Utils.CloseStream(ref errWriter);
                }
            }
            catch
            {
            }
            finally
            {
                errWriter?.Close();
            }
        }

#region references


#endregion references

#region data dumping

        private void DumpIndexInfo(Version version, ClrtDump dump, string indexingDuration = null)
        {
            var path = _fileMoniker.GetFilePath(-1, Constants.TxtIndexInfoFilePostfix);
            StreamWriter txtWriter = null;
            int i = 0;
            try
            {
                ResetReadOnlyAttribute(path);
                txtWriter = new StreamWriter(path);
                txtWriter.WriteLine("MDR Version: ["
                                    + version
                                    + "], this is this application version.");
                txtWriter.WriteLine("Dump Path: " + _fileMoniker.Path);
                txtWriter.WriteLine("Dump Size: " + Utils.GetFileSizeString(_fileMoniker.Path));

                if (!string.IsNullOrWhiteSpace(indexingDuration))
                {
                    txtWriter.WriteLine("Indexing Duration: " + indexingDuration);
                }
                var runtimes = dump.Runtimes;
                var clrinfos = dump.ClrInfos;
                txtWriter.WriteLine("Runtime Count: " + runtimes.Length);
                txtWriter.WriteLine();
                for (i = 0; i < runtimes.Length; ++i)
                {
                    txtWriter.WriteLine("RUNTIME " + i);
                    var runtime = runtimes[i];
                    var clrinfo = clrinfos[i];
                    txtWriter.WriteLine("Dac Path: " + dump.DacPaths[i]);
                    txtWriter.WriteLine("Runtime version: " + clrinfo?.Version.ToString());
                    txtWriter.WriteLine("Clr type: " + clrinfo?.Flavor.ToString());
                    txtWriter.WriteLine("Module info, file name: " + clrinfo?.ModuleInfo?.FileName);
                    txtWriter.WriteLine("Module info, image base: " + $"0x{clrinfo?.ModuleInfo?.ImageBase:x14}");
                    txtWriter.WriteLine("Module info, file size: " + $"{clrinfo?.ModuleInfo?.FileSize:#,###}");
                    txtWriter.WriteLine("Heap count: " + runtime.HeapCount);
                    txtWriter.WriteLine("Can walk heap: " + runtime.Heap.CanWalkHeap.ToString());
                    txtWriter.WriteLine("Pointer size: " + runtime.PointerSize);
                    txtWriter.WriteLine("Is server GC : " + runtime.ServerGC);
                    if (runtime.AppDomains != null)
                    {
                        txtWriter.WriteLine("Application domains: [" + runtime.AppDomains.Count + "]");
                        for (int j = 0, jcnt = runtime.AppDomains.Count; j < jcnt; ++j)
                        {
                            var appDoamin = runtime.AppDomains[j];
                            txtWriter.Write("## (" + appDoamin.Id + ") " 
                                                + Utils.RealAddressStringHeader(appDoamin.Address)
                                                + (appDoamin.Name ?? "unnamed")
                                                 + ", modules " + appDoamin.Modules.Count);
                            if (appDoamin.ApplicationBase != null)
                                txtWriter.Write(" base: " + Utils.ReplaceUriSpaces(appDoamin.ApplicationBase));
                            if (appDoamin.ConfigurationFile != null)
                                txtWriter.Write(" config: " + Utils.ReplaceUriSpaces(appDoamin.ConfigurationFile));
                            txtWriter.WriteLine();
                        }
                    }
                    if (runtime.SystemDomain != null)
                        txtWriter.WriteLine("System domain: " + (runtime.SystemDomain.Name ?? "unnamed") + ", id: " +
                                            runtime.SystemDomain.Id + ", module cnt: " +
                                            runtime.SystemDomain.Modules.Count);
                    if (runtime.SharedDomain != null)
                        txtWriter.WriteLine("Shared domain: " + (runtime.SharedDomain.Name ?? "unnamed") + ", id: " +
                                            runtime.SharedDomain.Id + ", module cnt: " +
                                            runtime.SharedDomain.Modules.Count);

                    txtWriter.WriteLine("Instance Count: " + Utils.LargeNumberString(_instanceCount[i]));
                    txtWriter.WriteLine("Type Count: " + Utils.LargeNumberString(_typeCount[i]));
                    txtWriter.WriteLine("Finalizer Queue Count: " + Utils.LargeNumberString(_finalizerCount[i]));
                    txtWriter.WriteLine("Roots Count: " + Utils.LargeNumberString(_rootCount[i]));
                    string error;
                    var heapBalance = dump.GetHeapBalance(i, out error);
                    if (error != null)
                        AddError(i, "[Indexer.DumpIndexInfo]" + Environment.NewLine + error);
                    else
                    {
                        var lst = heapBalance.Select(kv => (double)kv.Value).ToArray();
                        bool suspect;
                        double mean;
                        var stdDev = Utils.GetStandardDeviation(lst, out suspect, out mean);
                        // TODO JRD -- here we consider heap unbabalanced if standard dev is greater than half of heap sizes average
                        txtWriter.WriteLine((suspect ? (Constants.HeavyMultiplicationHeader) : "") +  "Heap Balance");
                        foreach(var kv in heapBalance)
                        {
                            txtWriter.WriteLine("Heap {0,2}: {1} bytes", kv.Key, Utils.LargeNumberString(kv.Value));
                        }
                        txtWriter.WriteLine("Standard Deviation: " + Utils.LargeNumberString((long)stdDev) + ", Mean: " + Utils.LargeNumberString((long)mean));
                    }

                    txtWriter.WriteLine();
                }
            }
            catch (Exception ex)
            {
                AddError(i, "[Indexer.DumpIndexInfo]" + Environment.NewLine + Utils.GetExceptionErrorString(ex));
            }
            finally
            {
                txtWriter?.Close();
                //if (txtWriter != null)
                //{
                //	txtWriter.Close();
                //	File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
                //}
            }
        }

#endregion data dumping

#region indexing helpers

        public static void MarkAsRootedOld(ulong addr, int addrNdx, ulong[] instances, int[][] references)
        {
            if (Utils.IsRooted(addr)) return;
            instances[addrNdx] = Utils.SetAsRooted(addr);
            if (references[addrNdx] == null) return;
            for (int i = 0, icnt = references[addrNdx].Length; i < icnt; ++i)
            {
                var refr = references[addrNdx][i];
                var address = instances[refr];
                if (!Utils.IsRooted(address))
                    if (address != 0UL)
                    {
                        instances[refr] = Utils.SetAsRooted(address);
                        MarkAsRooted(address, refr, instances, references);
                    }
            }
        }

        public static void MarkAsRooted(ulong addr, int addrNdx, ulong[] instances, int[][] references)
        {
            if (Utils.IsRooted(addr)) return;
            instances[addrNdx] = Utils.SetAsRooted(addr);
            if (references[addrNdx] == null) return;
            Queue<KeyValuePair<ulong, int>> que = new Queue<KeyValuePair<ulong, int>>(1024);
            for (int i = 0, icnt = references[addrNdx].Length; i < icnt; ++i)
            {
                var refr = references[addrNdx][i];
                var address = instances[refr];
                if (Utils.IsRooted(address) || address == 0UL) continue;
                instances[refr] = Utils.SetAsRooted(address);
                que.Enqueue(new KeyValuePair<ulong, int>(address, refr));
            }
            while (que.Count > 0)
            {
                var kv = que.Dequeue();
                var address = kv.Key;
                if (Utils.IsRooted(address)) continue;
                var refr = kv.Value;
                instances[refr] = Utils.SetAsRooted(address);
                if (references[refr] == null) return;
                for (int i = 0, icnt = references[addrNdx].Length; i < icnt; ++i)
                {
                    refr = references[addrNdx][i];
                    address = instances[refr];
                    if (Utils.IsRooted(address) || address == 0UL) continue;
                    instances[refr] = Utils.SetAsRooted(address);
                    que.Enqueue(new KeyValuePair<ulong, int>(address, refr));
                }
            }
        }

        public static string[] GetTypeNames(ClrHeap heap, out string error)
        {
            error = null;
            try
            {
                List<string> typeNames = new List<string>(35000);
                AddStandardTypeNames(typeNames);
                var typeList = heap.EnumerateTypes();
                typeNames.AddRange(typeList.Select(clrType => clrType.Name));
                typeNames.Sort(StringComparer.Ordinal);
                string[] names = typeNames.Distinct().ToArray();
                return names;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
        }



        public static void AddStandardTypeNames(List<string> typeNames)
        {
            typeNames.Add(Constants.NullTypeName);
            typeNames.Add(Constants.UnknownTypeName);
            typeNames.Add(Constants.ErrorTypeName);
            typeNames.Add(Constants.FreeTypeName);
            typeNames.Add(Constants.System__Canon);
            typeNames.Add(Constants.SystemObject);
        }

        private void AddError(int rtNdx, string error)
        {
            _errors[rtNdx].Add(DateTime.Now.ToString("s") + Environment.NewLine + error);
        }

        private void ResetReadOnlyAttribute(string path)
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }
        }

        public static ClrThread[] GetThreads(ClrRuntime runtime)
        {
            var lst = new List<ClrThread>(256);
            var thrdLst = runtime.Threads;
            for (int i = 0, icnt = thrdLst.Count; i < icnt; ++i)
            {
                lst.Add(thrdLst[i]);
            }
            lst.Sort(new ClrThreadCmp());
            return lst.ToArray();
        }

        public static int GetThreadId(ClrThread[] ary, ClrThread thrd)
        {
            if (thrd == null) return Constants.InvalidIndex;
            return Array.BinarySearch(ary, thrd, new ClrThreadCmp());
        }

        public static BlockingObject[] GetBlockingObjects(ClrHeap heap)
        {
            var lst = new List<BlockingObject>(1024);
            foreach (var block in heap.EnumerateBlockingObjects())
            {
                var clrType = heap.GetObjectType(block.Object);
                if (clrType == null || clrType.Name == "Free") continue;
                lst.Add(block);
            }
            var ary = lst.ToArray();
            Array.Sort(ary, new BlockingObjectCmp());
            return ary;
        }

        public static BlockingObject[] GetBlockingObjectsEx(ClrHeap heap, out BlockingObject[] free)
        {
            var lst = new List<BlockingObject>(1024);
            var freelst = new List<BlockingObject>(1024);
            foreach (var block in heap.EnumerateBlockingObjects())
            {
                var clrType = heap.GetObjectType(block.Object);
                if (clrType == null || clrType.Name == "Free")
                {
                    freelst.Add(block);
                    continue;
                }
                lst.Add(block);
            }
            var cmp = new BlockingObjectCmp();
            var ary = lst.ToArray();
            Array.Sort(ary, cmp);
            free = freelst.ToArray();
            Array.Sort(free, cmp);
            return ary;
        }

#endregion indexing helpers
    }

    public class BlkObjInfoCmp : IComparer<Tuple<BlockingObject, ClrThread[], ClrThread[]>>
    {
        public int Compare(Tuple<BlockingObject, ClrThread[], ClrThread[]> a,
            Tuple<BlockingObject, ClrThread[], ClrThread[]> b)
        {
            return a.Item1.Object < b.Item1.Object ? -1 : (a.Item1.Object > b.Item1.Object ? 1 : 0);
        }
    }
}
