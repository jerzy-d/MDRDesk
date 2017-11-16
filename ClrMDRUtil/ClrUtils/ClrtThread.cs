using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
    public class ClrtThread
    {
        [Flags]
        public enum Traits : int
        {
            Alive = 1,
            Finalizer = 1 << 1,
            GC = 1 << 2,
            DebuggerHelper = 1 << 3,
            ThreadpoolTimer = 1 << 4,
            ThreadpoolCompletionPort = 1 << 5,
            ThreadpoolWorker = 1 << 6,
            ThreadpoolWait = 1 << 7,
            ThreadpoolGate = 1 << 8,
            SuspendingEE = 1 << 9,
            ShutdownHelper = 1 << 10,
            AbortRequested = 1 << 11,
            Aborted = 1 << 12,
            GCSuspendPending = 1 << 13,
            UserSuspended = 1 << 14,
            DebugSuspended = 1 << 15,
            Background = 1 << 16,
            Unstarted = 1 << 17,
            CoInitialized = 1 << 18,
            STA = 1 << 19,
            MTA = 1 << 20,
            END = 1 << 21
        }

        private ulong _address;
        private int _traits;
        private uint _osId;
        private int _managedId;
        private uint _lockCount;
        private ulong _teb;
        private int[] _blkObjects;
        private int[] _frames;
        private ulong[] _frameStackPtrs;
        private int[] _liveStackObjects;
        private ulong _stackBase;
        private ulong _stackLimit;
        private ulong _exception;

        public int[] LiveStackObjects => _liveStackObjects;
        private int[] _deadStackObjects;
        public int[] DeadStackObjects => _deadStackObjects;

        public ulong Address => _address;
        public uint OSThreadId => _osId;
        public int ManagedThreadId => _managedId;
        public uint LockCount => _lockCount;
        public int[] BlockingObjects => _blkObjects;
        public int[] Frames => Frames1;

        public bool IsAlive => (_traits & (int)Traits.Alive) != 0;
        public bool IsGC => (_traits & (int)Traits.GC) != 0;
        public bool IsFinalizer => (_traits & (int)Traits.Finalizer) != 0;
        public bool IsThreadpoolWorker => (_traits & (int)Traits.ThreadpoolWorker) != 0;

        public int[] Frames1 { get => _frames; set => _frames = value; }

        public ClrtThread(ClrThread thrd, BlockingObject[] blkObjects, BlockingObjectCmp blkCmp)
        {
            _address = thrd.Address;
            _traits = GetTraits(thrd);
            _osId = thrd.OSThreadId;
            _managedId = thrd.ManagedThreadId;
            _lockCount = thrd.LockCount;
            _teb = thrd.Teb;
            _blkObjects = GetBlockingObjects(thrd.BlockingObjects, blkObjects, blkCmp);
            Frames1 = Utils.EmptyArray<int>.Value;
            _frameStackPtrs = Utils.EmptyArray<ulong>.Value;
            _liveStackObjects = Utils.EmptyArray<int>.Value;
            _deadStackObjects = Utils.EmptyArray<int>.Value;
            _stackBase = thrd.StackBase;
            _stackLimit = thrd.StackLimit;
            _exception = thrd.CurrentException != null ? thrd.CurrentException.Address : Constants.InvalidAddress;
        }

        public ClrtThread(ClrThread thrd, BlockingObject[] blkObjects, BlockingObjectCmp blkCmp, int[] frames, ulong[] stackPtrs, int[] aliveStackObjects, int[] deadStackObjects)
        {
            _address = thrd.Address;
            _traits = GetTraits(thrd);
            _osId = thrd.OSThreadId;
            _managedId = thrd.ManagedThreadId;
            _lockCount = thrd.LockCount;
            _teb = thrd.Teb;
            _blkObjects = GetBlockingObjects(thrd.BlockingObjects, blkObjects, blkCmp);
            Frames1 = frames;
            _frameStackPtrs = stackPtrs;
            _liveStackObjects = aliveStackObjects;
            _deadStackObjects = deadStackObjects;
            _stackBase = thrd.StackBase;
            _stackLimit = thrd.StackLimit;
            _exception = thrd.CurrentException != null ? thrd.CurrentException.Address : Constants.InvalidAddress;
        }

        public ClrtThread(ulong address,
                            int traits,
                            uint osId,
                            int managedId,
                            ulong teb,
                            uint lockCnt,
                            int[] blocks,
                            int[] frames,
                            ulong[] stackPtrs,
                            int[] aliveStackObjects,
                            int[] deadStackObjects,
                            ulong _stackBase,
                            ulong _stackLimit,
                            ulong _exception)
        {
            _address = address;
            _traits = traits;
            _osId = osId;
            _managedId = managedId;
            _lockCount = lockCnt;
            _teb = teb;
            _blkObjects = blocks;
            Frames1 = frames;
            _frameStackPtrs = stackPtrs;
            _liveStackObjects = aliveStackObjects;
            _deadStackObjects = deadStackObjects;
        }

        private int[] GetBlockingObjects(IList<BlockingObject> lst, BlockingObject[] blkObjects, BlockingObjectCmp blkCmp)
        {
            if (lst == null || lst.Count < 1) return Utils.EmptyArray<int>.Value;
            var waitLst = new List<int>();
            for (int i = 0, icnt = lst.Count; i < icnt; ++i)
            {
                var ndx = Array.BinarySearch(blkObjects, lst[i], blkCmp);
                if (ndx >= 0) waitLst.Add(ndx);
            }
            return waitLst.Count > 0 ? waitLst.ToArray() : Utils.EmptyArray<int>.Value;
        }

        public static int GetTraits(ClrThread t)
        {
            int traits = 0;
            traits |= t.IsAlive ? (int)Traits.Alive : 0;
            traits |= t.IsAbortRequested ? (int)Traits.AbortRequested : 0;
            traits |= t.IsAborted ? (int)Traits.Aborted : 0;
            traits |= t.IsAbortRequested ? (int)Traits.AbortRequested : 0;
            traits |= t.IsBackground ? (int)Traits.Background : 0;
            traits |= t.IsCoInitialized ? (int)Traits.CoInitialized : 0;
            traits |= t.IsDebugSuspended ? (int)Traits.DebugSuspended : 0;
            traits |= t.IsDebuggerHelper ? (int)Traits.DebuggerHelper : 0;
            traits |= t.IsFinalizer ? (int)Traits.Finalizer : 0;
            traits |= t.IsGC ? (int)Traits.GC : 0;
            traits |= t.IsGCSuspendPending ? (int)Traits.GCSuspendPending : 0;
            traits |= t.IsMTA ? (int)Traits.MTA : 0;
            traits |= t.IsSTA ? (int)Traits.STA : 0;
            traits |= t.IsShutdownHelper ? (int)Traits.ShutdownHelper : 0;
            traits |= t.IsSuspendingEE ? (int)Traits.SuspendingEE : 0;
            traits |= t.IsThreadpoolCompletionPort ? (int)Traits.ThreadpoolCompletionPort : 0;
            traits |= t.IsThreadpoolGate ? (int)Traits.ThreadpoolGate : 0;
            traits |= t.IsThreadpoolTimer ? (int)Traits.ThreadpoolTimer : 0;
            traits |= t.IsThreadpoolWait ? (int)Traits.ThreadpoolWait : 0;
            traits |= t.IsThreadpoolWorker ? (int)Traits.ThreadpoolWorker : 0;
            traits |= t.IsUnstarted ? (int)Traits.Unstarted : 0;
            traits |= t.IsUserSuspended ? (int)Traits.UserSuspended : 0;
            return traits;
        }

        public static string GetTraitsString(int traits)
        {
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
            int trait = 1;
            while (trait < (int)Traits.END)
            {
                if ((traits & trait) != 0)
                {
                    sb.Append((Traits)trait).Append(", ");
                }
                trait <<= 1;
            }
            if (sb.Length > 0) sb.Remove(sb.Length - 2, 2);
            if (Utils.StartsWith(sb, "Alive, "))
                sb.Remove(0, "Alive, ".Length);
            else
                sb.Insert(0, "Dead, ");
            return StringBuilderCache.GetStringAndRelease(sb);
        }

        public string GetTraitsString(StringBuilder sb)
        {
            int trait = 1;
            while (trait < (int)Traits.END)
            {
                if ((_traits & trait) != 0)
                {
                    sb.Append((Traits)trait).Append(", ");
                }
                trait <<= 1;
            }
            if (sb.Length > 0) sb.Remove(sb.Length - 2, 2);
            if (Utils.StartsWith(sb, "Alive, "))
                sb.Remove(0, "Alive, ".Length);
            else
                sb.Insert(0, "Dead, "); return sb.ToString();
        }

        public void Dump(BinaryWriter bw)
        {
            bw.Write(_address);
            bw.Write(_teb);
            bw.Write(_blkObjects.Length);
            for (int i = 0, icnt = _blkObjects.Length; i < icnt; ++i)
            {
                bw.Write(_blkObjects[i]);
            }
            bw.Write(_traits);
            bw.Write(_osId);
            bw.Write(_managedId);
            bw.Write(_lockCount);
            bw.Write(_stackBase);
            bw.Write(_stackLimit);
            bw.Write(_exception);

            bw.Write(Frames1.Length);
            for (int i = 0, icnt = Frames1.Length; i < icnt; ++i)
            {
                bw.Write(Frames1[i]);
            }
            bw.Write(_frameStackPtrs.Length);
            for (int i = 0, icnt = _frameStackPtrs.Length; i < icnt; ++i)
            {
                bw.Write(_frameStackPtrs[i]);
            }
            bw.Write(_liveStackObjects.Length);
            for (int i = 0, icnt = _liveStackObjects.Length; i < icnt; ++i)
            {
                bw.Write(_liveStackObjects[i]);
            }
            bw.Write(_deadStackObjects.Length);
            for (int i = 0, icnt = _deadStackObjects.Length; i < icnt; ++i)
            {
                bw.Write(_deadStackObjects[i]);
            }
        }

        public static ClrtThread Load(BinaryReader br)
        {
            ulong address = br.ReadUInt64();
            ulong teb = br.ReadUInt64();
            int[] blks = ReadIntArray(br);
            int traits = br.ReadInt32();
            uint osId = br.ReadUInt32();
            int managedId = br.ReadInt32();
            uint lockCnt = br.ReadUInt32();
            ulong stackBase = br.ReadUInt64();
            ulong stackLimits = br.ReadUInt64();
            ulong exception = br.ReadUInt64();

            int[] frames = ReadIntArray(br);
            ulong[] stackPtrs = ReadUlongArray(br);
            int[] aliveObjects = ReadIntArray(br);
            int[] deadObjects = ReadIntArray(br);

            return new ClrtThread(address, traits, osId, managedId, teb, lockCnt, blks, frames, stackPtrs, aliveObjects, deadObjects,stackBase,stackLimits,exception);
        }

        private static int[] ReadIntArray(BinaryReader br)
        {
            int cnt = br.ReadInt32();
            int[] ary = cnt == 0 ? Utils.EmptyArray<int>.Value : new int[cnt];
            for (int i = 0; i < cnt; ++i)
            {
                ary[i] = br.ReadInt32();
            }
            return ary;
        }

        private static ulong[] ReadUlongArray(BinaryReader br)
        {
            int cnt = br.ReadInt32();
            ulong[] ary = cnt == 0 ? Utils.EmptyArray<ulong>.Value : new ulong[cnt];
            for (int i = 0; i < cnt; ++i)
            {
                ary[i] = br.ReadUInt64();
            }
            return ary;
        }
    }

    public class ClrThreadCmp : IComparer<ClrThread>
    {
        public int Compare(ClrThread a, ClrThread b)
        {
            return a.Address < b.Address ? -1 : (a.Address > b.Address ? 1 : 0);
        }
    }

    public class ClrThreadEqualityCmp : IEqualityComparer<ClrThread>
    {
        public bool Equals(ClrThread a, ClrThread b)
        {
            return a.Address == b.Address;
        }
        public int GetHashCode(ClrThread t)
        {
            return t.Address.GetHashCode();
        }
    }
}
