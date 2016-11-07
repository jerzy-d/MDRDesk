using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClrMDRUtil.Utils;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	public class ClrtRootInfo
	{
		private readonly ulong[] _freachableQue; // is it? from Runtime.EnumerateFinalizerQueueObjectAddresses()

		public static ulong[] GetRootAddresses(ClrHeap heap)
		{
			var roots = heap.EnumerateRoots(true);
			var dct = new Dictionary<ulong, ulong>();

			foreach (var root in roots)
			{
				ulong rootAddr = root.Address;
				ulong objAddr = root.Object;
				if (rootAddr != 0Ul)
				{
					rootAddr = Utils.SetAsRoot(root.Address);
					ulong addr;
					if (dct.TryGetValue(root.Address, out addr))
					{
						dct[root.Address] = rootAddr | addr;
					}
					else
					{
						dct.Add(root.Address, rootAddr);
					}
				}
				if (objAddr != 0UL)
				{
					if (root.Kind == GCRootKind.Finalizer)
					{
						objAddr = Utils.SetAsFinalizer(root.Object);
					}
					objAddr = Utils.SetAsRoot(objAddr);
					ulong addr;
					if (dct.TryGetValue(root.Object, out addr))
					{
						dct[root.Object] = addr | objAddr;
					}
					else
					{
						dct.Add(root.Object, objAddr);
					}
				}
			}

			var addrAry = dct.Values.ToArray();
			var cmp = new Utils.AddressCmpAcs();
			Array.Sort(addrAry, cmp);
			return addrAry;
		}

	}
}
