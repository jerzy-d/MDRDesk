﻿namespace DmpNdxQueries
[<AutoOpen>]
module Types =
    open System
    open System.Text
    open System.IO
    open System.Collections.Generic
    open System.Diagnostics
    open FSharp.Charting
    open FSharp.Charting.ChartTypes
    open Microsoft.Diagnostics.Runtime
    open ClrMDRIndex

    let typeKind (clrType:ClrType) : TypeKind =
        let elemType = clrType.ElementType
        let kind = TypeKinds.SetClrElementType(TypeKind.Unknown, elemType)
        match elemType with
        | ClrElementType.Array   -> kind ||| TypeKind.ArrayKind
        | ClrElementType.SZArray -> kind ||| TypeKind.ArrayKind
        | ClrElementType.String  -> kind ||| TypeKind.StringKind ||| TypeKind.ValueKind ||| TypeKind.Str
        | ClrElementType.Object  ->
            let subKind = kind ||| TypeKind.ReferenceKind
            if clrType.IsException then
                subKind ||| TypeKind.Exception;
            else
                match clrType.Name with
                | "System.Object"  -> subKind ||| TypeKind.Object
                | "System.__Canon" -> subKind ||| TypeKind.System__Canon
                | _                -> subKind
        | ClrElementType.Struct  ->
            let subKind = kind ||| TypeKind.StructKind
            match clrType.Name with
            | "System.Decimal"   -> subKind ||| TypeKind.Decimal ||| TypeKind.ValueKind
            | "System.DateTime"  -> subKind ||| TypeKind.DateTime ||| TypeKind.ValueKind
            | "System.TimeSpan"  -> subKind ||| TypeKind.TimeSpan ||| TypeKind.ValueKind
            | "System.Guid"      -> subKind ||| TypeKind.Guid ||| TypeKind.ValueKind
            | _                  -> subKind
        | ClrElementType.Unknown -> TypeKind.Unknown
        | _                      -> kind ||| TypeKind.PrimitiveKind ||| TypeKind.ValueKind  ||| TypeKind.Primitive;

    let clrElementType kind : ClrElementType = TypeKinds.GetClrElementType(kind)
    let valueKind kind = TypeKinds.GetValueTypeKind(kind)
    let specificKind kind = TypeKinds.GetParticularTypeKind(kind)

    let typeDefaultValue (clrType:ClrType) : string =
        let elemType = clrType.ElementType
        match elemType with
        | ClrElementType.Array | ClrElementType.SZArray ->
            "[0]/null"
        | ClrElementType.String ->
            "\"\"/null"
        | ClrElementType.Object ->
            "null"
        | ClrElementType.Struct  ->
             match clrType.Name with
            | "System.Decimal"   -> "0"
            | "System.DateTime"  -> "< 01/01/1800"
            | "System.TimeSpan"  -> "0"
            | "System.Guid"      -> "00000000-0000-0000-0000-000000000000"
            | _                  -> "empty"
        | ClrElementType.Unknown -> "unknown"
        | _                      -> "0"

    let getTypeValue (heap:ClrHeap) (addr:address) (clrType:ClrType) (kind:TypeKind) : string =
        match valueKind kind with
        | TypeKind.ValueKind ->
            let specKind = specificKind kind
            match specificKind kind with
            | TypeKind.Decimal ->
                Constants.NullValue
            | TypeKind.DateTime ->
                Constants.NullValue
            | TypeKind.TimeSpan ->
                Constants.NullValue
            | TypeKind.Guid ->
                Constants.NullValue
            | TypeKind.Str ->
                Constants.NullValue
            | TypeKind.Primitive ->
                Constants.NullValue
            | _ ->
                Constants.NullValue
        | _ ->
            Constants.NullValue

    let getFieldValue (heap:ClrHeap) (addr:address) (intr:bool) (fld:ClrInstanceField) (kind:TypeKind) : string =
        match valueKind kind with
        | TypeKind.ValueKind ->
            let specKind = specificKind kind
            match specificKind kind with
            | TypeKind.Decimal ->
                ValueExtractor.GetDecimalValue(addr, fld)
            | TypeKind.DateTime ->
                Constants.NullValue
            | TypeKind.TimeSpan ->
                Constants.NullValue
            | TypeKind.Guid ->
                Constants.NullValue
            | TypeKind.Str ->
                let fldAddr = unbox<uint64>(fld.GetValue(addr,intr,false))
                ValueExtractor.GetStringAtAddress(fldAddr,heap)
            | TypeKind.Primitive ->
                let obj = fld.GetValue(addr,intr,false)
                let elemType = clrElementType kind
                ValueExtractor.GetPrimitiveValue(obj, elemType)
            | _ ->
                Constants.NullValue
        | _ ->
            Constants.NullValue

    let getObjectType (heap:ClrHeap) (addr:address) = 
        let clrType = heap.GetObjectType(addr)
        let kind = typeKind clrType
        match TypeKinds.GetMainTypeKind(kind) with
        | TypeKind.Unknown ->
            EmptyClrTypeSidekick.Value
        | TypeKind.ReferenceKind ->
            match TypeKinds.GetParticularTypeKind(kind) with
            | TypeKind.Str 
            | TypeKind.Exception
            | TypeKind.Ary ->
                new ClrTypeSidekick(clrType,kind,null)
            | TypeKind.SystemObject ->
                new ClrTypeSidekick(clrType,kind,null)
            | TypeKind.System__Canon ->
                new ClrTypeSidekick(clrType,kind,null)
            | _ ->
                getReferenceFields heap addr (new ClrTypeSidekick(clrType,kind,null))
        | TypeKind.Struct ->
            match TypeKinds.GetParticularTypeKind(kind) with
            | TypeKind.DateTime | TypeKind.Guid | TypeKind.TimeSpan | TypeKind.Decimal ->
                new ClrTypeSidekick(clrType,kind,null)
            | _ -> 
                getStructgFields heap addr (new ClrTypeSidekick(clrType,kind,null))
        | _ ->
            EmptyClrTypeSidekick.Value

    /// We get get this one, in good heaps. Why?
    let isErrorType (clrType: ClrType) =
        Utils.SameStrings(clrType.Name,"ERROR")

    /// <summary>
    /// Some reference types have names which might require further investigation.
    /// </summary>
    /// <param name="name">Type name.</param>
    let isTypeNameVague (name:string) =
        Utils.SameStrings(name,"System.Object") || Utils.SameStrings(name,"System.__Canon")

    /// Used when we looking for clearly defined type.
    let isTypeUnknown (clrType: ClrType) =
        clrType = null || isErrorType clrType || isTypeNameVague clrType.Name

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

    let tryGetType' (heap:ClrHeap) (addr:address) (intr:bool) (fld:ClrInstanceField) =
        if fld.IsValueClass then
            fld.Type
        else
            let valueObj = fld.GetValue(addr,intr)
            tryGetType heap fld.Type valueObj

    ///
    let tryGetFieldType (heap:ClrHeap) (addr:address) (fld:ClrInstanceField) =
       if isNull fld.Type || isTypeNameVague fld.Type.Name then
          let fldAddr = ValueExtractor.ReadPointerAtAddress(addr,heap)
          heap.GetObjectType(fldAddr)
       else
           fld.Type

    let private getStructTypeFieldSidekick (heap:ClrHeap) (clrs:ClrTypeSidekick) (addr:address) (fld:ClrInstanceField) =
        let clrType = fld.Type
        let kind = TypeKinds.GetTypeKind(fld.Type)
        if  kind = TypeKind.Unknown then
            ()
        else
            match TypeKinds.GetMainTypeKind(kind) with
            | TypeKind.ReferenceKind ->
                match TypeKinds.GetParticularTypeKind(kind) with
                | TypeKind.System__Canon | TypeKind.SystemObject ->
                    let fldAddr = fld.GetAddress(addr,true)
                    let paddr = ValueExtractor.ReadPointerAtAddress(fldAddr,heap)
                    let fldType = heap.GetObjectType(paddr)
                    let fldCats = getTypeCategory fldType
                    clrs.AddField(new ClrTypeSidekick(fldType,kind,fld))
                | _ ->
                    clrs.AddField(new ClrTypeSidekick(clrType,kind,fld))
            | TypeKind.Struct ->
                ()
            | _ ->
                clrs.AddField(new ClrTypeSidekick(clrType,kind,fld))
                ()

            ()

    let getArrayElementAddress (aryType:ClrType) (addr:address) (aryLen:int) =
        let mutable ndx:int = 0
        let mutable elemAddr = 0UL
        let mutable notDone:bool = true
        while notDone && ndx < aryLen do
            elemAddr <- aryType.GetArrayElementAddress(addr,ndx)
            if (elemAddr <> 0UL) then
                notDone <- false
            else
                ndx <- ndx + 1
        elemAddr
                

    let private getArrayElementTypeSidekick (heap:ClrHeap) (clrs:ClrTypeSidekick) (addr:address) =
        let aryType = clrs.ClrType
        let kind = clrs.Kind
        let aryLen = aryType.GetArrayLength(addr)
        clrs.SetData(aryLen)
        let aryCompType = aryType.ComponentType
        let aryCompKind = TypeKinds.GetTypeKind(aryCompType)
        match TypeKinds.GetMainTypeKind(aryCompKind) with
        | TypeKind.Unknown -> ()
        | TypeKind.ReferenceKind ->
            match TypeKinds.GetParticularTypeKind(aryCompKind) with
            | TypeKind.Ary ->
                let aryElemAddr = getArrayElementAddress aryType addr aryLen
                let aryElemType = heap.GetObjectType(aryElemAddr)
                if aryElemType <> null then
                    let aryElemKind = TypeKinds.GetTypeKind(aryElemType)
                    let aryElemLen = aryElemType.GetArrayLength(aryElemAddr)
                    let aryElemSidekick = new ClrTypeSidekick(aryElemType,aryElemKind)
                    aryElemSidekick.SetData(aryElemLen)
                    clrs.AddField(aryElemSidekick)
                else
                    let aryElemSidekick = new ClrTypeSidekick(aryCompType,aryCompKind)
                    aryElemSidekick.SetData(-1) // unable to get internal array length
                    clrs.AddField(aryElemSidekick)
            | TypeKind.System__Canon | TypeKind.SystemObject ->
                let aryElemAddr = getArrayElementAddress aryType addr aryLen
                let aryElemType = heap.GetObjectType(aryElemAddr)
                if aryElemType <> null then
                    let aryElemKind = TypeKinds.GetTypeKind(aryElemType)
                    let aryElemSidekick = new ClrTypeSidekick(aryElemType,aryElemKind)
                    clrs.AddField(aryElemSidekick)
                else
                    let aryElemSidekick = new ClrTypeSidekick(aryCompType,aryCompKind)
                    clrs.AddField(aryElemSidekick)
            | _ ->
                clrs.AddField(new ClrTypeSidekick(aryCompType,aryCompKind))
        | TypeKind.StructKind ->
            match TypeKinds.GetParticularTypeKind(aryCompKind) with
            | TypeKind.DateTime | TypeKind.Decimal | TypeKind.Guid | TypeKind.TimeSpan ->
                 clrs.AddField(new ClrTypeSidekick(aryCompType,aryCompKind))
            | _ ->
                let aryElemAddr = getArrayElementAddress aryType addr aryLen
                let arySidekick = new ClrTypeSidekick(aryCompType,aryCompKind)
                clrs.AddField(arySidekick)
                for i = 0 to aryCompType.Fields.Count - 1 do
                    let fld = aryCompType.Fields.[i]
                    getStructTypeFieldSidekick heap arySidekick aryElemAddr fld
        | TypeKind.PrimitiveKind ->
                clrs.AddField(new ClrTypeSidekick(aryCompType,aryCompKind))
        | _ ->
                clrs.AddField(new ClrTypeSidekick(aryCompType,aryCompKind))
        ()

