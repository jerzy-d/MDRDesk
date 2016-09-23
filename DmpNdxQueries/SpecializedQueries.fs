namespace DmpNdxQueries
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

    /// <summary>
    /// Get all references of WeakReference instances.
    /// </summary>
    /// <param name="heap">MDR heap.</param>
    /// <param name="addresses">Addresses of WeakReference instances</param>
    /// <param name="m_handle">Instance of m_handle field.</param>
    /// <param name="m_value">Instance of m_value field (from System.IntPtr type).</param>
    /// <returns></returns>
    let getWeakReferenceInfos (heap:ClrHeap) (addresses:address array) (m_handle:ClrInstanceField) (m_value:ClrInstanceField): (string*triple<address,address,string>[]) =
        let typeInfos = ResizeArray<triple<address,address,string>>(addresses.Length)
        try
            for addr in addresses do
                let m_handleVal = m_handle.GetValue(addr, false, false) :?> int64
                let m_valueVal = m_value.GetValue(Convert.ToUInt64(m_handleVal), true, false) :?> address
                let clrType = heap.GetObjectType(m_valueVal) // type pointed to by WeakReference instance at address addr
                let typeName = if isNull clrType then Constants.Unknown else clrType.Name
                typeInfos.Add(new triple<address,address,string>(addr,m_valueVal,typeName))
                ()
            (null, typeInfos.ToArray())
        with
            | exn -> (exn.ToString(),null)
