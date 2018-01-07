using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
    public class ClassValue
    {
        public static ValueTuple<string, ClrType, ClrElementKind, ValueTuple<ClrType[],ClrElementKind[], object[], StructValues[]>> 
            GetClassValues(ClrHeap heap, ulong addr)
        {
            try
            {
                addr = Utils.RealAddress(addr);
                var type = heap.GetObjectType(addr);
                if (type == null) return ("Object Value Error" + Constants.HeavyGreekCrossPadded + "Cannot find an instance." + Constants.HeavyGreekCrossPadded + "Heap cannot get object type at address: " + Utils.RealAddressString(addr), null, ClrElementKind.Unknown, (null,null,null,null));
                var kind = TypeExtractor.GetElementKind(type);
                var fldCnt = type.Fields.Count;

                var fldTypes = fldCnt == 0 ? Utils.EmptyArray<ClrType>.Value : new ClrType[fldCnt];
                var fldKinds = fldCnt == 0 ? Utils.EmptyArray<ClrElementKind>.Value : new ClrElementKind[fldCnt];
                var objects = fldCnt == 0 ? Utils.EmptyArray<object>.Value : new object[fldCnt];
                StructValues[] structVals = null;
                for (int i = 0; i < fldCnt; ++i)
                {
                    var fld = type.Fields[i];
                    var fldType = fld.Type; // returns ClrElementKind.Unknown if fld.Type is null
                    var fldKind = TypeExtractor.GetElementKind(fldType);
                    if (fldType == null || TypeExtractor.IsAmbiguousKind(fldKind))
                    {
                        var fldValObj = fld.GetValue(addr, type.IsValueClass, false);
                        if (fldValObj != null && fldValObj is ulong)
                        {
                            var t = heap.GetObjectType((ulong)fldValObj);
                            if (t != null)
                            {
                                fldType = t;
                                fldKind = TypeExtractor.GetElementKind(t);
                            }
                        }
                    }
                    fldTypes[i] = fldType;
                    fldKinds[i] = fldKind;
                    if (fldKind == ClrElementKind.Unknown) continue; // nothing to do here, from MDR lib: There is
                                                                     // a bug in several versions of our debugging layer which causes this.
                    if (TypeExtractor.IsString(fldKind))
                    {
                        objects[i] = fld.GetValue(addr, false, true);
                    }
                    else if (TypeExtractor.IsObjectReference(fldKind))
                    {

                        object obj = fld.GetValue(addr, false, false);
                        if (obj != null && (ulong)obj != Constants.InvalidAddress)
                        {
                            var t = heap.GetObjectType((ulong)obj);
                            if (t!=null)
                            {
                                var k = TypeExtractor.GetElementKind(t);
                                fldTypes[i] = t;
                                fldKinds[i] = k;
                            }
                        }

                        objects[i] = obj;
                    }
                    else if (TypeExtractor.IsEnum(fldKind))
                    {
                        objects[i] = ValueExtractor.GetEnumValueObject(addr, fld,false);
                    }
                    else if (fldType.IsPrimitive)
                    {
                        objects[i] = ValueExtractor.GetPrimitiveValueObject(addr, fld, false);
                    }
                    else if (TypeExtractor.IsKnownStruct(fldKind))
                    {
                        switch (TypeExtractor.GetSpecialKind(fldKind))
                        {
                            case ClrElementKind.DateTime:
                                objects[i] = ValueExtractor.GetDateTime(addr, fld, false);
                                break;
                            case ClrElementKind.Guid:
                                objects[i] = ValueExtractor.GetGuid(addr, fld, false);
                                break;
                            case ClrElementKind.Decimal:
                                objects[i] = ValueExtractor.GetDecimal(addr, fld, false);
                                break;
                            case ClrElementKind.TimeSpan:
                                objects[i] = ValueExtractor.TimeSpanValue(addr, fldType);
                                break;
                        }
                    }
                    else if (TypeExtractor.IsStruct(fldKind))
                    {
                        StructFields sf = StructFields.GetStructFields(fldType);
                        StructFieldsEx sfx = StructFieldsEx.GetStructFields(sf, fldType);
                        sfx.ResetTypes();
                    }
                }



                return (null, type, kind, (fldTypes, fldKinds, objects, structVals));
            }
            catch (Exception ex)
            {
                return (Utils.GetExceptionErrorString(ex), null, ClrElementKind.Unknown, (null,null,null,null));
            }


        }

        public static ValueTuple<string, ClrType, ClrElementKind, ClrType[], ClrElementKind[]>
        GetClassTypeInfo(ClrHeap heap, ulong addr)
        {
            try
            {
                addr = Utils.RealAddress(addr);
                var type = heap.GetObjectType(addr);
                if (type == null) return ("Object Value Error" + Constants.HeavyGreekCrossPadded + "Cannot find an instance." + Constants.HeavyGreekCrossPadded + "Heap cannot get object type at address: " + Utils.RealAddressString(addr), null, ClrElementKind.Unknown, null, null);
                var kind = TypeExtractor.GetElementKind(type);
                var fldCnt = type.Fields.Count;

                var fldTypes = fldCnt == 0 ? Utils.EmptyArray<ClrType>.Value : new ClrType[fldCnt];
                var fldKinds = fldCnt == 0 ? Utils.EmptyArray<ClrElementKind>.Value : new ClrElementKind[fldCnt];
                var strings = fldCnt == 0 ? Utils.EmptyArray<string>.Value : new string[fldCnt];
                for (int i = 0; i < fldCnt; ++i)
                {
                    var fld = type.Fields[i];
                    var fldType = fld.Type; // returns ClrElementKind.Unknown if fld.Type is null
                    fldTypes[i] = fldType;
                    var fldKind = TypeExtractor.GetElementKind(fldType);
                    fldKinds[i] = fldKind;
                    if (fldKind == ClrElementKind.Unknown) continue; // nothing to do here, from MDR lib: There is
                                                                     // a bug in several versions of our debugging layer which causes this.
                    if (TypeExtractor.IsAmbiguousKind(fldKind))
                    {
                        (ClrType aType, ClrElementKind aKind) = TypeExtractor.GetReferenceFieldRealTypeAndKind(heap, addr, fld);
                        if (aType != null)
                        {
                            fldType = aType;
                            fldTypes[i] = fldType;
                            fldKind = aKind;
                            fldKinds[i] = fldKind;
                        }
                    }
                }
                return (null, type, kind, fldTypes, fldKinds);
            }
            catch (Exception ex)
            {
                return (Utils.GetExceptionErrorString(ex), null, ClrElementKind.Unknown, null, null);
            }
        }

        public static ValueTuple<string, ClrType, ClrElementKind, ValueTuple<ClrType[], ClrElementKind[], string[], StructValueStrings[]>>
        GetClassValueStrings(ClrHeap heap, ulong addr)
        {
            try
            {
                addr = Utils.RealAddress(addr);
                var type = heap.GetObjectType(addr);
                if (type == null) return ("Object Value Error" + Constants.HeavyGreekCrossPadded + "Cannot find an instance." + Constants.HeavyGreekCrossPadded + "Heap cannot get object type at address: " + Utils.RealAddressString(addr), null, ClrElementKind.Unknown, (null, null, null, null));
                var kind = TypeExtractor.GetElementKind(type);
                var fldCnt = type.Fields.Count;

                var fldTypes = fldCnt == 0 ? Utils.EmptyArray<ClrType>.Value : new ClrType[fldCnt];
                var fldKinds = fldCnt == 0 ? Utils.EmptyArray<ClrElementKind>.Value : new ClrElementKind[fldCnt];
                var strings = fldCnt == 0 ? Utils.EmptyArray<string>.Value : new string[fldCnt];
                StructValueStrings[] structVals = null;
                for (int i = 0; i < fldCnt; ++i)
                {
                    var fld = type.Fields[i];
                    var fldType = fld.Type; // returns ClrElementKind.Unknown if fld.Type is null
                    fldTypes[i] = fldType;
                    var fldKind = TypeExtractor.GetElementKind(fldType);
                    fldKinds[i] = fldKind;
                    if (fldKind == ClrElementKind.Unknown) continue; // nothing to do here, from MDR lib: There is
                                                                     // a bug in several versions of our debugging layer which causes this.
                    if (TypeExtractor.IsAmbiguousKind(fldKind))
                    {
                        (ClrType aType, ClrElementKind aKind) = TypeExtractor.GetReferenceFieldRealTypeAndKind(heap, addr, fld);
                        if (aType != null)
                        {
                            fldType = aType;
                            fldTypes[i] = fldType;
                            fldKind = aKind;
                            fldKinds[i] = fldKind;
                        }
                    }
                    if (!Utils.SameStrings(fld.Type.Name,fldType.Name))
                    {
                        ulong fldAddr = fld.GetAddress(addr, type.IsValueClass);
                        if (TypeExtractor.IsString(fldKind))
                        {
                            var obj = ValueExtractor.GetStringValue(fldType, fldAddr);
                            strings[i] = obj == null ? Constants.NullValue : (string)obj;
                        }
                        else if (TypeExtractor.IsObjectReference(fldKind))
                        {
                            var obj = fld.GetValue(addr, false, false);
                            strings[i] = obj == null ? Constants.InvalidAddressStr : Utils.RealAddressString((ulong)obj);
                        }
                        else if (TypeExtractor.IsEnum(fldKind))
                        {
                            long intVal;
                            strings[i] = ValueExtractor.GetEnumValueString(fldAddr, fldType, out intVal);
                        }
                        else if (fldType.IsPrimitive)
                        {
                            var obj = fld.Type.GetValue(fldAddr);
                            strings[i] = ValueExtractor.PrimitiveValue(obj, fldKind);
                        }
                        else if (TypeExtractor.IsKnownStruct(fldKind))
                        {
                            switch (TypeExtractor.GetSpecialKind(fldKind))
                            {
                                case ClrElementKind.DateTime:
                                    strings[i] = ValueExtractor.DateTimeValueString(fldAddr, fldType, null);
                                    break;
                                case ClrElementKind.Guid:
                                    strings[i] = ValueExtractor.GuidValueAsString(fldAddr, fldType);
                                    break;
                                case ClrElementKind.Decimal:
                                    strings[i] = ValueExtractor.DecimalValueAsString(fldAddr, fldType,null);
                                    break;
                                case ClrElementKind.TimeSpan:
                                    strings[i] = ValueExtractor.TimeSpanValueAsString(fldAddr, fldType);
                                    break;
                            }
                        }
                        else if (TypeExtractor.IsStruct(fldKind))
                        {
                            StructFields sf = StructFields.GetStructFields(fldType);
                            StructFieldsEx sfx = StructFieldsEx.GetStructFields(sf, fldType);
                            sfx.ResetTypes();
                            if (structVals == null) structVals = new StructValueStrings[fldCnt];
                            ulong structAddr = fld.GetAddress(addr, false);
                            structVals[i] = StructFieldsEx.GetStructValueStrings(sfx, heap, structAddr);
                        }

                        continue;
                    }

                    if (TypeExtractor.IsString(fldKind))
                    {
                        var obj = fld.GetValue(addr, false, true);
                        strings[i] = obj == null ? Constants.NullValue : (string)obj;
                    }
                    else if (TypeExtractor.IsObjectReference(fldKind))
                    {
                        var obj = fld.GetValue(addr, false, false);
                        strings[i] = obj == null ? Constants.InvalidAddressStr : Utils.RealAddressString((ulong)obj);
                    }
                    else if (TypeExtractor.IsEnum(fldKind))
                    {
                        strings[i] = ValueExtractor.GetEnumString(addr, fld, false);
                    }
                    else if (fldType.IsPrimitive)
                    {
                        strings[i] = ValueExtractor.PrimitiveValue(addr, fld, false);
                    }
                    else if (TypeExtractor.IsKnownStruct(fldKind))
                    {
                        switch (TypeExtractor.GetSpecialKind(fldKind))
                        {
                            case ClrElementKind.DateTime:
                                strings[i] = ValueExtractor.GetDateTimeString(addr, fld, false);
                                break;
                            case ClrElementKind.Guid:
                                strings[i] = ValueExtractor.GuidValueAsString(addr, fldType);
                                break;
                            case ClrElementKind.Decimal:
                                strings[i] = ValueExtractor.GetDecimalValue(addr, fld, false);
                                break;
                            case ClrElementKind.TimeSpan:
                                strings[i] = ValueExtractor.TimeSpanValueAsString(addr, fldType);
                                break;
                        }
                    }
                    else if (TypeExtractor.IsStruct(fldKind))
                    {
                        StructFields sf = StructFields.GetStructFields(fldType);
                        StructFieldsEx sfx = StructFieldsEx.GetStructFields(sf, fldType);
                        sfx.ResetTypes();
                        if (structVals == null) structVals = new StructValueStrings[fldCnt];
                        ulong structAddr = fld.GetAddress(addr, false);
                        structVals[i] = StructFieldsEx.GetStructValueStrings(sfx, heap, structAddr);
                    }
                }



                return (null, type, kind, (fldTypes, fldKinds, strings, structVals));
            }
            catch (Exception ex)
            {
                return (Utils.GetExceptionErrorString(ex), null, ClrElementKind.Unknown, (null, null, null, null));
            }


        }

        public static int IndexOfField(IList<ClrInstanceField> lst, string fldName)
        {
            for (int i = 0, icnt = lst.Count; i < icnt; ++i)
            {
                if (Utils.SameStrings(lst[i].Name, fldName)) return i;
            }
            return Constants.InvalidIndex;
        }
    }
}
