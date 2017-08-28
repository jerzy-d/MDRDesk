using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
    public class IdReferences
    {
        int[] _ids;
        int[] _offsets;
        long[] _references;

        public IdReferences(int[] ids, int[] offs, long[] refs)
        {
            _ids = ids;
            _offsets = offs;
            _references = refs;
        }

        public KeyValuePair<int[],int[]> GetReferences(int id)
        {
            var ndx = Array.BinarySearch(_ids, id);
            if (ndx < 0) return new KeyValuePair<int[], int[]>(Utils.EmptyArray<int>.Value, Utils.EmptyArray<int>.Value);
            int off = _offsets[ndx];
            int cnt = (int)_references[off];
            int[] refs1 = new int[cnt];
            int[] refs2 = new int[cnt];
            for (int i = 0; i < cnt; ++i)
            {
                long r = _references[++off];
                refs1[i] = (int)(r & 0x00000000FFFFFFFF);
                refs2[i] = (int)(((ulong)r & 0xFFFFFFFF00000000)>>32);
            }
            return new KeyValuePair<int[], int[]>(refs1,refs2);
        }

        public static (string, int[], int[], long[]) LoadIdReferences(string path)
        {
            string error = null;
            BinaryReader br = null;
            try
            {
                br = new BinaryReader(File.Open(path, FileMode.Open));
                int refCnt = br.ReadInt32();
                int[] vals = new int[refCnt];
                for (int i = 0; i < refCnt; ++i)
                {
                    vals[i] = br.ReadInt32();
                }
                int[] offs = new int[refCnt];
                for (int i = 0; i < refCnt; ++i)
                {
                    offs[i] = br.ReadInt32();
                }
                int totRefCnt = br.ReadInt32();
                long[] refs = new long[totRefCnt];
                for (int i = 0; i < totRefCnt; ++i)
                {
                    refs[i] = br.ReadInt64();
                }
                return (null, vals, offs, refs);
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return (error, null, null, null);
            }
            finally
            {
                br?.Close();
            }
        }

        public static bool SaveIndicesReferences(string path, SortedDictionary<int, long[]> refs, out string error)
        {
            error = null;
            BinaryWriter bw = null;
            try
            {
                int refCnt = refs.Count;
                bw = new BinaryWriter(File.Open(path, FileMode.Create));
                bw.Write(refCnt);
                int[] roffs = new int[refCnt];
                int off = 0;
                int ndx = 0;
                foreach (var r in refs)
                {
                    bw.Write(r.Key);
                    roffs[ndx] = off;
                    if (++ndx < refCnt)
                        off += r.Value.Length + 1;
                }
                for (int i = 0; i < refCnt; ++i)
                {
                    bw.Write(roffs[i]);
                }
                bw.Write(off);
                foreach (var r in refs)
                {
                    var ary = r.Value;
                    int cnt = ary.Length;
                    bw.Write((long)cnt);
                    for (int i = 0; i < cnt; ++i)
                    {
                        bw.Write(ary[i]);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return false;
            }
            finally
            {
                bw?.Close();
            }
        }

        public static bool SaveIndicesReferences(string path, SortedDictionary<int, List<long>> refs, out string error)
        {
            error = null;
            BinaryWriter bw = null;
            try
            {
                int refCnt = refs.Count;
                bw = new BinaryWriter(File.Open(path, FileMode.Create));
                bw.Write(refCnt);
                int[] roffs = new int[refCnt];
                int off = 0;
                int ndx = 0;
                foreach (var r in refs)
                {
                    bw.Write(r.Key);
                    roffs[ndx] = off;
                    if (++ndx < refCnt)
                        off += r.Value.Count + 1;
                }
                for (int i = 0; i < refCnt; ++i)
                {
                    bw.Write(roffs[i]);
                }
                bw.Write(off);
                foreach (var r in refs)
                {
                    var lst = r.Value;
                    int cnt = lst.Count;
                    bw.Write((long)cnt);
                    for (int i = 0; i < cnt; ++i)
                    {
                        bw.Write(lst[i]);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return false;
            }
            finally
            {
                bw?.Close();
            }
        }
    }
}
