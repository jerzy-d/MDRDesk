// Learn more about F# at http://fsharp.org. See the 'F# Tutorial' project
// for more guidance on F# programming.
#r "../ClrMd/Microsoft.Diagnostics.Runtime.dll"
#R "../packages/FSharp.Charting.0.90.14/lib/net40/FSharp.Charting.dll"
#r "../MDRDesk/bin/x64/Debug/ClrMDRUtil.dll"
#r "../MDRDesk/bin/x64/Debug/ClrMDRIndex.dll"
#r "bin/Debug/DmpNdxQueries.dll"

open System
open System.Text
open System.IO
open System.Collections.Generic
open System.Diagnostics
open Microsoft.Diagnostics.Runtime
open ClrMDRIndex
open DmpNdxQueries


let indexFolder = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.dmp.map"
let dumpsFolder = @"D:\Jerzy\WinDbgStuff\dumps"
let dacsFolder = @"D:\Jerzy\WinDbgStuff\mscordacwks"
let procdumpFolder = @"D:\bin\SysinternalsSuite"

let initSetup =
    Setup.SetDumpsFolder(dumpsFolder)
    Setup.SetDacFolder(dacsFolder)
    Setup.SetProcdumpFolder(procdumpFolder)
    Setup.SetRecentIndexList(new List<string>(0))
    Setup.SetRecentAdhocList(new List<string>(0))

let openDumpIndex (indexFolder:string) : (string * DumpIndex) =
    initSetup
    let error = ref String.Empty
    try
        let version = new Version(0,3,0,8)
        let dumpIndex = DumpIndex.OpenIndexInstanceReferences(version, indexFolder, 0, error, null)
        (!error, dumpIndex)
    with
        | exn -> (exn.ToString(), null)


let result = openDumpIndex indexFolder
if isNull (fst result) then
    use dumpIndex = snd result
    printfn "success"
else
    printfn "Failed\n: %s" (fst result)


//let msize = 4
let modulo n m = ((n%m)+m)%m
let wrap msize (x,y) = ((modulo (x-1) msize) + 1, (modulo (y-1) msize) + 1)

let neighbs m (x,y) = Seq.map (wrap m) [(x-1,y-1); (x,y-1); (x+1,y-1); (x-1,y); (x+1,y); (x-1,y+1); (x,y+1); (x+1,y+1)]

let nb11 = neighbs 4 (1,1)

printf "%A" (nb11 |> Seq.toList)


