﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Diagnostics.Runtime;
using System.Net;

namespace ClrMDRIndex
{
    public delegate string ArrayElementValue(ClrHeap heap, ClrType aryType, ulong addr, ClrType elemType, int ndx);


    public class ValueExtractor
    {
        public static bool Is64Bit = Environment.Is64BitProcess;

        public static ulong GetReferenceFieldAddress(ulong addr, ClrInstanceField fld, bool intern)
        {
            Debug.Assert(fld != null);
            object valObj = fld.GetValue(addr, intern, false);
            if (valObj == null) return Constants.InvalidAddress;
            Debug.Assert(valObj is ulong);
            return (ulong)valObj;
        }


        public static string TypeDefaultValueAsString(ClrType type)
        {
            var kind = TypeExtractor.GetElementKind(type);
            var elemType = TypeExtractor.GetClrElementType(kind);
            switch(elemType)
            {
                case ClrElementType.Array:
                case ClrElementType.SZArray:
                    return "[0]/null";
                case ClrElementType.String:
                    return "\"\"/null";
                case ClrElementType.Object:
                    return null;
                case ClrElementType.Struct:
                    var specKind = TypeExtractor.GetSpecialKind(kind);
                    switch (specKind)
                    {
                        case ClrElementKind.Decimal:
                            return "0";
                        case ClrElementKind.DateTime:
                            return "< 01/01/1800";
                        case ClrElementKind.TimeSpan:
                            return "0";
                        case ClrElementKind.Guid:
                            return "00000000-0000-0000-0000-000000000000";
                        default:
                            return "empty";
                    }
                case ClrElementType.Unknown:
                    return "unknown";
                default:
                    return "0";
            }
        }

        #region decimal

        public static string GetDecimalValue(ulong parentAddr, ClrInstanceField field, bool intr)
        {
            var addr = field.GetAddress(parentAddr, intr);
            var flags = (int)field.Type.Fields[0].GetValue(addr, true);
            var hi = (int)field.Type.Fields[1].GetValue(addr, true);
            var lo = (int)field.Type.Fields[2].GetValue(addr, true);
            var mid = (int)field.Type.Fields[3].GetValue(addr, true);

            int[] bits = { lo, mid, hi, flags };
            decimal d = new decimal(bits);

            return d.ToString(CultureInfo.InvariantCulture);
        }

        //public static decimal GetDecimal(ulong parentAddr, ClrInstanceField field, bool intr)
        //{
        //    var addr = field.GetAddress(parentAddr, intr);
        //    var flags = (int)field.Type.Fields[0].GetValue(addr, true);
        //    var hi = (int)field.Type.Fields[1].GetValue(addr, true);
        //    var lo = (int)field.Type.Fields[2].GetValue(addr, true);
        //    var mid = (int)field.Type.Fields[3].GetValue(addr, true);

        //    int[] bits = { lo, mid, hi, flags };
        //    decimal d = new decimal(bits);

        //    return d;
        //}

        /// <summary>
        /// Get decimal value given a class/struct address and its decimal field.
        /// </summary>
        /// <param name="parentAddr">Enclosing class/struct address.</param>
        /// <param name="field">Decimal field.</param>
        /// <param name="intern">Is the field parent a struct?</param>
        /// <returns>Decimal value.</returns>
        public static decimal GetDecimalPAF(ulong parentAddr, ClrInstanceField field, bool intern)
        {
            var addr = field.GetAddress(parentAddr, intern);
            var flags = (int)field.Type.Fields[0].GetValue(addr, true);
            var hi = (int)field.Type.Fields[1].GetValue(addr, true);
            var lo = (int)field.Type.Fields[2].GetValue(addr, true);
            var mid = (int)field.Type.Fields[3].GetValue(addr, true);

            int[] bits = { lo, mid, hi, flags };
            decimal d = new decimal(bits);

            return d;
        }

        /// <summary>
        /// Get decimal value given a class/struct address and its decimal field.
        /// </summary>
        /// <param name="parentAddr">Enclosing class/struct address.</param>
        /// <param name="field">Decimal field.</param>
        /// <param name="intern">Is the field parent a struct?</param>
        /// <returns>Decimal value.</returns>
        public static string DecimalStringPAF(ulong parentAddr, ClrInstanceField field, bool intern, string formatSpec=null)
        {
            decimal d = GetDecimalPAF(parentAddr, field, intern);
            return formatSpec == null ? d.ToString("G", CultureInfo.InvariantCulture) : d.ToString(formatSpec);
        }

        //public static string DecimalValue(ulong addr, ClrInstanceField field)
        //{
        //    Debug.Assert(field.Type != null);
        //    var flags = (int)field.Type.Fields[0].GetValue(addr, true);
        //    var hi = (int)field.Type.Fields[1].GetValue(addr, true);
        //    var lo = (int)field.Type.Fields[2].GetValue(addr, true);
        //    var mid = (int)field.Type.Fields[3].GetValue(addr, true);

        //    int[] bits = { lo, mid, hi, flags };
        //    decimal d = new decimal(bits);

        //    return d.ToString("G", CultureInfo.InvariantCulture);
        //}

        ////public static string GetDecimalValueR(ulong addr, ClrType type, string formatSpec)
        //{
        //    decimal d = GetDecimalValueR(addr, type);
        //    return formatSpec == null ? d.ToString("G", CultureInfo.InvariantCulture) : d.ToString(formatSpec);
        //}

        //public static decimal GetDecimalValueR(ulong addr, ClrType type)
        //{
        //    var flags = (int)type.Fields[0].GetValue(addr, true);
        //    var hi = (int)type.Fields[1].GetValue(addr, true);
        //    var lo = (int)type.Fields[2].GetValue(addr, true);
        //    var mid = (int)type.Fields[3].GetValue(addr, true);

        //    int[] bits = { lo, mid, hi, flags };
        //    return new decimal(bits);
        //}

        public static string DecimalValueAsString(ulong addr, ClrType type, string formatSpec)
        {
            decimal d = GetDecimalValue(addr, type);
            return formatSpec == null ? d.ToString("G",CultureInfo.InvariantCulture) : d.ToString(formatSpec);
        }

        public static string DecimalValueAsString(ulong addr, ClrType type, bool intr, string formatSpec = null)
        {
            decimal d = GetDecimalValue(addr, type);
            return formatSpec == null ? d.ToString("G", CultureInfo.InvariantCulture) : d.ToString(formatSpec);
        }

        //public static decimal GetDecimalValue(ulong addr, ClrType type, bool intr)
        //{
        //    var flags = (int)type.Fields[0].GetValue(addr, intr);
        //    var hi = (int)type.Fields[1].GetValue(addr, true);
        //    var lo = (int)type.Fields[2].GetValue(addr, true);
        //    var mid = (int)type.Fields[3].GetValue(addr, true);
        //    var checkflags = flags & 0x0000FF01;
        //    if (!IsValidDecimalFlag(flags)) return 0m;
        //    int[] bits = { lo, mid, hi, flags };
        //    return new decimal(bits);
        //}

        public static decimal GetDecimalValue(ulong addr, ClrType type)
        {
            var flags = (int)type.Fields[0].GetValue(addr, true);
            var hi = (int)type.Fields[1].GetValue(addr, true);
            var lo = (int)type.Fields[2].GetValue(addr, true);
            var mid = (int)type.Fields[3].GetValue(addr, true);
            var checkflags = flags & 0x0000FF01;
            if(!IsValidDecimalFlag(flags)) return 0m;
            int[] bits = { lo, mid, hi, flags };
            return new decimal(bits);
        }

        private static bool IsValidDecimalFlag(int flag)
        {
            var cflag = flag & 0x0000FF01;
            if ((cflag >> 16) != 0) return false;
            if ((cflag >> 8) > 28) return false;
            if ((flag & 0x000000fE) > 0) return false;
            return true;
        }

        //public static void Swap(int[] ary, int i1, int i2)
        //{
        //    var temp = ary[i1];
        //    ary[i1] = ary[i2];
        //    ary[i2] = temp;
        //}

        //public static decimal GetDecimalValue(ClrHeap heap, ulong addr)
        //{
        //    int[] bits = ReadIntAryAtAddress(addr, 4, heap);
        //    // flags, hi, lo, mid 
        //    Swap(bits, 0, 3);
        //    // mid, hi, lo, flags 
        //    Swap(bits, 2, 0);
        //    // lo, hi,mid, flags 
        //    Swap(bits, 1, 2);
        //    // lo, mid, hi, flags
        //    return new decimal(bits);
        //}

        //public static string GetDecimalValue(ClrHeap heap, ulong addr, string formatSpec)
        //{
        //    decimal d = GetDecimalValue(heap, addr);
        //    return formatSpec == null ? d.ToString(CultureInfo.InvariantCulture) : d.ToString(formatSpec);
        //}

        #endregion decimal

        #region string

        public static string GetStringAtAddress(ulong addr, bool intr, ClrInstanceField fld)
        {
            var obj = fld.GetValue(addr, intr, true);
            return obj == null ? Constants.NullValue : (string)obj;
        }

