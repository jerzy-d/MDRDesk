using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	/// <summary>
	/// Helper class to pass index info without need to reference ClrMDRIndex assembly.
	/// </summary>
	/// <remarks>Used mostly with F# functions.</remarks>
	public class IndexCurrentInfo
	{
		private ulong[] _instances; // list of all heap addresses
		private uint[] _sizes; // size of instances from Microsoft.Diagnostics.Runtime GetSize(Address objRef) method
		private int[] _instTypes; // ids of instance types
		private int[] _instSortedByTypes; // addresses sorted by types, for faster lookups
		private int[] _instTypeOffsets; // to speed up type addresses lookup 
		private ClrtTypes _clrtTypes; // type informations, see ClrtTypes class for details
		private string[] _stringIds; // ordered by string ids

		public IndexCurrentInfo(ulong[] instances, uint[] sizes, int[] instTypes, int[] instSortedByTypes,
			int[] instTypeOffsets, ClrtTypes clrtTypes, string[] stringIds)
		{
			_instances = instances;
			_sizes = sizes;
			_instTypes = instTypes;
			_instSortedByTypes = instSortedByTypes;
			_instTypeOffsets = instTypeOffsets;
			_clrtTypes = clrtTypes;
			_stringIds = stringIds;
		}

		public int GetTypeId(string typeName)
		{
			return _clrtTypes.GetTypeId(typeName);
		}

		public string GetTypeName(int typeId)
		{
			return _clrtTypes.GetName(typeId);
		}

		public KeyValuePair<string, int> GetTypeNameAndIdAtAddr(ulong addr)
		{
			var ndx = Array.BinarySearch(_instances, addr);
			if (ndx < 0)
				return new KeyValuePair<string, int>(Constants.Unknown, Constants.InvalidIndex);
			int typeId = _instTypes[ndx];
			string typeName = GetTypeName(typeId);
			return new KeyValuePair<string, int>(typeName, typeId);
		}

		public int GetTypeIdAtAddr(ulong addr)
		{
			var ndx = Array.BinarySearch(_instances, addr);
			if (ndx < 0) return Constants.InvalidIndex;
			return _instTypes[ndx];
		}

		public bool HasInternalAddresses(ClrType clrType)
		{
			return ClrtTypes.HasInternalAddresses(clrType);
		}
	}
}
