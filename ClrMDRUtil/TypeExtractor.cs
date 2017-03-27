using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
    public class TypeExtractor
    {
        private static readonly string[] KnownTypes = new string[]
        {
            "System.Collections.Generic.Dictionary<",
            "System.Collections.Generic.HashSet<",
            "System.Collections.Generic.SortedDictionary<",
        };

        public static bool IsKnownType(string typeName)
        {
            if (string.Compare("System.Text.StringBuilder", typeName, StringComparison.Ordinal) == 0) return true;

            for (int i = 0, icnt = KnownTypes.Length; i < icnt; ++i)
            {
                if (typeName.StartsWith(KnownTypes[i], StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public static ClrtDisplayableType GetClrtDisplayableType(ClrHeap heap, ulong[] addresses, out string error)
        {
            error = null;
            try
            {
                for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
                {
                    var addr = addresses[i];
                    ClrType clrType = heap.GetObjectType(addr);
                    ClrElementKind kind = ClrTypeElementKind.GetElementKind(clrType);
                }
                return null; // TODO JRD !!
            }
            catch(Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
        }

    }
}
