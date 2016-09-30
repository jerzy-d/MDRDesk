namespace DmpNdxQueries
[<AutoOpen>]
module Auxiliaries =
    open System
    open System.Text
    open System.IO
    open System.Collections.Generic
    open System.Diagnostics
    open FSharp.Charting
    open FSharp.Charting.ChartTypes
    open Microsoft.Diagnostics.Runtime
    open ClrMDRIndex

   (*
        usefull defs
    *)
    let emptyStringArray = [||]

    type address = uint64

    let nsPrefix:string = " \u0359 "
    let zeroAddressStr = "0x00000000000000"
    let nullName = "{null}"
    let nonValue = "\u2734"

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

    type ClrCategory = TypeCategory * TypeCategory

    type AddrNameStruct =
        struct
            val public Addr: address
            val public Name: String
            new (addr: address, name: String) = { Addr = addr; Name = name}
        end

    type Triple<'A, 'B, 'C> =
        struct
            val mutable public First: 'A
            val mutable public Second: 'B
            val mutable public Third: 'C
            new (first: 'A, second: 'B, third: 'C) = {First = first; Second = second; Third = third }
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
    let getTypeCategory (clrType:ClrType) : ClrCategory =
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


    let getValueExtractorCategory (cat:TypeCategory) : ValueExtractor.TypeCategory =
        let catVal = int cat
        enum<ValueExtractor.TypeCategory>(catVal)

    let convertClrCategoryToValueExtractorPair (cat:ClrCategory) =
        let key = getValueExtractorCategory (fst cat)
        let value = getValueExtractorCategory (snd cat)
        new KeyValuePair<ValueExtractor.TypeCategory,ValueExtractor.TypeCategory>(key,value)

    type ClrtValue =
        | Value of string
        | Values of string array
        | Composite of ClrtValue
        | Composites of ClrtValue array
        | Address of uint64

    type KeyValuePairInfo =
        { keyFld: ClrInstanceField;
          keyType: ClrType;
          keyCat: TypeCategory * TypeCategory;
          valFld: ClrInstanceField;
          valType: ClrType;
          valCat: TypeCategory * TypeCategory; }

    let kvStringAddressComparer = new KvStringAddressComparer()

    let hasInternalAddresses (clrType:ClrType) = clrType.IsValueClass

    let getFieldName (clrType:ClrType) (ndx:int) =
        match clrType with
        | null -> String.Empty
        | _ -> 
            match clrType.Fields with
            | null -> String.Empty
            | _ -> if clrType.Fields.Count > ndx then
                       clrType.Fields.[ndx].Name
                    else
                        String.Empty
    let getField (clrType:ClrType) (ndx:int) =
        match clrType with
        | null -> null
        | _ -> 
            match clrType.Fields with
            | null -> null
            | _ -> if clrType.Fields.Count > ndx then
                       clrType.Fields.[ndx]
                    else
                        null

    let fieldCount (clrType:ClrType) =
        match clrType.Fields with
        | null -> 0
        | _ -> clrType.Fields.Count
        
    let fieldTypeName (fld:ClrInstanceField) =
        match fld.Type with
        | null -> Constants.Unknown
        | _ -> fld.Type.Name
        
    /// Types which do not tell us much.
    let isTypeNameVague (name:string) =
        name = "System.Object" || name = "System.__Canon"
    
    /// We get get this one, in good heaps. Why?
    let isErrorType (clrType: ClrType) =
        clrType.Name = "ERROR"

    let getDispAddress (objAddr: obj) : string =
        if objAddr = null then Constants.ZeroAddressStr else Utils.AddressString(unbox<address>objAddr)


    let getTypeFieldIndices (clrType:ClrType) (fldNames:string array) =
        let indices:int[] = Array.create fldNames.Length (-1)
        for i = 0 to clrType.Fields.Count - 1 do
            let ndx = Array.IndexOf(fldNames, clrType.Fields.[i].Name)
            if ndx >= 0 then
                indices.[ndx] <- i
        indices

    let getReferenceFieldAddress (addr:address) (fld:ClrInstanceField) (intern:bool) =
        let valObj = fld.GetValue(addr,intern,false)
        match valObj with
        | null -> Constants.InvalidAddress
        |_ -> unbox<uint64>(valObj)

    (*
        getting selected field values for a selected type
    *)

    let getFieldValue (heap : ClrHeap) (field : ClrInstanceField) (typeCats: ClrCategory) (classAddr : address) (isinternal:bool) =
        let clrType: ClrType = field.Type
        if clrType = null then 
            "!field-type-null"
        else
            match fst typeCats with
                | TypeCategory.Reference ->
                    let addr = unbox<address>(field.GetValue(classAddr))
                    match snd typeCats with
                        | TypeCategory.String ->
                            if (addr = 0UL) then Constants.NullName
                            else field.GetValue(classAddr,isinternal,true).ToString()
                        | TypeCategory.Exception ->
                            if (addr = 0UL) then Constants.NullName
                            else ValueExtractor.GetShortExceptionValue(addr, field.Type,heap)
                        | TypeCategory.Array -> // get general info only
                            let aryTypeStr = if field.Type.ComponentType = null then "unknown component type" else field.Type.ComponentType.Name
                            let len = clrType.GetArrayLength(addr)
                            getDispAddress addr + " [" + string len + "] : " + aryTypeStr
                        | TypeCategory.SystemObject ->
                            let cT = heap.GetObjectType(addr);
                            if (not (isNull cT) && cT.Name = "System.Decimal") then
                                ValueExtractor.GetDecimalValue(addr, cT, null)
                            else
                                getDispAddress addr
                        | TypeCategory.Reference ->
                            getDispAddress addr
                        | _ ->
                            getDispAddress addr + " What the heck is this?"
                | TypeCategory.Struct ->
                    let addr = field.GetAddress(classAddr, clrType.IsValueClass)
                    match snd typeCats with
                        | TypeCategory.Decimal -> ValueExtractor.GetDecimalValue(addr,field.Type,null)
                        | TypeCategory.DateTime -> ValueExtractor.GetDateTimeValue(addr,field.Type)
                        | TypeCategory.TimeSpan -> ValueExtractor.GetTimeSpanValue(addr,field.Type)
                        | TypeCategory.Guid -> ValueExtractor.GetGuidValue(addr,field.Type)
                        | _ -> "struct"
                | TypeCategory.Primitive -> // primitives
                   let o = field.GetValue(classAddr,clrType.IsValueClass)
                   ValueExtractor.GetPrimitiveValue(o, field.Type)
                | _ -> "?DON'T KNOW HOW TO GET VALUE?"

    let getObjectValue (heap : ClrHeap) (clrType:ClrType) (typeCats: ClrCategory) (value : Object) (isinternal:bool) =
            match fst typeCats with
                | TypeCategory.Reference ->
                    let addr = unbox<uint64>(value)
                    match snd typeCats with
                        | TypeCategory.String ->
                            if (addr = 0UL) then Constants.NullName
                            else ValueExtractor.GetStringValue(clrType, addr)
                        | TypeCategory.Exception ->
                            if (addr = 0UL) then Constants.NullName
                            else ValueExtractor.GetShortExceptionValue(addr, clrType, heap)
                        | TypeCategory.Array -> // get general info only
//                            let aryTypeStr = if field.Type.ComponentType = null then "unknown component type" else field.Type.ComponentType.Name
//                            let len = clrType.GetArrayLength(addr)
                            getDispAddress addr + " [" + "?" + "] : "
                        | TypeCategory.SystemObject ->
                            getDispAddress addr
                        | TypeCategory.Reference ->
                            getDispAddress addr
                        | _ ->
                            getDispAddress addr + " What the heck is this?"
                | TypeCategory.Struct ->
                    let addr = unbox<uint64>(value)
                    match snd typeCats with
                        | TypeCategory.Decimal -> ValueExtractor.GetDecimalValue(addr,clrType,null)
                        | TypeCategory.DateTime -> ValueExtractor.GetDateTimeValue(addr,clrType)
                        | TypeCategory.TimeSpan -> ValueExtractor.GetTimeSpanValue(addr,clrType)
                        | TypeCategory.Guid -> ValueExtractor.GetGuidValue(addr,clrType)
                        | _ -> "struct"
                | TypeCategory.Primitive -> // primitives
                   ValueExtractor.GetPrimitiveValue(value, clrType)
                | _ -> "?DON'T KNOW HOW TO GET VALUE?"

    let tryGetPrimitiveValue (heap:ClrHeap) (classAddr:address) (field : ClrInstanceField) (internalAddr: bool) =
        let clrType = field.Type
        let cat = getTypeCategory clrType
        match fst cat with
                | TypeCategory.Reference ->
                    match snd cat with
                        | TypeCategory.String ->
                            let addr = unbox<address>(field.GetValue(classAddr, internalAddr,false))
                            if (addr = 0UL) then Constants.NullName
                            else ValueExtractor.GetStringValue(clrType, addr)
                        | TypeCategory.Exception ->
                            let addr = unbox<address>(field.GetValue(classAddr, internalAddr,false))
                            if (addr = 0UL) then Constants.NullName
                            else ValueExtractor.GetShortExceptionValue(addr, clrType, heap)
                        | _ ->
                            nonValue
                | TypeCategory.Struct ->
                    match snd cat with
                        | TypeCategory.Decimal -> 
                            let addr = unbox<address>(field.GetValue(classAddr, internalAddr,false))
                            ValueExtractor.GetDecimalValue(addr,clrType,null)
                        | TypeCategory.DateTime -> 
                            let addr = unbox<address>(field.GetValue(classAddr, internalAddr,false))
                            ValueExtractor.GetDateTimeValue(addr,clrType)
                        | TypeCategory.TimeSpan -> 
                            let addr = unbox<address>(field.GetValue(classAddr, internalAddr,false))
                            ValueExtractor.GetTimeSpanValue(addr,clrType)
                        | TypeCategory.Guid -> 
                            let addr = unbox<address>(field.GetValue(classAddr, internalAddr,false))
                            ValueExtractor.GetGuidValue(addr,clrType)
                        | _ -> nonValue
                | TypeCategory.Primitive -> // primitives
                   let valObj = field.GetValue(classAddr)
                   ValueExtractor.GetPrimitiveValue(valObj, clrType)
                | _ -> nonValue

    let getFieldVal (heap:ClrHeap) (parentAddr:address) (fld:ClrInstanceField) (fldType:ClrType) (fldCat:ClrCategory) (isInternal:bool) =
        let value = fld.GetValue(parentAddr,isInternal)
        getObjectValue heap fldType fldCat value isInternal

    let getKeyValuePairValues (heap:ClrHeap) (kvpAddr:address) (kvpInfo:KeyValuePairInfo) = 
        let keyStr = getFieldVal heap kvpAddr kvpInfo.keyFld kvpInfo.keyType kvpInfo.keyCat true
        let valStr = getFieldVal heap kvpAddr kvpInfo.valFld kvpInfo.valType kvpInfo.valCat true
        (keyStr,valStr)



    /// Used when we looking for clearly defined type.
    let isTypeUnknown (clrType: ClrType) =
        clrType = null || isErrorType clrType || isTypeNameVague clrType.Name

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

    let getStringAtAddress (addr:address) (heap:ClrHeap) =
        if (addr = 0UL) then 
            null
        else
            let lenBuf = Array.create 4 0uy
            let off = addr + (uint64 IntPtr.Size)
            heap.ReadMemory(off,lenBuf,0,4) |> ignore
            let len = BitConverter.ToInt32(lenBuf, 0) * sizeof<char>
            let strBuf = Array.create len 0uy
            heap.ReadMemory(off + 4UL, strBuf, 0, len) |> ignore
            Encoding.Unicode.GetString(strBuf)

 

    let getErrorString (errors: ResizeArray<string>) : string =
        if not (isNull errors) && errors.Count > 0 then
            let sb = new StringBuilder(128)
            errors.[0] |> sb.Append |> ignore
            for i = 1 to errors.Count do
                sb.Append(" || ").Append(errors.[i]) |> ignore
            sb.ToString()
        else
            null

    let tryGetType (heap:ClrHeap) (clrType:ClrType) (obj:Object) =
        if isTypeNameVague clrType.Name then
            try
                let objAsAddr = unbox<uint64>(obj)
                let aType =  heap.GetObjectType(objAsAddr)
                if isNull aType then
                    clrType
                else
                    aType
            with
                | exn -> clrType
        else
            clrType

    (*
        Charts
    *)

    let getColumnChart (data:(string*int64) array) =
        let chart = Chart.Column data
        new ChartControl(chart)

    let getColumnChartWithTitle (data:(string*int64) array) (title:string) =
        let chart = Chart.Column (data, Title=title)
        new ChartControl(chart)
