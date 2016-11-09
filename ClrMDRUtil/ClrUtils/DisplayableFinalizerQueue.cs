using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class DisplayableFinalizerQueue
	{
		public FinalizerQueueDisplayableItem[] Items { get; private set; }
		public int TotalCount { get; private set; }
		public int TotalUnrootedCount { get; private set; }
		public string Information { get; private set; }
	

		public DisplayableFinalizerQueue()
		{
			Items = Utils.EmptyArray<FinalizerQueueDisplayableItem>.Value;
		}

		public DisplayableFinalizerQueue(FinalizerQueueDisplayableItem[] items, int totalCount, int totalUnrootedCount, DumpFileMoniker fileMoniker)
		{
			Items = items;
			TotalCount = totalCount;
			TotalUnrootedCount = totalUnrootedCount;
			Information = "Crash dump: " + fileMoniker.DumpFileName
			              + Environment.NewLine + "Total count: " + Utils.CountString(totalCount)
			              + Environment.NewLine + "Total unrooted count: " + Utils.CountString(totalUnrootedCount);
		}
	}

	public class FinalizerQueueDisplayableItem
	{
		public string TotalCount { get; private set; }
		public string NotRootedCount { get; private set; }
		public string TypeName { get; private set; }
		public ulong Addresses { get; private set; }

		public FinalizerQueueDisplayableItem(int totCnt, int notRooted, string typeName, ulong[] addresses)
		{
			TotalCount = Utils.CountString(totCnt);
			NotRootedCount = Utils.CountString(notRooted);
			TypeName = typeName;
		}

		public static DisplayableFinalizerQueue GetDisplayableFinalizerQueue(ClrtRoot[] data, ulong[] instances, string[] typeNames, DumpFileMoniker fileMoniker)
		{
			if (data == null || data.Length < 1) return new DisplayableFinalizerQueue(); // empty

			int prevTypeId = data[0].TypeId;
			List<FinalizerQueueDisplayableItem> items = new List<FinalizerQueueDisplayableItem>(1024);
			List<KeyValuePair<int,int>> lst = new List<KeyValuePair<int, int>>(256);
			List<ulong> typeAddresses = new List<ulong>(1024);
			int totalCount = 0;
			int totalUnrootedCount = 0;
			int typeCount = 0;
			int notRooted = 0;
			for (int i = 0, icnt = data.Length; i < icnt; ++i)
			{
				if (prevTypeId != data[i].TypeId)
				{
					string typeName = prevTypeId < 0 ? Constants.UnknownTypeName : typeNames[prevTypeId];
					var item = new FinalizerQueueDisplayableItem(typeCount, notRooted, typeName, typeAddresses.ToArray());
					items.Add(item);
					typeCount = 0;
					notRooted = 0;
					lst.Clear();
					typeAddresses.Clear();
					prevTypeId = data[i].TypeId;
				}
				int instNdx = Utils.AddressSearch(instances, data[i].Object);
				ulong addr = instNdx < 0 ? data[i].Object : instances[instNdx];
				typeAddresses.Add(addr);
				++typeCount;
				++totalCount;
				if (Utils.IsNotRooted(addr))
				{
					++notRooted;
					++totalUnrootedCount;
				}
			}
			if (typeCount > 0)
			{
				string typeName = prevTypeId < 0 ? Constants.UnknownTypeName : typeNames[prevTypeId];
				var item = new FinalizerQueueDisplayableItem(typeCount, notRooted, typeName, typeAddresses.ToArray());
				items.Add(item);
			}

			return new DisplayableFinalizerQueue(items.ToArray(), totalCount, totalUnrootedCount,fileMoniker);
		}

	}
}