//    let private getReferenceTypeFieldSidekick (heap:ClrHeap) (clrs:ClrTypeSidekick) (addr:address) (fld:ClrInstanceField) =
//        let clrType = clrs.ClrType
//        let cats = clrs.Categories
//
//        ()



    let getTypeSidekick (heap:ClrHeap) (clrType:ClrType) (kind:TypeKind) (addr:address) =
        Debug.Assert(clrType<>null);
        let clrs = new ClrTypeSidekick(clrType, kind, null);
        match TypeKinds.GetMainTypeKind(kind) with
        | TypeKind.ReferenceKind ->
            match TypeKinds.GetParticularTypeKind(kind) with
            | TypeKind.Str | TypeKind.Exception -> ()
            | TypeKind.Ary ->
                getArrayElementTypeSidekick heap clrs addr 
            | _ ->
                for i = 0 to clrType.Fields.Count - 1 do
                    let fld = clrType.Fields.[i]
                    getStructTypeFieldSidekick heap clrs addr fld
        | TypeKind.Struct ->
            match TypeKinds.GetParticularTypeKind(kind) with
            | TypeKind.DateTime | TypeKind.TimeSpan | TypeKind.Decimal | TypeKind.Guid -> ()
            | _ ->
                for i = 0 to clrType.Fields.Count - 1 do
                    let fld = clrType.Fields.[i]
                    getStructTypeFieldSidekick heap clrs addr fld
                ()
        | _ -> ()
            
        clrs

    let getTypeSidekickAtAddress (heap:ClrHeap) (addr:address) : (string * ClrTypeSidekick) =
        let clrType = heap.GetObjectType(addr)
        if isNull clrType then
            ("Cannot get a clr type at address: " + Utils.AddressString(addr), EmptyClrTypeSidekick.Value)
        else
            let kind = TypeKinds.GetTypeKind(clrType)
            let clrs = getTypeSidekick heap clrType kind addr
            (null,clrs)

    (*
        Misc
    *)

    // Tomas Petricek F# Snippets
    let NiceTypeName (t:System.Type) : string =
        let sb = new System.Text.StringBuilder(80)
        let rec build (t:System.Type) =
          if t.IsGenericType then 
            // Remove the `1 part from generic names
            let tick = t.Name.IndexOf('`')
            let name = t.Name.Substring(0, tick) 
            Printf.bprintf sb "%s" name
            Printf.bprintf sb "<"
            // Print generic type arguments recursively
            let args = t.GetGenericArguments()
            for i in 0 .. args.Length - 1 do 
              if i <> 0 then Printf.bprintf sb ", "
              build args.[i]
            Printf.bprintf sb ">"
          else
            // Print ordiary type name
            Printf.bprintf sb "%s" t.Name
        build t
        sb.ToString()

