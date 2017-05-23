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
        Int16 = ClrElementType.Int16,
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
		Free = 0x01000000,
		Error = 0x02000000
	}

	public class TypeExtractor
    {
        public static ClrElementKind GetElementKind(ClrType clrType)
        {
            if (clrType == null) return ClrElementKind.Unknown;
            ClrElementKind kind = (ClrElementKind)(clrType.ElementType);
			if (Utils.SameStrings(clrType.Name, "Error")) return ClrElementKind.Error;
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
			else if (clrType.IsEnum) kind |= ClrElementKind.Enum;
			else if (clrType.IsFree) kind |= ClrElementKind.Free;
            else if (clrType.IsException) kind |= ClrElementKind.Exception;
            return kind;
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ClrElementKind GetSpecialKind(ClrElementKind kind)
        {
            int specKind = (int)kind & (int)0x7FFF0000;
            return (ClrElementKind)specKind;
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ClrElementKind GetStandardKind(ClrElementKind kind)
        {
            int stdKind = (int)kind & 0x0000FFFF;
            return (ClrElementKind)stdKind;
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsString(ClrElementKind kind)
        {
            return GetStandardKind(kind) == ClrElementKind.String;
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsObjectReference(ClrElementKind kind)
		{
			var stdKind = GetStandardKind(kind);
			return stdKind == ClrElementKind.String || stdKind == ClrElementKind.Class
								 || stdKind == ClrElementKind.Array || stdKind == ClrElementKind.SZArray
								 || stdKind == ClrElementKind.Object;
		}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsKnownPrimitive(ClrElementKind kind)
        {
            var stdKind = GetStandardKind(kind);
            if (stdKind == ClrElementKind.String
                || stdKind == ClrElementKind.Boolean
                || stdKind == ClrElementKind.Char
                || stdKind == ClrElementKind.Double
                || stdKind == ClrElementKind.Float
                || stdKind == ClrElementKind.FunctionPointer
                || stdKind == ClrElementKind.Int16
                || stdKind == ClrElementKind.Int32
                || stdKind == ClrElementKind.Int64
                || stdKind == ClrElementKind.Int8
                || stdKind == ClrElementKind.NativeInt
                || stdKind == ClrElementKind.NativeUInt
                || stdKind == ClrElementKind.Pointer
                || stdKind == ClrElementKind.UInt16
                || stdKind == ClrElementKind.UInt32
                || stdKind == ClrElementKind.UInt64
                || stdKind == ClrElementKind.UInt8
                )
                return true;
            var specKind = GetSpecialKind(kind);
            if (specKind == ClrElementKind.Decimal
                || specKind == ClrElementKind.DateTime
                || specKind == ClrElementKind.Guid
                || specKind == ClrElementKind.TimeSpan
                )
                return true;
            return false;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsStruct(ClrElementKind kind)
		{
			return kind == ClrElementKind.Struct;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsSystemObject(ClrElementKind kind)
        {
            int stdKind = (int)kind & 0x0000FFFF;
            return (ClrElementKind)stdKind == ClrElementKind.SystemObject;
        }

        public static bool IsAmbiguousKind(ClrElementKind kind)
        {
            ClrElementKind specKind = GetSpecialKind(kind);
            switch (specKind)
            {
                case ClrElementKind.SystemVoid:
                case ClrElementKind.SystemObject:
                case ClrElementKind.Interface:
                case ClrElementKind.Abstract:
                case ClrElementKind.System__Canon:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsUndecidedKind(ClrElementKind kind)
        {
            ClrElementKind specKind = GetSpecialKind(kind);
            switch (specKind)
            {
                case ClrElementKind.SystemVoid:
                case ClrElementKind.Interface:
                case ClrElementKind.Abstract:
                case ClrElementKind.System__Canon:
                    return true;
                default:
                    return false;
            }
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
        public static KeyValuePair<ClrType, ClrElementKind> TryGetRealType(ClrHeap heap, ulong addr)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FieldCount(ClrType clrType)
        {
            return clrType == null ? 0 : (clrType.Fields==null ? 0 : clrType.Fields.Count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasInternalAddresses(ClrType clrType)
        {
            return clrType == null ? false : clrType.IsValueClass;
        }

        public static void TryGetClrtDisplayableTypeFields(IndexProxy ndxProxy, ClrHeap heap, ClrtDisplayableType dispType, ClrType clrType, ulong addr, List<int> ambiguousFields, List<int> ambiguousFields2, ClrtDisplayableType[] fields)
        {
            Debug.Assert(clrType.Fields.Count > 0);
            ambiguousFields2.Clear();
            for (int i = 0, icnt = ambiguousFields.Count; i < icnt; ++i)
            {
                var fld = clrType.Fields[i];
                var fldType = fld.Type;
                var kind = GetElementKind(fldType);
                var specKind = TypeExtractor.GetSpecialKind(kind);
                if (kind == ClrElementKind.Unknown)
                {
                    fields[i] = new ClrtDisplayableType(dispType, Constants.InvalidIndex, i, Constants.NullName, fld.Name, kind);
                    ambiguousFields2.Add(i);
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
                                ambiguousFields2.Add(i);
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
                            ambiguousFields2.Add(i);
                            break;
                        default:
                            fields[i] = new ClrtDisplayableType(dispType, typeId, i, fldType.Name, fld.Name, kind);
                            break;
                    }
                }
            }
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
                        default:
                            fields[i] = new ClrtDisplayableType(dispType, typeId, i, fldType.Name, fld.Name, kind);
                            break;
                    }
                }
            }
            return fields;
        }


        public static ClrtDisplayableType GetClrtDisplayableType(IndexProxy ndxProxy, ClrHeap heap, ClrtDisplayableType parent, int typeId, ulong[] addresses, out string error)
        {
            error = null;
            try
            {
                List<int> ambiguousFields = new List<int>();
                List<int> ambiguousFields2 = new List<int>();
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
                                return new ClrtDisplayableType(parent, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
                            case ClrElementKind.Interface:
                                throw new ApplicationException("Interface kind is not expected from ClrHeap.GetHeapObject(...) method.");
                            case ClrElementKind.Enum:
                                return new ClrtDisplayableType(parent, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
                            case ClrElementKind.System__Canon:
                                throw new ApplicationException("System__Canon kind is not expected from ClrHeap.GetHeapObject(...) method.");
                            case ClrElementKind.Exception:
                            case ClrElementKind.Abstract:
                            case ClrElementKind.SystemVoid:
                            case ClrElementKind.SystemObject:
                                return new ClrtDisplayableType(parent, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
                        }
                    }
                    else
                    {
                        switch (TypeExtractor.GetStandardKind(kind))
                        {
                            case ClrElementKind.String:
                                return new ClrtDisplayableType(parent, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
                            case ClrElementKind.SZArray:
                            case ClrElementKind.Array:
                                return new ClrtDisplayableType(parent, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
                            case ClrElementKind.Struct:
                            case ClrElementKind.Object:
                            case ClrElementKind.Class:
                                var dispType = new ClrtDisplayableType(parent, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
                                var dispFlds = GetClrtDisplayableTypeFields(ndxProxy, heap, dispType, clrType, addr, ambiguousFields);
                                var ambiguousCount = ambiguousFields.Count;
                                var switchList = false;
                                for (int j = i+1, jcnt = addresses.Length; j < jcnt && ambiguousCount > 0; ++j)
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
                                return new ClrtDisplayableType(parent, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
                        }
                    }
                }
                return new ClrtDisplayableType(parent, typeId, Constants.InvalidIndex, Constants.NullName, String.Empty, ClrElementKind.Unknown);
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
        }

        public static ClrtDisplayableType GetClrtDisplayableTypeFields(IndexProxy ndxProxy, ClrHeap heap, ClrtDisplayableType parent, ulong[] addresses, out string error)
        {
            error = null;
            try
            {
                List<int> ambiguousFields = new List<int>();
                List<int> ambiguousFields2 = new List<int>();
                KeyValuePair<ClrType, ClrElementKind> typeInfo = new KeyValuePair<ClrType, ClrElementKind>(null, ClrElementKind.Unknown);
                for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
                {
                    var addr = addresses[i];
                    if (Utils.IsInvalidAddress(addr)) continue;

                    typeInfo = TryGetRealType(heap, addr);
                    var clrType = typeInfo.Key;
                    var kind = typeInfo.Value;
                    var specKind = TypeExtractor.GetSpecialKind(kind);

                    {
                        switch (TypeExtractor.GetStandardKind(kind))
                        {
                            case ClrElementKind.Struct:
                            case ClrElementKind.Object:
                            case ClrElementKind.Class:
                                var dispFlds = GetClrtDisplayableTypeFields(ndxProxy, heap, parent, clrType, addr, ambiguousFields);
                                var ambiguousCount = ambiguousFields.Count;
                                var switchList = false;
                                for (int j = i + 1, jcnt = addresses.Length; j < jcnt && ambiguousCount > 0; ++j)
                                {
                                    var jaddr = addresses[j];
                                    if (switchList)
                                    {
                                        ambiguousCount = ambiguousFields2.Count;
                                        switchList = !switchList;
                                        TryGetClrtDisplayableTypeFields(ndxProxy, heap, parent, clrType, jaddr, ambiguousFields2, ambiguousFields, dispFlds);
                                        continue;
                                    }
                                    ambiguousCount = ambiguousFields.Count;
                                    switchList = !switchList;
                                    TryGetClrtDisplayableTypeFields(ndxProxy, heap, parent, clrType, jaddr, ambiguousFields, ambiguousFields2, dispFlds);
                                }
                                parent.AddFields(dispFlds);
                                return parent;
                            case ClrElementKind.Unknown:
                                continue;
                            default:
                                throw new ApplicationException("TypeExtractor.GetClrtDisplayableTypeFields -- ");
                        }
                    }
                }
                return parent;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
        }


        public static KeyValuePair<ClrType, ulong> TryGetReferenceType(ClrHeap heap, ulong addr, ClrInstanceField fld, bool intr)
        {
            var valueObj = fld.GetValue(addr, intr);
            Debug.Assert(valueObj is ulong);
            ulong value = (ulong)valueObj;
            var clrType = heap.GetObjectType(value);
            return new KeyValuePair<ClrType, ulong>(clrType, value);
        }

        public static (ClrType, ClrInstanceField, ulong) GetReferenceTypeField(ClrHeap heap, ClrType clrType, ulong addr, string fldName)
        {
            ClrInstanceField fld = clrType.GetFieldByName(fldName);
            var valueObj = fld.GetValue(addr, false);
            Debug.Assert(valueObj is ulong);
            ulong value = (ulong)valueObj;
            var fldType = heap.GetObjectType(value);
            return (fldType, fld, value);
        }
        public static (ClrType, ClrInstanceField, ulong) GetStructTypeField(ClrHeap heap, ClrType clrType, ulong addr, string fldName)
        {
            ClrInstanceField fld = clrType.GetFieldByName(fldName);
            var value = fld.GetAddress(addr, true);
            var fldAddr = ValueExtractor.ReadPointerAtAddress(addr, heap);
            var fldType = heap.GetObjectType(fldAddr);
            return (fldType, fld, fldAddr);
        }
    }
}
