namespace DmpNdxQueries
[<AutoOpen>]
module CollectionContent =
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
        System.Collections.Generic.Dictionary<TKey,TValue>
    *)

    let getDictionaryCount (heap:ClrHeap) (addr:address) =
        let clrType = heap.GetObjectType(addr)
        if (isNull clrType) then
            ("Cannot get type at address: " + Utils.AddressString(addr), -1)
        else
            let fld = clrType.GetFieldByName("count")
            let count = fld.GetValue(addr,false,false)
            (null,unbox<int>(count))

    let getDictionaryEntries (heap:ClrHeap) (addr:address) : string * address * ClrType =
         let dctType = heap.GetObjectType(addr)
         let fld = dctType.GetFieldByName("entries")
         let entrAddr = unbox<address>(fld.GetValue(addr,false,false))
         let entrType = heap.GetObjectType(entrAddr)
         (null,entrAddr,entrType)

    let getDictionaryStringKeys (heap:ClrHeap) (addr:address) (entrAryType:ClrType): string array =
         let count = entrAryType.GetArrayLength(addr)
         let lst = new ResizeArray<string>()
         for i = 0 to count - 1 do
            let elemAddr = entrAryType.GetArrayElementAddress(addr,i)
            let raddr = ValueExtractor.ReadUlongAtAddress(elemAddr,heap)
            if (raddr <> Constants.InvalidAddress) then
                let sval = ValueExtractor.GetStringAtAddress(raddr,heap)
                lst.Add(sval)
            else ()
         lst.ToArray()

    let getEntryStringKeys (heap:ClrHeap) (addr:address) : string array =
         let entrAryType = heap.GetObjectType(addr)
         let count = entrAryType.GetArrayLength(addr)
         let lst = new ResizeArray<string>()
         for i = 0 to count - 1 do
            let elemAddr = entrAryType.GetArrayElementAddress(addr,i)
            let raddr = ValueExtractor.ReadUlongAtAddress(elemAddr,heap)
            if (raddr <> Constants.InvalidAddress) then
                let sval = ValueExtractor.GetStringAtAddress(raddr,heap)
                lst.Add(sval)
            else ()
         lst.ToArray()

    
    (*
        System.Collections.Generic.SortedDictionary<TKey,TValue>
    *)

    /// Get tree (key,value) pairs in inorder traversal
    let rec getNodeValues (heap:ClrHeap) (nodeType:ClrType) (addr:address) (fldNdxs:int32 array) (kvpInfo:KeyValuePairInfo) (keys:ResizeArray<string>) (values:ResizeArray<string>)=
        if addr <> 0UL then
            let lnodeAddr = unbox<address>(nodeType.Fields.[fldNdxs.[0]].GetValue(addr))
            getNodeValues heap nodeType lnodeAddr fldNdxs kvpInfo keys values |> ignore

            let kvpFld = nodeType.Fields.[fldNdxs.[2]]
            let kvpAddr = kvpFld.GetAddress(addr)
            
            let keyVal, valVal = getKeyValuePairValues heap kvpAddr kvpInfo
            keys.Add (keyVal)
            values.Add(valVal)

            let rnodeAddr = unbox<address>(nodeType.Fields.[fldNdxs.[1]].GetValue(addr))
            getNodeValues heap nodeType rnodeAddr fldNdxs kvpInfo keys values |> ignore
            ()
    
    type DictionaryResult =
        struct
            val Error: string
            val DctType: string
            val Address: address
            val KeyType: string
            val ValType: string
            val Keys: string array
            val Values: string array
            new(error, dctType, addr, keyType, valType, keys, values) = { Error = error; DctType = dctType; Address = addr; KeyType = keyType; ValType = valType; Keys = keys; Values = values }
        end

    let getErrorDictionaryResult (error:string) : DictionaryResult =
       new DictionaryResult( error, null, 0UL, null, null, null, null )


    /// 
    let getSortedDictionaryValues (heap:ClrHeap) (addr:address) : DictionaryResult =
        try
            let dctType = heap.GetObjectType addr
            if isNull dctType then
                getErrorDictionaryResult ("Get type returns null at address: " + Utils.AddressString(addr))
            else if not (dctType.Name.StartsWith "System.Collections.Generic.SortedDictionary<") then
                getErrorDictionaryResult ("Expected type: SortedDictionary, but  at address: "+ Utils.AddressString(addr) + " we have: " + dctType.Name)
            else if isNull dctType.Fields || dctType.Fields.Count < 1 then
                getErrorDictionaryResult ("SortedDictionary instance at address: "+ Utils.AddressString(addr) + " does not have fields.")
            else
                let setFld = dctType.GetFieldByName("_set"); // get TreeSet 
                let setFldAddr = unbox<address>(setFld.GetValue(addr))
                let setType = heap.GetObjectType(setFldAddr)
                let rootFld = setType.GetFieldByName("root") // get TreeSet root node
                let rootFldAddr = unbox<address>(rootFld.GetValue(setFldAddr))
                let rootType = heap.GetObjectType(rootFldAddr)
                let fldNdxs = getTypeFieldIndices rootType [|"Left"; "Right"; "Item"|] // get indices of fields we are interested in
                // KeyVsaluePair info
                let kvpFld = rootType.Fields.[fldNdxs.[2]] // this is Item field (KeyValuePair)
                let kvpAddr = kvpFld.GetAddress(rootFldAddr)
                let kFld = kvpFld.Type.GetFieldByName("key")
                let vFld = kvpFld.Type.GetFieldByName("value")
                // get values for type fixup
                let key = kFld.GetValue(kvpAddr,true)
                let value = vFld.GetValue(kvpAddr,true)
                // type fixup
                let kType = tryGetType heap kFld.Type key 
                let vType = tryGetType heap vFld.Type value
                // type categories
                let kTypeCat = getTypeCategory kType
                let vTypeCat = getTypeCategory vType
                let kvpInfo = { KeyValuePairInfo.keyFld = kFld; keyType = kType; keyCat = kTypeCat; valFld = vFld; valType = vType; valCat = vTypeCat; }
                // get keys and values in tree order
                let keys = new ResizeArray<string>()
                let values = new ResizeArray<string>()
                getNodeValues heap rootType rootFldAddr fldNdxs kvpInfo keys values
                new DictionaryResult( null, dctType.Name, addr, kType.Name, vType.Name, keys.ToArray(), values.ToArray() )
        with
            | exn -> getErrorDictionaryResult (exn.ToString())


    (*
        Arrays.
    *)



