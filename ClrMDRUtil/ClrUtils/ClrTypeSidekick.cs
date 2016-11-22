using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	public class ClrTypeSidekick
	{
		public ClrType ClrType { get; }
		public ClrInstanceField InstanceField { get; }
		public TypeKind Kind { get; }
		public List<ClrTypeSidekick> Fields { get; }
		public object Data { get; private set; }

		public bool IsInvalid => ClrType == null;

		public bool IsArray => TypeKinds.IsArray(Kind);

		public ClrTypeSidekick GetField(int ndx)
		{
			return Fields.Count > ndx ? Fields[ndx] : null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="clrType"></param>
		/// <param name="instanceField"></param>
		public ClrTypeSidekick(ClrType clrType, ClrInstanceField instanceField)
		{
			ClrType = clrType;
			InstanceField = instanceField;
			Kind = TypeKinds.GetTypeKind(clrType);
			Fields = new List<ClrTypeSidekick>(0);
			Data = null;
		}

		public ClrTypeSidekick(ClrType clrType, TypeKind kind)
		{
			ClrType = clrType;
			InstanceField = null;
			Kind = kind;
			Fields = new List<ClrTypeSidekick>(0);
			Data = null;
		}

		public ClrTypeSidekick(ClrType clrType, TypeKind kind, ClrInstanceField instanceField)
		{
			ClrType = clrType;
			InstanceField = instanceField;
			Kind = kind;
			Fields = new List<ClrTypeSidekick>(0);
			Data = null;
		}

		public void AddField(ClrTypeSidekick clrs)
		{
			Fields.Add(clrs);
		}

		public void AddField(ClrType clrType, ClrInstanceField instanceField)
		{
			var t = new ClrTypeSidekick(clrType,instanceField);
			Fields.Add(t);
		}

		public void SetData(object data)
		{
			Data = data;
		}
	}

	public static class EmptyClrTypeSidekick
	{
		public static readonly ClrTypeSidekick Value = new ClrTypeSidekick(null,null);
	}
}
