using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClrMDRIndex;

namespace MDRDesk
{
	public class DisplayableGeneration
	{
		public string Instance { get; private set; }
		public string Generation0 { get; private set; }
		public string Generation1 { get; private set; }
		public string Generation2 { get; private set; }
		public string LOH { get; private set; }
		public string TotalCount { get; private set; }

		public DisplayableGeneration(string instanceType, int[] counts)
		{
			Debug.Assert(counts.Length>=4);
			Instance = instanceType;
			Generation0 = Utils.CountString(counts[0]);
			Generation1 = Utils.CountString(counts[1]);
			Generation2 = Utils.CountString(counts[2]);
			LOH = Utils.CountString(counts[3]);
			int total = counts[0] + counts[1] + counts[2] + counts[3];
			TotalCount = Utils.SizeString(total);
		}
		public DisplayableGeneration(string instanceType, ulong[] counts)
		{
			Debug.Assert(counts.Length >= 4);
			Instance = instanceType;
			Generation0 = Utils.SizeString(counts[0]);
			Generation1 = Utils.SizeString(counts[1]);
			Generation2 = Utils.SizeString(counts[2]);
			LOH = Utils.SizeString(counts[3]);
			ulong total = counts[0] + counts[1] + counts[2] + counts[3];
			TotalCount = Utils.SizeString(total);
		}
	}
}
