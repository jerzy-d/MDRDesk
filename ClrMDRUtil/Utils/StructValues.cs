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
        ClrElementKind[] _kinds;
        public ClrElementKind[] Kinds => _kinds;
        string[] _names;
        public string[] Names => _names;
        string[] _typeNames;
        public string[] TypeNames => _typeNames;
        StructFields[] _structs;
        public StructFields[] Structs => _structs;

        public StructFields(ClrElementKind[] kinds, string[] names, string[] typeNames, StructFields[] structs)
        {
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
            return new StructFields(kinds, names, typeNames, structFields);
        }

        public static void Description(StructFields sf, StringBuilder sb, string indent)
        {
            for (int i = 0, icnt = sf._names.Length; i < icnt; ++i)
            {
                sb.AppendLine();
                sb.Append(indent).Append(sf._names[i]).Append(" : ").Append(sf._typeNames[i]);
                if (sf.Structs != null && sf.Structs[i]!=null && !sf.Structs[i].IsEmpty())
                {
                    sb.AppendLine();
                    Description(sf.Structs[i], sb, indent + "   ");
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

        public static ValueTuple<StructFields, StructFieldsEx> GetStructInfo(ClrHeap heap, ulong addr, out string error)
        {
            error = null;
            try
            {
                ClrType type = heap.GetObjectType(addr);
                if (type == null)
                {
                    error = "StructValues.GetStructInfo" + Constants.HeavyGreekCrossPadded
                   + "Expected structure at address: " + Utils.RealAddressString(addr) + Constants.HeavyGreekCrossPadded
                   + "ClrHeap.GetObjectType return null." + type.Name + Constants.HeavyGreekCrossPadded;
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
