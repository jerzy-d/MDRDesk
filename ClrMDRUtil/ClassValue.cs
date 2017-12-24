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
                    fldTypes[i] = fldType;
                    var fldKind = TypeExtractor.GetElementKind(fldType);
                    fldKinds[i] = fldKind;
                    if (fldKind == ClrElementKind.Unknown) continue; // nothing to do here, from MDR lib: There is
                                                                     // a bug in several versions of our debugging layer which causes this.
                    if (TypeExtractor.IsString(fldKind))
                    {
                        objects[i] = fld.GetValue(addr, false, true);
                    }
                    else if (TypeExtractor.IsObjectReference(fldKind))
                    {
                        objects[i] = fld.GetValue(addr, false, false);
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
