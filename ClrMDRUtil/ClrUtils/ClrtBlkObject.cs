using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	public class ClrtBlkObject
	{
		private ulong _address;
		private bool _taken;
		private BlockingReason _blkReason;
		private int _owner;
		private int _recursionCnt;
		private int _typeId; // actual type
		private int[] _owners;
		private int[] _waiters;

		public int Owner => _owner;
		public int[] Owners => _owners;
		public int[] Waiters => _waiters;

		/// <summary>
		/// Create instance of our version BlockingObject.
		/// </summary>
		/// <param name="bo">Instance of BlockingObject from ClrHeap.</param>
		/// <param name="threads">List of threads sorted by their os andc managed ids. Internal thread id is the index 
		/// of a thread in this list.</param>
		public ClrtBlkObject(BlockingObject bo, ClrThread[] threads, KeyValuePair<ulong[],int[]> typeInfos)
		{
			_address = bo.Object;
			_recursionCnt = bo.RecursionCount;
			_blkReason = bo.Reason;
			_taken = bo.Taken;
			_owner = _taken && bo.HasSingleOwner ? GetThreadId(threads, bo.Owner) : Constants.InvalidIndex;
			_owners = GetThreadList(bo.Owners, threads);
			_waiters = GetThreadList(bo.Waiters, threads);
			_typeId = GetTypeId(_address, typeInfos.Key , typeInfos.Value);
		}

		public static int GetThreadId(ClrThread[] ary, ClrThread thrd)
		{
			if (thrd == null) return Constants.InvalidIndex;
			return Array.BinarySearch(ary, thrd, new ClrThreadCmp());
		}


		private int[] GetThreadList(IList<ClrThread> lst, ClrThread[] threads)
		{
			if (lst == null || lst.Count < 1) return Utils.EmptyArray<int>.Value;
			List<int> threadLst = new List<int>();
			for (int i = 0, icnt = lst.Count; i < icnt; ++i)
			{
				var id = GetThreadId(threads, lst[i]);
				if (id >= 0) threadLst.Add(id);
			}
			return threadLst.Count > 0 ? threadLst.ToArray() : Utils.EmptyArray<int>.Value;
		}

		private int GetTypeId(ulong addr, ulong[] instances, int[] types)
		{
			var idNdx = Array.BinarySearch(instances, addr);
			return idNdx < 0 ? Constants.InvalidIndex : types[idNdx];
		}

	}
}
