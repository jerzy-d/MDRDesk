﻿using System;
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
        #region address handling

        public struct RootBits
        {
            public static ulong Mask = 0xF000000000000000;
            public static ulong AddressMask = 0x0FFFFFFFFFFFFFFF;
            public static ulong Rooted = 0x8000000000000000;
            public static ulong Finalizer = 0x4000000000000000;
            public static ulong Root = 0x2000000000000000;
            public static ulong Local = 0x1000000000000000;
            public static ulong RootedMask = (Rooted | Root);
            public static ulong NotRootMask = ~(Root);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong CopyAddrFlag(ulong source, ulong target)
        {
            if ((source & RootBits.AddressMask) == (target & RootBits.AddressMask))
            {
                return source | target;
            }
            return target | ((source&RootBits.Mask) & ~RootBits.Root);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong SetAsRoot(ulong addr)
        {
            return addr |= RootBits.RootedMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong SetAsLocal(ulong addr)
        {
            return addr |= RootBits.Local;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong SetAsFinalizer(ulong addr)
        {
            return addr |= RootBits.Finalizer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong SetAsRooted(ulong addr)
        {
            return addr |= RootBits.Rooted;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong RealAddress(ulong addr)
        {
            return addr & RootBits.AddressMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsRealAddress(ulong addr)
        {
            return (addr & RootBits.Mask) == 0ul;
        }

        public static int[] GetAddressIndices(ulong[] addrs, ulong[] all)
        {
            Debug.Assert(Utils.AreAddressesSorted(addrs));
            Debug.Assert(Utils.AreAddressesSorted(all));
            List<int> lst = new List<int>(addrs.Length);
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
                ++lndx;

            }
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
                addresses[aNdx] |= bit;
                ++setCount;
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


        /// <summary>
        /// Format address for display showing its flags.
        /// </summary>
        /// <param name="addr">Instance address, should be flagged during reference building.</param>
        /// <returns>Formatted address string.</returns>
        /// <remarks>
        /// \u25BC ▼ : root, non-local variable
        /// \u2718 ✘ : in finalizer queue
        /// \u2731 ✱ : local variable
        /// \u2714 ✔ : unrooted
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetAddressFormat(ulong addr)
        {
            if (IsRoot(addr))
            {
                if (IsFinalizer(addr))
                {
                    return "\u25BC\u2718{0:x14}";
                }
                return "\u25BCx{0:x14}";
            }
            bool nonRooted = !IsNonRootRooted(addr);
            if (IsLocal(addr))
            {
                if (IsFinalizer(addr))
                {
                    if (nonRooted)
                    {
                        return "\u2731\u2714\u2718{0:x13}";
                    }
                    return "\u2731\u2718{0:x14}";
                }
                if (nonRooted)
                {
                    return "\u2731\u2714{0:x14}";
                }
                return "\u2731x{0:x14}";
            }
            if (nonRooted)
            {
                if (IsFinalizer(addr))
                {
                    return "\u2714\u2718{0:x14}";
                }
                return "\u2714x{0:x14}";
            }
            if (IsFinalizer(addr))
            {
                return "\u2718x{0:x14}";
            }
            return "0x{0:x14}";
        }

        /// <summary>
        /// Same as GetAddressFormat, see above, with the space attached at the end.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetAddressHeaderFormat(ulong addr)
        {
            if (IsRoot(addr))
            {
                if (IsFinalizer(addr))
                {
                    return "\u25BC\u2718{0:x14} ";
                }
                return "\u25BCx{0:x14} ";
            }
            bool nonRooted = !IsNonRootRooted(addr);
            if (IsLocal(addr))
            {
                if (IsFinalizer(addr))
                {
                    if (nonRooted)
                    {
                        return "\u2731\u2714\u2718{0:x13} ";
                    }
                    return "\u2731\u2718{0:x14} ";
                }
                if (nonRooted)
                {
                    return "\u2731\u2714{0:x14} ";
                }
                return "\u2731x{0:x14} ";
            }
            if (nonRooted)
            {
                if (IsFinalizer(addr))
                {
                    return "\u2714\u2718{0:x14} ";
                }
                return "\u2714x{0:x14} ";
            }
            if (IsFinalizer(addr))
            {
                return "\u2718x{0:x14} ";
            }
            return "0x{0:x14} ";
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string FlaggedAddressStringHeader(ulong addr)
        {
            return string.Format("0x{0:x16} ", addr);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong SetRooted(ulong addr)
        {
            return addr | (ulong)RootBits.Rooted;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinalizer(ulong addr)
        {
            return (addr & (ulong)RootBits.Finalizer) > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsRooted(ulong addr)
        {
            return (addr & (ulong)RootBits.RootedMask) > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNonRootRooted(ulong addr)
        {
            return (addr & (ulong)RootBits.Rooted) > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLocal(ulong addr)
        {
            return (addr & (ulong)RootBits.Local) > 0;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SameRealAddresses(ulong a1, ulong a2)
        {
            return RealAddress(a1) == RealAddress(a2);
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

        public class AddressComparison : IComparer<ulong>
        {
            public int Compare(ulong a, ulong b)
            {
                var reala = Utils.RealAddress(a);
                var realb = Utils.RealAddress(b);
                return reala < realb ? -1 : (reala > realb ? 1 : 0);
            }
        }

        public class AddressEqualityComparer : IEqualityComparer<ulong>
        {
            public bool Equals(ulong addr1, ulong addr2)
            {
                return RealAddress(addr1) == RealAddress(addr2);
            }

            public int GetHashCode(ulong addr)
            {
                return addr.GetHashCode();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong GetAddressValue(string addrStr)
        {
            if (string.IsNullOrEmpty(addrStr)) return Constants.InvalidAddress;
            try
            {
                return Convert.ToUInt64(addrStr.Substring(4), 16); // it might contain address flags
            }
            catch (FormatException)
            {
                return Constants.InvalidAddress;
            }
            catch (OverflowException)
            {
                return Constants.InvalidAddress;
            }
            catch (ArgumentException)
            {
                return Constants.InvalidAddress;
            }
        }

        #endregion address handling

        #region IO

        public static char[] DirSeps = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

        public static  string GetFileInfo(string path)
        {
            Debug.Assert(System.IO.File.Exists(path));
            System.IO.FileInfo fi = new System.IO.FileInfo(path);
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
            sb.Append(path).AppendLine();
            sb.Append("File size: ").AppendLine(Utils.SizeString(fi.Length));
            var dt = fi.LastWriteTime.ToUniversalTime();
            sb.Append("Last write time (UTC): ").Append(dt.ToLongDateString()).Append(" ").AppendLine(dt.ToLongTimeString());
            return StringBuilderCache.GetStringAndRelease(sb);
        }

        public static string GetFileSizeString(string path)
        {
            return Utils.SizeString(GetFileSize(path));
        }

        public static long GetFileSize(string path)
        {
            if (!System.IO.File.Exists(path)) return 0;
            System.IO.FileInfo fi = new System.IO.FileInfo(path);
            return fi.Length;
        }

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

        public static ulong[] ReadUlongArray(string path, out string error)
        {
            error = null;
            BinaryReader br = null;
            try
            {
                br = new BinaryReader(File.Open(path, FileMode.Open));
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

        public static long[] ReadLongArray(string path, int cnt, out string error)
        {
            error = null;
            BinaryReader br = null;
            try
            {
                br = new BinaryReader(File.Open(path, FileMode.Open));
                var ary = new long[cnt];
                for (int i = 0; i < cnt; ++i)
                {
                    ary[i] = br.ReadInt64();
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

        public static ClrElementKind[] ReadClrElementKindArray(string path, out string error)
        {
            error = null;
            BinaryReader br = null;
            try
            {
                br = new BinaryReader(File.Open(path, FileMode.Open));
                var cnt = br.ReadInt32();
                var ary = new ClrElementKind[cnt];
                for (int i = 0; i < cnt; ++i)
                {
                    ary[i] = (ClrElementKind)br.ReadInt32();
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

        public static ValueTuple<int[], int[]> ReadKvIntIntArrayAsTwoArrays(string path, out string error)
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
                return (ary1, ary2);
            }
            catch (Exception ex)
            {
                error = GetExceptionErrorString(ex);
                return (null,null);
            }
            finally
            {
                br?.Close();
            }
        }

        public static ValueTuple<int[], int[], ClrElementKind[]> ReadArrayInfos(string path, out string error)
        {
            error = null;
            BinaryReader br = null;
            try
            {
                br = new BinaryReader(File.Open(path, FileMode.Open));
                var cnt = br.ReadInt32();
                int[] ary1 = new int[cnt];
                int[] ary2 = new int[cnt];
                ClrElementKind[] ary3 = new ClrElementKind[cnt];
                for (int i = 0; i < cnt; ++i)
                {
                    ary1[i] = br.ReadInt32();
                    ary2[i] = br.ReadInt32();
                    ary3[i] = (ClrElementKind)br.ReadInt32();
                }
                return (ary1, ary2, ary3);
            }
            catch (Exception ex)
            {
                error = GetExceptionErrorString(ex);
                return (null, null, null);
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
                var ary = new KeyValuePair<int, int>[cnt];
                for (int i = 0; i < cnt; ++i)
                {
                    var key = br.ReadInt32();
                    var value = br.ReadInt32();
                    ary[i] = new KeyValuePair<int, int>(key, value);
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

        public static bool WriteArrayInfos(string path, IList<ValueTuple<int, int, ClrElementKind>> lst, out string error)
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
                    br.Write(kv.Item1);
                    br.Write(kv.Item2);
                    br.Write((int)(kv.Item3));
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

        public static bool WriteStringList(string filePath, IList<string> lst, out string error)
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

        public static void CloseStream(ref BinaryWriter s)
        {
            s?.Close();
            s = null;
        }

        #endregion IO

        #region String Utils

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ReplaceUriSpaces(string str)
        {
            return str.Replace("%20", " ");
        }

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
            if (bracketPos == 0) return name;

            int lastCharIndex = name.Length - 1;


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
                int p = name.IndexOf("+<>");
                if (p > 0)
                {
                    p = name.LastIndexOf('.', p);
                    if (p > 0)
                        return name.Substring(p + 1) + "+" + Constants.NamespaceSepPadded + name.Substring(0, p);
                }
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

        public static T[] CloneArray<T>(T[] ary)
        {
            var cnt = ary.Length;
            var nary = new T[cnt];
            for (int i = 0; i < cnt; ++i)
            {
                nary[i] = ary[i];
            }
            return nary;
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

        //public static string GetFancyIntStr(int val, int width)
        //{
        //    char[] digits = new char[width];
        //    for (int i = width - 1; i >= 0; --i)
        //    {
        //        if (val == 0) { digits[i] = '\u274D'; continue; }
        //        int rem = val % 10;
        //        digits[i] = rem == 0 ? '\u274D' : (char)(0x277F + rem);
        //        val /= 10;
        //    }
        //    return new string(digits);
        //}

        /// <summary>
        /// TODO JRD -- can I use this?
        /// </summary>
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

        public static string GetSubscriptIntStr(int val)
        {
            int width = NumberOfDigits(val);
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
        
        /// <summary>
        /// TODO JRD -- can I use this?
        /// </summary>
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

        public static string GetValidName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            bool firstLetterOk = char.IsLetter(name[0]) || name[0] == '_';
            bool needChange = false;
            for (int i = 0, icnt = name.Length; i < icnt; ++i)
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
                        return string.Format(@"\u{0:x4}", (uint)c);
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

        public static string GetShorterStringRemoveNewlines(string str, int len)
        {
            str = str.Replace(Environment.NewLine, Constants.HeavyRightArrowPadded);
            if (str.Length <= len) return str;
            return str.Substring(0, len - 1) + Constants.HorizontalEllipsisChar;
        }

        #endregion String Utils

        #region Comparers

        public class LambdaComparer<T> : IComparer<T>
        {
            readonly Func<T, T, int> _compareFunction;

            public LambdaComparer(Func<T, T, int> compareFunction)
            {
                _compareFunction = compareFunction;
            }

            public int Compare(T item1, T item2)
            {
                return _compareFunction(item1, item2);
            }
        }

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

        public static bool SameRealAddresses(ulong[] ary1, ulong[] ary2)
        {
            if (ary1 == null && ary2 == null) return true;
            if (ary1 == null || ary2 == null) return false;
            if (ary1.Length != ary2.Length) return false;
            for (int i = 0, icnt = ary1.Length; i < icnt; ++i)
            {
                if (RealAddress(ary1[i]) != RealAddress(ary2[i]))
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

        public class IntCmpDesc : IComparer<int>
        {
            public int Compare(int a, int b)
            {
                return a < b ? 1 : (a > b ? -1 : 0);
            }
        }

        //public class IntTripleCmp : IComparer<triple<int, String, ulong>>
        //{
        //    public int Compare(triple<int, String, ulong> a, triple<int, String, ulong> b)
        //    {
        //        return a.First < b.First ? -1 : (a.First > b.First ? 1 : 0);
        //    }
        //}

        public class KVStrStrCmp : IComparer<KeyValuePair<string, string>>
        {
            public int Compare(KeyValuePair<string, string> a, KeyValuePair<string, string> b)
            {
                int cmp = string.Compare(a.Key, b.Key, StringComparison.Ordinal);
                if (cmp == 0) cmp = string.Compare(a.Value, b.Value, StringComparison.Ordinal);
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

        public class KVIntUlongCmpAsc : IComparer<KeyValuePair<int, ulong>>
        {
            public int Compare(KeyValuePair<int, ulong> a, KeyValuePair<int, ulong> b)
            {
                if (a.Key == b.Key)
                    return a.Value > b.Value ? -1 : (a.Value < b.Value ? 1 : 0);
                return a.Key > b.Key ? -1 : (a.Key < b.Key ? 1 : 0);
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

        public class KvStrKvStrInt : IComparer<KeyValuePair<string, KeyValuePair<string, int>[]>>
        {
            public int Compare(KeyValuePair<string, KeyValuePair<string, int>[]> a,
                KeyValuePair<string, KeyValuePair<string, int>[]> b)
            {
                return string.Compare(a.Key, b.Key, StringComparison.Ordinal);
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
        public class KvIntStrDesc : IComparer<KeyValuePair<int, string>>
        {
            public int Compare(KeyValuePair<int, string> a, KeyValuePair<int, string> b)
            {
                if (a.Key == b.Key)
                    return string.Compare(a.Value, b.Value, StringComparison.Ordinal);
                return a.Key > b.Key ? -1 : 1;
            }
        }

        public class KvStrKindCmp : IComparer<KeyValuePair<string,ClrElementKind>>
        {
            public int Compare(KeyValuePair<string, ClrElementKind> a, KeyValuePair<string, ClrElementKind> b)
            {
                return string.Compare(a.Key, b.Key, StringComparison.Ordinal);
            }
        }

        public class KvStrKindEqCmp : IEqualityComparer<KeyValuePair<string, ClrElementKind>>
        {
            public bool Equals(KeyValuePair<string, ClrElementKind> a, KeyValuePair<string, ClrElementKind> b)
            {
                return string.Compare(a.Key, b.Key, StringComparison.Ordinal)==0;
            }

            public int GetHashCode(KeyValuePair<string, ClrElementKind> kv)
            {
                return kv.Key.GetHashCode();
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
            return string.Format("RT[{0}] ", index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string UintHexStringHeader(uint num)
        {
            return string.Format("[0x{0:x8}] ", num);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string HResultStringHeader(int num)
        {
            return string.Format("[0x{0:x8} {1,8:####}] ", (uint)num, num);
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
        public static string StopAndGetDurationStringAndRestart(Stopwatch stopWatch)
        {
            stopWatch.Stop();
            string duration = DurationString(stopWatch.Elapsed);
            stopWatch.Restart();
            return duration;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SortableLengthStringHeader(ulong len)
        {
            return len == 0 ? "00,000,000,000,000" : string.Format("[{0,14:0#,###,###,###}] ", len);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SortableSizeString(int sz)
        {
            return sz == 0 ? Constants.ZeroStr : string.Format("{0,12:0##,###,###}", sz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SortableSizeStringHeader(int sz)
        {
            return sz == 0 ? "[000,000,000,000] " : string.Format("[{0,12:0#,###,###}] ", sz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SortableCountStringHeader(int sz)
        {
            return sz == 0 ? "[00,000,000] " : string.Format("[{0,8:0#,###,###}] ", sz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SizeString(int sz)
        {
            return sz == 0 ? Constants.ZeroStr : string.Format("{0,12:#,###,###}", sz);
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
            return sz == 0 ? Constants.ZeroStr : string.Format("{0,12:#,###,###}", sz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SizeString(ulong sz)
        {
            return sz == 0 ? Constants.ZeroStr : string.Format("{0,12:#,###,###}", sz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string JustSizeString(ulong sz)
        {
            return sz == 0 ? Constants.ZeroStr : string.Format("{0:#,###,###}", sz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string CountStringHeader(int sz)
        {
            return sz == 0 ? "[          0] " : string.Format("[{0,11:#,###,###}] ", sz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string CountString(int sz)
        {
            return sz == 0 ? "0" : string.Format("{0:#,###,###}", sz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string CountStringPadded(int sz)
        {
            return sz == 0 ? " [       0] " : string.Format(" [{0,8:#,###,###}] ", sz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SizeStringHeader(long sz)
        {
            return sz == 0 ? "[           0] " : string.Format("[{0,12:#,###,###}] ", sz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string SizeStringHeader(int sz)
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

        #endregion Formatting

        #region Misc

        // TODO JRD -- handle IPV6
        public static long GetIpAddressValue(string ipaddr)
        {
            var addr = IPAddress.Parse(ipaddr);
            var bytes = addr.GetAddressBytes();

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
            var bytes = addr > Int32.MaxValue ? BitConverter.GetBytes(addr) : BitConverter.GetBytes((Int32)addr);
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
            return ((c >= '0' && c <= '9') ||
                         (c >= 'a' && c <= 'f') ||
                         (c >= 'A' && c <= 'F'));
        }

        public static string CleanupHexString(string hexStr)
        {
            if (hexStr == null || hexStr.Length < 1) return string.Empty;
            int last = hexStr.Length - 1;
            for (; last >= 0; --last)
            {
                if (IsHexChar(hexStr[last])) break;
            }
            if (last < 0) return string.Empty;
            int first = 0;
            for (; first < last; ++first)
            {
                char c = hexStr[first];
                if (c != '0' && IsHexChar(c)) break;
            }
            if (first >= last) return new string(hexStr[last], 1);
            return hexStr.Substring(first, last - first + 1);
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
            cache.Add(str, str);
            return str;
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

        public static int NumberOfDigits(int val)
        {
            int n = 1;
            if (val >= 100000000) { n += 8; val /= 100000000; }
            if (val >= 10000) { n += 4; val /= 10000; }
            if (val >= 100) { n += 2; val /= 100; }
            if (val >= 10) { n += 1; }
            return n;
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
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
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

        public static int[] Iota(int cnt, int init=0)
        {
            var ary = new int[cnt];
            for (int i = 0, icnt = cnt; i < icnt; ++i)
                ary[i] = init++;
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

        public static bool IsSorted(IList<string> lst, out Tuple<string, string> badCouple)
        {
            badCouple = null;
            for (int i = 1, icnt = lst.Count; i < icnt; ++i)
            {
                if (string.Compare(lst[i - 1], lst[i], StringComparison.Ordinal) > 0)
                {
                    badCouple = new Tuple<string, string>(lst[i - 1], lst[i]);
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
                if (RealAddress(lst[i - 1]) > RealAddress(lst[i])) return false;
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
                if (cmp.Compare(lst[i - 1], lst[i]) > 0) return false;
            }
            return true;
        }

        public static int RemoveDuplicates<T>(List<T> lst, IComparer<T> cmp)
        {
            Debug.Assert(IsSorted(lst, cmp));
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

        public static int[] GetIntArrayMapping(int[] ary, out int[] offs)
        {
            var cnt = ary.Length;
            var arySorted = new int[cnt];
            Array.Copy(ary, arySorted, cnt);
            var indices = Iota(cnt);
            Array.Sort(arySorted, indices);
            var offsets = new List<int>(Math.Min(cnt, 1024 * 8));
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
            KeyValuePair<int, string>[] hist = new KeyValuePair<int, string>[dct.Count];
            int ndx = 0;
            foreach (var kv in dct)
            {
                hist[ndx++] = new KeyValuePair<int, string>(
                        kv.Value?.Count ?? 0,
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
                val = val * 10 + (str[begin] - '0');
            return val;
        }

        public static long ConvertToLong(string str, int begin, int end)
        {
            long val = 0;
            for (; begin < end; ++begin)
                val = val * 10 + (str[begin] - '0');
            return val;
        }

        public static T[] GetArray<T>(IEnumerable<T> enumeration)
        {
            List<T> lst = new List<T>();
            foreach (var t in enumeration)
            {
                lst.Add(t);
            }
            if (lst.Count < 1) return Utils.EmptyArray<T>.Value;
            return lst.ToArray();
        }

        #region Errors Formatting

        public static string GetErrorString(string caption, string heading, string text, string details = null)
        {
            return (caption ?? string.Empty) + Constants.HeavyGreekCrossPadded
                    + (heading ?? string.Empty) + Constants.HeavyGreekCrossPadded
                    + (text ?? string.Empty) + Constants.HeavyGreekCrossPadded
                    + details ?? string.Empty;
        }

        public static string GetExceptionErrorString(Exception ex, string prefix = null)
        {
            string title = (prefix != null) ? prefix + ex.GetType().Name : ex.GetType().Name;
            return title + Constants.HeavyGreekCrossPadded // caption
                   + ex.Source + Constants.HeavyGreekCrossPadded // heading
                   + ex.Message + Constants.HeavyGreekCrossPadded // text
                   + ex.StackTrace; // details;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInformation(string str)
        {
            return !string.IsNullOrEmpty(str) && (str[0] == Constants.InformationSymbol || str[0] == Constants.MediumVerticalBar);
        }

        #endregion Errors Formatting

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
            if (Int32.TryParse(parts[3], out revision))
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

        /// <summary>
        /// Force gc collection, and compact LOH.
        /// </summary>
        public static void ForceGcWithCompaction()
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
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

        public static double GetStandardDeviation(IList<double> lst, out bool suspect, out double average)
        {
            suspect = false;
            average = 0.0;
            if (lst.Count < 2)
            {
                if (lst.Count == 1) average = lst[0];
                return 0.0;
            }
            double avg = lst.Average();
            double sumOfSquaresOfDifferences = lst.Select(val => (val - avg) * (val - avg)).Sum();
            double sd = Math.Sqrt(sumOfSquaresOfDifferences / lst.Count);
            if (sd > avg / 2) suspect = true;
            average = avg;
            return sd;
        }

        public static void Gnomesort<T>(T[] seq, Comparer<T> cmp = null)
        {
            Debug.Assert(seq != null);
            int len = seq.Length;
            if (len < 2) return;
            if (cmp == null) cmp = Comparer<T>.Default;
            for (int i = 0, icnt = seq.Length; i < icnt; )
            {
                if (i == 0 || cmp.Compare(seq[i - 1], seq[i]) <= 0) ++i;
                else
                {
                    T t = seq[i];
                    seq[i] = seq[i - 1];
                    seq[i - 1] = t;
                    --i;
                }
            }
        }

        public static void Gnomesort2<T>(T[] seq, Comparer<T> cmp = null)
        {
            Debug.Assert(seq != null);
            int len = seq.Length;
            if (len < 2) return;
            if (cmp == null) cmp = Comparer<T>.Default;
            for (int i = 1, icnt = seq.Length; i < icnt; ++i)
            {
                GnomesortImpl<T>(seq, i, cmp);
            }
        }

        public static void GnomesortImpl<T>(T[] seq, int pos, Comparer<T> cmp)
        {
            while (pos > 0 && cmp.Compare(seq[pos-1],seq[pos]) > 0)
            {
                T t = seq[pos];
                seq[pos] = seq[pos - 1];
                seq[pos - 1] = t;
                --pos;
            }
        }

        #endregion Misc
    }
}
