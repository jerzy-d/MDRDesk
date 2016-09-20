﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
    public class DependencyNode
    {
        private int _level;
        private int _typeId;
        private string _typeName;
        private string _field;
        private ulong[] _addresses;
        private ulong[][] _descendantMap;


        private DependencyNode[] _descendants;

        public ulong[] Addresses => _addresses;
        public ulong[][] DescendantMap => _descendantMap;
        public int Level => _level;
        public DependencyNode[] Descendants => _descendants;


        public override string ToString()
        {
            return "[" + _addresses.Length + "] (" + _field + ") " + _typeName;
        }

        public DependencyNode(int level, int typeId, string typeName, string fldName, ulong[] addresses)
        {
            _level = level;
            _typeId = typeId;
            _typeName = typeName;
            _field = fldName;
            _addresses = addresses;
            _descendantMap = Utils.EmptyArray<ulong[]>.Value;
            _descendants = Utils.EmptyArray<DependencyNode>.Value;
        }

        public void SetDescendants(DependencyNode[] nodes, ulong[][] groups)
        {
            _descendants = nodes;
            _descendantMap = groups;
        }

	    /// <summary>
	    /// Group ancestors and descendants by descendants type id;
	    /// </summary>
	    /// <param name="map">Index map.</param>
	    /// <param name="ancestor"></param>
	    /// <param name="ancestors">Ancestor addresses.They are expected to be sorted.</param>
	    /// <param name="error">Exception message if any.</param>
	    public static DependencyNode[] BuildBranches(Map map, DependencyNode ancestor, ulong[] ancestors, out string error)
        {
	        error = null;
	        try
	        {
				var typeRelationMap = new SortedDictionary<KeyValuePair<int, int>, quadruple<string, string, HashSet<ulong>, HashSet<ulong>>>(new Utils.KVIntIntCmp());
				var fldDpnds = map.FieldDependencies;
				for (int i = 0, icnt = ancestors.Length; i < icnt; ++i)
				{
					ulong ancestorAddr = ancestors[i];
					KeyValuePair<ulong, int>[] descendants = fldDpnds.GetFieldParents(ancestorAddr, out error);
					for (int j = 0, jcnt = descendants.Length; j < jcnt; ++j)
					{
						ulong descAddr = descendants[j].Key;
						KeyValuePair<string, int> typeInfo = map.GetTypeNameAndIdAtAddr(descAddr);
						string fldName = map.GetString(descendants[j].Value);
						KeyValuePair<int, int> key = new KeyValuePair<int, int>(typeInfo.Value, descendants[j].Value);
						quadruple<string, string, HashSet<ulong>, HashSet<ulong>> relation;
						if (typeRelationMap.TryGetValue(key, out relation))
						{
							relation.Third.Add(ancestorAddr);
							relation.Forth.Add(descAddr);
						}
						else
						{
							var ancsSet = new HashSet<ulong>() { ancestorAddr };
							var decsSet = new HashSet<ulong>() { descAddr };
							typeRelationMap.Add(key, new quadruple<string, string, HashSet<ulong>, HashSet<ulong>>(typeInfo.Key, fldName, ancsSet, decsSet));
						}
					}
				}

				DependencyNode[] nodes = new DependencyNode[typeRelationMap.Count];
				ulong[][] ancestorGrouping = new ulong[typeRelationMap.Count][];
				int ndx = 0;
				foreach (KeyValuePair<KeyValuePair<int, int>, quadruple<string, string, HashSet<ulong>, HashSet<ulong>>> kv in typeRelationMap)
				{
					ulong[] addresses = kv.Value.Forth.ToArray();
					Array.Sort(addresses);
					ancestorGrouping[ndx] = kv.Value.Third.ToArray();
					Array.Sort(ancestorGrouping[ndx]);
					nodes[ndx++] = new DependencyNode(ancestor.Level + 1, kv.Key.Key, kv.Value.First, kv.Value.Second, addresses);
				}

				if (ancestor != null) ancestor.SetDescendants(nodes, ancestorGrouping);
				return nodes;
			}
			catch (Exception ex)
	        {
		        error = Utils.GetExceptionErrorString(ex);
		        return null;
	        }

			//for (int i = 0, icnt = ancestors.Length; i < icnt; ++i)
			//   {
			//       for (int j = 0, jcnt = descendants.Length; j < jcnt; ++j)
			//       {
			//           var addrDescendants = descendants[j];
			//           for (int k = 0, kcnt = addrDescendants.Length; k < kcnt; ++k)
			//           {

			//               KeyValuePair<string, int> typeInfo = map.GetTypeNameAndIdAtAddr(addrDescendants[k].Key);
			//               string fldName = map.GetString(addrDescendants[k].Value);
			//               try
			//               {
			//                   KeyValuePair<int, int> key = new KeyValuePair<int, int>(typeInfo.Value, addrDescendants[k].Value);
			//                   quadruple<string, string, List<ulong>, List<ulong>> trio;
			//                   if (dct.TryGetValue(key, out trio))
			//                   {
			//                       trio.Third.Add(addrDescendants[k].Key);
			//                       if (!trio.Forth.Contains(ancestors[i])) trio.Forth.Add(ancestors[i]);
			//                   }
			//                   else
			//                   {
			//                       dct.Add(key, new quadruple<string, string, List<ulong>, List<ulong>>(
			//                           typeInfo.Key,
			//                           fldName,
			//                           new List<ulong>(8) { addrDescendants[k].Key },
			//                           new List<ulong>(8) { ancestors[i] }
			//                           ));
			//                   }
			//               }
			//               catch (Exception ex)
			//               {
			//                   int s = 0;
			//               }

			//           }

			//       }
			//   }

			//DependencyNode[] nodes = new DependencyNode[dct.Count];
   //         ulong[][] ancestorGrouping = new ulong[dct.Count][];
   //         int ndx = 0;
   //         foreach (KeyValuePair<KeyValuePair<int, int>, quadruple<string, string, List<ulong>, List<ulong>>> kv in dct)
   //         {
   //             kv.Value.Third.Sort();
   //             ancestorGrouping[ndx] = kv.Value.Forth.ToArray();
   //             nodes[ndx++] = new DependencyNode(ancestor.Level+1,kv.Key.Key, kv.Value.First, kv.Value.Second, kv.Value.Third.ToArray());
   //         }

   //         if (ancestor != null) ancestor.SetDescendants(nodes,ancestorGrouping);

   //         return nodes;
        }
    }
}