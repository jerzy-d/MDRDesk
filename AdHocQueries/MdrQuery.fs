namespace AdHocQueries
[<AutoOpen>]
module Queries =
    open System
    open System.Text
    open System.IO
    open System.Collections.Generic
    open System.Diagnostics
    open Microsoft.Diagnostics.Runtime

    /// <summary>Goes thru all addresses of a runtime heap
    /// and reads object types.</summary>
    /// <param name="runtime">Instance of Microsoft.Diagnostics.RuntimeClrRuntime.</param>
    /// <returns>Error string if fails, null otherwise.</returns>
    let warmupHeap (runtime:ClrRuntime) : (string * address array) =
        let heap = runtime.GetHeap()
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

    let getHeapAddresses (runtime:ClrRuntime) : (string * address array * address array) =
        let heap = runtime.GetHeap()
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

    let getTypeWithMethodTables (runtime:ClrRuntime) =
        let heap = runtime.GetHeap()
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

    let getManagedWorkItem (runtime:ClrRuntime) =
        let heap = runtime.GetHeap()
        try
            let thrdPool = runtime.GetThreadPool()
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
                result.Add (new AddrNameStruct(wi.Object,wi.Type.Name))
            (null,info,result)
        with
            | exn -> (exn.ToString(),null,null)

    let addStrAddrLstDct (dct:IDictionary<string,List<address>>) (str:string) (addr:address) =
        let mutable lst = null
        if dct.TryGetValue(str,&lst) then
            lst.Add(addr)
        else
            lst <- new List<address>()
            lst.Add addr
            dct.Add(str,lst)
        ()


    /// <summary>Goes thru all addresses of a runtime heap
    /// and reads object types.</summary>
    /// <param name="runtime">Instance of Microsoft.Diagnostics.RuntimeClrRuntime.</param>
    /// <returns>Error string if fails, null otherwise.</returns>
    let getStringInstancesInfo (runtime:ClrRuntime) (progress:IProgress<string>): (string * SortedDictionary<string,KeyValuePair<int,int>>) =
        let _64Bit = is64BitDump runtime
        let _ptrSize = ptrSize _64Bit
        let heap = runtime.GetHeap()
        let mutable addrCnt = 0
        try
            let mutable ndx:int32 = 0
            let result = new SortedDictionary<string,KeyValuePair<int,int>>()
            while ndx < heap.Segments.Count do
                let seg = heap.Segments.[ndx]
                let mutable addr = seg.FirstObject;
                while addr <> 0UL do
                    if ((addrCnt%100000) = 0) && not (isNull progress) then
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
                                let lenInBytes = str.Length * 2 + stringBaseSize _64Bit
                                result.Add(str, new KeyValuePair<int,int>(1, roundupToPowerOf2Boundary lenInBytes _ptrSize))
                    addr <- seg.NextObject(addr)
                ndx <- ndx + 1
            (null,result)
        with
            | exn -> (exn.ToString(),null)

    /// <summary>Goes thru all addresses of a runtime heap
    /// and reads object types.</summary>
    /// <param name="runtime">Instance of Microsoft.Diagnostics.RuntimeClrRuntime.</param>
    /// <returns>Error string if fails, null otherwise.</returns>
    let getInterfaceObjects (runtime:ClrRuntime, interfaceName:string) : (string * SortedDictionary<string,List<address>>) =
        let heap = runtime.GetHeap()
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
            | exn -> (exn.ToString(),null)


    let getNamespaceObjects (runtime:ClrRuntime, interfaceName:string) =
        let heap = runtime.GetHeap()
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

    let getObjectValue (heap : ClrHeap) (clrType:ClrType) (typeCats: TypeCategory * TypeCategory) (value : Object) (isinternal:bool) =
            match fst typeCats with
                | TypeCategory.Reference ->
                    let addr = unbox<uint64>(value)
                    match snd typeCats with
                        | TypeCategory.String ->
                            if (addr = 0UL) then nullName
                            else value.ToString()
                        | TypeCategory.Exception ->
                            if (addr = 0UL) then nullName
                            else getExceptionString addr clrType
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
                        | TypeCategory.Decimal -> getDecimalValueStr addr clrType null
                        | TypeCategory.DateTime -> getDateTimeValue addr clrType null
                        | TypeCategory.TimeSpan -> getTimeSpanValue addr clrType
                        | TypeCategory.Guid -> getGuidValue addr clrType
                        | _ -> "struct"
                | TypeCategory.Primitive -> // primitives
                    getPrimitiveValue value clrType.ElementType
                | _ -> "?DON'T KNOW HOW TO GET VALUE?"

    (*
        Arrays.
    *)

    let getArrayValues (heap:ClrHeap) (aryAddr:uint64) (aryType:ClrType) (aryElemType:ClrType) (elemType:ClrType) (count: int32) =
        let mutable ndx:int32 = 0
        let mutable elemAddr:Object = null
        let mutable elemVal:Object = null
        let mutable value:string = null
        let values = new ResizeArray<string>(count)
        let cats = getTypeCategory elemType
        while ndx < count do
            if aryElemType.IsObjectReference then
                elemAddr <- aryType.GetArrayElementValue(aryAddr, ndx)
                elemVal <- elemType.GetValue(unbox<address>(elemAddr))
                value <- getObjectValue heap elemType cats elemVal false
                values.Add (value.ToString())
            else if aryElemType.IsPrimitive then
                elemVal <- aryType.GetArrayElementValue(aryAddr, ndx)
                value <- getObjectValue heap elemType cats elemVal false
                values.Add (value.ToString())
            else
                values.Add("..???..")
            
            ndx <- ndx + 1
        values

    let rec tryGetArrayElemType (heap:ClrHeap) (aryAddr:uint64) (aryType:ClrType) (ndx:int32) (max:int32) =
        if ndx = max then 
            null
        else
            let elemAddr = aryType.GetArrayElementAddress(aryAddr, ndx)
            let elemType = heap.GetObjectType(elemAddr)
            if elemType <> null && not (isTypeUnknown elemType) then
                elemType
            else
                let elemVal = aryType.GetArrayElementValue(aryAddr, ndx)
                if (elemVal <> null) && (elemVal :? address) then
                    let elemType = heap.GetObjectType(unbox<address>(elemVal))
                    if elemType <> null && not (isTypeUnknown elemType) then
                        elemType
                    else
                        tryGetArrayElemType heap aryAddr aryType (ndx+1) max
                else
                    tryGetArrayElemType heap aryAddr aryType (ndx+1) max

    let getArrayElemType (heap:ClrHeap) (aryAddr:uint64) (clrType:ClrType) (ndx:int32) (max:int32) =
        if clrType.ComponentType <> null && not (isTypeUnknown clrType.ComponentType) then
            clrType.ComponentType
        else 
            tryGetArrayElemType heap aryAddr clrType ndx max


    let getArrayContentImpl (runtime:ClrRuntime) (addr:uint64) (aryType:ClrType) : string * string * int32 * string array =
        let heap = runtime.GetHeap()
        let count = aryType.GetArrayLength(addr)
        let aryElemType = getArrayElemType heap addr aryType 0 count
        if aryElemType = null then
            ("Getting type at : " + (getAddrDispStr addr) + " failed, null was returned.", null, count, emptyStringArray)
        else
            let values = getArrayValues heap addr aryType aryType.ComponentType aryElemType count
            (null, aryElemType.Name,count,values.ToArray())

    let getArrayContent (runtime:ClrRuntime) (addr:uint64) : string * string * int32 * string array =
        let heap = runtime.GetHeap()
        let clrType = heap.GetObjectType(addr)
        if (clrType = null) then 
            ("Getting type at : " + (getAddrDispStr addr) + " failed, null was returned.", null, 0, emptyStringArray)
        elif (not clrType.IsArray) then
            ("Type at : " + (getAddrDispStr addr) + " is not an array.", null, 0, emptyStringArray)
        else
            getArrayContentImpl runtime addr clrType 
