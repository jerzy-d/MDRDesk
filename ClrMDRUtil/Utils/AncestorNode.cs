using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class AncestorNode
	{
		public enum SortAncestors
		{
			None,
			ByTypeName,
			ByteInstanceCount,
			ByteInstanceCountDesc,
		}

		public readonly int Level;
		public readonly int TypeId;
		public readonly string TypeName;
		private int[] _instances;
		public int[] Instances => _instances;
		private AncestorNode[] _ancestors;
		public AncestorNode[] Ancestors => _ancestors;

		public AncestorNode(int level, int typeId, string typeName, int[] instances)
		{
			Level = level;
			TypeId = typeId;
			TypeName = typeName;
			_instances = instances;
			_ancestors = Utils.EmptyArray<AncestorNode>.Value;
		}

		public void AddNodes(AncestorNode[] ancestors)
		{
			_ancestors = ancestors;
		}

		public void Sort(SortAncestors sortType)
		{
			switch (sortType)
			{
				case SortAncestors.ByteInstanceCountDesc:
					Array.Sort(_ancestors, new FrequencyCmpDesc());
					break;
				case SortAncestors.ByteInstanceCount:
					Array.Sort(_ancestors, new FrequencyCmp());
					break;
				default:
					Array.Sort(_ancestors, new TypeNameCmp());
					break;
			}
		}

		public override string ToString()
		{
			return "[" + _instances.Length + "] " + TypeName;
		}
		public class TypeNameCmp : IComparer<AncestorNode>
		{
			public int Compare(AncestorNode a, AncestorNode b)
			{
				return string.Compare(a.TypeName, b.TypeName, StringComparison.Ordinal);
			}
		}

		public class FrequencyCmp : IComparer<AncestorNode>
		{
			public int Compare(AncestorNode a, AncestorNode b)
			{
				return a._instances.Length < b._instances.Length ? -1 : (a._instances.Length > b._instances.Length ? 1 : 0);
			}
		}
		public class FrequencyCmpDesc : IComparer<AncestorNode>
		{
			public int Compare(AncestorNode a, AncestorNode b)
			{
				return a._instances.Length > b._instances.Length ? -1 : (a._instances.Length < b._instances.Length ? 1 : 0);
			}
		}

	}
}
