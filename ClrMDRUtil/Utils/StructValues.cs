using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
    public struct StructValues
    {
        public object[] _values;
        public StructValues[] _structs;

        public StructValues(object[] values, StructValues[] structs)
        {
            _values = values;
            _structs = structs;
        }
    }

    public struct StructValueStrings
    {
        public string[] _values;
        public StructValueStrings[] _structs;

        public StructValueStrings(string[] values, StructValueStrings[] structs)
        {
            _values = values;
            _structs = structs;
        }

        public static string[] GetStrings(StructValueStrings[] vals)
        {
            if (vals == null) return null;
            string[] strvals = new string[vals.Length];
            for(int i = 0, icnt = vals.Length; i < icnt; ++i)
            {
                strvals[i] = StructValueStrings.MergeValues(vals[i]);
            }
            return strvals;
        }

        public static string MergeValues(StructValueStrings val)
        {
            StringBuilder sb = new StringBuilder(128);
            sb.Append(Constants.LeftAngleBracketStr);
            var values = val._values;
            var structs = val._structs;
            if (structs == null)
            {
                sb.Append(values[0]);
                for (int i = 1, icnt = values.Length; i < icnt; ++i)
                {
                    sb.Append(Constants.MediumVerticalBarPadded).Append(values[i]);
                }
                sb.Append(Constants.RightAngleBracketStr);
                return sb.ToString();
            }

            if (values[0] == null)
                sb.Append(MergeValues(structs[0]));
            else
                sb.Append(values[0]);
            for (int i = 1, icnt = values.Length; i < icnt; ++i)
            {
                sb.Append(Constants.MediumVerticalBarPadded);
                if (values[i] == null)
                    sb.Append(MergeValues(structs[i]));
                else
                    sb.Append(values[i]);
            }
            sb.Append(Constants.RightAngleBracketStr);
            return sb.ToString();
        }
    }

    public class StructFields
    {
        private string _typeName;
        public string TypeName => _typeName;
        ClrElementKind[] _kinds;
        public ClrElementKind[] Kinds => _kinds;
        string[] _names;
        public string[] Names => _names;
        string[] _typeNames;
        public string[] TypeNames => _typeNames;
        StructFields[] _structs;
        public StructFields[] Structs => _structs;

        public StructFields(string typeName, ClrElementKind[] kinds, string[] names, string[] typeNames, StructFields[] structs)
        {
            _typeName = typeName;
            _kinds = kinds;
            _names = names;
            _typeNames = typeNames;
            _structs = structs;
        }

        public bool IsEmpty()
        {
            return _kinds == null;
        }

        public bool IsAmbiguousKind(int i)
        {
            Debug.Assert(i >= 0 && i < _kinds.Length);
            return TypeExtractor.IsAmbiguousKind(_kinds[i]);
        }

        public void SetKind(ClrElementKind kind, int i)
        {
            Debug.Assert(i >= 0 && i < _kinds.Length);
            _kinds[i] = kind;
        }

        public static StructFields GetStructFields(ClrType type)
        {
            Debug.Assert(type.IsValueClass);
            var flds = type.Fields.ToArray(); // enumeration seems off when this is not done
            var cnt = flds.Length;
            var kinds = new ClrElementKind[cnt];
            var names = new string[cnt];
            var typeNames = new string[cnt];
            StructFields[] structFields = null;

            for (int i = 0; i < cnt; ++i)
            {
                var fld = flds[i];
                kinds[i] = TypeExtractor.GetElementKind(fld.Type);
                names[i] = fld.Name;
                typeNames[i] = fld.Type == null ? Constants.UnknownName : fld.Type.Name;
                if (TypeExtractor.IsStruct(kinds[i]))
                {
                    if (structFields == null) structFields = new StructFields[cnt];
                    structFields[i] = GetStructFields(fld.Type);
                }
            }
            return new StructFields(type.Name, kinds, names, typeNames, structFields);
        }

        public static void Description(StructFields sf, StringBuilder sb, string indent)
        {
            for (int i = 0, icnt = sf._names.Length; i < icnt; ++i)
            {
                sb.AppendLine();
                sb.Append(indent).Append(sf._names[i]).Append(" : ").Append(sf._typeNames[i]);
                if (sf.Structs != null && sf.Structs[i] != null && !sf.Structs[i].IsEmpty())
                {
                    sb.AppendLine();
                    Description(sf.Structs[i], sb, indent + "   ");
                }
            }
        }
    }


    public class StructFieldsInfo
    {
        ClrType _type;
        ClrType[] _types;
        ClrElementKind[] _typeKinds;
        ClrInstanceField[] _fields;
        ClrElementKind[] _fldKinds;
        StructFieldsInfo[] _structFlds;
        int _totalFldCount;

        public StructFieldsInfo(ClrType type, ClrType[] types, ClrElementKind[] typeKinds, ClrInstanceField[] fields, ClrElementKind[] fldKinds, StructFieldsInfo[] structFlds)
        {
            _type = type;
            _types = types;
            _typeKinds = typeKinds;
            _fields = fields;
            _fldKinds = fldKinds;
            _structFlds = structFlds;
            _totalFldCount = -1;
        }

        private bool IsTotalFldCountSet => _totalFldCount != -1;

        private void SetTotalFldCount()
        {
            _totalFldCount = _fields.Length;
            if (_structFlds != null)
            {
                for (int i = 0, icnt = _structFlds.Length; i < icnt; ++i)
                {
                    if (_structFlds[i] != null)
                    {
                        SetTotalFldCount(_structFlds[i]);
                    }
                }
            }
        }

        private void SetTotalFldCount(StructFieldsInfo sfi)
        {
            Debug.Assert(sfi != null);
            _totalFldCount += sfi._fields.Length;
            if (sfi._structFlds != null)
            {
                for (int i = 0, icnt = sfi._structFlds.Length; i < icnt; ++i)
                {
                    if (sfi._structFlds[i] != null)
                    {
                        SetTotalFldCount(sfi._structFlds[i]);
                    }
                }
            }
        }

        public static StructFieldsInfo GetStructFields(ClrType type, ClrHeap heap, ulong addr)
        {
            Debug.Assert(type.IsValueClass);
            var flds = type.Fields;
            var cnt = flds.Count;
            StructFieldsInfo[] structFields = null;
            var types = new ClrType[cnt];
            var typeKinds = new ClrElementKind[cnt];
            var fields = new ClrInstanceField[cnt];
            var fldKinds = new ClrElementKind[cnt];
            for (int i = 0; i < cnt; ++i)
            {
                var fld = flds[i];
                fields[i] = fld;
                fldKinds[i] = TypeExtractor.GetElementKind(fld.Type);
                types[i] = fld.Type;
                typeKinds[i] = fldKinds[i];
                if (TypeExtractor.IsAmbiguousKind(fldKinds[i]))
                {
                    ClrType fType = null;
                    ClrElementKind fKind = ClrElementKind.Unknown;
                    object obj = fld.GetValue(addr, true, false);
                    if (obj is ulong)
                    {
                        (fType,fKind) = TypeExtractor.GetRealType(heap, (ulong)obj);
                    }
                    if (fType != null)
                    {
                        types[i] = fType;
                        typeKinds[i] = fKind;
                    }
                }

                if (TypeExtractor.IsStruct(fldKinds[i]))
                {
                    if (structFields == null) structFields = new StructFieldsInfo[cnt];
                    var faddr = fld.GetAddress(addr, true);
                    structFields[i] = GetStructFields(types[i], heap, faddr);
                }
            }
            return new StructFieldsInfo(type, types, typeKinds, fields, fldKinds, structFields);
        }

        public static StructValueStrings GetStructValueStrings(StructFieldsInfo sfi, ClrHeap heap, ulong addr)
        {
            if (!sfi.IsTotalFldCountSet) sfi.SetTotalFldCount();
            var values = new string[sfi._fields.Length];
            StructValueStrings[] structs = null;

            for (int i = 0, icnt = sfi._fields.Length; i < icnt; ++i)
            {
                if (sfi._structFlds != null && sfi._structFlds[i] != null)
                {
                    if (structs == null) structs = new StructValueStrings[sfi._fields.Length];
                    var faddr = sfi._fields[i].GetAddress(addr, true);
                    structs[i] = GetStructValueStrings(sfi._structFlds[i], heap, faddr);
                }
                else
                {
                    values[i] = GetValue(sfi._types[i], sfi._typeKinds[i], sfi._fields[i], sfi._fldKinds[i], heap, addr, true);
                }
            }
            return new StructValueStrings(values, structs);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="field"></param>
        /// <param name="kind"></param>
        /// <param name="heap"></param>
        /// <param name="addr"></param>
        /// <returns></returns>
        public static string GetValue(ClrType type, ClrElementKind typeKind, ClrInstanceField field, ClrElementKind fldKind, ClrHeap heap, ulong addr, bool intr)
        {
            Debug.Assert(!TypeExtractor.IsStruct(typeKind) && !TypeExtractor.IsStruct(fldKind));
            if (TypeExtractor.IsAmbiguousKind(fldKind))
            {
                if (TypeExtractor.IsString(typeKind))
                {
                    var faddr = ValueExtractor.ReadUlongAtAddress(addr, heap);
                    return ValueExtractor.GetStringAtAddress(faddr,heap);
                }

                if (TypeExtractor.IsKnownStruct(fldKind))
                {
                    switch (TypeExtractor.GetSpecialKind(fldKind))
                    {
                        case ClrElementKind.Decimal:
                            return ValueExtractor.DecimalValueAsString(addr, type, null);
                        case ClrElementKind.DateTime:
                            return ValueExtractor.DateTimeValueString(addr, type);
                        case ClrElementKind.TimeSpan:
                            return ValueExtractor.TimeSpanValueAsString(addr, type);
                        case ClrElementKind.Guid:
                            return ValueExtractor.GuidValue(addr, field);
                        default:
                            return Constants.DontKnowHowToGetValue;
                    }
                }

                if (TypeExtractor.IsPrimitive(typeKind))
                {
                    return ValueExtractor.PrimitiveValueAsString(addr, type, typeKind);
                }
                if (TypeExtractor.IsObjectReference(typeKind))
                {
                    var obj = field.GetValue(addr, intr, false);
                    if (obj == null || !(obj is ulong)) return Constants.ZeroAddressStr;
                    return Utils.RealAddressString((ulong)obj);
                }
                return Constants.DontKnowHowToGetValue;
            }
            if (TypeExtractor.IsString(fldKind))
            {
                return ValueExtractor.GetStringAtAddress(addr, intr, field);
            }
            if (TypeExtractor.IsKnownStruct(fldKind))
            {
                switch (TypeExtractor.GetSpecialKind(fldKind))
                {
                    case ClrElementKind.Decimal:
                        return ValueExtractor.GetDecimalValue(addr, field, intr);
                    case ClrElementKind.DateTime:
                        return ValueExtractor.GetDateTimeValue(addr, field, intr);
                    case ClrElementKind.TimeSpan:
                        return ValueExtractor.TimeSpanValue(addr, field);
                    case ClrElementKind.Guid:
                        return ValueExtractor.GuidValue(addr, field);
                    default:
                        return Constants.DontKnowHowToGetValue;
                }
            }
            if (TypeExtractor.IsPrimitive(fldKind))
            {
                return ValueExtractor.PrimitiveValue(addr, field, intr);
            }
            if (TypeExtractor.IsObjectReference(fldKind))
            {
                var obj = field.GetValue(addr, intr, false);
                if (obj == null || !(obj is ulong)) return Constants.ZeroAddressStr;
                return Utils.RealAddressString((ulong)obj);
                
            }
            return Constants.DontKnowHowToGetValue;
        }

        public static StructFields GetStructDescription(StructFieldsInfo sfi)
        {
            if (sfi == null) return null;
            int cnt = sfi._fields.Length;
            StructFields[] structFields = null;
            var names = new string[cnt];
            var typeNames = new string[cnt];
            var kinds = new ClrElementKind[cnt];
            for (int i = 0, icnt = sfi._fields.Length; i < icnt; ++i)
            {
                if (sfi._structFlds != null && sfi._structFlds[i] != null)
                {
                    if (structFields == null) structFields = new StructFields[cnt];
                    structFields[i] = GetStructDescription(sfi._structFlds[i]);
                    continue;
                }
                var fld = sfi._fields[i];
                var fldKind = sfi._fldKinds[i];
                var typ = sfi._types[i];
                var typKind = sfi._typeKinds[i];
                names[i] = fld.Name;
                typeNames[i] = typ.Name;
                kinds[i] = typKind;
            }

            return new StructFields(sfi._type.Name, kinds, names, typeNames, structFields);
        }

        public static void Description(StructFieldsInfo sfi, StringBuilder sb, string indent)
        {
            sb.Append(indent).Append(sfi._type.Name);
            indent = indent + "   ";
            for (int i = 0, icnt = sfi._fields.Length; i < icnt; ++i)
            {
                sb.AppendLine();
                sb.Append(indent).Append(sfi._fields[i].Name).Append(" : ").Append(sfi._types[i].Name);
                if (sfi._structFlds != null && sfi._structFlds[i] != null)
                {
                    sb.AppendLine();
                    Description(sfi._structFlds[i], sb, indent + "   ");
                }
            }
        }
    }


    public class StructFieldsEx
    {
        StructFields _structFields;
        public StructFields Structs => _structFields;
        ClrType[] _types;
        ClrInstanceField[] _fields;
        StructFieldsEx[] _ex;

        public StructFieldsEx(StructFields structFields, ClrType[] types, ClrInstanceField[] fields, StructFieldsEx[] ex)
        {
            _structFields = structFields;
            _types = types;
            _fields = fields;
            _ex = ex;
        }

        public static StructFieldsEx GetStructFields(StructFields sf, ClrType type)
        {
            Debug.Assert(type.IsValueClass);
            var flds = type.Fields;
            var cnt = flds.Count;
            StructFieldsEx[] structFields = null;
            var types = new ClrType[cnt];
            var fields = new ClrInstanceField[cnt];
            for (int i = 0; i < cnt; ++i)
            {
                var fld = flds[i];
                var kind = TypeExtractor.GetElementKind(fld.Type);
                if (TypeExtractor.IsStruct(kind))
                {
                    fields[i] = fld;
                    types[i] = fld.Type;
                    if (structFields == null) structFields = new StructFieldsEx[cnt];
                    Debug.Assert(sf.Structs[i] != null);
                    structFields[i] = GetStructFields(sf.Structs[i], fld.Type);
                }
                else
                {
                    fields[i] = fld;
                    types[i] = fld.Type;
                }
            }
            return new StructFieldsEx(sf, types, fields, structFields);
        }

        public static StructFieldsEx GetStructFields(StructFields sf, ClrType type, ClrHeap heap, ulong addr)
        {
            Debug.Assert(type.IsValueClass);
            var flds = type.Fields;
            var cnt = flds.Count;
            StructFieldsEx[] structFields = null;
            var types = new ClrType[cnt];
            var fields = new ClrInstanceField[cnt];
            for (int i = 0; i < cnt; ++i)
            {
                var fld = flds[i];
                var kind = TypeExtractor.GetElementKind(fld.Type);
                ClrType fType = null;
                if (TypeExtractor.IsAmbiguousKind(kind))
                {
                    object obj = fld.GetValue(addr, false, false);
                    if (obj is ulong)
                    {
                        fType = heap.GetObjectType((ulong)obj);
                    }
                }

                fields[i] = fld;
                types[i] = fType ?? fld.Type;

                if (TypeExtractor.IsStruct(kind))
                {
                    if (structFields == null) structFields = new StructFieldsEx[cnt];
                    Debug.Assert(sf.Structs[i] != null);
                    structFields[i] = GetStructFields(sf.Structs[i], fld.Type);
                }
            }
            return new StructFieldsEx(sf, types, fields, structFields);
        }

        public static ValueTuple<StructFields, StructFieldsEx> GetStructInfo(ClrType structType, ClrHeap heap, ulong addr, out string error)
        {
            Debug.Assert(structType.IsValueClass);
            error = null;
            try
            {
                ClrType type = null;
                object val = structType.Fields[0].GetValue(addr,true,false);

                if (val is ulong)
                    type = heap.GetObjectType((ulong)val);

                if (type == null)
                {
                    error = "StructValues.GetStructInfo" + Constants.HeavyGreekCrossPadded
                   + "Expected structure at address: " + Utils.RealAddressString(addr) + Constants.HeavyGreekCrossPadded
                   + "ClrHeap.GetObjectType return null. Field name: " + structType.Fields[0].Name + Constants.HeavyGreekCrossPadded;
                    return (null, null);
                }
                ClrElementKind kind = TypeExtractor.GetElementKind(type);
                if (TypeExtractor.IsStruct(kind))
                {
                    error = "StructValues.GetStructInfo" + Constants.HeavyGreekCrossPadded
                   + "Expected structure at address: " + Utils.RealAddressString(addr) + Constants.HeavyGreekCrossPadded
                   + "Found: " + type.Name + Constants.HeavyGreekCrossPadded;
                    return (null,null);
                }


                StructFields sf = StructFields.GetStructFields(type);
                StructFieldsEx sfx = StructFieldsEx.GetStructFields(sf, type, heap, addr);
                return (sf, sfx);
            }
            catch(Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return (null, null);
            }

        }





        public void ResetTypes()
        {
            for (int i = 0; i < _types.Length; ++i)
            {
                _types[i] = null;
            }
            if (_ex != null)
            {
                for (int i = 0; i < _ex.Length; ++i)
                {
                    if (_ex[i] != null) _ex[i].ResetTypes();
                }
            }
        }
        public static string StructString(StructFieldsEx structFld, ClrHeap heap, ulong addr)
        {
            StructValueStrings val = GetStructValueStrings(structFld, heap, addr);
            return StructValueStrings.MergeValues(val);
        }

        public static StructValueStrings GetArrayElementStructStrings(StructFieldsEx structFld, ClrHeap heap, ulong addr)
        {
            var cnt = structFld._fields.Length;
            var structInfo = structFld._structFields;
            string[] vals = new string[cnt];
            StructValueStrings[] structVals = null;
            for (int i = 0; i < cnt; ++i)
            {
                if (structInfo.IsAmbiguousKind(i))
                {
                    var fobj = structFld._fields[i].GetValue(addr, true);
                    var faddr = (ulong)fobj;
                    var ftype = heap.GetObjectType(faddr);
                    if (ftype != null)
                    {
                        structFld._types[i] = ftype;
                        structInfo.SetKind(TypeExtractor.GetElementKind(ftype),i);
                    }
                }
                var fldType = structFld._types[i];
                var fld = structFld._fields[i];
                var kind = structFld._structFields.Kinds[i];
                Debug.Assert(fld != null);
                if (structFld._ex?[i] != null)
                {
                    if (structVals == null) structVals = new StructValueStrings[cnt];

                    structVals[i] = GetArrayElementStructStrings(structFld._ex[i], heap, fld.GetAddress(addr,true));
                }
                else if (fldType != null)
                {
                    var fobj = fld.GetValue(addr, true);
                    var faddr = (ulong)fobj;
                    vals[i] = ValueExtractor.GetTypeValueString(heap, faddr, fldType, kind);
                }
                else
                {
                    vals[i] = ValueExtractor.GetFieldValueString(heap, addr, true, fld, kind);
                }
            }
            return new StructValueStrings(vals, structVals);
        }


        public static StructValues GetStructValues(StructFieldsEx structFld, ClrHeap heap, ulong addr)
        {
            var cnt = structFld._fields.Length;
            var structInfo = structFld._structFields;
            var vals = new object[cnt];
            StructValues[] structVals = null;
            for (int i = 0; i < cnt; ++i)
            {
                if (structInfo.IsAmbiguousKind(i))
                {
                    var fobj = structFld._fields[i].GetValue(addr, true);
                    var faddr = (ulong)fobj;
                    var ftype = heap.GetObjectType(faddr);
                    if (ftype != null)
                    {
                        structFld._types[i] = ftype;
                        structInfo.SetKind(TypeExtractor.GetElementKind(ftype), i);
                    }
                }
                var fldType = structFld._types[i];
                var fld = structFld._fields[i];
                var kind = structFld._structFields.Kinds[i];
                Debug.Assert(fld != null);
                if (structFld._ex?[i] != null)
                {
                    if (structVals == null) structVals = new StructValues[cnt];

                    structVals[i] = GetStructValues(structFld._ex[i], heap, fld.GetAddress(addr, true));
                }
                else if (fldType != null)
                {
                    var fobj = fld.GetValue(addr, true);
                    var faddr = (ulong)fobj;
                    vals[i] = ValueExtractor.GetTypeValue(heap, faddr, fldType, kind);
                }
                else
                {
                    vals[i] = ValueExtractor.GetFieldValue(heap, addr, true, fld, kind);
                }
            }
            return new StructValues(vals, structVals);
        }

        public static StructValueStrings GetStructValueStrings(StructFieldsEx structFld, ClrHeap heap, ulong addr)
        {
            var cnt = structFld._fields.Length;
            var structInfo = structFld._structFields;
            var vals = new string[cnt];
            StructValueStrings[] structVals = null;
            for (int i = 0; i < cnt; ++i)
            {
                if (structInfo.IsAmbiguousKind(i))
                {
                    var fobj = structFld._fields[i].GetValue(addr, true);
                    var faddr = (ulong)fobj;
                    var ftype = heap.GetObjectType(faddr);
                    if (ftype != null)
                    {
                        structFld._types[i] = ftype;
                        structInfo.SetKind(TypeExtractor.GetElementKind(ftype), i);
                    }
                }
                var fldType = structFld._types[i];
                var fld = structFld._fields[i];
                var kind = structFld._structFields.Kinds[i];
                Debug.Assert(fld != null);
                if (structFld._ex?[i] != null)
                {
                    if (structVals == null) structVals = new StructValueStrings[cnt];

                    structVals[i] = GetStructValueStrings(structFld._ex[i], heap, fld.GetAddress(addr, true));
                }
                else if (fldType != null)
                {
                    var fobj = fld.GetValue(addr, true);
                    var faddr = (ulong)fobj;
                    vals[i] = ValueExtractor.GetTypeValueString(heap, faddr, fldType, kind);
                }
                else
                {
                    if (TypeExtractor.IsKnownPrimitive(kind))
                    {
                        vals[i] = ValueExtractor.GetFieldValueString(heap, addr, true, fld, kind);
                    }
                    else
                    {
                        var fobj = fld.GetAddress(addr, true);
                        var faddr = (ulong)fobj;
                        vals[i] = ValueExtractor.GetFieldValueString(heap, faddr, true, fld, kind);
                    }
                }
            }
            return new StructValueStrings(vals, structVals);
        }


    }
}
