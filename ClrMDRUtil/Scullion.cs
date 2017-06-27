using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using ClrMDRUtil.Utils;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	public class Scullion
	{
        public const ulong AddressFlagMask = 0x00FFFFFFFFFFFFFF;
        public const ulong AddressMask = 0xFF00000000000000;

        private string _error;
        public string Error => _error;

        string _refsDataFile, _fwdRefOffsetsFile, _fwdRfsFile, _bwdRefOffsetsFile, _bwdRefsFile, _instanceFile;

        ulong[] _instances;
        public int InstanceCount { get; private set; }
        int[] _reversedRefsCounts;
        int[] _fwdRefsCounts;
        List<ulong> _notFoundInstances;
        Dictionary<int,ulong> _reflagSet = new Dictionary<int, ulong>();
		IProgress<string> _progress;


		public Scullion( 
            ulong[] instances,
            string refsDataFile, 
            string fwdRefOffsetsFile, 
            string fwdRfsFile, 
            string bwdRefOffsetsFile, 
            string bwdRefsFile,
            string instanceFile,
			IProgress<string> progress)
		{
            _refsDataFile = refsDataFile;
            _fwdRefOffsetsFile = fwdRefOffsetsFile;
            _fwdRfsFile = fwdRfsFile;
            _bwdRefOffsetsFile = bwdRefOffsetsFile;
            _bwdRefsFile = bwdRefsFile;
            _instanceFile = instanceFile;

            _instances = instances;
			_progress = progress;
            InstanceCount = instances.Length;
            _reversedRefsCounts = new int[InstanceCount];
            _fwdRefsCounts = new int[InstanceCount];
            _notFoundInstances = new List<ulong>();
		}

        public int GetReflagCount()
        {
            return _reflagSet.Count;
        }
        
        public int GetNotFoundCount()
        {
            return _notFoundInstances.Count;
        }

        //public KeyValuePair<int,int> GetReversedMinMax()
        //{
        //    return new KeyValuePair<int, int>(_reversed_min, _reversed_max);
        //}

		public bool CreateForwardReferences(ClrHeap heap, out string error)
		{
			error = null;
			BinaryWriter bw = null;
			try
			{
				bw = new BinaryWriter(File.Open(_refsDataFile, FileMode.Create));
				var fieldAddrOffsetList = new List<ulong>(64);
				for (int i = 0, icnt = _instances.Length; i < icnt; ++i)
				{
					if (i== 31691 || i== 31825)
					{
						int a= 1;
					}
					var addr = Utils.RealAddress(_instances[i]);
					var clrType = heap.GetObjectType(addr);
					Debug.Assert(clrType!=null);
					if (TypeExtractor.IsExludedType(clrType.Name))
					{
						bw.Write((int)0);
						continue;
					}

                    if (clrType.Name== @"Eze.Server.Common.Pulse.Common.Types.CachedPositionValues<System.Decimal>")
                    {
                        int a = 1;
                    }

					fieldAddrOffsetList.Clear();
					clrType.EnumerateRefsOfObjectCarefully(addr, (address, off) =>
					{
						fieldAddrOffsetList.Add(address);
					});
					if (fieldAddrOffsetList.Count < 1)
					{
						bw.Write((int)0);
						continue;
					}

					int acount = PreprocessParentRefs(addr, fieldAddrOffsetList);
					if (acount == 0)
					{
						bw.Write((int)0);
						continue;
					}
					if (acount == 1)
					{
						int a = 1;
					}

					bw.Write(acount);
					//bw.Write(addr);
					for (int j = 0; j < acount; ++j)
					{
						ulong childAddr = fieldAddrOffsetList[j];
						bw.Write(childAddr);
					}
				}
				return true;
			}
			catch(Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				bw?.Close();
			}

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
            int ndx = 1;
			for( ; i < count;  ++i)
			{
                var val = lst[i];
                if (val == parent || val == lst[ndx]) continue;
                lst[ndx++] = val;
			}
			return new KeyValuePair<ulong, int>(parent, ndx);
		}

		public int PreprocessParentRefs(ulong parent, List<ulong> lst)
		{
			// assert lst has to be sorted TODO JRD
			int count = lst.Count;
			Debug.Assert(lst.Count > 0);
			lst.Sort(new Utils.AddressComparison());
			int i = 0, ndx = 0;
			for (; i < count; ++i)
				if (!Utils.SameRealAddresses(lst[i], parent)) break;
			if (i == count) return 0;
			lst[0] = lst[i++];
			for (; i < count; ++i)
			{
				var val = lst[i];
				if (val == parent || val == lst[ndx]) continue;
				lst[++ndx] = val;
			}
			return ndx+1;
		}

		public void BuildReferences()
        {
            string error = null;
            BinaryReader br = null;
            BinaryWriter bwoffsets = null;
            BinaryWriter bwrefs = null;
            MemoryMappedFile mmf = null;
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
                br = new BinaryReader(File.Open(_refsDataFile, FileMode.Open));
                bwoffsets = new BinaryWriter(File.Open(_fwdRefOffsetsFile, FileMode.Create));
                bwrefs = new BinaryWriter(File.Open(_fwdRfsFile, FileMode.Create));

                for (int i= 0, icnt = InstanceCount; i < icnt; ++i)
                {
					if (i == 31691 || i == 31825)
					{
						int a = 1;
					}
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
                    //var kv = PreprocessParentRefs(ulongBuffer, refCnt);
                    bwoffsets.Write(offset);
                    //if (kv.Value == 0)
                    //{
                    //    continue;
                    //}
                    //intBuffer = CheckBufferSize(intBuffer, ref intBufferSize, kv.Value);
                    //Debug.Assert(address_search(_instances, 0, lastInstanceNdx, kv.Key) == i);
                    int bufNdx = 0;
                    for(int j = 0; j < refCnt; ++j)
                    {
                        ulong childAddr = ulongBuffer[j];
                        int ndx = address_search(_instances, 0, lastInstanceNdx, childAddr);
                        if (ndx < 0)
                        {
                            _notFoundInstances.Add(childAddr);
                            continue;
                        }

                        _reversedRefsCounts[ndx] += 1;
                        _fwdRefsCounts[i] += 1;
                        offset += sizeof(int);
                        bwdTotalCount += 1;
                        // update child root flags
                        if (!contains_flags(_instances, i, ndx))
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
                        CopyBytes(ndx, intBuff);
						for (int k = 0; k < 4; ++k) byteBuffer[bufNdx++] = intBuff[k];
                    }

                    if (bufNdx > 0)
                    {
                        bwrefs.Write(byteBuffer, 0, bufNdx);
                    }

                }
                bwoffsets.Write(offset); // last extra offset to get the count of last item;
                bwoffsets.Close();
                bwoffsets = null;
                br.Close();
                br = null;
                bwrefs.Close();
                bwrefs = null;

                File.Delete(_refsDataFile);

                if (_reflagSet.Count>0)
                {
                    // TODO JRD
                    int a = 1;
                }

                Utils.WriteUlongArray(_instanceFile, _instances, out error);
                _instances = null; // release mem

                ClrMDRIndex.Unsafe.FileWriter.CreateFileWithSize(_bwdRefsFile, bwdTotalCount * sizeof(int), out error);
                ClrMDRIndex.Unsafe.FileWriter.CreateFileWithSize(_bwdRefOffsetsFile, (InstanceCount+1) * sizeof(long), out error);
 
                bwoffsets = new BinaryWriter(File.Open(_bwdRefOffsetsFile, FileMode.Create));

                long[] _bwdRefOffsets = new long[InstanceCount + 1];
                offset = 0L;
                for (int i = 0, icnt = _reversedRefsCounts.Length; i < icnt; ++i)
                {
                    bwoffsets.Write(offset);
                    _bwdRefOffsets[i] = offset;
                    offset += _reversedRefsCounts[i]*sizeof(int);
                }

                bwoffsets.Write(offset);
                bwoffsets.Close();
                bwoffsets = null;
                _bwdRefOffsets[_reversedRefsCounts.Length] = offset;
				br = new BinaryReader(File.Open(_fwdRfsFile, FileMode.Open));
				var fs = File.Open(_bwdRefsFile, FileMode.Open, FileAccess.ReadWrite);
                bwrefs = new BinaryWriter(fs);
                //mmf = MemoryMappedFile.CreateFromFile(fs,
                //                                        null,
                //                                        0,
                //                                        MemoryMappedFileAccess.CopyOnWrite,
                //                                        HandleInheritability.None,
                //                                        false
                //                                        );

                SortedDictionary<int, List<int>> dct = new SortedDictionary<int, List<int>>();
                for (int i = 0, icnt = _fwdRefsCounts.Length; i < icnt; ++i)
                {
                    int cnt = _fwdRefsCounts[i];
                    if (cnt == 0) continue;
                    for(int j = 0, jcnt = cnt; j < jcnt; ++j)
                    {
                        // read forward references
                        int childNdx = br.ReadInt32();
						if (childNdx == 31692 || childNdx == 31826)
						{
							int a = 1;
						}
						List<int> parentList;
                        if (dct.TryGetValue(childNdx,out parentList))
                        {
                            parentList.Add(i);
                        }
                        else
                        {
                            parentList = new List<int>() { i };
                            dct.Add(childNdx, parentList);
                        }
                        if (parentList.Count == _reversedRefsCounts[childNdx])
                        {
                            // dump to file
                            fs.Seek(_bwdRefOffsets[childNdx], SeekOrigin.Begin);
                            for (int k = 0, kcnt = parentList.Count; k < kcnt; ++k)
                            {
                                bwrefs.Write(parentList[k]);
                            }
                            Debug.Assert(GetCountFromIntOffsets(_bwdRefOffsets[childNdx], _bwdRefOffsets[childNdx + 1]) == parentList.Count);
                            // remove from dct
                            dct.Remove(childNdx);
                        }
                    }
                }
                bwrefs.Close();
                bwrefs = null;


            }
            catch (Exception ex)
            {
                _error = Utils.GetExceptionErrorString(ex);
            }
            finally
            {
                br?.Close();
                bwoffsets?.Close();
                bwrefs?.Close();
                mmf?.Dispose();
            }



        }

        private int GetCountFromIntOffsets(long off1, long off2)
        {
            return ((int)(off2 - off1)) / sizeof(int);
        }

        // unsafe static void CopyBytes(int value, byte[] buffer, int ndx)
        //{
        //    fixed (byte* b = &buffer[ndx])
        //        *((int*)b) = value;
        //}

		unsafe static void CopyBytes(int value, byte[] buffer)
		{
			fixed (byte* b = buffer)
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
            var lflg = ary[lhs] & AddressMask;
            if (lflg == 0) return true;
            return (ary[lhs] & AddressMask) == (ary[rhs] & AddressMask);
        }

        bool contains_flags(ulong[] ary, int lhs, int rhs)
        {
            var lflg = ary[lhs] & AddressMask;
            if (lflg == 0) return true;
            var rflg = ary[rhs] & AddressMask;
            ulong i = 0x8000000000000000;
            if ((lflg & i) != 0 && (rflg & i) == 0) return false;
            i >>= 1;
            if ((lflg & i) != 0 && (rflg & i) == 0) return false;
            i >>= 1;
            if ((lflg & i) != 0 && (rflg & i) == 0) return false;
            i >>= 1;
            if ((lflg & i) != 0 && (rflg & i) == 0) return false;
            return true;
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
