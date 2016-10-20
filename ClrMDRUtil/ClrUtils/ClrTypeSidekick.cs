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
		public TypeCategories Categories { get; }
		public List<ClrTypeSidekick> Fields { get; }
		public object Data { get; private set; }

		public bool IsInvalid => ClrType == null;

		public bool IsArray => Categories.First == TypeCategory.Reference && Categories.Second == TypeCategory.Array;

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
			Categories = TypeCategories.GetCategories(clrType);
			Fields = new List<ClrTypeSidekick>(0);
			Data = null;
		}

		public ClrTypeSidekick(ClrType clrType, TypeCategories cats)
		{
			ClrType = clrType;
			InstanceField = null;
			Categories = cats;
			Fields = new List<ClrTypeSidekick>(0);
			Data = null;
		}

		public ClrTypeSidekick(ClrType clrType, TypeCategories cats, ClrInstanceField instanceField)
		{
			ClrType = clrType;
			InstanceField = instanceField;
			Categories = cats;
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

		private static void GetReferenceTypeFieldSidekick(ClrHeap heap, ClrTypeSidekick clrs, ulong addr, ClrInstanceField fld)
		{
			var clrType = clrs.ClrType;
			var cats = clrs.Categories;
			switch (cats.First)
			{
				case TypeCategory.Reference:

					break;
				case TypeCategory.Struct:

					break;
			}
		}

		public static ClrTypeSidekick GeTypeSidekick(ClrHeap heap, ClrType clrType, TypeCategories cats, ulong addr)
		{
			Debug.Assert(clrType!=null);
			var clrs = new ClrTypeSidekick(clrType, cats, null);
			if (clrType.Fields == null || clrType.Fields.Count < 1) return clrs;
			for (int i = 0, icnt = clrType.Fields.Count; i < icnt; ++i)
			{
				switch (cats.First)
				{
					case TypeCategory.Reference:

						break;
					case TypeCategory.Struct:

						break;
				}
			}
			return clrs;
		}
	}

	public static class EmptyClrTypeSidekick
	{
		public static readonly ClrTypeSidekick Value = new ClrTypeSidekick(null,null);
	}
}
