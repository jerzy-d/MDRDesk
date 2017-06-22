using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
            Ancestors = 1,
            Descendants = 1 << 2,
            Rooted = 1 << 3,
            Unrooted = 1 << 4,
            All = 1 << 5,

        }

        enum RefFile
        {
            FwdOffsets,
            FwdRefs,
            BwdOffsets,
            BwdRefs,
            Count
        }

        string[] _fileList;
        ulong[] _instances;
        long[] _fwdOffsets;
        long[] _bwdOffsets;
        BinaryReader[] _readers;

        public InstanceReferences(ulong[] instances,  string[] fileList)
        {
            Debug.Assert(fileList.Length == (int)RefFile.Count);
            _fileList = fileList;
            _instances = instances;
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
                    int count = ReferenceCount(offset, offsets[ndx+1]);
                    totalCount += count;
                    if (count == 0)
                    {
                        lst[i] = new IndexNode(ndx, level);
                        continue;
                    }
                    IndexNode[] refs = ReadReferences(br, offset, count, level+1, que);
                    lst[i] = new IndexNode(ndx, level, refs);
                }
                ++level;
                if (maxLevel <= level) return new KeyValuePair<IndexNode[], int>(lst,totalCount);

                while(que.Count > 0)
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

        private IndexNode[] ReadReferences(BinaryReader br, long offset, int count, int level)
        {
            IndexNode[] refs = new IndexNode[count];
            br.BaseStream.Seek(offset, SeekOrigin.Begin);
            for (int i = 0, icnt = count; i < icnt; ++i)
            {
                var ndx = br.ReadInt32();
                refs[i] = new IndexNode(ndx, level);
            }
            return refs;
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

        private long[] GetOffsets(RefFile fileNdx)
        {
            switch(fileNdx)
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

        long[] ReadOffsets(RefFile fileNdx, int count)
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

        private BinaryReader GetReader(RefFile fileNdx)
        {
            if (_readers[(int)fileNdx] != null) return _readers[(int)fileNdx];
            _readers[(int)fileNdx] = new BinaryReader(File.Open(_fileList[(int)fileNdx], FileMode.Open, FileAccess.Read));
            return _readers[(int)fileNdx];
        }

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

