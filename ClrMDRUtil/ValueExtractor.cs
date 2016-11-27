using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

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
	public enum TypeKind : int
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
		ReferenceKind =	0x00000100,
		StructKind =		0x00000200,
		PrimitiveKind =	0x00000300,
		EnumKind =			0x00000400,
		StringKind =		0x00000500,
		ArrayKind =		0x00000600,
		InterfaceKind =	0x00000700,

		// 2 MSB more detailed info
		Decimal =		0x00010000,
		DateTime =		0x00020000,
		TimeSpan =		0x00030000,
		Guid =			0x00040000,
		Exception =     0x00050000,
		Str =           0x00060000,
		SystemObject =	0x00070000,
		System__Canon = 0x00080000,
		Ary =           0x00090000,

		ClrElementTypeMask =		0x000000FF,
		MainTypeKindMask =			0x0000FF00,
		ParticularTypeKindMask =	0x7FFF0000,
	}

	public class TypeKinds
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TypeKind GetClrElementType(TypeKind kind)
		{
			return (kind & TypeKind.ClrElementTypeMask);
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
		public static bool IsArray(TypeKind kind)
		{
			return (GetMainTypeKind(kind) & kind) != 0;
		}

		public static TypeKind GetTypeKind(ClrType clrType)
		{
			var elemType = clrType.ElementType;
			var kind = TypeKinds.SetClrElementType(TypeKind.Unknown, elemType);
			switch (elemType)
			{
				case ClrElementType.Array:
				case ClrElementType.SZArray:
					return kind | TypeKind.ArrayKind;
				case ClrElementType.String:
					return kind | TypeKind.StringKind;
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
							return kind | TypeKind.Decimal;
						case "System.DateTime":
							return kind | TypeKind.DateTime;
						case "System.TimeSpan":
							return kind | TypeKind.TimeSpan;
						case "System.Guid":
							return kind | TypeKind.Guid;
						default:
							return kind;
					}
				case ClrElementType.Unknown:
					return TypeKind.Unknown;
				default:
					return kind | TypeKind.PrimitiveKind;
			}
		}
	}

	public struct TypeCategories
	{
		public readonly TypeCategory First;
		public readonly TypeCategory Second;
		public readonly ClrElementType ClrElement;

		TypeCategories(TypeCategory first, TypeCategory second, ClrElementType clrElement)
		{
			First = first;
			Second = second;
			ClrElement = clrElement;
		}

		public static TypeCategories GetCategories(ClrType clrType)
		{
			if (clrType == null) return new TypeCategories(TypeCategory.Uknown, TypeCategory.Uknown, ClrElementType.Unknown);
			switch (clrType.ElementType)
			{
				case ClrElementType.String:
					return new TypeCategories(TypeCategory.Reference, TypeCategory.String, clrType.ElementType);
				case ClrElementType.SZArray:
					return new TypeCategories(TypeCategory.Reference, TypeCategory.Array, clrType.ElementType);
				case ClrElementType.Object:
					if (clrType.IsException)
						return new TypeCategories(TypeCategory.Reference, TypeCategory.Exception, clrType.ElementType);
					if (Utils.SameStrings(clrType.Name, "System.Object"))
						return new TypeCategories(TypeCategory.Reference, TypeCategory.SystemObject, clrType.ElementType);
					if (Utils.SameStrings(clrType.Name, "System.__Canon"))
						return new TypeCategories(TypeCategory.Reference, TypeCategory.System__Canon, clrType.ElementType);
					if (clrType.IsArray)
						return new TypeCategories(TypeCategory.Reference, TypeCategory.Array, clrType.ElementType);
					if (clrType.IsInterface)
						return new TypeCategories(TypeCategory.Reference, TypeCategory.Interface, clrType.ElementType);
					return new TypeCategories(TypeCategory.Reference, TypeCategory.Reference, clrType.ElementType);
				case ClrElementType.Struct:
					if (Utils.SameStrings(clrType.Name, "System.Decimal"))
						return new TypeCategories(TypeCategory.Struct, TypeCategory.Decimal, clrType.ElementType);
					if (Utils.SameStrings(clrType.Name, "System.DateTime"))
						return new TypeCategories(TypeCategory.Struct, TypeCategory.DateTime, clrType.ElementType);
					if (Utils.SameStrings(clrType.Name, "System.TimeSpan"))
						return new TypeCategories(TypeCategory.Struct, TypeCategory.TimeSpan, clrType.ElementType);
					if (Utils.SameStrings(clrType.Name, "System.Guid"))
						return new TypeCategories(TypeCategory.Struct, TypeCategory.Guid, clrType.ElementType);
					if (clrType.IsInterface)
						return new TypeCategories(TypeCategory.Struct, TypeCategory.Interface, clrType.ElementType);
					return new TypeCategories(TypeCategory.Struct, TypeCategory.Struct, clrType.ElementType);
				case ClrElementType.Unknown:
					return new TypeCategories(TypeCategory.Uknown, TypeCategory.Uknown, clrType.ElementType);
				default:
					return new TypeCategories(TypeCategory.Primitive, TypeCategory.Primitive, clrType.ElementType);
			}
		}

	}

	public class ValueExtractor
	{
		public static bool Is64Bit = Environment.Is64BitOperatingSystem;

		//
		// System.Decimal
		//

		public static string GetDecimalValue(ulong parentAddr, ClrInstanceField field)
		{
			var addr = field.GetAddress(parentAddr, true);
			var flags = (int)field.Type.Fields[0].GetValue(addr);
			var hi = (int)field.Type.Fields[1].GetValue(addr);
			var lo = (int)field.Type.Fields[2].GetValue(addr);
			var mid = (int)field.Type.Fields[3].GetValue(addr);

			int[] bits = { lo, mid, hi, flags };
			decimal d = new decimal(bits);

			return d.ToString(CultureInfo.InvariantCulture);
		}

		public static decimal GetDecimal(ulong parentAddr, ClrInstanceField field)
		{
			var addr = field.GetAddress(parentAddr, true);
			var flags = (int)field.Type.Fields[0].GetValue(addr);
			var hi = (int)field.Type.Fields[1].GetValue(addr);
			var lo = (int)field.Type.Fields[2].GetValue(addr);
			var mid = (int)field.Type.Fields[3].GetValue(addr);

			int[] bits = { lo, mid, hi, flags };
			decimal d = new decimal(bits);

			return d;
		}

		public static string GetDecimalValue(ulong addr, ClrType type, string formatSpec)
		{
			decimal d = GetDecimalValue(addr, type);
			return formatSpec == null ? d.ToString(CultureInfo.InvariantCulture) : d.ToString(formatSpec);
		}

		public static decimal GetDecimalValue(ulong addr, ClrType type)
		{
			var flags = (int)type.Fields[0].GetValue(addr, true);
			var hi = (int)type.Fields[1].GetValue(addr, true);
			var lo = (int)type.Fields[2].GetValue(addr, true);
			var mid = (int)type.Fields[3].GetValue(addr, true);

			int[] bits = { lo, mid, hi, flags };
			return new decimal(bits);
		}

		//
		// System.String
		//

		public static string GetStringAtAddress(ulong addr, ClrHeap heap)
		{
			if (addr == 0UL) return Constants.NullName;
			var lenBuf = new byte[4];
			addr += (ulong)IntPtr.Size;
			heap.ReadMemory(addr, lenBuf, 0, 4);
			int len = BitConverter.ToInt32(lenBuf, 0) * sizeof(char);
			var strBuf = new byte[len];
			heap.ReadMemory(addr + 4, strBuf, 0, len);
			return Encoding.Unicode.GetString(strBuf);
		}


		public static ulong ReadUlongAtAddress(ulong addr, ClrHeap heap)
		{
			if (addr == 0UL) return Constants.InvalidAddress;
			var lenBuf = new byte[8];
			heap.ReadMemory(addr, lenBuf, 0, 8);
			ulong val = BitConverter.ToUInt64(lenBuf, 0);
			return val;
		}

		public static ulong ReadPointerAtAddress(ulong addr, ClrHeap heap)
		{
			if (addr == 0UL) return Constants.InvalidAddress;
			ulong ptr;
			if (heap.ReadPointer(addr, out ptr))
				return ptr;
			return Constants.InvalidAddress;
		}

		// TODO JRD -- check this one 
		public static string GetStringValue(ClrType clrType, ulong addr)
		{
			if (addr == Constants.InvalidAddress) return Constants.NullValue;
			ClrInstanceField instanceField;
			int fieldOffset;
			clrType.GetFieldForOffset(0, true, out instanceField, out fieldOffset);
			object fieldValue = null;
			if (instanceField != null)
			{
				fieldValue = instanceField.GetValue(addr);
			}
			var length = (int)(fieldValue ?? 0);
			if (length < 1)
				return Constants.EmptyStringValue;

			var charArray = new char[length + 2];

			ClrInstanceField charInstanceField;
			int charFieldOffset;
			clrType.GetFieldForOffset(4, true, out charInstanceField, out charFieldOffset);
			ulong offset = 0;
			charArray[0] = '\"';
			charArray[charArray.Length - 1] = '\"';
			for (var i = 0; i < length; ++i)
			{
				var charObject = charInstanceField.GetValue(addr + offset);
				var unicodeChar = (char)charObject;
				if (unicodeChar < 0x0020 || unicodeChar == 0x007F || unicodeChar > 0x1EFF)
					unicodeChar = '?';
				charArray[i + 1] = unicodeChar;
				offset += 2;
			}


			return new string(charArray);
		}

		//
		// System.DateTime
		//

		private const ulong TicksMask = 0x3FFFFFFFFFFFFFFF;

		// TODO JRD -- skipping firs entry in array
		public static string GetDateTimeValue(ulong addr, ClrType type, string formatSpec = null)
		{
			var data = (ulong)type.Fields[0].GetValue(addr);
			data = data & TicksMask;
			var dt = DateTime.FromBinary((long)data);
			return formatSpec == null ? dt.ToString(CultureInfo.InvariantCulture) : dt.ToString(formatSpec);
		}

		public static string GetDateTimeValue(ClrHeap heap, ulong addr, string formatSpec = null)
		{
			byte[] bytes = new byte[8];
			var read = heap.ReadMemory(addr, bytes, 0, 8);
			ulong data = BitConverter.ToUInt64(bytes, 0);
			data = data & TicksMask;
			try // might throw on bad data
			{
				var dt = DateTime.FromBinary((long)data);
				return formatSpec == null ? dt.ToString(CultureInfo.InvariantCulture) : dt.ToString(formatSpec);
			}
			catch (Exception)
			{
				return formatSpec == null ? DateTime.MinValue.ToString(CultureInfo.InvariantCulture) : DateTime.MinValue.ToString(formatSpec);
			}
		}

		public static string GetDateTimeValue(ulong addr, ClrInstanceField fld, bool internalPtr, string formatSpec = null)
		{
			ulong fldAddr = fld.GetAddress(addr, internalPtr);
			var data = (ulong)fld.Type.Fields[0].GetValue(fldAddr, true);
			data = data & TicksMask;
			var dt = DateTime.FromBinary((long)data);
			return formatSpec == null ? dt.ToString(CultureInfo.InvariantCulture) : dt.ToString(formatSpec);
		}

		public static DateTime GetDateTime(ulong addr, ClrInstanceField fld, bool internalPtr)
		{
			ulong fldAddr = fld.GetAddress(addr, internalPtr);
			var data = (ulong)fld.Type.Fields[0].GetValue(fldAddr, true);
			data = data & TicksMask;
			var dt = DateTime.FromBinary((long)data);
			return dt;
		}

		//
		// System.TimeSpan
		//
		// TODO JRD -- skipping first entry in arrays
		public static string GetTimeSpanValue(ulong addr, ClrType type)
		{
			var data = (long)type.Fields[0].GetValue(addr);
			var ts = TimeSpan.FromTicks(data);
			return ts.ToString("c");
		}

		public static string GetTimeSpanValue(ulong addr, ClrInstanceField fld) // TODO JRD -- check if this works
		{
			ulong fldAddr = fld.GetAddress(addr, true);
			var data = (long)fld.Type.Fields[0].GetValue(fldAddr, true);
			var ts = TimeSpan.FromTicks(data);
			return ts.ToString("c");
		}

		public static TimeSpan GetTimeSpan(ulong addr, ClrInstanceField fld) // TODO JRD -- check if this works
		{
			ulong fldAddr = fld.GetAddress(addr, true);
			var data = (long)fld.Type.Fields[0].GetValue(fldAddr, true);
			var ts = TimeSpan.FromTicks(data);
			return ts;
		}

		public static string GetTimeSpanValue(ClrHeap heap, ulong addr) // TODO JRD -- check if this works
		{
			byte[] bytes = new byte[8];
			var read = heap.ReadMemory(addr, bytes, 0, 8);
			long data = BitConverter.ToInt64(bytes, 0);
			try // might throw on bad data
			{
				var ts = TimeSpan.FromTicks(data);
				return ts.ToString("c");
			}
			catch (Exception)
			{
				return TimeSpan.MinValue.ToString("c");
			}
		}

		//
		// System.Guid
		//
		// TODO JRD -- bad
		public static string GetGuidValue(ulong addr, ClrType type)
		{
			StringBuilder sb = StringBuilderCache.Acquire(64);

			var ival = (int)type.Fields[0].GetValue(addr,true);
			sb.AppendFormat("{0:X8}", ival);
			sb.Append('-');
			var sval = (short)type.Fields[1].GetValue(addr,true);
			sb.AppendFormat("{0:X4}", sval);
			sb.Append('-');
			sval = (short)type.Fields[2].GetValue(addr,true);
			sb.AppendFormat("{0:X4}", sval);
			sb.Append('-');
			for (var i = 3; i < 11; ++i)
			{
				if (i == 5) sb.Append('-');
				var val = (byte)type.Fields[i].GetValue(addr,true);
				sb.AppendFormat("{0:X2}", val);
			}
			return StringBuilderCache.GetStringAndRelease(sb);
		}

		public static string GetGuidValue(ulong addr, ClrInstanceField field)
		{
			StringBuilder sb = StringBuilderCache.Acquire(64);
			var fldAddr = field.GetAddress(addr);
			if (fldAddr == 0UL) return Constants.NullValue;
			var ival = (int)field.Type.Fields[0].GetValue(fldAddr, true);
			sb.AppendFormat("{0:X8}", ival);
			sb.Append('-');
			var sval = (short)field.Type.Fields[1].GetValue(fldAddr, true);
			sb.AppendFormat("{0:X4}", sval);
			sb.Append('-');
			sval = (short)field.Type.Fields[2].GetValue(fldAddr, true);
			sb.AppendFormat("{0:X4}", sval);
			sb.Append('-');
			for (var i = 3; i < 11; ++i)
			{
				if (i == 5) sb.Append('-');
				var val = (byte)field.Type.Fields[i].GetValue(fldAddr, true);
				sb.AppendFormat("{0:X2}", val);
			}
			return StringBuilderCache.GetStringAndRelease(sb);
		}

		public static bool IsGuidEmpty(ulong addr, ClrInstanceField field)
		{
			var fldAddr = field.GetAddress(addr);
			if (fldAddr == 0UL) return true;
			var ival = (int)field.Type.Fields[0].GetValue(fldAddr, true);
			if (ival != 0) return false;
			var sval = (short)field.Type.Fields[1].GetValue(fldAddr, true);
			if (sval != 0) return false;
			sval = (short)field.Type.Fields[2].GetValue(fldAddr, true);
			if (sval != 0) return false;
			for (var i = 3; i < 11; ++i)
			{
				var val = (byte)field.Type.Fields[i].GetValue(fldAddr, true);
				if (val != 0) return false;
			}
			return true;
		}

		public static bool IsEmptyGuid(ulong addr, ClrType type)
		{
			var ival = (int)type.Fields[0].GetValue(addr, true);
			if (ival != 0) return false;
			var sval = (short)type.Fields[1].GetValue(addr, true);
			if (sval != 0) return false;
			sval = (short)type.Fields[2].GetValue(addr, true);
			if (sval != 0) return false;
			for (var i = 3; i < 11; ++i)
			{
				var val = (byte)type.Fields[i].GetValue(addr, true);
				if (val != 0) return false;
			}
			return true;
		}

		//
		// Exceptions
		//

		public static string GetExceptionValue(ulong addr, ClrType type, ClrHeap heap)
		{
			Debug.Assert(type.IsException);

			var classNameObj = type.GetFieldByName("_className").GetValue(addr);
			var classNameVal = classNameObj == null ? Constants.NullName : GetStringAtAddress((ulong)classNameObj, heap);

			var messageObj = type.GetFieldByName("_message").GetValue(addr);
			var messageVal = messageObj == null ? Constants.NullName : GetStringAtAddress((ulong)messageObj, heap);

			var stackTraceObj = type.GetFieldByName("_stackTrace").GetValue(addr);
			var stackTraceVal = stackTraceObj == null ? Constants.NullName : GetStringAtAddress((ulong)stackTraceObj, heap);

			var hresult = type.GetFieldByName("_HResult");
			Debug.Assert(hresult.ElementType == ClrElementType.Int32);
			int hresultValue = (int)hresult.GetValue(addr);

			var sb = StringBuilderCache.Acquire(64);
			sb.Append("[").Append(hresultValue).Append("] ");
			sb.Append(classNameVal).Append(Constants.NamespaceSepPadded);
			sb.Append(messageVal).Append(Constants.NamespaceSepPadded);
			sb.Append(Utils.ReplaceNewlines(stackTraceVal));
			return StringBuilderCache.GetStringAndRelease(sb);
		}

		public static string GetShortExceptionValue(ulong addr, ClrType type, ClrHeap heap)
		{
			Debug.Assert(type.IsException);

			if (Utils.IsInvalidAddress(addr)) return Constants.NullValue;
			var classNameObj = type.GetFieldByName("_className").GetValue(addr);
			var classNameVal = classNameObj == null ? Constants.NullValue : GetStringAtAddress((ulong)classNameObj, heap);

			var messageObj = type.GetFieldByName("_message").GetValue(addr);
			var messageVal = messageObj == null ? Constants.NullValue : GetStringAtAddress((ulong)messageObj, heap);

			var hresult = type.GetFieldByName("_HResult");
			Debug.Assert(hresult.ElementType == ClrElementType.Int32);
			int hresultValue = (int)hresult.GetValue(addr);

			var sb = StringBuilderCache.Acquire(64);
			sb.Append("[").Append(hresultValue).Append("] ");
			sb.Append(classNameVal).Append(Constants.NamespaceSepPadded);
			sb.Append(messageVal).Append(Constants.NamespaceSepPadded);
			return StringBuilderCache.GetStringAndRelease(sb);
		}

		//
		// Primitive types.
		//

		public static string GetPrimitiveValue(object obj, ClrType clrType)
		{
			switch (clrType.ElementType)
			{
				case ClrElementType.Unknown:
					return "unknown_type";
				case ClrElementType.Boolean:
					return ((bool)obj).ToString();
				case ClrElementType.Char:
					return ((char)obj).ToString();
				case ClrElementType.Int8:
					return ((Byte)obj).ToString();
				case ClrElementType.Int16:
					return ((Int16)obj).ToString();
				case ClrElementType.Int32:
					return ((Int32)obj).ToString();
				case ClrElementType.Int64:
					return ((Int64)obj).ToString();
				case ClrElementType.UInt8:
					return ((Byte)obj).ToString();
				case ClrElementType.UInt16:
					return ((UInt16)obj).ToString();
				case ClrElementType.UInt32:
					return ((UInt32)obj).ToString();
				case ClrElementType.UInt64:
					return ((UInt64)obj).ToString();
				case ClrElementType.Float:
				case ClrElementType.Double:
					return string.Format("0.0000", (Double)obj);

				case ClrElementType.Pointer:
					return "pointer: " + Utils.AddressString((UInt64)obj);
				case ClrElementType.NativeInt:
					return "native int: " + obj.ToString();
				case ClrElementType.NativeUInt:
					return "native uint: " + obj.ToString();
				case ClrElementType.String:
				case ClrElementType.FunctionPointer:
				case ClrElementType.Object:
				case ClrElementType.SZArray:
					return "NOT_PRIMITIVE: " + obj;
				default:
					return "uknown_element_type";
			}
		}

		public static bool IsPrimitiveValueDefault(ulong addr, ClrInstanceField field)
		{
			if (field.Type == null) return true;
			var obj = field.GetValue(addr);
			switch (field.ElementType)
			{
				case ClrElementType.Unknown:
					return false;
				case ClrElementType.Boolean:
					return ((bool)obj) == false;
				case ClrElementType.Char:
					return ((char)obj) == 0;
				case ClrElementType.Int8:
					return ((Byte)obj) == 0;
				case ClrElementType.Int16:
					return ((Int16)obj) == 0;
				case ClrElementType.Int32:
					return ((Int32)obj) == 0;
				case ClrElementType.Int64:
					return ((Int64)obj)==0;
				case ClrElementType.UInt8:
					return ((Byte)obj)==0;
				case ClrElementType.UInt16:
					return ((UInt16)obj)==0;
				case ClrElementType.UInt32:
					return ((UInt32)obj)==0UL;
				case ClrElementType.UInt64:
					return ((UInt64)obj)==0UL;
				case ClrElementType.Float:
				case ClrElementType.Double:
					return (Double)obj == 0.0;
				case ClrElementType.Pointer:
					return Is64Bit ? (UInt64)obj ==0 : (UInt32)obj==0;
				case ClrElementType.NativeInt:
					return (int)obj==0;
				case ClrElementType.NativeUInt:
					return (uint)obj==0;
				default:
					return true;
			}
		}
		public static int GetPrimitiveValueSize(ClrElementType elementType)
		{
			switch (elementType)
			{
				case ClrElementType.Unknown:
					return Constants.PointerSize;
				case ClrElementType.Boolean:
					return sizeof(bool);
				case ClrElementType.Char:
					return sizeof(char);
				case ClrElementType.Int8:
					return 1;
				case ClrElementType.Int16:
				case ClrElementType.UInt16:
					return 2;
				case ClrElementType.Int32:
				case ClrElementType.UInt32:
				case ClrElementType.Float:
				case ClrElementType.NativeInt:
				case ClrElementType.NativeUInt:
					return 4;
				case ClrElementType.Int64:
				case ClrElementType.UInt8:
				case ClrElementType.Double:
				case ClrElementType.UInt64:
					return 8;
				case ClrElementType.Pointer:
				case ClrElementType.String:
				case ClrElementType.FunctionPointer:
				case ClrElementType.Object:
				case ClrElementType.SZArray:
					return Constants.PointerSize;
				default:
					return Constants.PointerSize;
			}
		}


		public static string GetPrimitiveValue(object obj, ClrElementType elemType)
		{
			if (obj == null) return Constants.NullValue;
			if (ClrElementType.Float == elemType || ClrElementType.Double == elemType)
				return string.Format("0.0000", (Double)obj);

			if (ClrElementType.Boolean == elemType)
				return ((bool)obj).ToString();
			if (ClrElementType.Char == elemType)
				return ((char)obj).ToString();
			if (ClrElementType.Int8 == elemType)
				return ((Byte)obj).ToString();
			if (ClrElementType.Int16 == elemType)
				return ((Int16)obj).ToString();
			if (ClrElementType.Int32 == elemType)
				return ((Int32)obj).ToString();
			if (ClrElementType.Int64 == elemType)
				return ((Int64)obj).ToString();
			if (ClrElementType.UInt8 == elemType)
				return ((Byte)obj).ToString();
			if (ClrElementType.UInt16 == elemType)
				return ((UInt16)obj).ToString();
			if (ClrElementType.UInt32 == elemType)
				return ((UInt32)obj).ToString();
			if (ClrElementType.UInt64 == elemType)
				return ((UInt64)obj).ToString();
			if (ClrElementType.Pointer == elemType)
				return "pointer: " + $"{(UInt64)obj:x14}";
			if (ClrElementType.NativeInt == elemType)
				return "native int: " + obj.ToString();
			if (ClrElementType.NativeUInt == elemType)
				return "native uint: " + obj.ToString();
			if (ClrElementType.String == elemType
				|| ClrElementType.FunctionPointer == elemType
				|| ClrElementType.Object == elemType
				|| ClrElementType.SZArray == elemType)
				return "NOT_PRIMITIVE: " + obj;

			if (ClrElementType.Unknown == elemType)
				return "unknown_type";
			return "uknown_element_type";
		}


		public static string TryGetPrimitiveValue(ClrHeap heap, ulong classAddr, ClrInstanceField field, bool internalAddr)
		{
			var clrType = field.Type;
			var cat = TypeCategories.GetCategories(clrType);
			object addrObj;
			switch (cat.First)
			{
				case TypeCategory.Reference:
					switch (cat.Second)
					{
						case TypeCategory.String:
							addrObj = field.GetValue(classAddr, internalAddr, false);
							if (addrObj == null) return Constants.NullValue;
							return ValueExtractor.GetStringValue(clrType, (ulong)addrObj);
						case TypeCategory.Exception:
							addrObj = field.GetValue(classAddr, internalAddr, false);
							if (addrObj == null) return Constants.NullName;
							return ValueExtractor.GetShortExceptionValue((ulong)addrObj, clrType, heap);
						default:
							return Constants.NonValue;
					}
				case TypeCategory.Struct:
					switch (cat.Second)
					{
						case TypeCategory.Decimal:
							return ValueExtractor.GetDecimalValue(classAddr, field);
						case TypeCategory.DateTime:
							return ValueExtractor.GetDateTimeValue(classAddr, field, internalAddr, null);
						case TypeCategory.TimeSpan:
							addrObj = field.GetValue(classAddr, internalAddr, false);
							if (addrObj == null) return Constants.NullName;
							return ValueExtractor.GetTimeSpanValue((ulong)addrObj, clrType);
						case TypeCategory.Guid:
							return ValueExtractor.GetGuidValue(classAddr, field);
						default:
							return Constants.NonValue;
					}
				case TypeCategory.Primitive:
					addrObj = field.GetValue(classAddr, internalAddr, false);
					return ValueExtractor.GetPrimitiveValue(addrObj, clrType);
				default:
					return Constants.NonValue;
			}
		}


		public static void SetSegmentInterval(List<triple<bool, ulong, ulong>> intervals, ulong addr, ulong sz, bool free)
		{
			Debug.Assert(intervals.Count > 0 && sz > 0ul);
			var lstNdx = intervals.Count - 1;
			var last = intervals[lstNdx];
			bool lastFree = last.First;
			var lastAddr = last.Second + last.Third;

			var curLastAddr = addr + sz;
			if (curLastAddr <= lastAddr) return; // ?
			long diff = 0L;
			if (addr > lastAddr)
				diff = (long)addr - (long)lastAddr;
			else if (addr < lastAddr)
				diff = (long)lastAddr - (long)addr;

			if (lastFree && free) // append to last
			{
				last.Third = curLastAddr - last.Second;
				intervals[lstNdx] = last;
				return;
			}

			if (!lastFree && !free) // append to last or insert free followed by !free
			{
				if (diff > 0)
				{
					intervals.Add(new triple<bool, ulong, ulong>(true, lastAddr, (ulong)diff));
					intervals.Add(new triple<bool, ulong, ulong>(false, addr, sz));
					return;
				}
				last.Third = curLastAddr - last.Second;
				intervals[lstNdx] = last;
				return;
			}

			if (lastFree && !free)
			{
				if (diff > 0)
				{
					last.Third = last.Third + (ulong)diff;
					intervals[lstNdx] = last;
				}
				else if (diff < 0)
				{
					throw new ApplicationException("address less then last address");
				}
				intervals.Add(new triple<bool, ulong, ulong>(false, addr, sz));
				return;
			}

			if (!lastFree && free)
			{
				intervals.Add(new triple<bool, ulong, ulong>(false, lastAddr, curLastAddr - lastAddr));
				return;
			}

			throw new ApplicationException("Should not happen!");

		}

		public static void GetConcurrentDictionary(ClrHeap heap, ulong address, out string error)
		{
			error = null;
			try
			{
				var clrType = heap.GetObjectType(address);
				var m_tables_fld = clrType.GetFieldByName("m_tables");
				var m_growLockArray = GetPrimitiveValue(m_tables_fld.GetValue(address), ClrElementType.Boolean);
				var m_keyRehashCount = GetPrimitiveValue(m_tables_fld.GetValue(address), ClrElementType.Boolean);


			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
			}
		}
	}
}
