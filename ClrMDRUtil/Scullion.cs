using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClrMDRIndex;
using System.IO;

namespace ClrMDRIndex
{
	public class Scullion
	{
        public const ulong AddressFlagMask = 0x00FFFFFFFFFFFFFF;
        public const ulong AddressMask = 0xFF00000000000000;

        private string _error;
        public string Error => _error;

        string _rootFolder;
        string _instancesFile, _refsDataFile, _fwdRefOffsetsFile, _fwdRfsFile, _bwdRefOffsetsFile, _bwdRefsFile;

        ulong[] _instances;
        int[] _reversedCounts;
        List<ulong> _notFoundInstances;
        Dictionary<int,ulong> _reflagSet = new Dictionary<int, ulong>();

        int _reflag_count;
        int _reversed_min;
        int _reversed_max;

        public Scullion(string rootFolder, 
            ulong[] instances,
            string instancesFile, 
            string refsDataFile, 
            string fwdRefOffsetsFile, 
            string fwdRfsFile, 
            string bwdRefOffsetsFile, 
            string bwdRefsFile)
		{
			_rootFolder = rootFolder;
            _instancesFile = instancesFile;
            _refsDataFile = refsDataFile;
            _fwdRefOffsetsFile = fwdRefOffsetsFile;
            _fwdRfsFile = fwdRfsFile;
            _bwdRefOffsetsFile = bwdRefOffsetsFile;
            _bwdRefsFile = bwdRefsFile;

            _instances = instances;
            _reversedCounts = new int[_instances.Length];
            _notFoundInstances = new List<ulong>();
		}

        public int GetReflagCount()
        {
            return _reflag_count;
        }
        
        public int GetNotFoundCount()
        {
            return _notFoundInstances.Count;
        }

