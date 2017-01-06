using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using ClrMDRUtil.Utils;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	public class ClrtRootInfo
	{
		private readonly ulong[] _freachableQue; // is it? from ClrHeap.EnumerateFinalizerQueueObjectAddresses()
		private readonly ClrtRoot[][] _roots; // rooots by Kind
		private readonly ulong[] _rootAddresses; // unique root addresses, sorted, with out finalizer
		private readonly ulong[] _finalizerAddresses; // unique finalizer addresses, sorted, with out finalizer

		public ulong[] RootAddresses => _rootAddresses;
		public ulong[] FinalizerAddresses => _finalizerAddresses;


		public ClrtRootInfo(ClrtRoot[][] roots, ulong[] rootAddresses, ulong[] finalizerAddresses, ulong[] fque)
		{
			_roots = roots;
			_rootAddresses = rootAddresses;
			_finalizerAddresses = finalizerAddresses;
			_freachableQue = fque;
		}

		public static Tuple<ulong[],ulong[]> GetRootAddresses(int rtm, ClrHeap heap, string[] typeNames, StringIdDct strIds, DumpFileMoniker fileMoniker, out string error)
		{
			error = null;
			try
			{
				var roots = heap.EnumerateRoots(true);
				var addrDct = new Dictionary<ulong, ulong>();
				var finlDct = new Dictionary<ulong, ulong>();
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
						typeName = clrType == null ? null : clrType.Name;
					}
					else
					{
						typeName = root.Type.Name;
					}
					typeId = Array.BinarySearch(typeNames, typeName,StringComparer.Ordinal);
					if (typeId < 0) typeId = Constants.InvalidIndex;
					string rootName = root.Name ?? string.Empty;
					var nameId = strIds.JustGetId(rootName);
					var clrtRoot = new ClrtRoot(root, typeId, nameId);
					ourRoots[(int)root.Kind].Add(clrtRoot);
					
					if (rootAddr != 0Ul)
					{
						rootAddr = Utils.SetAsRoot(root.Address);
						ulong addr;
						if (addrDct.TryGetValue(root.Address, out addr))
						{
							addrDct[root.Address] = rootAddr | addr;
						}
						else
						{
							addrDct.Add(root.Address, rootAddr);
						}
					}
					if (objAddr != 0UL)
					{
						ulong addr;
						if (root.Kind == GCRootKind.Finalizer)
						{
							objAddr = Utils.SetAsFinalizer(objAddr);
							if (finlDct.TryGetValue(root.Object,out addr))
							{
								finlDct[root.Object] = objAddr | objAddr;
							}
							else
							{
								finlDct.Add(root.Object,objAddr);
							}
						}
						else
						{
							objAddr = Utils.SetAsRoot(objAddr);
							if (addrDct.TryGetValue(root.Object, out addr))
							{
								addrDct[root.Object] = addr | objAddr;
							}
							else
							{
								addrDct.Add(root.Object, objAddr);
							}
						}
					}
				}

				// TODO JRD -- does that make sense?
				//
				ulong[] fque = heap.EnumerateFinalizableObjectAddresses().ToArray();
				Array.Sort(fque);
				// root infos TODO JRD -- Fix this
				//
				var rootCmp = new ClrtRootObjCmp();
				for (int i = 0, icnt = (int)GCRootKind.Max + 1; i < icnt; ++i)
				{
					ourRoots[i].Sort(rootCmp);
				}
				// unique addresses
				//
				var addrAry = addrDct.Values.ToArray();
				var cmp = new Utils.AddressCmpAcs();
				Array.Sort(addrAry, cmp);

				var finlAry = finlDct.Values.ToArray();
				Array.Sort(finlAry, cmp);

				var result = new Tuple<ulong[], ulong[]>(addrAry, finlAry);

				if (!ClrtRootInfo.Save(rtm, ourRoots, result, fque, fileMoniker, out error)) return null;

				return result;

			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		#region save/load

		public static bool Save(int rtm, List<ClrtRoot>[] ourRoots, Tuple<ulong[], ulong[]> rootAddrInfo, ulong[] fque, DumpFileMoniker fileMoniker, out string error)
		{
			error = null;
			BinaryWriter bw = null;
			try
			{
				var path = fileMoniker.GetFilePath(rtm, Constants.MapRootsInfoFilePostfix);
				bw = new BinaryWriter(File.Open(path, FileMode.Create));

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

				bw.Write(rootAddrInfo.Item1.Length);
				for (int i = 0, icnt = rootAddrInfo.Item1.Length; i < icnt; ++i)
				{
					bw.Write(rootAddrInfo.Item1[i]);
				}

				bw.Write(rootAddrInfo.Item2.Length);
				for (int i = 0, icnt = rootAddrInfo.Item2.Length; i < icnt; ++i)
				{
					bw.Write(rootAddrInfo.Item2[i]);
				}

				bw.Write(fque.Length); // TODO JRD -- maybe not needed
				for (int i = 0, icnt = fque.Length; i < icnt; ++i)
				{
					bw.Write(fque[i]);
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

		public static ClrtRootInfo Load(int rtm, DumpFileMoniker fileMoniker, out string error)
		{
			error = null;
			BinaryReader bw = null;
			try
			{
				var path = fileMoniker.GetFilePath(rtm, Constants.MapRootsInfoFilePostfix);
				bw = new BinaryReader(File.Open(path, FileMode.Open));

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

				count = bw.ReadInt32();
				ulong[] rootAddresses = new ulong[count];
				for (int i = 0; i < count; ++i)
				{
					rootAddresses[i] = bw.ReadUInt64();
				}

				count = bw.ReadInt32();
				ulong[] finlAddresses = new ulong[count];
				for (int i = 0; i < count; ++i)
				{
					finlAddresses[i] = bw.ReadUInt64();
				}

				count = bw.ReadInt32(); // TODO JRD -- maybe not needed
				ulong[] fque = new ulong[count];
				for (int i = 0; i < count; ++i)
				{
					fque[i] = bw.ReadUInt64();
				}

				return new ClrtRootInfo(roots,rootAddresses,finlAddresses,fque);
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

		public void GetFinalizerInfo(string[] typeNames)
		{
			var finals = _roots[(int)GCRootKind.Finalizer];
			for (int i = 0, icnt = _roots.Length; i < icnt; ++i)
			{
				var typeName = typeNames[finals[i].TypeId];


			}
		}

		public ClrtRoot[] GetFinalizerItems()
		{
			return _roots[(int) GCRootKind.Finalizer];
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
			return (GCRootKind)((uint)kind >> 4);
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
			Address = raddr == 0UL ? 0UL : Utils.SetAsRoot(raddr);
			Object = (raddr == 0UL) ? obj : Utils.SetAsRootPointee(root.Object);
			
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
			return a.Object < b.Object ? -1 : (a.Object > b.Object ? 1 : 0);
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

}
