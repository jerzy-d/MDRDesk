using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
    public class CollectionContent
    {
        #region array

        public static ValueTuple<ClrType,ClrElementKind,int> ArrayInfo(ClrHeap heap, ClrType type, ulong addr)
        {
            ClrType elType = type.ComponentType;
            ClrElementKind elKind = TypeExtractor.GetElementKind(elType);
            int len = type.GetArrayLength(addr);
            return (elType, elKind, len);
        }

        public static ValueTuple<string, ClrType, ClrType, ClrElementKind, string[], string[]> GetArrayContent(ClrHeap heap, ulong addr)
        {
            try
            {
                addr = Utils.RealAddress(addr); // we might get decorated one
                ClrType aryType = heap.GetObjectType(addr);
                if (aryType == null)
                    return ("Cannot get array object type. Invalid " + Utils.RealAddressString(addr) + " address?", null, null, ClrElementKind.Unknown, null, null);
                if (!aryType.IsArray)
                    return ("The object at " + Utils.RealAddressString(addr) + " address is not an array.", null, null, ClrElementKind.Unknown, null, null);
                (ClrType elType, ClrElementKind elKind, int aryLen) = ArrayInfo(heap, aryType, addr);
                if (aryLen == 0)
                    return (null, aryType, elType, elKind, Utils.EmptyArray<string>.Value, null);

                if (TypeExtractor.IsString(elKind))
                {
                    var vals = ArrayOfStringValues(heap, aryType, elType, addr, aryLen);
                    return (null, aryType, elType, elKind, vals, null);
                }
                if (TypeExtractor.IsObjectReference(elKind))
                {
                    var vals = ArrayOfReferenceValues(heap, aryType, addr, aryLen);
                    return (null, aryType, elType, elKind, vals, null);
                }
                if (TypeExtractor.IsEnum(elKind))
                {
                    var vals = ArrayOfEnumValues(heap, aryType, elType, addr, aryLen);
                    return (null, aryType, elType, elKind, vals, null);
                }
                if (elType.IsPrimitive)
                {
                    var primitives = ArrayOfPrimitiveValues(heap, aryType, addr, aryLen, elKind);
                    return (null, aryType, elType, elKind, primitives, null);
                }
                if (TypeExtractor.IsKnownStruct(elKind))
                {
                    string[] values = new string[aryLen];
                    values = ArrayOfKnownTypeValues(heap, aryType, addr, aryLen, elType, elKind);
                    return (null, aryType, elType, elKind, values, null);
                }
                if (TypeExtractor.IsStruct(elKind))
                {
                    var values = ArrayOfStructValues(heap, aryType, elType, addr, aryLen);
                    return (null, aryType, elType, elKind, values, null);
                }

                return ("Array Value" + Constants.HeavyGreekCrossPadded + "Getting array value failed." + Constants.HeavyGreekCrossPadded + "Don't know how to get this value" + Constants.HeavyGreekCrossPadded + aryType.Name,
                    aryType, elType, elKind, Utils.EmptyArray<string>.Value, null);
            }
            catch (Exception ex)
            {
                return (Utils.GetExceptionErrorString(ex), null, null, ClrElementKind.Unknown, null, null);
            }

        }

        /// <summary>
        /// Get unknown struct value.
        /// </summary>
        public static ValueTuple<string, ClrType, ClrType[], ClrElementKind[], string[][]> GetSelectedFieldsArrayContent(ClrHeap heap, ulong addr, string[] fldnNmes)
        {
            try
            {
                return ("Not implemented yet.",null,null,null,null);
            }
            catch(Exception ex)
            {
                return (Utils.GetExceptionErrorString(ex), null, null, null, null);
            }
        }

        public static string[] ArrayOfReferenceValues(ClrHeap heap, ClrType type, ulong addr, int aryLen)
        {
            string[] values = new string[aryLen];
            for (int i = 0; i < aryLen; ++i)
            {
                object obj = type.GetArrayElementValue(addr, i);
                values[i] = obj == null
                    ? Constants.InvalidAddressStr
                    : Utils.RealAddressString((ulong)type.GetArrayElementValue(addr, i));
            }
            return values;
        }

        public static string[] ArrayOfStringValues(ClrHeap heap, ClrType type, ClrType elType, ulong addr, int aryLen)
        {
            string[] values = new string[aryLen];
            for (int i = 0; i < aryLen; ++i)
            {
                object obj = type.GetArrayElementValue(addr, i);
                values[i] = obj == null ? Constants.NullValue : (string)elType.GetArrayElementValue(addr, i);
            }
            return values;
        }

        public static string[] ArrayOfKnownTypeValues(ClrHeap heap, ClrType type, ulong addr, int aryLen, ClrType elType, ClrElementKind elKind)
        {
            string[] values = new string[aryLen];

            for (int i = 0; i < aryLen; ++i)
            {
                ulong elAddr = type.GetArrayElementAddress(addr, i);
                switch (TypeExtractor.GetSpecialKind(elKind))
                {
                    case ClrElementKind.DateTime:
                        values[i] = ValueExtractor.DateTimeValueAsString(elAddr, elType);
                        break;
                    case ClrElementKind.Guid:
                        values[i] = ValueExtractor.GuidValueAsString(elAddr, elType);
                        break;
                    case ClrElementKind.Decimal:
                        values[i] = ValueExtractor.DecimalValueAsString(elAddr, elType, null);
                        break;
                    case ClrElementKind.TimeSpan:
                        values[i] = ValueExtractor.TimeSpanValueAsString(elAddr, elType);
                        break;
                }
            }
            return values;
        }

        public static string[] ArrayOfPrimitiveValues(ClrHeap heap, ClrType type, ulong addr, int aryLen, ClrElementKind elKind)
        {
            string[] values = new string[aryLen];
            ClrElementType elType = TypeExtractor.GetClrElementType(elKind);
            for (int i = 0; i < aryLen; ++i)
            {
                object obj = type.GetArrayElementValue(addr, i);
                values[i] = ValueExtractor.PrimitiveValueAsString(obj, elType);
            }
            return values;
        }

        /// <summary>
        /// Tries to return enum value and its name.
        /// </summary>
        public static string[] ArrayOfEnumValues(ClrHeap heap, ClrType type, ClrType elType, ulong addr, int aryLen)
        {
            string[] values = new string[aryLen];
            long lVal;
            var enumElem = elType.GetEnumElementType();
            for (int i = 0; i < aryLen; ++i)
            {
                object obj = type.GetArrayElementValue(addr, i);
                values[i] = ValueExtractor.GetEnumAsString(addr, elType, enumElem, obj, out lVal);
            }
            return values;
        }

        public static string[] ArrayOfExceptionValues(ClrHeap heap, ClrType type, ClrType elType, ulong addr, int aryLen)
        {
            string[] values = new string[aryLen];
            long lVal;
            var enumElem = elType.GetEnumElementType();
            for (int i = 0; i < aryLen; ++i)
            {
                object obj = type.GetArrayElementValue(addr, i);
                values[i] = ValueExtractor.GetEnumAsString(addr, elType, enumElem, obj, out lVal);
            }
            return values;
        }

        public static string[] ArrayOfStructValues(ClrHeap heap, ClrType type, ClrType elType, ulong addr, int aryLen)
        {
            string[] values = new string[aryLen];
            ClrInstanceField[] flds = elType.Fields.ToArray();
            ClrType[] fldTypes = null;
            ulong[] fldTypeAddresses = null;
            ClrElementKind[] kinds = new ClrElementKind[flds.Length];
            for (int j = 0, jcnt = flds.Length; j < jcnt; ++j)
            {
                kinds[j] = TypeExtractor.GetElementKind(flds[j].Type);
            }
            StringBuilder sb = new StringBuilder(512);
            string val;
            for (int i = 0; i < aryLen; ++i)
            {
                ulong elAddr = type.GetArrayElementAddress(addr, i);
                sb.Clear();

                for (int j = 0, jcnt = flds.Length; j < jcnt;  ++j)
                {
                    if (TypeExtractor.IsAmbiguousKind(kinds[j]))
                    {
                        var fobj = flds[j].GetValue(elAddr, true);
                        var faddr = (ulong)fobj;
                        var ftype = heap.GetObjectType(faddr);
                        if (ftype != null)
                        {
                            if (fldTypes == null)
                            {
                                fldTypes = new ClrType[flds.Length];
                                fldTypeAddresses = new ulong[flds.Length];
                            }
                            fldTypes[j] = ftype;
                            kinds[j] = TypeExtractor.GetElementKind(ftype);
                        }
                    }
                    if (j > 0) sb.Append(Constants.HeavyGreekCrossPadded);
                    if (fldTypes != null && fldTypes[j] != null)
                    {

                        val = ValueExtractor.GetTypeValue(heap, elAddr, fldTypes[j], kinds[j]);
                        sb.Append(val);
                    }
                    else
                    {
                        val = ValueExtractor.GetFieldValue(heap, elAddr, false, flds[j], kinds[j]);
                        sb.Append(val);
                    }
                }
                values[i] = sb.ToString();
            }

            return values;
        }

        #endregion array
    }
}
