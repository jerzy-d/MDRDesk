namespace DmpNdxQueries
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
            | exn -> (exn.ToString(),null)

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
                result.Add (new Auxiliaries.AddrNameStruct(wi.Object,wi.Type.Name))
            (null,info,result)
        with
            | exn -> (exn.ToString(),null,null)

(*
    let getDisplayableTypeFields (heap:ClrHeap) (addr: address) (clrType:ClrType) =
        if clrType.Fields.Count == 0 then
            dispType
        else
            for fld in clrType.Fields do
                


    let getDisplayableType (heap:ClrHeap) (addr: address) (typeId:int) (fieldName:string) =
        let clrType = heap.GetObjectType(addr)
        let cat = getTypeCategory clrType
        let dispType = new ClrtDisplayableType(typeId, clrType.Name, fieldName, 
            new KeyValuePair<ValueExtractor.TypeCategory, ValueExtractor.TypeCategory>(fst cat, snd cat))
            *)
            
