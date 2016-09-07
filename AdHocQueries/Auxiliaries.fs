namespace AdHocQueries
[<AutoOpen>]
module Auxiliaries =
    open System
    open System.Text
    open System.IO
    open System.Collections.Generic
    open System.Diagnostics
    open Microsoft.Diagnostics.Runtime

   (*
        usefull defs
    *)
    let emptyStringArray = [||]

    type address = uint64

    let nsPrefix:string = " \u0359 "
    let zeroAddressStr = "0x00000000000000"
    let nullName = "{null}"

    let addressString (addr:address) = String.Format("0x{0:x14}", addr)
    let fullAddressString (addr:address) = String.Format("0x{0:x16}", addr)
    let sortableLengthString (len:address) = String.Format("{0,14:0#,###,###,###}", len)

    let ptrSize is64Bit =  if is64Bit then 8 else 4
    let stringBaseSize is64Bit = if is64Bit then 26 else 14

    /// Convienient categorization of clr types when getting a type instance value.
    type TypeCategory =
        | Uknown = 0
        | Reference = 1
        | Struct = 2
        | Primitive = 3
        | Enum = 4
        | String = 5
        | Array = 6
        | Decimal = 7
        | DateTime = 8
        | TimeSpan = 9
        | Guid = 10
        | Exception = 11
        | SystemObject = 12
        | System__Canon = 13

    type AddrNameStruct =
        struct
            val public Addr: address
            val public Name: String
            new (addr: address, name: String) = { Addr = addr; Name = name}
        end

    /// KeyValuePair<string,address>> comparer.
    type KvStringAddressComparer() =
        interface IComparer<KeyValuePair<string,address>> with
            member x.Compare(a, b) =
                let cmp = String.Compare(a.Key,b.Key)
                if cmp <> 0 then
                    cmp
                else
                    if a.Value < b.Value then
                        -1
                    else
                        if a.Value > b.Value then
                            1
                        else
                            0

    let is64BitDump (runtime:ClrRuntime) = runtime.DataTarget.PointerSize = 8u

    let roundupToPowerOf2Boundary (number:int) (powerOf2:int) =
        (number + powerOf2 - 1) &&& ~~~(powerOf2 - 1)

    /// Convienient categorization of clr types when getting a type instance value.
    let getTypeCategory (clrType:ClrType) =
        if isNull clrType then
            (TypeCategory.Uknown, TypeCategory.Uknown)
        else
            match clrType.ElementType with
                | ClrElementType.String -> (TypeCategory.Reference, TypeCategory.String)
                | ClrElementType.SZArray -> (TypeCategory.Reference, TypeCategory.Array)
                | ClrElementType.Object ->  
                    if clrType.IsException then (TypeCategory.Reference, TypeCategory.Exception)
                    elif clrType.Name = "System.Object" then (TypeCategory.Reference, TypeCategory.SystemObject)
                    elif clrType.Name = "System.__Canon" then (TypeCategory.Reference, TypeCategory.System__Canon)
                    else (TypeCategory.Reference, TypeCategory.Reference)
                | ClrElementType.Struct ->
                    match clrType.Name with
                    | "System.Decimal" -> (TypeCategory.Struct, TypeCategory.Decimal)
                    | "System.DateTime" -> (TypeCategory.Struct, TypeCategory.DateTime)
                    | "System.TimeSpan" -> (TypeCategory.Struct, TypeCategory.TimeSpan)
                    | "System.Guid" -> (TypeCategory.Struct, TypeCategory.Guid)
                    | _ -> (TypeCategory.Struct, TypeCategory.Struct)
                | _ -> (TypeCategory.Primitive, TypeCategory.Primitive)

    /// Format address given unsigned long value.
    let getAddrDispStr (addr : uint64) =
        "0x" + Convert.ToString((int64 addr),16).ToLower()

    /// Format address given boxed unsigned long value.
    let getAddrObjDispStr (addr : Object) =
        "0x" + Convert.ToString((int64 (unbox<uint64>(addr))),16).ToLower()

    let kvStringAddressComparer = new KvStringAddressComparer()

        /// Types which do not tell us much.
    let isTypeNameVague (name:string) =
        name = "System.Object" || name = "System.__Canon"
    
    /// We get get this one, in good heaps. Why?
    let isErrorType (clrType: ClrType) =
        clrType.Name = "ERROR"

    /// Used when we looking for clearly defined type.
    let isTypeUnknown (clrType: ClrType) =
        clrType = null || isErrorType clrType || isTypeNameVague clrType.Name

    let getDispAddress (objAddr: obj) : string =
        if objAddr = null then zeroAddressStr else getAddrObjDispStr objAddr

    let scanSize = 256

    let getFirstIndex (ary:int array) (id:int) (ndx:int) (count:int) =
        if ndx = 0 then
            0
        else if ary.[ndx-1] <> id then
            ndx
        else if ndx = ary.Length - 1 || ary.[ndx+1] <> id then
            ndx - count + 1
        else
            
            let mutable dist = scanSize
            let mutable fst = max 0 (ndx - dist)
            while fst > 0 && ary.[fst] = id do
                dist <- dist * 2
                fst <- max 0 (ndx - dist)
            if fst = 0 && ary.[fst] = id then
                0
            else 
                -1
