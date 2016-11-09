using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class AncestorDispRecord
	{
		private int _typeId;
		private string _typeName;
		private string _fieldName;
		private ulong[] _instances;

		public int TypeId => _typeId;
		public string TypeName => _typeName;
		public string FieldName => _fieldName;
		public ulong[] Instances => _instances;

		public AncestorDispRecord(int typeId, string typeName, string fieldName, ulong[] instances)
		{
			_typeId = typeId;
			_typeName = typeName;
			_fieldName = fieldName;
			_instances = instances;
		}

		public AncestorDispRecord(int typeId, string typeName, ulong[] instances)
		{
			_typeId = typeId;
			_typeName = typeName;
			_fieldName = string.Empty;
			_instances = instances;
		}

		public override string ToString()
		{
			return _typeName;
		}
	}

	public class AncestorDispRecordCmp : IComparer<AncestorDispRecord>
	{
		public int Compare(AncestorDispRecord a, AncestorDispRecord b)
		{
			//if (Utils.SameStrings(a.TypeName,b.TypeName))
			//{
			//	if (Utils.SameStrings(a.FieldName, b.FieldName))
			//	{
			//		return a.TypeId < b.TypeId ? -1 : (a.TypeId > b.TypeId ? 1 : 0);
			//	}
			//	return string.Compare(a.FieldName, b.FieldName, StringComparison.Ordinal);
			//}
			return string.Compare(a.TypeName, b.TypeName, StringComparison.Ordinal);
		}
	}
}
