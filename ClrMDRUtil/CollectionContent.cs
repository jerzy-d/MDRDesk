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

        public static ValueTuple<string, ClrType, ClrType, StructFields, string[], StructValueStrings[]> GetArrayContentAsStrings(ClrHeap heap, ulong addr)
        {
            try
            {
                addr = Utils.RealAddress(addr); // we might get decorated one
                ClrType aryType = heap.GetObjectType(addr);
                if (aryType == null)
                    return ("Cannot get array object type. Invalid " + Utils.RealAddressString(addr) + " address?", null, null, null, null, null);
                if (!aryType.IsArray)
                    return ("The object at " + Utils.RealAddressString(addr) + " address is not an array.", null, null, null, null, null);
                (ClrType elType, ClrElementKind elKind, int aryLen) = ArrayInfo(heap, aryType, addr);
                if (aryLen == 0)
                    return (null, aryType, elType, null, Utils.EmptyArray<string>.Value, null);

                if (TypeExtractor.IsString(elKind))
                {
                    var vals = ArrayOfStringValues(heap, aryType, elType, addr, aryLen);
                    return (null, aryType, elType, null, vals, null);
                }
                if (TypeExtractor.IsObjectReference(elKind))
                {
                    var vals = ArrayOfReferenceValueStrings(heap, aryType, addr, aryLen);
                    return (null, aryType, elType, null, vals, null);
                }
                if (TypeExtractor.IsEnum(elKind))
                {
                    var vals = ArrayOfEnumValues(heap, aryType, elType, addr, aryLen);
                    return (null, aryType, elType, null, vals, null);
                }
                if (elType.IsPrimitive)
                {
                    var primitives = ArrayOfPrimitiveValueStrings(heap, aryType, addr, aryLen, elKind);
                    return (null, aryType, elType, null, primitives, null);
                }
                if (TypeExtractor.IsKnownStruct(elKind))
                {
                    string[] values = new string[aryLen];
                    values = ArrayOfKnownTypeValueStrings(heap, aryType, addr, aryLen, elType, elKind);
                    return (null, aryType, elType, null, values, null);
                }
                if (TypeExtractor.IsStruct(elKind))
                {
                    (StructFields fs, StructValueStrings[] values) = ArrayOfStructValueStrings(heap, aryType, elType, addr, aryLen);
                    return (null, aryType, elType, fs, null, values);
                }

                return ("Array Value" + Constants.HeavyGreekCrossPadded + "Getting array value failed." + Constants.HeavyGreekCrossPadded + "Don't know how to get this value" + Constants.HeavyGreekCrossPadded + aryType.Name,
                    aryType, elType, null, Utils.EmptyArray<string>.Value, null);
            }
            catch (Exception ex)
            {
                return (Utils.GetExceptionErrorString(ex), null, null, null, null, null);
            }

        }

        public static ValueTuple<string, ClrType, ClrType, StructFields, object[], StructValues[]> GetArrayContent(ClrHeap heap, ulong addr)
        {
            try
            {
                addr = Utils.RealAddress(addr); // we might get decorated one
                ClrType aryType = heap.GetObjectType(addr);
                if (aryType == null)
                    return ("Cannot get array object type. Invalid " + Utils.RealAddressString(addr) + " address?", null, null, null, null, null);
                if (!aryType.IsArray)
                    return ("The object at " + Utils.RealAddressString(addr) + " address is not an array.", null, null, null, null, null);
                (ClrType elType, ClrElementKind elKind, int aryLen) = ArrayInfo(heap, aryType, addr);
                if (aryLen == 0)
                    return (null, aryType, elType, null, Utils.EmptyArray<string>.Value, null);

                if (TypeExtractor.IsString(elKind))
                {
                    var vals = ArrayOfStringValues(heap, aryType, elType, addr, aryLen);
                    return (null, aryType, elType, null, vals, null);
                }
                if (TypeExtractor.IsObjectReference(elKind))
                {
                    var vals = ArrayOfReferenceValueStrings(heap, aryType, addr, aryLen);
                    return (null, aryType, elType, null, vals, null);
                }
                if (TypeExtractor.IsEnum(elKind))
                {
                    var vals = ArrayOfEnumValues(heap, aryType, elType, addr, aryLen);
                    return (null, aryType, elType, null, vals, null);
                }
                if (elType.IsPrimitive)
                {
                    var primitives = ArrayOfPrimitiveValueStrings(heap, aryType, addr, aryLen, elKind);
                    return (null, aryType, elType, null, primitives, null);
                }
                if (TypeExtractor.IsKnownStruct(elKind))
                {
                    string[] values = new string[aryLen];
                    values = ArrayOfKnownTypeValueStrings(heap, aryType, addr, aryLen, elType, elKind);
                    return (null, aryType, elType, null, values, null);
                }
                if (TypeExtractor.IsStruct(elKind))
                {
                    //(StructFields fs, StructValueStrings[] values) = ArrayOfStructValueStrings(heap, aryType, elType, addr, aryLen);
                    //return (null, aryType, elType, fs, null, values);
                }

                return ("Array Value" + Constants.HeavyGreekCrossPadded + "Getting array value failed." + Constants.HeavyGreekCrossPadded + "Don't know how to get this value" + Constants.HeavyGreekCrossPadded + aryType.Name,
                    aryType, elType, null, Utils.EmptyArray<string>.Value, null);
            }
            catch (Exception ex)
            {
                return (Utils.GetExceptionErrorString(ex), null, null, null, null, null);
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

        public static string[] ArrayOfReferenceValueStrings(ClrHeap heap, ClrType type, ulong addr, int aryLen)
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

        public static object[] ArrayOfReferenceValues(ClrHeap heap, ClrType type, ulong addr, int aryLen)
        {
            var values = new object[aryLen];
            for (int i = 0; i < aryLen; ++i)
            {
                values[i] = type.GetArrayElementValue(addr, i);
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

        public static string[] ArrayOfKnownTypeValueStrings(ClrHeap heap, ClrType type, ulong addr, int aryLen, ClrType elType, ClrElementKind elKind)
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

        public static object[] ArrayOfKnownTypeValues(ClrHeap heap, ClrType type, ulong addr, int aryLen, ClrType elType, ClrElementKind elKind)
        {
            var values = new object[aryLen];

            for (int i = 0; i < aryLen; ++i)
            {
                ulong elAddr = type.GetArrayElementAddress(addr, i);
                switch (TypeExtractor.GetSpecialKind(elKind))
                {
                    case ClrElementKind.DateTime:
                        values[i] = ValueExtractor.DateTimeValue(elAddr, elType);
                        break;
                    case ClrElementKind.Guid:
                        values[i] = ValueExtractor.GuidValue(elAddr, elType);
                        break;
                    case ClrElementKind.Decimal:
                        values[i] = ValueExtractor.GetDecimalValue(elAddr, elType);
                        break;
                    case ClrElementKind.TimeSpan:
                        values[i] = ValueExtractor.TimeSpanValue(elAddr, elType);
                        break;
                }
            }
            return values;
        }

        public static string[] ArrayOfPrimitiveValueStrings(ClrHeap heap, ClrType type, ulong addr, int aryLen, ClrElementKind elKind)
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

        public static object[] ArrayOfPrimitiveValues(ClrHeap heap, ClrType type, ulong addr, int aryLen, ClrElementKind elKind)
        {
            var values = new object[aryLen];
            ClrElementType elType = TypeExtractor.GetClrElementType(elKind);
            for (int i = 0; i < aryLen; ++i)
            {
                values[i] = type.GetArrayElementValue(addr, i);
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

        public static string[] ArrayOfExceptionValueStrings(ClrHeap heap, ClrType type, ClrType elType, ulong addr, int aryLen)
        {
            string[] values = new string[aryLen];
            for (int i = 0; i < aryLen; ++i)
            {
                ulong elAddr = type.GetArrayElementAddress(addr, i);
                values[i] = ValueExtractor.GetExceptionValue(elAddr, elType, heap);
            }
            return values;
        }

        public static ValueTuple<StructFields,StructValueStrings[]> ArrayOfStructValueStrings(ClrHeap heap, ClrType type, ClrType elType, ulong addr, int aryLen)
        {
            StructFields sf = StructFields.GetStructFields(elType);
            StructFieldsEx sfx = StructFieldsEx.GetStructFields(sf, elType);
            sfx.ResetTypes();
            StructValueStrings[] values = new StructValueStrings[aryLen];
            for (int i = 0; i < aryLen; ++i)
            {
                ulong elAddr = type.GetArrayElementAddress(addr, i);
                values[i] = StructFieldsEx.GetArrayElementStructStrings(sfx, heap, elAddr);
            }
            return (sf,values);
        }

        #endregion array

        #region System.Collections.Generic.Queue<T>
        /// <summary>
        /// TODO JRD
        /// </summary>
        /// <param name="heap"></param>
        /// <param name="addr"></param>
        /// <returns></returns>
        public static ValueTuple<string, KeyValuePair<string, string>[], string[]> QueueContentAsStrings(ClrHeap heap, ulong addr)
        {
            try
            {
                addr = Utils.RealAddress(addr);
                ClrType clrType = heap.GetObjectType(addr);
                if (!TypeExtractor.Is(TypeExtractor.KnownTypes.Queue, clrType.Name))
                    return ("Instance at: " + Utils.RealAddressString(addr) + " is not " + TypeExtractor.GetKnowTypeName(TypeExtractor.KnownTypes.Queue), null, null);
                (string error, ClrType type, ClrElementKind kind, (ClrType[] fldTypes, ClrElementKind[] fldKinds, object[] values, StructValues[] structValues)) =
                    ClassValue.GetClassValues(heap, addr);
                if (error != null) return (error, null, null);
                
                ulong _array = 0;
                int _head = 0, _tail = 0, _size = 0, _version = 0;
                for(int i = 0, icnt = type.Fields.Count; i < icnt; ++i)
                {
                    var fld = type.Fields[i];
                    switch (fld.Name)
                    {
                        case "_head":
                            _head = (int)values[i];
                            break;
                        case "_tail":
                            _tail = (int)values[i];
                            break;
                        case "_size":
                            _size = (int)values[i];
                            break;
                        case "_version":
                            _version = (int)values[i];
                            break;
                        case "_array":
                            _array = (ulong)values[i];
                            break;
                    }
                }

                (string er, ClrType _arrayType, ClrType elType, StructFields structFlds, string[] aryVals, StructValueStrings[] aryStructValues) =
                    GetArrayContentAsStrings(heap, _array);

                
                string[] aryValues = new string[_size];
                int aryLen = aryVals.Length;
                int count1 = aryLen - _head < _size ? aryLen - _head : _size;
                Array.Copy(aryVals, _head, aryValues, 0, count1);
                int count2 = _size - count1;
                if (count2 > 0)
                    Array.Copy(aryVals, 0, aryValues, aryLen - _head, count2);
                return (null, null, null);
            }
            catch (Exception ex)
            {
                var error = Utils.GetExceptionErrorString(ex);
                return (error, null, null);
            }
        }


        #endregion System.Collections.Generic.Queue<T>

        #region System.Collections.Generic.Stack<T>

        public static ValueTuple<string, KeyValuePair<string, string>[], string[]> StackContentAsStrings(ClrHeap heap, ulong addr)
        {
            try
            {
                addr = Utils.RealAddress(addr);
                ClrType clrType = heap.GetObjectType(addr);
                if (!TypeExtractor.Is(TypeExtractor.KnownTypes.Stack, clrType.Name))
                    return ("Instance at: " + Utils.RealAddressString(addr) + " is not " + TypeExtractor.GetKnowTypeName(TypeExtractor.KnownTypes.Stack), null, null);
                (string error, ClrType type, ClrElementKind kind, (ClrType[] fldTypes, ClrElementKind[] fldKinds, object[] values, StructValues[] structValues)) =
                    ClassValue.GetClassValues(heap, addr);
                if (error != null) return (error, null, null);

                ulong _array = 0;
                int _size = 0, _version = 0;
                for (int i = 0, icnt = type.Fields.Count; i < icnt; ++i)
                {
                    var fld = type.Fields[i];
                    switch (fld.Name)
                    {
                         case "_size":
                            _size = (int)values[i];
                            break;
                        case "_version":
                            _version = (int)values[i];
                            break;
                        case "_array":
                            _array = (ulong)values[i];
                            break;
                    }
                }

                (string er, ClrType _arrayType, ClrType elType, StructFields structFlds, string[] aryVals, StructValueStrings[] aryStructValues) =
                    GetArrayContentAsStrings(heap, _array);

                string[] aryValues = new string[_size];
                Array.Copy(aryVals, 0, aryValues, 0, _size);
                return (null, null, null);
            }
            catch (Exception ex)
            {
                var error = Utils.GetExceptionErrorString(ex);
                return (error, null, null);
            }
        }

        #endregion System.Collections.Generic.Stack<T>

        #region System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue>

        public static ValueTuple<string, KeyValuePair<string, string>[], string[]> ConcurrentDictionaryContentAsStrings(ClrHeap heap, ulong addr)
        {
            try
            {
                addr = Utils.RealAddress(addr);
                ClrType clrType = heap.GetObjectType(addr);
                if (!TypeExtractor.Is(TypeExtractor.KnownTypes.ConcurrentDictionary, clrType.Name))
                    return ("Instance at: " + Utils.RealAddressString(addr) + " is not " + TypeExtractor.GetKnowTypeName(TypeExtractor.KnownTypes.ConcurrentDictionary), null, null);
                (string error, ClrType type, ClrElementKind kind, (ClrType[] fldTypes, ClrElementKind[] fldKinds, object[] values, StructValues[] structValues)) =
                    ClassValue.GetClassValues(heap, addr);
                if (error != null) return (error, null, null);
                int tablesNdx = ClassValue.IndexOfField(type.Fields, "m_tables");
                if (tablesNdx < 0) return (null, null, null); // TODO JRD
                ulong tblAddr = (ulong)values[tablesNdx];

                (string err, ClrType tblType, ClrElementKind tblKind, (ClrType[] tblFldTypes, ClrElementKind[] tblFldKinds, object[] tblValues, StructValues[] tblStructValues)) =
                    ClassValue.GetClassValues(heap, tblAddr);
                int bucketNdx = ClassValue.IndexOfField(tblType.Fields, "m_buckets");
                if (bucketNdx < 0) return (null, null, null); // TODO JRD
                ulong bucketAddr = (ulong)tblValues[bucketNdx];
                (string er, ClrType bucketType, ClrType bucketElType, StructFields bucketStructs, object[] bucketValues, StructValues[] bucketStructValues) =
                    GetArrayContent(heap, bucketAddr);

                int cntNdx = ClassValue.IndexOfField(tblType.Fields, "m_countPerLock");
                if (cntNdx < 0) return (null, null, null); // TODO JRD
                ulong cntAddr = (ulong)tblValues[cntNdx];
                (string e, ClrType cntType, ClrType cntElType, StructFields cntStructs, object[] cntValues, StructValues[] cntStructValues) =
                    GetArrayContent(heap, cntAddr);


                return (null, null, null);
            }
            catch (Exception ex)
            {
                var error = Utils.GetExceptionErrorString(ex);
                return (error, null, null);
            }
        }

        #endregion System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue>
    }
}
