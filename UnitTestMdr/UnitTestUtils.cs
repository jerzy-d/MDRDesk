using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClrMDRIndex;

namespace UnitTestMdr
{
	[TestClass]
	public class UnitTestUtils
	{
		[TestMethod]
		public void TestArena()
		{
			const int bcnt = 10;
			const int bsize = 100;
			var arena = new Arena<KeyValuePair<ulong, int>>(bcnt, bsize);

			int x = 0;
			ulong u = 0UL;
			for (int i = 0, icnt = bcnt * bsize - 1; i < icnt; ++i)
			{
				arena.Add(new KeyValuePair<ulong, int>(u, x));
				++x;
				++u;
			}

			var acount = arena.Count;

			Assert.IsTrue(bcnt * bsize - 1 == arena.Count, "ClrMDRIndex.Arena: Unexpected arena count.");
			x = 0;
			u = 0UL;
			for (int i = 0, icnt = bcnt * bsize - 1; i < icnt; ++i)
			{
				var kv = arena.Get(i);
				Assert.IsTrue(kv.Key == u && kv.Value == x, "ClrMDRIndex.Arena: Get returns bad value.");
				++x;
				++u;
			}
		}

		[TestMethod]
		public void TestMisc()
		{
			var ndx = Int32.MinValue;

			var s1 = String.Format("0x{0:x8}", ndx);
			var s2 = String.Format("0x{0:x8}", 0xC0000000);
			var s3 = String.Format("0x{0:x8}", 0x80000000);
			var s4 = String.Format("0x{0:x8}", 0x40000000);
			var s5 = String.Format("0x{0:x8}", -1);

			Assert.IsTrue(s1 == s3);
		}

		[TestMethod]
		public void TestValueIndex()
		{
			var ndx = 0;
			var dnx = IndexValue.DecorateMultiparentIndex(ndx);
			var isMulti = IndexValue.IsMultiparentIndex(dnx);
			Assert.IsTrue(isMulti);
		}
	}
}
