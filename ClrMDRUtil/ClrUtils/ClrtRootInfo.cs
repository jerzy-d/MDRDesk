using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using ClrMDRUtil.Utils;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	public class ClrtRootInfo
	{
		private readonly ClrtRoot[][] _roots; // rooots by Kind

		public ClrtRootInfo(ClrtRoot[][] roots)
		{
			_roots = roots;
		}

		public ulong[] RootAddresses
		{
			get
			{
				HashSet<ulong> set = new HashSet<ulong>(new Utils.AddressEqualityComparer());
				for (int i = 0, icnt = _roots.Length; i < icnt; ++i)
				{
					if (i == (int)GCRootKind.Finalizer) continue;
					var ary = _roots[i];
					for (int j = 0, jcnt = ary.Length; j < jcnt; ++j)
					{
						set.Add(ary[j].Address);
					}
				}
				var result = set.ToArray();
				Array.Sort(result, new Utils.AddressComparison());
				return result;
			}
		}

		public ulong[] RootObjects
		{
			get
			{
				HashSet<ulong> set = new HashSet<ulong>(new Utils.AddressEqualityComparer());
				for (int i = 0, icnt = _roots.Length; i < icnt; ++i)
				{
					if (i == (int)GCRootKind.Finalizer) continue;
					var ary = _roots[i];
					for (int j = 0, jcnt = ary.Length; j < jcnt; ++j)
					{
						set.Add(ary[j].Object);
					}
				}
				var result = set.ToArray();
				Array.Sort(result, new Utils.AddressComparison());
				return result;
			}
		}

		public ulong[] FinalizerAddresses {
			get {
				HashSet<ulong> set = new HashSet<ulong>(new Utils.AddressEqualityComparer());
				var rootary =  _roots[(int)GCRootKind.Finalizer];
				var result = new ulong[rootary.Length];
				for (int j = 0, jcnt = rootary.Length; j < jcnt; ++j)
				{
					result[j] = rootary[j].Object;
				}
				Array.Sort(result, new Utils.AddressComparison());
				return result;
			}
		}

		public static Tuple<ulong[],ulong[]> GetRootAddresses(int rtm, ClrRuntime runTm, ClrHeap heap, string[] typeNames, StringIdDct strIds, DumpFileMoniker fileMoniker, out string error)
		{
			error = null;
			try
			{
				var roots = heap.EnumerateRoots(true);
				var objSet = new  HashSet<ulong>(new Utils.AddressEqualityComparer());
				var finlSet = new  HashSet<ulong>(new Utils.AddressEqualityComparer());
				List<ClrtRoot>[] ourRoots = new List<ClrtRoot>[(int)GCRootKind.Max + 1];
				for (int i = 0, icnt = (int)GCRootKind.Max + 1; i < icnt; ++i)
				{
					ourRoots[i] = new List<ClrtRoot>(256);
				}

				foreach (var root in roots)
				{
					ulong rootAddr = root.Address;
					ulong objAddr = root.Object;
					int typeId;
					string typeName;
					if (root.Type == null)
					{
						var clrType = heap.GetObjectType(objAddr);
						typeName = clrType == null ? Constants.UnknownName : clrType.Name;
					}
					else
					{
						typeName = root.Type.Name;
					}

					typeId = Array.BinarySearch(typeNames, typeName,StringComparer.Ordinal);
					if (typeId < 0) typeId = Constants.InvalidIndex;
					string rootName = root.Name == null ? Constants.UnknownName : root.Name;

					var nameId = strIds.JustGetId(rootName);
					var clrtRoot = new ClrtRoot(root, typeId, nameId);

					ourRoots[(int)root.Kind].Add(clrtRoot);
					
					if (objAddr != 0UL)
					{
						ulong addr;
						if (root.Kind == GCRootKind.Finalizer)
						{
							objAddr = Utils.SetAsFinalizer(objAddr);
							finlSet.Add(objAddr);
						}
						else
						{
							objAddr = Utils.SetRooted(objAddr);
							objSet.Add(objAddr);
						}
					}
				}

				// root infos TODO JRD -- Fix this
				//
				var rootCmp = new ClrtRootObjCmp();
				for (int i = 0, icnt = (int)GCRootKind.Max + 1; i < icnt; ++i)
				{
					ourRoots[i].Sort(rootCmp);
				}

				// unique addresses
				//
				var addrAry = objSet.ToArray();
				var cmp = new Utils.AddressCmpAcs();
				Array.Sort(addrAry, new Utils.AddressComparison());

				var finlAry = finlSet.ToArray();
				Array.Sort(finlAry, new Utils.AddressComparison());

				var result = new Tuple<ulong[], ulong[]>(addrAry, finlAry);

				if (!ClrtRootInfo.Save(rtm, ourRoots, fileMoniker, out error)) return null;

				return result;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public static ValueTuple<ulong[], ulong[]> SetupRootAddresses(int rtm, ClrRuntime runTm, ClrHeap heap, string[] typeNames, StringIdDct strIds, DumpFileMoniker fileMoniker, out string error)
		{
			error = null;
			try
			{
				var roots = heap.EnumerateRoots(true);
				var objSet = new HashSet<ulong>(new Utils.AddressEqualityComparer());
				var finlSet = new HashSet<ulong>(new Utils.AddressEqualityComparer());
				List<ClrtRoot>[] ourRoots = new List<ClrtRoot>[(int)GCRootKind.Max + 1];
				for (int i = 0, icnt = (int)GCRootKind.Max + 1; i < icnt; ++i)
				{
					ourRoots[i] = new List<ClrtRoot>(256);
				}

				foreach (var root in roots)
				{
					ulong rootAddr = root.Address;
					ulong objAddr = root.Object;
					int typeId;
					string typeName;
					if (root.Type == null)
					{
						var clrType = heap.GetObjectType(objAddr);
						typeName = clrType == null ? Constants.UnknownName : clrType.Name;
					}
					else
					{
						typeName = root.Type.Name;
					}

					typeId = Array.BinarySearch(typeNames, typeName, StringComparer.Ordinal);
					if (typeId < 0) typeId = Constants.InvalidIndex;
					string rootName = root.Name == null ? Constants.UnknownName : root.Name;

					var nameId = strIds.JustGetId(rootName);
					var clrtRoot = new ClrtRoot(root, typeId, nameId);

					ourRoots[(int)root.Kind].Add(clrtRoot);

 					if (objAddr != 0UL)
					{
						if (root.Kind == GCRootKind.Finalizer)
						{
							objAddr = Utils.SetAsFinalizer(objAddr);
							finlSet.Add(objAddr);
						}
						else
						{
							objAddr = Utils.SetRooted(objAddr);
							objSet.Add(objAddr);
						}
					}
                    if (rootAddr != 0UL)
                    {
                        if (root.Kind == GCRootKind.Finalizer)
                        {
                            rootAddr = Utils.SetAsFinalizer(rootAddr);
                            finlSet.Add(rootAddr);
                        }
                        else
                        {
                            rootAddr = Utils.SetRooted(rootAddr);
                            objSet.Add(rootAddr);
                        }
                    }

                }

				// root infos TODO JRD -- Fix this
				//
				var rootCmp = new ClrtRootObjCmp();
				for (int i = 0, icnt = (int)GCRootKind.Max + 1; i < icnt; ++i)
				{
					ourRoots[i].Sort(rootCmp);
				}

				// unique addresses
				//
				var addrAry = objSet.ToArray();
				Array.Sort(addrAry, new Utils.AddressComparison());

				var finlAry = finlSet.ToArray();
				Array.Sort(finlAry, new Utils.AddressComparison());

                if (!ClrtRootInfo.Save(rtm, ourRoots, fileMoniker, out error)) return new ValueTuple<ulong[],ulong[]>(null,null);
                return new ValueTuple<ulong[], ulong[]>(addrAry, finlAry);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return new ValueTuple<ulong[],ulong[]>(null,null);
			}
		}

		public ValueTuple<ClrtRoot[], ulong[], ClrtRoot[], ulong[]> GetRootObjectSearchList()
        {
            List<ClrtRoot> clrtAddrLst = new List<ClrtRoot>(1024 * 10);
            List<ulong> rootAddrLst = new List<ulong>(1024 * 10);
            List<ClrtRoot> clrtObjLst = new List<ClrtRoot>(1024 * 10);
            List<ulong> objAddrLst = new List<ulong>(1024 * 10);

            for (int i = 0, icnt = _roots.Length; i < icnt; ++i)
            {
                var ary = _roots[i];
                for (int j = 0, jcnt = ary.Length; j < jcnt; ++j)
                {
                    var r = ary[j];
                    if (r.Address != Constants.InvalidAddress)
                    {
                        rootAddrLst.Add(Utils.RealAddress(r.Address));
                        clrtAddrLst.Add(r);
                    }
                    if (r.Object != Constants.InvalidAddress)
                    {
                        objAddrLst.Add(Utils.RealAddress(r.Object));
                        clrtObjLst.Add(r);
                    }
                }
            }
            var clrtAddrLstAry = clrtAddrLst.ToArray();
            var rootAddrLstAry = rootAddrLst.ToArray();
            Array.Sort(rootAddrLstAry, clrtAddrLstAry,new Utils.AddressComparison());

            var clrtObjLstAry = clrtObjLst.ToArray();
            var objAddrLstAry = objAddrLst.ToArray();
            Array.Sort(objAddrLstAry, clrtObjLstAry, new Utils.AddressComparison());

            return new ValueTuple<ClrtRoot[], ulong[], ClrtRoot[], ulong[]>(clrtAddrLstAry, rootAddrLstAry, clrtObjLstAry, objAddrLstAry);
        }

        #region save/load

        public static bool Save(int rtm, List<ClrtRoot>[] ourRoots, DumpFileMoniker fileMoniker, out string error)
		{
			error = null;
			BinaryWriter bw = null;
			try
			{
				var path = fileMoniker.GetFilePath(rtm, Constants.MapRootsInfoFilePostfix);
				bw = new BinaryWriter(File.Open(path, FileMode.Create));

				// root details by kind
				//
				bw.Write(ourRoots.Length);
				for (int i = 0, icnt = ourRoots.Length; i < icnt; ++i)
				{
					var kindRoots = ourRoots[i];
					bw.Write(kindRoots.Count);
					for (int j = 0, jcnt = kindRoots.Count; j < jcnt; ++j)
					{
						kindRoots[j].Dump(bw);
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

		public static bool FinalyzerAddressFixup(int rtm, ulong[] finalyzerAddresses, DumpFileMoniker fileMoniker, out string error)
		{
			error = null;
			FileWriter fw = null;
			try
			{
				var path = fileMoniker.GetFilePath(rtm, Constants.MapRootsInfoFilePostfix);
				fw = new FileWriter(path, FileMode.Open, FileAccess.Write, FileShare.None);
				fw.Seek(sizeof (int), SeekOrigin.Begin);

				for (int i = 0, icnt = finalyzerAddresses.Length; i < icnt; ++i)
				{
					fw.Write(finalyzerAddresses[i]);
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
				fw?.Dispose();
			}
		}

		public static ClrtRootInfo Load(int rtm, DumpFileMoniker fileMoniker, out string error)
		{
			error = null;
			BinaryReader bw = null;
			try
			{
				var path = fileMoniker.GetFilePath(rtm, Constants.MapRootsInfoFilePostfix);
				bw = new BinaryReader(File.Open(path, FileMode.Open));

				// root details
				//
				int count = bw.ReadInt32();
				ClrtRoot[][] roots = new ClrtRoot[count][];
				for (int i = 0; i < count; ++i)
				{
					int kindCount = bw.ReadInt32();
					roots[i] = new ClrtRoot[kindCount];
					for (int j = 0; j < kindCount; ++j)
					{
						roots[i][j] = ClrtRoot.Load(bw);
					}
				}
				return new ClrtRootInfo(roots);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				bw?.Close();
			}
		}

        #endregion save/load


        public ClrtRoot[] GetFinalizerItems()
		{
			return _roots[(int)GCRootKind.Finalizer];
		}
		public ClrtRoot[] GetItems(GCRootKind kind)
		{
			return _roots[(int)kind]==null ? Utils.EmptyArray<ClrtRoot>.Value : _roots[(int)kind];
		}

		public ulong[] GetAddresses(GCRootKind kind)
		{
			var ary = GetItems(kind);
			if (ary.Length < 1) return Utils.EmptyArray<ulong>.Value;
			ulong[] addrs = new ulong[ary.Length];
			for (int i = 0, icnt = ary.Length; i < icnt; ++i)
			{
				addrs[i] = ary[i].Address;
			}
			Array.Sort(addrs, new Utils.AddressComparison());
			return addrs;
		}
		public ulong[] GetObjects(GCRootKind kind)
		{
			var ary = GetItems(kind);
			if (ary.Length < 1) return Utils.EmptyArray<ulong>.Value;
			ulong[] addrs = new ulong[ary.Length];
			for (int i = 0, icnt = ary.Length; i < icnt; ++i)
			{
				addrs[i] = ary[i].Object;
			}
			Array.Sort(addrs, new Utils.AddressComparison());
			return addrs;
		}

		public KeyValuePair<ulong,ulong>[] GetAddressObjectPairsUnordered(GCRootKind kind)
		{
			var ary = GetItems(kind);
			if (ary.Length < 1) return Utils.EmptyArray<KeyValuePair<ulong, ulong>>.Value;
			KeyValuePair<ulong, ulong>[] addrs = new KeyValuePair<ulong, ulong>[ary.Length];
			for (int i = 0, icnt = ary.Length; i < icnt; ++i)
			{
				addrs[i] = new KeyValuePair<ulong, ulong>(ary[i].Address,ary[i].Object);
			}
			return addrs;
		}
	}

	public struct ClrtRoot
	{
		[Flags]
		public enum Kinds
		{
			None = 0,

			/// <summary>
			/// the Object is an "interior" pointer.  This means that the pointer may actually
			/// point inside an object instead of to the start of the object.
			/// </summary>
			Interior = 1,

			/// <summary>
			/// The root "pins" the object, preventing the GC from relocating it.
			/// </summary>
			Pinned = 1 << 1,

			/// <summary>
			/// Unfortunately some versions of the APIs we consume do not give us perfect information.  If
			/// this property is true it means we used a heuristic to find the value, and it might not
			/// actually be considered a root by the GC.
			/// </summary>
			PossibleFalsePositive = 1 << 2,


			StaticVar = 1 << 4,
			ThreadStaticVar = 1 << 5,
			LocalVar = 1 << 6,
			Strong = 1 << 7,
			Weak = 1 << 8,
			Pinning = 1 << 9,
			Finalizer = 1 << 10,
			AsyncPinning = 1 << 11,
			Max = 1 << 11,
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Kinds Convert(GCRootKind kind)
		{
			return (Kinds)(1 << (4 + (int)kind));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Kinds GetGCRootKindPart(Kinds kind)
		{
			return (Kinds)((uint)kind & 0xFFFFFE00);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static GCRootKind GetGCRootKind(Kinds kind)
		{
            Kinds k = (Kinds)((uint)kind & 0xFFFFFFF0);

			switch (k)
            {
                case Kinds.StaticVar:
                    return GCRootKind.StaticVar;
                case Kinds.ThreadStaticVar:
                    return GCRootKind.ThreadStaticVar;
                case Kinds.LocalVar:
                    return GCRootKind.LocalVar;
                case Kinds.Strong:
                    return GCRootKind.Strong;
                case Kinds.Weak:
                    return GCRootKind.Weak;
                case Kinds.Pinning:
                    return GCRootKind.Pinning;
                case Kinds.Finalizer:
                    return GCRootKind.Finalizer;
                case Kinds.AsyncPinning:
                    return GCRootKind.AsyncPinning;
            }
            throw new ArgumentException("[ClrtRoot.GetGCRootKind]");
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string GetKindString()
		{
			return GetGCRootKind(RootKind).ToString();
		}

		public readonly ulong Address;
		public readonly ulong Object;
		public readonly int TypeId; // The type of the object this root points to.  That is, ClrHeap.GetObjectType(ClrRoot.Object).
		public readonly int NameId;
		public readonly Kinds RootKind;

		public readonly int DomainId; // it is Constant.InvalidIndex if there is no AppDomain associated with the root
		public readonly uint OsThreadId; // if the root does not have a thread associated with it, this will be Constant.InvalidIndex

		public readonly int ManagedThreadId; // if the root does not have a thread associated with it, this will be Constant.InvalidIndex

		public static ClrtRoot EmptyClrtRoot = new ClrtRoot(Constants.InvalidAddress, Constants.InvalidAddress, Constants.InvalidIndex, Constants.InvalidIndex, Constants.InvalidIndex, Constants.InvalidThreadId, Constants.InvalidIndex, Kinds.None);

		public static bool IsDummyRoot(ClrtRoot root)
		{
			return root.Address == Constants.InvalidAddress && root.Object == Constants.InvalidAddress;
		}

		public ClrtRoot(ClrtRoot root)
		{
			Address = root.Address;
			Object = root.Object;
			TypeId = root.TypeId;
			NameId = root.DomainId;
			RootKind = root.RootKind;
			DomainId = root.DomainId;
			OsThreadId = root.OsThreadId;
			ManagedThreadId = root.ManagedThreadId;
		}


		public ClrtRoot(ClrRoot root, int typeId, int nameId)
		{
			ulong raddr = root.Address;
			ulong obj = root.Object;
			Address = raddr == 0UL ? 0UL : Utils.SetRooted(raddr);
			Object = (raddr == 0UL) ? obj : Utils.SetRooted(root.Object);
			
			TypeId = typeId;
			NameId = nameId;
			DomainId = root.AppDomain != null ? root.AppDomain.Id : Constants.InvalidIndex;
			OsThreadId = root.Thread != null ? root.Thread.OSThreadId : Constants.InvalidThreadId;
			ManagedThreadId = root.Thread != null ? root.Thread.ManagedThreadId : Constants.InvalidIndex; ;

			int traits = (int)Convert(root.Kind);
			if (root.IsInterior) traits |= (int)Kinds.Interior;
			if (root.IsPinned) traits |= (int)Kinds.Pinned;
			if (root.IsPossibleFalsePositive) traits |= (int)Kinds.PossibleFalsePositive;
			RootKind = (Kinds)traits;
		}

		public ClrtRoot(ClrRoot root, int typeId, int nameId, int domainId, uint osthreadId, int managedThrdId)
		{
			Address = root.Address;
			Object = root.Object;
			TypeId = typeId;
			NameId = nameId;
			DomainId = domainId;
			OsThreadId = osthreadId;
			ManagedThreadId = managedThrdId;

			int traits = (int)Convert(root.Kind);
			if (root.IsInterior) traits |= (int)Kinds.Interior;
			if (root.IsPinned) traits |= (int)Kinds.Pinned;
			if (root.IsPossibleFalsePositive) traits |= (int)Kinds.PossibleFalsePositive;
			RootKind = (Kinds)traits;
		}

		private ClrtRoot(ulong address, ulong @object, int typeId, int nameId, int domainId, uint osthreadId, int managedThrdId, Kinds trait)
		{
//			return new ClrtRoot(address, @object, typeId, nameId, domainId, osThreadId, managedThreadId, rootTraits);
			Address = address;
			Object = @object;
			TypeId = typeId;
			NameId = nameId;
			DomainId = domainId;
			OsThreadId = osthreadId;
			ManagedThreadId = managedThrdId;
			RootKind = trait;
		}

        #region io

        public void Dump(BinaryWriter bw)
		{
			bw.Write(Address);
			bw.Write(Object);
			bw.Write(TypeId);
			bw.Write(NameId);
			bw.Write(DomainId);
			bw.Write(OsThreadId);
			bw.Write(ManagedThreadId);
			bw.Write((int)RootKind);
		}

		public static ClrtRoot Load(BinaryReader br)
		{
			ulong address = br.ReadUInt64();
			ulong @object = br.ReadUInt64();
			int typeId = br.ReadInt32();
			int nameId = br.ReadInt32();
			int domainId = br.ReadInt32();
			uint osThreadId = br.ReadUInt32();
			int managedThreadId = br.ReadInt32();
			Kinds rootTraits = (Kinds)br.ReadInt32();
			return new ClrtRoot(address, @object, typeId, nameId, domainId, osThreadId, managedThreadId, rootTraits);
		}

        #endregion io

    }


	public class ClrtRootObjCmp : IComparer<ClrtRoot>
	{
		public int Compare(ClrtRoot a, ClrtRoot b)
		{
			if (a.TypeId == b.TypeId)
			{
				var aObj = Utils.RealAddress(a.Object);
				var bObj = Utils.RealAddress(b.Object);
				return aObj < bObj ? -1 : (aObj > bObj ? 1 : 0);
			}
			return a.TypeId < b.TypeId ? -1 : 1;
		}
	}

	public class ClrRootObjCmp : IComparer<ClrRoot>
	{
		public int Compare(ClrRoot a, ClrRoot b)
		{
			var aObj = Utils.RealAddress(a.Object);
			var bObj = Utils.RealAddress(b.Object);
			return aObj < bObj ? -1 : (aObj > bObj ? 1 : 0);
		}
	}

	public class ClrRootNameCmp : IComparer<ClrRoot>
	{
		public int Compare(ClrRoot a, ClrRoot b)
		{
			var aname = a.Name ?? string.Empty;
			var bname = b.Name ?? string.Empty;
			int cmp = string.Compare(aname, bname, StringComparison.Ordinal);
			if (cmp == 0)
			{
				aname = a.Type == null ? string.Empty : (a.Type.Name ?? string.Empty);
				bname = b.Type == null ? string.Empty : (b.Type.Name ?? string.Empty);
				return string.Compare(aname, bname, StringComparison.Ordinal);
			}
			return cmp;
		}
	}

	public class ClrRootEqualityComparer : IEqualityComparer<ClrRoot>
	{
		public bool Equals(ClrRoot r1, ClrRoot r2)
		{
			if (r2 == null && r1 == null)
				return true;
			if (r1 == null | r2 == null)
				return false;
			return r1.Object == r2.Object;
		}

		public int GetHashCode(ClrRoot r)
		{
			return r.Object.GetHashCode();
		}
	}

    public class ClrtRootEqualityComparer : IEqualityComparer<ClrtRoot>
    {
        public bool Equals(ClrtRoot r1, ClrtRoot r2)
        {
            if (Utils.RealAddress(r1.Object) == Utils.RealAddress(r2.Object))
                return Utils.RealAddress(r1.Address) == Utils.RealAddress(r2.Address);
            return false;
        }

        public int GetHashCode(ClrtRoot r)
        {
            return Utils.RealAddress(r.Object).GetHashCode() ^ Utils.RealAddress(r.Address).GetHashCode() ^ 17;
        }
    }
}
