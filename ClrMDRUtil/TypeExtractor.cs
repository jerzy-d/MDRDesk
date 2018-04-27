using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Globalization;

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

        Decimal =       0x00010000,
        DateTime =      0x00020000,
        TimeSpan =      0x00030000,
        Guid =          0x00040000,
        Exception =     0x00050000,
        System__Canon = 0x00060000,
        Interface =     0x00070000,
        Abstract =      0x00080000,
        Enum =          0x00090000,
        SystemObject =  0x000A0000,
        SystemVoid =    0x000B0000,
        SystemNullable =0x000C0000,

        Null =          0x00F00000,
        Free =          0x01000000,
        Error =         0x02000000
    }

    public class TypeExtractor
    {
        public static ClrElementKind GetElementKind(ClrType clrType)
        {
            if (clrType == null) return ClrElementKind.Unknown;
            ClrElementKind kind = (ClrElementKind)(clrType.ElementType);
            string name = clrType.Name;
            if (Utils.SameStrings(name, "Error")) return ClrElementKind.Error;
            if (name.StartsWith("System.Nullable<", StringComparison.Ordinal)) return kind |= ClrElementKind.SystemNullable;
            switch (kind)
            {
                case ClrElementKind.Object:
                    switch (name)
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
        public static string ToString(ClrElementKind kind)
        {
            var clrKind = GetClrElementType(kind);
            var spKind = GetSpecialKind(kind);
            if (spKind != ClrElementKind.Unknown)
                return clrKind.ToString() + "|" + spKind.ToString();
            return clrKind.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSpecialKind(ClrElementKind kind)
        {
            return ((int)kind & (int)0x7FFF0000) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsKnownStruct(ClrElementKind kind)
        {
            ClrElementKind specKind = (ClrElementKind)((int)kind & 0x7FFF0000);
            return specKind == ClrElementKind.DateTime
                || specKind == ClrElementKind.Decimal
                || specKind == ClrElementKind.Guid
                || specKind == ClrElementKind.TimeSpan;
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
        public static ClrElementType GetClrElementType(ClrElementKind kind)
        {
            int stdKind = (int)kind & 0x0000FFFF;
            return (ClrElementType)stdKind;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsString(ClrElementKind kind)
        {
            return GetStandardKind(kind) == ClrElementKind.String;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsGuid(ClrElementKind kind)
        {
            return GetSpecialKind(kind) == ClrElementKind.Guid;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEnum(ClrElementKind kind)
        {
            return GetSpecialKind(kind) == ClrElementKind.Enum;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSystemNullable(ClrElementKind kind)
        {
            return GetSpecialKind(kind) == ClrElementKind.SystemNullable;
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
        public static bool IsValueClass(ClrElementKind kind)
        {
            var stdKind = GetStandardKind(kind);
            return stdKind == ClrElementKind.Struct;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNonStringObjectReference(ClrElementKind kind)
        {
            var stdKind = GetStandardKind(kind);
            return stdKind == ClrElementKind.Class
                                 || stdKind == ClrElementKind.Array || stdKind == ClrElementKind.SZArray
                                 || stdKind == ClrElementKind.Object;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsArray(ClrElementKind kind)
        {
            var stdKind = GetStandardKind(kind);
            return stdKind == ClrElementKind.Array || stdKind == ClrElementKind.SZArray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSystem__Canon(ClrElementKind kind)
        {
            return (GetSpecialKind(kind) & ClrElementKind.System__Canon) != 0;
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
        public static bool IsPrimitive(ClrElementKind kind)
        {
            var stdKind = GetStandardKind(kind);
            if (stdKind == ClrElementKind.Boolean
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
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetFieldCount(ClrType type)
        {
            return  (type == null || type.Fields == null) ? 0 : type.Fields.Count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTuple<ClrType, ClrInstanceField, ClrElementKind>[] GetFieldsAndKinds(ClrType type)
        {
            int cnt = GetFieldCount(type);
            if (cnt < 1) return Utils.EmptyArray<ValueTuple<ClrType, ClrInstanceField, ClrElementKind>>.Value;
            var ary = new ValueTuple<ClrType, ClrInstanceField, ClrElementKind>[cnt];
            for(int i = 0; i < cnt; ++i)
            {
                ClrInstanceField fld = type.Fields[i];
                ClrType fldType = fld.Type;
                ClrElementKind fldKind = GetElementKind(fldType);
                ary[i] = (fldType, fld, fldKind);
            }
            return ary;
        }

        public static ValueTuple<ClrType, ClrInstanceField, ClrElementKind> GetTypeFieldAndKind(ClrType type, string fldName)
        {
            int cnt = GetFieldCount(type);
            if (cnt < 1) return (null, null, ClrElementKind.Unknown);
            for (int i = 0; i < cnt; ++i)
            {
                ClrInstanceField fld = type.Fields[i];
                if (Utils.SameStrings(fldName,fld.Name))
                {
                    return (fld.Type, fld, GetElementKind(fld.Type));
                }
            }
            return (null, null, ClrElementKind.Unknown);
        }

        public static bool GetTypeFromString(string str, ClrElementKind kind, out object val)
        {
            bool ok = false;
            val = null;
            var specKind = TypeExtractor.GetSpecialKind(kind);

            if (specKind != ClrElementKind.Unknown)
            {
                switch (specKind)
                {
                    case ClrElementKind.Exception:
                    case ClrElementKind.Enum:
                    case ClrElementKind.Free:
                    case ClrElementKind.Guid:
                        Guid guid;
                        if (Guid.TryParse(str, out guid))
                        {
                            ok = true;
                            val = guid;
                        }
                        break;
                    case ClrElementKind.DateTime:
                        DateTime dt;
                        if (DateTime.TryParse(str, out dt))
                        {
                            ok = true;
                            val = dt;

                        }
                        break;
                    case ClrElementKind.TimeSpan:
                        TimeSpan ts;
                        if (TimeSpan.TryParse(str, out ts))
                        {
                            ok = true;
                            val = ts;
                        }
                        break;
                    case ClrElementKind.Decimal:
                        decimal decimalVal;
                        if (Decimal.TryParse(str, out decimalVal))
                        {
                            ok = true;
                            val = decimalVal;
                        }
                        break;
                    case ClrElementKind.SystemVoid:
                        break;
                    case ClrElementKind.SystemObject:
                    case ClrElementKind.Interface:
                    case ClrElementKind.Abstract:
                    case ClrElementKind.System__Canon:
                        ulong addr;
                        if (str.StartsWith("0x") || str.StartsWith("0X"))
                        {
                            str = str.Substring(2);
                        }
                        if (UInt64.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out addr))
                        {
                            ok = true;
                            val = addr;
                        }
                        break;
                    default:
                        break;
                }
            }
            else
            {
                var stdKind = TypeExtractor.GetStandardKind(kind);
                switch (stdKind)
                {
                    case ClrElementKind.Boolean:
                        ok = true;
                        if (str.Length == 1)
                        {
                            char c = char.ToLower(str[0]);
                            if (c == 't') val = true;
                            else if (c == 'f') val = false;
                            else ok = false;
                        }
                        else
                        {
                            str = str.ToLower();
                            if (Utils.SameStrings("true", str)) val = true;
                            else if (Utils.SameStrings("false", str)) val = false;
                            else ok = false;
                        }
                        break;
                    case ClrElementKind.Class:
                    case ClrElementKind.Struct:
                        break;
                    case ClrElementKind.SZArray:
                    case ClrElementKind.Array:
                    case ClrElementKind.Object:
                        ulong addr;
                        if (str.StartsWith("0x") || str.StartsWith("0X"))
                        {
                            str = str.Substring(2);
                        }
                        if (UInt64.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out addr))
                        {
                            ok = true;
                            val = addr;
                        }
                        break;
                    case ClrElementKind.Char:
                        char ch;
                        if (char.TryParse(str,out ch))
                        {
                            val = ch;
                            ok = true;
                        }
                        break;
                    case ClrElementKind.Int8:
                    case ClrElementKind.UInt8:
                    case ClrElementKind.Int16:
                    case ClrElementKind.UInt16:
                    case ClrElementKind.Int32:
                        Int32 i32;
                        if (Int32.TryParse(str, out i32))
                        {
                            val = i32;
                            ok = true;
                        }
                        break;
                    case ClrElementKind.UInt32:
                    case ClrElementKind.Int64:
                    case ClrElementKind.UInt64:

                        break;
                    case ClrElementKind.Float:
                    case ClrElementKind.Double:
                        break;
                    case ClrElementKind.String:
                        ok = true;
                        break;
                    case ClrElementKind.Pointer:
                    case ClrElementKind.NativeInt:
                    case ClrElementKind.NativeUInt:
                    case ClrElementKind.FunctionPointer:
                        break;
                    default:
                        break;
                }
            }
            return ok;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUnknownStruct(ClrElementKind kind)
        {
            return kind == ClrElementKind.Struct;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSystemObject(ClrElementKind kind)
        {
            return GetSpecialKind(kind) == ClrElementKind.SystemObject;
        }

        public static bool IsAmbiguousKind(ClrElementKind kind)
        {
            if (kind == ClrElementKind.Unknown) return true;
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

        public static ClrType GetReferenceFieldRealType(ClrHeap heap, ulong parentAddr, ClrInstanceField fld)
        {
            if (fld == null) return null;
            var addrObj = fld.GetValue(parentAddr);
            if (addrObj == null) return null;
            if (addrObj is ulong) return heap.GetObjectType((ulong)addrObj);
            return null;
        }

        public static ValueTuple<ClrType, ClrElementKind> GetReferenceFieldRealTypeAndKind(ClrHeap heap, ulong parentAddr, ClrInstanceField fld)
        {
            if (fld == null) return (null, ClrElementKind.Unknown);
            var addrObj = fld.GetValue(parentAddr);
            if (addrObj == null) return (null, ClrElementKind.Unknown);
            if (addrObj is ulong)
            {
                var clrType = heap.GetObjectType((ulong)addrObj);
                return (clrType, GetElementKind(clrType));
            }
            return (null, ClrElementKind.Unknown);
        }

        public static ValueTuple<bool, ClrElementKind, ClrType, ClrElementKind> IsUndecidedType(ClrType clrType)
        {
            var kind = GetElementKind(clrType);
            if (clrType.BaseType == null)
                return new ValueTuple<bool, ClrElementKind, ClrType, ClrElementKind>(IsAmbiguousKind(kind), kind, null, ClrElementKind.Unknown);
            var baseKind = GetElementKind(clrType.BaseType);
            bool isUndecided = !IsSystemObject(baseKind) || IsAmbiguousKind(kind);
            return new ValueTuple<bool, ClrElementKind, ClrType, ClrElementKind>(isUndecided, kind, clrType.BaseType, baseKind);
        }

        private static readonly string[] excludedTypeNames = new[]
        {
                "Free",
                "System.DateTime",
                "System.Decimal",
                "System.Guid",
                "System.String",
                "System.TimeSpan",

        };

        public static bool IsExludedType(string typeName)
        {
            return Array.IndexOf(excludedTypeNames, typeName) >= 0;
        }

        private static readonly string[] KnownTypeNames = new string[]
        {
            "System.Text.StringBuilder",
            "System.Collections.Generic.Dictionary<",
            "System.Collections.Generic.HashSet<",
            "System.Collections.Generic.SortedDictionary<",
            "System.Collections.Generic.SortedList<",
            "System.Collections.Generic.List<",
            "System.Collections.Generic.Queue<",
            "System.Collections.Generic.Stack<",
            "System.Collections.Concurrent.ConcurrentDictionary<",
            "System.Collections.Generic.SortedSet<",
        };

        public enum KnownTypes
        {
            StringBuilder,
            Dictionary,
            HashSet,
            SortedDictionary,
            SortedList,
            List,
            Queue,
            Stack,
            ConcurrentDictionary,
            SortedSet,
            Unknown,
        }

        public static bool Is(KnownTypes knownType, string typeName)
        {
            if (knownType == KnownTypes.StringBuilder) return Utils.SameStrings(typeName, KnownTypeNames[(int)KnownTypes.StringBuilder]);
            return typeName.StartsWith(KnownTypeNames[(int)knownType], StringComparison.Ordinal);
        }

        public static string GetKnowTypeName(KnownTypes kt)
        {
            switch(kt)
            {
                case KnownTypes.StringBuilder:
                    return "StringBuilder";
                case KnownTypes.Dictionary:
                    return "Dictionary<TKey,TValue>";
                case KnownTypes.HashSet:
                    return "HashSet<T>";
                case KnownTypes.SortedDictionary:
                    return "SortedDictionary<TKey,TValue>";
                case KnownTypes.SortedList:
                    return "SortedList<TKey,TValue>";
                case KnownTypes.List:
                    return "List<T>";
                case KnownTypes.Queue:
                    return "Queue<T>";
                case KnownTypes.Stack:
                    return "Stack<T>";
                case KnownTypes.ConcurrentDictionary:
                    return "ConcurrentDictionary<TKey,TValue>";
                case KnownTypes.SortedSet:
                    return "SortedSet<T>";
                default:
                    return "Unknown Type";
            }
        }

        public static bool IsKnownType(string typeName)
        {
            for (int i = 0, icnt = KnownTypeNames.Length; i < icnt; ++i)
            {
                if (typeName.StartsWith(KnownTypeNames[i], StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public static string GetKnowTypeName(string typeName)
        {
            KnownTypes kt = IsKnownCollection(typeName);
            return GetKnowTypeName(kt);
        }

        public static string GetDisplayableTypeName(string typeName)
        {
            KnownTypes kt = IsKnownCollection(typeName);
            if (kt != KnownTypes.Unknown) return GetKnowTypeName(kt);
            return Utils.BaseTypeName(typeName);
        }

        public static KnownTypes IsKnownCollection(string typeName)
        {
            for (int i = 0, icnt = KnownTypeNames.Length; i < icnt; ++i)
            {
                if (typeName.StartsWith(KnownTypeNames[i], StringComparison.Ordinal))
                    return (KnownTypes)(i);
            }
            return KnownTypes.Unknown;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTuple<ClrType, ClrElementKind, string> TryGetRealType(ClrHeap heap, ulong addr)
        {
            ClrElementKind kind = ClrElementKind.Unknown;
            ClrType clrType = heap.GetObjectType(addr);
            string name = string.Empty;
            if (clrType != null)
            {
                name = clrType.Name;
                if (clrType.IsRuntimeType)
                {
                    var type = clrType.GetRuntimeType(addr);
                    if (type != null)
                    {
                        name = type.Name;
                    }
                }
                kind = GetElementKind(clrType);
            }
            return new ValueTuple<ClrType, ClrElementKind,string>(clrType, kind,name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTuple<ClrType, ClrElementKind, ClrType, ClrElementKind> GetRealType(ClrHeap heap, ulong addr)
        {
            ClrElementKind kind = ClrElementKind.Unknown;
            ClrType clrType = heap.GetObjectType(addr);
            kind = GetElementKind(clrType);
            ClrType rtype = null;
            ClrElementKind rkind = ClrElementKind.Unknown;
            if (clrType != null && clrType.IsRuntimeType)
            {
                rtype = clrType.GetRuntimeType(addr);
                rkind = GetElementKind(clrType);
            }
            return (clrType, kind, rtype, rkind);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static KeyValuePair<ClrType, ClrElementKind> TryGetRealType(ClrHeap heap, ulong addr, ClrInstanceField fld, bool isInternal)
        {
            var obj = fld.GetValue(addr, isInternal);
            if (obj == null || !(obj is ulong)) return new KeyValuePair<ClrType, ClrElementKind>(null, ClrElementKind.Unknown);
            var oaddr = (ulong)obj;
            if (oaddr == 0UL) return new KeyValuePair<ClrType, ClrElementKind>(null, ClrElementKind.Unknown);

            ClrType clrType = heap.GetObjectType(oaddr);
            ClrElementKind kind = clrType == null ? ClrElementKind.Unknown : GetElementKind(clrType);
            return new KeyValuePair<ClrType, ClrElementKind>(clrType, kind);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong GetFieldValueAsAddress(ClrInstanceField fld, ulong addr, bool intern)
        {
            var obj = fld.GetValue(addr, intern, false);
            if (obj == null) return Constants.InvalidAddress;
            return (obj is ulong) ? (ulong)obj : Constants.InvalidAddress;
        }

        public static ValueTuple<ClrType, ClrElementKind, ulong> GetRealType(ClrHeap heap, ulong addr, ClrInstanceField fld, bool intern)
        {
            ClrType clrType = null;
            ClrElementKind kind = ClrElementKind.Unknown;
            ulong oAddr = Constants.InvalidAddress;
            if (fld.Type != null)
            {
                kind = GetElementKind(fld.Type);
                if (TypeExtractor.IsAmbiguousKind(kind))
                {
                    oAddr = fld.GetAddress(addr, intern);
                    clrType = heap.GetObjectType(oAddr);
                    kind = GetElementKind(fld.Type);
                }
                if (!(kind== ClrElementKind.Unknown) && !TypeExtractor.IsAmbiguousKind(kind))
                {
                    if (TypeExtractor.IsNonStringObjectReference(kind))
                        oAddr = GetFieldValueAsAddress(fld, addr, intern);
                    return new ValueTuple<ClrType, ClrElementKind, ulong>(fld.Type, kind, oAddr);
                }
            }
            oAddr = GetFieldValueAsAddress(fld, addr, intern);
            if (oAddr == Constants.InvalidAddress) return new ValueTuple<ClrType, ClrElementKind, ulong>(null, ClrElementKind.Unknown, Constants.InvalidAddress);
            clrType = heap.GetObjectType(oAddr);
            kind = clrType == null ? ClrElementKind.Unknown : GetElementKind(clrType);
            return new ValueTuple<ClrType, ClrElementKind, ulong>(clrType, kind, oAddr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FieldCount(ClrType clrType)
        {
            return clrType == null ? 0 : (clrType.Fields == null ? 0 : clrType.Fields.Count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasInternalAddresses(ClrType clrType)
        {
            return clrType == null ? false : clrType.IsValueClass;
        }

        public static void TryGetClrtDisplayableTypeFields(IndexProxy ndxProxy, ClrHeap heap, ulong addr, List<int> ambiguousFields, ClrtDisplayableType[] flds, SortedDictionary<string, List<ulong>>[] fldAddresses)
        {
            for (int i = 0, icnt = ambiguousFields.Count; i < icnt; ++i)
            {
                int fldNdx = ambiguousFields[i];
                var cdt = flds[fldNdx];
                var dct = fldAddresses[i];
                var nextParentType = heap.GetObjectType(addr);
                if (nextParentType == null) return;
                var nextFld = nextParentType.Fields[fldNdx];
                (ClrType nextFldType, ClrElementKind nextFldKind, ulong address) = GetRealType(heap, addr, nextFld, nextParentType.IsValueClass);
                if (nextFldType == null) continue;
                List<ulong> addrLst;
                if (dct.TryGetValue(nextFldType.Name, out addrLst))
                {
                    addrLst.Add(address);
                }
                else
                {
                    addrLst = new List<ulong>(64) { address };
                    dct.Add(nextFldType.Name, addrLst);
                    var typeId = ndxProxy.GetTypeId(nextFldType.Name);
                    cdt.AddAlternative(new ClrtDisplayableType(cdt, typeId, fldNdx, nextFldType.Name, nextFld.Name, nextFldKind));
                }
            }
        }

        public static ClrtDisplayableType[] GetClrtDisplayableTypeFields(IndexProxy ndxProxy, ClrHeap heap,
                                                                            ClrtDisplayableType dispType, ClrType clrType, ulong addr,
                                                                            List<int> ambiguousFields)
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

                    fields[i] = new ClrtDisplayableType(dispType, Constants.InvalidIndex, i, Constants.UnknownFieldTypeName, fld.Name, kind);
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
                            ambiguousFields.Add(i);
                            fields[i] = new ClrtDisplayableType(dispType, typeId, i, fldType.Name, fld.Name, kind);
                            break;
                    }
                }
                else
                {
                    switch (TypeExtractor.GetStandardKind(kind))
                    {
                        case ClrElementKind.Struct:
                        case ClrElementKind.SZArray:
                        case ClrElementKind.Array:
                        case ClrElementKind.String:
                            fields[i] = new ClrtDisplayableType(dispType, typeId, i, fldType.Name, fld.Name, kind);
                            break;
                        case ClrElementKind.Class:
                        case ClrElementKind.Object:
                            (bool isAmbiguous, ClrElementKind clrTypeKind, ClrType baseType, ClrElementKind baseTypeKind) = IsUndecidedType(fldType);
                            var refType = GetReferenceFieldRealType(heap, addr, fld);
                            if (refType == null || !Utils.SameStrings(refType.Name, fldType.Name)) isAmbiguous = true;
                            if (isAmbiguous) ambiguousFields.Add(i);
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


        /// <summary>
        /// 
        /// </summary>
        /// <param name="ndxProxy">Basic indexed dump data.</param>
        /// <param name="heap">MDR's a crash dump heap.</param>
        /// <param name="parent"></param>
        /// <param name="typeId"></param>
        /// <param name="addresses"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public static ClrtDisplayableType GetClrtDisplayableType(IndexProxy ndxProxy, ClrHeap heap, int typeId, ulong[] addresses, out string error)
        {
            error = null;
            try
            {
                List<int> ambiguousFields = new List<int>();
                ClrType clrType = null;

                for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
                {
                    var addr = Utils.RealAddress(addresses[i]);
                    if (Utils.IsInvalidAddress(addr)) continue;
                    clrType = heap.GetObjectType(addr);
                    if (clrType == null) continue;
                    if (clrType.Fields == null || clrType.Fields.Count < 1)
                    {
                        error = Constants.MediumVerticalBarHeader + "Type: '" + clrType.Name + "' has no fields.";
                        return null;
                    }

                    (bool isAmbiguous, ClrElementKind clrTypeKind, ClrType baseType, ClrElementKind baseTypeKind) = IsUndecidedType(clrType);
                    var specKind = TypeExtractor.GetSpecialKind(clrTypeKind);


                    if (specKind != ClrElementKind.Unknown)
                    {
                        switch (specKind)
                        {
                            case ClrElementKind.Free:
                            case ClrElementKind.Guid:
                            case ClrElementKind.DateTime:
                            case ClrElementKind.TimeSpan:
                            case ClrElementKind.Decimal:
                                error = Constants.MediumVerticalBarHeader + "Type: '" + clrType.Name + "' is known structure or free.";
                                return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, clrTypeKind);
                            case ClrElementKind.Interface:
                                throw new MdrException("[GetClrtDisplayableType.GetClrtDisplayableType] Interface kind is not expected from ClrHeap.GetHeapObject(...) method.");
                            case ClrElementKind.Enum:
                                error = Constants.MediumVerticalBarHeader + "Type: '" + clrType.Name + "' is boxed enum.";
                                return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, clrTypeKind);
                            case ClrElementKind.System__Canon:
                                throw new MdrException("[GetClrtDisplayableType.GetClrtDisplayableType] System__Canon kind is not expected from ClrHeap.GetHeapObject(...) method.");
                            case ClrElementKind.Exception:
                                var dispType = new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, clrTypeKind);
                                var dispFlds = GetClrtDisplayableTypeFields(ndxProxy, heap, dispType, clrType, addr, ambiguousFields);
                                if (ambiguousFields.Count > 0)
                                {
                                    SortedDictionary<string, List<ulong>>[] fldAddrDcts = new SortedDictionary<string, List<ulong>>[ambiguousFields.Count];
                                    for (int j = 0, jcnt = fldAddrDcts.Length; j < jcnt; ++j)
                                    {
                                        fldAddrDcts[j] = new SortedDictionary<string, List<ulong>>(StringComparer.Ordinal);
                                    }
                                    for (int j = 0, jcnt = addresses.Length; j < jcnt; ++j)
                                    {
                                        var jaddr = addresses[j];
                                        try
                                        {
                                            TryGetClrtDisplayableTypeFields(ndxProxy, heap, jaddr, ambiguousFields, dispFlds, fldAddrDcts);
                                        }
                                        catch (Exception ex)
                                        {
                                            error = Utils.GetExceptionErrorString(ex);
                                        }
                                    }
                                    HandleAlternatives(dispType, dispFlds, ambiguousFields, fldAddrDcts);
                                }
                                dispType.AddFields(dispFlds);
                                return dispType;
                            case ClrElementKind.Abstract:
                            case ClrElementKind.SystemVoid:
                            case ClrElementKind.SystemObject:
                                error = Constants.MediumVerticalBarHeader + "Type: '" + clrType.Name + "' is System.Object.";
                                return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, clrTypeKind);
                        }
                    }
                    else
                    {
                        if (TypeExtractor.IsPrimitive(clrTypeKind))
                        {
                            error = Constants.MediumVerticalBarHeader + "Type: '" + clrType.Name + "' is primitive.";
                            return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, clrTypeKind);
                        }
                        switch (TypeExtractor.GetStandardKind(clrTypeKind))
                        {
                            case ClrElementKind.String:
                                error = Constants.MediumVerticalBarHeader + "Type: '" + clrType.Name + "' is known structure or free.";
                                return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, clrTypeKind);
                            case ClrElementKind.SZArray:
                            case ClrElementKind.Array:
                                return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, clrTypeKind);
                            case ClrElementKind.Struct:
                            case ClrElementKind.Object:
                            case ClrElementKind.Class:
                                var dispType = new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, clrTypeKind);
                                var dispFlds = GetClrtDisplayableTypeFields(ndxProxy, heap, dispType, clrType, addr, ambiguousFields);

                                if (ambiguousFields.Count > 0)
                                {
                                    SortedDictionary<string, List<ulong>>[] fldAddrDcts = new SortedDictionary<string, List<ulong>>[ambiguousFields.Count];
                                    for (int j = 0, jcnt = fldAddrDcts.Length; j < jcnt; ++j)
                                    {
                                        fldAddrDcts[j] = new SortedDictionary<string, List<ulong>>(StringComparer.Ordinal);
                                    }
                                    for (int j = 0, jcnt = addresses.Length; j < jcnt; ++j)
                                    {
                                        var jaddr = addresses[j];
                                        try
                                        {
                                            TryGetClrtDisplayableTypeFields(ndxProxy, heap, jaddr, ambiguousFields, dispFlds, fldAddrDcts);
                                        }
                                        catch (Exception ex)
                                        {
                                            error = Utils.GetExceptionErrorString(ex);
                                        }
                                    }
                                    HandleAlternatives(dispType, dispFlds, ambiguousFields, fldAddrDcts);
                                }
                                dispType.AddFields(dispFlds);
                                return dispType;
                            case ClrElementKind.Unknown:
                                continue;
                            default:
                                return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, clrTypeKind);
                        }
                    }
                }
                return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, Constants.NullName, String.Empty, ClrElementKind.Unknown);
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="ndxProxy">Basic indexed dump data.</param>
        /// <param name="heap">MDR's a crash dump heap.</param>
        /// <param name="parent"></param>
        /// <param name="typeId">Type id of the parent's field.</param>
        /// <param name="addresses"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public static ClrtDisplayableType GetClrtDisplayableType(IndexProxy ndxProxy, ClrHeap heap, ClrtDisplayableType parent, int typeId, ulong[] addresses, out string error)
        {
            error = null;
            try
            {
                List<int> ambiguousFields = new List<int>();
                ClrType clrType = null;
                Debug.Assert(!parent.HasFields);

                for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
                {
                    var addr = Utils.RealAddress(addresses[i]);
                    if (Utils.IsInvalidAddress(addr)) continue;
                    clrType = heap.GetObjectType(addr);
                    if (clrType == null) continue;

                    if (clrType.Fields == null || clrType.Fields.Count < 1)
                    {
                        error = Constants.InformationSymbolHeader + "Type '" + clrType.Name + "' has no fields.";
                        return null;
                    }

                    (bool isAmbiguous, ClrElementKind clrTypeKind, ClrType baseType, ClrElementKind baseTypeKind) = IsUndecidedType(clrType);

                    var specKind = TypeExtractor.GetSpecialKind(clrTypeKind);
                    if (specKind != ClrElementKind.Unknown)
                    {
                        switch (specKind)
                        {
                            case ClrElementKind.Interface:
                                throw new MdrException("[TypeExtractor.GetClrtDisplayableType] Interface kind is not expected from ClrHeap.GetHeapObject(...) method.");
                            case ClrElementKind.System__Canon:
                                throw new MdrException("[TypeExtractor.GetClrtDisplayableType] System__Canon kind is not expected from ClrHeap.GetHeapObject(...) method.");
                            case ClrElementKind.Abstract:
                                throw new MdrException("[TypeExtractor.GetClrtDisplayableType] Abstract kind is not expected from ClrHeap.GetHeapObject(...) method.");
                        }
                    }

                    switch (TypeExtractor.GetStandardKind(clrTypeKind))
                    {
                        case ClrElementKind.Struct:
                        case ClrElementKind.Object:
                        case ClrElementKind.Class:
                            var dispFlds = GetClrtDisplayableTypeFields(ndxProxy, heap, parent, clrType, addr, ambiguousFields);
                            if (ambiguousFields.Count > 0)
                            {
                                SortedDictionary<string, List<ulong>>[] fldAddrDcts = new SortedDictionary<string, List<ulong>>[ambiguousFields.Count];
                                for (int j = 0, jcnt = fldAddrDcts.Length; j < jcnt; ++j)
                                {
                                    fldAddrDcts[j] = new SortedDictionary<string, List<ulong>>(StringComparer.Ordinal);
                                }
                                for (int j = 0, jcnt = addresses.Length; j < jcnt; ++j)
                                {
                                    var jaddr = addresses[j];
                                    try
                                    {
                                        TryGetClrtDisplayableTypeFields(ndxProxy, heap, jaddr, ambiguousFields, dispFlds, fldAddrDcts);
                                    }
                                    catch (Exception ex)
                                    {
                                        error = Utils.GetExceptionErrorString(ex);
                                    }
                                }
                                HandleAlternatives(parent, dispFlds, ambiguousFields, fldAddrDcts);
                            }
                            parent.AddFields(dispFlds);
                            return parent;
                        case ClrElementKind.Unknown:
                            continue;
                        default:
                            return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, clrTypeKind);
                    }
                }
                return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, Constants.NullName, String.Empty, ClrElementKind.Unknown);
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
        }



        private static void HandleAlternatives(ClrtDisplayableType parent, ClrtDisplayableType[] fields, List<int> ambiguousFields, SortedDictionary<string, List<ulong>>[] fldAddrDcts)
        {
            List<string> names = new List<string>(8);
            for (int i = 0, icnt = ambiguousFields.Count; i < icnt; ++i)
            {
                var fldNdx = ambiguousFields[i];
                var fld = fields[fldNdx];
                var dct = fldAddrDcts[i];
                if (!fld.HasAlternatives) // not found any instance
                {
                    fld.SetAddresses(Utils.EmptyArray<ulong>.Value);
                    continue;
                }

                if (fld.Alternatives.Length == 1)
                {
                    var alt = fld.Alternatives[0];
                    alt.SetParent(parent);
                    alt.SetAlterntives(null);
                    alt.SetFieldIndex(fld.FieldIndex);
                    alt.SetAddresses(dct[alt.TypeName].ToArray());
                    fields[fldNdx] = alt;
                    continue;
                }
                names.Clear();
                var dummyAry = Utils.EmptyArray<ClrtDisplayableType>.Value;
                var dummy = ClrtDisplayableType.GetDummy(parent, fld.TypeName, fld.FieldName + " ALTERNATIVES", fld.FieldIndex);
                for (int k = 0, kcnt = fld.Alternatives.Length; k < kcnt; ++k)
                {
                    var alt = fld.Alternatives[k];
                    alt.SetParent(dummy);
                    alt.SetAlterntives(dummyAry);
                    alt.SetFieldIndex(fld.FieldIndex);
                    if (!names.Contains(alt.FieldName))
                    {
                        names.Add(alt.FieldName);
                    }
                    alt.SetAddresses(dct[alt.TypeName].ToArray());
                }
                var fldName = string.Join(",", names);
                dummy.SetAlterntives(fld.Alternatives);
                dummy.SetFieldName("{" + fldName + "} alternatives");
                fields[fldNdx] = dummy;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ndxProxy">Basic indexed dump data.</param>
        /// <param name="heap">MDR's a crash dump heap.</param>
        /// <param name="addresses"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        //public static ClrtDisplayableType GetClrtDisplayableType(IndexProxy ndxProxy, ClrHeap heap, ulong[] addresses, out string error)
        //{
        //    error = null;
        //    try
        //    {
        //        List<int> ambiguousFields = new List<int>();
        //        ClrType clrType = null;

        //        for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
        //        {
        //            var addr = Utils.RealAddress(addresses[i]);
        //            if (Utils.IsInvalidAddress(addr)) continue;
        //            clrType = heap.GetObjectType(addr);
        //            if (clrType == null) continue;
        //            var typeId = ndxProxy.GetTypeId(clrType.Name);
        //            var clrTypeKind = GetElementKind(clrType);

        //            //(bool isAmbiguous, ClrElementKind clrTypeKind, ClrType baseType, ClrElementKind baseTypeKind) = IsUndecidedType(clrType);


        //            var specKind = TypeExtractor.GetSpecialKind(clrTypeKind);
        //            if (specKind != ClrElementKind.Unknown)
        //            {
        //                switch (specKind)
        //                {
        //                    case ClrElementKind.Free:
        //                    case ClrElementKind.Guid:
        //                    case ClrElementKind.DateTime:
        //                    case ClrElementKind.TimeSpan:
        //                    case ClrElementKind.Decimal:
        //                        return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, clrTypeKind);
        //                    case ClrElementKind.Interface:
        //                        throw new ApplicationException("Interface kind is not expected from ClrHeap.GetHeapObject(...) method.");
        //                    case ClrElementKind.Enum:
        //                        return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, clrTypeKind);
        //                    case ClrElementKind.System__Canon:
        //                        throw new ApplicationException("System__Canon kind is not expected from ClrHeap.GetHeapObject(...) method.");
        //                    case ClrElementKind.Abstract:
        //                    case ClrElementKind.SystemVoid:
        //                    case ClrElementKind.SystemObject:
        //                        return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, clrTypeKind);
        //                }
        //            }
        //            else
        //            {
        //                switch (TypeExtractor.GetStandardKind(clrTypeKind))
        //                {
        //                    case ClrElementKind.String:
        //                        return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, clrTypeKind);
        //                    case ClrElementKind.SZArray:
        //                    case ClrElementKind.Array:
        //                        return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, clrTypeKind);
        //                    case ClrElementKind.Struct:
        //                    case ClrElementKind.Object:
        //                    case ClrElementKind.Class:
        //                        var dispType = new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, clrTypeKind);
        //                        var dispFlds = GetClrtDisplayableTypeFields(ndxProxy, heap, dispType, clrType, addr, ambiguousFields);
        //                        for (int j = 0, jcnt = addresses.Length; j < jcnt; ++j)
        //                        {
        //                            var jaddr = addresses[j];
        //                            TryGetClrtDisplayableTypeFields(ndxProxy, heap, jaddr, ambiguousFields, dispFlds);
        //                        }
        //                        if (ambiguousFields.Count > 0)
        //                        {
        //                            HandleAlternatives(dispType, dispFlds, ambiguousFields);
        //                        }
        //                        dispType.AddFields(dispFlds);
        //                        return dispType;
        //                    case ClrElementKind.Unknown:
        //                        continue;
        //                    default:
        //                        return new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, clrTypeKind);
        //                }
        //            }
        //        }
        //        return new ClrtDisplayableType(null, Constants.InvalidIndex, Constants.InvalidIndex, Constants.NullName, String.Empty, ClrElementKind.Unknown);
        //    }
        //    catch (Exception ex)
        //    {
        //        error = Utils.GetExceptionErrorString(ex);
        //        return null;
        //    }
        //}


        //public static ClrtDisplayableType GetClrtDisplayableType(IndexProxy ndxProxy, ClrHeap heap, ClrtDisplayableType[] parents, int typeId, ulong[] addresses, out string error)
        //{
        //	error = null;
        //	try
        //	{



        //		List<int> ambiguousFields = new List<int>();
        //		List<ClrType> ambiguousFieldTypes = new List<ClrType>();
        //		KeyValuePair<ClrType, ClrElementKind> typeInfo = new KeyValuePair<ClrType, ClrElementKind>(null, ClrElementKind.Unknown);
        //		for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
        //		{
        //			var addr = addresses[i];
        //			if (Utils.IsInvalidAddress(addr)) continue;

        //			typeInfo = TryGetRealType(heap, addr);
        //			var clrType = typeInfo.Key;
        //			var kind = typeInfo.Value;
        //			var specKind = TypeExtractor.GetSpecialKind(kind);
        //			if (specKind != ClrElementKind.Unknown)
        //			{
        //				switch (specKind)
        //				{
        //					case ClrElementKind.Free:
        //					case ClrElementKind.Guid:
        //					case ClrElementKind.DateTime:
        //					case ClrElementKind.TimeSpan:
        //					case ClrElementKind.Decimal:
        //						return new ClrtDisplayableType(parent, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
        //					case ClrElementKind.Interface:
        //						throw new ApplicationException("Interface kind is not expected from ClrHeap.GetHeapObject(...) method.");
        //					case ClrElementKind.Enum:
        //						return new ClrtDisplayableType(parent, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
        //					case ClrElementKind.System__Canon:
        //						throw new ApplicationException("System__Canon kind is not expected from ClrHeap.GetHeapObject(...) method.");
        //					case ClrElementKind.Exception:
        //					case ClrElementKind.Abstract:
        //					case ClrElementKind.SystemVoid:
        //					case ClrElementKind.SystemObject:
        //						return new ClrtDisplayableType(parent, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
        //				}
        //			}
        //			else
        //			{
        //				switch (TypeExtractor.GetStandardKind(kind))
        //				{
        //					case ClrElementKind.String:
        //						return new ClrtDisplayableType(parent, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
        //					case ClrElementKind.SZArray:
        //					case ClrElementKind.Array:
        //						return new ClrtDisplayableType(parent, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
        //					case ClrElementKind.Struct:
        //					case ClrElementKind.Object:
        //					case ClrElementKind.Class:
        //						var dispType = new ClrtDisplayableType(parent, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
        //						var dispFlds = GetClrtDisplayableTypeFields(ndxProxy, heap, dispType, clrType, addr, ambiguousFields, ambiguousFieldTypes);
        //						for (int j = i + 1, jcnt = addresses.Length; j < jcnt; ++j)
        //						{
        //							var jaddr = addresses[j];
        //							TryGetClrtDisplayableTypeFields(ndxProxy, heap, dispType, clrType, jaddr, ambiguousFields, ambiguousFieldTypes);
        //						}
        //						dispType.AddFields(dispFlds);
        //						return dispType;
        //					case ClrElementKind.Unknown:
        //						continue;
        //					default:
        //						return new ClrtDisplayableType(parent, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind);
        //				}
        //			}
        //		}
        //		return new ClrtDisplayableType(parent, typeId, Constants.InvalidIndex, Constants.NullName, String.Empty, ClrElementKind.Unknown);
        //	}
        //	catch (Exception ex)
        //	{
        //		error = Utils.GetExceptionErrorString(ex);
        //		return null;
        //	}
        //}


        //public static ClrtDisplayableType GetClrtDisplayableTypeFields(IndexProxy ndxProxy, ClrHeap heap, ClrtDisplayableType parent, ulong[] addresses, out string error)
        //      {
        //          error = null;
        //          try
        //          {
        //              List<int> ambiguousFields = new List<int>();
        //              List<ClrType> ambiguousFieldTypes = new List<ClrType>();
        //              KeyValuePair<ClrType, ClrElementKind> typeInfo = new KeyValuePair<ClrType, ClrElementKind>(null, ClrElementKind.Unknown);
        //              for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
        //              {
        //                  var addr = addresses[i];
        //                  if (Utils.IsInvalidAddress(addr)) continue;

        //                  typeInfo = TryGetRealType(heap, addr);
        //                  var clrType = typeInfo.Key;
        //                  var kind = typeInfo.Value;
        //                  var specKind = TypeExtractor.GetSpecialKind(kind);

        //                  {
        //                      switch (TypeExtractor.GetStandardKind(kind))
        //                      {
        //                          case ClrElementKind.Struct:
        //                          case ClrElementKind.Object:
        //                          case ClrElementKind.Class:
        //                              var dispFlds = GetClrtDisplayableTypeFields(ndxProxy, heap, parent, clrType, addr, ambiguousFields, ambiguousFieldTypes);
        //                              var ambiguousCount = ambiguousFields.Count;
        //                              for (int j = i + 1, jcnt = addresses.Length; j < jcnt && ambiguousCount > 0; ++j)
        //                              {
        //                                  var jaddr = addresses[j];
        //                                  TryGetClrtDisplayableTypeFields(ndxProxy, heap, parent, clrType, jaddr, ambiguousFields, ambiguousFieldTypes);
        //                              }
        //                              parent.AddFields(dispFlds);
        //                              return parent;
        //                          case ClrElementKind.Unknown:
        //                              continue;
        //                          default:
        //                              throw new ApplicationException("TypeExtractor.GetClrtDisplayableTypeFields -- ");
        //                      }
        //                  }
        //              }
        //              return parent;
        //          }
        //          catch (Exception ex)
        //          {
        //              error = Utils.GetExceptionErrorString(ex);
        //              return null;
        //          }
        //      }


        public static (ClrType, ClrElementKind) TryGetFieldReferenceType(ClrHeap heap, ulong addr, ClrInstanceField fld, bool intr)
        {
            var valueObj = fld.GetValue(addr, intr, false);
            if (valueObj == null) return (null, ClrElementKind.Unknown);
            if (valueObj is ulong)
            {
                ulong value = (ulong)valueObj;
                if (value==0UL) return (null, ClrElementKind.Unknown);
                var clrType = heap.GetObjectType(value);
                if (clrType != null)
                {
                    var kind = GetElementKind(clrType);
                    return (clrType, kind);
                }
            }
            ulong faddr = fld.GetAddress(addr, true);
            if (faddr != Constants.InvalidAddress)
            {
                var clrType = heap.GetObjectType(faddr);
                var kind = GetElementKind(clrType);
                return (clrType, kind);
            }
            return (null, ClrElementKind.Unknown);
        }

        //public static (ClrType, ClrInstanceField, ulong) GetReferenceTypeField(ClrHeap heap, ClrType clrType, ulong addr, string fldName)
        //{
        //    ClrInstanceField fld = clrType.GetFieldByName(fldName);
        //    var valueObj = fld.GetValue(addr, false);
        //    Debug.Assert(valueObj is ulong);
        //    ulong value = (ulong)valueObj;
        //    var fldType = heap.GetObjectType(value);
        //    return (fldType, fld, value);
        //}
        public static (ClrType, ClrInstanceField, ulong) GetStructTypeField(ClrHeap heap, ClrType clrType, ulong addr, string fldName)
        {
            ClrInstanceField fld = clrType.GetFieldByName(fldName);
            var value = fld.GetAddress(addr, true);
            var fldAddr = ValueExtractor.ReadPointerAtAddress(addr, heap);
            var fldType = heap.GetObjectType(fldAddr);
            return (fldType, fld, fldAddr);
        }

        public static ValueTuple<ClrType, ClrType> GetKeyValuePairTypesByName(ClrHeap heap, string name, string nameBase)
        {
            int baseLen = nameBase.Length;
            string genericStr = name.Substring(baseLen, name.Length - baseLen - 1);
            int comaCount = genericStr.Count(x => x == ',');
            if (comaCount == 1)
            {
                string[] items = genericStr.Split(',');
                return (heap.GetTypeByName(items[0]), heap.GetTypeByName(items[1]));
            }
            (string keyName, string valName) = SplitKeyValuePairTypeNames(genericStr);
            return (heap.GetTypeByName(keyName), heap.GetTypeByName(valName));
        }

        public static ValueTuple<string, string> SplitKeyValuePairTypeNames(string s)
        {
            int firstOpenBracket = s.IndexOf("<");
            int lastCloseBracket = s.LastIndexOf(">");
            int firstComaIndex = s.IndexOf(',');
            int lastComaIndex = s.IndexOf(',');
            if (firstComaIndex < firstOpenBracket) return (s.Substring(firstComaIndex), s.Substring(firstComaIndex + 1));
            if (lastComaIndex > lastCloseBracket) return (s.Substring(lastComaIndex), s.Substring(lastComaIndex + 1));
            int matchCount = 1;
            int bracketNdx = 0;
            for (int i = firstOpenBracket + 1, icnt = s.Length; matchCount > 0 && i < icnt; ++i)
            {
                if (s[i] == '>') { --matchCount; bracketNdx = i; }
                else if (s[i] == '<') ++matchCount;
            }
            firstComaIndex = s.IndexOf(',', bracketNdx);
            return (s.Substring(0, firstComaIndex), s.Substring(firstComaIndex + 1));
        }

    }
}
