using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class TypeDependencyNode
	{
		int _ancestorTypeId;
		int _descendantTypeId;
		ulong[] _ancestorAddresses;
		ulong[] _descendantAddresses;

		static void BuildBranches(ulong[] instances, int[] typeIds, int ancestorTypeId, ulong[] ancestors, ulong[][] descendants)
		{
			
		}
	}
}
