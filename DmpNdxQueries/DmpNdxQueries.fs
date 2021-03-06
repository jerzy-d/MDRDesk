﻿namespace DmpNdxQueries
[<AutoOpen>]
module FQry =
    open System
    open System.Text
    open System.IO
    open System.Collections.Generic
    open System.Diagnostics
    open Microsoft.Diagnostics.Runtime
    open ClrMDRIndex

    let addStrAddrLstDct (dct:IDictionary<string,List<address>>) (str:string) (addr:address) =
        let mutable lst = null
        if dct.TryGetValue(str,&lst) then
            lst.Add(addr)
        else
            lst <- new List<address>()
            lst.Add addr
            dct.Add(str,lst)
        ()

    let warmupHeap (runtime:ClrRuntime) : (string * address array) =
        let heap = runtime.Heap
        let nullTypes = ResizeArray<address>()
        try
            let mutable ndx:int32 = 0            
            while ndx < heap.Segments.Count do
                let seg = heap.Segments.[ndx]
                let mutable addr = seg.FirstObject;
                while addr <> 0UL do
                    let clrtype = heap.GetObjectType(addr)
                    if isNull clrtype then nullTypes.Add(addr)
                    addr <- seg.NextObject(addr)
                ndx <- ndx + 1
            (null, nullTypes.ToArray())
        with
            | exn -> (exn.ToString(),nullTypes.ToArray())

    let heapWarmup (heap:ClrHeap) : (string * address array * int) =
        let nullTypes = ResizeArray<address>()
        let mutable count:int32 = 0
        try
            let mutable ndx:int32 = 0
            while ndx < heap.Segments.Count do
                let seg = heap.Segments.[ndx]
                let mutable addr = seg.FirstObject;
                while addr <> 0UL do
                    let clrtype = heap.GetObjectType(addr)
                    if isNull clrtype then nullTypes.Add(addr)
                    addr <- seg.NextObject(addr)
                    count <- count + 1
                ndx <- ndx + 1
            (null, nullTypes.ToArray(),count)
        with
            | exn -> (exn.ToString(),nullTypes.ToArray(),count)

    let getTypeAddresses (heap:ClrHeap) (typeName:string): (string * KeyValuePair<address,uint64> array) =
        let types = ResizeArray<KeyValuePair<address,uint64>>(1024)
        try
            let mutable ndx:int32 = 0            
            while ndx < heap.Segments.Count do
                let seg = heap.Segments.[ndx]
                let mutable addr = seg.FirstObject;
                while addr <> 0UL do
                    let clrtype = heap.GetObjectType(addr)
                    if isNull clrtype then ()
                    elif Utils.SameStrings(typeName, clrtype.Name) then
                        types.Add(new KeyValuePair<address,uint64>(addr,clrtype.MethodTable))
                    else ()
                    addr <- seg.NextObject(addr)
                ndx <- ndx + 1
            (null, types.ToArray())
        with
            | exn -> (exn.ToString(),types.ToArray())

    let getHeapAddresses (heap:ClrHeap) : (string * address array * address array) =
        let nullTypes = ResizeArray<address>()
        let instances = ResizeArray<address>(10000000)
        try
            let mutable ndx:int32 = 0            
            while ndx < heap.Segments.Count do
                let seg = heap.Segments.[ndx]
                let mutable addr = seg.FirstObject;
                while addr <> 0UL do
                    let clrtype = heap.GetObjectType(addr)
                    if isNull clrtype then nullTypes.Add(addr)
                    else instances.Add(addr)
                    addr <- seg.NextObject(addr)
                ndx <- ndx + 1
            (null, instances.ToArray(), nullTypes.ToArray())
        with
            | exn -> (exn.ToString(),instances.ToArray(), nullTypes.ToArray())


    let getStringInstancesInfo (heap:ClrHeap) (progress:IProgress<string>): (string * SortedDictionary<string,KeyValuePair<int,int>>) =
        let mutable addrCnt = 0
        try
            let mutable ndx:int32 = 0
            let result = new SortedDictionary<string,KeyValuePair<int,int>>()
            while ndx < heap.Segments.Count do
                let seg = heap.Segments.[ndx]
                let mutable addr = seg.FirstObject;
                while addr <> 0UL do
                    if not (isNull progress) && ((addrCnt%100000) = 0) then
                        progress.Report(sprintf "GETTING STRINGS. Address count: %i. String count: %i." addrCnt result.Count)
                    addrCnt <- addrCnt + 1 
                    let clrtype = heap.GetObjectType(addr)
                    if not (isNull clrtype) && clrtype.IsString then
                        let str = getStringAtAddress addr heap
                        if not (isNull str) then
                            let (exists,kv) = result.TryGetValue str
                            if exists then
                                result.[str] <- new KeyValuePair<int,int>(kv.Key + 1, kv.Value)
                            else
                                let lenInBytes = clrtype.GetSize(addr)
                                result.Add(str, new KeyValuePair<int,int>(1, (int)lenInBytes))
                    addr <- seg.NextObject(addr)
                ndx <- ndx + 1
            (null,result)
        with
            | exn -> (exn.ToString(),null)

    let getStringCounts (heap:ClrHeap) (progress:IProgress<string>): (string * SortedDictionary<string,int>) =
        let mutable addrCnt = 0
        try
            let mutable ndx:int32 = 0
            let result = new SortedDictionary<string,int>()
            while ndx < heap.Segments.Count do
                let seg = heap.Segments.[ndx]
                let mutable addr = seg.FirstObject;
                while addr <> 0UL do
                    if not (isNull progress) && ((addrCnt%100000) = 0) then
                        progress.Report(sprintf "GETTING STRINGS. Address count: %i. String count: %i." addrCnt result.Count)
                    addrCnt <- addrCnt + 1 
                    let clrtype = heap.GetObjectType(addr)
                    if not (isNull clrtype) && clrtype.IsString then
                        let str = getStringAtAddress addr heap
                        if not (isNull str) then
                            let (exists,cnt) = result.TryGetValue str
                            if exists then
                                result.[str] <- cnt + 1
                            else
                                let lenInBytes = clrtype.GetSize(addr)
                                result.Add(str, 1)
                    addr <- seg.NextObject(addr)
                ndx <- ndx + 1
            (null,result)
        with
            | exn -> (exn.ToString(),null)

    /// <summary>Goes thru all addresses of a runtime heap
    /// and reads object types.</summary>
    /// <param name="runtime">Instance of Microsoft.Diagnostics.RuntimeClrRuntime.</param>
    /// <returns>Error string if fails, null otherwise.</returns>
    let getInterfaceObjects (heap:ClrHeap, interfaceName:string) : (string * SortedDictionary<string,List<address>>) =
         try
            let mutable ndx:int32 = 0
            let result = new SortedDictionary<string,List<address>>()
            while ndx < heap.Segments.Count do
                let seg = heap.Segments.[ndx]
                let mutable addr = seg.FirstObject;
                while addr <> 0UL do
                    let clrtype = heap.GetObjectType(addr)
                    if not (isNull clrtype) then
                        if Seq.exists (fun (intf:ClrInterface) -> String.Equals(intf.Name,interfaceName)) clrtype.Interfaces then
                             addStrAddrLstDct result clrtype.Name addr
                    addr <- seg.NextObject(addr)
                ndx <- ndx + 1
            (null,result)
         with
            | exn -> (Utils.GetExceptionErrorString(exn),null)

    let getObjectsImplementingInterface (heap:ClrHeap, interfaceName:string) : struct (string * SortedDictionary<string,List<address>>) =
         try
            let mutable ndx:int32 = 0
            let result = new SortedDictionary<string,List<address>>()
            while ndx < heap.Segments.Count do
                let seg = heap.Segments.[ndx]
                let mutable addr = seg.FirstObject;
                while addr <> 0UL do
                    let clrtype = heap.GetObjectType(addr)
                    if not (isNull clrtype) then
                        if Seq.exists (fun (intf:ClrInterface) -> String.Equals(intf.Name,interfaceName)) clrtype.Interfaces then
                             addStrAddrLstDct result clrtype.Name addr
                    addr <- seg.NextObject(addr)
                ndx <- ndx + 1
            struct (null,result)
         with
            | exn -> struct (Utils.GetExceptionErrorString(exn),null)


    let getTypeWithMethodTables (heap: ClrHeap) =
        let dct = new SortedDictionary<KeyValuePair<string,address>,int>(kvStringAddressComparer)
        try
            let mutable ndx:int32 = 0            
            while ndx < heap.Segments.Count do
                let seg = heap.Segments.[ndx]
                let mutable addr = seg.FirstObject;
                while addr <> 0UL do
                    let clrtype = heap.GetObjectType(addr)
                    if not (isNull clrtype) then
                        let tbls = clrtype.EnumerateMethodTables()
                        for tbl in tbls do
                            let kv = new KeyValuePair<string,address>(clrtype.Name,tbl)
                            let mutable cnt = 0;
                            let found = dct.TryGetValue(kv, &cnt)
                            if found then
                                dct.[kv] <- cnt + 1
                            else
                                dct.Add(kv,1)
                    addr <- seg.NextObject(addr)
                ndx <- ndx + 1
            (null, dct)
        with
            | exn -> (exn.ToString(),null)



    let getNamespaceObjects (heap:ClrHeap, interfaceName:string) =
        try
            let mutable ndx:int32 = 0
            let dctNsObjects = new SortedDictionary<string,List<address>>()
            let dctNsDerivedObjects = new SortedDictionary<string,List<address>>()
            let dctNsInterfaceObjects = new SortedDictionary<string,List<address>>()
            while ndx < heap.Segments.Count do
                let seg = heap.Segments.[ndx]
                let mutable addr = seg.FirstObject;
                while addr <> 0UL do
                    let clrtype = heap.GetObjectType(addr)
                    if not (isNull clrtype) then
                        if clrtype.Name.StartsWith(interfaceName) then
                            addStrAddrLstDct dctNsObjects clrtype.Name addr
                        else
                            if not (isNull clrtype.BaseType) && clrtype.BaseType.Name.StartsWith interfaceName then
                                addStrAddrLstDct dctNsDerivedObjects clrtype.Name addr
                            elif Seq.exists (fun (intf:ClrInterface) -> intf.Name.StartsWith(interfaceName)) clrtype.Interfaces then
                                addStrAddrLstDct dctNsInterfaceObjects clrtype.Name addr
                    addr <- seg.NextObject(addr)
                ndx <- ndx + 1
            (null,dctNsObjects,dctNsDerivedObjects,dctNsInterfaceObjects)
        with
            | exn -> (exn.ToString(),null,null,null)

    let getManagedWorkItem (runtime:ClrRuntime) =
        let heap = runtime.Heap
        try
            let thrdPool = runtime.ThreadPool
            let info = [|
                    thrdPool.TotalThreads;
                    thrdPool.RunningThreads;
                    thrdPool.IdleThreads;
                    thrdPool.MinThreads;
                    thrdPool.MaxThreads;
                    thrdPool.MinCompletionPorts;
                    thrdPool.MaxCompletionPorts;
                    thrdPool.CpuUtilization;
                    thrdPool.FreeCompletionPortCount;
                    thrdPool.MaxFreeCompletionPorts;
                    thrdPool.RunningThreads|]
            let wrkItems = thrdPool.EnumerateManagedWorkItems()
            let result = new ResizeArray<AddrNameStruct>()
            for wi in wrkItems do
                result.Add (new Auxiliaries.AddrNameStruct(wi.Object,wi.Type.Name))
            (null,info,result)
        with
            | exn -> (exn.ToString(),null,null)

    (*
        Instance hierarchy walk.
    *)

    let (|FieldTypeNull|FieldTypeIsStruct|Other|) (fld:ClrInstanceField) =
        if isNull fld.Type then
            FieldTypeNull
        elif fld.Type.IsValueClass then
            FieldTypeIsStruct
        else
            Other

