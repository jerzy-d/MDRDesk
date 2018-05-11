using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public static class StringBuilderCache
	{
		private const int MAX_BUILDER_SIZE = 512; // ???

		public static int MaxCapacity => MAX_BUILDER_SIZE;

		[ThreadStatic]
		private static StringBuilder CachedInstance;

		public static StringBuilder Acquire(int capacity = 32)
		{
			if (capacity <= MAX_BUILDER_SIZE)
			{
				StringBuilder sb = StringBuilderCache.CachedInstance;
				if (sb != null)
				{
					if (capacity <= sb.Capacity)
					{
						StringBuilderCache.CachedInstance = null;
						sb.Clear();
						return sb;
					}
				}
			}
			return new StringBuilder(capacity);
		}

		public static void Release(StringBuilder sb)
		{
			if (sb.Capacity <= MAX_BUILDER_SIZE)
			{
				StringBuilderCache.CachedInstance = sb;
			}
		}

		public static string GetStringAndRelease(StringBuilder sb)
		{
			string result = sb.ToString();
			Release(sb);
			return result;
		}
	}
}
