
namespace AdHocQueries
[<AutoOpen>]
module ValueExtractor =
    open System
    open System.Text
    open System.IO
    open System.Collections.Generic
    open System.Diagnostics
    open Microsoft.Diagnostics.Runtime


    let getStringAtAddress (addr:address) (heap:ClrHeap) =
        if (addr = 0UL) then 
            null
        else
            let lenBuf = Array.create 4 0uy
            let off = addr + 8UL
            heap.ReadMemory(off,lenBuf,0,4) |> ignore
            let len = BitConverter.ToInt32(lenBuf, 0) * sizeof<char>
            let strBuf = Array.create len 0uy
            heap.ReadMemory(off + 4UL, strBuf, 0, len) |> ignore
            Encoding.Unicode.GetString(strBuf)


    let getPrimitiveValue (objc:Object) (elemType:ClrElementType) : string =
        match elemType with
            | ClrElementType.Float | ClrElementType.Double -> String.Format("0.0000", unbox<double>(objc))
            | ClrElementType.Boolean -> (unbox<bool>(objc)).ToString()
            | ClrElementType.Char -> (unbox<char>(objc)).ToString()
            | ClrElementType.Int8 -> (unbox<byte>(objc)).ToString()
            | ClrElementType.Int16 -> (unbox<int16>(objc)).ToString()
            | ClrElementType.Int32 -> (unbox<int32>(objc)).ToString()
            | ClrElementType.Int64 -> (unbox<int64>(objc)).ToString()
            | ClrElementType.UInt8 -> (unbox<byte>(objc)).ToString()
            | ClrElementType.UInt16 -> (unbox<uint16>(objc)).ToString()
            | ClrElementType.UInt32 -> (unbox<uint32>(objc)).ToString()
            | ClrElementType.UInt64 -> (unbox<uint64>(objc)).ToString()
            | ClrElementType.Pointer -> "pointer: " + String.Format("{0:x12}", unbox<uint64>(objc))
            | ClrElementType.NativeInt -> "native int: " + objc.ToString()
            | ClrElementType.NativeUInt -> "native uint: " + objc.ToString()
            | ClrElementType.String | ClrElementType.FunctionPointer | ClrElementType.Object | ClrElementType.SZArray -> 
                "NOT_PRIMITIVE: " + objc.ToString()
            | ClrElementType.Unknown -> "unknown_type"
            | _ -> "uknown_element_type"

    //
    // System.DateTime
    //

    let ticksMask = 0x3FFFFFFFFFFFFFFFUL

    let getDateTimeValue (addr:address) (tp:ClrType) (formatSpec:string) =
        let mutable data = unbox<address>(tp.Fields.[0].GetValue(addr))
        data <- (data &&& ticksMask)
        let dt = DateTime.FromBinary((int64)data)
        if isNull formatSpec then dt.ToString() else dt.ToString(formatSpec)

    //
    // System.TimeSpan
    //

    let getTimeSpanValue (addr:address) (tp:ClrType) =
        let data = unbox<int64>(tp.Fields.[0].GetValue(addr))
        let ts = TimeSpan.FromTicks(data)
        ts.ToString("c")

    //
    // System.Decimal
    //

    let getFieldDecimalValue (parentAddr:address) (field:ClrInstanceField) =
        let addr = field.GetAddress(parentAddr, true)
        let flags = unbox<int32>(field.Type.Fields.[0].GetValue(addr))
        let hi = unbox<int32>(field.Type.Fields.[1].GetValue(addr))
        let lo = unbox<int32>(field.Type.Fields.[2].GetValue(addr))
        let mid = unbox<int32>(field.Type.Fields.[3].GetValue(addr))
        let bits = [| lo; mid; hi; flags |]
        let d = new Decimal(bits)
        d.ToString()

    let getDecimalValue (addr:address) (tp:ClrType) =
        let flags = unbox<int32>(tp.Fields.[0].GetValue(addr, false))
        let hi = unbox<int32>(tp.Fields.[1].GetValue(addr, false))
        let lo = unbox<int32>(tp.Fields.[2].GetValue(addr, false))
        let mid = unbox<int32>(tp.Fields.[3].GetValue(addr, false))
        let bits = [| lo; mid; hi; flags |]
        new Decimal(bits)
  
    let getDecimalValueStr (addr:address) (tp:ClrType) (formatSpec:string) =
        let d = getDecimalValue addr tp
        if (isNull formatSpec) then d.ToString() else d.ToString(formatSpec)


//    let getGuidValue (addr:address) (tp:ClrType) =
//        let ival  = unbox<int32>(tp.Fields.[0].GetValue(addr))
//        let sval  = unbox<int16>(tp.Fields.[1].GetValue(addr))
//        let sval' = unbox<int16>(tp.Fields.[2].GetValue(addr))
//        let b1 = unbox<byte>(tp.Fields.[3].GetValue(addr))
//        let b2 = unbox<byte>(tp.Fields.[4].GetValue(addr))
//        let b3 = unbox<byte>(tp.Fields.[5].GetValue(addr))
//        let b4 = unbox<byte>(tp.Fields.[6].GetValue(addr))
//        let b5 = unbox<byte>(tp.Fields.[7].GetValue(addr))
//        let b6 = unbox<byte>(tp.Fields.[8].GetValue(addr))
//        let b7 = unbox<byte>(tp.Fields.[9].GetValue(addr))
//        let b8 = unbox<byte>(tp.Fields.[10].GetValue(addr))
//        sprintf "%08x-%04x-%04x-%02x%02x-%02x%02x%02x%02x%02x%02x" ival sval sval' b1 b2 b3 b4 b5 b6 b7 b8

    let getGuidValue (addr:address) (tp:ClrType) =
        sprintf "%08x-%04x-%04x-%02x%02x-%02x%02x%02x%02x%02x%02x" (unbox<int32>(tp.Fields.[0].GetValue(addr)))
                                                                   (unbox<int16>(tp.Fields.[1].GetValue(addr)))
                                                                   (unbox<int16>(tp.Fields.[2].GetValue(addr)))
                                                                   (unbox<byte>(tp.Fields.[3].GetValue(addr)))
                                                                   (unbox<byte>(tp.Fields.[4].GetValue(addr)))
                                                                   (unbox<byte>(tp.Fields.[5].GetValue(addr)))
                                                                   (unbox<byte>(tp.Fields.[6].GetValue(addr)))
                                                                   (unbox<byte>(tp.Fields.[7].GetValue(addr)))
                                                                   (unbox<byte>(tp.Fields.[8].GetValue(addr)))
                                                                   (unbox<byte>(tp.Fields.[9].GetValue(addr)))
                                                                   (unbox<byte>(tp.Fields.[10].GetValue(addr)))

    //
    // System.Exception and derivative
    //

    let getExceptionString (addr:address) (tp:ClrType) =
        let hresult = tp.GetFieldByName("_HResult");
        let hresultValue = unbox<int32>(hresult.GetValue(addr))
        let message = tp.GetFieldByName("_message")
        let messageVal = if isNull message then nullName else message.GetValue(addr, false, true).ToString()
        "[" + hresultValue.ToString() + "] message: " + messageVal

