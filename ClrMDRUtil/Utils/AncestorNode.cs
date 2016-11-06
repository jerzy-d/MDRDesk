using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
    public class AncestorNode
    {
        public readonly int Level;
        public readonly int TypeId;
        public readonly string TypeName;
        public readonly int[] Instances;
        private AncestorNode[] _ancestors;
        public AncestorNode[] Ancestors => _ancestors;

        public AncestorNode(int level, int typeId, string typeName, int[] instances)
        {
            Level = level;
            TypeId = typeId;
            TypeName = typeName;
            Instances = instances;
            _ancestors = Utils.EmptyArray<AncestorNode>.Value;
        }

        public void AddNodes(AncestorNode[] ancestors)
        {
            _ancestors = ancestors;
        }

        public override string ToString()
        {
            return "[" + Instances.Length + "] " + TypeName;
        }
    }
}
