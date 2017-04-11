using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	public class ValueExtractor
	{
		public static bool Is64Bit = Environment.Is64BitOperatingSystem;


		//
		// System.Decimal
		//

		public static string GetDecimalValue(ulong parentAddr, ClrInstanceField field, bool intr)
		{
			var addr = field.GetAddress(parentAddr, intr);
			var flags = (int)field.Type.Fields[0].GetValue(addr,true);
			var hi = (int)field.Type.Fields[1].GetValue(addr, true);
			var lo = (int)field.Type.Fields[2].GetValue(addr, true);
			var mid = (int)field.Type.Fields[3].GetValue(addr, true);

			int[] bits = { lo, mid, hi, flags };
			decimal d = new decimal(bits);

			return d.ToString(CultureInfo.InvariantCulture);
		}

		public static decimal GetDecimal(ulong parentAddr, ClrInstanceField field, bool intr)
		{
			var addr = field.GetAddress(parentAddr, intr);
			var flags = (int)field.Type.Fields[0].GetValue(addr, true);
			var hi = (int)field.Type.Fields[1].GetValue(addr, true);
			var lo = (int)field.Type.Fields[2].GetValue(addr, true);
			var mid = (int)field.Type.Fields[3].GetValue(addr, true);

			int[] bits = { lo, mid, hi, flags };
			decimal d = new decimal(bits);

			return d;
		}

		public static string GetDecimalValueR(ulong addr, ClrType type, string formatSpec)
		{
			decimal d = GetDecimalValueR(addr, type);
			return formatSpec == null ? d.ToString(CultureInfo.InvariantCulture) : d.ToString(formatSpec);
		}

		public static decimal GetDecimalValueR(ulong addr, ClrType type)
		{
			var flags = (int)type.Fields[0].GetValue(addr, true);
			var hi = (int)type.Fields[1].GetValue(addr, true);
			var lo = (int)type.Fields[2].GetValue(addr, true);
			var mid = (int)type.Fields[3].GetValue(addr, true);

			int[] bits = { lo, mid, hi, flags };
			return new decimal(bits);
		}

		public static string GetDecimalValue(ulong addr, ClrType type, string formatSpec)
		{
			decimal d = GetDecimalValue(addr, type);
			return formatSpec == null ? d.ToString(CultureInfo.InvariantCulture) : d.ToString(formatSpec);
		}

		public static decimal GetDecimalValue(ulong addr, ClrType type)
		{
			var flags = (int)type.Fields[0].GetValue(addr, false);
			var hi = (int)type.Fields[1].GetValue(addr, false);
			var lo = (int)type.Fields[2].GetValue(addr, false);
			var mid = (int)type.Fields[3].GetValue(addr, false);

			int[] bits = { lo, mid, hi, flags };
			return new decimal(bits);
		}

		public static void Swap(int[] ary, int i1, int i2)
		{
			var temp = ary[i1];
			ary[i1] = ary[i2];
			ary[i2] = temp;
		}

		public static decimal GetDecimalValue(ClrHeap heap, ulong addr)
		{
			int[] bits = ReadIntAryAtAddress(addr, 4, heap);
			// flags, hi, lo, mid 
			Swap(bits,0,3);
			// mid, hi, lo, flags 
			Swap(bits, 2, 0);
			// lo, hi,mid, flags 
			Swap(bits, 1, 2);
			// lo, mid, hi, flags
			return new decimal(bits);
		}

		public static string GetDecimalValue(ClrHeap heap, ulong addr, string formatSpec)
		{
			decimal d = GetDecimalValue(heap, addr);
			return formatSpec == null ? d.ToString(CultureInfo.InvariantCulture) : d.ToString(formatSpec);
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

		public static int[] ReadIntAryAtAddress(ulong addr, int count, ClrHeap heap)
		{
			if (addr == 0UL) return null;
			var lenBuf = new byte[4];
			int[] result = new int[count];
			for (int i = 0; i < count; ++i)
			{
				heap.ReadMemory(addr, lenBuf, 0, 4);
				result[i] = BitConverter.ToInt32(lenBuf, 0);
				addr += 4;
			}
			return result;
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
		public static string GetDateTimeValue(ulong addr, ClrType type, bool intr, string formatSpec = null)
		{
			var data = (ulong)type.Fields[0].GetValue(addr,intr);
			data = data & TicksMask;
			var dt = DateTime.FromBinary((long)data);
			return formatSpec == null ? dt.ToString(CultureInfo.InvariantCulture) : dt.ToString(formatSpec);
		}

		//public static string GetDateTimeValue(ClrHeap heap, ulong addr, string formatSpec = null)
		//{
		//	byte[] bytes = new byte[8];
		//	heap.ReadMemory(addr, bytes, 0, 8);
		//	ulong data = BitConverter.ToUInt64(bytes, 0);
		//	data = data & TicksMask;
		//	try // might throw on bad data
		//	{
		//		var dt = DateTime.FromBinary((long)data);
		//		return formatSpec == null ? dt.ToString(CultureInfo.InvariantCulture) : dt.ToString(formatSpec);
		//	}
		//	catch (Exception)
		//	{
		//		return formatSpec == null ? DateTime.MinValue.ToString(CultureInfo.InvariantCulture) : DateTime.MinValue.ToString(formatSpec);
		//	}
		//}

		public static string GetDateTimeValue(ulong addr, ClrType clrType, string formatSpec = null)
		{
			var data = (ulong)clrType.Fields[0].GetValue(addr, false);
			data = data & TicksMask;
			var dt = DateTime.FromBinary((long)data);
			return formatSpec == null ? dt.ToString(CultureInfo.InvariantCulture) : dt.ToString(formatSpec);
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
			var data = (long)type.Fields[0].GetValue(addr,true);
			var ts = TimeSpan.FromTicks(data);
			return ts.ToString("c");
		}

		public static string GetTimeSpanValue(ulong addr, ClrInstanceField fld,bool intr) // TODO JRD -- check if this works
		{
			ulong fldAddr = fld.GetAddress(addr, intr);
			var data = (long)fld.Type.Fields[0].GetValue(fldAddr, true);
			var ts = TimeSpan.FromTicks(data);
			return ts.ToString("c");
		}

		public static TimeSpan GetTimeSpan(ulong addr, ClrInstanceField fld,bool intr) // TODO JRD -- check if this works
		{
			ulong fldAddr = fld.GetAddress(addr, intr);
			var data = (long)fld.Type.Fields[0].GetValue(fldAddr, true);
			var ts = TimeSpan.FromTicks(data);
			return ts;
		}

		public static string GetTimeSpanValue(ClrHeap heap, ulong addr) // TODO JRD -- check if this works
		{
			byte[] bytes = new byte[8];
			heap.ReadMemory(addr, bytes, 0, 8);
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

			var ival = (int)type.Fields[0].GetValue(addr, true);
			sb.AppendFormat("{0:X8}", ival);
			sb.Append('-');
			var sval = (short)type.Fields[1].GetValue(addr, true);
			sb.AppendFormat("{0:X4}", sval);
			sb.Append('-');
			sval = (short)type.Fields[2].GetValue(addr, true);
			sb.AppendFormat("{0:X4}", sval);
			sb.Append('-');
			for (var i = 3; i < 11; ++i)
			{
				if (i == 5) sb.Append('-');
				var val = (byte)type.Fields[i].GetValue(addr, true);
				sb.AppendFormat("{0:X2}", val);
			}
			return StringBuilderCache.GetStringAndRelease(sb);
		}

		public static string GetGuidValue(ulong addr, ClrInstanceField field,bool intr)
		{
			StringBuilder sb = StringBuilderCache.Acquire(64);
			var fldAddr = field.GetAddress(addr,intr);
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

		public static bool IsGuidEmpty(ulong addr, ClrInstanceField field,bool intr)
		{
			var fldAddr = field.GetAddress(addr,intr);
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

			var exType = heap.GetExceptionObject(addr);
			if (exType == null)
			{
				return "Cannot get exception object";
			}

			var sb = StringBuilderCache.Acquire(64);
			var exTypeName = exType.Type != null ? exType.Type.Name : Constants.UnknownTypeName;
			var hresult = exType.HResult;
			var message = exType.Message;
			sb.Append("HRESULT: ").AppendLine(Utils.HResultStringHeader(hresult));
			sb.Append("MESSAGE: ").AppendLine(message);
			sb.Append("TYPE:    ").Append(exTypeName);


			return StringBuilderCache.GetStringAndRelease(sb);

			//var classNameObj = type.GetFieldByName("_className").GetValue(addr);
			//var classNameVal = classNameObj == null ? Constants.NullName : GetStringAtAddress((ulong)classNameObj, heap);

			//var messageObj = type.GetFieldByName("_message").GetValue(addr);
			//var messageVal = messageObj == null ? Constants.NullName : GetStringAtAddress((ulong)messageObj, heap);

			//var stackTraceObj = type.GetFieldByName("_stackTrace").GetValue(addr);
			//var stackTraceVal = stackTraceObj == null ? Constants.NullName : GetStringAtAddress((ulong)stackTraceObj, heap);

			//var hresult = type.GetFieldByName("_HResult");
			//Debug.Assert(hresult.ElementType == ClrElementType.Int32);
			//int hresultValue = (int)hresult.GetValue(addr);

			//var sb = StringBuilderCache.Acquire(64);
			//sb.Append("[").Append(hresultValue).Append("] ");
			//sb.Append(classNameVal).Append(Constants.NamespaceSepPadded);
			//sb.Append(messageVal).Append(Constants.NamespaceSepPadded);
			//sb.Append(Utils.ReplaceNewlines(stackTraceVal));
			//return StringBuilderCache.GetStringAndRelease(sb);
		}

		public static string GetShortExceptionValue(ulong addr, ClrType type, ClrHeap heap)
		{
			Debug.Assert(type.IsException);

			var exType = heap.GetExceptionObject(addr);
			string exTypeName = null;
			int hresult = 0;
			string message = null;
			var sb = StringBuilderCache.Acquire(64);
			if (exType != null)
			{
				exTypeName = exType.Type != null ? exType.Type.Name : Constants.UnknownTypeName;
				hresult = exType.HResult;
				message = exType.Message;
				sb.Append(Utils.HResultStringHeader(hresult)).Append(Constants.NamespaceSepPadded);
                if (!string.IsNullOrEmpty(message))
				    sb.Append("MESSAGE: ").Append(message).Append(Constants.NamespaceSepPadded);
				sb.Append("TYPE: ").Append(exTypeName);
				return StringBuilderCache.GetStringAndRelease(sb);
			}



			if (Utils.IsInvalidAddress(addr)) return Constants.NullValue;
			var classNameFld = type.GetFieldByName("_className");
			var classNameObj = classNameFld.GetValue(addr, false, true);
			var classNameVal = classNameObj == null ? Constants.NullValue : classNameObj.ToString();

			var messageFld = type.GetFieldByName("_message");
			var messageObj = messageFld.GetValue(addr, false, true);
			var messageVal = messageObj == null ? Constants.NullValue : messageObj.ToString();

			var hresultFld = type.GetFieldByName("_HResult");
			Debug.Assert(hresultFld.ElementType == ClrElementType.Int32);
			int hresultValue = (int)hresultFld.GetValue(addr);

			sb.Append(Utils.HResultStringHeader(hresult)).Append(Constants.NamespaceSepPadded);
            if (!string.IsNullOrEmpty(messageVal))
                sb.Append("MESSAGE: ").Append(messageVal).Append(Constants.NamespaceSepPadded);
			sb.Append("TYPE: ").Append(classNameVal);
			return StringBuilderCache.GetStringAndRelease(sb);
		}

		//
		// Primitive types.
		//
		public static string GetPrimitiveValue(ulong addr, ClrType clrType)
		{
			if (!clrType.HasSimpleValue)
				return "Not primitive"; // TODO JRD
			var obj = clrType.GetValue(addr);
			return GetPrimitiveValue(obj, clrType);
		}

		public static string GetPrimitiveValue(object obj, ClrType clrType)
		{
			switch (clrType.ElementType)
			{
				case ClrElementType.Unknown:
					return "unknown_type";
				case ClrElementType.Boolean:
					return ((bool)obj).ToString();
				case ClrElementType.Char:
					return Utils.DisplayableChar((char) obj);
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
					return ((float)obj).ToString("N8");
				case ClrElementType.Double:
					return ((double)obj).ToString("N8");
				case ClrElementType.Pointer:
					return "pointer: " + Utils.AddressString((UInt64)obj);
				case ClrElementType.NativeInt:
					return "native int: " + obj;
				case ClrElementType.NativeUInt:
					return "native uint: " + obj;
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
					return Math.Abs((Double)obj) < Double.Epsilon;
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
				return ((Double)obj).ToString("C4");
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
				return "native int: " + obj;
			if (ClrElementType.NativeUInt == elemType)
				return "native uint: " + obj;
			if (ClrElementType.String == elemType
				|| ClrElementType.FunctionPointer == elemType
				|| ClrElementType.Object == elemType
				|| ClrElementType.SZArray == elemType)
				return "NOT_PRIMITIVE: " + obj;

			if (ClrElementType.Unknown == elemType)
				return "unknown_type";
			return "uknown_element_type";
		}

		public static string GetEnumValue(ulong parentAddr, ClrInstanceField field, bool intr)
		{
			var addrObj = field.GetValue(parentAddr, intr, false);
			var primVal = GetPrimitiveValue(addrObj, field.Type);
			var enumName = field.Type.GetEnumName(addrObj);
			return primVal + (enumName == null ? string.Empty : " " + enumName);
		}

		public static string GetEnumValue(ulong addr, ClrType clrType)
		{
			var primVal = GetPrimitiveValue(addr, clrType);
			var enumName = clrType.GetEnumName(addr);
			return primVal + (enumName == null ? string.Empty : " " + enumName);
		}

		public static string GetPrimitiveValue(ulong parentAddr, ClrInstanceField field, bool intr)
		{
			var addrObj = field.GetValue(parentAddr, intr, false);
			return GetPrimitiveValue(addrObj, field.Type);
		}

		//public static string TryGetPrimitiveValue(ClrHeap heap, ulong classAddr, ClrInstanceField field, bool internalAddr)
		//{
		//	var clrType = field.Type;
		//	var kind = TypeKinds.GetTypeKind(clrType);
		//	object addrObj;
		//	switch (TypeKinds.GetMainTypeKind(kind))
		//	{
		//		case TypeKind.StringKind:
		//			addrObj = field.GetValue(classAddr, internalAddr, false);
		//			if (addrObj == null) return Constants.NullValue;
		//			return GetStringValue(clrType, (ulong)addrObj);
		//		case TypeKind.ReferenceKind:
		//			switch (TypeKinds.GetParticularTypeKind(kind))
		//			{
		//				case TypeKind.Exception:
		//					addrObj = field.GetValue(classAddr, internalAddr, false);
		//					if (addrObj == null) return Constants.NullName;
		//					return GetShortExceptionValue((ulong)addrObj, clrType, heap);
		//				default:
		//					return Constants.NonValue;
		//			}
		//		case TypeKind.StructKind:
		//			switch (TypeKinds.GetParticularTypeKind(kind))
		//			{
		//				case TypeKind.Decimal:
		//					return GetDecimalValue(classAddr, field,internalAddr);
		//				case TypeKind.DateTime:
		//					return GetDateTimeValue(classAddr, field, internalAddr);
		//				case TypeKind.TimeSpan:
		//					addrObj = field.GetValue(classAddr, internalAddr, false);
		//					if (addrObj == null) return Constants.NullName;
		//					return GetTimeSpanValue((ulong)addrObj, clrType);
		//				case TypeKind.Guid:
		//					return GetGuidValue(classAddr, field);
		//				default:
		//					return Constants.NonValue;
		//			}
		//		case TypeKind.PrimitiveKind:
		//			addrObj = field.GetValue(classAddr, internalAddr, false);
		//			return GetPrimitiveValue(addrObj, clrType);
		//		default:
		//			return Constants.NonValue;
		//	}
		//}


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
			// ReSharper disable ConditionIsAlwaysTrueOrFalse
			if (lastFree && !free)
			// ReSharper restore ConditionIsAlwaysTrueOrFalse
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

			Debug.Assert(!lastFree && free);

			intervals.Add(new triple<bool, ulong, ulong>(false, lastAddr, curLastAddr - lastAddr));
		}

		public static KeyValuePair<string, InstanceValue> GetInstanceValue(IndexProxy ndxProxy, ClrHeap heap, ulong decoratedAddr, int fldNdx)
		{
			var addr = Utils.RealAddress(decoratedAddr);
			var typeInfo = TypeExtractor.TryGetRealType(heap, addr);
			var clrType = typeInfo.Key;
			var kind = typeInfo.Value;
			var specKind = TypeExtractor.GetSpecialKind(kind);
			var typeId1 = ndxProxy.GetTypeIdAtAddr(addr);
			var typeId2 = ndxProxy.GetTypeId(clrType.Name);
			string value;
			if (specKind != ClrElementKind.Unknown)
			{
				switch (specKind)
				{
					case ClrElementKind.Guid:
						value = GetGuidValue(addr, clrType);
						return new KeyValuePair<string, InstanceValue>(null, new InstanceValue(typeId1, addr, clrType.Name, value, Utils.AddressString(decoratedAddr)));
					case ClrElementKind.DateTime:
						value = GetDateTimeValue(addr, clrType);
						return new KeyValuePair<string, InstanceValue>(null, new InstanceValue(typeId1, addr, clrType.Name, value, Utils.AddressString(decoratedAddr)));
					case ClrElementKind.TimeSpan:
						value = GetTimeSpanValue(addr, clrType);
						return new KeyValuePair<string, InstanceValue>(null, new InstanceValue(typeId1, addr, clrType.Name, value, Utils.AddressString(decoratedAddr)));
					case ClrElementKind.Decimal:
						value = GetDecimalValue(addr, clrType, "0,0.00");
						return new KeyValuePair<string, InstanceValue>(null, new InstanceValue(typeId1, addr, clrType.Name, value, Utils.AddressString(decoratedAddr)));
					case ClrElementKind.Exception:
						value = GetShortExceptionValue(addr, clrType, heap);
						return new KeyValuePair<string, InstanceValue>(null, new InstanceValue(typeId1, addr, clrType.Name, value, Utils.AddressString(decoratedAddr)));
					case ClrElementKind.Free:
					case ClrElementKind.Abstract:
					case ClrElementKind.SystemVoid:
					case ClrElementKind.SystemObject:
						return new KeyValuePair<string, InstanceValue>(null, new InstanceValue(typeId1, addr, clrType.Name, string.Empty, Utils.AddressString(decoratedAddr)));
					default:
						return new KeyValuePair<string, InstanceValue>("Unexpected special kind", new InstanceValue(typeId1, addr, clrType.Name, string.Empty, Utils.AddressString(decoratedAddr)));
				}
			}
			else
			{
				switch(kind)
				{

				}
			}

			return new KeyValuePair<string, InstanceValue>(null, null);
/*
			if (specKind != ClrElementKind.Unknown)
			{
				switch (specKind)
				{
					case ClrElementKind.Free:
						return new KeyValuePair<string, InstanceValue>(null,new InstanceValue(typeId1, addr, clrType.Name, string.Empty, Utils.AddressString(decoratedAddr)));
					case ClrElementKind.Guid:

						return new KeyValuePair<string, InstanceValue>(null, new InstanceValue(typeId1, addr, clrType.Name, string.Empty, Utils.AddressString(decoratedAddr)));
					case ClrElementKind.DateTime:
					case ClrElementKind.TimeSpan:
					case ClrElementKind.Decimal:
						return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
					case ClrElementKind.Interface:
						throw new ApplicationException("Interface kind is not expected from ClrHeap.GetHeapObject(...) method.");
					case ClrElementKind.Enum:
						return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
					case ClrElementKind.System__Canon:
						throw new ApplicationException("System__Canon kind is not expected from ClrHeap.GetHeapObject(...) method.");
					case ClrElementKind.Exception:
					case ClrElementKind.Abstract:
					case ClrElementKind.SystemVoid:
					case ClrElementKind.SystemObject:
						return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
				}
			}
			else
			{
				switch (TypeExtractor.GetStandardKind(kind))
				{
					case ClrElementKind.String:
						return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
					case ClrElementKind.SZArray:
					case ClrElementKind.Array:
						return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
					case ClrElementKind.Struct:
					case ClrElementKind.Object:
					case ClrElementKind.Class:
						var dispType = new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
						var dispFlds = GetClrtDisplayableTypeFields(ndxProxy, heap, dispType, clrType, addr, ambiguousFields);
						var ambiguousCount = ambiguousFields.Count;
						var switchList = false;
						for (int j = i + 1, jcnt = addresses.Length; j < jcnt && ambiguousCount > 0; ++j)
						{
							var jaddr = addresses[j];
							if (switchList)
							{
								ambiguousCount = ambiguousFields2.Count;
								switchList = !switchList;
								TryGetClrtDisplayableTypeFields(ndxProxy, heap, dispType, clrType, jaddr, ambiguousFields2, ambiguousFields, dispFlds);
								continue;
							}
							ambiguousCount = ambiguousFields.Count;
							switchList = !switchList;
							TryGetClrtDisplayableTypeFields(ndxProxy, heap, dispType, clrType, jaddr, ambiguousFields, ambiguousFields2, dispFlds);
						}
						dispType.AddFields(dispFlds);
						return dispType;
					case ClrElementKind.Unknown:
						continue;
					default:
						return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
				}
			}
*/
		}

		public static KeyValuePair<string,InstanceValue> GetClassStructValue(IndexProxy ndxProxy, ClrHeap heap, ulong decoratedAddr, ClrType clrType,  TypeKind kind, int fldNdx)
        {
            Debug.Assert(clrType != null);
            var addr = Utils.RealAddress(decoratedAddr);
            var fldCount = TypeExtractor.FieldCount(clrType);
            if (fldCount < 1) return new KeyValuePair<string, InstanceValue>(clrType.Name + " is not struct/class with fields.", null);
            var internalAddresses = TypeExtractor.HasInternalAddresses(clrType);
            var typeId = ndxProxy.GetTypeId(clrType.Name);
            var instVal = new InstanceValue(typeId, addr, clrType.Name, String.Empty, Utils.RealAddressString(addr));

            for (int i=0; i < fldCount; ++i)
            {

            }

            return new KeyValuePair<string, InstanceValue>(null, instVal);
        }

        public static DisplayableString GetFieldValue(ClrHeap heap, ulong addr, bool intr, ClrInstanceField fld)
        {
            return new DisplayableString("Test");
        }

        /*
        try
            let addr = Utils.RealAddress(decoratedAddr)
            let fldCount = fieldCount clrType
            let internalAddresses = hasInternalAddresses clrType
            let mutable instVal = InstanceValue(ndxProxy.GetTypeId(clrType.Name), addr, clrType.Name, String.Empty, Utils.RealAddressString(addr));

            if fldCount = 0 then
                (clrType.Name + " is not struct/class with fields.",null)
            else
                for fldNdx = 0 to fldCount-1 do
                    let fld = clrType.Fields.[fldNdx]
                    let fldTypeName = if isNull fld.Type then Constants.UnknownTypeName else fld.Type.Name
                    let fldKind = typeKind fld.Type
                    let fldVal = getFieldValue heap addr internalAddresses fld fldKind
                    let typeId = ndxProxy.GetTypeId(fldTypeName)
                    let mainKind = TypeKinds.GetMainTypeKind(fldKind)
                    match mainKind with
                    | TypeKind.ArrayKind
                    | TypeKind.ReferenceKind ->
                        let fldAddr = getReferenceFieldAddress addr fld internalAddresses
                        let fldObjType = heap.GetObjectType(fldAddr)
                        let fldObjName = if isNull fldObjType then fldTypeName else fldObjType.Name
                        let fldTypeId = ndxProxy.GetTypeId(fldObjName)
                        instVal.Addvalue(new InstanceValue(fldTypeId, fldAddr, fldObjName, fld.Name, fldVal))
                    | _ ->
                        instVal.Addvalue(new InstanceValue(typeId, Constants.InvalidAddress, fldTypeName, fld.Name, fldVal, fldNdx))
                (null,instVal)
        with
            | exn -> (Utils.GetExceptionErrorString(exn),null)
            */

	}
}