        public KeyValuePair<int,int> GetReversedMinMax()
        {
            return new KeyValuePair<int, int>(_reversed_min, _reversed_max);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lst">Addresses of an instance and its references (object fields).</param>
        /// <param name="count">Items count, array length can be larger.</param>
        /// <returns>Address of the instance (parent), and count of unique references.</returns>
		public KeyValuePair<ulong,int> PreprocessParentRefs(ulong[] lst, int count)
		{
            // assert lst has to be sorted TODO JRD
            Debug.Assert(lst.Length > 0);
			var parent = lst[0];
            int i = 1;
            for (; i < count; ++i) if (lst[i] != parent) break;
            if (i == count) return new KeyValuePair<ulong, int>(parent, 0);
            lst[0] = lst[i];
            int ndx = 0;
			for( ; i < count;  ++i)
			{
                var val = lst[i];
                if (val == parent || val == lst[ndx]) continue;
                lst[++ndx] = val;
			}
			return new KeyValuePair<ulong, int>(parent, ndx);
		}

        public void BuildReferences()
        {
            string error = null;
            BinaryReader br = null;
            BinaryWriter bwoffsets = null;
            BinaryWriter bwrefs = null;
            int byteBufferSize = 2048;
            byte[] byteBuffer = new byte[byteBufferSize];
            int ulongBufferSize = 2048/4;
            ulong[] ulongBuffer = new ulong[ulongBufferSize];
            //int intBufferSize = 2048 / 4;
            //int[] intBuffer = new int[intBufferSize];
            long offset = 0L;
            int lastInstanceNdx = _instances.Length - 1;
            byte[] intBuff = new byte[4];
            long bwdTotalCount = 0;

            try
            {
                br = new BinaryReader(File.Open(FilePath(_refsDataFile), FileMode.Open));
                bwoffsets = new BinaryWriter(File.Open(FilePath(_fwdRefOffsetsFile), FileMode.Create));
                bwrefs = new BinaryWriter(File.Open(FilePath(_fwdRfsFile), FileMode.Create));

                for (int i= 0, icnt = _instances.Length; i < icnt; ++i)
                {
                    var refCnt = br.ReadInt32();
                    if (refCnt == 0)
                    {
                        bwoffsets.Write(offset);
                        continue;
                    }
                    int byteCount = refCnt * sizeof(ulong);
                    byteBuffer = CheckBufferSize(byteBuffer, ref byteBufferSize, byteCount);
                    br.Read(byteBuffer, 0, byteCount);
                    ulongBuffer = CheckBufferSize(ulongBuffer, ref ulongBufferSize, refCnt);
                    ToUInt64(byteBuffer, byteCount, ulongBuffer);
                    var kv = PreprocessParentRefs(ulongBuffer, refCnt);
                    bwoffsets.Write(offset);
                    if (kv.Value == 0)
                    {
                        continue;
                    }
                    //intBuffer = CheckBufferSize(intBuffer, ref intBufferSize, kv.Value);
                    Debug.Assert(address_search(_instances, 0, lastInstanceNdx, kv.Key) == i);
                    int bufNdx = 0;
                    for(int j = 0, jcnt = kv.Value; j < jcnt; ++j)
                    {
                        ulong childAddr = ulongBuffer[j];
                        int ndx = address_search(_instances, 0, lastInstanceNdx, childAddr);
                        if (ndx < 0)
                        {
                            _notFoundInstances.Add(childAddr);
                            continue;
                        }

                        _reversedCounts[ndx] += 1;
                        bwdTotalCount += 1;
                        // update child root flags
                        if (!same_addr_flags(_instances, i, ndx))
                        {
                            if (ndx < i) // child address smaller than parent's
                            { // in this case we might need to update address flag of current's children
                                ulong flag;
                                if (_reflagSet.TryGetValue(ndx,out flag))
                                {
                                    _reflagSet[ndx] = flag | (_instances[i] & AddressMask);
                                }
                                else
                                {
                                    _reflagSet.Add(ndx, (_instances[i] & AddressMask));
                                }
                            }
                            copy_addr_flags(_instances, i, ndx);
                        }
                        CopyBytes(ndx, byteBuffer, bufNdx * sizeof(int));
                        ++bufNdx;
                    }

                    if (bufNdx > 0)
                    {
                        bwrefs.Write(byteBuffer, 0, bufNdx * sizeof(int));
                    }

                }
                bwoffsets.Write(offset); // last extra offset to get the count of last item;
                bwoffsets.Close();
                bwoffsets = null;

                if (_reflagSet.Count>0)
                {
                    // TODO JRD
                }

                ClrMDRIndex.Utils.WriteUlongArray(_rootFolder + Path.DirectorySeparatorChar + _instancesFile, _instances, out error);
                ClrMDRIndex.Unsafe.FileWriter.CreateFileWithSize(FilePath(_bwdRefsFile), bwdTotalCount * sizeof(int), out error);
                ClrMDRIndex.Unsafe.FileWriter.CreateFileWithSize(FilePath(_bwdRefOffsetsFile), (_instances.Length+1) * sizeof(ulong), out error);
                _instances = null;




            }
            catch (Exception ex)
            {
                _error = Utils.GetExceptionErrorString(ex);
            }
            finally
            {
                br?.Close();
                bwoffsets?.Close();
            }



        }

        private string FilePath(string file)
        {
            return _rootFolder + Path.DirectorySeparatorChar + file;
        }

        unsafe static void CopyBytes(int value, byte[] buffer, int ndx)
        {
            fixed (byte* b = &buffer[ndx])
                *((int*)b) = value;
        }

        int address_search(ulong[] ary, int left, int right, ulong key)
        {
            key = (key & AddressFlagMask);
            while (left <= right)
            {
                int middle = (left + right) / 2;
                ulong ary_item = (ary[middle] & AddressFlagMask);
                if (key == ary_item) return middle;
                if (key < ary_item) right = middle - 1; else left = middle + 1;
            }
            return -1;
        }

        bool same_addr_flags(ulong[] ary, int lhs, int rhs)
        {
            return (ary[lhs] & AddressMask) == (ary[rhs] & AddressMask);
        }

        void copy_addr_flags(ulong[] ary, int from, int to)
        {
            ulong fromVal = ary[from];
            ulong toVal = ary[to];
            ary[to] = toVal | (fromVal & AddressMask);
        }

        private static T[] CheckBufferSize<T>(T[] buf, ref int size, int requiredSize)
        {
            if (size >= requiredSize) return buf;
            while (requiredSize > size)
            {
                size += size / 2;
            }
            return new T[size];
        }

        public static unsafe long ToInt64(byte[] value, int count)
        {
            fixed (byte* pbyte = &value[0])
            {
                return *((long*)pbyte);
            }
        }

        public static unsafe int ToUInt64(byte[] value, int count, ulong[] dest)
        {
            int destCount = count / 8;
            fixed (byte* pbyte = &value[0])
            {
                byte* p = pbyte;
                for (int i = 0; i < destCount; ++i)
                {
                    dest[i] = *((ulong*)p);
                    p += 8;
                }
                return destCount;
            }
        }

    }
}
