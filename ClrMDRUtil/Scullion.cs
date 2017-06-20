using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRUtil
{
	public class Scullion
	{
		string _folder;

		public Scullion(string folder, ulong[] instances)
		{
			_folder = folder;

		}

		public int PreprocessParentRefs(List<ulong> lst, ulong[] ary)
		{
			// assert lst has to be sorted TODO JRD
			
			var parent = lst[0];
			lst.Add(parent);
			int aryNdx = 0;
			for(int i = 1, icnt = lst.Count; i < icnt;  ++i)
			{
				if (lst[i] == parent || lst[i] == lst[i+1]) continue;
				ary[aryNdx++] = lst[i];
			}
			return aryNdx;
		} 

	}
}
