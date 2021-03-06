﻿namespace DmpNdxQueries
[<AutoOpen>]
module SpecializedQueries =
    open System
    open System.Text
    open System.IO
    open System.Collections.Generic
    open System.Diagnostics
    open Microsoft.Diagnostics.Runtime
    open ClrMDRIndex

(* description TODO JRD *)

    
    let getAddressFromField (addr:address) (field:ClrInstanceField) (intern:bool): address =
        let valObj = field.GetValue(addr, intern, false)
        if isNull valObj then 0UL else valObj :?> uint64

    let getIntFromField (addr:address) (field:ClrInstanceField) (intern:bool): int32 =
        let valObj = field.GetValue(addr, intern, false)
        if isNull valObj then 0 else valObj :?> int32

    let getCharArray (addr:address) (clrType:ClrType) : char array =
        let aryLen = clrType.GetArrayLength(addr)
        let mutable ary:char array = Array.create aryLen '\u0000'
        for i in [0..aryLen-1] do
            ary.[i] <- unbox<char>(clrType.GetArrayElementValue(addr,i))
        ary

    let getCharArrayWithLenght (addr:address) (clrType:ClrType) (length:int): char array =
        let aryLen = clrType.GetArrayLength(addr)
        let len = min aryLen length
        let mutable ary:char array = Array.create len '\u0000'
        for i in [0..len-1] do
            ary.[i] <- unbox<char>(clrType.GetArrayElementValue(addr,i))
        ary

    let getIntArrayFromType (addr:address) (clrType:ClrType) (intern:bool) =
        Debug.Assert((not (isNull clrType)) && clrType.IsArray)
        let count = clrType.GetArrayLength(addr)
        let mutable ary:string array = Array.create count null
        for i in [0..count-1] do
            ary.[i] <- unbox<int>(clrType.GetArrayElementValue(addr,i)).ToString()
        ary

    //let getKeyValuePairContent (heap:ClrHeap) (addr:address) (clrType:ClrType)

    let getObjectTypeArrayFromType (heap:ClrHeap) (addr:address) (clrType:ClrType) (intern:bool) =
        Debug.Assert((not (isNull clrType)) && clrType.IsArray)
        let count = clrType.GetArrayLength(addr)
        let mutable ary:ClrType array = Array.create count null
        let mutable elemType = null
        for i in [0..count-1] do
            ary.[i] <- 
                let elemObj = clrType.GetArrayElementValue(addr,i)
                let elemAddr = if elemObj = null then 0UL else unbox<uint64>(elemObj)
                match elemAddr with
                | 0UL -> null
                | _ -> if isNull elemType then
                            elemType <- heap.GetObjectType(elemAddr)
                            elemType
                       else
                            elemType
        ary

    (*** System.WeakReference *)

    /// <summary>
    /// Get all references of WeakReference instances.
    /// </summary>
    /// <param name="heap">MDR heap.</param>
    /// <param name="addresses">Addresses of WeakReference instances</param>
    /// <param name="m_handle">Instance of m_handle field.</param>
    /// <param name="m_value">Instance of m_value field (from System.IntPtr type).</param>
    /// <returns></returns>
