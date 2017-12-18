using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	//public class ClrtBlockingObjects
	//{
	//	private ClrtBlkObject[] _blockingObjects;

	//	//public static ClrtBlkObject[] GetClrtBlkObjects(BinaryReader br, int activeBlockCount)
	//	//{
			
	//	//}
	//}

	public class ClrtBlkObject
	{
		private ulong _address;
		private bool _taken;
		private BlockingReason _blkReason;
		private int _recursionCnt;
		private int _typeId; // actual type
		private int _index;


		public int Index => _index;
		public int TypeId => _typeId;
		public BlockingReason BlkReason => _blkReason;

        public readonly static ClrtBlkObject Invalid = new ClrtBlkObject(Constants.InvalidAddress, false, BlockingReason.None, -1, Constants.InvalidIndex, Constants.InvalidIndex);

        /// <summary>
        /// Create instance of our version BlockingObject.
        /// </summary>
        /// <param name="bo">Instance of BlockingObject from ClrHeap.</param>
        /// <param name="blockInfoNdx">Index of the owners,waiters information.</param>
        /// <param name="typeId">Type of this object.</param>
        public ClrtBlkObject(BlockingObject bo, int blockInfoNdx, int typeId)
		{
			_address = bo.Object;
			_recursionCnt = bo.RecursionCount;
			_blkReason = bo.Reason;
			_taken = bo.Taken;
			_index = blockInfoNdx;
			_typeId = typeId;
		}

		private ClrtBlkObject(ulong address, bool taken, BlockingReason blkReason, int recursionCnt, int typeId, int blockInfoNdx)
		{
			_address = address;
			_recursionCnt = recursionCnt;
			_blkReason = blkReason;
			_taken = taken;
			_index = blockInfoNdx;
			_typeId = typeId;
		}

        public string[] DataDescriptions(string[] typeNames)
        {
            string[] descr = new string[5];
            descr[0] = "Address:  " + Utils.RealAddressString(_address);
            descr[1] = "Recusion Count:  " + _recursionCnt.ToString();
            descr[2] = "Block Reason:  " + _blkReason.ToString();
            descr[3] = "Taken:  " + _taken.ToString();
            descr[4] = _typeId == Constants.InvalidIndex ? "Unknown Type" : typeNames[_typeId];
            return descr;
        }

		public void Dump(BinaryWriter bw)
		{
			bw.Write(_address);
			bw.Write(_recursionCnt);
			bw.Write((int)_blkReason);
			bw.Write(_index);
			bw.Write(_typeId);
			bw.Write(_taken);
		}

		public static ClrtBlkObject Load(BinaryReader bw)
		{
			ulong addr = bw.ReadUInt64();
			int recursionCnt = bw.ReadInt32();
			BlockingReason blkReason = (BlockingReason)bw.ReadInt32();
			int blockInfoNdx = bw.ReadInt32();
			int typeId = bw.ReadInt32();
			bool taken = bw.ReadBoolean();

			return new ClrtBlkObject(addr,taken,blkReason,recursionCnt,typeId,blockInfoNdx);
		}
	}

	public class BlockingObjectCmp : IComparer<BlockingObject>
	{
		public int Compare(BlockingObject a, BlockingObject b)
		{
			return a.Object < b.Object ? -1 : (a.Object > b.Object ? 1 : 0);
		}
	}

	public class BlockingObjectEqualityCmp : IEqualityComparer<BlockingObject>
	{
		public bool Equals(BlockingObject a, BlockingObject b)
		{
			return a.Object == b.Object;
		}
		public int GetHashCode(BlockingObject codeh)
		{
			return codeh.GetHashCode();
		}
	}
}
