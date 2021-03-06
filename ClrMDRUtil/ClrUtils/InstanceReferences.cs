﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using ClrMDRUtil;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
    public class InstanceReferences : IDisposable
    {
        [Flags]
        public enum ReferenceType
        {
            None = 0,
            Ancestors = 1,
            Descendants = 1 << 2,
            Rooted = 1 << 3,
            Unrooted = 1 << 4,
            Finalizer = 1 << 5,
            All = 1 << 6,
        }

        public static string InstanceTypeString(ReferenceType refType)
        {
            if ((refType & ReferenceType.Rooted) > 0) return "ROOTED";
            if ((refType & ReferenceType.Unrooted) > 0) return "UNROOTED";
            if ((refType & ReferenceType.Finalizer) > 0) return "FINALIZER";
            return "ALL";
        }

        enum RefFile
        {
            FwdOffsets,
            FwdRefs,
            BwdOffsets,
            BwdRefs,
            Count
        }

        public const ulong AddressFlagMask = 0x00FFFFFFFFFFFFFF;
        public const ulong AddressMask = 0xFF00000000000000;


        const int MaxNodes = 10000;

        string[] _fileList;
        ulong[] _instances;
        long[] _fwdOffsets;
        long[] _bwdOffsets;
        BinaryReader[] _readers;

        MemoryMappedFile[] _maps;
        MemoryMappedViewAccessor[] _views;



        #region building fields

        int[] _forwardRefsCounts;
        int[] _reversedRefsCounts;
        int _totalReversedRefs;
        string _error;
        public string Error => _error;
        Stopwatch _stopWatch;
        IProgress<string> _progress;
        string _instanceFilePath;
        BitSet _reflagSet;
        //HashSet<int> _reflagSet;

        #endregion building fields

        #region ctors / initialization

        public InstanceReferences(ulong[] instances, string[] fileList, IProgress<string> progress = null, string instanceFilePath = null)
        {
            Debug.Assert(fileList.Length == (int)RefFile.Count);
            _fileList = fileList;
            _instances = instances;
            _progress = progress;
            _instanceFilePath = instanceFilePath;
            _maps = new MemoryMappedFile[(int)RefFile.Count];
            _views = new MemoryMappedViewAccessor[(int)RefFile.Count];
            _readers = new BinaryReader[(int)RefFile.Count];


        }

        public InstanceReferences(ulong[] instances, int runtmNdx, DumpFileMoniker moniker)
        {
            _instances = instances;
            _fileList = new string[(int)RefFile.Count];
            _fileList[0] = moniker.GetFilePath(runtmNdx, Constants.MapRefFwdOffsetsFilePostfix);
            _fileList[1] = moniker.GetFilePath(runtmNdx, Constants.MapFwdRefsFilePostfix);
            _fileList[2] = moniker.GetFilePath(runtmNdx, Constants.MapRefBwdOffsetsFilePostfix);
            _fileList[3] = moniker.GetFilePath(runtmNdx, Constants.MapBwdRefsFilePostfix);
            _maps = new MemoryMappedFile[(int)RefFile.Count];
            _views = new MemoryMappedViewAccessor[(int)RefFile.Count];
            _readers = new BinaryReader[(int)RefFile.Count];
            _readers = new BinaryReader[(int)RefFile.Count];
        }

        public bool LoadReferences(out string error)
        {
            error = null;
            try
            {
                return true;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return false;
            }
        }

        #endregion ctors / initialization

        private int ReferenceCount(long off1, long off2)
        {
            return (int)(off2 - off1) / sizeof(int);
        }

        private int[] ReadReferences(BinaryReader br, long offset, int count)
        {
            int[] refs = new int[count];
            br.BaseStream.Seek(offset, SeekOrigin.Begin);
            for (int i = 0, icnt = count; i < icnt; ++i)
            {
                refs[i] = br.ReadInt32();
            }
            return refs;
        }

        private int[] ReadReferences(MemoryMappedViewAccessor va, long offset, int count)
        {
            int[] refs = new int[count];
            va.ReadArray<int>(offset, refs, 0, count);
            return refs;
        }

        private void ReadReferences(BinaryReader br, long offset, int count, int[] buf)
        {
            Debug.Assert(buf.Length >= count);
            br.BaseStream.Seek(offset, SeekOrigin.Begin);
            for (int i = 0, icnt = count; i < icnt; ++i)
            {
                buf[i] = br.ReadInt32();
            }
        }

        private void ReadReferences(MemoryMappedViewAccessor va, long offset, int count, int[] buf)
        {
            Debug.Assert(buf.Length >= count);
            va.ReadArray<int>(offset, buf, 0, count);
        }

        private IndexNode[] ReadReferences(BinaryReader br, long offset, int count, int level, Queue<IndexNode> que)
        {
            IndexNode[] refs = new IndexNode[count];
            br.BaseStream.Seek(offset, SeekOrigin.Begin);
            for (int i = 0, icnt = count; i < icnt; ++i)
            {
                var ndx = br.ReadInt32();
                var node = new IndexNode(ndx, level);
                refs[i] = node;
                que.Enqueue(node);
            }
            return refs;
        }

        private IndexNode[] ReadReferences(MemoryMappedViewAccessor va, long offset, int count, int level, Queue<IndexNode> que)
        {
            IndexNode[] refs = new IndexNode[count];
            int[] ndxs = new int[count];
            va.ReadArray<int>(offset, ndxs, 0, count);
            for (int i = 0, icnt = count; i < icnt; ++i)
            {
                var ndx = ndxs[i];
                var node = new IndexNode(ndx, level);
                refs[i] = node;
                que.Enqueue(node);
            }
            return refs;
        }

        private long[] GetOffsets(RefFile fileNdx)
        {
            switch (fileNdx)
            {
                case RefFile.FwdOffsets:
                    if (_fwdOffsets == null)
                        _fwdOffsets = ReadOffsets(fileNdx, _instances.Length + 1);
                    return _fwdOffsets;
                case RefFile.BwdOffsets:
                    if (_bwdOffsets == null)
                        _bwdOffsets = ReadOffsets(fileNdx, _instances.Length + 1);
                    return _bwdOffsets;
                default:
                    return null;
            }
        }

        private long[] ReadOffsets(RefFile fileNdx, int count)
        {
            BinaryReader br = null;
            try
            {
                br = GetReader(fileNdx);
                long[] ary = new long[count];
                for (int i = 0, icnt = count; i < icnt; ++i)
                    ary[i] = br.ReadInt64();
                return ary;
            }
            finally
            {
                br?.Close();
                _readers[(int)fileNdx] = null;
            }

        }

        private ValueTuple<long,int> GetRefOffsetAndCount(MemoryMappedViewAccessor va, int instNdx)
        {
            long off;
            va.Read<long>((long)instNdx * (long)sizeof(long),out off);
            long next;
            va.Read<long>((long)(instNdx+1) * (long)sizeof(long), out next);
            return (off, (int)((next - off) / sizeof(int)));
        }

        private BinaryReader GetReader(RefFile fileNdx)
        {
            if (_readers[(int)fileNdx] != null) return _readers[(int)fileNdx];
            _readers[(int)fileNdx] = new BinaryReader(File.Open(_fileList[(int)fileNdx], FileMode.Open, FileAccess.Read));
            return _readers[(int)fileNdx];
        }

        private MemoryMappedViewAccessor GetMapReader(RefFile fileNdx)
        {
            if (_views[(int)fileNdx] == null)
            {
                if (_maps[(int)fileNdx] == null)
                {
                    _maps[(int)fileNdx] = MemoryMappedFile.CreateFromFile(_fileList[(int)fileNdx], FileMode.Open,null);

                }
                _views[(int)fileNdx] = _maps[(int)fileNdx].CreateViewAccessor();
            }

            return _views[(int)fileNdx];
        }


        private BinaryReader GetReader(RefFile fileNdx, FileMode mode)
        {
            return new BinaryReader(File.Open(_fileList[(int)fileNdx], mode, FileAccess.Read));
        }

        private BinaryWriter GetWriter(RefFile fileNdx, FileMode mode)
        {
            return new BinaryWriter(File.Open(_fileList[(int)fileNdx], mode, FileAccess.Write, FileShare.None));
        }

        
        #region queries

        public KeyValuePair<IndexNode, int>[] GetReferenceNodes(int[] addrNdxs, int maxLevel, out string error, ReferenceType refType = ReferenceType.Ancestors | ReferenceType.All)
        {
            error = null;
            try
            {
                var results = new List<KeyValuePair<IndexNode, int>>(addrNdxs.Length);
                var uniqueSet = new HashSet<int>();
                var que = new Queue<IndexNode>(256);
                long[] offsets = GetOffsets((ReferenceType.Ancestors & refType) != 0 ? RefFile.BwdOffsets : RefFile.FwdOffsets);
                BinaryReader br = GetReader((ReferenceType.Ancestors & refType) != 0 ? RefFile.BwdRefs : RefFile.FwdRefs);
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
                        int curndx = curNode.Index;
                        long offset = offsets[curndx];
                        int count = ReferenceCount(offset, offsets[curndx + 1]);
                        if (count == 0) continue;

                        var nodes = new IndexNode[count];
                        curNode.AddNodes(nodes);
                        var refs = ReadReferences(br, offset, count);
                        for (int i = 0; i < count; ++i)
                        {
                            var rNdx = refs[i];
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

        public KeyValuePair<IndexNode, int>[] GetMappedReferenceNodes(int[] addrNdxs, int maxLevel, out string error, ReferenceType refType = ReferenceType.Ancestors | ReferenceType.All)
        {
            error = null;
            try
            {
                var results = new List<KeyValuePair<IndexNode, int>>(addrNdxs.Length);
                var uniqueSet = new HashSet<int>();
                var que = new Queue<IndexNode>(256);
                var offAccessor = GetMapReader((ReferenceType.Ancestors & refType) != 0 ? RefFile.BwdOffsets : RefFile.FwdOffsets);
                var refAccessor = GetMapReader((ReferenceType.Ancestors & refType) != 0 ? RefFile.BwdRefs : RefFile.FwdRefs);
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
                        int curndx = curNode.Index;

                        (long offset, int count) = GetRefOffsetAndCount(offAccessor, curndx);
                        if (count == 0) continue;

                        var nodes = new IndexNode[count];
                        curNode.AddNodes(nodes);
                        var refs = ReadReferences(refAccessor, offset, count);
                        for (int i = 0; i < count; ++i)
                        {
                            var rNdx = refs[i];
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

        public KeyValuePair<IndexNode[], int> GetAncestors(int[] instanceNdxs, int maxLevel, out string error, ReferenceType refType = ReferenceType.Ancestors | ReferenceType.All)
        {
            error = null;
            try
            {
                IndexNode[] lst = new IndexNode[instanceNdxs.Length];

                int level = 0;

                int totalCount = instanceNdxs.Length;
                Queue<IndexNode> que = new Queue<IndexNode>(instanceNdxs.Length);

                long[] offsets = GetOffsets((ReferenceType.Ancestors & refType) != 0 ? RefFile.BwdOffsets : RefFile.FwdOffsets);
                BinaryReader br = GetReader((ReferenceType.Ancestors & refType) != 0 ? RefFile.BwdRefs : RefFile.FwdRefs);
                for (int i = 0, icnt = instanceNdxs.Length; i < icnt; ++i)
                {
                    int ndx = instanceNdxs[i];
                    long offset = offsets[ndx];
                    int count = ReferenceCount(offset, offsets[ndx + 1]);
                    totalCount += count;
                    if (count == 0)
                    {
                        lst[i] = new IndexNode(ndx, level);
                        continue;
                    }
                    IndexNode[] refs = ReadReferences(br, offset, count, level + 1, que);
                    lst[i] = new IndexNode(ndx, level, refs);
                }
                ++level;
                if (maxLevel <= level) return new KeyValuePair<IndexNode[], int>(lst, totalCount);

                while (que.Count > 0)
                {
                    var node = que.Dequeue();
                    if (node.Level >= maxLevel) continue;
                    int ndx = node.Index;
                    Debug.Assert(!node.HasReferences());
                    long offset = offsets[ndx];
                    int count = ReferenceCount(offset, offsets[ndx + 1]);
                    totalCount += count;
                    if (count == 0) continue;
                    IndexNode[] refs = ReadReferences(br, offset, count, node.Level + 1, que);
                    node.AddNodes(refs);
                }

                return new KeyValuePair<IndexNode[], int>(lst, totalCount);
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return new KeyValuePair<IndexNode[], int>(null, 0); ;
            }
        }

        public KeyValuePair<IndexNode[], int> GetMappedAncestors(int[] instanceNdxs, int maxLevel, out string error, ReferenceType refType = ReferenceType.Ancestors | ReferenceType.All)
        {
            error = null;
            try
            {
                IndexNode[] lst = new IndexNode[instanceNdxs.Length];

                int level = 0;

                int totalCount = instanceNdxs.Length;
                Queue<IndexNode> que = new Queue<IndexNode>(instanceNdxs.Length);

                var offAccessor = GetMapReader((ReferenceType.Ancestors & refType) != 0 ? RefFile.BwdOffsets : RefFile.FwdOffsets);
                var refAccessor = GetMapReader((ReferenceType.Ancestors & refType) != 0 ? RefFile.BwdRefs : RefFile.FwdRefs);
                for (int i = 0, icnt = instanceNdxs.Length; i < icnt; ++i)
                {
                    int ndx = instanceNdxs[i];
                    (long offset, int count) = GetRefOffsetAndCount(offAccessor, ndx);
                    totalCount += count;
                    if (count == 0)
                    {
                        lst[i] = new IndexNode(ndx, level);
                        continue;
                    }
                    IndexNode[] refs = ReadReferences(refAccessor, offset, count, level + 1, que);
                    lst[i] = new IndexNode(ndx, level, refs);
                }
                ++level;
                if (maxLevel <= level) return new KeyValuePair<IndexNode[], int>(lst, totalCount);

                while (que.Count > 0)
                {
                    var node = que.Dequeue();
                    if (node.Level >= maxLevel) continue;
                    int ndx = node.Index;
                    Debug.Assert(!node.HasReferences());
                    (long offset, int count) = GetRefOffsetAndCount(offAccessor, ndx);
                    totalCount += count;
                    if (count == 0) continue;
                    IndexNode[] refs = ReadReferences(refAccessor, offset, count, node.Level + 1, que);
                    node.AddNodes(refs);
                }

                return new KeyValuePair<IndexNode[], int>(lst, totalCount);
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return new KeyValuePair<IndexNode[], int>(null, 0); ;
            }
        }

        public KeyValuePair<IndexNode, int> GetAncestors(int instanceNdx, int maxLevel, out string error, ReferenceType refType = ReferenceType.Ancestors | ReferenceType.All)
        {
            error = null;
            try
            {
                int level = 0;

                Queue<IndexNode> que = new Queue<IndexNode>(128);

                long[] offsets = GetOffsets((ReferenceType.Ancestors & refType) != 0 ? RefFile.BwdOffsets : RefFile.FwdOffsets);
                BinaryReader br = GetReader((ReferenceType.Ancestors & refType) != 0 ? RefFile.BwdRefs : RefFile.FwdRefs);
                long offset = offsets[instanceNdx];
                int count = ReferenceCount(offset, offsets[instanceNdx + 1]);
                if (count == 0)
                {
                    return new KeyValuePair<IndexNode, int>(new IndexNode(instanceNdx, level), 1);
                }
                int totalCount = count + 1;
                IndexNode[] refs = ReadReferences(br, offset, count, level + 1, que);
                var rootNode = new IndexNode(instanceNdx, level, refs);
                HashSet<int> set = new HashSet<int>();
                set.Add(rootNode.Index);
                while (que.Count > 0)
                {
                    var node = que.Dequeue();
                    if (node.Level >= maxLevel) continue;
                    if (!set.Add(node.Index)) continue;
                    int ndx = node.Index;
                    Debug.Assert(!node.HasReferences());
                    offset = offsets[ndx];
                    count = ReferenceCount(offset, offsets[ndx + 1]);
                    if (count == 0) continue;
                    totalCount += count;
                    refs = ReadReferences(br, offset, count, node.Level + 1, que);
                    node.AddNodes(refs);
                }

                return new KeyValuePair<IndexNode, int>(rootNode, totalCount);
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return new KeyValuePair<IndexNode, int>(null, 0); ;
            }
        }

        public KeyValuePair<IndexNode, int> GetMappedAncestors(int instanceNdx, int maxLevel, out string error, ReferenceType refType = ReferenceType.Ancestors | ReferenceType.All)
        {
            error = null;
            try
            {
                int level = 0;

                Queue<IndexNode> que = new Queue<IndexNode>(128);

                var offAccessor = GetMapReader((ReferenceType.Ancestors & refType) != 0 ? RefFile.BwdOffsets : RefFile.FwdOffsets);
                var refAccessor = GetMapReader((ReferenceType.Ancestors & refType) != 0 ? RefFile.BwdRefs : RefFile.FwdRefs);
                (long offset, int count) = GetRefOffsetAndCount(offAccessor, instanceNdx);
                if (count == 0)
                {
                    return new KeyValuePair<IndexNode, int>(new IndexNode(instanceNdx, level), 1);
                }
                int totalCount = count + 1;
                IndexNode[] refs = ReadReferences(refAccessor, offset, count, level + 1, que);
                var rootNode = new IndexNode(instanceNdx, level, refs);
                HashSet<int> set = new HashSet<int>();
                set.Add(rootNode.Index);
                while (que.Count > 0)
                {
                    var node = que.Dequeue();
                    if (node.Level >= maxLevel) continue;
                    if (!set.Add(node.Index)) continue;
                    int ndx = node.Index;
                    Debug.Assert(!node.HasReferences());
                    (offset, count) = GetRefOffsetAndCount(offAccessor, instanceNdx);
                    if (count == 0) continue;
                    totalCount += count;
                    refs = ReadReferences(refAccessor, offset, count, node.Level + 1, que);
                    node.AddNodes(refs);
                }

                return new KeyValuePair<IndexNode, int>(rootNode, totalCount);
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return new KeyValuePair<IndexNode, int>(null, 0); ;
            }
        }

        public int[] GetReferences(int instNdx, out string error, ReferenceType refType = ReferenceType.Ancestors | ReferenceType.All)
        {
            error = null;
            long[] offsets = GetOffsets((ReferenceType.Ancestors & refType) != 0 ? RefFile.BwdOffsets : RefFile.FwdOffsets);
            BinaryReader br = GetReader((ReferenceType.Ancestors & refType) != 0 ? RefFile.BwdRefs : RefFile.FwdRefs);

            long offset = offsets[instNdx];
            int count = ReferenceCount(offset, offsets[instNdx + 1]);
            if (count == 0) return Utils.EmptyArray<int>.Value;

            return ReadReferences(br, offset, count);
        }

        public int[] GetMappedReferences(int instNdx, out string error, ReferenceType refType = ReferenceType.Ancestors | ReferenceType.All)
        {
            error = null;
            var offAccessor = GetMapReader((ReferenceType.Ancestors & refType) != 0 ? RefFile.BwdOffsets : RefFile.FwdOffsets);
            var refAccessor = GetMapReader((ReferenceType.Ancestors & refType) != 0 ? RefFile.BwdRefs : RefFile.FwdRefs);

            (long offset, int count) = GetRefOffsetAndCount(offAccessor, instNdx);
            if (count == 0) return Utils.EmptyArray<int>.Value;

            return ReadReferences(refAccessor, offset, count);
        }

        #endregion queries

        #region building

        public bool CreateForwardReferences(ClrHeap heap, out string error)
        {
            const string progressHeader = Constants.HeavyAsteriskHeader + "[FwdRefs] ";
            error = null;
            int nullCount = 0;
            BinaryWriter bwFwdRefs = null, bwFwdOffs = null;
            _reflagSet = new BitSet(_instances.Length);
            //_reflagSet = new HashSet<int>();
            try
            {
                _stopWatch = new Stopwatch();
                _stopWatch.Start();
                _progress?.Report(progressHeader + "Creating forward references data...");
                _reversedRefsCounts = new int[_instances.Length];
                _forwardRefsCounts = new int[_instances.Length];
                int lastInstanceNdx = _instances.Length - 1; // for binary search
                bwFwdRefs = GetWriter(RefFile.FwdRefs, FileMode.Create);
                bwFwdOffs = GetWriter(RefFile.FwdOffsets, FileMode.Create);
                long offset = 0L;

                var fieldAddrOffsetList = new List<ulong>(64);
                for (int i = 0, icnt = _instances.Length; i < icnt; ++i)
                {
                    var addr = Utils.RealAddress(_instances[i]);
                    var clrType = heap.GetObjectType(addr);
                    //Debug.Assert(clrType != null); // TODO JRD restore this
                    bwFwdOffs.Write(offset);

                    if (clrType == null) { ++nullCount; continue; }
                    if (TypeExtractor.IsExludedType(clrType.Name)) continue;

                    fieldAddrOffsetList.Clear();
                    clrType.EnumerateRefsOfObjectCarefully(addr, (address, off) =>
                    {
                        fieldAddrOffsetList.Add(address);
                    });
                    if (fieldAddrOffsetList.Count < 1) continue;

                    int acount = PreprocessParentRefs(addr, fieldAddrOffsetList);
                    if (acount == 0) continue;

                    offset += acount * sizeof(int);
                    _forwardRefsCounts[i] += acount;

                    int parentNdx = Utils.AddressSearch(_instances, addr);
                    Debug.Assert(parentNdx >= 0);
                    for (int j = 0; j < acount; ++j)
                    {
                        ulong childAddr = fieldAddrOffsetList[j];
                        int childNdx = Utils.AddressSearch(_instances, childAddr);
                        Debug.Assert(childNdx >= 0);
                        if (copy_addr_flags_check(_instances, parentNdx, childNdx))
                        {
                            _reflagSet.Set(childNdx);
                            //_reflagSet.Add(childNdx);
                        }
                        _reversedRefsCounts[childNdx] += 1; // update reversed references count
                        ++_totalReversedRefs;
                        bwFwdRefs.Write(childNdx);
                    }
                }
                bwFwdOffs.Write(offset);
                _progress?.Report(progressHeader + "Creating forward references data done. " + Utils.StopAndGetDurationStringAndRestart(_stopWatch));
                _progress?.Report(progressHeader + "UNEXPECTED NULLS, count: " + nullCount);
                return true;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                _progress?.Report(progressHeader + "EXCEPTION: " + ex.Message);
                return false;
            }
            finally
            {
                bwFwdRefs?.Close();
                bwFwdOffs?.Close();
            }
        }

        public bool BuildReveresedReferences()
        {
            const string progressHeader = Constants.HeavyAsteriskHeader + "[BwdRefs] ";
            BinaryReader brFwdRefs = null;
            BinaryWriter bwBwdRefs = null;
            try
            {
                Utils.ForceGcWithCompaction();
                _progress?.Report(progressHeader + "Creating reveresed references data...");
                Unsafe.FileWriter.CreateFileWithSize(_fileList[(int)RefFile.BwdRefs], _totalReversedRefs * sizeof(int), out _error);
                Unsafe.FileWriter.CreateFileWithSize(_fileList[(int)RefFile.BwdOffsets], (_instances.Length + 1) * sizeof(long), out _error);
                long[] bwdRefOffsets = WriteBwdOffsets(_reversedRefsCounts);
                if (bwdRefOffsets == null)
                {
                    return false;
                }
                int[] intBuf = new int[1024];
                brFwdRefs = GetReader(RefFile.FwdRefs, FileMode.Open);
                bwBwdRefs = GetWriter(RefFile.BwdRefs, FileMode.Create);
                long fwdOffset = 0L;
                Dictionary<int, List<int>> bwdRefsDct = new Dictionary<int, List<int>>();

                for (int i = 0, icnt = _forwardRefsCounts.Length; i < icnt; ++i)
                {
                    int fwdCount = _forwardRefsCounts[i];
                    if (fwdCount == 0) continue;
                    intBuf = CheckBufferSize(intBuf, fwdCount);
                    ReadReferences(brFwdRefs, fwdOffset, fwdCount, intBuf);
                    fwdOffset += fwdCount * sizeof(int);
                    for (int j = 0; j < fwdCount; ++j)
                    {
                        int childNdx = intBuf[j];
                        if (copy_addr_flags_check(_instances, i, childNdx))
                        {
                            if (_forwardRefsCounts[childNdx] > 0)
                                _reflagSet.Set(childNdx);
                                //_reflagSet.Add(childNdx);
                        }

                        int childRefCount = _reversedRefsCounts[childNdx];
                        List<int> lst;
                        if (bwdRefsDct.TryGetValue(childNdx, out lst))
                        {
                            lst.Add(i);
                            if (lst.Count == childRefCount)
                            {
                                WriteRefs(bwBwdRefs, bwdRefOffsets[childNdx], lst);
                                bwdRefsDct.Remove(childNdx);
                            }
                        }
                        else
                        {
                            if (childRefCount == 1)
                            {
                                WriteRef(bwBwdRefs, bwdRefOffsets[childNdx], i);
                            }
                            else
                            {
                                lst = new List<int>(childRefCount);
                                lst.Add(i);
                                bwdRefsDct.Add(childNdx, lst);
                            }
                        }
                    }
                }

                bwBwdRefs.Close();
                bwBwdRefs = null;

                // release some mem
                //
                _reversedRefsCounts = null;
                bwdRefOffsets = null;

                Utils.ForceGcWithCompaction();

                // set rooted flags on references
                //
                if (_reflagSet.Count > 0)
                {
                    _progress?.Report(progressHeader + "Start of reflagging, count: " + Utils.CountString(_reflagSet.Count));
                    var ary = _reflagSet.GetSetBitIndices();
                    //var ary = new int[_reflagSet.Count];
                    //_reflagSet.CopyTo(ary);
                    Debug.Assert(Utils.IsSorted(ary));
                    //Array.Sort(ary);
                    IntSterta bh = new IntSterta(ary.Length + 8, ary); // transfer reflag items to the binary heap, data is sorted so heap constraints are met
                    ary = null;
                    Utils.ForceGcWithCompaction();
                    var fwdRefOffsets = new long[_forwardRefsCounts.Length + 1];
                    // put forward offsets in the array for fast access
                    //
                    long offset = 0L;
                    for (int i = 0, icnt = _forwardRefsCounts.Length; i < icnt; ++i)
                    {
                        fwdRefOffsets[i] = offset;
                        offset += _forwardRefsCounts[i] * sizeof(int);
                    }
                    fwdRefOffsets[_forwardRefsCounts.Length] = offset;
                    while (bh.Count > 0)
                    {
                        int ndx = bh.Pop();
                        _reflagSet.Unset(ndx);
                        //_reflagSet.Remove(ndx);
                        int cnt = _forwardRefsCounts[ndx];
                        if (cnt < 1) continue; // this should not happen
                        intBuf = CheckBufferSize(intBuf, cnt);
                        offset = fwdRefOffsets[ndx];
                        ReadReferences(brFwdRefs, offset, cnt, intBuf);
                        for (int i = 0; i < cnt; ++i)
                        {
                            int childNdx = intBuf[i];
                            bool check = copy_addr_flags_check(_instances, ndx, childNdx);
                            if (check && _forwardRefsCounts[childNdx] > 0 && _reflagSet.Set(childNdx))
                            //if (check && _forwardRefsCounts[childNdx] > 0 && _reflagSet.Add(childNdx))
                                bh.Push(childNdx); // need to revisit this instance
                        }
                    }
                }

                _progress?.Report(progressHeader + "Saving instance array: " + Utils.CountString(_instances.Length));
                Utils.WriteUlongArray(_instanceFilePath, _instances, out _error);
                _instances = null; // release mem

                _progress?.Report(progressHeader + "Creating reversed references data done. " + Utils.StopAndGetDurationStringAndRestart(_stopWatch));
                return true;
            }
            catch (Exception ex)
            {
                _error = Utils.GetExceptionErrorString(ex);
                _progress?.Report(progressHeader + "EXCEPTION: " + ex.Message);
                return false;
            }
            finally
            {
                brFwdRefs?.Close();
                bwBwdRefs?.Close();
                _stopWatch?.Stop();
            }
        }

        private void WriteRefs(BinaryWriter bw, long offset, List<int> vals)
        {
            vals.Sort();
            bw.BaseStream.Seek(offset, SeekOrigin.Begin);
            for (int i = 0, icnt = vals.Count; i < icnt; ++i)
            {
                bw.Write(vals[i]);
            }
        }
        private void WriteRef(BinaryWriter bw, long offset, int val)
        {
            bw.BaseStream.Seek(offset, SeekOrigin.Begin);
            bw.Write(val);
        }

        private long[] WriteBwdOffsets(int[] reversedRefsCounts)
        {
            BinaryWriter bw = null;
            try
            {
                bw = GetWriter(RefFile.BwdOffsets, FileMode.Create);
                long[] bwdRefOffsets = new long[_instances.Length + 1];
                long offset = 0L;
                for (int i = 0, icnt = _reversedRefsCounts.Length; i < icnt; ++i)
                {
                    bw.Write(offset);
                    bwdRefOffsets[i] = offset;
                    offset += reversedRefsCounts[i] * sizeof(int);
                }
                bw.Write(offset);
                bw.Close();
                bwdRefOffsets[_reversedRefsCounts.Length] = offset;
                return bwdRefOffsets;
            }
            catch (Exception ex)
            {
                _error = Utils.GetExceptionErrorString(ex);
                return null;
            }
            finally
            {
                bw?.Close();
            }
        }

        public int PreprocessParentRefs(ulong parent, List<ulong> lst)
        {
            // assert lst has to be sorted TODO JRD
            int count = lst.Count;
            Debug.Assert(count > 0);
            lst.Sort(new Utils.AddressComparison());
            int i = 0, ndx = 0;
            for (; i < count; ++i)
                if (!Utils.SameRealAddresses(lst[i], parent)) break;
            if (i == count) return 0;
            ulong prevVal = 0UL;
            for (; i < count; ++i)
            {
                var val = lst[i];
                if (val == 0UL || val == parent || val == prevVal) continue;
                prevVal = val;
                lst[ndx++] = val;
            }
            return ndx;
        }

        bool copy_addr_flags_check(ulong[] ary, int from, int to)
        {
            ulong fromValFlg = (ary[from] & Utils.RootBits.Mask) & Utils.RootBits.NotRootMask;
            if (fromValFlg == 0UL) return false;
            ulong toVal = ary[to];
            ulong toValFlg = toVal & Utils.RootBits.Mask;
            bool check = false;
            if (((fromValFlg & Utils.RootBits.Rooted) != 0) && ((toValFlg & Utils.RootBits.Rooted) == 0)) check = true;
            else if (((fromValFlg & Utils.RootBits.Finalizer) != 0) && ((toValFlg & Utils.RootBits.Finalizer) == 0)) check = true;
            if (check)
                ary[to] = toVal | fromValFlg;
            return check;
        }

        #endregion building

        #region io

        public static bool DeleteInstanceReferenceFiles(int runtmNdx, DumpFileMoniker moniker, out string error)
        {
            error = null;
            try
            {
                string[] fileList = new string[(int)RefFile.Count];
                fileList[0] = moniker.GetFilePath(runtmNdx, Constants.MapRefFwdOffsetsFilePostfix);
                fileList[1] = moniker.GetFilePath(runtmNdx, Constants.MapFwdRefsFilePostfix);
                fileList[2] = moniker.GetFilePath(runtmNdx, Constants.MapRefBwdOffsetsFilePostfix);
                fileList[3] = moniker.GetFilePath(runtmNdx, Constants.MapBwdRefsFilePostfix);
                //fileList[4] = moniker.GetFilePath(runtmNdx, Constants.MapRefFwdDataFilePostfix);

                for (int i = 0, icnt = fileList.Length; i < icnt; ++i)
                {
                    if (File.Exists(fileList[i]))
                        File.Delete(fileList[0]);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return false;
            }
        }

        public static bool InstanceReferenceFilesAvailable(int runtmNdx, DumpFileMoniker moniker, out string error)
        {
            error = null;
            try
            {
                string[] fileList = new string[(int)RefFile.Count];
                fileList[0] = moniker.GetFilePath(runtmNdx, Constants.MapRefFwdOffsetsFilePostfix);
                fileList[1] = moniker.GetFilePath(runtmNdx, Constants.MapFwdRefsFilePostfix);
                fileList[2] = moniker.GetFilePath(runtmNdx, Constants.MapRefBwdOffsetsFilePostfix);
                fileList[3] = moniker.GetFilePath(runtmNdx, Constants.MapBwdRefsFilePostfix);

                for (int i = 0, icnt = fileList.Length; i < icnt; ++i)
                {
                    if (!File.Exists(fileList[i])) return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return false;
            }
        }

        private static int[] CheckBufferSize(int[] buf, int requiredSize)
        {
            int size = buf.Length;
            if (size >= requiredSize) return buf;
            while (requiredSize > size)
            {
                size += size / 2;
            }
            return new int[size];
        }

        #endregion io

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
                for (int i = 0, icnt = (int)RefFile.Count; i < icnt; ++i)
                {
                    _readers[i]?.Close();
                    _views[i]?.Dispose();
                    _maps[i]?.Dispose();
                }
            }

            // Free any unmanaged objects here.
            //
            _disposed = true;
        }

        ~InstanceReferences()
        {
            Dispose(false);
        }

        #endregion dispose

    }

}