//    let getWeakReferenceInfos (heap:ClrHeap) (addresses:address array) (m_handle:ClrInstanceField) (m_value:ClrInstanceField): (string*triple<address,address,string>[]) =
//        let typeInfos = ResizeArray<triple<address,address,string>>(addresses.Length)
//        try
//            for flaggedAddr in addresses do
//                let addr = Utils.RealAddress(flaggedAddr)
//                let m_handleObj = m_handle.GetValue(addr, false, false)
//                let m_handleVal = if isNull m_handleObj then 0L else m_handleObj :?> int64
//                let m_valueObj = if m_handleVal = 0L then null else m_value.GetValue(Convert.ToUInt64(m_handleVal), true, false)
//                let m_valueVal = if isNull m_valueObj then 0UL else m_valueObj :?> address
//                let clrType = heap.GetObjectType(m_valueVal) // type pointed to by WeakReference instance at address addr
//                let typeName = if isNull clrType then Constants.Unknown else clrType.Name
//                typeInfos.Add(new triple<address,address,string>(flaggedAddr,m_valueVal,typeName))
//                ()
//            (null, typeInfos.ToArray())
//        with
//            | exn -> (exn.ToString(),null)
//
//    let getWeakReferenceReferences (heap:ClrHeap) (addresses:address array) (m_handle:ClrInstanceField) (m_value:ClrInstanceField) (typeNames:string array): (string*Dictionary<address,KeyValuePair<int,List<address>>>) =
//        let typeInfos = new Dictionary<address,KeyValuePair<int,List<address>>>()
//        try
//            for flaggedAddr in addresses do
//                let addr = Utils.RealAddress(flaggedAddr)
//                let m_handleObj = m_handle.GetValue(addr, false, false)
//                let m_handleVal = if isNull m_handleObj then 0L else m_handleObj :?> int64
//                let m_valueObj = if m_handleVal = 0L then null else m_value.GetValue(Convert.ToUInt64(m_handleVal), true, false)
//                let m_valueVal = if isNull m_valueObj then 0UL else m_valueObj :?> address
//                let (haveIt,haveVal) = typeInfos.TryGetValue(m_valueVal)
//                if haveIt then
//                    haveVal.Value.Add(flaggedAddr)
//                else
//                    let clrType = heap.GetObjectType(m_valueVal) // type pointed to by WeakReference instance at address addr
//                    let typeName = if isNull clrType then Constants.Unknown else clrType.Name
//                typeInfos.Add(new triple<address,address,string>(flaggedAddr,m_valueVal,typeName))
//                ()
//            (null, typeInfos)
//        with
//            | exn -> (exn.ToString(),null)



    (* System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue> *)

    let getConcurrentDictionaryFieldInfo (heap:ClrHeap) (clrType:ClrType) (addr:address) =
        let m_tables = clrType.GetFieldByName("m_tables")
        let m_growLockArray = clrType.GetFieldByName("m_growLockArray")
        let m_growLockArrayVal = ValueExtractor.GetPrimitiveValue(m_growLockArray.GetValue(addr), m_growLockArray.ElementType)
        let m_keyRehashCount = clrType.GetFieldByName("m_keyRehashCount");
        let m_keyRehashCountVal = ValueExtractor.GetPrimitiveValue(m_keyRehashCount.GetValue(addr), m_keyRehashCount.ElementType)
        let m_budget = clrType.GetFieldByName("m_budget");
        let m_budgetVal = ValueExtractor.GetPrimitiveValue(m_budget.GetValue(addr), m_budget.ElementType)
        (m_tables,m_growLockArrayVal,m_keyRehashCountVal,m_budgetVal)

        (*
        private class Tables
        {
            internal readonly Node[] m_buckets; // A singly-linked list for each bucket.
            internal readonly object[] m_locks; // A set of locks, each guarding a section of the table.
            internal volatile int[] m_countPerLock; // The number of elements guarded by each lock.
            internal readonly IEqualityComparer<TKey> m_comparer; // Key equality comparer
 
            internal Tables(Node[] buckets, object[] locks, int[] countPerLock, IEqualityComparer<TKey> comparer)
            {
                m_buckets = buckets;
                m_locks = locks;
                m_countPerLock = countPerLock;
                m_comparer = comparer;
            }
        }
        *)

    let getTablesFieldInfo (heap:ClrHeap) (clrType:ClrType) (addr:address) =
        let m_buckets = clrType.GetFieldByName("m_buckets")
        let m_bucketsAddr = getAddressFromField addr m_buckets false
        let m_bucketsType = heap.GetObjectType(m_bucketsAddr)
        let m_locks = clrType.GetFieldByName("m_locks")
        let m_locksAddr = getAddressFromField addr m_locks false
        let m_locksAddrType = heap.GetObjectType(m_locksAddr)
        let m_locksTypeAry = getObjectTypeArrayFromType heap m_locksAddr m_locksAddrType false
        let m_countPerLock = clrType.GetFieldByName("m_countPerLock");
        let m_countPerLockAddr = getAddressFromField addr m_countPerLock false
        let m_countPerLockType = heap.GetObjectType(m_countPerLockAddr)
        let m_countPerLockAray = getIntArrayFromType m_countPerLockAddr m_countPerLockType false 
        let m_comparer = clrType.GetFieldByName("m_comparer");
        let m_comparerAddr = getAddressFromField addr m_comparer false
        let m_comparerType = heap.GetObjectType(m_comparerAddr)
        (m_bucketsType, m_bucketsAddr, m_locksTypeAry, m_countPerLockAray, m_comparerType)

        (*
        private class Node
        {
            internal TKey m_key;
            internal TValue m_value;
            internal volatile Node m_next;
            internal int m_hashcode;
 
            internal Node(TKey key, TValue value, int hashcode, Node next)
            {
                m_key = key;
                m_value = value;
                m_next = next;
                m_hashcode = hashcode;
            }
        }
        *)

    let getConcurrentDictionaryNodeFields (heap:ClrHeap) (clrType:ClrType) (addr:address) (length:int) =
        let mutable result:address*ClrType*ClrInstanceField*ClrInstanceField*ClrInstanceField = (0UL, null,null,null,null)
        let mutable notDone = true
        let mutable index = 0

        while index < length && notDone do
            let nodeAddrObj = clrType.GetArrayElementValue(addr,index)
            match nodeAddrObj with
                | null -> ()
                | _ ->
                    let nodeAddr = unbox<uint64>(nodeAddrObj)
                    let clrType = heap.GetObjectType(nodeAddr)
                    if isNull clrType then
                        ()
                    else
                        let m_key = clrType.GetFieldByName("m_key")
                        let m_value = clrType.GetFieldByName("m_value")
                        let m_next = clrType.GetFieldByName("m_next")
                        result <- (nodeAddr,clrType,m_next,m_key,m_value)
                        notDone <- false
                    ()
            index <- index + 1
        result