        public static string GetStringAtAddress(ulong addr, ClrHeap heap)
        {
            if (addr == 0UL) return Constants.NullValueOld;
            var lenBuf = new byte[4];
            addr += (ulong)IntPtr.Size;
            heap.ReadMemory(addr, lenBuf, 0, 4);
            int len = BitConverter.ToInt32(lenBuf, 0) * sizeof(char);
            if (len == 0) return Constants.EmptyStringValue;
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
            if (addr == Constants.InvalidAddress) return Constants.NullValueOld;
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

        #endregion string

        #region System.Datetime

        private const ulong TicksMask = 0x3FFFFFFFFFFFFFFF;

        // TODO JRD -- skipping first entry in array -- remove this, it's used by F#
        public static string DateTimeValueAsString(ulong addr, ClrType type, string formatSpec = null)
        {
            var data = (ulong)type.Fields[0].GetValue(addr, true);
            data = data & TicksMask;
            var dt = DateTime.FromBinary((long)data);
            return formatSpec == null ? dt.ToString(CultureInfo.InvariantCulture) : dt.ToString(formatSpec);
        }

        public static string DateTimeValueAsString(ulong addr, ClrType type, bool intr, string formatSpec = null)
        {
            var data = (ulong)type.Fields[0].GetValue(addr, intr);
            data = data & TicksMask;
            var dt = DateTime.FromBinary((long)data);
            return formatSpec == null ? dt.ToString(CultureInfo.InvariantCulture) : dt.ToString(formatSpec);
        }

        public static DateTime DateTimeValue(ulong addr, ClrType type)
        {
            var data = (ulong)type.Fields[0].GetValue(addr, true);
            data = data & TicksMask;
            return  DateTime.FromBinary((long)data);
        }

        public static string DateTimeValueString(ulong addr, ClrType type, string formatSpec = null)
        {
            DateTime dt = DateTimeValue(addr, type);
            var data = (ulong)type.Fields[0].GetValue(addr, true);
            data = data & TicksMask;
            return formatSpec == null ? dt.ToString(CultureInfo.InvariantCulture) : dt.ToString(formatSpec);
        }

        //public static string GetDateTimeValue(ulong addr, ClrType clrType, string formatSpec = null)
        //{
        //    var data = (ulong)clrType.Fields[0].GetValue(addr, false);
        //    data = data & TicksMask;
        //    var dt = DateTime.FromBinary((long)data);
        //    return formatSpec == null ? dt.ToString(CultureInfo.InvariantCulture) : dt.ToString(formatSpec);
        //}

        public static string DateTimeValue(ulong addr, ClrInstanceField fld, string formatSpec = null)
        {
            //var data = (ulong)fld.Type.Fields[0].GetValue(addr, true);
            var data = (ulong)fld.Type.Fields[0].GetValue(addr, true, false);
            data = data & TicksMask;
            var dt = DateTime.FromBinary((long)data);
            return formatSpec == null ? dt.ToString(CultureInfo.InvariantCulture) : dt.ToString(formatSpec);
        }


        /// <summary>
        /// Get value of DateTimeField given enclosing object address.
        /// </summary>
        /// <param name="addr">Enclosing object address.</param>
        /// <param name="fld">DateTime field.</param>
        /// <param name="intern">Is enclosing object of value type (struct).</param>
        /// <param name="formatSpec">DateTime string format, optional.</param>
        /// <returns></returns>
        public static string DateTimeValue(ulong addr, ClrInstanceField fld, bool intern, string formatSpec = null)
        {
            addr = fld.GetAddress(addr, intern);
            var data = (ulong)fld.Type.Fields[0].GetValue(addr, true, false);
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

        /// <summary>
        /// Get DateTime value given address of containing instance.
        /// </summary>
        /// <param name="parentAddr">Address of the class/struct containing DateTime struct.</param>
        /// <param name="fld">DateTime field.</param>
        /// <param name="internalPtr">Is containing instance a struct.</param>
        /// <returns></returns>
        public static DateTime GetDateTimePAF(ulong parentAddr, ClrInstanceField fld, bool internalPtr)
        {
            ulong fldAddr = fld.GetAddress(parentAddr, internalPtr);
            var data = (ulong)fld.Type.Fields[0].GetValue(fldAddr, true);
            data = data & TicksMask;
            var dt = DateTime.FromBinary((long)data);
            return dt;
        }

        /// <summary>
        /// Get DateTime string given address of containing instance.
        /// </summary>
        /// <param name="parentAddr">Address of the class/struct containing DateTime struct.</param>
        /// <param name="fld">DateTime field.</param>
        /// <param name="internalPtr">Is containing instance a struct.</param>
        /// <param name="formatSpec">DateTime string fromatting, can be null.</param>
        /// <returns></returns>
        public static string GetDateTimeString(ulong parentAddr, ClrInstanceField fld, bool internalPtr, string formatSpec = null)
        {
            ulong fldAddr = fld.GetAddress(parentAddr, internalPtr);
            var data = (ulong)fld.Type.Fields[0].GetValue(fldAddr, true);
            data = data & TicksMask;
            var dt = DateTime.FromBinary((long)data);
            return formatSpec == null ? dt.ToString("s") : dt.ToString(formatSpec);
        }

        #endregion System.Datetime

        #region System.TimeSpan

        public static string TimeSpanValueString(ulong addr, ClrInstanceField fld, bool intr) // TODO JRD -- check if this works
        {
            ulong fldAddr = fld.GetAddress(addr, intr);
            var data = (long)fld.Type.Fields[0].GetValue(fldAddr, true);
            var ts = TimeSpan.FromTicks(data);
            return ts.ToString("c");
        }

        #endregion System.TimeSpan

        //
        // System.TimeSpan
        //
        // TODO JRD -- skipping first entry in arrays
        public static string TimeSpanValueAsString(ulong addr, ClrType type)
        {
            var data = (long)type.Fields[0].GetValue(addr, true);
            var ts = TimeSpan.FromTicks(data);
            return ts.ToString("c");
        }

        public static string TimeSpanValueAsString(ulong addr, ClrType type, bool intr)
        {
            var data = (long)type.Fields[0].GetValue(addr, intr);
            var ts = TimeSpan.FromTicks(data);
            return ts.ToString("c");
        }

        public static TimeSpan TimeSpanValue(ulong addr, ClrType type)
        {
            var data = (long)type.Fields[0].GetValue(addr, true);
            return TimeSpan.FromTicks(data);
        }

        public static string TimeSpanValue(ulong addr, ClrInstanceField fld) // TODO JRD -- check if this works
        {
            var data = (long)fld.Type.Fields[0].GetValue(addr, true);
            var ts = TimeSpan.FromTicks(data);
            return ts.ToString("c");
        }

        public static TimeSpan GetTimeSpan(ulong addr, ClrInstanceField fld) // TODO JRD -- check if this works
        {
            var data = (long)fld.Type.Fields[0].GetValue(addr, true);
            return TimeSpan.FromTicks(data);
        }


        public static TimeSpan GetTimeSpan(ulong addr, ClrInstanceField fld, bool intr) // TODO JRD -- check if this works
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

        #region System.Guid

        /// <summary>
        /// Gets a string of a guid value.
        /// </summary>
        /// <param name="addr">Address of the type instance.</param>
        /// <param name="type">Should be System.Decimal.</param>
        /// <returns></returns>
        /// <remarks>Used in CollectionContent for arrays.</remarks>
        //public static string GuidValueAsString(ulong addr, ClrType type)
        //{
        //    StringBuilder sb = StringBuilderCache.Acquire(64);
        //    var ival = (int)type.Fields[0].GetValue(addr, true);
        //    sb.AppendFormat("{0:X8}", ival);
        //    sb.Append('-');
        //    var sval = (short)type.Fields[1].GetValue(addr, true);
        //    sb.AppendFormat("{0:X4}", sval);
        //    sb.Append('-');
        //    sval = (short)type.Fields[2].GetValue(addr, true);
        //    sb.AppendFormat("{0:X4}", sval);
        //    sb.Append('-');
        //    for (var i = 3; i < 11; ++i)
        //    {
        //        if (i == 5) sb.Append('-');
        //        var val = (byte)type.Fields[i].GetValue(addr, true);
        //        sb.AppendFormat("{0:X2}", val);
        //    }
        //    return StringBuilderCache.GetStringAndRelease(sb);
        //}

        public static string GuidValueAsString(ulong addr, ClrType type, bool intr)
        {
            StringBuilder sb = StringBuilderCache.Acquire(64);

            var ival = (int)type.Fields[0].GetValue(addr, intr);
            sb.AppendFormat("{0:X8}", ival);
            sb.Append('-');
            var sval = (short)type.Fields[1].GetValue(addr, intr);
            sb.AppendFormat("{0:X4}", sval);
            sb.Append('-');
            sval = (short)type.Fields[2].GetValue(addr, intr);
            sb.AppendFormat("{0:X4}", sval);
            sb.Append('-');
            for (var i = 3; i < 11; ++i)
            {
                if (i == 5) sb.Append('-');
                var val = (byte)type.Fields[i].GetValue(addr, intr);
                sb.AppendFormat("{0:X2}", val);
            }
            return StringBuilderCache.GetStringAndRelease(sb);
        }


        public static string GuidValue(ulong addr, ClrInstanceField field)
        {
            StringBuilder sb = StringBuilderCache.Acquire(64);
            var ival = (int)field.Type.Fields[0].GetValue(addr, true);
            sb.AppendFormat("{0:X8}", ival);
            sb.Append('-');
            var sval = (short)field.Type.Fields[1].GetValue(addr, true);
            sb.AppendFormat("{0:X4}", sval);
            sb.Append('-');
            sval = (short)field.Type.Fields[2].GetValue(addr, true);
            sb.AppendFormat("{0:X4}", sval);
            sb.Append('-');
            for (var i = 3; i < 11; ++i)
            {
                if (i == 5) sb.Append('-');
                var val = (byte)field.Type.Fields[i].GetValue(addr, true);
                sb.AppendFormat("{0:X2}", val);
            }
            return StringBuilderCache.GetStringAndRelease(sb);
        }

        public static Guid GetGuid(ulong parentAddr, ClrInstanceField field, bool intr)
        {
            ulong addr = field.GetAddress(parentAddr, intr);
            var ival = (int)field.Type.Fields[0].GetValue(addr, true);
            var sval1 = (short)field.Type.Fields[1].GetValue(addr, true);
            var sval2 = (short)field.Type.Fields[2].GetValue(addr, true);
            byte b1 = (byte)field.Type.Fields[3].GetValue(addr, true);
            byte b2 = (byte)field.Type.Fields[4].GetValue(addr, true);
            byte b3 = (byte)field.Type.Fields[5].GetValue(addr, true);
            byte b4 = (byte)field.Type.Fields[6].GetValue(addr, true);
            byte b5 = (byte)field.Type.Fields[7].GetValue(addr, true);
            byte b6 = (byte)field.Type.Fields[8].GetValue(addr, true);
            byte b7 = (byte)field.Type.Fields[9].GetValue(addr, true);
            byte b8 = (byte)field.Type.Fields[10].GetValue(addr, true);
            return new Guid(ival, sval1, sval2, b1, b2, b3, b4, b5, b6, b7, b8);
        }

        public static Guid GuidValue(ulong addr, ClrType type)
        {
            var ival = (int)type.Fields[0].GetValue(addr, true);
            var sval1 = (short)type.Fields[1].GetValue(addr, true);
            var sval2 = (short)type.Fields[2].GetValue(addr, true);
            byte b1 = (byte)type.Fields[3].GetValue(addr, true);
            byte b2 = (byte)type.Fields[4].GetValue(addr, true);
            byte b3 = (byte)type.Fields[5].GetValue(addr, true);
            byte b4 = (byte)type.Fields[6].GetValue(addr, true);
            byte b5 = (byte)type.Fields[7].GetValue(addr, true);
            byte b6 = (byte)type.Fields[8].GetValue(addr, true);
            byte b7 = (byte)type.Fields[9].GetValue(addr, true);
            byte b8 = (byte)type.Fields[10].GetValue(addr, true);
            return new Guid(ival, sval1, sval2, b1, b2, b3, b4, b5, b6, b7, b8);
        }

        public static string GetGuidValue(ulong addr, ClrInstanceField field, bool intr)
        {
            StringBuilder sb = StringBuilderCache.Acquire(64);
            var fldAddr = field.GetAddress(addr, intr);
            if (fldAddr == 0UL) return Constants.NullValueOld;
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

        public static bool IsGuidEmpty(ulong addr, ClrInstanceField field, bool intr)
        {
            var fldAddr = field.GetAddress(addr, intr);
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

        #endregion System.Guid

        #region exception

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
            sb.Append(Utils.RealAddressStringHeader(addr));
            sb.Append(Utils.HResultStringHeader(hresult)).Append(" ");
            sb.Append(message);
            sb.Append(" TYPE: ").Append(exTypeName);
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
                exTypeName = exType.Type != null ? exType.Type.Name : Constants.NullTypeName;
                hresult = exType.HResult;
                message = exType.Message;
                sb.Append(Utils.HResultStringHeader(hresult)).Append(Constants.NamespaceSepPadded);
                if (!string.IsNullOrEmpty(message))
                    sb.Append("MESSAGE: ").Append(message).Append(Constants.NamespaceSepPadded);
                sb.Append("TYPE: ").Append(exTypeName);
                return StringBuilderCache.GetStringAndRelease(sb);
            }



            if (Utils.IsInvalidAddress(addr)) return Constants.NullValueOld;
            var classNameFld = type.GetFieldByName("_className");
            var classNameObj = classNameFld.GetValue(addr, false, true);
            var classNameVal = classNameObj == null ? Constants.NullValueOld : classNameObj.ToString();

            var messageFld = type.GetFieldByName("_message");
            var messageObj = messageFld.GetValue(addr, false, true);
            var messageVal = messageObj == null ? Constants.NullValueOld : messageObj.ToString();

            var hresultFld = type.GetFieldByName("_HResult");
            Debug.Assert(hresultFld.ElementType == ClrElementType.Int32);
            int hresultValue = (int)hresultFld.GetValue(addr);

            sb.Append(Utils.HResultStringHeader(hresult)).Append(Constants.NamespaceSepPadded);
            if (!string.IsNullOrEmpty(messageVal))
                sb.Append("MESSAGE: ").Append(messageVal).Append(Constants.NamespaceSepPadded);
            sb.Append("TYPE: ").Append(classNameVal);
            return StringBuilderCache.GetStringAndRelease(sb);
        }

        #endregion exception

        #region enum

        public static string GetEnumValueString(ulong addr, ClrType clrType, out long intVal)
        {
            ClrElementType enumElem = clrType.GetEnumElementType();
            return GetEnum(addr, clrType, enumElem, out intVal);
        }

        public static string GetEnum(ulong addr, ClrType clrType, ClrElementType enumElem, out long intVal)
        {
            object enumVal = clrType.GetValue(addr);
            string name = null;
            intVal = long.MinValue;

            string num = null;
            if (enumVal is int)
            {
                intVal = (long)(int)enumVal;
                num = intVal.ToString();
            }
            else
            {
                // The approved types for an enum are byte, sbyte, short, ushort, int, uint, long, or ulong.
                switch (enumElem)
                {
                    case ClrElementType.Int32:
                        intVal = (long)(int)enumVal;
                        num = intVal.ToString();
                        break;
                    case ClrElementType.UInt32:
                        intVal = (long)((uint)enumVal);
                        num = intVal.ToString();
                        break;
                    case ClrElementType.UInt8:
                        intVal = (long)(byte)enumVal;
                        num = intVal.ToString();
                        break;
                    case ClrElementType.Int8:
                        intVal = (long)(sbyte)enumVal;
                        num = intVal.ToString();
                        break;
                    case ClrElementType.Int16:
                        intVal = (long)(short)enumVal;
                        num = intVal.ToString();
                        break;
                    case ClrElementType.UInt16:
                        intVal = (long)(ushort)enumVal;
                        num = intVal.ToString();
                        break;
                    case ClrElementType.Int64:
                        intVal = (long)enumVal;
                        num = intVal.ToString();
                        break;
                    case ClrElementType.UInt64:
                        intVal = (long)(ulong)enumVal;
                        num = intVal.ToString();
                        break;
                }
            }

            if (name == null)
            {
                if (intVal != long.MinValue) name = clrType.GetEnumName((int)intVal);
                if (name == null) name = clrType.GetEnumName(enumVal);
            }
            return (num == null ? "?" : num) + " " + (name == null ? "?" : name);
        }

        public static long GetEnumValue(ulong addr, ClrType clrType, ClrElementType enumElem)
        {
            object enumVal = clrType.GetValue(addr);

            if (enumVal is int)
            {
                return (long)(int)enumVal;
            }
            else
            {
                // The approved types for an enum are byte, sbyte, short, ushort, int, uint, long, or ulong.
                switch (enumElem)
                {
                    case ClrElementType.UInt32:
                        return (long)((uint)enumVal);
                    case ClrElementType.UInt8:
                        return (long)(byte)enumVal;
                    case ClrElementType.Int8:
                        return (long)(sbyte)enumVal;
                    case ClrElementType.Int16:
                        return (long)(short)enumVal;
                    case ClrElementType.UInt16:
                        return (long)(ushort)enumVal;
                    case ClrElementType.Int64:
                        return (long)enumVal;
                    case ClrElementType.UInt64:
                        return (long)(ulong)enumVal;
                    default:
                        return EnumValues.InvalidEnumValue;
                }
            }
        }

        public static string GetEnumAsString(ulong addr, ClrType clrType, ClrElementType enumElem, object enumVal, out long intVal)
        {
            string name = null;
            intVal = long.MinValue;

            string num = null;
            if (enumVal is int)
            {
                intVal = (long)(int)enumVal;
                num = intVal.ToString();
            }
            else
            {
                // The approved types for an enum are byte, sbyte, short, ushort, int, uint, long, or ulong.
                switch (enumElem)
                {
                    case ClrElementType.Int32:
                        intVal = (long)(int)enumVal;
                        num = intVal.ToString();
                        break;
                    case ClrElementType.UInt32:
                        intVal = (long)((uint)enumVal);
                        num = intVal.ToString();
                        break;
                    case ClrElementType.UInt8:
                        intVal = (long)(byte)enumVal;
                        num = intVal.ToString();
                        break;
                    case ClrElementType.Int8:
                        intVal = (long)(sbyte)enumVal;
                        num = intVal.ToString();
                        break;
                    case ClrElementType.Int16:
                        intVal = (long)(short)enumVal;
                        num = intVal.ToString();
                        break;
                    case ClrElementType.UInt16:
                        intVal = (long)(ushort)enumVal;
                        num = intVal.ToString();
                        break;
                    case ClrElementType.Int64:
                        intVal = (long)enumVal;
                        num = intVal.ToString();
                        break;
                    case ClrElementType.UInt64:
                        intVal = (long)(ulong)enumVal;
                        num = intVal.ToString();
                        break;
                }
            }

            if (name == null)
            {
                if (intVal != long.MinValue) name = clrType.GetEnumName((int)intVal);
                if (name == null) name = clrType.GetEnumName(enumVal);
            }
            return (num == null ? "?" : num) + " " + (name == null ? "?" : name);
        }
 
        public static long GetEnumValue(ulong parentAddr, ClrInstanceField field, bool intr)
        {
            var addrObj = field.GetAddress(parentAddr, intr);
            if (field.Type != null)
            {
                long intVal;
                string outStr = GetEnumValueString(addrObj, field.Type, out intVal);
                return intVal;
            }
            return int.MinValue;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="parentAddr"></param>
        /// <param name="field"></param>
        /// <param name="intr"></param>
        /// <returns></returns>
        public static string GetEnumString(ulong parentAddr, ClrInstanceField field, bool intr)
        {
            var enumObj = field.GetValue(parentAddr, intr);
            var addrObj = field.GetAddress(parentAddr, true);
            string name = null;
            if (field.Type != null)
            {
                name = field.Type.GetEnumName(enumObj);
            }
            return enumObj.ToString() + " " + (name==null ? "?" : name);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parentAddr"></param>
        /// <param name="field"></param>
        /// <param name="intr"></param>
        /// <returns></returns>
        public static object GetEnumValueObject(ulong parentAddr, ClrInstanceField field, bool intr)
        {
            return field.GetValue(parentAddr, intr);
        }

        public static string GetEnumStringOfField(ulong addr, ClrInstanceField field, bool intr)
        {
            if (field.Type != null)
            {
                long intVal;
                string outStr = GetEnumValueString(addr, field.Type, out intVal);
                return outStr;
            }
            return Constants.UnknownValue;
        }

        #endregion enum

        //
        // Primitive types.
        //

        public static object GetPrimitiveValueObject(ulong addr, ClrInstanceField fld, bool intern)
        {
            var kind = TypeExtractor.GetElementKind(fld.Type);
            if (kind == ClrElementKind.Unknown) return Constants.UnknownValue;
            var obj = fld.GetValue(addr, intern);
            return obj;
        }

        public static string PrimitiveValue(ulong addr, ClrInstanceField fld, bool intern)
        {
            var kind = TypeExtractor.GetElementKind(fld.Type);
            if (kind == ClrElementKind.Unknown) return Constants.UnknownValue;
            var obj = fld.GetValue(addr, intern);
            return PrimitiveValue(obj, kind);
        }

        public static string PrimitiveValue(object obj, ClrElementKind kind)
        {
            switch (kind)
            {
                case ClrElementKind.Unknown:
                    return Constants.UnknownValue;
                case ClrElementKind.Boolean:
                    return ((bool)obj).ToString();
                case ClrElementKind.Char:
                    return Utils.DisplayableChar((char)obj);
                case ClrElementKind.Int8:
                    return ((Byte)obj).ToString();
                case ClrElementKind.Int16:
                    return ((Int16)obj).ToString();
                case ClrElementKind.Int32:
                    return ((Int32)obj).ToString();
                case ClrElementKind.Int64:
                    return ((Int64)obj).ToString();
                case ClrElementKind.UInt8:
                    return ((Byte)obj).ToString();
                case ClrElementKind.UInt16:
                    return ((UInt16)obj).ToString();
                case ClrElementKind.UInt32:
                    return ((UInt32)obj).ToString();
                case ClrElementKind.UInt64:
                    return ((UInt64)obj).ToString();
                case ClrElementKind.Float:
                    return ((float)obj).ToString("N8");
                case ClrElementKind.Double:
                    return ((double)obj).ToString("N8");
                case ClrElementKind.Pointer:
                    return "pointer: " + Utils.AddressString((UInt64)obj);
                case ClrElementKind.NativeInt:
                    return "native int: " + obj;
                case ClrElementKind.NativeUInt:
                    return "native uint: " + obj;
                default:
                    return Constants.UnknownValue;
            }
        }


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
                    return Utils.DisplayableChar((char)obj);
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
                    return ((Int64)obj) == 0;
                case ClrElementType.UInt8:
                    return ((Byte)obj) == 0;
                case ClrElementType.UInt16:
                    return ((UInt16)obj) == 0;
                case ClrElementType.UInt32:
                    return ((UInt32)obj) == 0UL;
                case ClrElementType.UInt64:
                    return ((UInt64)obj) == 0UL;
                case ClrElementType.Float:
                case ClrElementType.Double:
                    return Math.Abs((Double)obj) < Double.Epsilon;
                case ClrElementType.Pointer:
                    return Is64Bit ? (UInt64)obj == 0 : (UInt32)obj == 0;
                case ClrElementType.NativeInt:
                    return (int)obj == 0;
                case ClrElementType.NativeUInt:
                    return (uint)obj == 0;
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

        public static string PrimitiveValueAsString(ulong addr, ClrType type, ClrElementKind elemKind)
        {
            var obj = type.GetValue(addr);
            return PrimitiveValueAsString(obj, TypeExtractor.GetClrElementType(elemKind));

        }

        public static string PrimitiveValueAsString(object obj, ClrElementType elemType)
        {
            if (obj == null) return Constants.NullValueOld;
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

        public static string GetPrimitiveValue(ulong parentAddr, ClrInstanceField field, bool intr)
        {
            var addrObj = field.GetValue(parentAddr, intr, false);
            return GetPrimitiveValue(addrObj, field.Type);
        }

        public static void SetSegmentInterval(List<ValueTuple<bool, ulong, ulong>> intervals, ulong addr, ulong sz, bool free)
        {
            Debug.Assert(intervals.Count > 0 && sz > 0ul);
            var lstNdx = intervals.Count - 1;
            var last = intervals[lstNdx];
            bool lastFree = last.Item1;
            var lastAddr = last.Item2 + last.Item3;

            var curLastAddr = addr + sz;
            if (curLastAddr <= lastAddr) return; // ?
            long diff = 0L;
            if (addr > lastAddr)
                diff = (long)addr - (long)lastAddr;
            else if (addr < lastAddr)
                diff = (long)lastAddr - (long)addr;

            if (lastFree && free) // append to last
            {
                last.Item3 = curLastAddr - last.Item2;
                intervals[lstNdx] = last;
                return;
            }

            if (!lastFree && !free) // append to last or insert free followed by !free
            {
                if (diff > 0)
                {
                    intervals.Add((true, lastAddr, (ulong)diff));
                    intervals.Add((false, addr, sz));
                    return;
                }
                last.Item3 = curLastAddr - last.Item2;
                intervals[lstNdx] = last;
                return;
            }
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (lastFree && !free)
            // ReSharper restore ConditionIsAlwaysTrueOrFalse
            {
                if (diff > 0)
                {
                    last.Item3 = last.Item3 + (ulong)diff;
                    intervals[lstNdx] = last;
                }
                else if (diff < 0)
                {
                    throw new MdrException("[ValueExtractor.SetSegmentInterval] Address less then last address.");
                }
                intervals.Add((false, addr, sz));
                return;
            }

            Debug.Assert(!lastFree && free);

            intervals.Add((false, lastAddr, curLastAddr - lastAddr));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="heap"></param>
        /// <param name="addr"></param>
        /// <param name="fld"></param>
        /// <param name="fldType"></param>
        /// <param name="fldKind"></param>
        /// <param name="intern">Is parent a structure.</param>
        /// <param name="theValue">If we want object (actual value), otherwise object is changed to string.</param>
        /// <returns></returns>
        public static object GetFieldValue(ClrHeap heap, ulong addr, ClrInstanceField fld, ClrType fldType, ClrElementKind fldKind, bool intern, bool theValue)
        {
            Debug.Assert(Utils.IsRealAddress(addr));
            var fldSpecKind = TypeExtractor.GetSpecialKind(fldKind);
            if (fldSpecKind != ClrElementKind.Unknown && fldSpecKind != ClrElementKind.System__Canon)
            {
                switch (fldSpecKind)
                {
                    case ClrElementKind.Guid:
                        return theValue ? (object)GetGuid(addr, fld, intern) : GetGuidValue(addr, fld, intern);
                        
                    case ClrElementKind.DateTime:
                        return theValue ? (object)GetDateTimePAF(addr, fld, intern) : GetDateTimeString(addr, fld, intern);
                    case ClrElementKind.TimeSpan:
                        return theValue ? (object)GetTimeSpan(addr, fld) : TimeSpanValue(addr, fld);
                    case ClrElementKind.Decimal:
                        return theValue ? (object)GetDecimalPAF(addr, fld, intern) : DecimalStringPAF(addr, fld, intern);
                    case ClrElementKind.Exception:
                        return theValue ? (object)addr : Utils.RealAddressString(addr);
                    case ClrElementKind.Enum:
                        return theValue ? (object)GetEnumValue(addr, fld, intern) : GetEnumString(addr, fld, intern);
                   // case ClrElementKind.Free:
                    case ClrElementKind.Abstract:
                    case ClrElementKind.SystemVoid:
                    case ClrElementKind.SystemObject:
                        var fldAddr = GetReferenceFieldAddress(addr, fld, intern);
                        return theValue ? (object)fldAddr : Utils.RealAddressString(fldAddr);
                        //return theValue ? (object)addr : Utils.RealAddressString(addr);
                    default:
                        return theValue ? (object)addr : Utils.RealAddressString(addr);
                }
            }
            else
            {
                switch (TypeExtractor.GetStandardKind(fldKind))
                {
                    case ClrElementKind.String:
                        try
                        {
                            //if (TypeExtractor.IsString(fldKind))
                            //    return (string)fld.GetValue(addr, false, true);
                            ulong strAddr = (ulong)fld.GetValue(addr, intern, false);
                            return GetStringAtAddress(strAddr, heap);
                            //string str = (string)fldType.GetValue(strAddr);
                            //return str;
                        }
                        catch
                        {
                            return string.Empty;
                        }
                    case ClrElementKind.SZArray:
                    case ClrElementKind.Array:
                    case ClrElementKind.Object:
                    case ClrElementKind.Class:
                        var fldAddr = GetReferenceFieldAddress(addr, fld, intern);
                        return theValue ? (object)fldAddr : Utils.RealAddressString(fldAddr);
                    case ClrElementKind.Unknown:
                        var ufldAddr = GetReferenceFieldAddress(addr, fld, intern);
                        return Utils.RealAddressString(addr) + Constants.HeavyAsteriskPadded + Utils.RealAddressString(ufldAddr);
                    default:
                        return theValue ? GetPrimitiveValueObject(addr, fld, intern) : PrimitiveValue(addr, fld, intern);
                }
            }

        }

        public static string ValueToString(object val, ClrElementKind kind)
        {
            if (val == null) return Constants.NullValue;
            var specKind = TypeExtractor.GetSpecialKind(kind);
            if (specKind != ClrElementKind.Unknown)
            {
                switch (specKind)
                {
                    case ClrElementKind.Guid:
                        return ((Guid)val).ToString("D");
                    case ClrElementKind.DateTime:
                        return (((DateTime)val)).ToString(CultureInfo.InvariantCulture);
                    case ClrElementKind.TimeSpan:
                        return (((DateTime)val)).ToString("c");
                    case ClrElementKind.Decimal:
                        return (((decimal)val)).ToString(CultureInfo.InvariantCulture);
                    case ClrElementKind.Enum:
                        if (val is long) return ((long)val).ToString(CultureInfo.InvariantCulture);
                        switch (TypeExtractor.GetStandardKind(kind))
                        {
                            case ClrElementKind.Int32:
                                return ((int)val).ToString(CultureInfo.InvariantCulture);
                            case ClrElementKind.Int64:
                                return (((long)val)).ToString(CultureInfo.InvariantCulture);
                            case ClrElementKind.UInt32:
                                return (((uint)val)).ToString(CultureInfo.InvariantCulture);
                            case ClrElementKind.UInt64:
                                return (((ulong)val)).ToString(CultureInfo.InvariantCulture);
                            default:
                                return Constants.NotAvailableValue;
                        }
                    case ClrElementKind.Exception:
                    case ClrElementKind.Free:
                    case ClrElementKind.Abstract:
                    case ClrElementKind.SystemVoid:
                    case ClrElementKind.SystemObject:
                    case ClrElementKind.Interface:
                        return Utils.RealAddressString((ulong)val);
                    case ClrElementKind.SystemNullable:
                        return "true";
                    default:
                        throw new ArgumentException("[ValueExtractor.ValueToString(..)] Invalid kind: " + kind.ToString());
                }
            }
            else
            {
                switch (kind)
                {
                    case ClrElementKind.String:
                        if (val is string) return (string)val;
                        return "Object is not a string.";
                    case ClrElementKind.SZArray:
                    case ClrElementKind.Array:
                    case ClrElementKind.Object:
                    case ClrElementKind.Class:
                        return Utils.RealAddressString((ulong)val);
                    case ClrElementKind.Unknown:
                        throw new ArgumentException("[ValueExtractor.ValueToString(..)] Invalid kind: " + kind.ToString());
                    default:
                        return PrimitiveValue(val, kind);
                }
            }

        }


        public static string GetTypeValueAsString(ClrHeap heap, ulong addr, ClrType clrType, ClrElementKind kind, bool intern)
        {
            if (kind == ClrElementKind.Unknown)
            {
                kind = TypeExtractor.GetElementKind(clrType);
                if (kind == ClrElementKind.Unknown)
                {
                    return Constants.UnknownValue;
                }
            }
            var specKind = TypeExtractor.GetSpecialKind(kind);

            if (specKind != ClrElementKind.Unknown)
            {
                switch (specKind)
                {
                    case ClrElementKind.Guid:
                        return GuidValueAsString(addr, clrType, intern);
                    case ClrElementKind.DateTime:
                        return DateTimeValueAsString(addr, clrType);
                    case ClrElementKind.TimeSpan:
                        return TimeSpanValueAsString(addr, clrType);
                    case ClrElementKind.Decimal:
                        return DecimalValueAsString(addr, clrType, null);
                    case ClrElementKind.Exception:
                        return GetShortExceptionValue(addr, clrType, heap);
                    case ClrElementKind.Enum:
                        long enumInt;
                        return GetEnumValueString(addr, clrType, out enumInt);
                    case ClrElementKind.Free:
                    case ClrElementKind.SystemVoid:
                        return Utils.RealAddressString(addr);
                }

            }

            var stdKind = TypeExtractor.GetStandardKind(kind);

            switch (stdKind)
            {
                case ClrElementKind.String:
                    return GetStringAtAddress(addr,heap);
                case ClrElementKind.SZArray:
                case ClrElementKind.Array:
                case ClrElementKind.Object:
                case ClrElementKind.Class:
                    return Utils.RealAddressString(addr);
                default:
                    return GetPrimitiveValue(addr, clrType);
            }
        }


        public static string GetFieldValue(IndexProxy ndxProxy, ClrHeap heap, ulong addr, ClrInstanceField fld, bool intern, out ClrType fldType, out ClrElementKind kind, out ulong fldAddr)
        {
            fldType = fld.Type;
            kind = TypeExtractor.GetElementKind(fldType);
            fldAddr = Constants.InvalidAddress;
            if (TypeExtractor.IsUnknownStruct(kind))
            {
                fldAddr = fld.GetAddress(addr, true);
                return Utils.RealAddressString(fldAddr);
            }

            var specKind = TypeExtractor.GetSpecialKind(kind);
            if (specKind != ClrElementKind.Unknown)
            {
                fldAddr = intern ? fld.GetAddress(addr, true) : addr;
                switch (specKind)
                {
                    case ClrElementKind.Guid:
                        //return GuidValue(fldAddr, fld);   // TODO JRD
                        return GetGuidValue(addr, fld, intern);
                    case ClrElementKind.DateTime:
                        return DateTimeValue(addr, fld, intern);
                    case ClrElementKind.TimeSpan:
                        return TimeSpanValueString(addr, fld, intern);
                    case ClrElementKind.Decimal:
                        return DecimalStringPAF(addr, fld, intern);
                    case ClrElementKind.Exception:
                        return Utils.RealAddressString(fldAddr);
                    case ClrElementKind.Enum:
                        return GetEnumString(addr, fld, false);
                    case ClrElementKind.SystemNullable:
                        fldAddr = fld.GetAddress(addr, true);
                        return NullableValueAsString(heap, fldAddr, fld.Type);
                    case ClrElementKind.Free:
                    case ClrElementKind.SystemVoid: // TODO JRD -- get the pointer address?
                        fldAddr = GetReferenceFieldAddress(addr, fld, false);
                        return Utils.RealAddressString(fldAddr);
                }

                if (TypeExtractor.IsAmbiguousKind(kind))
                {
                    fldAddr = GetReferenceFieldAddress(addr, fld, false);
                    var tempType = heap.GetObjectType(fldAddr);
                    if (tempType != null)
                    {
                        fldType = tempType;
                        var newkind = TypeExtractor.GetElementKind(fldType);
                        if (newkind != kind)
                        {
                            return GetTypeValueAsString(heap, fldAddr, fldType, newkind, intern);
                        }

                        specKind = TypeExtractor.GetSpecialKind(kind);
                    }
                }
            }

            if (kind == ClrElementKind.Unknown)
                throw new MdrException("[ValueExtractor.GetFieldValue] Some strange address: " + Utils.RealAddressString(addr) + " fld " + fld.Name + " " + Utils.RealAddressString(fldAddr));

            if (specKind != ClrElementKind.Unknown)
            {
                switch (specKind)
                {
                    case ClrElementKind.Guid:
                        return GuidValue(fldAddr, fld);
                    case ClrElementKind.DateTime:
                        return DateTimeValue(fldAddr, fld);
                    case ClrElementKind.TimeSpan:
                        return TimeSpanValue(fldAddr, fld);
                    case ClrElementKind.Decimal:
                        return DecimalStringPAF(addr, fld, intern);
                    case ClrElementKind.Exception:
                        return Utils.RealAddressString(fldAddr);
                    case ClrElementKind.Enum:
                        return GetEnumString(fldAddr, fld, intern);
                    case ClrElementKind.Free:
                    case ClrElementKind.SystemVoid: // TODO JRD -- get the pointer address?
                        fldAddr = GetReferenceFieldAddress(addr, fld, false);
                        return Utils.RealAddressString(fldAddr);
                    default:
                        return Utils.RealAddressString(fldAddr);
                }
            }
            else
            {
                switch (kind)
                {
                    case ClrElementKind.String:
                        fldAddr = GetReferenceFieldAddress(addr, fld, false);
                        if (fldAddr != Constants.InvalidAddress)
                            return GetStringAtAddress(fldAddr, heap);
                        else
                            return Constants.NullValueOld;
                    case ClrElementKind.SZArray:
                    case ClrElementKind.Array:
                    case ClrElementKind.Object:
                    case ClrElementKind.Class:
                        fldAddr = GetReferenceFieldAddress(addr, fld, false);
                        return Utils.RealAddressString(fldAddr);
                    default:
                        return PrimitiveValue(addr, fld, intern);
                }
            }
        }

        public static InstanceValue[] GetFieldValues(IndexProxy ndxProxy, ClrHeap heap, ClrType clrType, ClrElementKind kind, ulong addr, InstanceValue parent, out string error)
        {
            error = null;
            if (clrType.Fields == null || clrType.Fields.Count < 1) return Utils.EmptyArray<InstanceValue>.Value;
            InstanceValue[] fields = new InstanceValue[clrType.Fields.Count];
            for (int i = 0, icnt = clrType.Fields.Count; i < icnt; ++i)
            {
                var fld = clrType.Fields[i];
                ClrElementKind fldKind;
                ClrType fldType;
                ulong fldAddr;
                var value = GetFieldValue(ndxProxy, heap, addr, clrType.Fields[i], TypeExtractor.IsUnknownStruct(kind), out fldType, out fldKind, out fldAddr);
                var ftypeId = ndxProxy.GetTypeId(fldType.Name);
                fields[i] = new InstanceValue(ftypeId, fldKind, fldAddr, fldType.Name, fld.Name, value, i, parent);
            }
            return fields;
        }

        public static ValueTuple<string, InstanceValue> GetInstanceValue(IndexProxy ndxProxy, ClrHeap heap, ulong decoratedAddr, int fldNdx, InstanceValue parent)
        {
            var addr = Utils.RealAddress(decoratedAddr);
            (ClrType clrType, ClrElementKind kind, string realName) = TypeExtractor.TryGetRealType(heap, addr);
            var specKind = TypeExtractor.GetSpecialKind(kind);
            var typeId = ndxProxy.GetTypeIdAtAddr(addr);
            if (typeId == Constants.InvalidIndex) typeId = ndxProxy.GetTypeId(clrType.Name);
            bool intern = (parent != null) ? parent.IsValueClass() : false;
            string value;
            if (specKind != ClrElementKind.Unknown)
            {
                switch (specKind)
                {
                    case ClrElementKind.Guid:
                        value = GuidValueAsString(addr, clrType, intern);
                        return (null, new InstanceValue(typeId, kind, addr, clrType.Name, value, Utils.RealAddressString(decoratedAddr), fldNdx, parent));
                    case ClrElementKind.DateTime:
                        value = DateTimeValueAsString(addr, clrType);
                        return (null, new InstanceValue(typeId, kind, addr, clrType.Name, value, Utils.RealAddressString(decoratedAddr), fldNdx, parent));
                    case ClrElementKind.TimeSpan:
                        value = TimeSpanValueAsString(addr, clrType);
                        return (null, new InstanceValue(typeId, kind, addr, clrType.Name, value, Utils.RealAddressString(decoratedAddr), fldNdx, parent));
                    case ClrElementKind.Decimal:
                        value = DecimalValueAsString(addr, clrType, "0,0.00");
                        return (null, new InstanceValue(typeId, kind, addr, clrType.Name, value, Utils.RealAddressString(decoratedAddr), fldNdx, parent));
                    case ClrElementKind.Exception:
                        value = GetShortExceptionValue(addr, clrType, heap);
                        return (null, new InstanceValue(typeId, kind, addr, clrType.Name, value, Utils.RealAddressString(decoratedAddr), fldNdx, parent));
                    case ClrElementKind.Enum:
                        long enumVal;
                        value = GetEnumValueString(addr, clrType,out enumVal);
                        return (null, new InstanceValue(typeId, kind, addr, clrType.Name, string.Empty, value, fldNdx, parent));
                    case ClrElementKind.Free:
                    case ClrElementKind.Abstract:
                    case ClrElementKind.SystemVoid:
                    case ClrElementKind.SystemObject:
                        return (null, new InstanceValue(typeId, kind, addr, clrType.Name, string.Empty, Utils.RealAddressString(decoratedAddr), fldNdx, parent));
                    default:
                        return ("Unexpected special kind", new InstanceValue(typeId, kind, addr, clrType.Name, string.Empty, Utils.RealAddressString(decoratedAddr), fldNdx, parent));
                }
            }
            else
            {
                switch (kind)
                {
                    case ClrElementKind.String:
                        var str = GetStringAtAddress(addr, heap);
                        var sinst = new InstanceValue(typeId, kind, addr, clrType.Name, string.Empty, str, fldNdx, parent);
                        return (null, sinst);
                    case ClrElementKind.SZArray:
                    case ClrElementKind.Array:
                        return ArrayContent(ndxProxy, heap, addr, parent);
                    case ClrElementKind.Struct:
                    case ClrElementKind.Object:
                    case ClrElementKind.Class:
                        var inst = new InstanceValue(typeId, kind, addr, clrType.Name, string.Empty, Utils.RealAddressString(decoratedAddr), fldNdx, parent);
                        string error;
                        var fldValues = GetFieldValues(ndxProxy, heap, clrType, kind, addr, inst, out error);
                        inst.SetFields(fldValues);
                        return (error, inst);
                    case ClrElementKind.Unknown:
                        return ("Unknown element type.", null);
                    default:
                        var val = GetPrimitiveValue(addr, clrType);
                        var dinst = new InstanceValue(typeId, kind, addr, clrType.Name, string.Empty, val, fldNdx, parent);
                        return (null, dinst);
                }
            }
        }

        public static ValueTuple<string, InstanceValue[]> GetInstanceValueFields(IndexProxy ndxProxy, ClrHeap heap, ulong decoratedAddr, InstanceValue parent)
        {
            string error;
            InstanceValue[] fldValues;
            var addr = Utils.RealAddress(decoratedAddr);
            (ClrType clrType, ClrElementKind kind, string realName) = TypeExtractor.TryGetRealType(heap, addr);
            var specKind = TypeExtractor.GetSpecialKind(kind);
            var typeId = ndxProxy.GetTypeIdAtAddr(addr);
            if (typeId == Constants.InvalidIndex) typeId = ndxProxy.GetTypeId(clrType.Name);
            if (specKind != ClrElementKind.Unknown)
            {
                switch (specKind)
                {
                    case ClrElementKind.Guid:
                        fldValues = GetFieldValues(ndxProxy, heap, clrType, kind, addr, parent, out error);
                        parent.SetFields(fldValues);
                        return (error, fldValues);
                    case ClrElementKind.DateTime:
                        fldValues = GetFieldValues(ndxProxy, heap, clrType, kind, addr, parent, out error);
                        parent.SetFields(fldValues);
                        return (error, fldValues);
                    case ClrElementKind.TimeSpan:
                        fldValues = GetFieldValues(ndxProxy, heap, clrType, kind, addr, parent, out error);
                        parent.SetFields(fldValues);
                        return (error, fldValues);
                    case ClrElementKind.Decimal:
                        fldValues = GetFieldValues(ndxProxy, heap, clrType, kind, addr, parent, out error);
                        parent.SetFields(fldValues);
                        return (error, fldValues);
                    case ClrElementKind.Exception:
                        fldValues = GetFieldValues(ndxProxy, heap, clrType, kind, addr, parent, out error);
                        parent.SetFields(fldValues);
                        return (error, fldValues);
                    case ClrElementKind.Error:
                    case ClrElementKind.Free:
                    case ClrElementKind.Abstract:
                    case ClrElementKind.SystemVoid:
                    case ClrElementKind.SystemObject:
                        parent.SetFields(Utils.EmptyArray<InstanceValue>.Value);
                        return (Constants.InformationSymbolHeader + "There are no fields available for this type: " + clrType.Name, Utils.EmptyArray<InstanceValue>.Value);
                    default:
                        parent.SetFields(Utils.EmptyArray<InstanceValue>.Value);
                        return (Constants.InformationSymbolHeader + "Unexpected type. There are no fields available for: " + clrType.Name, Utils.EmptyArray<InstanceValue>.Value);
                }
            }
            else
            {
                switch (kind)
                {
                    case ClrElementKind.String:
                        parent.SetFields(Utils.EmptyArray<InstanceValue>.Value);
                        return (Constants.InformationSymbolHeader + "We treat the string types as primitives, there are no fields available.", Utils.EmptyArray<InstanceValue>.Value);
                    case ClrElementKind.SZArray:
                    case ClrElementKind.Array:
                        (string err, InstanceValue inst) = ArrayContent(ndxProxy, heap, decoratedAddr, parent);
                        return (err, new InstanceValue[] { inst });
                    case ClrElementKind.Struct:
                    case ClrElementKind.Object:
                    case ClrElementKind.Class:
                        fldValues = GetFieldValues(ndxProxy, heap, clrType, kind, addr, parent, out error);
                        parent.SetFields(fldValues);
                        return (error, fldValues);
                    case ClrElementKind.Unknown:
                        return ("Unknown element type.", null);
                    default:
                        parent.SetFields(Utils.EmptyArray<InstanceValue>.Value);
                        return (Constants.InformationSymbolHeader + "The primitive types do not have fields.", Utils.EmptyArray<InstanceValue>.Value);
                }
            }
        }

        public static string GetFieldValueString(ClrHeap heap, ulong addr, bool intr, ClrInstanceField fld, ClrElementKind fldKind, StructFieldsEx structFld=null)
        {
            if (structFld != null)
            {
                var fldAddr = fld.GetAddress(addr, intr);
                return StructFieldsEx.StructString(structFld, heap, fldAddr);
            }

            if (TypeExtractor.IsKnownStruct(fldKind))
            {
                switch(TypeExtractor.GetSpecialKind(fldKind))
                {
                    case ClrElementKind.Decimal:
                        return DecimalStringPAF(addr, fld, intr);
                    case ClrElementKind.DateTime:
                        return GetDateTimeValue(addr, fld, intr);
                    case ClrElementKind.TimeSpan:
                        return TimeSpanValue(addr, fld);
                    case ClrElementKind.Guid:
                        return GuidValue(addr, fld);
                }
            }

            if (TypeExtractor.IsString(fldKind))
            {
                return fld.GetValue(addr, intr,true) as string;
            }

            if (TypeExtractor.IsObjectReference(fldKind))
            {
                ulong fldAddr = (ulong)fld.GetValue(addr,intr,false);
                return Utils.RealAddressString(fldAddr);
            }

            if (TypeExtractor.IsEnum(fldKind))
            {
                return GetEnumString(addr, fld, intr);
            }

            if (TypeExtractor.IsKnownPrimitive(fldKind))
            {
                object val = fld.GetValue(addr, intr);
                return PrimitiveValueAsString(val, TypeExtractor.GetClrElementType(fldKind));
            }

            return "Don't know how to get value.";
        }

        public static object GetFieldValue(ClrHeap heap, ulong addr, bool intr, ClrInstanceField fld, ClrElementKind fldKind)
        {
            if (TypeExtractor.IsKnownStruct(fldKind))
            {
                switch (TypeExtractor.GetSpecialKind(fldKind))
                {
                    case ClrElementKind.Decimal:
                        return GetDecimalPAF(addr, fld, intr);
                    case ClrElementKind.DateTime:
                        return GetDateTimeValue(addr, fld, intr);
                    case ClrElementKind.TimeSpan:
                        return TimeSpanValue(addr, fld);
                    case ClrElementKind.Guid:
                        return GuidValue(addr, fld);
                }
            }

            if (TypeExtractor.IsString(fldKind))
            {
                return GetStringAtAddress(addr, heap);
            }

            if (TypeExtractor.IsObjectReference(fldKind))
            {
                var val = fld.GetValue(addr, intr);
                return (val == null || !(val is ulong)) ? Constants.InvalidAddress : val;
            }

            if (TypeExtractor.IsEnum(fldKind))
            {
                return GetEnumStringOfField(addr, fld, intr);
            }

            if (TypeExtractor.IsPrimitive(fldKind))
            {
                return GetPrimitiveValueObject(addr, fld, intr);
            }

            if (TypeExtractor.IsKnownPrimitive(fldKind))
            {
                object val = fld.GetValue(addr, intr);
                return PrimitiveValueAsString(val, TypeExtractor.GetClrElementType(fldKind));
            }

 

            return "Don't know how to get value.";
        }

        public static string GetTypeValueString(ClrHeap heap, ulong addr, ClrType type, bool intr, ClrElementKind kind)
        {
            if (TypeExtractor.IsKnownStruct(kind))
            {
                switch (TypeExtractor.GetSpecialKind(kind))
                {
                    case ClrElementKind.Decimal:
                        return DecimalValueAsString(addr, type, intr);
                    case ClrElementKind.DateTime:
                        return DateTimeValueAsString(addr, type, intr);
                    case ClrElementKind.TimeSpan:
                        return TimeSpanValueAsString(addr, type, intr);
                    case ClrElementKind.Guid:
                        return GuidValueAsString(addr, type, intr);
                }
            }

            if (TypeExtractor.IsString(kind))
            {
                return GetStringAtAddress(addr, heap);
            }

            if (TypeExtractor.IsObjectReference(kind))
            {
                return Utils.RealAddressString(addr);
            }

            if (TypeExtractor.IsEnum(kind))
            {
                long intVal;
                object obj = type.GetValue(addr);
                return GetEnumAsString(addr, type, TypeExtractor.GetClrElementType(kind), obj, out intVal);
            }

            if (TypeExtractor.IsKnownPrimitive(kind))
            {
                object val = type.GetValue(addr);
                return PrimitiveValueAsString(val, TypeExtractor.GetClrElementType(kind));
            }

            return "Don't know how to get value.";

        }

        public static object GetTypeValue(ClrHeap heap, ulong addr, ClrType type, ClrElementKind kind)
        {
            if (TypeExtractor.IsKnownStruct(kind))
            {
                switch (TypeExtractor.GetSpecialKind(kind))
                {
                    case ClrElementKind.Decimal:
                        return GetDecimalValue(addr, type);
                    case ClrElementKind.DateTime:
                        return DateTimeValue(addr, type);
                    case ClrElementKind.TimeSpan:
                        return TimeSpanValue(addr, type);
                    case ClrElementKind.Guid:
                        return GuidValue(addr, type);
                }
            }

            if (TypeExtractor.IsString(kind))
            {
                return GetStringAtAddress(addr, heap);
            }

            if (TypeExtractor.IsObjectReference(kind))
            {
                return addr;
            }

            if (TypeExtractor.IsEnum(kind))
            {
                return GetEnumValue(addr, type, TypeExtractor.GetClrElementType(kind));
            }

            if (TypeExtractor.IsKnownPrimitive(kind))
            {
                return type.GetValue(addr);
            }

            throw new ApplicationException("[ValueExtractor.GetTypeValue(..)]: Don't know how to get value.");
        }

        #region utils

        public static string GetFieldString(ClrHeap heap, ulong addr, ClrType type, string[] fldNames)
        {
            int ndx = 0;
            ClrInstanceField fld = type.GetFieldByName(fldNames[ndx++]);
            ClrType valType = type;
            while(ndx < fldNames.Length)
            {
                object val = fld.GetValue(addr, valType.IsValueClass, false);
                ulong valAddr = val != null ? (ulong)val : 0UL;
                if (valAddr == 0UL) return Constants.UnknownValue;
                addr = valAddr;
                valType = heap.GetObjectType(addr);
                if (valType == null) return Constants.UnknownValue;
                fld = valType.GetFieldByName(fldNames[ndx++]);
            }
            string strVal = fld.GetValue(addr, valType.IsValueClass, true) as string;
            return (strVal == null) ? Constants.NullValue : strVal;
        }

        public static ValueTuple<ClrType, ClrInstanceField, ClrElementKind> FindFieldByName(ValueTuple<ClrType, ClrInstanceField, ClrElementKind>[] fldInfos, string name)
        {
            for (int i = 0, icnt = fldInfos.Length; i < icnt; ++i)
            {
                if (Utils.SameStrings(fldInfos[i].Item2.Name, name))
                    return fldInfos[i];
            }
            return (null, null, ClrElementKind.Unknown);
        }

        public static int GetFieldIntValue(ValueTuple<ClrType, ClrInstanceField, ClrElementKind>[] fldInfos, string name, ClrHeap heap, ulong addr, bool intr)
        {
            for (int i = 0, icnt = fldInfos.Length; i < icnt; ++i)
            {
                if (Utils.SameStrings(fldInfos[i].Item2.Name, name))
                {
                    return GetFieldIntValue(heap, addr, fldInfos[i].Item2, intr);
                }
            }
            return 0; // TODO JRD ???
        }
 

        public static ValueTuple<ClrType,ClrElementKind> GetFieldType(ClrHeap heap, ulong parentAddr, ClrInstanceField fld)
        {
            ClrElementKind kind = TypeExtractor.GetElementKind(fld.Type);
            if (kind == ClrElementKind.Unknown) // fld.Type == null
            {
                return (null, ClrElementKind.Unknown); // TODO JRD
            }
            return (fld.Type, kind);
        }

        #endregion utils

        #region known collections

        public static int GetFieldIntValue(ClrHeap heap, ulong addr, ClrType clrType, string fldName)
        {
            var fld = clrType.GetFieldByName(fldName);
            if (fld == null) return int.MinValue;
            var obj = fld.GetValue(addr, clrType.IsValueClass, false);
            if (obj == null) return 0;
            Debug.Assert(obj is Int32);
            return (int)obj;
        }

        public static int GetFieldIntValue(ClrHeap heap, ulong addr, ClrInstanceField fld, bool intr)
        {
            var obj = fld.GetValue(addr, intr, false);
            if (obj == null) return 0;
            Debug.Assert(obj is Int32);
            return (int)obj;
        }

        public static ulong GetFieldAddressValue(ClrHeap heap, ulong addr, ClrInstanceField fld, bool intr)
        {
            var obj = fld.GetValue(addr, intr, false);
            if (obj == null) return 0;
            Debug.Assert(obj is ulong);
            return (ulong)obj;
        }

        public static long GetFieldLongValue(ClrHeap heap, ulong addr, ClrInstanceField fld, bool intr)
        {
            var obj = fld.GetValue(addr, intr, false);
            if (obj == null) return 0;
            Debug.Assert(obj is long);
            return (long)obj;
        }


        public static ValueTuple<string, ValueTuple<ClrType, ClrInstanceField, ClrElementKind, object>[]>
        GetCollectionInfo(ClrHeap heap, ulong addr, ClrType clrType, string[] flds)
        {
            try
            {
                var result = new ValueTuple<ClrType, ClrInstanceField, ClrElementKind, object>[flds.Length];
                for (int i = 0, icnt = flds.Length; i < icnt; ++i)
                {
                    ClrInstanceField fld = clrType.GetFieldByName(flds[i]);
                    (ClrType fldType,ClrElementKind fldKind) = GetFieldType(heap, addr, fld);
                    object obj = fld.GetValue(addr);
                    result[i] = (fldType, fld, fldKind, obj);
                }

                return (null, result);
            }
            catch (Exception ex)
            {
                var error = Utils.GetExceptionErrorString(ex);
                return (error, null);
            }
        }

        #region System.Collections.Generic.List<T>

        public static ValueTuple<ClrType, ClrType, ClrElementKind, ulong, int, int, int> ListInfo(ClrHeap heap, ulong addr, out string error)
        {
            error = null;
            var clrType = heap.GetObjectType(addr);
            if (clrType == null)
            {
                error = "Cannot get type at address: " + Utils.RealAddressString(addr);
                return (null, null, ClrElementKind.Unknown, 0Ul, 0, 0, 0);
            }
            if (!clrType.Name.StartsWith("System.Collections.Generic.List<"))
            {
                error = "The type at address: " + Utils.RealAddressString(addr) + " is not List<T>.";
                return (null, null, ClrElementKind.Unknown, 0UL, 0, 0, 0);
            }
            var itemsFld = clrType.GetFieldByName("_items");
            var sizeFld = clrType.GetFieldByName("_size");
            var versionFld = clrType.GetFieldByName("_version");

            var itemsobj = itemsFld.GetValue(addr, false, false);
            ulong itemsAddr = itemsobj == null ? 0UL : (ulong)itemsobj;
            var len = (int)sizeFld.GetValue(addr, false, false);
            var version = (int)versionFld.GetValue(addr, false, false);
            var itemsClrType = heap.GetObjectType(itemsAddr);
            var kind = TypeExtractor.GetElementKind(itemsClrType.ComponentType);
            int aryLen = itemsClrType.GetArrayLength(itemsAddr);
            return (clrType, itemsClrType, kind, itemsAddr, len,aryLen,version);
        }


        public static ValueTuple<string, KeyValuePair<string, string>[], string[]> GetListContent(ClrHeap heap, ulong addr)
        {
            try
            {
                string error;
                (ClrType lstType, ClrType itemsType, ClrElementKind itemKind, ulong itemAryAddr, int lstSize, int aryLen, int version) = 
                    ListInfo(heap, addr, out error);
                if (error != null) return (error, null, null);
                List<string> types = new List<string>();
                string[] items = GetAryItems(heap, itemAryAddr, itemsType, itemsType.ComponentType, itemKind, lstSize, types);
                
                KeyValuePair<string, string>[] fldDescription = new KeyValuePair<string, string>[]
                {
                    new KeyValuePair<string, string>("size", Utils.CountString(lstSize)),
                    new KeyValuePair<string, string>("array count", Utils.CountString(aryLen)),
                    new KeyValuePair<string, string>("version", version.ToString())
                };
                return (null, fldDescription, items);
            }
            catch(Exception ex)
            {
                return (Utils.GetExceptionErrorString(ex), null, null);
            }
        }

        #endregion System.Collections.Generic.List<T> content

        #region System.Collections.Generic.SortedList<TKey, TValue>
 
        public static ValueTuple<string, KeyValuePair<string, string>[], KeyValuePair<string, string>[]> GetSortedListContent(ClrHeap heap, ulong addr)
        {
            try
            {
                ClrType clrType = heap.GetObjectType(addr);

                int count = GetFieldIntValue(heap, addr, clrType, "_size");
                int version = GetFieldIntValue(heap, addr, clrType, "version");
                var keysFld = clrType.GetFieldByName("keys");
                ulong keysFldAddr = GetReferenceFieldAddress(addr,keysFld,false);
                (string err1, ClrType keysFldType, ClrType keyType, ClrElementKind keyFldKind, int len1) = ArrayInfo(heap, keysFldAddr);
                var aryLen = keysFldType == null ? 0 : keysFldType.GetArrayLength(keysFldAddr);
                KeyValuePair<string, string>[] fldDescription = new KeyValuePair<string, string>[]
                {
                new KeyValuePair<string, string>("count", count.ToString()),
                new KeyValuePair<string, string>("array count", aryLen.ToString()),
                new KeyValuePair<string, string>("version", version.ToString())
                };
                if (err1 != null || count < 1)
                {
                    return (err1, fldDescription, Utils.EmptyArray<KeyValuePair<string, string>>.Value);
                }
                var valuesFld = clrType.GetFieldByName("values");
                ulong valuesFldAddr = GetReferenceFieldAddress(addr, valuesFld, false);
                (string err2, ClrType valuesFldType, ClrType valueType, ClrElementKind valueFldKind, int len2) = ArrayInfo(heap, valuesFldAddr);
                if (err2!= null)
                {
                    return (err1, fldDescription, Utils.EmptyArray<KeyValuePair<string, string>>.Value);
                }
                Debug.Assert(len1 == len2);
                var values = new KeyValuePair<string, string>[count];

                List<string> types = new List<string>() { keyType.Name };
                var keyItems = TypeExtractor.IsUnknownStruct(keyFldKind)
                    ? GetAryStructItems(heap, keysFldAddr, keysFldType, keyType, keyFldKind, count, types)
                    : GetAryItems(heap, keysFldAddr, keysFldType, keyType, keyFldKind, count, types);
                types.Clear();
                types.Add(valueType.Name);
                var valueItems = TypeExtractor.IsUnknownStruct(valueFldKind)
                    ? GetAryStructItems(heap, valuesFldAddr, valuesFldType, valueType, valueFldKind, count, types)
                    : GetAryItems(heap, valuesFldAddr, valuesFldType, valueType, valueFldKind, count, types);

                for (int i = 0; i < count; ++i)
                {
                    values[i] = new KeyValuePair<string, string>(keyItems[i], valueItems[i]);
                }
                return (null, fldDescription, values);
            }
            catch (Exception ex)
            {
                return (Utils.GetExceptionErrorString(ex), null, null);
            }
        }

        #endregion SortedList<TKey, TValue> 

        //#region System.Collections.Generic.Dictionary<TKey, TValue>

        //public static ValueTuple<string, KeyValuePair<string, string>[] , KeyValuePair<string, string>[]> GetDictionaryContent(ClrHeap heap, ulong addr)
        //{
        //    try
        //    {
        //        ClrType dctClrType = heap.GetObjectType(addr);
        //        if (!TypeExtractor.Is(TypeExtractor.KnownTypes.Dictionary, dctClrType.Name))
        //            return ("Instance at: " + Utils.RealAddressString(addr) + " is not " + TypeExtractor.GetKnowTypeName(TypeExtractor.KnownTypes.Dictionary), null, null);
        //        ValueTuple<ClrType, ClrInstanceField, ClrElementKind>[] dctFldInfos = TypeExtractor.GetFieldsAndKinds(dctClrType);
        //        (ClrType entriesType, ClrInstanceField entriesFld, ClrElementKind emtriesKind) = FindFieldByName(dctFldInfos, "entries");
        //        Debug.Assert(entriesType != null);
        //        ulong entriesAddr = GetReferenceFieldAddress(addr, entriesFld, false);
 
        //        int count = GetFieldIntValue(dctFldInfos, "count", heap, addr, false);
        //        int version = GetFieldIntValue(dctFldInfos, "version", heap, addr, false);
        //        int freeCount = GetFieldIntValue(dctFldInfos, "freeCount", heap, addr, false);
        //        var aryLen = entriesType == null ? 0 : entriesType.GetArrayLength(entriesAddr);

        //        KeyValuePair<string, string>[] fldDescription = new KeyValuePair<string, string>[]
        //        {
        //        new KeyValuePair<string, string>("count", Utils.CountString(count-freeCount)),
        //        new KeyValuePair<string, string>("array count", Utils.CountString(aryLen)),
        //        new KeyValuePair<string, string>("version", version.ToString())
        //        };

        //        if (entriesType == null || entriesAddr == Constants.InvalidAddress || (count - freeCount) < 1)
        //            return (TypeExtractor.GetKnowTypeName(TypeExtractor.KnownTypes.Dictionary) + " is empty.", fldDescription, null);

        //        (string error, ClrType exEntriesType, ClrType elemType, ClrElementKind elemKind, int len) = ArrayInfo(heap, entriesAddr);
        //        if (error != null)
        //            return (error, fldDescription, null);
        //        ValueTuple<ClrType, ClrInstanceField, ClrElementKind>[] fldInfos = TypeExtractor.GetFieldsAndKinds(elemType);

        //        (ClrType hashType, ClrInstanceField hashFld, ClrElementKind hashKind) = FindFieldByName(fldInfos, "hashCode");
        //        (ClrType nextType, ClrInstanceField nextFld, ClrElementKind nextKind) = FindFieldByName(fldInfos, "next");
        //        (ClrType keyType, ClrInstanceField keyFld, ClrElementKind keyKind) = FindFieldByName(fldInfos, "key");
        //        (ClrType valType, ClrInstanceField valFld, ClrElementKind valKind) = FindFieldByName(fldInfos, "value");

        //        var values = new List<KeyValuePair<string, string>>(count - freeCount);
        //        for (int i = 0; i < aryLen; ++i)
        //        {
        //            // TODO JRD -- handle structures here as in array
        //            var eaddr = entriesType.GetArrayElementAddress(entriesAddr, i);
        //            var hash = GetFieldIntValue(heap, eaddr, hashFld, true);
        //            if (hash <= 0) continue;
        //            string keyVal = (string)GetFieldValue(heap, eaddr, keyFld, keyType, keyKind, true, false);
        //            string valueVal = (string)GetFieldValue(heap, eaddr, valFld, valType, valKind, true, false);
        //            values.Add(new KeyValuePair<string, string>(keyVal, valueVal));
        //        }
        //        return (null, fldDescription,values.ToArray());
        //    }
        //    catch (Exception ex)
        //    {
        //        string error = Utils.GetExceptionErrorString(ex);
        //        return (error, null, null);
        //    }
        //}

        //#endregion System.Collections.Generic.Dictionary<TKey, TValue>

        #region System.Collections.Generic.SortedDictionary<TKey, TValue>

        public static ValueTuple<string, KeyValuePair<string, string>[], KeyValuePair<string, string>[]> GetSortedDictionaryContent(ClrHeap heap, ulong addr)
        {
            try
            {
                ClrType dctType = heap.GetObjectType(addr);

                var setFld = dctType.GetFieldByName("_set"); // get TreeSet 
                var setFldAddr = (ulong)setFld.GetValue(addr, false, false);
                var setType = heap.GetObjectType(setFldAddr);
                var count = GetFieldIntValue(heap, setFldAddr, setType, "count");
                var version = GetFieldIntValue(heap, setFldAddr, setType, "version");
                var rootFld = setType.GetFieldByName("root"); // get TreeSet root node
                var rootFldAddr = (ulong)rootFld.GetValue(setFldAddr, false, false);
                var rootType = heap.GetObjectType(rootFldAddr);
                var leftNodeFld = rootType.GetFieldByName("Left");
                var rightNodeFld = rootType.GetFieldByName("Right");
                var itemNodeFld = rootType.GetFieldByName("Item");

                var keyFld = itemNodeFld.Type.GetFieldByName("key");
                var valFld = itemNodeFld.Type.GetFieldByName("value");
                var itemAddr = itemNodeFld.GetAddress(rootFldAddr, false);
                (ClrType keyFldType, ClrElementKind keyFldKind, ulong keyFldAddr) =
                        TypeExtractor.GetRealType(heap, itemAddr, keyFld, true);
                (ClrType valFldType, ClrElementKind valFldKind, ulong valFldAddr) =
                  TypeExtractor.GetRealType(heap, itemAddr, valFld, true);

                KeyValuePair<string, string>[] fldDescription = new KeyValuePair<string, string>[]
                {
                new KeyValuePair<string, string>("count", (count).ToString()),
                new KeyValuePair<string, string>("version", version.ToString())
                };

                var stack = new Stack<ulong>(2 * Utils.Log2(count + 1));
                var node = rootFldAddr;
                while (node != Constants.InvalidAddress)
                {
                    stack.Push(node);
                    node = GetReferenceFieldAddress(node, leftNodeFld, false);
                    //if (left != Constants.InvalidAddress) node = left;
                    //else
                    //{
                    //    var right = GetReferenceFieldAddress(node, rightNodeFld, false);
                    //    node = right;
                    //}
                }

                var values = new List<KeyValuePair<string, string>>(count);

                while (stack.Count > 0)
                {
                    node = stack.Pop();
                    var iAddr = itemNodeFld.GetAddress(node); // GetReferenceFieldAddress(node, itemNodeFld, false);
                    var keyStr = (string)GetFieldValue(heap, iAddr, keyFld, keyFldType, keyFldKind, true, false);
                    var valStr = (string)GetFieldValue(heap, iAddr, valFld, valFldType, valFldKind, true, false);
                    values.Add(new KeyValuePair<string, string>(keyStr, valStr));
                    node = GetReferenceFieldAddress(node, rightNodeFld, false);
                    //getReferenceFieldAddress node rightNodeFld false
                    while (node != Constants.InvalidAddress)
                    {
                        stack.Push(node);
                        node = GetReferenceFieldAddress(node, leftNodeFld, false);
                        if (node == Constants.InvalidAddress)
                            node = GetReferenceFieldAddress(node, rightNodeFld, false);
                    }
                }
                return (null, fldDescription, values.ToArray());
            }
            catch (Exception ex)
            {
                string error = Utils.GetExceptionErrorString(ex);
                return (error, null, null);
            }
        }

        #endregion System.Collections.Generic.SortedDictionary<TKey, TValue>

        //#region System.Collections.Generic.HashSet<T>

        //private static ValueTuple<ClrType, ClrInstanceField, ClrElementKind, ClrInstanceField, ClrInstanceField> GetHashSetSlotTypeInfo(ClrHeap heap, ulong hashSetAddr, ulong slotAddr, ClrType slotsType, int lastIndex)
        //{
        //    ClrType slotType = slotsType.ComponentType;
        //    ClrInstanceField hashCodeFld = slotType.GetFieldByName("hashCode");
        //    ClrInstanceField valueFld = slotType.GetFieldByName("value");
        //    ClrInstanceField nextFld = slotType.GetFieldByName("next");
        //    ClrElementKind valueFldTypeKind = TypeExtractor.GetElementKind(valueFld.Type);

        //    return (slotType, valueFld, valueFldTypeKind, hashCodeFld, nextFld);
        //}

        //public static ValueTuple<string, KeyValuePair<string, string>[], string[]> GetHashSetContent(ClrHeap heap, ulong addr)
        //{
        //    try
        //    {
        //        addr = Utils.RealAddress(addr);
        //        ClrType clrType = heap.GetObjectType(addr);
        //        if (!TypeExtractor.Is(TypeExtractor.KnownTypes.HashSet, clrType.Name))
        //            return ("Instance at: " + Utils.RealAddressString(addr) + " is not " + TypeExtractor.GetKnowTypeName(TypeExtractor.KnownTypes.HashSet), null, null);

        //        int count = GetFieldIntValue(heap, addr, clrType, "m_count");
        //        if (count < 1)
        //        {
        //            return (null, Utils.EmptyArray<KeyValuePair<string, string>>.Value, Utils.EmptyArray<string>.Value);
        //        }
        //        int lastIndex = GetFieldIntValue(heap, addr, clrType, "m_lastIndex");
        //        int version = GetFieldIntValue(heap, addr, clrType, "m_version");
        //        ClrInstanceField slotsFld = clrType.GetFieldByName("m_slots");
        //        (ClrType slotsFldType, ClrElementKind slotsFldKind, ulong slotsFldAddr) = TypeExtractor.GetRealType(heap, addr, slotsFld, false);
        //        var aryLen = slotsFldType == null ? 0 : slotsFldType.GetArrayLength(slotsFldAddr);
        //        KeyValuePair<string, string>[] fldDescription = new KeyValuePair<string, string>[]
        //        {
        //            new KeyValuePair<string, string>("count", count.ToString()),
        //            new KeyValuePair<string, string>("slot count", aryLen.ToString()),
        //            new KeyValuePair<string, string>("last index", lastIndex.ToString()),
        //            new KeyValuePair<string, string>("version", version.ToString())
        //        };

        //        (ClrType slotType, ClrInstanceField slotValueFld, ClrElementKind valueFldTypeKind, ClrInstanceField slotHashFld, ClrInstanceField slotNextFld) = GetHashSetSlotTypeInfo(heap, addr, slotsFldAddr, slotsFldType, lastIndex);

        //        var slotTypeName = slotType.Name;
        //        var slotValueFldTypeName = slotValueFld.Type.Name;

        //        string[] values = new string[count];
        //        int copied = 0;
        //        for (int i = 0; i < lastIndex && copied < count; ++i)
        //        {
        //            var eaddr = slotsFldType.GetArrayElementAddress(slotsFldAddr, i);
        //            int hash = GetFieldIntValue(heap, eaddr, slotHashFld, true);
        //            if (hash < 0) continue;
        //            string val = (string)GetFieldValue(heap, eaddr, slotValueFld, slotValueFld.Type, valueFldTypeKind, true, false);
        //            values[copied++] = val;
        //        }

        //        return (null, fldDescription, values);
        //    }
        //    catch (Exception ex)
        //    {
        //        string error = Utils.GetExceptionErrorString(ex);
        //        return (error, null, null);
        //    }

        //}


        //#endregion System.Collections.Generic.HashSet<T>

        #region System.Collections.Generic.Queue<T>
        /// <summary>
        /// TODO JRD
        /// </summary>
        /// <param name="heap"></param>
        /// <param name="addr"></param>
        /// <returns></returns>
        public static ValueTuple<string, KeyValuePair<string, string>[], string[]> GetQueueContent(ClrHeap heap, ulong addr)
        {
            try
            {
                addr = Utils.RealAddress(addr);
                ClrType clrType = heap.GetObjectType(addr);
                if (!TypeExtractor.Is(TypeExtractor.KnownTypes.Queue, clrType.Name))
                    return ("Instance at: " + Utils.RealAddressString(addr) + " is not " + TypeExtractor.GetKnowTypeName(TypeExtractor.KnownTypes.Queue), null, null);
                string[] fldNames = new[] { "_array", "_head", "_tail", "_size", "_version" };
                string error;
                ValueTuple<ClrType, ClrInstanceField, ClrElementKind, object>[] info;
                (error, info) = GetCollectionInfo(heap, addr, clrType, fldNames);
                if (error != null) return (error, null, null);
                ulong aryAddr = (ulong)(info[0].Item4);
                int _head = (int)(info[1].Item4);
                int _tail = (int)(info[2].Item4);
                int _size = (int)(info[3].Item4);
                ClrType aryClrType;
                ClrType aryComponentType;
                ClrElementKind aryKind;
                int aryLen;
                (error, aryClrType, aryComponentType, aryKind, aryLen) = ArrayInfo(heap, aryAddr);
                if (_size == 0)
                {
                    return (null, null, null);
                }

                string[] aryValues = new string[aryLen];
                KeyValuePair<string, string[]> aryData = GetAryValues(heap, aryClrType, aryAddr);
                var aryVals = aryData.Value;

                if (_head < _tail)
                {
                    for (int i = 0; i < _size; ++i)
                        aryValues[i] = aryVals[i];
                    for (int i = _size; i < aryLen; ++i)
                        aryValues[i] = Constants.HeavyBallotXStr + aryVals[i];
                }
                else
                {
                    int hlen = aryLen - _head;
                    for (int i = _size; i < aryLen; ++i)
                    {
                        if ((i >= _head && i < hlen) || (i>=hlen && i < _tail))
                            aryValues[i] = aryVals[i];
                        else
                            aryValues[i] = Constants.HeavyBallotXStr + aryVals[i];
                    }
                }

                return (null, null, null);
            }
            catch(Exception ex)
            {
                var error = Utils.GetExceptionErrorString(ex);
                return (error, null, null);
            }
        }


        #endregion System.Collections.Generic.Queue<T>

        #region array values

        public static KeyValuePair<string,string[]> GetAryValues(ClrHeap heap, ClrType aryClrType, ulong aryAddr)
        {
            try
            {
                return new KeyValuePair<string, string[]>(null, Utils.EmptyArray<string>.Value);
            }
            catch(Exception ex)
            {
                return new KeyValuePair<string, string[]>(Utils.GetExceptionErrorString(ex), null);
            }
            
        }

        public static ValueTuple<string, InstanceValue> ArrayContent(IndexProxy ndxProxy, ClrHeap heap, ulong decoratedAddr, InstanceValue parent)
        {
            var addr = Utils.RealAddress(decoratedAddr);
            (string error, ClrType clrType, ClrType elemType, ClrElementKind elemKind, int len) = ArrayInfo(heap, addr);
            if (error != null)
                return (error, null);
            var typeId = ndxProxy.GetTypeId(clrType.Name);
            var kind = TypeExtractor.GetElementKind(clrType);
            var aryInst = new InstanceValue(typeId, kind, addr, clrType.Name, String.Empty, Utils.RealAddressString(addr), Constants.InvalidIndex, parent);
            var elemTypeId = ndxProxy.GetTypeId(elemType.Name);
            var elemInst = new InstanceValue(elemTypeId, elemKind, Constants.InvalidAddress, elemType.Name, String.Empty, Utils.CountString(len), Constants.InvalidIndex, aryInst);
            aryInst.SetFields(new InstanceValue[] { elemInst });
            List<string> types = new List<string>() { elemType.Name };
            var aryItems = TypeExtractor.IsUnknownStruct(elemKind)
                ? GetAryStructItems(heap, addr, clrType, elemType, elemKind, len, types)
                : GetAryItems(heap, addr, clrType, elemType, elemKind, len, types);
            if (types.Count > 1) // we have alternative types in the array
            {
                if (types.Count > 2)
                {
                    aryInst.AddArrayTypes(types);
                }
                else
                {
                    Debug.Assert(types.Count == 2);
                    var elemTypeName = types[1];
                    elemTypeId = ndxProxy.GetTypeId(elemTypeName);
                    if (elemTypeId >= 0)
                    {
                        elemKind = ndxProxy.GetTypeKind(elemTypeId);
                        elemInst = new InstanceValue(elemTypeId, elemKind, Constants.InvalidAddress, elemTypeName, String.Empty, Utils.CountString(len), Constants.InvalidIndex, aryInst);
                        aryInst.SetFields(new InstanceValue[] { elemInst });
                    }
                }
            }
            aryInst.AddArrayValues(aryItems);
            return (null, aryInst);

        }

        public static ValueTuple<string, ClrType, ClrType, ClrElementKind, int> ArrayInfo(ClrHeap heap, ulong addr)
        {
            var clrType = heap.GetObjectType(addr);
            if (clrType == null)
                return ("Cannot get type at address: " + Utils.RealAddressString(addr), null, null, ClrElementKind.Unknown, 0);
            if (!clrType.IsArray)
                return ("The type at address: " + Utils.RealAddressString(addr) + " is not array.", null, null, ClrElementKind.Unknown, 0);
            var len = clrType.GetArrayLength(addr);
            var kind = TypeExtractor.GetElementKind(clrType.ComponentType);
            return (null, clrType, clrType.ComponentType, kind, len);
        }

        public static string[] GetAryItems(ClrHeap heap, ulong addr, ClrType aryType, ClrType elemType, ClrElementKind elemKind, int aryCnt, List<string> types)
        {
            var ary = new string[aryCnt];
            for (int i = 0; i < aryCnt; ++i)
                ary[i] = GetAryItem(heap, addr, aryType, elemType, elemKind, i, types);
            return ary;
        }

        public static string[] GetAryStructItems(ClrHeap heap, ulong addr, ClrType aryType, ClrType elemType, ClrElementKind elemKind, int aryCnt, List<string> types)
        {
            var ary = new string[aryCnt];
            Debug.Assert(TypeExtractor.IsUnknownStruct(elemKind));
            List<ValueTuple<ClrType, ClrInstanceField, ClrElementKind>> fields = new List<(ClrType, ClrInstanceField, ClrElementKind)>(elemType.Fields.Count);
            ValueTuple<ClrType, ClrInstanceField, ClrElementKind>[] fldInfos = TypeExtractor.GetFieldsAndKinds(elemType);
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
            for (int i = 0; i < aryCnt; ++i)
                ary[i] = GetAryStructItem(heap, addr, aryType, elemType, i, fldInfos, sb);
            StringBuilderCache.Release(sb);
            return ary;
        }
        public static string GetAryStructItem(ClrHeap heap, ulong addr, ClrType aryType, ClrType elemType, int aryNdx, ValueTuple<ClrType, ClrInstanceField, ClrElementKind>[] fldInfos, StringBuilder sb)
        {
            sb.Clear();
            var elemAddr = aryType.GetArrayElementAddress(addr, aryNdx);
            (ClrType fldType, ClrInstanceField fld, ClrElementKind fldKind) = fldInfos[0];
            string val = (string)GetFieldValue(heap, elemAddr, fld, fldType, fldKind, true, false);
            sb.Append(val);
            for (int i = 1, icnt = fldInfos.Length; i < icnt; ++i)
            {
                (fldType, fld, fldKind) = fldInfos[i];
                val = (string)GetFieldValue(heap, elemAddr, fld, fldType, fldKind, true, false);
                sb.Append(Constants.HeavyGreekCrossPadded).Append(val);
            }
            return sb.ToString();
        }


        public static string GetAryItem(ClrHeap heap, ulong addr, ClrType aryType, ClrType elemType, ClrElementKind elemKind, int aryNdx, List<string> types)
        {

            if (elemKind == ClrElementKind.Unknown)
            {
                return Constants.UnknownValue;
            }
            var elemAddr = aryType.GetArrayElementAddress(addr, aryNdx);

            if (TypeExtractor.IsAmbiguousKind(elemKind))
            {
                var ambAddr = aryType.GetArrayElementValue(addr, aryNdx);
                if (ambAddr is ulong)
                {
                    var tempType = heap.GetObjectType((ulong)ambAddr);
                    if (tempType != null)
                    {
                        if (!types.Contains(tempType.Name))
                        {
                            types.Add(tempType.Name);
                        }
                        elemType = tempType;
                        elemKind = TypeExtractor.GetElementKind(elemType);
                    }
                    return Utils.RealAddressString((ulong)ambAddr);
                }
                return Constants.UnknownValue;
            }
            if (TypeExtractor.IsUnknownStruct(elemKind))
            {
                return Utils.RealAddressString(elemAddr);
            }
            var specKind = TypeExtractor.GetSpecialKind(elemKind);
            if (specKind != ClrElementKind.Unknown)
            {
                switch (specKind)
                {
                    case ClrElementKind.Guid:
                        return GuidValueAsString(elemAddr, elemType, false);
                    case ClrElementKind.DateTime:
                        return DateTimeValueAsString(elemAddr, elemType);
                    case ClrElementKind.TimeSpan:
                        return GetTimeSpanValue(heap, elemAddr);
                    case ClrElementKind.Decimal:
                        return DecimalValueAsString(elemAddr, elemType, null);
                    case ClrElementKind.Exception:
                        return Utils.RealAddressString(elemAddr);
                    case ClrElementKind.SystemVoid:
                        return Constants.UnknownValue;
                    case ClrElementKind.Free:
                    case ClrElementKind.Abstract:
                    case ClrElementKind.SystemObject:
                        var elemobj = aryType.GetArrayElementValue(addr, aryNdx);
                        return Utils.RealAddressString((ulong)elemobj);
                    default:
                        return Constants.UnknownValue;
                }
            }
            else
            {
                switch (elemKind)
                {
                    case ClrElementKind.String:
                        ulong faddr;
                        if (heap.ReadPointer(elemAddr, out faddr))
                            return GetStringAtAddress(faddr, heap);
                        else
                            return Constants.NullValueOld;
                    case ClrElementKind.SZArray:
                    case ClrElementKind.Array:
                    case ClrElementKind.Object:
                    case ClrElementKind.Class:
                        var obj = aryType.GetArrayElementValue(addr, aryNdx);
                        return Utils.RealAddressString((ulong)obj);
                    case ClrElementKind.Unknown:
                        return Utils.RealAddressString(elemAddr);
                    default:
                        var elemobj = aryType.GetArrayElementValue(addr, aryNdx);
                        return GetPrimitiveValue(elemobj, elemType);
                }
            }
        }

        #endregion array values

        #endregion known collections

        private static IPAddress GetIPAddress(ClrType clrType, ulong address)
        {
            const int AddressFamilyInterNetworkV6 = 23;
            const int IPv4AddressBytes = 4;
            const int IPv6AddressBytes = 16;
            const int NumberOfLabels = IPv6AddressBytes / 2;

            byte[] bytes;

            var m_FamilyFld = clrType.GetFieldByName("m_Family");
            var m_Family = ValueExtractor.GetEnumValue(address, m_FamilyFld, false);

            if (m_Family == AddressFamilyInterNetworkV6)
            {
                bytes = new byte[IPv6AddressBytes];
                int j = 0;

                var m_NumbersFld = clrType.GetFieldByName("m_Numbers");
                var m_NumbersAddr = (ulong)m_NumbersFld.GetValue(address);
                var m_NumbersType = m_NumbersFld.Type;

                for (int i = 0; i < NumberOfLabels; i++)
                {
                    ushort number = (ushort)m_NumbersType.GetArrayElementValue(m_NumbersAddr, i);
                    bytes[j++] = (byte)((number >> 8) & 0xFF);
                    bytes[j++] = (byte)(number & 0xFF);
                }
            }
            else
            {
                var m_m_AddressFld = clrType.GetFieldByName("m_Address");
                var m_AddressVal = (long)m_m_AddressFld.GetValue(address);

                bytes = new byte[IPv4AddressBytes];
                bytes[0] = (byte)(m_AddressVal);
                bytes[1] = (byte)(m_AddressVal >> 8);
                bytes[2] = (byte)(m_AddressVal >> 16);
                bytes[3] = (byte)(m_AddressVal >> 24);
            }

            return new IPAddress(bytes);
        }


        public static ValueTuple<object,ClrInstanceField> NullableValue(ClrHeap heap, ulong addr, ClrType type)
        {
            if (type == null) return (null,null);
            Debug.Assert(type.Fields.Count == 2);
            ClrInstanceField valueFld = type.Fields[0].Name == "value" ? type.Fields[0] : type.Fields[1];
            return (valueFld.GetValue(addr, true, false),valueFld);
        }

        public static string NullableValueAsString(ClrHeap heap, ulong addr, ClrType type)
        {
            (object val, ClrInstanceField valueFld) = NullableValue(heap, addr, type);
            if (val == null || valueFld == null || valueFld.Type == null) return Constants.UnknownValue;
            if (valueFld.Type.IsPrimitive)
                return PrimitiveValueAsString(val, valueFld.ElementType);
            return Constants.UnknownValue;
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
