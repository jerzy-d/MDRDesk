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
            if (elKind == ClrElementKind.Unknown || TypeExtractor.IsAmbiguousKind(elKind))
            {
                for (int i = 0; i < len; ++i)
                {

                }
            }
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
                    var vals = ArrayOfReferenceValues(heap, aryType, addr, aryLen);
                    return (null, aryType, elType, null, vals, null);
                }
                if (TypeExtractor.IsEnum(elKind))
                {
                    var vals = ArrayOfEnumValues(heap, aryType, elType, addr, aryLen);
                    return (null, aryType, elType, null, vals, null);
                }
                if (elType.IsPrimitive)
                {
                    var primitives = ArrayOfPrimitiveValues(heap, aryType, addr, aryLen, elKind);
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
            StructFieldsInfo sfi = null;
            StructValueStrings[] values = new StructValueStrings[aryLen];
            for (int i = 0; i < aryLen; ++i)
            {
                ulong elAddr = type.GetArrayElementAddress(addr, i);
                if (sfi == null)
                {
                    sfi = StructFieldsInfo.GetStructFields(elType, heap, elAddr);
                    if (sfi == null) continue;
                }
                values[i] = StructFieldsInfo.GetStructValueStrings(sfi, heap, elAddr);
            }
            return (StructFieldsInfo.GetStructDescription(sfi), values);
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
                var descr = new List<KeyValuePair<string, string>>(8);

                ulong _array = 0;
                int _head = 0, _tail = 0, _size = 0, _version = 0;
                for(int i = 0, icnt = type.Fields.Count; i < icnt; ++i)
                {
                    var fld = type.Fields[i];
                    switch (fld.Name)
                    {
                        case "_head":
                            _head = (int)values[i];
                            descr.Add(new KeyValuePair<string, string>("head", Utils.CountString(_head)));
                            break;
                        case "_tail":
                            _tail = (int)values[i];
                            descr.Add(new KeyValuePair<string, string>("tail", Utils.CountString(_tail)));
                            break;
                        case "_size":
                            _size = (int)values[i];
                            descr.Add(new KeyValuePair<string, string>("size", Utils.CountString(_size)));
                            break;
                        case "_version":
                            _version = (int)values[i];
                            descr.Add(new KeyValuePair<string, string>("version", Utils.CountString(_version)));
                            break;
                        case "_array":
                            _array = (ulong)values[i];
                            break;
                    }
                }

                (string er, ClrType _arrayType, ClrType elType, StructFields structFlds, string[] aryVals, StructValueStrings[] aryStructValues) =
                    GetArrayContentAsStrings(heap, _array);
                descr.Add(new KeyValuePair<string, string>("capacity", Utils.CountString(aryVals.Length)));

                string[] aryValues = new string[_size];
                int aryLen = aryVals.Length;
                int count1 = aryLen - _head < _size ? aryLen - _head : _size;
                Array.Copy(aryVals, _head, aryValues, 0, count1);
                int count2 = _size - count1;
                if (count2 > 0)
                    Array.Copy(aryVals, 0, aryValues, aryLen - _head, count2);
                return (null, descr.ToArray(), aryValues);
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
                {
                    string err = CheckCollection(heap, addr, TypeExtractor.KnownTypes.Stack);
                    if (err != null) return (err, null, null);
                }
                (string error, ClrType type, ClrElementKind kind, (ClrType[] fldTypes, ClrElementKind[] fldKinds, object[] values, StructValues[] structValues)) =
                    ClassValue.GetClassValues(heap, addr);
                if (error != null) return (error, null, null);
                var descr = new List<KeyValuePair<string, string>>(8);
                ulong _array = 0;
                int _size = 0, _version = 0;
                for (int i = 0, icnt = type.Fields.Count; i < icnt; ++i)
                {
                    var fld = type.Fields[i];
                    switch (fld.Name)
                    {
                         case "_size":
                            _size = (int)values[i];
                            descr.Add(new KeyValuePair<string, string>("size", Utils.CountString(_size)));
                            break;
                        case "_version":
                            _version = (int)values[i];
                            descr.Add(new KeyValuePair<string, string>("version", Utils.CountString(_version)));
                            break;
                        case "_array":
                            _array = (ulong)values[i];
                            break;
                    }
                }

                (string er, ClrType _arrayType, ClrType elType, StructFields structFlds, string[] aryVals, StructValueStrings[] aryStructValues) =
                    GetArrayContentAsStrings(heap, _array);
                descr.Add(new KeyValuePair<string, string>("capacity", Utils.CountString(aryVals.Length)));
                descr.Add(new KeyValuePair<string, string>("order", "bottom -> up"));

                string[] aryValues = new string[_size];
                Array.Copy(aryVals, 0, aryValues, 0, _size);
                return (null, descr.ToArray(), aryValues);
            }
            catch (Exception ex)
            {
                var error = Utils.GetExceptionErrorString(ex);
                return (error, null, null);
            }
        }

        #endregion System.Collections.Generic.Stack<T>

        #region System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue>

        public static ValueTuple<string, KeyValuePair<string, string>[], KeyValuePair<string, string>[]> ConcurrentDictionaryContentAsStrings(ClrHeap heap, ulong addr)
        {
            try
            {
                addr = Utils.RealAddress(addr);
                { // check if we have desired type at the given address
                    ClrType clrType = heap.GetObjectType(addr);
                    if (clrType == null)
                        return ("Cannot get type of instance at: " + Utils.RealAddressString(addr) + ", invalid address?", null, null);
                    if (!TypeExtractor.Is(TypeExtractor.KnownTypes.ConcurrentDictionary, clrType.Name))
                        return ("Instance at: " + Utils.RealAddressString(addr) + " is not " + TypeExtractor.GetKnowTypeName(TypeExtractor.KnownTypes.ConcurrentDictionary), null, null);
                    clrType = null;
                }

                (string error, ClrType type, ClrElementKind kind, (ClrType[] fldTypes, ClrElementKind[] fldKinds, object[] values, StructValues[] structValues)) =
                    ClassValue.GetClassValues(heap, addr);
                if (error != null) return (error, null, null);
                int tablesNdx = ClassValue.IndexOfField(type.Fields, "m_tables");
                if (tablesNdx < 0) return (null, null, null); // TODO JRD
                ulong tblAddr = (ulong)values[tablesNdx];
                var fldDescriptionLst = new List<KeyValuePair<string, string>>(8);
                StructFieldsEx sfxKey = null;
                StructFieldsEx sfxValue = null;

                (ClrType keyTypeByName, ClrType valTypeByName) = TypeExtractor.GetKeyValuePairTypesByName(heap, type.Name, "System.Collections.Concurrent.ConcurrentDictionary<");

                (string err, ClrType tblType, ClrElementKind tblKind, (ClrType[] tblFldTypes, ClrElementKind[] tblFldKinds, object[] tblValues, StructValues[] tblStructValues)) =
                    ClassValue.GetClassValues(heap, tblAddr);
                int bucketNdx = ClassValue.IndexOfField(tblType.Fields, "m_buckets");
                if (bucketNdx < 0) return (null, null, null); // TODO JRD
                ulong bucketAddr = (ulong)tblValues[bucketNdx];
                (string er, ClrType bucketType, ClrType bucketElType, StructFields bucketStructs, object[] bucketValues, StructValues[] bucketStructValues) =
                    GetArrayContent(heap, bucketAddr);
                fldDescriptionLst.Add(new KeyValuePair<string, string>("bucket count", Utils.CountString(bucketValues.Length)));

                int cntNdx = ClassValue.IndexOfField(tblType.Fields, "m_countPerLock");
                if (cntNdx < 0) return (null, null, null); // TODO JRD
                ulong cntAddr = (ulong)tblValues[cntNdx];
                (string e, ClrType cntType, ClrType cntElType, StructFields cntStructs, object[] cntValues, StructValues[] cntStructValues) =
                    GetArrayContent(heap, cntAddr);
                int iCount = 0;
                for(int i = 0, icnt = cntValues.Length; i < icnt; ++i)
                {
                    iCount += (int)cntValues[i];
                }
                fldDescriptionLst.Add(new KeyValuePair<string, string>("count", Utils.CountString(iCount)));
                KeyValuePair<string, string>[] result = new KeyValuePair<string, string>[iCount];

                int m_keyNdx=0;
                int m_valueNdx=0;
                int m_nextNdx=-1;
                int resultNdx = 0;
                ClrType nodeType=null;
                ClrElementKind nodeKind;
                ClrType[] nodeFldTypes=null;
                ClrElementKind[] nodeFldKinds=null;
                ClrType aNodeType = null;

                for (int i = 0, icnt = bucketValues.Length; i < icnt; ++i)
                {
                    ulong bAddr = (ulong)bucketValues[i];
                    if (bAddr == Constants.InvalidAddress) continue;
                    if (m_nextNdx == -1)
                    {
                        (error, nodeType, nodeKind, nodeFldTypes, nodeFldKinds) = ClassValue.GetClassTypeInfo(heap, bAddr);
                        m_keyNdx = ClassValue.IndexOfField(nodeType.Fields, "m_key");
                        fldDescriptionLst.Add(new KeyValuePair<string, string>("key type", nodeFldTypes[m_keyNdx].Name));
                        m_valueNdx = ClassValue.IndexOfField(nodeType.Fields, "m_value");
                        fldDescriptionLst.Add(new KeyValuePair<string, string>("value type", nodeFldTypes[m_valueNdx].Name));
                        m_nextNdx = ClassValue.IndexOfField(nodeType.Fields, "m_next");
                    }
                    for (ulong node = (ulong)bucketValues[i]; node != Constants.InvalidAddress;)
                    {
                        string key = Constants.NullValue;
                        string val = Constants.NullValue;



                        ulong keyAddr = TypeExtractor.IsObjectReference(nodeFldKinds[m_keyNdx])
                                            ? (ulong)nodeType.Fields[m_keyNdx].GetValue(node,false,false)
                                            : nodeType.Fields[m_keyNdx].GetAddress(node);
                        if (TypeExtractor.IsStruct(nodeFldKinds[m_keyNdx]))
                        {
                            if (sfxKey == null)
                            {
                                var kAddr = nodeType.Fields[m_keyNdx].GetAddress(node,true);
                                var kType = heap.GetObjectType(kAddr);
                                StructFields sf = StructFields.GetStructFields(nodeFldTypes[m_keyNdx]);
                                sfxKey = StructFieldsEx.GetStructFields(sf, nodeFldTypes[m_keyNdx]);
                                sfxKey.ResetTypes();
                            }
                            var structVal = StructFieldsEx.GetStructValueStrings(sfxKey, heap, keyAddr);
                            key = StructValueStrings.MergeValues(structVal);
                        }
                        else
                        {
                            key = ValueExtractor.GetTypeValueString(heap, keyAddr, nodeFldTypes[m_keyNdx], nodeFldKinds[m_keyNdx]);
                        }
                        ulong valAddr = TypeExtractor.IsObjectReference(nodeFldKinds[m_valueNdx])
                                            ? (ulong)nodeType.Fields[m_valueNdx].GetValue(node, false, false)
                                            : nodeType.Fields[m_valueNdx].GetAddress(node);
                        if (TypeExtractor.IsStruct(nodeFldKinds[m_valueNdx]))
                        {
                            if (sfxValue == null)
                            {
                                StructFields sf = StructFields.GetStructFields(nodeFldTypes[m_valueNdx]);
                                sfxValue = StructFieldsEx.GetStructFields(sf, nodeFldTypes[m_valueNdx]);
                                sfxValue.ResetTypes();
                            }
                            var structVal = StructFieldsEx.GetStructValueStrings(sfxValue, heap, valAddr);
                            val = StructValueStrings.MergeValues(structVal);
                        }
                        else
                        {
                            val = ValueExtractor.GetTypeValueString(heap, valAddr, nodeFldTypes[m_valueNdx], nodeFldKinds[m_valueNdx]);
                        }
                        result[resultNdx++] = new KeyValuePair<string, string>(key, val);
                        node = (ulong)nodeType.Fields[m_nextNdx].GetValue(node,false);
                    }
                }

                if (sfxKey != null)
                    fldDescriptionLst.Add(new KeyValuePair<string, string>(null, GetStructFieldsDescr(sfxKey, "Key Structure:")));
                if (sfxValue != null)
                    fldDescriptionLst.Add(new KeyValuePair<string, string>(null, GetStructFieldsDescr(sfxValue, "Value Structure:")));
                return (null, fldDescriptionLst.ToArray(), result);
            }
            catch (Exception ex)
            {
                return (Utils.GetExceptionErrorString(ex), null, null);
            }
        }

        #endregion System.Collections.Concurrent.ConcurrentDictionary<TKey,TValue>

        #region System.Collections.Generic.HashSet<T>

        // C:\WinDbgStuff\Dumps\TestApp.exe_180107_110845.dmp.map
        // TODO JRD -- check error
        //  0x000173c1b230a8       System.Collections.Generic.HashSet<System.String>

        public static ValueTuple<string, KeyValuePair<string, string>[], string[]> HashSetContentAsStrings(ClrHeap heap, ulong addr)
        {
            try
            {
                addr = Utils.RealAddress(addr);
                {
                    string err = CheckCollection(heap, addr, TypeExtractor.KnownTypes.HashSet);
                    if (err != null) return (err, null, null);
                }
                (string error, ClrType type, ClrElementKind kind, (ClrType[] fldTypes, ClrElementKind[] fldKinds, object[] values, StructValues[] structValues)) =
                    ClassValue.GetClassValues(heap, addr);

                int m_count = GetFieldInt(type.Fields, "m_count", values);
                int m_freeList = GetFieldInt(type.Fields, "m_freeList", values);
                int m_lastIndex = GetFieldInt(type.Fields, "m_lastIndex", values);
                int m_version = GetFieldInt(type.Fields, "m_version", values);
                int slotsFldNdx = GetFieldNdx(type.Fields, "m_slots");

                ClrType slotsType = fldTypes[slotsFldNdx];
                ulong slotsAddr =(ulong)values[slotsFldNdx];
                bool useItemType;
                (ClrType slotType, ClrType itemType, ClrInstanceField itemFld, StructFieldsInfo itemSfi, ClrElementKind itemKind, ClrInstanceField hashCodeFld, ClrInstanceField nextFld) = GetHashSetSlotTypeInfo(heap, slotsAddr, slotsType, out useItemType);
                int aryLen = slotsType.GetArrayLength(slotsAddr);
                string[] hvalues = new string[m_count];
                int copied = 0;
                for (int i = 0; i < m_lastIndex && copied < m_count; ++i)
                {
                    var eaddr = slotsType.GetArrayElementAddress(slotsAddr, i);
                    int hash = GetIntFromField(hashCodeFld, eaddr, true);
                    if (hash < 0) continue;

                    if (itemSfi != null)
                    {
                        var iAddr = itemFld.GetAddress(eaddr, true);
                        StructValueStrings structVal = StructFieldsInfo.GetStructValueStrings(itemSfi, heap, iAddr);
                        hvalues[copied++] = StructValueStrings.MergeValues(structVal);

                    }
                    else if (useItemType)
                    {
                        ulong a = TypeExtractor.IsObjectReference(itemKind)
                            ? (ulong)itemFld.GetValue(eaddr, true)
                            : itemFld.GetAddress(eaddr, true);
                        //if (valEnum != null) valVal = valEnum.GetEnumString(a, valType, TypeExtractor.GetClrElementType(valKind));
                        hvalues[copied++] = ValueExtractor.GetTypeValueAsString(heap, a, itemType, itemKind);
                    }
                    else
                    {
                        //if (valEnum != null) valVal = valEnum.GetEnumString(valObj, TypeExtractor.GetClrElementType(valKind));
                        hvalues[copied++] = (string)ValueExtractor.GetFieldValue(heap, eaddr, itemFld, itemType, itemKind, true, false);
                    }

                    //string val = (string)GetFieldValue(heap, eaddr, slotValueFld, slotValueFld.Type, valueFldTypeKind, true, false);
                    //values[copied++] = val;
                }

                KeyValuePair<string, string>[] fldDescription = new KeyValuePair<string, string>[]
                {
                    new KeyValuePair<string, string>("count", m_count.ToString()),
                    new KeyValuePair<string, string>("array len", aryLen.ToString()),
                    new KeyValuePair<string, string>("version", m_version.ToString())
                };


                return (null, fldDescription, hvalues);
            }
            catch(Exception ex)
            {
                return (Utils.GetExceptionErrorString(ex), null, null);
            }
        }

        private static ValueTuple<ClrType, ClrType, ClrInstanceField, StructFieldsInfo, ClrElementKind, ClrInstanceField, ClrInstanceField> GetHashSetSlotTypeInfo(ClrHeap heap, ulong slotsAddr, ClrType slotsType, out bool useItemType)
        {
            useItemType = false;
            ClrType slotType = slotsType.ComponentType;
            ClrInstanceField hashCodeFld = slotType.GetFieldByName("hashCode");
            ClrInstanceField itemFld = slotType.GetFieldByName("value");
            ClrInstanceField nextFld = slotType.GetFieldByName("next");
            ClrType itemType = itemFld.Type;
            ClrElementKind itemKind = TypeExtractor.GetElementKind(itemType);
            StructFieldsInfo itemSfi = null;
            if (TypeExtractor.IsStruct(itemKind))
            {
                int cnt = slotsType.GetArrayLength(slotsAddr);
                for (int i = 0; i < cnt; ++i)
                {
                    var eaddr = slotsType.GetArrayElementAddress(slotsAddr, i);
                    var iaddr = itemFld.GetAddress(eaddr, true);
                    itemSfi = StructFieldsInfo.GetStructFields(itemType, heap, iaddr);
                    if (itemSfi != null) break;
                }
            }
            else if (TypeExtractor.IsAmbiguousKind(itemKind))
            {
                int cnt = slotsType.GetArrayLength(slotsAddr);
                for (int i = 0; i < cnt; ++i)
                {
                    var eaddr = slotsType.GetArrayElementAddress(slotsAddr, i);
                    var fobj = itemFld.GetValue(eaddr, true, false);
                    if (fobj != null && (fobj is ulong))
                    {
                        var t = heap.GetObjectType((ulong)fobj);
                        if (t != null)
                        {
                            itemType = t;
                            var k = TypeExtractor.GetElementKind(itemType);
                            if (k != itemKind) useItemType = true;
                            itemKind = k;
                            break;
                        }
                    }

                }
            }

            return (slotType, itemType, itemFld, itemSfi, itemKind, hashCodeFld, nextFld);
        }


        #endregion System.Collections.Generic.HashSet<T>

        #region System.Collections.Generic.Dictionary<TKey,TValue>

        public static ValueTuple<string, KeyValuePair<string, string>[], KeyValuePair<string, string>[]> DictionaryContentAsStrings(ClrHeap heap, ulong addr)
        {
            try
            {
                addr = Utils.RealAddress(addr);
                {
                    string err = CheckCollection(heap, addr, TypeExtractor.KnownTypes.Dictionary);
                    if (err != null) return (err, null, null);
                }

                (string error, ClrType type, ClrElementKind kind, (ClrType[] fldTypes, ClrElementKind[] fldKinds, object[] values, StructValues[] structValues)) =
                    ClassValue.GetClassValues(heap, addr);
                if (error != null) return (error, null, null);

                int count = GetFieldInt(type, "count", values);
                int version = GetFieldInt(type, "version", values);
                int freeCount = GetFieldInt(type, "freeCount", values);
                (ulong entriesAddr, ClrType entriesType) = GetFieldUInt64AndType(type, "entries", fldTypes, values);

                if (entriesType == null || entriesAddr == Constants.InvalidAddress || (count - freeCount) < 1)
                    return (TypeExtractor.GetKnowTypeName(TypeExtractor.KnownTypes.Dictionary) + " is empty.", null, null);

                (ClrType aryElemType, ClrElementKind aryElemKind, int aryLen) = ArrayInfo(heap, entriesType, entriesAddr);

                (ClrType hashType, ClrInstanceField hashFld, ClrElementKind hashKind) = TypeExtractor.GetTypeFieldAndKind(aryElemType, "hashCode");
                (ClrType nextType, ClrInstanceField nextFld, ClrElementKind nextKind) = TypeExtractor.GetTypeFieldAndKind(aryElemType, "next");
                (ClrType keyType, ClrInstanceField keyFld, ClrElementKind keyKind) = TypeExtractor.GetTypeFieldAndKind(aryElemType, "key");
                (ClrType valType, ClrInstanceField valFld, ClrElementKind valKind) = TypeExtractor.GetTypeFieldAndKind(aryElemType, "value");
                EnumValues keyEnum = null;
                EnumValues valEnum = null;
                if (keyType.IsEnum) keyEnum = new EnumValues(keyType);
                if (valType.IsEnum) valEnum = new EnumValues(valType);

                StructFieldsInfo keySfi = null, valSfi = null;

                bool useKeyTypeToGetValue = false;
                bool useValTypeToGetValue = false;
                var valList = new List<KeyValuePair<string, string>>(count - freeCount);
                for (int i = 0; i < aryLen; ++i)
                {
                    var keyVal = Constants.UnknownValue;
                    var valVal = Constants.UnknownValue;
                    var eaddr = entriesType.GetArrayElementAddress(entriesAddr, i);
                    var hash = ValueExtractor.GetFieldIntValue(heap, eaddr, hashFld, true);
                    if (hash <= 0) continue;

                    if (TypeExtractor.IsStruct(keyKind))
                    {
                        var kAddr = keyFld.GetAddress(eaddr, true);
                        if (keySfi == null)
                        {
                            keySfi = StructFieldsInfo.GetStructFields(keyType, heap, kAddr);
                        }
                        StructValueStrings structVal = StructFieldsInfo.GetStructValueStrings(keySfi, heap, kAddr);
                        keyVal = StructValueStrings.MergeValues(structVal);
                    }
                    else
                    {
                        object keyObj = keyFld.GetValue(eaddr, keyType.HasSimpleValue, false);
                        if (keyObj != null && TypeExtractor.IsAmbiguousKind(keyKind))
                        {
                            var t = heap.GetObjectType((ulong)keyObj);
                            if (t != null)
                            {
                                var k = TypeExtractor.GetElementKind(t);
                                keyType = t;
                                keyKind = k;
                                if (keyType.IsEnum)
                                {
                                    keyEnum = new EnumValues(keyType);
                                }
                                useKeyTypeToGetValue = true;
                            }
                        }
                        if (useKeyTypeToGetValue)
                        {
                            ulong a = TypeExtractor.IsObjectReference(keyKind)
                                        ? (ulong)keyFld.GetValue(eaddr, true)
                                        : keyFld.GetAddress(eaddr, true);
                            if (keyEnum != null) keyVal = keyEnum.GetEnumString(a, keyType, TypeExtractor.GetClrElementType(keyKind));
                            else keyVal = ValueExtractor.GetTypeValueAsString(heap, a, keyType, keyKind);
                        }
                        else
                        {
                            if (keyEnum != null) keyVal = keyEnum.GetEnumString(keyObj, TypeExtractor.GetClrElementType(keyKind));
                            else keyVal = (string)ValueExtractor.GetFieldValue(heap, eaddr, keyFld, keyType, keyKind, true, false);
                        }
                    }

                    if (TypeExtractor.IsStruct(valKind))
                    {
                        var vAddr = valFld.GetAddress(eaddr, true);
                        if (valSfi == null)
                        {
                            valSfi = StructFieldsInfo.GetStructFields(valType, heap, vAddr);
                        }
                        StructValueStrings structVal = StructFieldsInfo.GetStructValueStrings(valSfi, heap, vAddr);
                        valVal = StructValueStrings.MergeValues(structVal);
                    }
                    else
                    {
                        object valObj = valFld.GetValue(eaddr, valType.HasSimpleValue, false);
                        if (valObj != null && TypeExtractor.IsAmbiguousKind(valKind))
                        {
                            var t = heap.GetObjectType((ulong)valObj);
                            if (t != null)
                            {
                                var k = TypeExtractor.GetElementKind(t);
                                valType = t;
                                if (valKind != k) useValTypeToGetValue = true;
                                valKind = k;
                                if (valType.IsEnum)
                                {
                                    valEnum = new EnumValues(valType);
                                }
                                useValTypeToGetValue = true;
                            }
                        }

                        if (useValTypeToGetValue)
                        {
                            ulong a = TypeExtractor.IsObjectReference(valKind)
                                ? (ulong)valFld.GetValue(eaddr, true)
                                : valFld.GetAddress(eaddr, true);
                            if (valEnum != null) valVal = valEnum.GetEnumString(a, valType, TypeExtractor.GetClrElementType(valKind));
                            else valVal = ValueExtractor.GetTypeValueAsString(heap, a, valType, valKind);
                        }
                        else
                        {
                            if (valEnum != null) valVal = valEnum.GetEnumString(valObj, TypeExtractor.GetClrElementType(valKind));
                            else valVal = (string)ValueExtractor.GetFieldValue(heap, eaddr, valFld, valType, valKind, true, false);
                        }
                    }

                    valList.Add(new KeyValuePair<string, string>(keyVal, valVal));
                }

                var keyTypeName = keyType.Name;
                if (keySfi != null)
                {
                    var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
                    StructFieldsInfo.Description(keySfi, sb, "   ");
                    keyTypeName = StringBuilderCache.GetStringAndRelease(sb);
                }
                var valTypeName = valType.Name;
                if (valSfi != null)
                {
                    var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
                    StructFieldsInfo.Description(valSfi, sb, "   ");
                    valTypeName = StringBuilderCache.GetStringAndRelease(sb);
                }

                KeyValuePair<string, string>[] fldDescription = new KeyValuePair<string, string>[]
                {
                    new KeyValuePair<string, string>("count", Utils.CountString(count-freeCount)),
                    new KeyValuePair<string, string>("array len", Utils.CountString(aryLen)),
                    new KeyValuePair<string, string>("version", version.ToString()),
                    new KeyValuePair<string, string>("key type", keyTypeName),
                    new KeyValuePair<string, string>("value type", valTypeName),
                };

                
                return (error, fldDescription, valList.ToArray());
            }
            catch (Exception ex)
            {
                return (Utils.GetExceptionErrorString(ex), null, null);
            }
        }

        #endregion System.Collections.Generic.Dictionary<TKey,TValue>

        #region System.Collections.Generic.SortedDictionary<TKey, TValue>

        public static ValueTuple<string, KeyValuePair<string, string>[], KeyValuePair<string, string>[]> GetSortedDictionaryContentAsStrings(ClrHeap heap, ulong addr)
        {
            try
            {
                string error;
                addr = Utils.RealAddress(addr);
                {
                    error = CheckCollection(heap, addr, TypeExtractor.KnownTypes.SortedDictionary);
                    if (error != null) return (error, null, null);
                }
                ClrType dctType;
                ClrElementKind dctKind, setKind;
                ClrType[] dctFldTypes, setFldTypes;
                ClrElementKind[] dctFldKinds, setFldKinds;
                object[] dctVals, setVals;
                StructValues[] dctStructVals, setStructVals;
                (error, dctType, dctKind, (dctFldTypes, dctFldKinds, dctVals, dctStructVals)) =
                    ClassValue.GetClassValues(heap, addr);
                (ulong setAddr, ClrType setType) = GetFieldUInt64AndType(dctType,"_set", dctFldTypes, dctVals);

                (error, setType, setKind, (setFldTypes, setFldKinds, setVals, setStructVals)) =
                    ClassValue.GetClassValues(heap, setAddr);


                int count = GetFieldInt(setType.Fields, "count", setVals);
                int version = GetFieldInt(setType.Fields, "version", setVals);
                KeyValuePair<string, string>[] fldDescription = new KeyValuePair<string, string>[]
                {
                    new KeyValuePair<string, string>("count", (count).ToString()),
                    new KeyValuePair<string, string>("version", version.ToString())
                };

                (ulong rootAddr, ClrType rootType) = GetFieldUInt64AndType(setType, "root", setFldTypes, setVals);
                ClrElementKind rootKind;
                ClrType[] rootFldTypes;
                ClrElementKind[] rootFldKinds;
                object[] rootVals;
                StructValues[] rootStructVals;
                (error, rootType, rootKind, (rootFldTypes, rootFldKinds, rootVals, rootStructVals)) =
                    ClassValue.GetClassValues(heap, rootAddr);



                var dctValues = new List<KeyValuePair<string, string>>(count);

                /*
                var setFld = dctType.GetFieldByName("_set"); // get TreeSet 
                var setFldAddr = (ulong)setFld.GetValue(addr, false, false);
                var setType = heap.GetObjectType(setFldAddr);
                //var count = GetFieldIntValue(heap, setFldAddr, setType, "count");
                //var version = GetFieldIntValue(heap, setFldAddr, setType, "version");
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

                //KeyValuePair<string, string>[] fldDescription = new KeyValuePair<string, string>[]
                //{
                //new KeyValuePair<string, string>("count", (count).ToString()),
                //new KeyValuePair<string, string>("version", version.ToString())
                //};

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

                //var values = new List<KeyValuePair<string, string>>(count);

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
                */
                return (null, fldDescription, dctValues.ToArray());
            }
            catch (Exception ex)
            {
                string error = Utils.GetExceptionErrorString(ex);
                return (error, null, null);
            }
        }

        #endregion System.Collections.Generic.SortedDictionary<TKey, TValue>

        #region System.Collections.Generic.SortedSet<T>

        public static ValueTuple<string, KeyValuePair<string, string>[], string[]> SortedSetContentAsStrings(ClrHeap heap, ulong addr)
        {
            try
            {
                addr = Utils.RealAddress(addr);
                {
                    string err = CheckCollection(heap, addr, TypeExtractor.KnownTypes.SortedSet);
                    if (err != null) return (err, null, null);
                }
                (string error, ClrType type, ClrElementKind kind, (ClrType[] fldTypes, ClrElementKind[] fldKinds, object[] values, StructValues[] structValues)) =
                    ClassValue.GetClassValues(heap, addr);

                ulong root = Constants.InvalidAddress;
                int count = 0, version = 0;
                for (int i = 0, icnt = type.Fields.Count; i < icnt; ++i)
                {
                    var fld = type.Fields[i];
                    switch (fld.Name)
                    {
                        case "root":
                            root = (ulong)values[i];
                            break;
                        case "count":
                            count = (int)values[i];
                            break;
                        case "version":
                            version = (int)values[i];
                            break;
                    }
                }
                string[] resultAry = null;
                if (root != Constants.InvalidAddress || count > 0)
                {
                    (string er, ClrType nodeType, ClrElementKind nodeKind, (ClrType[] nodeFldTypes, ClrElementKind[] nodeFldKinds, object[] nodeValues, StructValues[] nodeStructValues)) =
                        ClassValue.GetClassValues(heap, root);

                    ClrInstanceField item = null;
                    ClrInstanceField left = null;
                    ClrInstanceField right = null;
                    ClrElementKind itemKind = ClrElementKind.Unknown;
                    ClrType itemType = null;

                    for (int i = 0, icnt = nodeType.Fields.Count; i < icnt; ++i)
                    {
                        var fld = nodeType.Fields[i];
                        switch (fld.Name)
                        {
                            case "Item":
                                item = fld;
                                itemKind = nodeFldKinds[i];
                                itemType = nodeFldTypes[i];
                                break;
                            case "Left":
                                left = fld;
                                break;
                            case "Right":
                                right = fld;
                                break;
                        }
                    }

                    List<object> items = new List<object>(count);
                    InOrderWalk(root, left, right, item, count, items);
                    Debug.Assert(items.Count == count);
                    resultAry = new string[items.Count];
                    if (TypeExtractor.IsString(itemKind))
                    {
                        for (int i = 0, icnt = items.Count; i < icnt; ++i)
                        {
                            resultAry[i] = (string)itemType.GetValue((ulong)items[i]);
                        }
                    }
                    else
                    {
                        for (int i = 0, icnt = items.Count; i < icnt; ++i)
                        {
                            resultAry[i] = ValueExtractor.ValueToString(items[i], itemKind);
                        }
                    }
                }

                var descr = new List<KeyValuePair<string, string>>(8);
                descr.Add(new KeyValuePair<string, string>("count", Utils.CountString(count)));
                descr.Add(new KeyValuePair<string, string>("version", Utils.CountString(version)));

                return (null, descr.ToArray(), resultAry);
            }
            catch (Exception ex)
            {
                var error = Utils.GetExceptionErrorString(ex);
                return (error, null, null);
            }
        }

        static void InOrderWalk(ulong addr, ClrInstanceField left, ClrInstanceField right, ClrInstanceField item, int count, List<object> items)
        {
            var stack = new Stack<ulong>((int)Math.Log(count, 2));
            while (addr != Constants.InvalidAddress)
            {
                stack.Push(addr);
                addr = GetAddressFromField(left, addr);
            }
            while (stack.Count > 0)
            {
                addr = stack.Pop();
                items.Add(item.GetValue(addr, false, false));

                addr = GetAddressFromField(right, addr);
                while (addr != Constants.InvalidAddress)
                {
                    stack.Push(addr);
                    addr = GetAddressFromField(left, addr);
                }
            }
        }

        #endregion System.Collections.Generic.SortedSet<T>

        #region System.Text.StringBuilder

        public static ValueTuple<string, KeyValuePair<string, string>[], string> StringBuilderContent(ClrHeap heap, ulong addr)
        {
            try
            {
                addr = Utils.RealAddress(addr);
                {
                    string err = CheckCollection(heap, addr, TypeExtractor.KnownTypes.StringBuilder);
                    if (err != null) return (err, null, null);
                }
                (string error, ClrType type, ClrElementKind kind, (ClrType[] fldTypes, ClrElementKind[] fldKinds, object[] values, StructValues[] structValues)) =
                    ClassValue.GetClassValues(heap, addr);

                int chunkLenNdx = 0, chunkNdx=0, prevChunkNdx = 0, chunkCapacityNdx = 0;

                for (int i = 0, icnt = type.Fields.Count; i < icnt; ++i)
                {
                    var fld = type.Fields[i];
                    switch (fld.Name)
                    {
                        case "m_ChunkLength":
                            chunkLenNdx = i;
                            break;
                        case "m_MaxCapacity":
                            chunkCapacityNdx = i;
                            break;
                        case "m_ChunkPrevious":
                            prevChunkNdx = i;
                            break;
                        case "m_ChunkChars":
                            chunkNdx = i;
                            break;
                    }
                }

                List<char[]> chunks = new List<char[]>(8);
                (int totalSize, int chunkCount, int capacity) = GetStringBuilderArrays(addr, type.Fields[chunkNdx], type.Fields[prevChunkNdx], type.Fields[chunkLenNdx], type.Fields[chunkLenNdx], chunks, 0, 0, 0);
                var descr = new List<KeyValuePair<string, string>>(8);
                descr.Add(new KeyValuePair<string, string>("size", Utils.CountString(totalSize)));
                descr.Add(new KeyValuePair<string, string>("capacity", Utils.CountString(capacity)));
                descr.Add(new KeyValuePair<string, string>("chunk count", Utils.CountString(chunkCount)));
                ulong prevChunk = GetAddressFromField(type.Fields[prevChunkNdx], addr);
                descr.Add(new KeyValuePair<string, string>("previous chunk", Utils.RealAddressString(prevChunk)));

                char[] charAry = new char[totalSize];
                int offset = 0;
                for (int i = 0, icnt = chunks.Count; i < icnt; ++i)
                {
                    Array.Copy(chunks[i], 0, charAry, offset, chunks[i].Length);
                    offset += chunks[i].Length;
                }
                return (null, descr.ToArray(), new string(charAry));
            }
            catch (Exception ex)
            {
                var error = Utils.GetExceptionErrorString(ex);
                return (error, null, null);
            }
        }


        static ValueTuple<int,int,int> GetStringBuilderArrays(ulong addr, ClrInstanceField m_ChunkChars, ClrInstanceField m_ChunkPrevious, ClrInstanceField m_ChunkLength, ClrInstanceField m_MaxCapacity, List<char[]> chunks, int size, int chunkCount, int capacity)
        {
            if (addr == Constants.InvalidAddress) return (size,chunkCount,capacity);
            ulong chunkAddr = GetAddressFromField(m_ChunkChars,addr);
            if (chunkAddr == Constants.InvalidAddress) return (size, chunkCount, capacity);
            chunkCount += 1;
            int chunkCapacity = GetIntFromField(m_MaxCapacity, addr);
            capacity += chunkCapacity;
            int usedLength = GetIntFromField(m_ChunkLength, addr);
            if (usedLength <= 0) return (size, chunkCount, capacity);
            size += usedLength;
            char[] data = GetCharArrayWithLenght(chunkAddr, m_ChunkChars.Type, usedLength);
            chunks.Insert(0, data);
            ulong prevChunk = GetAddressFromField(m_ChunkPrevious, addr);
            return GetStringBuilderArrays(prevChunk, m_ChunkChars, m_ChunkPrevious, m_ChunkLength, m_MaxCapacity, chunks, size, chunkCount, capacity);
            throw new ApplicationException("[CollectioContent.GetStringBuilderArrays] Recursive method should not get here.");
        }

        #endregion System.Text.StringBuilder


        #region utils

        static string CheckCollection(ClrHeap heap, ulong addr, TypeExtractor.KnownTypes knownType)
        {
            ClrType clrType = heap.GetObjectType(addr);
            if (clrType == null)
                return "Cannot get type of instance at: " + Utils.RealAddressString(addr) + ", invalid address?";
            if (!TypeExtractor.Is(knownType, clrType.Name))
                return "Instance at: " + Utils.RealAddressString(addr) + " is not " + TypeExtractor.GetKnowTypeName(knownType);
            return null;
        }

        static ulong GetAddressFromField(ClrInstanceField fld, ulong addr)
        {
            object obj = fld.GetValue(addr, false, false);
            if (obj == null) return Constants.InvalidAddress;
            Debug.Assert(obj is ulong);
            return (ulong)obj;
        }

        static int GetIntFromField(ClrInstanceField fld, ulong addr, bool intr = false)
        {
            object obj = fld.GetValue(addr, intr, false);
            if (obj == null) return int.MinValue;
            Debug.Assert(obj is int);
            return (int)obj;
        }

        static char[] GetCharArrayWithLenght(ulong addr, ClrType clrType, int length)
        {
            int aryLen = clrType.GetArrayLength(addr);
            int len = Math.Min(aryLen, length);
            char[] ary = new char[len];
            for (int i = 0; i < len; ++i)
            {
                ary[i] = (char)clrType.GetArrayElementValue(addr, i);
            }
            return ary;
        }

        static string GetStructFieldsDescr(StructFieldsEx sfx, string title)
        {
            StringBuilder sb = new StringBuilder(128);
            sb.Append(title);
            StructFields.Description(sfx.Structs, sb, string.Empty);
            return sb.ToString();
        }

        static int GetFieldInt(ClrType type, string fldName, object[] values)
        {
            int ndx = ClassValue.IndexOfField(type.Fields, fldName);
            Debug.Assert(values[ndx] is int);
            return (int)values[ndx];
        }

        static int GetFieldInt(IList<ClrInstanceField> fields, string fldName, object[] values)
        {
            int ndx = Constants.InvalidIndex;
            for (int i = 0, icnt = fields.Count; i < icnt; ++i)
            {
                if (string.Compare(fldName,fields[i].Name,StringComparison.Ordinal)==0)
                {
                    ndx = i;
                    break;
                }
            }
            if (ndx == Constants.InvalidIndex) return Constants.InvalidIndex;
            Debug.Assert(values[ndx] is int);
            return (int)values[ndx];
        }

        static ulong GetFieldUlong(IList<ClrInstanceField> fields, string fldName, object[] values)
        {
            int ndx = Constants.InvalidIndex;
            for (int i = 0, icnt = fields.Count; i < icnt; ++i)
            {
                if (string.Compare(fldName, fields[i].Name, StringComparison.Ordinal) == 0)
                {
                    ndx = i;
                    break;
                }
            }
            if (ndx == Constants.InvalidIndex) return Constants.InvalidAddress;
            Debug.Assert(values[ndx] is ulong);
            return (ulong)values[ndx];
        }

        static int GetFieldNdx(IList<ClrInstanceField> fields, string fldName)
        {
            int ndx = Constants.InvalidIndex;
            for (int i = 0, icnt = fields.Count; i < icnt; ++i)
            {
                if (string.Compare(fldName, fields[i].Name, StringComparison.Ordinal) == 0)
                {
                    ndx = i;
                    break;
                }
            }
            return ndx;
        }

        public static ValueTuple<ulong,ClrType> GetFieldUInt64AndType(ClrType type, string fldName, ClrType[] fldTypes, object[] values)
        {
            int ndx = ClassValue.IndexOfField(type.Fields, fldName);
            Debug.Assert(values[ndx] is ulong);
            return ((ulong)values[ndx],fldTypes[ndx]);
        }

        #endregion utils
    }
}
