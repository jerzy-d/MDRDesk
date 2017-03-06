using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
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
		public readonly int ReferenceCount;
		public readonly int TypeId;
		public readonly string TypeName;
		public readonly AncestorNode Parent;
		private object _data;
		public object Data => _data;
		private int[] _instances;
		public int[] Instances => _instances;
		private AncestorNode[] _ancestors;
		public AncestorNode[] Ancestors => _ancestors;

		public AncestorNode(AncestorNode parent, int level, int referenceCount, int typeId, string typeName, int[] instances)
		{
			Parent = parent;
			Level = level;
			ReferenceCount = referenceCount;
			TypeId = typeId;
			TypeName = typeName;
			_data = null;
			_instances = instances;
			_ancestors = Utils.EmptyArray<AncestorNode>.Value;
		}

		public int AncestorInstanceCount()
		{
			int count = 0;
			for (int i = 0, icnt = _ancestors.Length; i < icnt; ++i)
			{
				count += _ancestors[i].Instances.Length;
			}
			return count;
		}

		public void AddNodes(AncestorNode[] ancestors)
		{
			_ancestors = ancestors;
		}

		public void AddData(object data)
		{
			_data = data;
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
			return "[" + Utils.CountString(_instances.Length) + 
				(ReferenceCount > 0 ? ("/" + Utils.CountString(ReferenceCount)) : string.Empty)
				+ "] " + TypeName;
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
