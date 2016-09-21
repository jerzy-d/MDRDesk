using System;
using System.Collections.Generic;
using System.IO.Pipes;
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

	    public TypeDependencyNode(int ancestorTypeId, ulong[] ancestors, int descendantTypeId, ulong[] descendants)
	    {
	        _ancestorTypeId = ancestorTypeId;
	        _ancestorAddresses = ancestors;
	        _descendantTypeId = descendantTypeId;
	        _descendantAddresses = descendants;
	    }

        /// <summary>
        /// Group ancestors and descendants by descendants type id;
        /// </summary>
        /// <param name="instances">All heap instance addreses.</param>
        /// <param name="typeIds">Type ids of instances.</param>
        /// <param name="ancestors">Ancestor addresses.They are expected to be sorted.</param>
        /// <param name="descendants">For each ancestor instance we have array of descendants.</param>
		static TypeDependencyNode[] BuildBranches(ulong[] instances, int[] typeIds, int ancestorTypeId, ulong[] ancestors, ulong[][] descendants)
		{
			Dictionary<int,KeyValuePair<List<ulong>,List<ulong>>> dct = new Dictionary<int, KeyValuePair<List<ulong>, List<ulong>>>();

            for (int i = 0, icnt = ancestors.Length; i < icnt; ++i)
            {
                for (int j = 0, jcnt = descendants[i].Length; j < jcnt; ++j)
                {
                    int instNdx = Array.BinarySearch(instances, descendants[i][j]);
                    int typeId = typeIds[instNdx];
                    KeyValuePair<List<ulong>, List<ulong>> kv;
                    if (dct.TryGetValue(typeId, out kv))
                    {
                        if (kv.Key[kv.Key.Count - 1] != ancestors[i]) kv.Key.Add(ancestors[i]);
                        kv.Value.Add(descendants[i][j]);
                    }
                    else
                    {
                        dct.Add(typeId,new KeyValuePair<List<ulong>, List<ulong>>(
                            new List<ulong>(16) { ancestors[i] },
                            new List<ulong>(16) { descendants[i][j] } 
                            ));
                    }
                }
            }
            TypeDependencyNode[] nodes = new TypeDependencyNode[dct.Count];
            int ndx = 0;
            foreach (KeyValuePair<int,KeyValuePair < List<ulong>,List < ulong >>> kv in dct)
            {
                nodes[ndx++] = new TypeDependencyNode(ancestorTypeId,kv.Value.Key.ToArray(),kv.Key,kv.Value.Value.ToArray());
            }
            return nodes;
		}
	}
}