//    let getArrayValues (heap:ClrHeap) (aryAddr:uint64) (aryType:ClrType) (aryElemType:ClrType) (elemType:ClrType) (count: int32) =
//        let mutable ndx:int32 = 0
//        let mutable elemAddr:Object = null
//        let mutable elemVal:Object = null
//        let mutable value:string = null
//        let values = new ResizeArray<string>(count)
//        let cats = getTypeCategory elemType
//        while ndx < count do
//            if aryElemType.IsObjectReference then
//                elemAddr <- aryType.GetArrayElementValue(aryAddr, ndx)
//                elemVal <- elemType.GetValue(unbox<address>(elemAddr))
//                value <- getObjectValue heap elemType cats elemVal false
//                values.Add (value.ToString())
//            else if aryElemType.IsPrimitive then
//                elemVal <- aryType.GetArrayElementValue(aryAddr, ndx)
//                value <- getObjectValue heap elemType cats elemVal false
//                values.Add (value.ToString())
//            else
//                values.Add("..???..")
//            
//            ndx <- ndx + 1
//        values
//
//    let rec tryGetArrayElemType (heap:ClrHeap) (aryAddr:uint64) (aryType:ClrType) (ndx:int32) (max:int32) =
//        if ndx = max then 
//            null
//        else
//            let elemAddr = aryType.GetArrayElementAddress(aryAddr, ndx)
//            let elemType = heap.GetObjectType(elemAddr)
//            if elemType <> null && not (isTypeUnknown elemType) then
//                elemType
//            else
//                let elemVal = aryType.GetArrayElementValue(aryAddr, ndx)
//                if (elemVal <> null) && (elemVal :? address) then
//                    let elemType = heap.GetObjectType(unbox<address>(elemVal))
//                    if elemType <> null && not (isTypeUnknown elemType) then
//                        elemType
//                    else
//                        tryGetArrayElemType heap aryAddr aryType (ndx+1) max
//                else
//                    tryGetArrayElemType heap aryAddr aryType (ndx+1) max
//
//    let rec tryGetArrayElementType (heap:ClrHeap) (aryAddr:uint64) (aryType:ClrType) (ndx:int32) (max:int32) =
//        if ndx = max then 
//            null
//        else
//            let elemAddr = aryType.GetArrayElementAddress(aryAddr, ndx)
//            let elemType = heap.GetObjectType(elemAddr)
//            if elemType <> null && not (isTypeUnknown elemType) then
//                elemType
//            else
//                let elemVal = aryType.GetArrayElementValue(aryAddr, ndx)
//                if (elemVal <> null) && (elemVal :? address) then
//                    let elemType = heap.GetObjectType(unbox<address>(elemVal))
//                    if elemType <> null && not (isTypeUnknown elemType) then
//                        elemType
//                    else
//                        tryGetArrayElemType heap aryAddr aryType (ndx+1) max
//                else
//                    tryGetArrayElemType heap aryAddr aryType (ndx+1) max
//
//    let getArrayElemType (heap:ClrHeap) (aryAddr:uint64) (clrType:ClrType) (ndx:int32) (max:int32) =
//        if clrType.ComponentType <> null && not (isTypeUnknown clrType.ComponentType) then
//            clrType.ComponentType
//        else 
//            tryGetArrayElemType heap aryAddr clrType ndx max
//
//    
//
//    let getArrayContentImpl (heap:ClrHeap) (addr:uint64) (aryType:ClrType) : string * string * int32 * string array =
//        let count = aryType.GetArrayLength(addr)
//        let aryElemType = getArrayElemType heap addr aryType 0 count
//        if aryElemType = null then
//            ("Getting type at : " + Utils.AddressString(addr) + " failed, null was returned.", null, count, emptyStringArray)
//        else
//            let values = getArrayValues heap addr aryType aryType.ComponentType aryElemType count
//            (null, aryElemType.Name,count,values.ToArray())

    let getArrayStringElem (heap:ClrHeap) (addr:address) (aryType:ClrType) (ndx:int) =
        let elemAddr = aryType.GetArrayElementAddress(addr,ndx)
        if elemAddr = Constants.InvalidAddress then
            Constants.NullValue
        else
            let raddr = ValueExtractor.ReadUlongAtAddress(elemAddr,heap)
            ValueExtractor.GetStringAtAddress(raddr,heap)

    let getArrayExceptionElem (heap:ClrHeap) (addr:address) (aryType:ClrType) (elemType:ClrType) (ndx:int) =
        let elemAddr = aryType.GetArrayElementAddress(addr,ndx)
        if elemAddr = Constants.InvalidAddress then
            Constants.NullValue
        else
            let raddr = ValueExtractor.ReadUlongAtAddress(elemAddr,heap)
            (addressMarkupString raddr) + " " + ValueExtractor.GetExceptionValue(raddr, elemType, heap)

    let getArrayReferenceElem (heap:ClrHeap) (addr:address) (aryType:ClrType) (elemType:ClrType) (ndx:int) =
        let elemAddr = aryType.GetArrayElementAddress(addr,ndx)
        if elemAddr = Constants.InvalidAddress then
            Constants.NullValue
        else
            let raddr = ValueExtractor.ReadUlongAtAddress(elemAddr,heap)
            (addressMarkupString raddr) + " " + elemType.Name

    let getArrayKnownStructElem (heap:ClrHeap) (addr:address) (aryType:ClrType) (elemType:ClrType) (cat:TypeCategory) (ndx:int) =
        let elemAddr = aryType.GetArrayElementAddress(addr,ndx)
        if elemAddr = Constants.InvalidAddress then
            Constants.NullValue
        else
            let raddr = ValueExtractor.ReadUlongAtAddress(elemAddr,heap)
            match cat with
                | TypeCategory.DateTime ->
                    ValueExtractor.GetDateTimeValue( raddr, elemType, null)
                | TypeCategory.TimeSpan ->
                    ValueExtractor.GetTimeSpanValue( raddr, elemType)
                | TypeCategory.Decimal ->
                    ValueExtractor.GetDecimalValue( raddr, elemType, null)
                | TypeCategory.Guid ->
                    ValueExtractor.GetGuidValue( raddr, elemType)
                | _ -> "Don't know how to get value of " + elemType.Name

    let getArrayValues (heap:ClrHeap) (addr:address) (ary:ClrTypeSidekick) : string * int32 * string array =
        let mutable ndx:int32 = 0
        let aryClrType = ary.ClrType
        let count = aryClrType.GetArrayLength(addr)
        let elemInfo = ary.GetField(0); // for arrays first field is element type info
        let mutable value:string = null
        let values = new ResizeArray<string>(count)
        let elemType = elemInfo.ClrType
        let elemCats = elemInfo.Categories
        while ndx < count do
            match elemCats.First with
            | TypeCategory.Reference ->
                match elemCats.Second with
                | TypeCategory.String ->
                    value <- getArrayStringElem heap addr aryClrType ndx
                | TypeCategory.Exception ->
                    value <- getArrayExceptionElem heap addr aryClrType elemType ndx
                | _ ->
                    value <- getArrayReferenceElem heap addr aryClrType elemType ndx
            | TypeCategory.Struct ->
                match elemCats.Second with
                | TypeCategory.DateTime ->
                    value <- getArrayReferenceElem heap addr aryClrType elemType ndx
                | TypeCategory.TimeSpan -> ()
                | TypeCategory.Decimal -> ()
                | TypeCategory.Guid -> ()
                | _ ->
                    if elemType.Name.StartsWith("System.Collections.Generic.Dictionary+Entry") then
                        let elemAddr = aryClrType.GetArrayElementAddress(addr,ndx)
                        let raddr = ValueExtractor.ReadUlongAtAddress(elemAddr,heap)
                        let sval = ValueExtractor.GetStringAtAddress(raddr,heap)

                        let elemFld2Addr = elemAddr + (uint64)elemType.Fields.[1].Offset;
                        let raddr2 = ValueExtractor.ReadUlongAtAddress(elemFld2Addr,heap)
                        let tp = heap.GetObjectType(raddr2)
                        let name = if (isNull tp) then Constants.NullTypeName else tp.Name
                        if (tp<>null) && tp.IsString then
                            let vstr = ValueExtractor.GetStringAtAddress(raddr2,heap)
                            value <- sval.ToString() + Constants.HeavyGreekCrossPadded + name + Constants.HeavyGreekCrossPadded + vstr
                        else
                            value <- sval.ToString() + Constants.HeavyGreekCrossPadded + name
                
            | _ ->
                let elemObj = aryClrType.GetArrayElementValue(addr, ndx)
                value <- ValueExtractor.GetPrimitiveValue(elemObj,elemType.ElementType)
            values.Add(value)
            ndx <- ndx + 1
        (elemType.Name, count, values.ToArray())

    let getArrayContent (heap:ClrHeap) (addr:uint64) : string * string * int32 * string array =
        let error, clrSidekick = Types.getTypeSidekickAtAddress heap  addr
        if error <> null then 
            ("Getting type info at : " + Utils.AddressString(addr) + " failed.", null, 0, emptyStringArray)
        elif (not clrSidekick.IsArray) then
            ("Type at : " + Utils.AddressString(addr) + " is not an array.", null, 0, emptyStringArray)
        else
            let elemName, aryCount, values = getArrayValues heap addr clrSidekick
            (null, elemName, aryCount, values) 