//    let rec getConcurrentDictionaryNodeContent (heap:ClrHeap) (addr:address) (clrType:ClrType) (m_next:ClrInstanceField) (m_key:ClrInstanceField) (m_keyCat:TypeCategories) (m_value:ClrInstanceField) (m_valueCat:TypeCategories) (values:ResizeArray<KeyValuePair<string,string>>) =
//        let m_keyValue = getFieldValue heap m_key m_keyCat addr false
//        let m_valueValue = getFieldValue heap m_value m_valueCat addr false
//        values.Add(new KeyValuePair<string,string>(m_keyValue,m_valueValue))
//        let nextObj = m_next.GetValue(addr)
//        match nextObj with
//        | null -> ()
//        | _ ->
//            let newAddr = unbox<address>(nextObj)
//            if newAddr = 0UL then
//                ()
//            else
//                getConcurrentDictionaryNodeContent heap newAddr clrType m_next m_key m_keyCat m_value m_valueCat values
//        ()
//
//    let getConcurrentDictionaryContent (heap:ClrHeap) (addr:address) =
//        let clrType = heap.GetObjectType(addr)
//        let m_tables, m_growLockArray, m_keyRehashCount, m_budget = getConcurrentDictionaryFieldInfo heap clrType addr
//        let m_tablesAddr = getAddressFromField addr m_tables false
//        let m_tablesType = heap.GetObjectType(m_tablesAddr)
//        let m_bucketsType, m_bucketsAddr, m_locksTypeAry, m_countPerLockAray, m_comparerType = getTablesFieldInfo heap m_tablesType m_tablesAddr
//        let m_bucketsAryLength = m_bucketsType.GetArrayLength(m_bucketsAddr)
//        let mutable values = ResizeArray<KeyValuePair<string,string>>()
//        let nodeAddr, nodeType, m_next, m_key, m_value = getConcurrentDictionaryNodeFields heap m_bucketsType m_bucketsAddr m_bucketsAryLength
//        // check if key and value types are usable
//
//
//
//        let m_keyCategory = getTypeCategory m_key.Type
//        let m_valueCategory = getTypeCategory m_value.Type
//
//        for i in [0..m_bucketsAryLength-1] do
//            getConcurrentDictionaryNodeContent heap addr nodeType m_next m_key m_keyCategory m_value m_valueCategory values
//        ()
//
