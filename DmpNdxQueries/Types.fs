namespace DmpNdxQueries
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

    let private getStructTypeFieldSidekick (heap:ClrHeap) (clrs:ClrTypeSidekick) (addr:address) (fld:ClrInstanceField) =
        let clrType = fld.Type
        let cats = TypeCategories.GetCategories(fld.Type)
        if cats.First = TypeCategory.Uknown then
            ()
        else
            match cats.First with
            | TypeCategory.Reference ->
                match cats.Second with
                | TypeCategory.System__Canon | TypeCategory.SystemObject ->
                    let fldAddr = fld.GetAddress(addr,true)
                    let paddr = ValueExtractor.ReadPointerAtAddress(fldAddr,heap)
                    let fldType = heap.GetObjectType(paddr)
                    let fldCats = getTypeCategory fldType
                    clrs.AddField(new ClrTypeSidekick(fldType,cats,fld))
                | _ ->
                    clrs.AddField(new ClrTypeSidekick(clrType,cats,fld))
            | TypeCategory.Struct ->
                ()
            | _ ->
                clrs.AddField(new ClrTypeSidekick(clrType,cats,fld))
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
        let cats = clrs.Categories
        let aryLen = aryType.GetArrayLength(addr)
        clrs.SetData(aryLen)
        let aryCompType = aryType.ComponentType
        let aryCompCats = getTypeCategory aryCompType
        match aryCompCats.First with
        | TypeCategory.Uknown -> ()
        | TypeCategory.Reference ->
            match aryCompCats.Second with
            | TypeCategory.Array ->
                let aryElemAddr = getArrayElementAddress aryType addr aryLen
                let aryElemType = heap.GetObjectType(aryElemAddr)
                if aryElemType <> null then
                    let aryElemCats = getTypeCategory aryElemType
                    let aryElemLen = aryElemType.GetArrayLength(aryElemAddr)
                    let aryElemSidekick = new ClrTypeSidekick(aryElemType,aryElemCats)
                    aryElemSidekick.SetData(aryElemLen)
                    clrs.AddField(aryElemSidekick)
                else
                    let aryElemSidekick = new ClrTypeSidekick(aryCompType,aryCompCats)
                    aryElemSidekick.SetData(-1) // unable to get internal array length
                    clrs.AddField(aryElemSidekick)
            | TypeCategory.System__Canon | TypeCategory.SystemObject ->
                let aryElemAddr = getArrayElementAddress aryType addr aryLen
                let aryElemType = heap.GetObjectType(aryElemAddr)
                if aryElemType <> null then
                    let aryElemCats = getTypeCategory aryElemType
                    let aryElemSidekick = new ClrTypeSidekick(aryElemType,aryElemCats)
                    clrs.AddField(aryElemSidekick)
                else
                    let aryElemSidekick = new ClrTypeSidekick(aryCompType,aryCompCats)
                    clrs.AddField(aryElemSidekick)
            | _ ->
                clrs.AddField(new ClrTypeSidekick(aryCompType,aryCompCats))
        | TypeCategory.Struct ->
            match aryCompCats.Second with
            | TypeCategory.DateTime | TypeCategory.Decimal | TypeCategory.Guid | TypeCategory.TimeSpan ->
                 clrs.AddField(new ClrTypeSidekick(aryCompType,aryCompCats))
            | _ ->
                let aryElemAddr = getArrayElementAddress aryType addr aryLen
                let arySidekick = new ClrTypeSidekick(aryCompType,aryCompCats)
                clrs.AddField(arySidekick)
                for i = 0 to aryCompType.Fields.Count - 1 do
                    let fld = aryCompType.Fields.[i]
                    getStructTypeFieldSidekick heap arySidekick aryElemAddr fld
        | TypeCategory.Primitive ->
                clrs.AddField(new ClrTypeSidekick(aryCompType,aryCompCats))
        | _ ->
                clrs.AddField(new ClrTypeSidekick(aryCompType,aryCompCats))
        ()

    let private getReferenceTypeFieldSidekick (heap:ClrHeap) (clrs:ClrTypeSidekick) (addr:address) (fld:ClrInstanceField) =
        let clrType = clrs.ClrType
        let cats = clrs.Categories

        ()



    let getTypeSidekick (heap:ClrHeap) (clrType:ClrType) (cats:TypeCategories) (addr:address) =
        Debug.Assert(clrType<>null);
        let clrs = new ClrTypeSidekick(clrType, cats, null);
        match cats.First with
        | TypeCategory.Reference ->
            match cats.Second with
            | TypeCategory.String | TypeCategory.Exception -> ()
            | TypeCategory.Array ->
                getArrayElementTypeSidekick heap clrs addr 
            | _ ->
                for i = 0 to clrType.Fields.Count - 1 do
                    let fld = clrType.Fields.[i]
                    getStructTypeFieldSidekick heap clrs addr fld
        | TypeCategory.Struct ->
            match cats.Second with
            | TypeCategory.DateTime | TypeCategory.TimeSpan | TypeCategory.Decimal | TypeCategory.Guid -> ()
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
            let cats = getTypeCategory clrType
            let clrs = getTypeSidekick heap clrType cats addr
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

