﻿using System;
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

		public IndexProxy(ClrtDump dump, ulong[] instances, int[] instanceTypes, string[] typeNames)
		{
			Dump = dump;
			Instances = instances;
			InstanceTypes = instanceTypes;
			TypeNames = typeNames;
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
	}
}