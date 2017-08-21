using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
    public class IdReferences
    {
        int[] _ids;
        int[] _offsets;
        int[] _references;

        public IdReferences(int[] ids, int[] offs, int[] refs)
        {
            _ids = ids;
            _offsets = offs;
            _references = refs;
        }

        public int[] GetReferences(int id)
        {
            var ndx = Array.BinarySearch(_ids, id);
            if (ndx < 0) return Utils.EmptyArray<int>.Value;
            int off = _offsets[ndx];
            int cnt = _references[off];
            int[] refs = new int[cnt];
            for(int i = 0; i < cnt; ++i)
            {
                refs[i] = _references[++off];
            }
            return refs;
        }

    }
}
