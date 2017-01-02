using System;
using System.Runtime.CompilerServices;
using Microsoft.Diagnostics.Runtime;

// ReSharper disable InconsistentNaming

namespace ClrMDRIndex
{
	public enum TypeCategory
	{
		Uknown = 0,
		Reference = 1,
		Struct = 2,
		Primitive = 3,
		Enum = 4,
		String = 5,
		Array = 6,
		Decimal = 7,
		DateTime = 8,
		TimeSpan = 9,
		Guid = 10,
		Exception = 11,
		SystemObject = 12,
		System__Canon = 13,
		Interface = 14,
	}

	[Flags]
	public enum TypeKind
	{
		// LSB -- ClrElementType values
		Unknown = 0,
		Boolean = 2,
		Char = 3,
		Int8 = 4,
		UInt8 = 5,
		Int16 = 6,
		UInt16 = 7,
		Int32 = 8,
		UInt32 = 9,
		Int64 = 10,
		UInt64 = 11,
		Float = 12,
		Double = 13,
		String = 14,
		Pointer = 15,
		Struct = 17,
		Class = 18,
		Array = 20,
		NativeInt = 24,
		NativeUInt = 25,
		FunctionPointer = 27,
		Object = 28,
		SZArray = 29,

		// second LSB top level kinds
		ReferenceKind = 0x00000100,
		StructKind = 0x00000200,
		PrimitiveKind = 0x00000300,
		EnumKind = 0x00000400,
		StringKind = 0x00000500,
		ArrayKind = 0x00000600,
		InterfaceKind = 0x00000700,

		// 2 MSB more detailed info
		Decimal = 0x00010000,
		DateTime = 0x00020000,
		TimeSpan = 0x00030000,
		Guid = 0x00040000,
		Exception = 0x00050000,
		Str = 0x00060000,
		SystemObject = 0x00070000,
		System__Canon = 0x00080000,
		Ary = 0x00090000,
		Primitive = 0x000A0000,

		// our value kind
		ValueKind = 0x10000000,

		ClrElementTypeMask = 0x000000FF,
		MainTypeKindMask = 0x0000FF00,
		ParticularTypeKindMask = 0x0FFF0000,
		ValueTypeKindMask = 0x70000000,
	}

	public class TypeKinds
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ClrElementType GetClrElementType(TypeKind kind)
		{
			return (ClrElementType)(kind & TypeKind.ClrElementTypeMask);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TypeKind SetClrElementType(TypeKind kind, ClrElementType elemType)
		{
			return (kind | (TypeKind)elemType);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TypeKind GetMainTypeKind(TypeKind kind)
		{
			return (kind & TypeKind.MainTypeKindMask);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TypeKind SetMainTypeKind(TypeKind outKind, TypeKind kind)
		{
			return (outKind | kind);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TypeKind GetParticularTypeKind(TypeKind kind)
		{
			return (kind & TypeKind.ParticularTypeKindMask);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TypeKind SetParticularTypeKind(TypeKind outKind, TypeKind kind)
		{
			return (outKind | kind);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TypeKind GetValueTypeKind(TypeKind kind)
		{
			return (kind & TypeKind.ValueTypeKindMask);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsClassStruct(TypeKind kind)
		{
			var subKind = GetMainTypeKind(kind);
			switch (subKind)
			{
				case TypeKind.InterfaceKind:
				case TypeKind.ReferenceKind:
					return true;
				case TypeKind.StructKind:
					var specificKind = GetParticularTypeKind(kind);
					switch (specificKind)
					{
						case TypeKind.Decimal:
						case TypeKind.DateTime:
						case TypeKind.TimeSpan:
						case TypeKind.Guid:
							return false;
						default:
							return true;
					}
				default:
					return false;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsArray(TypeKind kind)
		{
			return (GetMainTypeKind(kind) & kind) == TypeKind.ArrayKind;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsValue(TypeKind kind)
		{
			return (GetValueTypeKind(kind) & kind) == TypeKind.ValueKind;
		}

		public static TypeKind GetTypeKind(ClrType clrType)
		{
			var elemType = clrType.ElementType;
			var kind = SetClrElementType(TypeKind.Unknown, elemType);
			switch (elemType)
			{
				case ClrElementType.Array:
				case ClrElementType.SZArray:
					return kind | TypeKind.ArrayKind;
				case ClrElementType.String:
					return kind | TypeKind.StringKind | TypeKind.ValueKind | TypeKind.Str;
				case ClrElementType.Object:
					kind |= TypeKind.ReferenceKind;
					if (clrType.IsException)
						return kind | TypeKind.Exception;
					switch (clrType.Name)
					{
						case "System.Object":
							return kind | TypeKind.Object;
						case "System.__Canon":
							return kind | TypeKind.System__Canon;
						default:
							return kind;
					}
				case ClrElementType.Struct:
					kind |= TypeKind.StructKind;
					switch (clrType.Name)
					{
						case "System.Decimal":
							return kind | TypeKind.Decimal | TypeKind.ValueKind;
						case "System.DateTime":
							return kind | TypeKind.DateTime | TypeKind.ValueKind;
						case "System.TimeSpan":
							return kind | TypeKind.TimeSpan | TypeKind.ValueKind;
						case "System.Guid":
							return kind | TypeKind.Guid | TypeKind.ValueKind;
						default:
							return kind;
					}
				case ClrElementType.Unknown:
					return TypeKind.Unknown;
				default:
					if (clrType.IsEnum)
						return kind | TypeKind.EnumKind | TypeKind.ValueKind | TypeKind.Primitive;
					return kind | TypeKind.PrimitiveKind | TypeKind.ValueKind | TypeKind.Primitive;
			}
		}
	}
}

// ReSharper restore InconsistentNaming
