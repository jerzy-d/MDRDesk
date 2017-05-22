using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Threading;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;
using System.Net;

namespace ClrMDRIndex
{
	public class Utils
	{
		#region Address Handling

		public struct RootBits
		{
			public static ulong Mask = 0xF000000000000000;
			public static ulong FinalizerMask = 0x3000000000000000;
			public static ulong AddressMask = 0x0FFFFFFFFFFFFFFF;
			public static ulong Rooted = 0x8000000000000000;
			public static ulong Finalizer = 0x4000000000000000;
			public static ulong Root = 0x2000000000000000;
			public static ulong RootedMask = (Rooted|Root);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong SetAsFinalizer(ulong addr)
		{
			return addr |= (ulong)RootBits.Finalizer;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong SetAsRooted(ulong addr)
		{
			return addr |= (ulong)RootBits.Rooted;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong SetAsRoot(ulong addr)
		{
			return addr |= (ulong)RootBits.Root;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong RealAddress(ulong addr)
		{
			return addr & (ulong)RootBits.AddressMask;
		}

		public static int[] GetAddressIndices(ulong[] addrs, ulong[] all)
		{
			Debug.Assert(Utils.AreAddressesSorted(addrs));
			Debug.Assert(Utils.AreAddressesSorted(all));
			List<int> lst = new List<int>(addrs.Length);
			int andx = 0, lndx=0, alen=addrs.Length, llen=all.Length;
			while (andx < alen && lndx < llen)
			{
				if (addrs[andx] == all[lndx])
				{
					lst.Add(lndx);
					++lndx;
					++andx;
					continue;
				}
				if (addrs[andx] < all[lndx])
				{
					++andx;
					continue;
				}
				++lndx;

			}
			return lst.ToArray();
		}

		public static int[] GetAddressIndicesAndRest(ulong[] addrs, ulong[] all, out int[] rest)
		{
			Debug.Assert(Utils.AreAddressesSorted(addrs));
			Debug.Assert(Utils.AreAddressesSorted(all));
			List<int> lst = new List<int>(addrs.Length);
			List<int> rlst = new List<int>(all.Length-addrs.Length);
			int andx = 0, lndx = 0, alen = addrs.Length, llen = all.Length;
			while (andx < alen && lndx < llen)
			{
				if (addrs[andx] == all[lndx])
				{
					lst.Add(lndx);
					++lndx;
					++andx;
					continue;
				}
				if (addrs[andx] < all[lndx])
				{
					++andx;
					continue;
				}
				rlst.Add(lndx);
				++lndx;
			}
			rest = rlst.ToArray();
			return lst.ToArray();
		}

		public static ulong[] GetRealAddressesInPlace(ulong[] addrs)
		{
			for (int i = 0, icnt = addrs.Length; i < icnt; ++i)
			{
				addrs[i] = RealAddress(addrs[i]);
			}
			return addrs;
		}

		public static ulong[] GetRealAddresses(ulong[] addrs)
		{
			ulong[] ary = new ulong[addrs.Length];
			for (int i = 0, icnt = addrs.Length; i < icnt; ++i)
			{
				ary[i] = RealAddress(addrs[i]);
			}
			return ary;
		}


		/// <summary>
		/// Marking of the instance addresses.
		/// Setting top bits of instance addresses, this way we will know if instances is rooted or belong to finalizer queue.
		/// </summary>
		/// <param name="bitSetters">Array of special addresses, instances known to be rooted, or belonging to finalizer queue.</param>
		/// <param name="addresses">Instance array to be marked.</param>
		/// <param name="bit">Marking bitmask.</param>
		public static int SetAddressBit(ulong[] bitSetters, ulong[] addresses, ulong bit)
		{
			int bNdx = 0, bLen = bitSetters.Length, aNdx = 0, aLen = addresses.Length, setCount=0;
			while (bNdx < bLen && aNdx < aLen)
			{
				var addr = Utils.RealAddress(addresses[aNdx]);
				var baddr = Utils.RealAddress(bitSetters[bNdx]);
				if (baddr > addr)
				{
					++aNdx;
					continue;
				}
				if (baddr < addr)
				{
					++bNdx;
					continue;
				}
				addresses[aNdx] |= bit;
				++setCount;
				++aNdx;
				++bNdx;
			}
			return setCount;
		}


		public static int SetAddressBit(Bitset bitset, ulong[] addresses, ulong bit)
		{
            if (bitset == null)
            {
                for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
                {
                        addresses[i] |= bit;
                }
                return addresses.Length;
            }

            Debug.Assert(bitset.Size == addresses.Length);
			int count = 0;
			for (int i = 0, icnt = bitset.Size; i < icnt; ++i)
			{
				if (bitset.IsSet(i))
				{
					addresses[i] |= bit;
					++count;
				}
			}
			return count;
		}

         public static int SetAddressBitIfSet(ulong[] bitSetters, ulong[] addresses, ulong bit)
		{
			int bNdx = 0, bLen = bitSetters.Length, aNdx = 0, aLen = addresses.Length, setCount = 0;
			while (bNdx < bLen && aNdx < aLen)
			{
				var addr = Utils.RealAddress(addresses[aNdx]);
				var baddr = Utils.RealAddress(bitSetters[bNdx]);
				if (baddr > addr)
				{
					++aNdx;
					continue;
				}
				if (baddr < addr)
				{
					++bNdx;
					continue;
				}
				if ((bitSetters[bNdx] & bit) > 0)
				{
					addresses[aNdx] |= bit;
					++setCount;
				}
				++aNdx;
				++bNdx;
			}
			return setCount;
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string AddressString(ulong addr)
        {
			ulong realAddr = RealAddress(addr);
			string format = GetAddressFormat(addr);
			return string.Format(format, realAddr);
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string AddressStringHeader(ulong addr)
		{
			ulong realAddr = RealAddress(addr);
			string format = GetAddressHeaderFormat(addr);
			return string.Format(format, realAddr);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string GetAddressFormat(ulong addr)
		{
			bool isRoot = IsRoot(addr);
			bool isFinalizer = IsFinalizer(addr);
			if (isRoot && isFinalizer)
				return "\u25BC\u2718{0:x14}";
			if (isRoot)
				return "\u25BCx{0:x14}";
			bool isRooted = IsRooted(addr);
			if (!isRooted && isFinalizer)
				return "\u2714\u2718{0:x14}";
			if (isFinalizer)
				return "\u2718x{0:x14}";
			if (isRooted)
				return "0x{0:x14}";
			return "\u2714x{0:x14}";
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string GetAddressHeaderFormat(ulong addr)
		{
			bool isRoot = IsRoot(addr);
			bool isFinalizer = IsFinalizer(addr);
			if (isRoot && isFinalizer)
				return "\u25BC\u2718{0:x14} ";
			if (isRoot)
				return "\u25BCx{0:x14} ";
			bool isRooted = IsRooted(addr);
			if (!isRooted && isFinalizer)
				return "\u2714\u2718{0:x14} ";
			if (isFinalizer)
				return "\u2718x{0:x14} ";
			if (isRooted)
				return "0x{0:x14} ";
			return "\u2714x{0:x14} ";
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string RealAddressString(ulong addr)
		{
			ulong realAddr = RealAddress(addr);
			return string.Format("0x{0:x14}", realAddr);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string RealAddressStringHeader(ulong addr)
		{
			ulong realAddr = RealAddress(addr);
			return string.Format("0x{0:x14} ", realAddr);
		}

		//[MethodImpl(MethodImplOptions.AggressiveInlining)]
		//public static string MultiAddressString(int addrCount)
		//{
		//	return string.Format("\u275A{0}\u275A", Utils.LargeNumberString(addrCount));
		//}


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong SetRooted(ulong addr)
		{
			return addr | (ulong)RootBits.Rooted;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong SetFinalizer(ulong addr)
		{
			return addr | (ulong)RootBits.Finalizer;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsFinalizer(ulong addr)
		{
			return (addr & (ulong)RootBits.Finalizer) > 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsRooted(ulong addr)
		{
			return (addr & (ulong)RootBits.Rooted) > 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsUnrooted(ulong addr)
		{
			return (addr & (ulong)RootBits.RootedMask) == 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsRoot(ulong addr)
		{
			return (addr & (ulong)RootBits.Root) > 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasBit(ulong addr, ulong bit)
		{
			return (addr & bit) > 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SortAddresses(ulong[] addresses)
        {
            Array.Sort(addresses, new AddressCmpAcs());
        }

        /// <summary>
        /// Binary search to find an index of the address in a ulong array.
        /// </summary>
        /// <param name="addresses">Array of ulongs.</param>
        /// <param name="addr">Address to look for.</param>
        /// <param name="lhs">An index to start the search, (left hand side).</param>
        /// <param name="rhs">The last index included in the search range, (right hand side).</param>
        /// <returns>Index of the item if found, invalid index (-1) otherwise.</returns>
        public static int AddressSearch(ulong[] addresses, ulong addr, int lhs, int rhs)
		{
			var cleanAddr = RealAddress(addr);
			while (lhs <= rhs)
			{
				int mid = lhs + (rhs - lhs)/2;
				ulong midAddr = RealAddress(addresses[mid]);
				if (midAddr == cleanAddr)
					return mid;
				if (midAddr < cleanAddr)
					lhs = mid + 1;
				else
					rhs = mid - 1;
			}
			return Constants.InvalidIndex;
		}

		public static int AddressSearch(ulong[] addresses, ulong addr)
		{
			int lhs = 0;
			int rhs = addresses.Length - 1;
			var cleanAddr = RealAddress(addr);
			while (lhs <= rhs)
			{
				int mid = lhs + (rhs - lhs) / 2;
				ulong midAddr = RealAddress(addresses[mid]);
				if (midAddr == cleanAddr)
					return mid;
				if (midAddr < cleanAddr)
					lhs = mid + 1;
				else
					rhs = mid - 1;
			}
			return Constants.InvalidIndex;
		}

		#endregion Address Handling

		#region IO

		public static char[] DirSeps = new char[] {Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar};

		public static string GetPathLastFolder(string path)
		{
			if (string.IsNullOrWhiteSpace(path) || path.Length < 1) return string.Empty;
			if (path[path.Length - 1] == Path.DirectorySeparatorChar || path[path.Length - 1] == Path.DirectorySeparatorChar)
			{
				var ary = path.Split(DirSeps, StringSplitOptions.RemoveEmptyEntries);
				if (ary.Length < 1) return string.Empty;
				return ary[ary.Length - 1];
			}
			return Path.GetFileName(path);
		}

		public static string[] GetStringListFromFile(string filePath, out string error)
		{
			error = null;
			StreamReader rd = null;
			try
			{
				rd = new StreamReader(filePath);
				var ln = rd.ReadLine();
				var count = Int32.Parse(ln);
				var ary = new string[count];
				for (var i = 0; i < count; ++i)
				{
					ln = rd.ReadLine();
					ary[i] = ln;
				}
				return ary;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				rd?.Close();
			}
		}

		public static bool WriteStringListToFile(string filePath, IList<string> lst, out string error)
		{
			error = null;
			StreamWriter wr = null;
			try
			{
				wr = new StreamWriter(filePath);
				int cnt = lst.Count;
				wr.WriteLine(cnt);
				for (var i = 0; i < cnt; ++i)
				{
					wr.WriteLine(lst[i]);
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
				wr?.Close();
			}
		}

		public static ulong[] ReadUlongArray(string path, out string error)
		{
			error = null;
			BinaryReader br = null;
			try
			{
				br = new BinaryReader(File.Open(path,FileMode.Open));
				var cnt = br.ReadInt32();
				var ary = new ulong[cnt];
				for (int i = 0; i < cnt; ++i)
				{
					ary[i] = br.ReadUInt64();
				}
				return ary;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				br?.Close();
			}
		}

		public static uint[] ReadUintArray(string path, out string error)
		{
			error = null;
			BinaryReader br = null;
			try
			{
				br = new BinaryReader(File.Open(path, FileMode.Open));
				var cnt = br.ReadInt32();
				var ary = new uint[cnt];
				for (int i = 0; i < cnt; ++i)
				{
					ary[i] = br.ReadUInt32();
				}
				return ary;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				br?.Close();
			}
		}

		public static ClrElementType[] ReadClrElementTypeArray(string path, out string error)
		{
			error = null;
			BinaryReader br = null;
			try
			{
				br = new BinaryReader(File.Open(path, FileMode.Open));
				var cnt = br.ReadInt32();
				var ary = new ClrElementType[cnt];
				for (int i = 0; i < cnt; ++i)
				{
					ary[i] = (ClrElementType)br.ReadInt32();
				}
				return ary;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				br?.Close();
			}
		}

		public static int[] ReadIntArray(string path, out string error)
		{
			error = null;
			BinaryReader br = null;
			try
			{
				br = new BinaryReader(File.Open(path, FileMode.Open));
				var cnt = br.ReadInt32();
				var ary = new int[cnt];
				for (int i = 0; i < cnt; ++i)
				{
					ary[i] = br.ReadInt32();
				}
				return ary;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				br?.Close();
			}
		}

		public static Tuple<int[],int[]> ReadKvIntIntArrayAsTwoArrays(string path, out string error)
		{
			error = null;
			BinaryReader br = null;
			try
			{
				br = new BinaryReader(File.Open(path, FileMode.Open));
				var cnt = br.ReadInt32();
				int[] ary1 = new int[cnt];
				int[] ary2 = new int[cnt];
				for (int i = 0; i < cnt; ++i)
				{
					ary1[i] = br.ReadInt32();
					ary2[i] = br.ReadInt32();
				}
				return new Tuple<int[], int[]>(ary1,ary2);
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				br?.Close();
			}
		}

		public static KeyValuePair<int, int>[] ReadKvIntIntArray(string path, out string error)
		{
			error = null;
			BinaryReader br = null;
			try
			{
				br = new BinaryReader(File.Open(path, FileMode.Open));
				var cnt = br.ReadInt32();
				var ary = new KeyValuePair<int,int>[cnt];
				for (int i = 0; i < cnt; ++i)
				{
					var key = br.ReadInt32();
					var value = br.ReadInt32();
					ary[i] = new KeyValuePair<int, int>(key,value);
				}
				return ary;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				br?.Close();
			}
		}

		public static KeyValuePair<int, ulong>[] ReadKvIntUInt64Array(string path, out string error)
		{
			error = null;
			BinaryReader br = null;
			try
			{
				br = new BinaryReader(File.Open(path, FileMode.Open));
				var cnt = br.ReadInt32();
				var ary = new KeyValuePair<int, ulong>[cnt];
				for (int i = 0; i < cnt; ++i)
				{
					var key = br.ReadInt32();
					var value = br.ReadUInt64();
					ary[i] = new KeyValuePair<int, ulong>(key, value);
				}
				return ary;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				br?.Close();
			}
		}

		public static bool WriteKvIntIntArray(string path, IList<KeyValuePair<int, int>> lst, out string error)
		{
			error = null;
			BinaryWriter br = null;
			try
			{
				br = new BinaryWriter(File.Open(path, FileMode.Create));
				br.Write(lst.Count);
				for (int i = 0, icnt = lst.Count; i < icnt; ++i)
				{
					var kv = lst[i];
					br.Write(kv.Key);
					br.Write(kv.Value);
				}
				return true;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				br?.Close();
			}
		}

		public static bool WriteStringList(string filePath, IList<string> lst,out string error)
		{
			error = null;
			StreamWriter rd = null;
			try
			{
				rd = new StreamWriter(filePath);
				rd.WriteLine(lst.Count);
				for (int i = 0, icnt = lst.Count; i < icnt; ++i)
				{
					rd.WriteLine(lst[i]);
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
				rd?.Close();
			}
		}

		public static bool WriteUlongArray(string path, IList<ulong> lst, out string error)
		{
			error = null;
			BinaryWriter bw = null;
			try
			{
				bw = new BinaryWriter(File.Open(path, FileMode.Create));
				var cnt = lst.Count;
				bw.Write(cnt);
				for (int i = 0; i < cnt; ++i)
				{
					bw.Write(lst[i]);
				}
				return true;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				bw?.Close();
			}
		}

		public static bool WriteAddressAsStringArray(string path, IList<ulong> lst, out string error)
		{
			error = null;
			StreamWriter bw = null;
			try
			{
				bw = new StreamWriter(path);
				var cnt = lst.Count;
				bw.WriteLine("#### " + Utils.LargeNumberString(cnt));
				for (int i = 0; i < cnt; ++i)
				{
					bw.WriteLine(Utils.AddressString(lst[i]));
				}
				return true;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				bw?.Close();
			}
		}

		public static bool WriteUlongIntArrays(string path, IList<ulong> lst1, IList<int> lst2, out string error)
        {
            Debug.Assert(lst1.Count == lst2.Count);
            error = null;
            BinaryWriter bw = null;
            try
            {
                bw = new BinaryWriter(File.Open(path, FileMode.Create));
                var cnt = lst1.Count;
                bw.Write(cnt);
                for (int i = 0; i < cnt; ++i)
                {
                    bw.Write(lst1[i]);
                    bw.Write(lst2[i]);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = GetExceptionErrorString(ex);
                return false;
            }
            finally
            {
                bw?.Close();
            }
        }

		public static bool WriteUlongUintIntArrays(string path, IList<ulong> lst1, IList<uint> lst2, IList<int> lst3, out string error)
		{
			Debug.Assert(lst1.Count == lst2.Count && lst1.Count == lst3.Count);
			error = null;
			BinaryWriter bw = null;
			try
			{
				bw = new BinaryWriter(File.Open(path, FileMode.Create));
				var cnt = lst1.Count;
				bw.Write(cnt);
				for (int i = 0; i < cnt; ++i)
				{
					bw.Write(lst1[i]);
					bw.Write(lst2[i]);
					bw.Write(lst3[i]);
				}
				return true;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				bw?.Close();
			}
		}

		public static bool ReadUlongIntArrays(string path, out ulong[] lst1, out int[] lst2, out string error)
		{
			error = null;
			lst1 = null;
			lst2 = null;
			BinaryReader br = null;
			try
			{
				br = new BinaryReader(File.Open(path, FileMode.Open));
				var cnt = br.ReadInt32();
				lst1 = new ulong[cnt];
				lst2 = new int[cnt];
				for (int i = 0; i < cnt; ++i)
				{
					lst1[i] = br.ReadUInt64();
					lst2[i] = br.ReadInt32();
				}
				return true;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				br?.Close();
			}
		}

		public static bool ReadUlongUintIntArrays(string path, out ulong[] lst1, out uint[] lst2, out int[] lst3, out string error)
		{
			error = null;
			lst1 = null;
			lst2 = null;
			lst3 = null;
			BinaryReader br = null;
			try
			{
				br = new BinaryReader(File.Open(path, FileMode.Open));
				var cnt = br.ReadInt32();
				lst1 = new ulong[cnt];
				lst2 = new uint[cnt];
				lst3 = new int[cnt];
				for (int i = 0; i < cnt; ++i)
				{
					lst1[i] = br.ReadUInt64();
					lst2[i] = br.ReadUInt32();
					lst3[i] = br.ReadInt32();
				}
				return true;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				br?.Close();
			}
		}

		public static bool WriteIntArrays(string path, IList<int> lst1, IList<int> lst2, out string error)
        {
            error = null;
            BinaryWriter bw = null;
            try
            {
                bw = new BinaryWriter(File.Open(path, FileMode.Create));
	            var cnt1 = lst1.Count;
	            var cnt2 = lst2.Count;
				bw.Write(cnt1);
				bw.Write(cnt2);
				if (cnt1 == cnt2)
                {
                    for (int i = 0; i < cnt1; ++i)
                    {
                        bw.Write(lst1[i]);
                        bw.Write(lst2[i]);
                    }
                }
                else
                {
                    for (int i = 0; i < cnt1; ++i)
                    {
                        bw.Write(lst1[i]);
                    }
                    for (int i = 0; i < cnt2; ++i)
                    {
                        bw.Write(lst2[i]);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = GetExceptionErrorString(ex);
                return false;
            }
            finally
            {
                bw?.Close();
            }
        }

		public static bool ReadIntArrays(string path, out int[] lst1, out int[] lst2, out string error)
		{
			error = null;
			BinaryReader br = null;
			lst1 = lst2 = null;
			try
			{
				br = new BinaryReader(File.Open(path, FileMode.Open));
				var cnt1 = br.ReadInt32();
				var cnt2 = br.ReadInt32();
				lst1 = new int[cnt1];
				lst2 = new int[cnt2];
				if (cnt1 == cnt2)
				{
					for (int i = 0; i < cnt1; ++i)
					{
						lst1[i] = br.ReadInt32();
						lst2[i] = br.ReadInt32();
					}
				}
				else
				{
					for (int i = 0; i < cnt1; ++i)
					{
						lst1[i] = br.ReadInt32();
					}
					for (int i = 0; i < cnt2; ++i)
					{
						lst2[i] = br.ReadInt32();
					}
				}
				return true;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				br?.Close();
			}
		}

		public static bool WriteUintArray(string path, IList<uint> lst, out string error)
		{
			error = null;
			BinaryWriter bw = null;
			try
			{
				bw = new BinaryWriter(File.Open(path, FileMode.Create));
				var cnt = lst.Count;
				bw.Write(cnt);
				for (int i = 0; i < cnt; ++i)
				{
					bw.Write(lst[i]);
				}
				return true;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				bw?.Close();
			}
		}

		public static bool WriteIntArray(string path, IList<int> lst, out string error)
		{
			error = null;
			BinaryWriter bw = null;
			try
			{
				bw = new BinaryWriter(File.Open(path, FileMode.Create));
				var cnt = lst.Count;
				bw.Write(cnt);
				for (int i = 0; i < cnt; ++i)
				{
					bw.Write(lst[i]);
				}
				return true;
			}
			catch (Exception ex)
			{
				error = GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				bw?.Close();
			}
		}

		public static void CloseStream(ref StreamWriter s)
		{
			s?.Close();
			s = null;
		}

		public static void CloseStream(ref StreamReader s)
		{
			s?.Close();
			s = null;
		}

		public static void CloseStream(ref BinaryWriter s)
		{
			s?.Close();
			s = null;
		}

		public static void CloseStream(ref BinaryReader s)
		{
			s?.Close();
			s = null;
		}

		public static void DeleteFiles(IList<string> paths, out string error)
		{
			error = null;
			try
			{
				for (int i = 0, icnt = paths.Count; i < icnt; ++i)
				{
					File.Delete(paths[i]);
				}
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
			}
		}


		public static HashSet<char> InvalidPathChars = new HashSet<char>(Path.GetInvalidPathChars());
		 
		public static string GetValidFileName(string name)
		{
			bool found = false;
			for (int i = 0, icnt = name.Length; i < icnt; ++i)
			{
				if (InvalidPathChars.Contains(name[i]))
				{
					found = true;
					break;
				}
			}
			if (!found) return name;
			var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
			sb.Append(name);
			for (int i = 0, icnt = name.Length; i < icnt; ++i)
			{
				if (InvalidPathChars.Contains(name[i]))
				{
					sb[i] = '_';
				}
			}
			return StringBuilderCache.GetStringAndRelease(sb);
		}

		#endregion IO

		#region Dac File Search

		public static string SearchDacFolder(string dacFileName, string dacFileFolder)
		{

			var folder = new DirectoryInfo(dacFileFolder);
			foreach (var dir in folder.EnumerateDirectories())
			{
				var pathName = dir.Name;
				var dirName = Path.GetFileName(pathName);
				if (string.Compare(dirName, dacFileName, StringComparison.OrdinalIgnoreCase) == 0)
				{
					return LookForDacDll(dir);
				}
			}
			return null;
		}

		private static string LookForDacDll(DirectoryInfo dir)
		{
			Queue<DirectoryInfo> que = new Queue<DirectoryInfo>();
			que.Enqueue(dir);
			while (que.Count > 0)
			{
				dir = que.Dequeue();
				foreach (var file in dir.EnumerateFiles())
				{
					var fname = Path.GetFileName(file.Name);
					if (fname.StartsWith("mscordacwks", StringComparison.OrdinalIgnoreCase)
						&& fname.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
					{
						return file.FullName;
					}
				}
				foreach (var d in dir.EnumerateDirectories())
				{
					que.Enqueue(d);
				}
			}
			return null;
		}

		#endregion Dac File Search

		#region String Utils

		public static string[] ReverseTypeNames(string[] names)
		{
			if (names == null || names.Length < 1) return EmptyArray<string>.Value;
			string[] rnames = new string[names.Length];
			for (int i = 0, icnt = names.Length; i < icnt; ++i)
			{
				rnames[i] = ReverseTypeName(names[i]);
			}
			return rnames;
		}

		public static string ReverseTypeName(string name)
		{
			if (string.IsNullOrWhiteSpace(name)) return string.Empty;
			var plusPos = name.IndexOf('+');
			var bracketPos = name.IndexOf('<');
			var lastDotPos = name.LastIndexOf('.');
			if (lastDotPos < 0 && bracketPos < 0 && plusPos < 0)
				return name;
			if (bracketPos == 0)
			{
				return name;
			}
			if (plusPos < 0 && bracketPos < 0)
			{
				return name.Substring(lastDotPos + 1) + Constants.NamespaceSepPadded + name.Substring(0, lastDotPos);
			}
			else if (bracketPos < 0)
			{
				return name.Substring(plusPos + 1) + "+" + Constants.NamespaceSepPadded + name.Substring(0, plusPos);
			}
			else if (plusPos < 0)
			{
				var pos = name.LastIndexOf('.', bracketPos, bracketPos - 1);
				if (pos <= 0) return name;
				return name.Substring(pos + 1) + Constants.NamespaceSepPadded + name.Substring(0, pos);

			}
			else if (plusPos < bracketPos)
			{
				return name.Substring(plusPos + 1) + "+" + Constants.NamespaceSepPadded + name.Substring(0, plusPos);
			}
			else
			{
				var pos = name.LastIndexOf('.', bracketPos, bracketPos - 1);
				return name.Substring(pos + 1) + Constants.NamespaceSepPadded + name.Substring(0, pos);
			}
		}

        public static string BaseTypeName(string name)
        {
            var plusPos = name.IndexOf('+');
            var bracketPos = name.IndexOf('<');
            var lastDotPos = name.LastIndexOf('.');
            if (lastDotPos < 0 && bracketPos < 0 && plusPos < 0)
                return name;
            if (bracketPos == 0)
            {
                return name;
            }
            if (plusPos < 0 && bracketPos < 0)
            {
                return name.Substring(lastDotPos + 1);
            }
            else if (bracketPos < 0)
            {
                return name.Substring(plusPos + 1) + "+";
            }
            else if (plusPos < 0)
            {
                var pos = name.LastIndexOf('.', bracketPos, bracketPos - 1);
                return name.Substring(pos + 1);

            }
            else if (plusPos < bracketPos)
            {
                return name.Substring(plusPos + 1) + "+";
            }
            else
            {
                var pos = name.LastIndexOf('.', bracketPos, bracketPos - 1);
                return name.Substring(pos + 1);
            }
        }

		public static string BaseArrayName(string name, int lenght)
		{
			string aryBaseName = Utils.BaseTypeName(name);
			if (aryBaseName.EndsWith("[]")) aryBaseName = aryBaseName.Substring(0, aryBaseName.Length - 2);
			return aryBaseName + "[" + lenght + "]";
		}

		public static int[] SortAndGetMap(string[] ary)
		{
			int cnt = ary.Length;
			var map = new int[cnt];
			for (var i = 0; i < cnt; ++i)
			{
				map[i] = i;
			}
			Array.Sort(ary, map,StringComparer.Ordinal);
			return map;
		}

		public static string[] CloneIntArray(string[] ary)
		{
			var cnt = ary.Length;
			var nary = new string[cnt];
			for (int i = 0; i < cnt; ++i)
			{
				nary[i] = ary[i];
			}
			return nary;
		}

		public static string ReplaceNewlines(string str)
		{
			if (string.IsNullOrEmpty(str)) return str;
			for (int i = 0, icnt = str.Length; i < icnt; ++i)
			{
				if (str[i] == '\n')
				{
					var newStr = str.Replace("\r\n", Constants.WindowsNewLine);
					newStr = newStr.Replace("\n", Constants.UnixNewLine);
					return newStr;
				}
			}
			return str;
		}

		public static string RemoveWhites(string str)
		{
			bool replaceWhites = false;
			for (int i = 0, icnt = str.Length; i < icnt; ++i)
			{
				if (char.IsWhiteSpace(str[i]))
				{
					replaceWhites = true;
					break;
				}
			}
			if (replaceWhites)
			{
				StringBuilder sb = new StringBuilder(str);
				sb.Replace("\r\n", "_");
				for (int i = 0, icnt = sb.Length; i < icnt; ++i)
				{
					if (char.IsWhiteSpace(sb[i]))
					{
						sb[i] = '_';
					}
				}
				return sb.ToString();
			}
			return str;
		}

		public static string RestoreNewlines(string str)
		{
			if (string.IsNullOrEmpty(str)) return str;
			for (int i = 0, icnt = str.Length; i < icnt; ++i)
			{
				if (str[i] == Constants.WindowsNewLineChar || str[i] == Constants.UnixNewLineChar)
				{
					var sb = new StringBuilder(str);
					sb.Replace(Constants.WindowsNewLine, "\r\n");
					sb.Replace(Constants.UnixNewLine, "\n");
					return sb.ToString();
				}
			}
			return str;
		}

        

        public static string GetFancyIntStr(int val, int width)
        {
            char[] digits = new char[width];
            for (int i = width-1; i >= 0; --i)
            {
                if (val == 0) { digits[i] = '\u274D'; continue; }
                int rem = val % 10;
                digits[i] = rem == 0 ? '\u274D' : (char)(0x277F + rem);
                val /= 10;
            }
            return new string(digits);
        }

        public static string GetSubscriptIntStr(int val, int width)
        {
            char[] digits = new char[width];
            for (int i = width - 1; i >= 0; --i)
            {
                if (val == 0) { digits[i] = '\u2080'; continue; }
                int rem = val % 10;
                digits[i] = rem == 0 ? '\u2080' : (char)(0x2080 + rem);
                val /= 10;
            }
            return new string(digits);
        }

        public static int SkipWhites(string str, int pos)
		{
			for (; pos < str.Length && Char.IsWhiteSpace(str[pos]); ++pos) ;
			return pos;
		}

        public static int SkipDecimalDigits(string str, int pos)
        {
            for (; pos < str.Length && Char.IsDigit(str[pos]); ++pos) ;
            return pos;
        }

        public static int SkipNonWhites(string str, int pos)
		{
			for (; pos < str.Length && !Char.IsWhiteSpace(str[pos]); ++pos) ;
			return pos;
		}

 
		private static bool IsBracket(char c)
		{
			return c == '['
			       || c == ']'
			       || c == '{'
			       || c == '}'
			       || c == '('
			       || c == ')';
		}

		private static char GetOpposingBracket(char c)
		{
			if (c == '[') return ']';
			if (c == '{') return '}';
			if (c == '(') return ')';
			if (c == ']') return '[';
			if (c == '}') return '{';
			if (c == ')') return '(';
			throw new ArgumentException("Utils.GetOpposingBracket -- not handled bracket: " + c);
		}

		private static int FindChar(string s, char c, int pos)
		{
			for (int i = pos, icnt = s.Length; i < icnt; ++i)
			{
				if (s[i] == c) return i;
			}
			return -1;
		}

		public static bool SameStringArrays(string[] ary1, string[] ary2)
		{
			if (ary1 == null && ary2 == null) return true;
			if (ary1 == null || ary2 == null) return false;
			if (ary1.Length != ary2.Length) return false;
			for (int i = 0, icnt = ary1.Length; i < icnt; ++i)
			{
				if (!SameStrings(ary1[i], ary2[i])) return false;
			}
			return true;
		}

		//public static bool ParseReportLine(string ln, ReportFile.ColumnType[] columns, List<string> lst)
		//{
		//	if (string.IsNullOrWhiteSpace(ln)) return false;
		//	int pos = 0, epos = 0;
		//	int lastItem = columns.Length - 1;
		//	for (int i = 0, icount = columns.Length; i < icount && epos < ln.Length; ++i)
		//	{
		//		pos = Utils.SkipWhites(ln, epos);
		//		if (i == lastItem && columns[lastItem] == ReportFile.ColumnType.String)
		//		{
		//			var substr = ln.Substring(pos);
		//			lst.Add(string.IsNullOrEmpty(substr) ? string.Empty : substr);
		//			return true;
		//		}

		//		if (IsBracket(ln[pos]))
		//		{
		//			char b = GetOpposingBracket(ln[pos]);
		//			++pos;
		//			epos = FindChar(ln, b, pos);
		//			if (epos < 0) return false;
		//			if (pos < epos) lst.Add(ln.Substring(pos, epos - pos).Trim());
		//			else lst.Add(string.Empty);
		//		}
		//		else
		//		{
		//			epos = SkipNonWhites(ln, pos);
		//			if (pos < epos) lst.Add(ln.Substring(pos, epos - pos).Trim());
		//			else lst.Add(string.Empty);
		//		}
		//		++epos;
		//	}

		//	return true;
		//}

		public static bool StartsWithPrefix(string str, IList<string> prefs)
		{
			for (int i = 0, icnt = prefs.Count; i < icnt; ++i)
			{
				if (str.StartsWith(prefs[i]))
					return true;
			}
			return false;
		}

		public static string GetValidName(string name)
		{
			if (string.IsNullOrEmpty(name)) return name;
			bool firstLetterOk = char.IsLetter(name[0]) || name[0] == '_';
			bool needChange = false;
			for (int i = 0,icnt=name.Length; i < icnt; ++i)
			{
				if (char.IsLetterOrDigit(name[i]) || name[i] == '_') continue;
				needChange = true;
				break;
			}
			if (firstLetterOk && !needChange) return name;
			var sb = StringBuilderCache.Acquire(name.Length + 1);
			if (!firstLetterOk)
			{
				sb.Append('_');
			}
			for (int i = 0, icnt = name.Length; i < icnt; ++i)
			{
				if (char.IsWhiteSpace(name[i])) continue;
				if (!(char.IsLetterOrDigit(name[i]) || name[i] == '_'))
				{
					sb.Append('_');
					continue;
				}
				sb.Append(name[i]);
			}
			return StringBuilderCache.GetStringAndRelease(sb);
		}

		public static string DisplayableChar(char c)
		{
			switch (c)
			{
				case ' ':
					return @"' '";
				case '\u0000': // Null
					return @"\0";
				case '\u0007': // Alert
					return @"\a";
				case '\u0008': // Backspace
					return @"\b";
				case '\u0009': // Horizontal tab
					return @"\t";
				case '\u000A': // New line
					return @"\n";
				case '\u000B': // Vertical tab
					return @"\v";
				case '\u000C': // Form feed
					return @"\f";
				case '\u000D': // Carriage retur
					return @"\r";
				case '\u0022': // Double quote
					return @"\""";
				case '\u0027': // Single quote
					return @"\'";
				case '\u005C': // Backslash
					return @"\\";

				default:
					if (Char.IsLetterOrDigit(c) || Char.IsPunctuation(c))
						return c.ToString();
					else
						return string.Format(@"\u{0:x4}", (uint) c);
			}
		}

		public static bool StartsWith(StringBuilder sb, string s)
		{
			if (s.Length > sb.Length) return false;
			for (int i = 0, icnt = s.Length; i < icnt; ++i)
			{
				if (sb[i] != s[i]) return false;
			}
			return true;
		}

		#endregion String Utils

		#region Comparers

		public static bool SameIntArrays(int[] ary1, int[] ary2)
		{
			if (ary1 == null && ary2 == null) return true;
			if (ary1 == null || ary2 == null) return false;
			if (ary1.Length != ary2.Length) return false;
			for (int i = 0, icnt = ary1.Length; i < icnt; ++i)
			{
				if (ary1[i] != ary2[i])
					return false;
			}
			return true;
		}

		public class AddressCmpDesc : IComparer<ulong>
        {
            public int Compare(ulong a, ulong b)
            {
                return RealAddress(a) < RealAddress(b) ? 1 : (RealAddress(a) > RealAddress(b) ? -1 : 0);
            }
        }

        public class AddressCmpAcs : IComparer<ulong>
        {
            public int Compare(ulong a, ulong b)
            {
                return RealAddress(a) < RealAddress(b) ? -1 : (RealAddress(a) > RealAddress(b) ? 1 : 0);
            }
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool SameStrings(string s1, string s2)
		{
			return string.Compare(s1, s2, StringComparison.Ordinal) == 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int CompareStrings(string s1, string s2)
		{
			return string.Compare(s1, s2, StringComparison.Ordinal);
		}

		public class LongCmpDesc : IComparer<long>
		{
			public int Compare(long a, long b)
			{
				return a < b ? 1 : (a > b ? -1 : 0);
			}
		}

		public class IntCmpDesc : IComparer<int>
		{
			public int Compare(int a, int b)
			{
				return a < b ? 1 : (a > b ? -1 : 0);
			}
		}

		public class UIntCmpDesc : IComparer<uint>
		{
			public int Compare(uint a, uint b)
			{
				return a < b ? 1 : (a > b ? -1 : 0);
			}
		}

		public class IntPairCmp : IComparer<pair<int, int>>
		{
			public int Compare(pair<int, int> a, pair<int, int> b)
			{
				return a.First < b.First ? -1 : (a.First > b.First ? 1 : 0);
			}
		}

		public class IntTripleCmp : IComparer<triple<int, String, ulong>>
		{
			public int Compare(triple<int, String, ulong> a, triple<int, String, ulong> b)
			{
				return a.First < b.First ? -1 : (a.First > b.First ? 1 : 0);
			}
		}

		public class KVStrStrCmp : IComparer<KeyValuePair<string, string>>
		{
			public int Compare(KeyValuePair<string, string> a, KeyValuePair<string, string> b)
			{
				return string.Compare(a.Value, b.Value, StringComparison.Ordinal);
			}
		}

		public class KVUlongStrByStrCmp : IComparer<KeyValuePair<ulong, string>>
		{
			public int Compare(KeyValuePair<ulong, string> a, KeyValuePair<ulong, string> b)
			{
				int cmp = string.Compare(a.Value, b.Value, StringComparison.Ordinal);
				if (cmp == 0)
				{
					cmp = a.Key < b.Key ? -1 : (a.Key > b.Key ? 1 : 0);
				}
				return cmp;
			}
		}


		public class NumericKvCmp<T1,T2> : IComparer<KeyValuePair<T1, T2>>
		{
			public int Compare(KeyValuePair<T1, T2> a, KeyValuePair<T1, T2> b)
			{
				int cmp = Comparer<T1>.Default.Compare(a.Key, b.Key);
				if (cmp == 0)
					cmp = Comparer<T2>.Default.Compare(a.Value, b.Value);
				return cmp;
			}
		}

		public class KVIntIntCmp : IComparer<KeyValuePair<int, int>>
        {
            public int Compare(KeyValuePair<int, int> a, KeyValuePair<int, int> b)
            {
                if (a.Key == b.Key)
                    return a.Value < b.Value ? -1 : (a.Value > b.Value ? 1 : 0);
                return a.Key < b.Key ? -1 : 1;
            }
        }

		public class KVUlongIntKCmp : IComparer<KeyValuePair<ulong, int>>
		{
			public int Compare(KeyValuePair<ulong, int> a, KeyValuePair<ulong, int> b)
			{
				return a.Key < b.Key ? -1 : (a.Key > b.Key ? 1 : 0);
			}
		}

		public class KVIntUlongCmpAsc : IComparer<KeyValuePair<int, ulong>>
		{
			public int Compare(KeyValuePair<int, ulong> a, KeyValuePair<int, ulong> b)
			{
				if (a.Key == b.Key)
					return a.Value > b.Value ? -1 : (a.Value < b.Value ? 1 : 0);
				return a.Key > b.Key ? -1 : (a.Key < b.Key ? 1 : 0);
			}
		}

		public class KVIntUlongCmpDesc : IComparer<KeyValuePair<int, ulong>>
		{
			public int Compare(KeyValuePair<int, ulong> a, KeyValuePair<int, ulong> b)
			{
				if (a.Key == b.Key)
					return a.Value < b.Value ? -1 : (a.Value > b.Value ? 1 : 0);
				return a.Key < b.Key ? -1 : (a.Key > b.Key ? 1 : 0);
			}
		}

		public class KVUlongUlongKeyCmp : IComparer<KeyValuePair<ulong, ulong>>
		{
			public int Compare(KeyValuePair<ulong, ulong> a, KeyValuePair<ulong, ulong> b)
			{
				if (a.Key == b.Key)
					return a.Value < b.Value ? -1 : (a.Value > b.Value ? 1 : 0);
				return a.Key < b.Key ? -1 : (a.Key > b.Key ? 1 : 0);
			}
		}


		/// <summary>
		/// Sort order: Third, Second, First
		/// </summary>
		public class TripleUlUlStrByStrUl2Cmp : IComparer<triple<ulong, ulong, string>>
		{
			public int Compare(triple<ulong, ulong, string> a, triple<ulong, ulong, string> b)
			{
				int cmp = string.Compare(a.Third, b.Third, StringComparison.Ordinal);
				if (cmp==0)
				{
					if (a.Third == b.Third)
						return a.Second < b.Second ? -1 : (a.Second > b.Second ? 1 : 0);
					return a.First < b.First ? -1 : (a.First > b.First ? 1 : 0);
				}
				return cmp;
			}
		}

		public class TripleUlongUlongIntKeyCmp : IComparer<triple<ulong, ulong,int>>
		{
			public int Compare(triple<ulong, ulong,int> a, triple<ulong, ulong,int> b)
			{
				if (a.First == b.First)
				{
					if (a.Second == b.Second)
						return a.Third < b.Third ? -1 : (a.Third > b.Third ? 1 : 0);
					return a.Second < b.Second ? -1 : (a.Second > b.Second ? 1 : 0);
				}
				return a.First < b.First ? -1 : (a.First > b.First ? 1 : 0);
			}
		}

		public class TripleIntUlongStrCmpAsc : IComparer<triple<int, ulong, string>>
		{
			public int Compare(triple<int, ulong, string> a, triple<int, ulong, string> b)
			{
				if (a.First == b.First)
				{
					if (a.Second == b.Second)
						return string.Compare(a.Third, b.Third, StringComparison.Ordinal);
					return a.Second < b.Second ? -1 : (a.Second > b.Second ? 1 : 0);
				}
				return a.First < b.First ? -1 : (a.First > b.First ? 1 : 0);
			}
		}

		public class TripleIntUlongStrCmpDesc : IComparer<triple<int, ulong, string>>
		{
			public int Compare(triple<int, ulong, string> a, triple<int, ulong, string> b)
			{
				if (a.First == b.First)
				{
					if (a.Second == b.Second)
						return string.Compare(b.Third, a.Third, StringComparison.Ordinal);
					return b.Second < a.Second ? -1 : (b.Second > a.Second ? 1 : 0);
				}
				return b.First < a.First ? -1 : (b.First > a.First ? 1 : 0);
			}
		}

		public class TripleStrStrStrCmp : IComparer<triple<string,string,string>>
		{
			private int _ndx;

			public TripleStrStrStrCmp(int ndx)
			{
				_ndx = ndx;
			}

			public int Compare(triple<string, string, string> a, triple<string, string, string> b)
			{
				switch (_ndx)
				{
					case 0:
						return string.Compare(a.First, b.First, StringComparison.Ordinal);
					case 1:
						return string.Compare(a.Second, b.Second, StringComparison.Ordinal);
					default:
						return string.Compare(a.Third, b.Third, StringComparison.Ordinal);
				}
			}
		}

		public class QuadrupleUlongUlongIntKeyCmp : IComparer<quadruple<ulong, ulong, int, int>>
        {
            public int Compare(quadruple<ulong, ulong, int, int> a, quadruple<ulong, ulong, int, int> b)
            {
                if (a.First == b.First)
                {
                    if (a.Second == b.Second)
                    {
                        if (a.Third == b.Third)
                        {
                            return a.Forth < b.Forth ? -1 : (a.Forth > b.Forth ? 1 : 0);
                        }
                        return a.Third < b.Third ? -1 : (a.Third > b.Third ? 1 : 0);
                    }
                    return a.Second < b.Second ? -1 : (a.Second > b.Second ? 1 : 0);
                }
                return a.First < b.First ? -1 : (a.First > b.First ? 1 : 0);
            }
        }

        public class QuadrupleIntIntStrStrCmp : IComparer<quadruple<int, int, string, string>>
        {
            public int Compare(quadruple<int, int, string, string> a, quadruple<int, int, string, string> b)
            {
                if (a.First == b.First)
                {
                    if (a.Second == b.Second)
                    {
                        if (Utils.SameStrings(a.Third,b.Third))
                        {
                            return string.Compare(a.Forth,b.Forth,StringComparison.Ordinal);
                        }
                        return string.Compare(a.Third, b.Third, StringComparison.Ordinal);
                    }
                    return a.Second < b.Second ? -1 : (a.Second > b.Second ? 1 : 0);
                }
                return a.First < b.First ? -1 : (a.First > b.First ? 1 : 0);
            }
        }

        public class QuadrupleIntStrStrAryUlongCmp : IComparer<quadruple<int, string, string, ulong[]>>
        {
            public int Compare(quadruple<int, string, string, ulong[]> a, quadruple<int, string, string, ulong[]> b)
            {
                if (a.First == b.First)
                {
                    if (Utils.SameStrings(a.Second, b.Second))
                    {
                         return string.Compare(a.Third, b.Third, StringComparison.Ordinal);
                    }
                    return string.Compare(a.Third, b.Third, StringComparison.Ordinal);
                }
                return a.First < b.First ? -1 : (a.First > b.First ? 1 : 0);
            }
        }

        public class TripleIntStrStrCmp : IComparer<triple<int, string, string>>
        {
            public int Compare(triple<int, string, string> a, triple<int, string, string> b)
            {
                if (a.First == b.First)
                {
                    if (Utils.SameStrings(a.Second, b.Second))
                    {
                        return string.Compare(a.Third, b.Third, StringComparison.Ordinal);
                    }
                   return string.Compare(a.Second, b.Second, StringComparison.Ordinal);
                }
                return a.First < b.First ? -1 : (a.First > b.First ? 1 : 0);
            }
        }
        public class StrListCmp : IComparer<IList<string>>
		{
			public int Compare(IList<string> a, IList<string> b)
			{
				var aLstCnt = a.Count;
				var bLstCnt = b.Count;
				for (int i = 0; i < aLstCnt && i < bLstCnt; ++i)
				{
					var cmp = string.Compare(a[i], b[i], StringComparison.Ordinal);
					if (cmp != 0) return cmp;
				}
				return aLstCnt < bLstCnt ? -1 : (aLstCnt > bLstCnt ? 1 : 0);
			}
		}

		public static NumStrCmpAsc NumStrAscComparer = new NumStrCmpAsc();

		public class NumStrCmpAsc : IComparer<string>
		{
			public int Compare(string a, string b)
			{
				bool aMinusSign = a.Length > 0 && a[0] == '-';
				bool bMinusSign = b.Length > 0 && b[0] == '-';
				if (aMinusSign && bMinusSign)
					return CompareNegatives(a, b);
				if (aMinusSign && !bMinusSign) return -1;
				if (!aMinusSign && bMinusSign) return 1;

				if (a.Length == b.Length)
				{
					for (int i = 0, icnt = a.Length; i < icnt; ++i)
					{
						if (a[i] < b[i]) return -1;
						if (a[i] > b[i]) return 1;
					}
					return 0;
				}
				return a.Length < b.Length ? -1 : 1;
			}

			private int CompareNegatives(string a, string b)
			{
				if (a.Length == b.Length)
				{
					for (int i = 1, icnt = a.Length; i < icnt; ++i)
					{
						if (a[i] > b[i]) return -1;
						if (a[i] < b[i]) return 1;
					}
					return 0;
				}
				return a.Length < b.Length ? 1 : -1;
			}
		}

		public static NumStrCmpDesc NumStrDescComparer = new NumStrCmpDesc();

		public class NumStrCmpDesc : IComparer<string>
		{
			public int Compare(string a, string b)
			{
				bool aMinusSign = a.Length > 0 && a[0] == '-';
				bool bMinusSign = b.Length > 0 && b[0] == '-';
				if (aMinusSign && bMinusSign)
					return CompareNegatives(a, b);
				if (aMinusSign && !bMinusSign) return 1;
				if (!aMinusSign && bMinusSign) return -1;

				if (a.Length == b.Length)
				{
					for (int i = 0, icnt = a.Length; i < icnt; ++i)
					{
						if (a[i] < b[i]) return 1;
						if (a[i] > b[i]) return -1;
					}
					return 0;
				}
				return a.Length > b.Length ? -1 : 1;
			}

			private int CompareNegatives(string a, string b)
			{
				if (a.Length == b.Length)
				{
					for (int i = 1, icnt = a.Length; i < icnt; ++i)
					{
						if (a[i] > b[i]) return 1;
						if (a[i] < b[i]) return -1;
					}
					return 0;
				}
				return a.Length < b.Length ? -1 : 1;
			}
		}



	    public class KvStrKvStrInt : IComparer<KeyValuePair<string, KeyValuePair<string, int>[]>>
	    {
	        public int Compare(KeyValuePair<string, KeyValuePair<string, int>[]> a,
                KeyValuePair<string, KeyValuePair<string, int>[]>  b)
	        {
	            return string.Compare(a.Key, b.Key,StringComparison.Ordinal);
	        }
	    }

        public class KvStrInt : IComparer<KeyValuePair<string, int>>
        {
            public int Compare(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
            {
                var cmp = string.Compare(a.Key, b.Key, StringComparison.Ordinal);
                if (cmp == 0)
                {
                    cmp = a.Value < b.Value ? -1 : (a.Value > b.Value ? 1 : 0);
                }
                return cmp;
            }
        }

		public class KvIntStr : IComparer<KeyValuePair<int, string>>
		{
			public int Compare(KeyValuePair<int, string> a, KeyValuePair<int, string> b)
			{
				if (a.Key == b.Key)
					return string.Compare(a.Value, b.Value, StringComparison.Ordinal);
				return a.Key < b.Key ? -1 : 1;
			}
		}

		public class IntArrayHeadCmp : IComparer<Int32[]>
		{
			public int Compare(int[] a, int[] b)
			{
				if ((a == null || a.Length < 1) && (b == null || b.Length < 1)) return 0;
				if (a == null || a.Length < 1) return -1;
				if (b == null || b.Length < 1) return 1;
				var len = Math.Min(a.Length, b.Length);
				for (int i = 0; i < len; ++i)
				{
					if (a[i] < b[i]) return -1;
					if (a[i] > b[i]) return 1;
				}
				return a.Length == b.Length ? 0 : (a.Length < b.Length ? -1 : 1);
			}
		}
	

	#endregion Comparers

		#region Formatting

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string RuntimeStringHeader(int index)
		{
			return string.Format("RT[{0}] ",index);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string UintHexStringHeader(uint num)
        {
            return string.Format("[0x{0:x8}] ", num);
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string HResultStringHeader(int num)
		{
			return string.Format("[0x{0:x8} {1,8:####}] ", (uint)num,num);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string DurationString(TimeSpan ts)
        {
            return string.Format(" {0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string StopAndGetDurationString(Stopwatch stopWatch)
		{
			stopWatch.Stop();
			return DurationString(stopWatch.Elapsed);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SortableLengthString(ulong len)
        {
            return len == 0 ? "             O" : string.Format("{0,14:0#,###,###,###}", len);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SortableLengthStringHeader(ulong len)
        {
            return len == 0 ? "             O" : string.Format("[{0,14:0#,###,###,###}] ", len);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SortableSizeString(int sz)
        {
            return sz == 0 ? "           O" : string.Format("{0,12:0##,###,###}", sz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SortableSizeStringHeader(int sz)
        {
            return sz == 0 ? "[           O] " : string.Format("[{0,12:0#,###,###}] ", sz);
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string SortableCountStringHeader(int sz)
		{
			return sz == 0 ? "[       O] " : string.Format("[{0,8:0#,###,###}] ", sz);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SizeString(int sz)
        {
            return sz == 0 ? "           O" : string.Format("{0,12:#,###,###}", sz);
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string SizeStringHeader(int sz, int digitCount)
		{
			if (sz == 0)
			{
				if (digitCount < 2)
					return "[0] ";
				return "[" + new string(' ', digitCount - 1) + "0] ";
			}
			string format = "[{0," + digitCount.ToString() + ":#,###,###}] ";
			return string.Format(format, sz);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string SizeStringHeader(int sz, int digitCount, string format)
		{
			if (sz == 0)
			{
				if (digitCount < 2)
					return "[0] ";
				return "[" + new string(' ', digitCount - 1) + "0] ";
			}
			return string.Format(format, sz);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SizeString(long sz)
        {
            return sz == 0 ? "           0" : string.Format("{0,12:#,###,###}", sz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SizeString(ulong sz)
        {
            return sz == 0 ? "           0" : string.Format("{0,12:#,###,###}", sz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SizeStringHeader(int sz)
        {
            return sz == 0 ? "[           0] " : string.Format("[{0,12:#,###,###}] ", sz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string CountStringHeader(int sz)
        {
            return sz == 0 ? "[       0] " : string.Format("[{0,8:#,###,###}] ", sz);
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string CountString(int sz)
		{
			return sz == 0 ? "0" : string.Format("{0:#,###,###}", sz);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SizeStringHeader(long sz)
        {
            return sz == 0 ? "[           0] " : string.Format("[{0,12:#,###,###}] ", sz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SmallIdHeader(int id)
        {
            return string.Format("[{0,06}] ", id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SmallNumberHeader(int num)
        {
            return string.Format("[{0,03}] ", num);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string LargeNumberString(int num)
        {
            return num == 0 ? Constants.ZeroStr : string.Format("{0:#,###,###,###}", num);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string LargeNumberString(long num)
        {
            return num == 0 ? Constants.ZeroStr : string.Format("{0:#,###,###,###}", num);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string LargeNumberString(ulong num)
        {
            return num == 0 ? Constants.ZeroStr : string.Format("{0:#,###,###,###}", num);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string TimeString(DateTime dt)
        {
            return dt.ToString("hh:mm:ss:fff");
        }

        public static string FormatBytes(long bytes)
        {
            const int scale = 1024;
            string[] orders = new string[] { "GB", "MB", "KB", "Bytes" };
            long max = (long)Math.Pow(scale, orders.Length - 1);
            foreach (string order in orders)
            {
                if (bytes > max)
                    return string.Format("{0:##.##} {1}", decimal.Divide(bytes, max), order);

                max /= scale;
            }
            return "0 Bytes";
        }

		public static string GetDigitsString(long val, int maxLen, char[] ary)
		{
			Debug.Assert(ary.Length==maxLen);

			long div = 10;
			for (int i = maxLen-1; i >= 0; --i)
			{
				long digit = val%div;
				ary[i] = (char)('0' + digit);
				val /= div;
				div *= 10;
			}
			return new string(ary);
		}

		#endregion Formatting

		#region Misc

		// TODO JRD -- handle IPV6
        public static long GetIpAddressValue(string ipaddr)
        {
			var addr = IPAddress.Parse(ipaddr);
			var bytes = addr.GetAddressBytes();
            //if (BitConverter.IsLittleEndian)
            //    Array.Reverse(bytes);

            if (bytes.Length > 8)
            {
                var ipnum = BitConverter.ToUInt64(bytes, 8);
                ipnum <<= 64;
                ipnum += BitConverter.ToUInt64(bytes, 0);
                return (long)ipnum;
            }
            else
            {
                var ipnum = BitConverter.ToUInt32(bytes, 0);
                return (long)ipnum;
            }
		}

		// TODO JRD -- handle IPV6
		public static string GetIpAddress(long addr)
        {
		    var bytes = addr > Int32.MaxValue ?	BitConverter.GetBytes(addr) : BitConverter.GetBytes((Int32)addr);
            //if (System.BitConverter.IsLittleEndian)
            //    Array.Reverse(bytes);
            if (bytes.Length > 8)
            {
                var ipnum = BitConverter.ToUInt64(bytes, 8);
                ipnum <<= 64;
                ipnum += BitConverter.ToUInt64(bytes, 0);
                return new IPAddress((long)ipnum).ToString();
            }
            else
            {
                var ipnum = BitConverter.ToUInt32(bytes, 0);
                return new IPAddress((long)ipnum).ToString();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasHexPrefix(string chars)
		{
			if (chars.Length > 2)
			{
				if (chars[0] == '0' && (chars[1] == 'x' || chars[1] == 'X'))
					return true;
			}
			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsHex(string chars)
		{
			int i = 0;
			if (HasHexPrefix(chars))
				i = 2;
			bool isHex;
			for (int icnt = chars.Length; i < icnt; ++i)
			{
				var c = chars[i];
				isHex = ((c >= '0' && c <= '9') ||
						 (c >= 'a' && c <= 'f') ||
						 (c >= 'A' && c <= 'F'));

				if (!isHex)
					return false;
			}
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsHexChar(char c)
		{
			return  ((c >= '0' && c <= '9') ||
						 (c >= 'a' && c <= 'f') ||
						 (c >= 'A' && c <= 'F'));
		}

        public static KeyValuePair<bool, ulong> GetFirstUlong(string str)
        {
            var ndx = str.IndexOf("0x", StringComparison.OrdinalIgnoreCase);
            if (ndx >= 0)
            {
                int charCount = 0;
                ndx += "0x".Length;
                int i = ndx;
                for (; i < str.Length; ++i)
                {
                    if (IsHexChar(str[i])) ++charCount;
                    else break;
                }
                if (charCount > 0 && (i == str.Length || char.IsWhiteSpace(str[i])))
                {
                    ulong val = Convert.ToUInt64(str.Substring(ndx, charCount), 16);
                    return new KeyValuePair<bool, ulong>(true, val);
                }
            }
            return new KeyValuePair<bool, ulong>(false, 0UL);
        }


        public static string GetCachedString(string str, Dictionary<string, string> cache)
		{
			string cachedStr;
			if (cache.TryGetValue(str, out cachedStr))
				return cachedStr;
			cache.Add(str,str);
			return str;
		}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNonValue(string val)
	    {
	        return !string.IsNullOrEmpty(val) && val[0] == Constants.NonValueChar;
	    }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsIndexInvalid(int ndx)
		{
			return Constants.InvalidIndex == ndx;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsInvalidAddress(ulong addr)
		{
			return addr == Constants.InvalidAddress;
		}

		private static int _id = 0;

		public static int GetNewID()
		{
			var id = Interlocked.Increment(ref _id);
			return id;
		}

		public static int NumberOfDigits(ulong val)
		{
			if (val >= 1000000000UL) return 10;
			int n = 1;
			if (val >= 100000000) { val /= 100000000; n += 8; }
			if (val >= 10000) { val /= 10000; n += 4; }
			if (val >= 100) { val /= 100; n += 2; }
			if (val >= 10) { val /= 10; n += 1; }
			return n;
		}

		public static int NumberOfDigits(int val)
		{
			int n = 1;
			if (val >= 100000000) { n += 8; val /= 100000000; }
			if (val >= 10000) { n += 4; val /= 10000; }
			if (val >= 100) { n += 2; val /= 100; }
			if (val >= 10) { n += 1; }
			return n;
		}

		public static int BitCount(uint c)
		{
			unchecked
			{
				c = c - ((c >> 1) & 0x55555555);
				c = (c & 0x33333333) + ((c >> 2) & 0x33333333);
				c = ((c + (c >> 4) & 0xF0F0F0F) * 0x1010101) >> 24;
			}
			return (int) c;
		}

		public static int Log2(int value)
		{
			int c = 0;
			while (value > 0)
			{
				c++;
				value >>= 1;
			}
			return c;
		}

		public static string GetNameWithoutId(string name)
		{
			if (string.IsNullOrWhiteSpace(name)) return name;
			int pos = name.IndexOf("__");
			if (pos <= 0) return name;
			return name.Substring(0, pos);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int RoundupToPowerOf2Boundary(int number, int powerOf2)
		{
			return (number + powerOf2 - 1) & ~(powerOf2 - 1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong RoundupToPowerOf2Boundary(ulong number, ulong powerOf2)
		{
			return (number + powerOf2 - 1) & ~(powerOf2 - 1);
		}

		public static bool IsWhiteSpace(string str)
		{
			if (str == null || str.Length == 0) return false;
			for (int i = 0, icnt = str.Length; i < icnt; ++i)
			{
				if (!Char.IsWhiteSpace(str[i])) return false;
			}
			return true;
		}

		public static bool IsSpaces(string str)
		{
			if (str == null || str.Length == 0) return false;
			for (int i = 0, icnt = str.Length; i < icnt; ++i)
			{
				if (str[i] != ' ') return false;
			}
			return true;
		}

		private const int scanSize = 256;

		/// <summary>
		/// Find index of the first entry with value = val.
		/// The array is grouped by values.
		/// </summary>
		public static int GetFirstIndex(int[] ary, int ndx, int count, int val)
		{
			if (ndx == 0) return 0;
			if (ary[ndx - 1] != val) return ndx;
			if (ndx == ary.Length - 1 || ary[ndx + 1] != val) return ndx - count + 1;

			int dist = scanSize;
			int fst = Math.Max(0, ndx - dist);
			while (fst > 0 && ary[fst] == val)
			{
				dist *= 2;
				fst = Math.Max(0, ndx - dist);
			}
			if (fst == 0 && ary[fst] == val) return 0;
			if (ndx - fst <= scanSize)
			{
				for (; fst < ndx; ++fst)
				{
					if (ary[fst] == val)
						return fst;
				}
				Debug.Assert(false, "Utils. GetFirstIndex -- cannot find the value?");
			}

			return -1; // TODO JRD
		}

	    public static int GetFirstLastValueIndices(int[] ary, int ndx, out int end)
	    {
	        int val = ary[ndx];
	        int first = ndx;
	        while (first > 0 && ary[first] == val) --first;
	        first = ary[first] == val ? first : --first;
	        end = ndx;
	        int aryEnd = ary.Length;
	        while (end < aryEnd && ary[end] == val) ++end;
	        return first;
	    }

		public static int[] CloneIntArray(int[] ary)
		{
			const int INT_SIZE = 4;

			var cnt = ary.Length;
			var nary = new int[cnt];
			Buffer.BlockCopy(ary, 0, nary, 0, cnt * INT_SIZE);
			return nary;
		}

		public static T[] CloneArray<T>(T[] ary)
		{
			var cnt = ary.Length;
			var nary = new T[cnt];
			Array.Copy(ary,nary,cnt);
			return nary;
		}

		public static int[] SortAndGetMap(int[] ary)
		{
			int cnt = ary.Length;
			var map = new int[cnt];
			for (var i = 0; i < cnt; ++i)
			{
				map[i] = i;
			}
			Array.Sort(ary, map);
			return map;
		}

		public static void InitArray<T>(T[] ary, T value)
		{
			for (int i = 0, icnt = ary.Length; i < icnt; ++i)
				ary[i] = value;
		}

		public static void Iota(int[] ary)
		{
			for (int i = 0, icnt = ary.Length; i < icnt; ++i)
				ary[i] = i;
		}

		public static int[] Iota(int cnt)
		{
			var ary = new int[cnt];
			for (int i = 0, icnt = cnt; i < icnt; ++i)
				ary[i] = i;
			return ary;
		}

		public static void GetPermutations(int[] list, int k, int m, List<int[]> result)
		{

			if (k == m)
			{
				result.Add(list.ToArray());
			}
			else
				for (int i = k; i <= m; i++)
				{
					Swap(ref list[k], ref list[i]);
					GetPermutations(list, k + 1, m, result);
					Swap(ref list[k], ref list[i]);
				}
		}

		private static void Swap(ref int a, ref int b)
		{
			if (a == b) return;

			a ^= b;
			b ^= a;
			a ^= b;
		}

		public static bool AreEqual(int[] ary1, int[] ary2)
		{
			if (ary1 == null && ary2 == null) return true;
			if (ary1 == null || ary2 == null) return false;
			if (ary1.Length != ary2.Length) return false;
			for (int i = 0, icnt = ary1.Length; i < icnt; ++i)
			{
				if (ary1[i] != ary2[i]) return false;
			}
			return true;
		}

		/// <summary>
		/// First array has all the elements of the second one.
		/// </summary>
		/// <param name="ary1">Super set, elements are sorted.</param>
		/// <param name="ary2">Subset of ary1, elements are sorted.</param>
		/// <returns>True if ary1 contains all the element of ary2.</returns>
		public static bool Contains(int[] ary1, int[] ary2)
		{
			if (ary1 == null && ary2 == null) return true;
			if (ary1 == null || ary2 == null) return false;
			Debug.Assert(IsSorted(ary1));
			Debug.Assert(IsSorted(ary2));
			if (ary1.Length < ary2.Length) return false;

			int ndx1 = 0, ndx2 = 0, len1 = ary1.Length, len2 = ary2.Length, found = 0;
			while (ndx1 < len1 && ndx2 < len2)
			{
				if (ary1[ndx1] < ary2[ndx2])
				{
					++ndx1;
					continue;
				}
				if (ary1[ndx1] > ary2[ndx2])
				{
					++ndx2;
					continue;
				}
				++ndx1;
				++ndx2;
				++found;
			}
			return found == len2;
		}

		public static bool IsSorted(IList<string> lst, out Tuple<string,string> badCouple)
		{
			badCouple = null;
			for (int i = 1, icnt = lst.Count; i < icnt; ++i)
			{
				if (string.Compare(lst[i - 1], lst[i], StringComparison.Ordinal) > 0)
				{
					badCouple = new Tuple<string, string>(lst[i-1],lst[i]);
					return false;
				}
			}
			badCouple = new Tuple<string, string>(string.Empty, string.Empty);
			return true;
		}

        public static bool IsSorted(IList<string> lst, out KeyValuePair<string, string>[] bad)
        {
            bad = null;
            var badlst = new List<KeyValuePair<string, string>>();
            for (int i = 1, icnt = lst.Count; i < icnt; ++i)
            {
                if (string.Compare(lst[i - 1], lst[i], StringComparison.Ordinal) > 0)
                {
                    badlst.Add(new KeyValuePair<string, string>(lst[i - 1], lst[i]));
                 }
            }
            bad = badlst.ToArray();
            return bad.Length < 1 ? true : false;
        }

		public static bool AreAddressesSorted(IList<ulong> lst)
		{
			for (int i = 1, icnt = lst.Count; i < icnt; ++i)
			{
				if (RealAddress(lst[i-1]) > RealAddress(lst[i])) return false;
			}
			return true;
		}

		public static bool IsSorted<T>(IList<T> lst) where T : System.IComparable<T>
		{
			for (int i = 1, icnt = lst.Count; i < icnt; ++i)
			{
				if (lst[i - 1].CompareTo(lst[i]) > 0) return false;
			}
			return true;
		}

		public static bool AreAllDistinct(ulong[] ary)
		{
			Debug.Assert(IsSorted(ary));
			for (int i = 1, icnt = ary.Length; i < icnt; ++i)
			{
				if (ary[i-1] == ary[i])
					return false;
			}
			return true;
		}

		public static bool AreAllDistinct(int[] ary)
		{
			Debug.Assert(IsSorted(ary));
			for (int i = 1, icnt = ary.Length; i < icnt; ++i)
			{
				if (ary[i - 1] == ary[i])
					return false;
			}
			return true;
		}

		public static int[] RemoveDuplicates(List<int> ary)
		{
			ary.Sort();
			List<int> lst = new List<int>(ary.Count);
			lst.Add(ary[0]);
			for (int i = 1, icnt = ary.Count; i < icnt; ++i)
			{
				if (ary[i - 1] == ary[i]) continue;
				lst.Add(ary[i]);
			}
			return lst.ToArray();
		}

		public static int[] RemoveDuplicates(int[] ary)
		{
			Debug.Assert(IsSorted(ary));
			List<int> lst = new List<int>(ary.Length);
			lst.Add(ary[0]);
			for (int i = 1; i < ary.Length; ++i)
			{
				if (ary[i - 1] == ary[i]) continue;
				lst.Add(ary[i]);
			}
			return lst.ToArray();
		}

		public static bool AreAllInExcept0(ulong[] main, ulong[] subAry)
		{
			Debug.Assert(IsSorted(main));
			Debug.Assert(IsSorted(subAry));
			Debug.Assert(main.Length >= subAry.Length);
			for (int i = 0, icnt = subAry.Length; i < icnt; ++i)
			{
				if (subAry[i] == 0) continue;
				var ndx = Utils.AddressSearch(main, subAry[i]);
				if (ndx < 0) return false;
			}
			return true;
		}

		public static bool DoesNotContain(ulong[] ary, ulong val)
		{
			for (int i = 0, icnt = ary.Length; i < icnt; ++i)
			{
				if (ary[i] == val) return false;
			}
			return true;
		}

		public static bool DoesNotContain(int[] ary, int val)
		{
			for (int i = 0, icnt = ary.Length; i < icnt; ++i)
			{
				if (ary[i] == val) return false;
			}
			return true;
		}

		public static bool IsSorted(IList<int> lst)
		{
			for (int i = 1, icnt = lst.Count; i < icnt; ++i)
			{
				if (lst[i - 1] > lst[i]) return false;
			}
			return true;
		}

		public static bool IsSorted<T>(IList<T> lst, IComparer<T> cmp)
		{
			for (int i = 1, icnt = lst.Count; i < icnt; ++i)
			{
				if (cmp.Compare(lst[i - 1],lst[i]) > 0) return false;
			}
			return true;
		}

		public static int RemoveDuplicates<T>(List<T> lst, IComparer<T> cmp)
		{
			Debug.Assert(IsSorted(lst,cmp));
			int removedCount = 0;
			for (int i = 1; i < lst.Count; ++i)
			{
				if (cmp.Compare(lst[i - 1], lst[i]) == 0)
				{
					lst.RemoveAt(i);
					--i;
					++removedCount;
				}
			}
			return removedCount;
		}

		/// <summary>
		/// Get all items in main array which are not in sub array.
		/// </summary>
		/// <param name="main">Instance od ulong array.</param>
		/// <param name="sub">Instance od ulong array.</param>
		/// <returns>Set difference.</returns>
		/// <remarks>Length of main array should be greater or equal to syb array length. Both arrays must be sorted.</remarks>
		public static ulong[] Difference(ulong[] main, ulong[] sub)
		{
			Debug.Assert(main!=null && sub!=null && main.Length >= sub.Length);
			Debug.Assert(Utils.IsSorted(main) && Utils.IsSorted(sub));
			var lst = new List<ulong>(main.Length - sub.Length);
			int sNdx = 0, sLen = sub.Length;
			for (int i = 0, icnt = main.Length; i < icnt; ++i)
			{
				var addr = main[i];
				if (sNdx < sLen)
				{
					if (addr == sub[sNdx])
					{
						++sNdx;
						continue;
					}
					if (addr < sub[sNdx])
					{
						lst.Add(addr);
						continue;
					}
					while (sNdx < sLen && addr > sub[sNdx]) ++sNdx;
					continue;
				}
				lst.Add(addr);
			}
			return lst.ToArray();
		}

		public static ulong[] MergeAddressesRemove0s(ulong[] ary1, ulong[] ary2)
		{
			Debug.Assert(IsSorted(ary1));
			Debug.Assert(IsSorted(ary2));
			ulong[] longer = ary1.Length < ary2.Length ? ary2 : ary1;
			ulong[] shorter = ary1.Length < ary2.Length ? ary1 : ary2;
			int lNdx = 0, sNdx = 0, lLen = longer.Length, sLen = shorter.Length;
			var lst = new List<ulong>(lLen +sLen);
			while (RealAddress(shorter[sNdx]) == 0UL) ++sNdx;
			while (RealAddress(longer[sNdx]) == 0UL) ++lNdx;

			while (lNdx < lLen || sNdx < sLen)
			{
				if (sNdx < sLen && lNdx < lLen)
				{
					var sAddr = RealAddress(shorter[sNdx]);
					var lAddr = RealAddress(longer[lNdx]);
					if (sAddr < lAddr)
					{
						AddUnique(lst, shorter[sNdx]);
						++sNdx;
						continue;
					}
					if (lAddr < sAddr)
					{
						AddUnique(lst, longer[lNdx]);
						++lNdx;
						continue;
					}
					AddUnique(lst, shorter[sNdx]);
					++sNdx;
					++lNdx;
					continue;
				}
				if (lNdx < lLen)
				{
					AddUnique(lst, longer[lNdx]);
					++lNdx;
					continue;
				}
				AddUnique(lst, shorter[sNdx]);
				++sNdx;
			}
			return lst.ToArray();
		}

		public static ulong[] Remove0sFromSorted(ulong[] ary)
		{
			if (ary == null) return ary;
			Debug.Assert(IsSorted(ary));
			int i = 0, len = ary.Length;

			for (; i < len; ++i)
			{
				if (ary[i] != 0UL) break;
			}
			if (i == 0) return ary;
			if (i == len) return Utils.EmptyArray<ulong>.Value;

			var newAry = new ulong[len - i];
			Buffer.BlockCopy(ary,i*sizeof(ulong), newAry, 0, len - i);
			return newAry;
		}

		public static bool CheckInverted(int[] head1, int[][] list1, int[] head2, int[][] list2, out int badHead1, out int badHead2)
		{
			badHead1 = Constants.InvalidIndex;
			badHead2 = Constants.InvalidIndex;
			for (int i = 0, icnt = head1.Length; i < icnt; ++i)
			{
				var lst1 = list1[i];
				for (int j = 0, jcnt = lst1.Length; j < jcnt; ++j)
				{
					var ndx1 = Array.BinarySearch(head2, lst1[j]);
					if (ndx1 < 0)
					{
						badHead1 = i;
						return false;
					}
					var lst2 = list2[ndx1];
					for (int k = 0, kcnt = lst2.Length; k < kcnt; ++k)
					{
						var ndx2 = Array.BinarySearch(head1, lst2[k]);
						if (ndx2 < 0)
						{
							badHead2 = ndx1;
							return false;
						}
					}
				}
			}
			return true;
		}

		public static void AddUnique(List<ulong> lst, ulong val)
		{
			int cnt = lst.Count;
			if (cnt > 0 && RealAddress(lst[cnt - 1]) == RealAddress(val)) return;
			lst.Add(val);
		}

		public static int[] GetIntArrayMapping(int[] ary, out int[] offs)
		{
			var cnt = ary.Length;
			var arySorted = new int[cnt];
			Array.Copy(ary,arySorted,cnt);
			var indices = Iota(cnt);
			Array.Sort(arySorted,indices);
			var offsets = new List<int>(Math.Min(cnt,1024*8));
			var curTypeId = 0;
			offsets.Add(0);
			for (int i = 0; i < cnt; ++i)
			{
				while (arySorted[i] != curTypeId)
				{
					offsets.Add(i);
					++curTypeId;
				}
			}
			offsets.Add(cnt);
			offs = offsets.ToArray();
			return indices;
		}

		public static int[] GetIdArray(int id, int[] ary, int[] map, int[] offsets)
		{
			var cnt = offsets[id + 1] - offsets[id];
			var outAry = new int[cnt];
			var offset = offsets[id];
			for (int i = 0; i < cnt; ++i)
			{
				outAry[i] = ary[map[offset]];
				++offset;
			}
			return outAry;
		}

		public static KeyValuePair<int, string>[] GetHistogram<T>(SortedDictionary<string, List<T>> dct)
		{
			KeyValuePair<int, string>[] hist =new KeyValuePair<int, string>[dct.Count];
			int ndx = 0;
			foreach (var kv in dct)
			{
				hist[ndx++] = new KeyValuePair<int, string>(
						kv.Value?.Count ?? 0,
						kv.Key
					);
			}
			// sort in descending order
			var cmp = Comparer<KeyValuePair<int,string>>.Create((a, b) =>
			{
				return a.Key < b.Key ? 1 : (a.Key > b.Key ? -1 : string.Compare(a.Value,b.Value,StringComparison.Ordinal));
			});
			Array.Sort(hist,cmp);
			return hist;
		}

		public static KeyValuePair<int, string>[] GetHistogram(SortedDictionary<string, int> dct)
		{
			KeyValuePair<int, string>[] hist = new KeyValuePair<int, string>[dct.Count];
			int ndx = 0;
			foreach (var kv in dct)
			{
				hist[ndx++] = new KeyValuePair<int, string>(
						kv.Value,
						kv.Key
					);
			}
			// sort in descending order
			var cmp = Comparer<KeyValuePair<int, string>>.Create((a, b) =>
			{
				return a.Key < b.Key ? 1 : (a.Key > b.Key ? -1 : string.Compare(a.Value, b.Value, StringComparison.Ordinal));
			});
			Array.Sort(hist, cmp);
			return hist;
		}

		public static int ConvertToInt(string str, int begin, int end)
		{
			int val = 0;
			for (; begin < end; ++begin)
				val = val*10 + (str[begin] - '0');
			return val;
		}

		public static long ConvertToLong(string str, int begin, int end)
		{
			long val = 0;
			for (; begin < end; ++begin)
				val = val * 10 + (str[begin] - '0');
			return val;
		}

		public static KeyValuePair<string, int>[] GetOrderedByValue(IDictionary<string, int> dct)
		{
			var ary = dct.ToArray();
			Array.Sort(ary,(a,b) => a.Value < b.Value ? -1 : (a.Value > b.Value? 1 : 0));
			return ary;
		}

		public static KeyValuePair<ulong, KeyValuePair<string, int>>[] GetOrderedByValue(IDictionary<ulong, KeyValuePair<string, int>> dct)
		{
			var ary = dct.ToArray();
			Array.Sort(ary, (a, b) => a.Value.Value < b.Value.Value ? -1 : (a.Value.Value > b.Value.Value ? 1 : 0));
			return ary;
		}

		public static KeyValuePair<string, int>[] GetOrderedByValueDesc(IDictionary<string, int> dct)
		{
			var ary = dct.ToArray();
			Array.Sort(ary, (a, b) => a.Value > b.Value ? -1 : (a.Value < b.Value ? 1 : 0));
			return ary;
		}

		public static KeyValuePair<ulong, KeyValuePair<string, int>>[] GetOrderedByValueDesc(IDictionary<ulong, KeyValuePair<string, int>> dct)
		{
			var ary = dct.ToArray();
			Array.Sort(ary, (a, b) => a.Value.Value > b.Value.Value ? -1 : (a.Value.Value < b.Value.Value ? 1 : 0));
			return ary;
		}

		public static T[] CopyToArray<T>(T firstItem, IList<T> rest)
		{

			T[] ary = new T[rest.Count+1];
			ary[0] = firstItem;
			for (int i = 0, icnt = rest.Count; i < icnt; ++i)
			{
				ary[i + 1] = rest[i];
			}
			return ary;
		}

		#region Errors Formatting

		public static string GetErrorString(string caption, string heading, string text, string details=null)
		{
			return (caption ?? string.Empty) + Constants.HeavyGreekCrossPadded
					+ (heading ?? string.Empty) + Constants.HeavyGreekCrossPadded
					+ (text ?? string.Empty) + Constants.HeavyGreekCrossPadded
					+ details ?? string.Empty;
		}

		public static string GetExceptionErrorString(Exception ex)
		{

			return ex.GetType().Name + Constants.HeavyGreekCrossPadded // caption
			       + ex.Source + Constants.HeavyGreekCrossPadded // heading
			       + ex.Message + Constants.HeavyGreekCrossPadded // text
			       + ex.StackTrace; // details;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsInformation(string str)
		{
			return !string.IsNullOrEmpty(str) && (str[0] == Constants.InformationSymbol);
		}

		//public static string ToRoman(int number)
		//{
		//	if ((number < 0) || (number > 3999)) throw new ArgumentOutOfRangeException("insert value betwheen 1 and 3999");
		//	var sb = StringBuilderCache.Acquire(32);
		//	while (number > 0)
		//	{
		//		if (number >= 1000) return "M" + ToRoman(number - 1000);  // 2160 Ⅰ ROMAN NUMERAL ONE
		//		if (number >= 900) return "CM" + ToRoman(number - 900); //EDIT: i've typed 400 instead 900
		//		if (number >= 500) return "D" + ToRoman(number - 500);
		//		if (number >= 400) return "CD" + ToRoman(number - 400);
		//		if (number >= 100) return "C" + ToRoman(number - 100);
		//		if (number >= 90) return "XC" + ToRoman(number - 90);
		//		if (number >= 50) return "L" + ToRoman(number - 50);
		//		if (number >= 40) return "XL" + ToRoman(number - 40);
		//		if (number >= 10) return "X" + ToRoman(number - 10);
		//		if (number >= 9) return "IX" + ToRoman(number - 9);
		//		if (number >= 5) return "V" + ToRoman(number - 5);
		//		if (number >= 4) return "IV" + ToRoman(number - 4);
		//		if (number >= 1) return "I" + ToRoman(number - 1); // 2170 ⅰ SMALL ROMAN NUMERAL ONE
		//	}
		//}
		#endregion Errors Formatting


		public static int VersionValue(Version version)
		{
			return version.Major*1000 + version.Minor*100 + (version.MinorRevision);
		}

		public static string VersionString(Version version)
		{
			return version.ToString();
		}

		/// <summary>
		/// Check if the index version matches current application.
		/// </summary>
		/// <param name="version">Version of this application.</param>
		/// <param name="indexInfoStr">Index general information as a string.</param>
		/// <returns>False if the index revision number is different then saved index revision.</returns>
		public static bool IsIndexVersionCompatible(Version version, string indexInfoStr)
		{
			const string verPrefix = "MDR Version: [";
			if (!indexInfoStr.StartsWith(verPrefix)) return false;
			var pos = indexInfoStr.IndexOf(']');
			var ver = indexInfoStr.Substring(verPrefix.Length, pos - verPrefix.Length);
			var parts = ver.Split('.');
			if (parts.Length < 4) return false;
			int revision;
			if (Int32.TryParse(parts[3],out revision))
			{
				return revision == version.Revision;
			}
			return false;
		}


		/// <summary>
		/// Return an empty array to avoid unnecessary memory allocation.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		public static class EmptyArray<T>
		{
			/// <summary>
			/// Static empty array of some type.
			/// </summary>
			public static readonly T[] Value = new T[0];
		}

  //      /// <summary>
  //      /// Return an empty list to avoid unnecessary memory allocation.
  //      /// </summary>
  //      /// <typeparam name="T"></typeparam>
  //      public static class EmptyList<T>
		//{
		//	/// <summary>
		//	/// Static empty list of some type.
		//	/// </summary>
		//	public static readonly List<T> Value = new List<T>(0);
		//}

		/// <summary>
		/// Force gc collection, and compact LOH.
		/// </summary>
		public static void ForceGcWithCompaction()
		{
			GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
			GC.Collect();
            GC.WaitForPendingFinalizers();
			GC.Collect();
		}


		public const int SizeDistributionLenght = 33;

		public static void AddSizeDistribution(int[] dist, ulong size)
		{
			if (size < 33) dist[0] += 1;
			else if (size < 65) dist[1] += 1;
			else if (size < 129) dist[2] += 1;
			else if (size < 257) dist[3] += 1;
			else if (size < 513) dist[4] += 1;
			else if (size < 1025) dist[5] += 1;
			else if (size < 2049) dist[6] += 1;
			else if (size < 4097) dist[7] += 1;
			else if (size < 8193) dist[8] += 1;
			else if (size < 16385) dist[9] += 1;
			else if (size < 32769) dist[10] += 1;
			else if (size < 65537) dist[11] += 1;
			else if (size < 131073) dist[12] += 1;
			else if (size < 262145) dist[13] += 1;
			else if (size < 524289) dist[14] += 1;
			else if (size < 1048577) dist[15] += 1;
			else if (size < 2097153) dist[16] += 1;
			else if (size < 4194305) dist[17] += 1;
			else if (size < 8388609) dist[18] += 1;
			else if (size < 16777217) dist[19] += 1;
			else if (size < 33554433) dist[20] += 1;
			else if (size < 67108865) dist[21] += 1;
			else if (size < 134217729) dist[22] += 1;
			else if (size < 268435457) dist[23] += 1;
			else if (size < 536870913) dist[24] += 1;
			else if (size < 1073741825) dist[25] += 1;
			else if (size < 2147483648) dist[26] += 1;
			else if (size < 1073741825) dist[27] += 1;
			else if (size < 4294967297) dist[28] += 1;
			else if (size < 8589934593) dist[29] += 1;
			else if (size < 17179869185) dist[30] += 1;
			else if (size < 34359738368) dist[31] += 1;
			else dist[32] += 1;
		}

		public static string GetWinDbgTypeName(string name)
		{
			return name.Replace('+', '_');
		}

		#endregion Misc
	}

	//internal class KeyValuePairComparer : Comparer<KeyValuePair<TKey, TValue>>
	//{
	//	internal IComparer<T> keyComparer;

	//	public KeyValuePairComparer(IComparer<T> keyComparer)
	//	{
	//		if (keyComparer == null)
	//		{
	//			this.keyComparer = Comparer<T>.Default;
	//		}
	//		else
	//		{
	//			this.keyComparer = keyComparer;
	//		}
	//	}

	//	public override int Compare(KeyValuePair<T, U> x, KeyValuePair<T, U> y)
	//	{
	//		return keyComparer.Compare(x.Key, y.Key);
	//	}
	//}

}
