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
        Arrays.
    *)

    let aryInfo (heap:ClrHeap) (addr:address) : (string * ClrType * ClrType * int * TypeKind) = 
        let clrType = heap.GetObjectType(addr)
        if isNull clrType then
            ("Cannot get type at address: " + Utils.AddressString(addr), null, null, 0, enum<TypeKind> ((int)ClrElementType.Unknown))
        elif not clrType.IsArray then
            ("The type at address: " + Utils.AddressString(addr) + " is not array.", clrType, null, 0, enum<TypeKind> ((int)ClrElementType.Unknown))
        else
            let len = clrType.GetArrayLength(addr)
            let kind = typeKind clrType.ComponentType


            (null, clrType, clrType.ComponentType, len, kind)

    let aryElemString (heap:ClrHeap) (addr:address) (aryType:ClrType) (elemType:ClrType) (ndx:int) =
        let elemAddr = aryType.GetArrayElementAddress(addr,ndx)
        if elemAddr = Constants.InvalidAddress then
            Constants.NullValue
        else
            let raddr = ValueExtractor.ReadUlongAtAddress(elemAddr,heap)
            ValueExtractor.GetStringAtAddress(raddr,heap)

    let aryElemDecimal (heap:ClrHeap) (addr:address) (aryType:ClrType) (elemType:ClrType) (ndx:int) =
        let elemAddr = aryType.GetArrayElementAddress(addr,ndx)
        if elemAddr = Constants.InvalidAddress then
            Constants.NullValue
        else
            ValueExtractor.GetDecimalValueR( elemAddr, elemType,null)

    let aryElemDatetime (heap:ClrHeap) (addr:address) (aryType:ClrType) (elemType:ClrType) (ndx:int) =
        let elemAddr = aryType.GetArrayElementAddress(addr,ndx)
        if elemAddr = Constants.InvalidAddress then
            Constants.NullValue
        else
            ValueExtractor.GetDateTimeValue( elemAddr, elemType,elemType.IsValueClass,null)

    let aryElemDatetimeR (heap:ClrHeap) (addr:address) (aryType:ClrType) (elemType:ClrType) (ndx:int) =
        let elemAddr = aryType.GetArrayElementAddress(addr,ndx)
        if elemAddr = Constants.InvalidAddress then
            Constants.NullValue
        else
            ValueExtractor.GetDateTimeValue(elemAddr, elemType, elemType.IsValueClass,null)

    let aryElemTimespanR (heap:ClrHeap) (addr:address) (aryType:ClrType) (elemType:ClrType) (ndx:int) =
        let elemAddr = aryType.GetArrayElementAddress(addr,ndx)
        if elemAddr = Constants.InvalidAddress then
            Constants.NullValue
        else
            ValueExtractor.GetTimeSpanValue(heap,elemAddr)

    let aryElemGuid (heap:ClrHeap) (addr:address) (aryType:ClrType) (elemType:ClrType) (ndx:int) =
        let elemAddr = aryType.GetArrayElementAddress(addr,ndx)
        if elemAddr = Constants.InvalidAddress then
            Constants.NullValue
        else
            ValueExtractor.GetGuidValue( elemAddr, elemType)

    let aryElemPrimitive (heap:ClrHeap) (addr:address) (aryType:ClrType) (elemType:ClrType) (ndx:int) =
        let elemobj = aryType.GetArrayElementValue(addr,ndx)
        if isNull elemobj then
            Constants.NullValue
        else
            ValueExtractor.GetPrimitiveValue( elemobj, elemType)
    
    let aryElemException (heap:ClrHeap) (addr:address) (aryType:ClrType) (elemType:ClrType) (ndx:int) =
        let elemAddr = unbox<uint64>(aryType.GetArrayElementValue(addr,ndx))
        if elemAddr = Constants.InvalidAddress then
            Constants.NullValue
        else
            ValueExtractor.GetShortExceptionValue(elemAddr, elemType, heap)

    let aryElemAddress (heap:ClrHeap) (addr:address) (aryType:ClrType) (elemType:ClrType) (ndx:int) =
        Utils.RealAddressString(aryType.GetArrayElementAddress(addr,ndx))

    let getAryItems (heap:ClrHeap) (addr:address) (aryType:ClrType) (elemType:ClrType) (cnt:int) getter =
        let ary = Array.create cnt null
        for i = 0 to (cnt - 1) do
            ary.[i] <- getter heap addr aryType elemType i
        ary

    let getAryContent (heap:ClrHeap) (addr:address) : string * ClrType * ClrType * int * string array =
        try
            let error, clrType, aryElemType, count, kind = aryInfo heap addr
            if not (isNull error) then
                (error,null,null,0,null)
            else
                let values = Array.create count null
                match valueKind kind with
                | TypeKind.ValueKind ->
                    match specificKind kind with
                    | TypeKind.Decimal ->
                        (null,clrType,aryElemType,count,getAryItems heap addr clrType aryElemType count aryElemDecimal)
                    | TypeKind.DateTime ->
                        (null,clrType,aryElemType,count,getAryItems heap addr clrType aryElemType count aryElemDatetimeR)
                    | TypeKind.TimeSpan ->
                        (null,clrType,aryElemType,count,getAryItems heap addr clrType aryElemType count aryElemTimespanR)
                    | TypeKind.Guid ->
                        (null,clrType,aryElemType,count,getAryItems heap addr clrType aryElemType count aryElemGuid)
                    | TypeKind.Str ->
                        (null,clrType,aryElemType,count,getAryItems heap addr clrType aryElemType count aryElemString)
                    | TypeKind.Primitive ->
                        (null,clrType,aryElemType,count,getAryItems heap addr clrType aryElemType count aryElemPrimitive)
                    | _ ->
                        (null,clrType,aryElemType,count,null)
                | _ ->
                    match mainKind kind with
                    | TypeKind.ReferenceKind ->
                        match specificKind kind with
                        | TypeKind.Exception ->
                            (null,clrType,aryElemType,count,getAryItems heap addr clrType aryElemType count aryElemException)
                        | _ -> 
                            (null,clrType,aryElemType,count,getAryItems heap addr clrType aryElemType count aryElemAddress)
                    | _ ->
                        (null,clrType,aryElemType,count,null)
        with
            | exn -> (Utils.GetExceptionErrorString(exn),null,null,0,null)

    let getIntAry (heap:ClrHeap) (addr:address) (clrType:ClrType) : string * int array =
        try
            let len = clrType.GetArrayLength(addr)
            let ary : int array = Array.create len 0
            for i = 0 to (len - 1) do
                ary.[i] <- unbox<int32>(clrType.GetArrayElementValue(addr,i))
            (null,ary)
        with
            | exn -> (Utils.GetExceptionErrorString(exn),null)

    (*
        System.Text.StringBuilder TODO JRD add chunk count
    *)

    let rec getStringBuilderArrays (addr:address) (m_ChunkChars:ClrInstanceField) (m_ChunkPrevious:ClrInstanceField) (m_ChunkLength:ClrInstanceField) (chunks:ResizeArray<char[]>) (size:int) : int =
        match addr with
            | 0UL -> size
            | _ ->
                let chunkAddr = getAddressFromField addr m_ChunkChars false
                match chunkAddr with
                    | 0UL -> size
                    | _ ->
                        let usedLength = getIntFromField addr m_ChunkLength false
                        let newSize = size + usedLength
                        let chunkAry = getCharArrayWithLenght chunkAddr m_ChunkChars.Type usedLength
                        chunks.Insert(0,chunkAry)
                        let prevAddr = getAddressFromField addr m_ChunkPrevious false
                        getStringBuilderArrays prevAddr m_ChunkChars m_ChunkPrevious m_ChunkLength chunks newSize


    let getStringBuilderContent (addr:address) (m_ChunkChars:ClrInstanceField) (m_ChunkPrevious:ClrInstanceField) (m_ChunkLength:ClrInstanceField) (m_ChunkOffset:ClrInstanceField) : string =
        let chunks =  ResizeArray<char[]>()
        let totalSize = getStringBuilderArrays addr m_ChunkChars m_ChunkPrevious m_ChunkLength chunks 0
        let mutable strAry = Array.create<char> totalSize '\u0000'
        let mutable offset = 0
        for chunk in chunks do
            Array.Copy(chunk,0,strAry,offset,chunk.Length)
            offset <- offset + chunk.Length
        new string(strAry)
            
    let getStringBuilderString (heap:ClrHeap) (addr:address) =
        let clrType = heap.GetObjectType(addr)
        let m_ChunkChars = clrType.GetFieldByName("m_ChunkChars")
        let m_ChunkPrevious = clrType.GetFieldByName("m_ChunkPrevious")
        let m_ChunkLength = clrType.GetFieldByName("m_ChunkLength")
        let m_ChunkOffset = clrType.GetFieldByName("m_ChunkOffset")
        getStringBuilderContent addr m_ChunkChars m_ChunkPrevious m_ChunkLength m_ChunkOffset

    (*
        System.Collections.Generic.Dictionary<TKey,TValue>
    *)

    let getDictionaryInfo (heap:ClrHeap) (addr:address) (clrType:ClrType) =
        let count = getFieldIntValue heap addr clrType "count"
        let version = getFieldIntValue heap addr clrType "version"
        let freeCount = getFieldIntValue heap addr clrType "freeCount"
        let fldDescription = [|
            new KeyValuePair<string,string>("count",count.ToString())
            new KeyValuePair<string,string>("free count",freeCount.ToString())
            new KeyValuePair<string,string>("version",version.ToString())
            |]
        let entries = clrType.GetFieldByName("entries")
        let entriesAddr = getReferenceFieldAddress addr entries false
        let entriesType = heap.GetObjectType(entriesAddr) // that is address of entries array
        let entryAddr = entriesType.GetArrayElementAddress(entriesAddr,0)
        let entryType = entriesType.ComponentType
        let Entry_hashCodeFld = entryType.GetFieldByName("hashCode")
        let Entry_nextFld = entryType.GetFieldByName("next")
        let Entry_keyFld = entryType.GetFieldByName("key")
        let Entry_valueFld = entryType.GetFieldByName("value")
        let Entry_keyType = tryGetFieldType heap (entryAddr + (uint64)Entry_keyFld.Offset) Entry_keyFld 
        let Entry_valueType = tryGetFieldType heap (entryAddr + (uint64)Entry_valueFld.Offset) Entry_valueFld 
        (fldDescription, count, entriesType, entriesAddr, Entry_hashCodeFld, Entry_nextFld, Entry_keyFld, Entry_keyType, Entry_valueFld, Entry_valueType)

    let dictionaryContent (heap:ClrHeap) (addr:address) =
        try
            let dctType = heap.GetObjectType(addr)
            let fldDescription, count, entriesType, entriesAddr, Entry_hashCodeFld, Entry_nextFld, Entry_keyFld, Entry_keyType, Entry_valueFld, Entry_valueType
                 = getDictionaryInfo heap addr dctType
            let Entry_keyKind = typeKind Entry_keyType
            let Entry_valueKind = typeKind Entry_valueType
            let entryList = new ResizeArray<KeyValuePair<string,string>>(count)
            let mutable index:int32 = 0
            while index < count do
                let entryAddr = entriesType.GetArrayElementAddress(entriesAddr,index)
                let hashCode = getIntValue entryAddr Entry_hashCodeFld true
                if hashCode >= 0 then
                    let keyVal = getFieldValue heap entryAddr true Entry_keyFld Entry_keyKind
                    let valVal = getFieldValue heap entryAddr true Entry_valueFld Entry_valueKind
                    entryList.Add(new KeyValuePair<string,string>(keyVal,valVal))
                index <- index + 1
            (null, fldDescription, count, dctType, Entry_keyType, Entry_valueType, entryList.ToArray())
        with
            | exn -> (Utils.GetExceptionErrorString(exn),null,0,null,null,null,null)


    let getDictionaryCount (heap:ClrHeap) (addr:address) =
        let clrType = heap.GetObjectType(addr)
        if (isNull clrType) then
            ("Cannot get type at address: " + Utils.AddressString(addr), -1)
        else
            let fld = clrType.GetFieldByName("count")
            let count = fld.GetValue(addr,false,false)
            (null,unbox<int>(count))
    
    (*
        System.Collections.Generic.SortedDictionary<TKey,TValue>
        _set fields
        Node root;
        IComparer<T> comparer;
        int count;
        int version;
    *)

    /// return error, description, count, root type, root address, root left field, root right field, key field, value field
    let getSortedDictionaryInfo (heap:ClrHeap) (addr:address) : string * KeyValuePair<string,string> array * int * ClrType * address =
        try
            let error, dctType = getSpecificType heap addr "System.Collections.Generic.SortedDictionary<"
            let dctType = heap.GetObjectType addr
            if isNull dctType then
                (error,null,0,null,0UL)
            else
                let setFld = dctType.GetFieldByName("_set"); // get TreeSet 
                let setFldAddr = unbox<address>(setFld.GetValue(addr))
                let setType = heap.GetObjectType(setFldAddr)
                let count = getFieldIntValue heap setFldAddr setType "count"
                let version = getFieldIntValue heap setFldAddr setType "version"
                
                let description = [|
                        new KeyValuePair<string,string>("Type Name",dctType.Name)
                        new KeyValuePair<string,string>("Count",count.ToString())
                        new KeyValuePair<string,string>("Version",version.ToString())
                        |]
                let rootFld = setType.GetFieldByName("root") // get TreeSet root node
                let rootFldAddr = unbox<address>(rootFld.GetValue(setFldAddr))
                let rootType = heap.GetObjectType(rootFldAddr)
                (null,description,count,rootType,rootFldAddr)
        with
            | exn -> (Utils.GetExceptionErrorString(exn),null,0,null,0UL)

    let getSortedDictionaryItemTypes (heap:ClrHeap) (nodeAddr:address) (itemFld:ClrInstanceField) =
        let keyFld = itemFld.Type.GetFieldByName("key")
        let valFld = itemFld.Type.GetFieldByName("value")
        let itemAddr = itemFld.GetAddress(nodeAddr)
        let keyType =  tryGetType' heap itemAddr true keyFld
        let valType =  tryGetType' heap itemAddr true valFld
        (keyFld, keyType,valFld,valType)

    let getSortedDicionaryContent (heap:ClrHeap) (addr:address) =
        let error,description,count,rootType,rootAddress = getSortedDictionaryInfo heap addr
        let leftNodeFld = rootType.GetFieldByName("Left")
        let rightNodeFld = rootType.GetFieldByName("Right")
        let itemNodeFld = rootType.GetFieldByName("Item")

        let keyFld, keyType,valFld,valType = getSortedDictionaryItemTypes heap rootAddress itemNodeFld

        let keyKind = typeKind keyType
        let valKind = typeKind valType

        let stack = new Stack<address>(2*Utils.Log2(count+1))
        let mutable node = rootAddress
        while node <> Constants.InvalidAddress do
            stack.Push(node)
            let left = getReferenceFieldAddress node leftNodeFld false
            if left <> Constants.InvalidAddress then
                node <- left
            else
                let right = getReferenceFieldAddress node rightNodeFld false
                node <- right
        let values = new ResizeArray<KeyValuePair<string,string>>(count)
        while stack.Count > 0 do
             node <- stack.Pop()

             let itemAddr = itemNodeFld.GetAddress(node)
             let keyStr = getFieldValue heap itemAddr true keyFld keyKind
             let valStr = getFieldValue heap itemAddr true valFld valKind
             values.Add(new KeyValuePair<string,string>(keyStr,valStr))

             node <- getReferenceFieldAddress node rightNodeFld false
             while node <> Constants.InvalidAddress do
                stack.Push(node)
                node <- getReferenceFieldAddress node leftNodeFld false
                if node = Constants.InvalidAddress then
                    node <- getReferenceFieldAddress node rightNodeFld false

        (error,description,values.ToArray())

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

    let getArrayKnownStructElem (heap:ClrHeap) (addr:address) (aryType:ClrType) (elemType:ClrType) (kind:TypeKind) (ndx:int) =
        let elemAddr = aryType.GetArrayElementAddress(addr,ndx)
        if elemAddr = Constants.InvalidAddress then
            Constants.NullValue
        else
             match TypeKinds.GetParticularTypeKind(kind) with
                | TypeKind.DateTime ->
                    ValueExtractor.GetDateTimeValue( elemAddr, elemType, elemType.IsValueClass, null)
                | TypeKind.TimeSpan ->
                    ValueExtractor.GetTimeSpanValue( elemAddr, elemType)
                | TypeKind.Decimal ->
                    ValueExtractor.GetDecimalValue( elemAddr, elemType,null)
                | TypeKind.Guid ->
                    ValueExtractor.GetGuidValue( elemAddr, elemType)
                | _ -> "Don't know how to get value of " + elemType.Name

    let getArrayValues (heap:ClrHeap) (addr:address) (ary:ClrTypeSidekick) : string * int32 * string array =
        let mutable ndx:int32 = 0
        let aryClrType = ary.ClrType
        let count = aryClrType.GetArrayLength(addr)
        let elemInfo = ary.GetField(0); // for arrays first field is element type info
        let mutable value:string = null
        let values = new ResizeArray<string>(count)
        let elemType = elemInfo.ClrType
        let elemKind = elemInfo.Kind
        while ndx < count do
            match TypeKinds.GetMainTypeKind(elemKind) with
            | TypeKind.ReferenceKind ->
                match TypeKinds.GetParticularTypeKind(elemKind) with
                | TypeKind.Str ->
                    value <- getArrayStringElem heap addr aryClrType ndx
                | TypeKind.Exception ->
                    value <- getArrayExceptionElem heap addr aryClrType elemType ndx
                | _ ->
                    value <- getArrayReferenceElem heap addr aryClrType elemType ndx
            | TypeKind.StructKind ->
                match TypeKinds.GetParticularTypeKind(elemKind) with
                | TypeKind.DateTime | TypeKind.TimeSpan | TypeKind.Decimal | TypeKind.Guid ->
                    value <- getArrayKnownStructElem heap addr aryClrType elemType elemKind ndx
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

        (*
        System.Collections.Generic.HashSet<T>
        private int[] m_buckets;
        private Slot[] m_slots;
        private int m_count;
        private int m_lastIndex;
        private int m_freeList;
        private IEqualityComparer<T> m_comparer;
        private int m_version;
        internal struct Slot {
            internal int hashCode;      // Lower 31 bits of hash code, -1 if unused
            internal T value;
            internal int next;          // Index of next entry, -1 if last
        }

        *)
    let getHashValueFieldOffset hashOff nextOff valOff =
        if valOff > 0 then
            valOff
        elif hashOff + nextOff = 4 then
            8
        elif nextOff = 0 || hashOff = 0 then
            4
        else
            0

    let getHashSetInfoDataType  (heap:ClrHeap) (hashSetAddr:address) (addr:address) (slots:ClrType) (lastIndex:int) : ClrInstanceField * ClrInstanceField * ClrType =
        let mutable index = 0;
        let elemType = slots.ComponentType;
        let hashCodeFld = elemType.GetFieldByName("hashCode")
        let valueFld = elemType.GetFieldByName("value")
        let nextFld = elemType.GetFieldByName("next")
        let mutable valType:ClrType = null
        if (isConreteType valueFld.Type) then
            (hashCodeFld,valueFld,valueFld.Type)
        else
            let mt = ValueExtractor.ReadUlongAtAddress(hashSetAddr + (uint64)96, heap);
            valType <- heap.GetTypeByMethodTable(mt)
            if valType=null then
                let mutable notFound = true
                while (notFound && index < lastIndex) do
                    let elemAddr = slots.GetArrayElementAddress(addr,index)

                    let hash = getIntValue elemAddr hashCodeFld true
                    if hash >= 0 then
                        let dataVal = valueFld.GetValue(elemAddr,true)
                        let valAddr = getReferenceFieldAddress elemAddr valueFld true
                        valType <- tryGetType'' heap valAddr true valueFld
                        if valType <> null then
                            notFound <- false
                        else
                            index <- index + 1
            (hashCodeFld,valueFld,valType)

    let getHashSetInfo (heap:ClrHeap) (addr:address) (setType:ClrType) : ClrType * address * ClrInstanceField * ClrInstanceField * ClrType * int * int * KeyValuePair<string,string> array =
        let count = getFieldIntValue heap addr setType "m_count"
        let lastIndex = getFieldIntValue heap addr setType "m_lastIndex"
        let version = getFieldIntValue heap addr setType "m_version"
        let slots = setType.GetFieldByName("m_slots")
        let slotsAddress = getReferenceFieldAddress addr slots false
        let slotsType = heap.GetObjectType slotsAddress
        let slotType = slotsType.ComponentType;
        let hashFld, valFld, valType = if count > 0 then getHashSetInfoDataType  heap addr slotsAddress slotsType lastIndex else (null,null,null)
        

        let slotsCount = slots.Type.GetArrayLength(slotsAddress)
        let description = [|
                            new KeyValuePair<string,string>("Type Name",setType.Name)
                            new KeyValuePair<string,string>("Value Type Name", typeName valType)
                            new KeyValuePair<string,string>("Count",count.ToString())
                            new KeyValuePair<string,string>("Capacity",slotsCount.ToString())
                            new KeyValuePair<string,string>("Version",version.ToString())
                            |]
        
        (slotsType, slotsAddress, hashFld, valFld, valType, lastIndex, count, description)

    let getHashSetContent (heap:ClrHeap) (addr:address) : string * string array * KeyValuePair<string,string> array =
        try
            let setType = heap.GetObjectType(addr)
            if isNull setType then
                ("There is no valid object at address: " + Utils.RealAddressString(addr), null, null)
            elif not (setType.Name.StartsWith("System.Collections.Generic.HashSet<")) then
                ("Expected HashSet<T> type at address: " + Utils.RealAddressString(addr) + ", there's " + setType.Name + " instead.", null, null)
            else
                let slotsType, slotsAddress, hashFld, valFld, valType, lastIndex, count, description = getHashSetInfo heap addr setType

                if count > 0 then
                    let kind = typeKind valType
                    let mutable index = 0
                    let values = new ResizeArray<string>(count)
                    while index < lastIndex do
                        let elemAddr = slotsType.GetArrayElementAddress(slotsAddress,index)
                        let hash = getIntValue elemAddr hashFld true
                        if hash >= 0 then
                            let dataVal = Types.getFieldValue heap elemAddr true valFld kind
                            values.Add(dataVal)
                        index <- index + 1

                    (null,values.ToArray(),description)
                else
                    (null,Utils.EmptyArray<string>.Value,description)
        with
            | exn -> (Utils.GetExceptionErrorString(exn),null,null)


//    let getArrayContent (heap:ClrHeap) (addr:uint64) : string * string * int32 * string array =
//        let error, clrSidekick = Types.getTypeSidekickAtAddress heap  addr
//        if error <> null then 
//            ("Getting type info at : " + Utils.AddressString(addr) + " failed.", null, 0, emptyStringArray)
//        elif (not clrSidekick.IsArray) then
//            ("Type at : " + Utils.AddressString(addr) + " is not an array.", null, 0, emptyStringArray)
//        else
//            let elemName, aryCount, values = getArrayValues heap addr clrSidekick
//            (null, elemName, aryCount, values) 