//    let getInstanceValueFields (ndxInfo:IndexCurrentInfo) (heap:ClrHeap) (clrType:ClrType) (addr:address) (instVal:InstanceValue): string = 
//        let fldCount = fieldCount clrType
//        let internalAddresses = hasInternalAddresses clrType
//        match fldCount with
//        | 0 -> ()
//        | _ ->
//            for fldNdx = 0 to fldCount-1 do
//                let fld = clrType.Fields.[fldNdx]
//                let clrValue = ValueExtractor.TryGetPrimitiveValue(heap, addr, fld, internalAddresses)
//                match Utils.IsNonValue(clrValue) with
//                | true ->
//                    match internalAddresses with
//                    | true ->
//                        let typeName = fieldTypeName fld
//                        let typeId = ndxInfo.GetTypeId(typeName)
//                        instVal.Addvalue(new InstanceValue(typeId, Constants.InvalidAddress, typeName, fld.Name, clrValue))
//                    | _ ->
//                        match fld with
//                        | FieldTypeNull | Other ->
//                            let fldAddr = getReferenceFieldAddress addr fld internalAddresses
//                            let fldTypeInfo = ndxInfo.GetTypeNameAndIdAtAddr(fldAddr)
//                            instVal.Addvalue(new InstanceValue(fldTypeInfo.Value, fldAddr, fldTypeInfo.Key, fld.Name, clrValue))
//                        | FieldTypeIsStruct ->
//                            let fldTypeId = ndxInfo.GetTypeId(fld.Type.Name);
//                            instVal.Addvalue(new InstanceValue(fldTypeId, addr, fld.Type.Name, fld.Name, clrValue, fldNdx))
//                        | _ -> ()
//                | _ ->
//                    let typeName = fieldTypeName fld
//                    let typeId = ndxInfo.GetTypeId(typeName)
//                    instVal.Addvalue(new InstanceValue(typeId, Constants.InvalidAddress, typeName, fld.Name, clrValue))
//        null

    let getInstanceStructValue (ndxProxy:IndexProxy) (heap:ClrHeap) (clrType:ClrType) (kind:TypeKind) (addr:address) (fldNdx:int) : string * InstanceValue * ClrType =
        Debug.Assert(fldNdx <> Constants.InvalidIndex)
        let typeInfo = ndxProxy.GetTypeNameAndIdAtAddr(addr)
        Debug.Assert(TypeKinds.GetMainTypeKind(kind) = TypeKind.StructKind)
        let mutable getFieldsFlag = false;
        let mutable fldName = String.Empty
        let value =
            match TypeKinds.GetParticularTypeKind(kind) with
            | TypeKind.Decimal  -> ValueExtractor.GetDecimalValue(addr,clrType,null)
            | TypeKind.DateTime -> ValueExtractor.GetDateTimeValue(addr,clrType)
            | TypeKind.TimeSpan -> ValueExtractor.GetTimeSpanValue(addr,clrType)
            | TypeKind.Guid     -> ValueExtractor.GetGuidValue(addr,clrType)
            | _ ->
                getFieldsFlag <- true
                fldName <- getFieldName clrType fldNdx
                Constants.NonValue
        (null, new InstanceValue(typeInfo.Value, addr, typeInfo.Key, fldName, value, fldNdx), if getFieldsFlag then clrType else null)


    let getInstanceClassValue (ndxProxy:IndexProxy) (heap:ClrHeap) (clrType:ClrType) (kind:TypeKind) (addr:address) : string * InstanceValue * ClrType =
        let typeInfo = ndxProxy.GetTypeNameAndIdAtAddr(addr)
        match TypeKinds.GetMainTypeKind(kind) with
        | TypeKind.Unknown ->
            ("Type kind (category) is unknown.", null, null)
        | TypeKind.ArrayKind ->
           let acnt = clrType.GetArrayLength(addr)
           let value = "[" + acnt.ToString() + "]"
           (null, new InstanceValue(typeInfo.Value, addr, typeInfo.Key, String.Empty, value), null)
        | TypeKind.StringKind ->
           let strVal = ValueExtractor.GetStringValue(clrType,addr)
           (null, new InstanceValue(typeInfo.Value, addr, typeInfo.Key, String.Empty, strVal), null)
        | TypeKind.ReferenceKind ->
            match TypeKinds.GetParticularTypeKind(kind) with
            | TypeKind.Exception ->
                 let value = ValueExtractor.GetShortExceptionValue(addr, clrType, heap)
                 (null, new InstanceValue(typeInfo.Value, addr, typeInfo.Key, String.Empty, value), clrType)
            | _ -> 
                (null, new InstanceValue(typeInfo.Value, addr, typeInfo.Key, String.Empty, Constants.NonValue), clrType)
        | TypeKind.StructKind ->
            ("Struct types ahould not be handled by getInstanceClassValue.", null, null)
        | TypeKind.PrimitiveKind ->
            let objVal = clrType.GetValue(addr)
            let value = ValueExtractor.GetPrimitiveValue(objVal, clrType)
            (null, new InstanceValue(typeInfo.Value, addr, typeInfo.Key, String.Empty, value), null)
        | _ -> ("Type kind (category) not found.", null, null) // should never happen, for testing and debbuging

    //let getInstanceValueFields (ndxProxy:IndexProxy) (heap:ClrHeap) (clrType:ClrType) (addr:address) (instVal:InstanceValue): string = 
    //    let fldCount = fieldCount clrType
    //    let internalAddresses = hasInternalAddresses clrType
    //    match fldCount with
    //    | 0 -> ()
    //    | _ ->
    //        let fields = ResizeArray<InstanceValue>()
    //        for fldNdx = 0 to fldCount-1 do
    //            let fld = clrType.Fields.[fldNdx]
    //            let fldType = fld.Type
    //            let kind = typeKind fldType
    //            let clrValue = getFieldValue heap addr clrType.IsValueClass fld kind
    //            match Utils.IsNonValue(clrValue) with
    //            | true ->
    //                match internalAddresses with
    //                | true ->
    //                    let typeName = fieldTypeName fld
    //                    let typeId = ndxProxy.GetTypeId(typeName)
    //                    fields.Add(new InstanceValue(typeId, Constants.InvalidAddress, typeName, fld.Name, clrValue))
    //                | _ ->
    //                    match fld with
    //                    | FieldTypeNull | Other ->
    //                        let fldAddr = getReferenceFieldAddress addr fld internalAddresses
    //                        let fldTypeInfo = ndxProxy.GetTypeNameAndIdAtAddr(fldAddr)
    //                        fields.Add(new InstanceValue(fldTypeInfo.Value, fldAddr, fldTypeInfo.Key, fld.Name, clrValue))
    //                    | FieldTypeIsStruct ->
    //                        let fldTypeId = ndxProxy.GetTypeId(fld.Type.Name);
    //                        fields.Add(new InstanceValue(fldTypeId, addr, fld.Type.Name, fld.Name, clrValue, fldNdx))
    //                    | _ -> ()
    //            | _ ->
    //                let typeName = fieldTypeName fld
    //                let typeId = ndxProxy.GetTypeId(typeName)
    //                fields.Add(new InstanceValue(typeId, Constants.InvalidAddress, typeName, fld.Name, clrValue))
    //        instVal.SetFields(fields.ToArray())
    //    null

    //let getInstanceValue (ndxProxy:IndexProxy) (heap:ClrHeap) (decoratedAddr:address) (fldNdx:int) : string * InstanceValue = 
    //    let mutable instVal = null
    //    let mutable instValResult = (null,null,null);
    //    let addr = Utils.RealAddress(decoratedAddr)
    //    let clrType = heap.GetObjectType(addr)
    //    if isNull clrType then
    //        ("Failed to find instance at address: " + Utils.AddressString(addr),null)
    //    else
    //        let kind = typeKind clrType
    //        let mainKind = TypeKinds.GetMainTypeKind(kind)
    //        match mainKind with
    //        | TypeKind.Unknown    -> instValResult <- ("ClrType category is TypeCategory.Unknown, at address: " + Utils.AddressString(addr),null,null)
    //        | TypeKind.ReferenceKind
    //        | TypeKind.StringKind
    //        | TypeKind.ArrayKind
    //        | TypeKind.PrimitiveKind -> instValResult <- getInstanceClassValue ndxProxy heap clrType kind addr
    //        | TypeKind.StructKind    -> instValResult <- getInstanceStructValue ndxProxy heap clrType kind addr fldNdx
    //        | _                      -> instValResult <- ("getInstanceValue doesn't know how to handle: " + kind.ToString(),null,null)
    //        let mutable error, instVal, clrType = instValResult
    //        if isNull error then
    //            match clrType with
    //            | null  -> ()
    //            | _     -> error <- getInstanceValueFields ndxProxy heap clrType addr instVal
    //            (error,instVal)
    //        else
    //            (error, null)

    (*
        Displayable types.
    *)

    //let getDisplayableTypeFields (ndxProxy:IndexProxy) (heap:ClrHeap) (addr: address) (clrType:ClrType) (dispType:ClrtDisplayableType) =
    //    let fldCount = fieldCount clrType
    //    match fldCount with
    //    | 0 -> ()
    //    | _ ->
    //        let mutable dispTypes:ClrtDisplayableType[] = Array.create fldCount null
    //        for fldNdx = 0 to fldCount-1 do
    //            let fld = clrType.Fields.[fldNdx]
    //            let typeName = fieldTypeName fld
    //            let typeId = ndxProxy.GetTypeId(typeName)
    //            let kind = typeKind fld.Type
    //            dispTypes.[fldNdx] <- new ClrtDisplayableType(dispType, typeId, fldNdx, typeName, fld.Name, kind)
    //        dispType.AddFields(dispTypes)

    //let getDisplayableType (ndxProxy:IndexProxy) (heap:ClrHeap) (addresses: address array) =
    //    let addr = addresses.[0]
    //    let clrType = heap.GetObjectType(addr)
    //    let typeId = ndxProxy.GetTypeIdAtAddr(addr)
    //    let kind = typeKind clrType
    //    let dispType = new ClrtDisplayableType(null, typeId, Constants.InvalidIndex, clrType.Name, String.Empty, kind)
    //    getDisplayableTypeFields ndxProxy heap addr clrType dispType
    //    dispType

    //let getDisplayableFieldType (ndxProxy:IndexProxy) (heap:ClrHeap) (dispType:ClrtDisplayableType) (addr: address) (fldndx:int) =
    //    let clrType = heap.GetObjectType(addr)
    //    let fld = getField clrType fldndx
    //    if isNull fld then
    //        ("Cannot get type field at index: " + fldndx.ToString(), null)
    //    else
    //        let count = fieldCount fld.Type
    //        match count with
    //        | 0 ->
    //            if (isFieldTypeNullOrInterface fld) then
    //                ("Field type is null or interface", null)
    //            else
    //                ("Field type does not have fields",null)
    //        | _ ->
    //            let flds = fld.Type.Fields
    //            let mutable dispTypes:ClrtDisplayableType[] = Array.create count null
    //            for fldNdx in [0..count-1] do
    //                let fld = fld.Type.Fields.[fldNdx]
    //                let kind = typeKind fld.Type
    //                let typeId = ndxProxy.GetTypeId(fld.Type.Name)
    //                dispTypes.[fldNdx] <-  new ClrtDisplayableType(dispType,typeId, fldNdx, fld.Type.Name, fld.Name, kind)
    //            (null, dispTypes)

    (*
        Type values data.
    *)

    //let getTypeFieldValue (ndxProxy:IndexProxy) (heap:ClrHeap) (addr:address) (clrType:ClrType) (fld:ClrInstanceField) : (address * obj) =
    //    (0UL,null)

    //let rec getFieldValues (ndxProxy:IndexProxy) (heap:ClrHeap) (clrType:ClrType) (addr:address) (fields:ResizeArray<FieldValue>) =
    //    match fields with
    //    | null -> ()
    //    | _ ->
    //        for fld in fields do
                
    //            let fldAddr, fldValue = getTypeFieldValue ndxProxy heap addr clrType fld.InstField
    //            getFieldValues ndxProxy heap fld.ClType fldAddr fld.Fields
    //        ()

    //let rec acceptFilter (ndxProxy:IndexProxy) (heap:ClrHeap) (clrType:ClrType) (addr:address) (fields:ResizeArray<FieldValue>) (accept:bool) =
    //    match fields with
    //    | null -> accept
    //    | _ ->
    //        let mutable curAccept = accept
    //        for fld in fields do
    //            let fldAddr, fldValue = getTypeFieldValue ndxProxy heap addr clrType fld.InstField
    //            curAccept <- fld.Accept(fldValue,accept)
    //            acceptFilter ndxProxy heap fld.ClType fldAddr fld.Fields curAccept |> ignore
    //        curAccept

    //let rec findFilter (fields:ResizeArray<FieldValue>) (found:bool) =
    //    match found with 
    //    | true -> true
    //    | _ -> 
    //        let mutable curFound = false
    //        for fld in fields do
    //            curFound <- if fld.HasFilter() then true else curFound
    //            match curFound with
    //            | true -> ()
    //            | _ -> if fld.HasFields() 
    //                       then findFilter fld.Fields curFound |> ignore
    //                       else ()
    //        curFound
            
    //let hasFilter' (fields:ResizeArray<FieldValue>) =
    //    findFilter fields false

    //let getTypeValuesAtAddresses (ndxProxy:IndexProxy) (heap:ClrHeap) (clrType:ClrType) (typeValue: TypeValue) (addresses:(address array)) =
    //    let mutable error:string = null
    //    let mutable allWell = true
    //    let mutable ndx:int32 = 0
    //    let fieldValues = typeValue.Fields
    //    let hasFilter = hasFilter' fieldValues
    //    for addr in addresses do
    //        if hasFilter && acceptFilter ndxProxy heap clrType addr typeValue.Fields true then
    //            getFieldValues ndxProxy heap clrType addr typeValue.Fields
    //    ()
