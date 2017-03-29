using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace ClrMDRIndex
{
    [Flags]
    public enum ClrElementKind : int
    {
        Unknown = ClrElementType.Unknown,
        Boolean = ClrElementType.Boolean,
        Char = ClrElementType.Char,
        Int8 = ClrElementType.Int8,
        UInt8 = ClrElementType.UInt8,
        Int16 = ClrElementType.UInt16,
        UInt16 = ClrElementType.UInt16,
        Int32 = ClrElementType.Int32,
        UInt32 = ClrElementType.UInt32,
        Int64 = ClrElementType.Int64,
        UInt64 = ClrElementType.UInt64,
        Float = ClrElementType.Float,
        Double = ClrElementType.Double,
        String = ClrElementType.String,
        Pointer = ClrElementType.Pointer,
        Struct = ClrElementType.Struct,
        Class = ClrElementType.Class,
        Array = ClrElementType.Array,
        NativeInt = ClrElementType.NativeInt,
        NativeUInt = ClrElementType.NativeUInt,
        FunctionPointer = ClrElementType.FunctionPointer,
        Object = ClrElementType.Object,
        SZArray = ClrElementType.SZArray,

        Decimal = 0x00010000,
        DateTime = 0x00020000,
        TimeSpan = 0x00030000,
        Guid = 0x00040000,
        Exception = 0x00050000,
        System__Canon = 0x00060000,
        Interface = 0x00070000,
        Abstract = 0x00080000,
        Enum = 0x00090000,
        SystemObject = 0x000A0000,
        SystemVoid = 0x000B0000,

        Null = 0x00F00000,
        Free = 0x01000000
    }

    public class TypeExtractor
    {

        public static ClrElementKind GetElementKind(ClrType clrType)
        {
            if (clrType == null) return ClrElementKind.Unknown;
            ClrElementKind kind = (ClrElementKind)(clrType.ElementType);
            switch (kind)
            {
                case ClrElementKind.Object:
                    switch (clrType.Name)
                    {
                        case "System.Object":
                            kind |= ClrElementKind.SystemObject;
                            break;
                        case "System.Void":
                            kind |= ClrElementKind.SystemVoid;
                            break;
                        case "System.__Canon":
                            kind |= ClrElementKind.System__Canon;
                            break;
                    }
                    break;
                case ClrElementKind.Struct:
                    switch (clrType.Name)
                    {
                        case "System.Decimal":
                            kind |= ClrElementKind.Decimal;
                            break;
                        case "System.DateTime":
                            kind |= ClrElementKind.DateTime;
                            break;
                        case "System.TimeSpan":
                            kind |= ClrElementKind.TimeSpan;
                            break;
                        case "System.Guid":
                            kind |= ClrElementKind.Guid;
                            break;
                    }
                    break;
            }

            if (clrType.IsInterface) kind |= ClrElementKind.Interface;
            else if (clrType.IsAbstract) kind |= ClrElementKind.Abstract;
            else if (clrType.IsFree) kind |= ClrElementKind.Free;
            else if (clrType.IsException) kind |= ClrElementKind.Exception;
            return kind;
        }

        public static ClrElementKind GetSpecialKind(ClrElementKind kind)
        {
            int specKind = (int)kind & (int)0x7FFF0000;
            return (ClrElementKind)specKind;
        }

        public static ClrElementKind GetStandardKind(ClrElementKind kind)
        {
            int stdKind = (int)kind & 0x0000FFFF;
            return (ClrElementKind)stdKind;
        }

        private static readonly string[] KnownTypes = new string[]
        {
            "System.Collections.Generic.Dictionary<",
            "System.Collections.Generic.HashSet<",
            "System.Collections.Generic.SortedDictionary<",
        };

        public static bool IsKnownType(string typeName)
        {
            if (string.Compare("System.Text.StringBuilder", typeName, StringComparison.Ordinal) == 0) return true;

            for (int i = 0, icnt = KnownTypes.Length; i < icnt; ++i)
            {
                if (typeName.StartsWith(KnownTypes[i], StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static KeyValuePair<ClrType,ClrElementKind> TryGetRealType(ClrHeap heap, ulong addr)
        {
            ClrType clrType = heap.GetObjectType(addr);
            ClrElementKind kind = clrType == null ? ClrElementKind.Unknown : GetElementKind(clrType);
            return new KeyValuePair<ClrType, ClrElementKind>(clrType, kind);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static KeyValuePair<ClrType, ClrElementKind> TryGetRealType(ClrHeap heap, ulong addr, ClrInstanceField fld, bool isInternal)
        {
            var obj = fld.GetValue(addr, isInternal);
            if (obj == null) return new KeyValuePair<ClrType, ClrElementKind>(null, ClrElementKind.Unknown);
            var oaddr = (ulong)obj;
            if (oaddr == 0UL) return new KeyValuePair<ClrType, ClrElementKind>(null, ClrElementKind.Unknown);

            ClrType clrType = heap.GetObjectType(oaddr);
            ClrElementKind kind = clrType == null ? ClrElementKind.Unknown : GetElementKind(clrType);
            return new KeyValuePair<ClrType, ClrElementKind>(clrType, kind);
        }


        public static ClrtDisplayableType[] GetClrtDisplayableTypeFields(IndexProxy ndxProxy, ClrHeap heap, ClrtDisplayableType dispType, ClrType clrType, ulong addr, List<int> ambiguousFields)
        {
            ambiguousFields.Clear();
            Debug.Assert(clrType.Fields.Count > 0);
            ClrtDisplayableType[] fields = new ClrtDisplayableType[clrType.Fields.Count];
            for (int i = 0, icnt = clrType.Fields.Count; i < icnt; ++i)
            {
                var fld = clrType.Fields[i];
                var fldType = fld.Type;
                var kind = GetElementKind(fldType);
                var specKind = TypeExtractor.GetSpecialKind(kind);
                if (kind == ClrElementKind.Unknown)
                {
                    fields[i] = new ClrtDisplayableType(dispType, Constants.InvalidIndex, i, Constants.NullName, fld.Name, kind);
                    ambiguousFields.Add(i);
                    continue;
                }

                var typeId = ndxProxy.GetTypeId(fldType.Name);
                if (specKind != ClrElementKind.Unknown)
                {
                    switch (specKind)
                    {
                        case ClrElementKind.Exception:
                        case ClrElementKind.Enum:
                        case ClrElementKind.Free:
                        case ClrElementKind.Guid:
                        case ClrElementKind.DateTime:
                        case ClrElementKind.TimeSpan:
                        case ClrElementKind.Decimal:
                            fields[i] = new ClrtDisplayableType(dispType, typeId, i, fldType.Name, fld.Name, kind);
                            break;
                        case ClrElementKind.SystemVoid:
                        case ClrElementKind.SystemObject:
                        case ClrElementKind.Interface:
                        case ClrElementKind.Abstract:
                        case ClrElementKind.System__Canon:
                            var fldInfo = TryGetRealType(heap, addr, fld, clrType.IsValueClass);
                            if (fldInfo.Key != null)
                            {
                                fldType = fldInfo.Key;
                                typeId = ndxProxy.GetTypeId(fldType.Name);
                                kind = GetElementKind(fldType);
                            }
                            else
                            {
                                ambiguousFields.Add(i);
                            }
                            fields[i] = new ClrtDisplayableType(dispType, typeId, i, fldType.Name, fld.Name, kind);
                            break;
                    }
                }
                else
                {
                    switch (TypeExtractor.GetStandardKind(kind))
                    {
                        case ClrElementKind.Class:
                        case ClrElementKind.Struct:
                        case ClrElementKind.SZArray:
                        case ClrElementKind.Array:
                        case ClrElementKind.String:
                            fields[i] = new ClrtDisplayableType(dispType, typeId, i, fldType.Name, fld.Name, kind);
                            break;
                        case ClrElementKind.Object:
                            fields[i] = new ClrtDisplayableType(dispType, typeId, i, fldType.Name, fld.Name, kind);
                            ambiguousFields.Add(i);
                            break;
                        default:
                            fields[i] = new ClrtDisplayableType(dispType, typeId, i, fldType.Name, fld.Name, kind);
                            break;
                    }
                }
            }
            return fields;
        }


        public static ClrtDisplayableType GetClrtDisplayableType(IndexProxy ndxProxy, ClrHeap heap, int typeId, ulong[] addresses, out string error)
        {
            error = null;
            try
            {
                List<int> ambiguousFields = new List<int>();
                KeyValuePair<ClrType, ClrElementKind> typeInfo = new KeyValuePair<ClrType, ClrElementKind>(null, ClrElementKind.Unknown);
                for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
                {
                    var addr = addresses[i];
                    if (Utils.IsInvalidAddress(addr)) continue;

                    typeInfo = TryGetRealType(heap, addr);
                    var clrType = typeInfo.Key;
                    var kind = typeInfo.Value;
                    var specKind = TypeExtractor.GetSpecialKind(kind);
                    if (specKind != ClrElementKind.Unknown)
                    {
                        switch (specKind)
                        {
                            case ClrElementKind.Free:
                            case ClrElementKind.Guid:
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
                                dispType.AddFields(dispFlds);
                                return dispType;
                            case ClrElementKind.Unknown:
                                continue;
                            default:
                                return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
                        }
                    }
                }
                return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, Constants.NullName, String.Empty, ClrElementKind.Unknown);
            }
            catch(Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
        }

    }
}
