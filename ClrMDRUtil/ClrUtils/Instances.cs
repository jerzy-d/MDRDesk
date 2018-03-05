using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
    public class Instances
    {
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

        private ulong[] _addresses;
        public ulong[] Addresses => _addresses;
        private ulong[] _addresses2;
        public ulong[] Addresses2 => _addresses2;
        ulong _splitAddr;

        public Instances(ulong[][] addresses)
        {
            (int len1, int len2, int split) = Instances.GetAddressSplitCount(addresses);
            _addresses = new ulong[len1];
            _addresses2 = new ulong[len2];
            int off = 0;
            for (int i = 0; i < split; ++i)
            {
                int len = addresses[i].Length;
                Array.Copy(addresses[i], 0, _addresses, off, len);
                off += len;
            }
            _splitAddr = _addresses[addresses.Length - 1];
            off = 0;
            for (int i = split, icnt = addresses.Length; i < icnt; ++i)
            {
                int len = addresses[i].Length;
                Array.Copy(addresses[i], 0, _addresses2, off, len);
                off += len;
            }
        }

        public int Count => _addresses.Length + _addresses2.Length;

        public static ValueTuple<int,int,int> GetAddressSplitCount(ulong[][] addresses)
        {
            const int SplitCount = 250000000;
            int count1 = 0, count2 = 0;
            int i = 0;
            for (int icnt = addresses.Length; i < icnt; ++i)
            {
                int len = addresses[i].Length;
                if (count1 + len > SplitCount)
                {
                    break;
                }
                count1 += len;
            }

            int split = i;

            for (int icnt = addresses.Length; i < icnt; ++i)
            {
                int len = addresses[i].Length;
                count2 += len;
            }
            return new ValueTuple<int, int,int>(count1, count2,split);
        }

        public int AddressSearch(ulong addr)
        {
            return addr > _splitAddr
                ? AddressSearch(_addresses2, addr)
                : AddressSearch(_addresses, addr);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong RealAddress(ulong addr)
        {
            return addr & RootBits.AddressMask;
        }

    }
}
