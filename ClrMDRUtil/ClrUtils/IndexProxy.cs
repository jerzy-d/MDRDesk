using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class IndexProxy
	{
		public readonly ClrtDump Dump;
		public readonly ulong[] Instances;
		public readonly int[] InstanceTypes;
        public readonly string[] TypeNames;
        public readonly ClrElementKind[] TypeKinds;
        public readonly ClrtRootInfo Roots;
		public readonly DumpFileMoniker FileMoniker;

		public static bool _is64Bit;
		public static bool Is64Bit => Environment.Is64BitProcess;
		public static uint WordSize => Environment.Is64BitProcess ? 8u : 4u;

		public IndexProxy(ClrtDump dump, ulong[] instances, int[] instanceTypes, string[] typeNames, ClrElementKind[] typeKinds, ClrtRootInfo roots, DumpFileMoniker fileMoniker)
		{
			Dump = dump;
			Instances = instances;
			InstanceTypes = instanceTypes;
			TypeNames = typeNames;
            TypeKinds = typeKinds;
            Roots = roots;
			FileMoniker = fileMoniker;
		}

		public int GetTypeId(string typeName)
		{
			var id = Array.BinarySearch(TypeNames,typeName,StringComparer.Ordinal);
			return id < 0 ? Constants.InvalidIndex : id;
		}

		public string GetTypeName(int typeId)
		{
			return typeId >= 0 && typeId < TypeNames.Length ? TypeNames[typeId] : Constants.UnknownTypeName;
		}

        public ClrElementKind GetTypeKind(int typeId)
        {
            return typeId >= 0 && typeId < TypeNames.Length ? TypeKinds[typeId] : ClrElementKind.Unknown;
        }

        public KeyValuePair<string, int> GetTypeNameAndIdAtAddr(ulong addr)
		{
			var ndx = Utils.AddressSearch(Instances, addr);
			if (ndx < 0)
				return new KeyValuePair<string, int>(Constants.Unknown, Constants.InvalidIndex);
			var typeId = InstanceTypes[ndx];
			var typeName = GetTypeName(typeId);
			return new KeyValuePair<string, int>(typeName, typeId);
		}

		public int GetTypeIdAtAddr(ulong addr)
		{
			var ndx = Utils.AddressSearch(Instances, addr);
			return ndx < 0 ? Constants.InvalidIndex : InstanceTypes[ndx];
		}

        public ulong[] GetTypeInstances(int typeId)
        {
            List<ulong> lst = new List<ulong>(1024);
            for (int i = 0, icnt = Instances.Length; i < icnt; ++i)
            {
                if (typeId == InstanceTypes[i]) lst.Add(Utils.RealAddress(Instances[i]));
            }
            lst.Sort();
            return lst.ToArray();
        }

    }
}
