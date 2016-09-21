using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public static class IndexValue
	{
		[Flags]
		public enum IndexType : uint
		{
			HeapInst,
			Value =			0x40000000,
			Extra =			0x80000000,
			Multiparent =	0xC0000000,
			Invalid =		0xFFFFFFFF,
		}

		public const int InvalidIndex = -1;

		private const uint NDX_MASK = 0xC0000000;
		private const uint NDX_HEAPINST = 0x00000000;
		private const uint NDX_EXTRA = 0x80000000;
		private const uint NDX_VALUE = 0x40000000;
		private const uint NDX_MULTIPARENT = 0xC0000000;


		public static int GetIndex(int ndx)
		{
			return (int)((uint)ndx & ~NDX_MASK);
		}

		public static int DecorateMultiparentIndex(int ndx)
		{
			return (int)((uint)ndx | NDX_MULTIPARENT);
		}

		public static bool IsIndex(int ndx)
		{
			return ((uint)ndx & NDX_MASK) == 0;
		}

		public static bool IsMultiparentIndex(int ndx)
		{
			return ((uint)ndx & NDX_MASK) == NDX_MULTIPARENT;
		}

		public static bool IsInvalidIndex(int ndx)
		{
			return ndx == InvalidIndex;
		}

	}
}
