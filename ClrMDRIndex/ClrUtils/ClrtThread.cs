using System;
using System.Collections.Generic;
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
			END = 1 <<21
		}

		private ulong _address;
		private int _traits;
		private uint _osId;
		private int _managedId;
		private uint _lockCount;
		private ulong _teb;
		private int[] _blkObjects;

		public ulong Address => _address;
		public uint OSThreadId => _osId;
		public int ManagedThreadId => _managedId;
		public uint LockCount => _lockCount;
		public int[] BlockingObjects => _blkObjects;

		public bool IsAlive => (_traits & (int)Traits.Alive) != 0;
		public bool IsGC => (_traits & (int)Traits.GC) != 0;
		public bool IsFinalizer => (_traits & (int)Traits.Finalizer) != 0;
		public bool IsThreadpoolWorker => (_traits & (int)Traits.ThreadpoolWorker) != 0;

		public ClrtThread(ClrThread thrd, ulong[] blkObjects)
		{
			_address = thrd.Address;
			_traits = GetTraits(thrd);
			_osId = thrd.OSThreadId;
			_managedId = thrd.ManagedThreadId;
			_lockCount = thrd.LockCount;
			_teb = thrd.Teb;
			_blkObjects = blkObjects != null ? GetBlockingObjects(thrd.BlockingObjects, blkObjects) : Utils.EmptyArray<int>.Value;
		}

		private int[] GetBlockingObjects(IList<BlockingObject> lst, ulong[] blkObjects)
		{
			if (lst == null || lst.Count < 1) return Utils.EmptyArray<int>.Value;
			var waitLst = new List<int>();
			for (int i = 0, icnt = lst.Count; i < icnt; ++i)
			{
				var ndx = Array.BinarySearch(blkObjects, lst[i].Object);
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
					sb.Append((Traits) trait).Append(", ");
				}
				trait <<= 1;
			}
			if (sb.Length > 0) sb.Remove(sb.Length - 2, 2);
			return StringBuilderCache.GetStringAndRelease(sb);
		}
	}
}
