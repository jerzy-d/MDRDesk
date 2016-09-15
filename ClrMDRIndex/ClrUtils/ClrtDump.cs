using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.IO;
using Microsoft.Diagnostics.Runtime;
using Microsoft.SqlServer.Server;

namespace ClrMDRIndex
{
	public class ClrtDump : IDisposable
	{
		#region Fields/Properties

		private readonly string _dumpPath;
		private DataTarget _dataTarget;
		private ClrRuntime[] _runtimes;
		private ClrInfo[] _clrInfos;
		private string[] _dacPaths;
		private string[] _requiredDacs;
		private int _curRuntimeIndex;

		public DataTarget DataTarget => _dataTarget;
		public string DumpPath => _dumpPath;
		public string DumpFileName => Path.GetFileName(_dumpPath);
		public string DumpFileNameNoExt => Path.GetFileNameWithoutExtension(_dumpPath);
		public ClrRuntime[] Runtimes => _runtimes;
		public string[] DacPaths => _dacPaths;
		public ClrInfo[] ClrInfos => _clrInfos;

		public int RuntimeCount => _runtimes.Length;
		public ClrRuntime Runtime => _runtimes[_curRuntimeIndex];

		#endregion Fields/Properties

		#region Ctors/Initializations

		public ClrtDump(string dmpPath)
		{
			_dumpPath = dmpPath;
			_curRuntimeIndex = Constants.InvalidIndex;
		}

		public bool Init(out string error)
		{
			error = null;
			try
			{
				_dataTarget = DataTarget.LoadCrashDump(_dumpPath);
				if (_dataTarget == null)
				{
					error = "ERROR [Runtime.Init] DataTarget.LoadCrashDump returned null.";
					return false;
				}
				// Now check bitness of our program/target:
				bool isTarget64Bit = _dataTarget.PointerSize == 8;
				if (Environment.Is64BitProcess != isTarget64Bit)
				{
					error = string.Format("Architecture mismatch:  Process is {0} but target is {1}",
						Environment.Is64BitProcess ? "64 bit" : "32 bit", isTarget64Bit ? "64 bit" : "32 bit");
					_dataTarget.Dispose();
					_dataTarget = null;
					return false;
				}

				var clrInfoLst = new List<ClrInfo>();
				var requiredDacs = new List<string>();
				foreach (var version in _dataTarget.ClrVersions)
				{
					clrInfoLst.Add(version);
					requiredDacs.Add(version.DacInfo.FileName);
				}
				if (clrInfoLst.Count < 1)
				{
					error = "ERROR [Runtime.Init] DataTarget.ClrVersions list is empty.";
					return false;
				}

				_curRuntimeIndex = Constants.InvalidIndex;
				_requiredDacs = requiredDacs.ToArray();
				_clrInfos = clrInfoLst.ToArray();
				clrInfoLst.Clear();
				_dacPaths = new string[_requiredDacs.Length];
				_runtimes = new ClrRuntime[_requiredDacs.Length];

				if (!string.IsNullOrWhiteSpace(Setup.DacPathFolder))
				{
					for (int i = 0, icnt = _clrInfos.Length; i < icnt; ++i)
					{
						_dacPaths[i] = Utils.SearchDacFolder(_requiredDacs[i], Setup.DacPathFolder);
						if (!string.IsNullOrWhiteSpace(_dacPaths[i]))
						{
							_runtimes[i] = _clrInfos[i].CreateRuntime(_dacPaths[i]);
							if (_runtimes[i] != null) _curRuntimeIndex = i;
						}
					}
				}

				for (int i = 0, icnt = _clrInfos.Length; i < icnt; ++i)
				{
					if (_runtimes[i] == null)
					{
						_dacPaths[i] = _dataTarget.SymbolLocator.FindBinary(_clrInfos[i].DacInfo);
						if (_dacPaths[i] != null) _runtimes[i] = _clrInfos[i].CreateRuntime(_dacPaths[i]);
						if (_runtimes[i] != null && _curRuntimeIndex < 1) _curRuntimeIndex = i;
					}
				}

				if (_curRuntimeIndex == Constants.InvalidIndex)
				{

					StringBuilder sb = new StringBuilder(256);
					for (int i = 0, icnt = _clrInfos.Length; i < icnt; ++i)
					{
						sb.AppendLine(_clrInfos[i].ToString() + ": dac " + _requiredDacs[i] + " -> " + (_dacPaths[i] ?? "not found"));
						_clrInfos[i] = null;
					}
					error = Utils.GetErrorString("Index Creation Failed", "Creating runtimes failed.", _dumpPath, sb.ToString());
					_clrInfos = null;
					_dataTarget.Dispose();
					_dataTarget = null;

					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}

		public ClrtDump Clone(out string error)
		{
			error = null;
			var dmp = new ClrtDump(_dumpPath);
			if (dmp.Init(out error)) return dmp;
			return null;
		}

		public static ClrtDump OpenDump(string dmpPath, out string error)
		{
			error = null;
			try
			{
				var dmp = new ClrtDump(dmpPath);
				if (!dmp.Init(out error))
				{
					return null;
				}
				return dmp;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public static string GetRequiredDac(string dump, out string error)
		{
			error = null;
			try
			{
				using (var dataTarget = DataTarget.LoadCrashDump(dump))
				{
					ClrInfo latest = null;
					foreach (var version in dataTarget.ClrVersions)
					{
						latest = version;
					}
					return latest.DacInfo.FileName;
				}
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public ClrHeap GetFreshHeap()
		{
			var runtime = Runtime;
			runtime.Flush();
			return runtime.GetHeap();
		}

		#endregion Ctors/Initializations

		#region Queries

		#region Memory Sizes

		public static bool GetInstanceSizeHierarchy(ClrRuntime runtime, ulong addr, out InstanceSizeNode root, out ulong totalInstSize, out string error)
		{
			error = null;
			root = null;
			totalInstSize = 0ul;
			try
			{
				runtime.Flush();
				var heap = runtime.GetHeap();
				var refAddresses = new Queue<KeyValuePair<ulong, InstanceSizeNode>>();
				var done = new HashSet<ulong>();
				done.Add(addr);
				ClrType cltType = heap.GetObjectType(addr);
				if (cltType == null)
				{
					error = "No instance type found at address: " + Utils.AddressString(addr);
					return false;
				}
				var clrTypeSize = cltType.GetSize(addr);
				int nodeId = 0;
				ulong totSize = clrTypeSize;

				root = new InstanceSizeNode(nodeId++, cltType.Name, cltType.ElementType, string.Empty, Utils.AddressString(addr), clrTypeSize);

				List<InstanceSizeNode> fldLst = new List<InstanceSizeNode>(64);
				for (int i = 0, icnt = cltType.Fields.Count; i < icnt; ++i)
				{
					var fld = cltType.Fields[i];
					var fldName = fld.Name;
					var fldType = fld.Type;
					var fldTypeName = fldType?.Name ?? Constants.NullName;

					if (fldType.ElementType == ClrElementType.Struct)
					{
						string val = string.Empty;
						ulong sz = 0ul;
						if (Utils.SameStrings(fldTypeName, "System.Decimal"))
						{
							sz = 8;
							totSize += sz;
							var faddr = addr + (ulong)fld.Offset;
							val = ClrMDRIndex.ValueExtractor.GetDecimalValue(faddr, fldType, null);
							var node = new InstanceSizeNode(nodeId++, fldTypeName, fld.ElementType, fldName, val, sz);
							fldLst.Add(node);
						}
						else if (Utils.SameStrings(fldTypeName, "System.DateTime"))
						{
							sz = 8;
							totSize += sz;
							var faddr = addr + (ulong)fld.Offset;
							val = ValueExtractor.GetDateTimeValue(faddr, fldType, null);
							var node = new InstanceSizeNode(nodeId++, fldTypeName, fld.ElementType, fldName, val, sz);
							fldLst.Add(node);
						}
						else if (Utils.SameStrings(fldTypeName, "System.TimeSpan"))
						{
							sz = 8;
							totSize += sz;
							var faddr = addr + (ulong)fld.Offset;
							val = ValueExtractor.GetTimeSpanValue(faddr, fldType);
							var node = new InstanceSizeNode(nodeId++, fldTypeName, fld.ElementType, fldName, val, sz);
							fldLst.Add(node);
						}
						else if (Utils.SameStrings(fldTypeName, "System.Guid"))
						{
							sz = 16;
							totSize += sz;
							var faddr = addr + (ulong)fld.Offset;
							val = ValueExtractor.GetGuidValue(faddr, fldType);
							var node = new InstanceSizeNode(nodeId++, fldTypeName, fld.ElementType, fldName, val, sz);
							fldLst.Add(node);
						}
						continue;
					}

					var fldObj = fld.GetValue(addr, cltType.ElementType == ClrElementType.Struct, false);
					if (fldObj == null)
					{
						var node = new InstanceSizeNode(nodeId++, fldTypeName, fld.ElementType, fldName, Constants.ZeroAddressStr, (ulong)Constants.PointerSize);
						totSize += (ulong)Constants.PointerSize;
						fldLst.Add(node);
						continue;
					}

					if (fldType == null)
					{
						var node = new InstanceSizeNode(nodeId++, fldTypeName, fld.ElementType, fldName, Constants.ZeroAddressStr, (ulong)Constants.PointerSize);
						totSize += (ulong)Constants.PointerSize;
						fldLst.Add(node);
						continue;
					}

					if (fldType.IsString)
					{
						ulong fldObjAddr = (ulong)fldObj;
						ulong sz = fldObjAddr == 0
							? 8
							: Utils.RoundupToPowerOf2Boundary(fldType.GetSize(fldObjAddr), (ulong)Constants.PointerSize);
						totSize += sz;
						var str = fldObjAddr != 0
							? ValueExtractor.GetStringAtAddress(fldObjAddr, heap)
							: Constants.NullName;
						var node = new InstanceSizeNode(nodeId++, fldTypeName, fld.ElementType, fldName, str, sz);
						fldLst.Add(node);
						continue;
					}




					if (fldType.IsArray)
					{
						ulong fldObjAddr = (ulong)fldObj;
						var asz = fldType.GetSize(fldObjAddr);
						var acnt = fldType.GetArrayLength(fldObjAddr);

						totSize += asz;
						var node = new InstanceSizeNode(nodeId++, fldTypeName, fld.ElementType, fldName, Utils.AddressString(fldObjAddr) + " [" + acnt + "]", asz);
						fldLst.Add(node);
						refAddresses.Enqueue(new KeyValuePair<ulong, InstanceSizeNode>(fldObjAddr, node));
						continue;
					}

					if (fldType.IsPrimitive)
					{
						var value = ValueExtractor.GetPrimitiveValue(fldObj, fldType.ElementType);
						var sz = (ulong)ValueExtractor.GetPrimitiveValueSize(fldType.ElementType);
						totSize += sz;
						var node = new InstanceSizeNode(nodeId++, fldTypeName, fld.ElementType, fldName, value, sz);
						fldLst.Add(node);
						continue;
					}



					if (fldType.IsObjectReference)
					{
						ulong fldObjAddr = (ulong)fldObj;

						var totSizeResult = GetTotalSize(heap, new[] { fldObjAddr }, done, out error);
						totSize += totSizeResult.Item1;


						//totSize += (ulong)Constants.PointerSize;
						//var val = fldType.IsException
						//	? ValueExtractor.GetExceptionValue(fldObjAddr, fldType, heap)
						//	: Utils.AddressString(fldObjAddr);

						if (error != null)
						{
							int a = 1;
						}

						var node = new InstanceSizeNode(nodeId++, fldTypeName, fld.ElementType, fldName, Utils.AddressString(fldObjAddr), totSizeResult.Item1);
						fldLst.Add(node);
						if (!fldType.IsException && fldName != "System.Object" && fldName != "System.__Canon")
							refAddresses.Enqueue(new KeyValuePair<ulong, InstanceSizeNode>(fldObjAddr, node));
						continue;
					}
				}
				root.AddNodes(fldLst.ToArray());

				// totSize += GetRootTypeSizeHierarchy(heap, refAddresses, nodeId, done);

				totalInstSize = totSize;
				return true;
			}
			catch (Exception ex)
			{
				Utils.GetExceptionErrorString(ex);
				return false;
			}
		}

		private static ulong GetRootTypeSizeHierarchy(ClrHeap heap, Queue<KeyValuePair<ulong, InstanceSizeNode>> instQue, int nodeId, HashSet<ulong> done)
		{
			ulong totSize = 0ul;
			InstanceSizeNode prevNode = null;
			InstanceSizeNode curNode = null;
			List<InstanceSizeNode> fldLst = new List<InstanceSizeNode>(64);
			while (instQue.Count > 0)
			{
				var kv = instQue.Dequeue();
				if (!done.Add(kv.Key)) continue;
				var curAddr = kv.Key;
				curNode = kv.Value;
				if (prevNode != null && prevNode.NodeId != curNode.NodeId)
				{
					if (fldLst.Count > 0)
					{
						prevNode.AddNodes(fldLst.ToArray());
						fldLst.Clear();
					}
				}
				prevNode = curNode;
				var clrType = heap.GetObjectType(curAddr);
				if (clrType == null) continue;

				if (clrType.IsArray)
				{
					var asz = clrType.GetSize(curAddr);
					totSize += asz;
					var acnt = clrType.GetArrayLength(curAddr);
					ClrType componentType = clrType.ComponentType;
					if (componentType == null || componentType.IsPrimitive || !componentType.ContainsPointers) continue;
					bool compTypeIsObjRef = componentType.IsObjectReference;
					bool compTypeIsStruct = componentType.IsValueClass;


					for (int i = 0; i < acnt; ++i)
					{
						ulong aryItemAddr = clrType.GetArrayElementAddress(curAddr, i);
						if (aryItemAddr == 0) continue;
						if (compTypeIsObjRef)
						{
							var aryItemType = heap.GetObjectType(aryItemAddr);
							if (aryItemType != null)
							{
								// enqueque ??;
							}
							continue;
						}
						if (compTypeIsStruct && componentType.Fields != null)
						{
							for (int j = 0, jcnt = componentType.Fields.Count; j < jcnt; ++j)
							{
								var fld = componentType.Fields[j];
								if (fld.IsPrimitive) continue;
							}
						}
					}

					continue;
				}



				for (int i = 0, icnt = clrType.Fields.Count; i < icnt; ++i)
				{
					var fld = clrType.Fields[i];
					var fldName = fld.Name;
					var fldType = fld.Type;
					var fldTypeName = fldType?.Name ?? Constants.NullName;

					var fldObj = fld.GetValue(curAddr, clrType.ElementType == ClrElementType.Struct);
					if (fldObj == null)
					{
						var node = new InstanceSizeNode(nodeId++, fldTypeName, fld.ElementType, fldName, Constants.ZeroAddressStr, (ulong)Constants.PointerSize);
						totSize += (ulong)Constants.PointerSize;
						fldLst.Add(node);
						continue;
					}
					if (fldType == null)
					{
						var node = new InstanceSizeNode(nodeId++, fldTypeName, fld.ElementType, fldName, Constants.ZeroAddressStr, (ulong)Constants.PointerSize);
						totSize += (ulong)Constants.PointerSize;
						fldLst.Add(node);
						continue;
					}

					if (fldType.IsPrimitive)
					{
						var value = ValueExtractor.GetPrimitiveValue(fldObj, fldType.ElementType);
						var sz = (ulong)ValueExtractor.GetPrimitiveValueSize(fldType.ElementType);
						totSize += sz;
						var node = new InstanceSizeNode(nodeId++, fldTypeName, fld.ElementType, fldName, value, sz);
						fldLst.Add(node);
						continue;
					}

					if (fldType.IsObjectReference)
					{
						ulong fldObjAddr = (ulong)fldObj;
						totSize += (ulong)Constants.PointerSize;
						var node = new InstanceSizeNode(nodeId++, fldTypeName, fld.ElementType, fldName, Utils.AddressString(fldObjAddr), (ulong)Constants.PointerSize);
						fldLst.Add(node);
						instQue.Enqueue(new KeyValuePair<ulong, InstanceSizeNode>(fldObjAddr, node));

						continue;
					}
				}
			}

			if (prevNode != null && prevNode.NodeId != curNode.NodeId)
			{
				if (fldLst.Count > 0)
				{
					prevNode.AddNodes(fldLst.ToArray());
					fldLst.Clear();
				}
			}

			return totSize;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="dmp"></param>
		/// <param name="addresses"></param>
		/// <param name="error"></param>
		/// <returns>
		/// Tuple of
		/// total size
		/// list of addresses for which type was not found -- TODO JRD check this
		/// collection of records : type name, type count, total type size
		/// collection of records : array type name, list of arrays element count
		/// /returns>
		public static Tuple<ulong, ulong[], SortedDictionary<string, KeyValuePair<int, ulong>>, SortedDictionary<string, List<int>>,triple<int,ulong,string>[]> GetTotalSizeDetail(ClrtDump dmp, ulong[] addresses, out string error)
		{
			error = null;
			ulong totalSize = 0;
			try
			{
				var runtime = dmp.Runtime;
				runtime.Flush();
				var heap = runtime.GetHeap();
				HashSet<ulong> done = new HashSet<ulong>();
				var aryDct = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
				var typeDct = new SortedDictionary<string, KeyValuePair<int, ulong>>(StringComparer.Ordinal);
				List<ulong> typesNotFound = new List<ulong>();
				var largeArrays = new BinaryHeap<triple<int, ulong,string>>(new Utils.TripleIntUlongStrCmpAsc());
				for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
				{
					var addr = addresses[i];
					ClrType clrType = heap.GetObjectType(addr);
					if (clrType == null)
					{
						typesNotFound.Add(addr);
						continue;
					}
					if (clrType.IsString)
					{
						totalSize += Utils.RoundupToPowerOf2Boundary(clrType.GetSize(addr), (ulong)Constants.PointerSize);
						continue;
					}
					if (clrType.IsObjectReference)
					{
						totalSize += GetObjectSizeDetails(heap, addr, done, typesNotFound, aryDct, typeDct, largeArrays);
					}
				}

				var bigArrays = new triple<int, ulong,string>[largeArrays.Count];
				for (int i = 0, icnt = largeArrays.Count; i < icnt; ++i)
				{
					bigArrays[i] = largeArrays.RemoveRoot();
				}

				Array.Sort(bigArrays,new Utils.TripleIntUlongStrCmpDesc());
				return new Tuple<ulong, ulong[], SortedDictionary<string, KeyValuePair<int, ulong>>, SortedDictionary<string, List<int>>, triple<int, ulong,string>[]>(totalSize, typesNotFound.ToArray(), typeDct, aryDct, bigArrays);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		private static ulong GetObjectSizeDetails(ClrHeap heap, ulong addr, HashSet<ulong> done, List<ulong> typesNotFound, SortedDictionary<string, List<int>> aryDct, SortedDictionary<string, KeyValuePair<int, ulong>> typeDct, BinaryHeap<triple<int, ulong,string>> largeArrays)
		{
			var que = new Queue<ulong>(64);
			que.Enqueue(addr);
			ulong totalSize = 0;
			while (que.Count > 0)
			{
				var curAddr = que.Dequeue();
				if (!done.Add(curAddr)) continue;

				var curType = heap.GetObjectType(curAddr);
				if (curType == null)
				{
					typesNotFound.Add(curAddr);
					continue;
				}

				#region Process Arrays

				if (curType.IsArray)
				{
					var asz = curType.GetSize(curAddr);
					totalSize += asz;
					AddNameCount(curType.Name, asz, typeDct);
					var acnt = curType.GetArrayLength(curAddr);
					if (largeArrays.Count < 20) largeArrays.Insert(new triple<int, ulong, string>(acnt, curAddr,curType.Name));
					else if (largeArrays.Peek().First < acnt)
					{
						largeArrays.RemoveRoot();
						largeArrays.Insert(new triple<int, ulong, string>(acnt, curAddr, curType.Name));
					}
					List<int> lst;
					if (aryDct.TryGetValue(curType.Name, out lst))
					{
						lst.Add(acnt);
					}
					else
					{
						aryDct.Add(curType.Name, new List<int>(16) { acnt });
					}
					ClrType componentType = curType.ComponentType;
					if (componentType == null || componentType.IsPrimitive || !componentType.ContainsPointers) continue;
					bool compTypeIsObjRef = componentType.IsObjectReference;
					bool compTypeIsStruct = componentType.IsValueClass;
					for (int i = 0; i < acnt; ++i)
					{
						ulong aryItemAddr = curType.GetArrayElementAddress(curAddr, i);
						if (aryItemAddr == 0) continue;
						if (compTypeIsObjRef)
						{
							var aryItemType = heap.GetObjectType(aryItemAddr);
							if (aryItemType == null)
							{
								typesNotFound.Add(aryItemAddr);
							}
							else
							{
								que.Enqueue(aryItemAddr);
							}
							continue;
						}
						if (compTypeIsStruct && componentType.Fields != null)
						{
							for (int j = 0, jcnt = componentType.Fields.Count; j < jcnt; ++j)
							{
								var fld = componentType.Fields[j];
								if (fld.IsPrimitive) continue;
								ProcessFieldSize(heap, fld, aryItemAddr, que, true);
							}
						}
					}

					continue;
				}

				#endregion Process Arrays

				if (curType.IsString)
				{
					var size = Utils.RoundupToPowerOf2Boundary(curType.GetSize(curAddr), (ulong)Constants.PointerSize);
					totalSize += size;
					AddNameCount(curType.Name, size, typeDct);
					continue;
				}

				if (curType.Fields == null || curType.Fields.Count < 1) continue;
				ulong sz = curType.GetSize(curAddr);
				AddNameCount(curType.Name, sz, typeDct);
				totalSize += Utils.RoundupToPowerOf2Boundary(sz, (ulong)Constants.PointerSize);

				bool parentIsRef = curType.IsObjectReference;
				for (int i = 0, icnt = curType.Fields.Count; i < icnt; ++i)
				{
					var fld = curType.Fields[i];
					if (fld.IsPrimitive) continue;
					ProcessFieldSize(heap, fld, curAddr, que, !parentIsRef);
				}
			}
			return totalSize;
		}

		private static void AddNameCount(string name, ulong size, SortedDictionary<string, KeyValuePair<int, ulong>> dct)
		{
			KeyValuePair<int, ulong> kv;
			if (dct.TryGetValue(name, out kv))
			{
				dct[name] = new KeyValuePair<int, ulong>(kv.Key + 1, kv.Value + size);
				return;
			}
			dct.Add(name, new KeyValuePair<int, ulong>(1, size));
		}

		public static Tuple<ulong, ulong[]> GetTotalSize(ClrHeap heap, ulong[] addresses, HashSet<ulong> done, out string error, bool justBaseSize = false)
		{
			error = null;
			ulong totalSize = 0;
			try
			{
				List<ulong> typesNotFound = new List<ulong>();
				for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
				{
					var addr = addresses[i];
					ClrType clrType = heap.GetObjectType(addr);
					if (clrType == null)
					{
						typesNotFound.Add(addr);
						continue;
					}
					if (clrType.IsString)
					{
						totalSize += Utils.RoundupToPowerOf2Boundary(clrType.GetSize(addr), (ulong)Constants.PointerSize);
						continue;
					}
					if (clrType.IsObjectReference)
					{
						var sz = justBaseSize
							? GetObjectBaseSize(heap, addr, done, typesNotFound)
							: GetObjectSize(heap, addr, done, typesNotFound);

						totalSize += sz;
					}
				}
				return new Tuple<ulong, ulong[]>(totalSize, typesNotFound.ToArray());
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return new Tuple<ulong, ulong[]>(0ul, Utils.EmptyArray<ulong>.Value);
			}
		}

		private static ulong GetObjectSize(ClrHeap heap, ulong addr, HashSet<ulong> done, List<ulong> typesNotFound)
		{
			var que = new Queue<ulong>(64);
			que.Enqueue(addr);
			ulong totalSize = 0;
			while (que.Count > 0)
			{
				var curAddr = que.Dequeue();
				if (!done.Add(curAddr)) continue;

				var curType = heap.GetObjectType(curAddr);
				if (curType == null)
				{
					typesNotFound.Add(curAddr);
					continue;
				}

				#region Process Arrays

				if (curType.IsArray)
				{
					var asz = curType.GetSize(curAddr);
					totalSize += asz;
					var acnt = curType.GetArrayLength(curAddr);
					ClrType componentType = curType.ComponentType;
					if (componentType == null || componentType.IsPrimitive || !componentType.ContainsPointers) continue;
					bool compTypeIsObjRef = componentType.IsObjectReference;
					bool compTypeIsStruct = componentType.IsValueClass;
					for (int i = 0; i < acnt; ++i)
					{
						ulong aryItemAddr = curType.GetArrayElementAddress(curAddr, i);
						if (aryItemAddr == 0) continue;
						if (compTypeIsObjRef)
						{
							var aryItemType = heap.GetObjectType(aryItemAddr);
							if (aryItemType == null)
							{
								typesNotFound.Add(aryItemAddr);
							}
							else
							{
								que.Enqueue(aryItemAddr);
							}
							continue;
						}
						if (compTypeIsStruct && componentType.Fields != null)
						{
							for (int j = 0, jcnt = componentType.Fields.Count; j < jcnt; ++j)
							{
								var fld = componentType.Fields[j];
								if (fld.IsPrimitive) continue;
								ProcessFieldSize(heap, fld, aryItemAddr, que, true);
							}
						}
					}

					continue;
				}

				#endregion Process Arrays

				if (curType.IsString)
				{
					totalSize += Utils.RoundupToPowerOf2Boundary(curType.GetSize(curAddr), (ulong)Constants.PointerSize);
					continue;
				}

				if (curType.Fields == null || curType.Fields.Count < 1) continue;
				ulong sz = curType.GetSize(curAddr);
				totalSize += Utils.RoundupToPowerOf2Boundary(sz, (ulong)Constants.PointerSize);

				bool parentIsRef = curType.IsObjectReference;
				for (int i = 0, icnt = curType.Fields.Count; i < icnt; ++i)
				{
					var fld = curType.Fields[i];
					if (fld.IsPrimitive) continue;
					ProcessFieldSize(heap, fld, curAddr, que, !parentIsRef);
				}
			}
			return totalSize;
		}

		private static ulong GetObjectBaseSize(ClrHeap heap, ulong addr, HashSet<ulong> done, List<ulong> typesNotFound)
		{
			ulong totalSize = 0;
			if (!done.Add(addr)) return 0ul;

			var curType = heap.GetObjectType(addr);
			if (curType == null)
			{
				typesNotFound.Add(addr);
				return 0ul;
			}

			ulong sz = curType.GetSize(addr);
			totalSize += Utils.RoundupToPowerOf2Boundary(sz, (ulong)Constants.PointerSize);
			return totalSize;
		}

		private static void ProcessFieldSize(ClrHeap heap, ClrInstanceField fld, ulong parentAddr, Queue<ulong> que, bool internalAddr = false)
		{
			if (fld.Type == null) return;
			if (fld.Type.IsObjectReference) // parent is reference class type
			{
				var objAddr = fld.GetValue(parentAddr, internalAddr, false);
				if (objAddr == null) return;
				ulong addr = (ulong)objAddr;
				if (addr == 0) return;
				que.Enqueue(addr);
			}
		}

		public static ulong GetObjectSize(ClrHeap heap, ClrType clrType, ulong addr, HashSet<ulong> done)
		{
			var que = new Queue<KeyValuePair<ClrType, ulong>>(64);
			que.Enqueue(new KeyValuePair<ClrType, ulong>(clrType, addr));
			ulong totalSize = 0;
			while (que.Count > 0)
			{
				var kv = que.Dequeue();
				if (!done.Add(kv.Value)) continue;

				var curType = kv.Key; //heap.GetObjectType(kv.Value);
				var curAddr = kv.Value;

				#region Process Arrays

				if (curType.IsArray)
				{
					var asz = curType.GetSize(curAddr);
					totalSize += asz;
					ClrType componentType = curType.ComponentType;
					if (componentType == null || !componentType.ContainsPointers) continue;
					var acnt = curType.GetArrayLength(curAddr);

					// try to find out was is the array type
					ClrType aryItemType = null;
					for (int i = 0; i < acnt; ++i)
					{
						ulong aryItemAddr = curType.GetArrayElementAddress(curAddr, i);
						if (aryItemAddr == 0) continue;
						aryItemType = heap.GetObjectType(aryItemAddr);
						break;
					}
					if (aryItemType != null)
					{
						int a = 1;
					}

					//var flds = clrType.ComponentType.Fields;
					//   bool hasObjRef = false;
					//for (int i = 0, icnt = flds.Count; i < icnt; ++i)
					//{
					//	var fld = flds[i];
					//	if (fld.Type.IsObjectReference)
					//	{
					//		var faddr = (ulong) fld.GetValue(curAddr,true);
					//		que.Enqueue(new KeyValuePair<ClrType, ulong>(fld.Type, faddr));
					//		continue;
					//	}
					//}
					continue;
				}

				#endregion Process Arrays

				if (curType.IsString)
				{
					totalSize += Utils.RoundupToPowerOf2Boundary(curType.GetSize(curAddr), (ulong)Constants.PointerSize);
					continue;
				}

				if (curType.Fields == null || curType.Fields.Count < 1) continue;
				ulong sz = curType.GetSize(curAddr);
				totalSize += Utils.RoundupToPowerOf2Boundary(sz, (ulong)Constants.PointerSize);

				for (int i = 0, icnt = curType.Fields.Count; i < icnt; ++i)
				{
					var fld = curType.Fields[i];
					if (fld.IsPrimitive) continue;
					if (fld.IsValueClass)
					{
						if (fld.Type != null && fld.Type.ContainsPointers)
							ProcessValueClassFieldSize(heap, curType.Fields[i], curAddr, que, done, false);
						continue;
					}
					ProcessClassFieldSize(heap, curType.Fields[i], curAddr, que, done, false);
				}
			}
			return totalSize;
		}

		private static void ProcessValueClassFieldSize(ClrHeap heap, ClrInstanceField fld, ulong parentAddr, Queue<KeyValuePair<ClrType, ulong>> que, HashSet<ulong> done, bool internalAddr = false)
		{
			Debug.Assert(fld.Type.IsValueClass);
			var addr = fld.GetAddress(parentAddr, internalAddr);
			if (addr == 0 || done.Contains(addr)) return;
			ClrType clrType = heap.GetObjectType(addr);
			if (clrType == null)
			{
				return;
			}
			que.Enqueue(new KeyValuePair<ClrType, ulong>(clrType, addr));
		}

		private static void ProcessClassFieldSize(ClrHeap heap, ClrInstanceField fld, ulong parentAddr, Queue<KeyValuePair<ClrType, ulong>> que, HashSet<ulong> done, bool internalAddr = false)
		{
			Debug.Assert(fld.Type.IsObjectReference);
			var objAddr = fld.GetValue(parentAddr, internalAddr, false);
			if (objAddr == null) return;
			ulong addr = (ulong)objAddr;
			if (addr == 0) return;
			ClrType clrType = heap.GetObjectType(addr);
			if (clrType == null)
			{
				return;
			}
			que.Enqueue(new KeyValuePair<ClrType, ulong>(clrType, addr));
		}

		private static void ProcessFieldSize(ClrHeap heap, ClrInstanceField fld, ulong parentAddr, Queue<KeyValuePair<ClrType, ulong>> que, HashSet<ulong> done, bool internalAddr = false)
		{
			Debug.Assert(fld.Type != null); // this should be checked by a caller
			if (fld.Type.IsObjectReference) // parent is reference class type
			{
				var objAddr = fld.GetValue(parentAddr, internalAddr, false);
				if (objAddr == null) return;
				ulong addr = (ulong)objAddr;
				if (addr == 0) return;
				ClrType clrType = heap.GetObjectType(addr);
				if (clrType != null) que.Enqueue(new KeyValuePair<ClrType, ulong>(clrType, addr));
				return;
			}
			Debug.Assert(fld.Type.IsValueClass);

		}


		#endregion Memory Sizes

		#region Strings

		public static StringStats GetStringStats(ClrHeap heap, ulong[] addresses, string dumpPath, out string error, int runtimeIndex = 0)
		{
			error = null;
			try
			{
				var strDct = new SortedDictionary<string, KeyValuePair<uint, List<ulong>>>(StringComparer.Ordinal);
				ulong dataOffset = (ulong)IntPtr.Size;
				byte[] lenBytes = new byte[4];
				for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
				{
					var addr = addresses[i];
					var clrType = heap.GetObjectType(addr);
					var hsz = clrType.GetSize(addr);
					if (hsz > (ulong)UInt32.MaxValue) hsz = (ulong)UInt32.MaxValue; // to be safe
					var off = addr + dataOffset;
					heap.ReadMemory(off, lenBytes, 0, 4);
					int len = 0;
					string str = string.Empty;
					try
					{
						len = BitConverter.ToInt32(lenBytes, 0) * sizeof(char);
						byte[] strBuf = new byte[len];
						heap.ReadMemory(off + 4UL, strBuf, 0, len);
						str = Encoding.Unicode.GetString(strBuf);
					}
					catch (Exception ex)
					{
						int a = 1; // TODO JRD ???
						len = 0;
					}
					KeyValuePair<uint, List<ulong>> kv;
					if (strDct.TryGetValue(str, out kv))
					{
						strDct[str].Value.Add(addr);
					}
					else
					{
						strDct.Add(str, new KeyValuePair<uint, List<ulong>>((uint)Utils.RoundupToPowerOf2Boundary(hsz, dataOffset), new List<ulong>() { addr }));
					}
				}

				var strTxtPath = DumpFileMoniker.GetRuntimeFilePath(runtimeIndex, dumpPath, Constants.TxtDumpStringsPostfix);
				var strDatPath = DumpFileMoniker.GetRuntimeFilePath(runtimeIndex, dumpPath, Constants.MapDumpStringsInfoPostfix);
				var strStats = StringStats.GetStringStats(strDct, strTxtPath, strDatPath, out error);
				return strStats;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public static ulong[] GetSpecificStringAddresses(ClrHeap heap, ulong[] addresses, string stringContent, out string error)
		{
			error = null;
			try
			{
				if (stringContent == null)
				{
					error = Utils.GetErrorString("Get Specific String Addresses", "Search failed, string argument is null.",
						"The string to search for cannot be empty");
					return null;
				}
				bool matchPrefix = false;
				if (stringContent.Length > 2 && stringContent[stringContent.Length - 1] == Constants.FancyKleeneStar)
				{
					matchPrefix = true;
					stringContent = stringContent.Substring(0, stringContent.Length - 1);
				}
				var lst = new List<ulong>(4 * 1024);
				ulong dataOffset = (ulong)IntPtr.Size;
				byte[] lenBytes = new byte[4];
				for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
				{
					var addr = addresses[i];
					var clrType = heap.GetObjectType(addr);
					var off = addr + dataOffset;
					heap.ReadMemory(off, lenBytes, 0, 4);
					int len = 0;
					string str = Constants.ImprobableString;
					try
					{
						len = BitConverter.ToInt32(lenBytes, 0) * sizeof(char);
						byte[] strBuf = new byte[len];
						heap.ReadMemory(off + 4UL, strBuf, 0, len);
						str = Encoding.Unicode.GetString(strBuf);
					}
					catch (Exception ex)
					{
						error = Utils.GetExceptionErrorString(ex);
						return null;
					}

					if (matchPrefix)
					{
						if (str.StartsWith(stringContent, StringComparison.Ordinal))
							lst.Add(addr);
						continue;
					}
					if (Utils.SameStrings(stringContent, str))
					{
						lst.Add(addr);
					}
				}

				return lst.ToArray();
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public static SortedDictionary<string, KeyValuePair<int, uint>> GetStringCountsAndSizes(ClrHeap heap,
				out long totalSize, out long totalUniqueSize, out int totalCount, out string error)
		{
			error = null;
			totalSize = 0;
			totalUniqueSize = 0;
			totalCount = 0;
			try
			{
				SortedDictionary<string, KeyValuePair<int, uint>> dct = new SortedDictionary<string, KeyValuePair<int, uint>>(StringComparer.Ordinal);
				ulong dataOffset = (ulong)IntPtr.Size;
				byte[] lenBytes = new byte[4];

				var segs = heap.Segments;

				for (int i = 0, icnt = segs.Count; i < icnt; ++i)
				{
					var seg = segs[i];
					ulong addr = seg.FirstObject;
					while (addr != 0ul)
					{
						var clrType = heap.GetObjectType(addr);
						if (clrType == null || !clrType.IsString) goto NEXT_OBJECT;
						ulong sz = clrType.GetSize(addr);
						uint size = sz > UInt32.MaxValue ? UInt32.MaxValue : (uint)sz;
						var off = addr + dataOffset;
						heap.ReadMemory(off, lenBytes, 0, 4);
						int len = 0;
						string str = Constants.ImprobableString;
						try
						{
							len = BitConverter.ToInt32(lenBytes, 0) * sizeof(char);
							byte[] strBuf = new byte[len];
							heap.ReadMemory(off + 4UL, strBuf, 0, len);
							str = Encoding.Unicode.GetString(strBuf);
							KeyValuePair<int, uint> dctEntry;
							if (dct.TryGetValue(str, out dctEntry))
							{
								dct[str] = new KeyValuePair<int, uint>(dctEntry.Key + 1, dctEntry.Value);
							}
							else
							{
								dct.Add(str, new KeyValuePair<int, uint>(1, size));
								totalUniqueSize += size;
							}
							++totalCount;
							totalSize += size;
						}
						catch (Exception ex)
						{
							error = Utils.GetExceptionErrorString(ex);
							return null;
						}

						NEXT_OBJECT:
						addr = seg.NextObject(addr);
					}
				}
				return dct;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}


		public static ulong[] GetTypeAddresses(ClrHeap heap, string typeName, out string error)
		{
			error = null;
			try
			{
				List<ulong> lst = new List<ulong>(10 * 1024);
				var segs = heap.Segments;
				for (int i = 0, icnt = segs.Count; i < icnt; ++i)
				{
					var seg = segs[i];
					ulong addr = seg.FirstObject;
					while (addr != 0ul)
					{
						var clrType = heap.GetObjectType(addr);
						if (clrType == null || clrType.Name != typeName) goto NEXT_OBJECT;
						lst.Add(addr);
						NEXT_OBJECT:
						addr = seg.NextObject(addr);
					}
				}
				return lst.ToArray();
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
				throw;
			}
		}

		public static SortedDictionary<string, quadruple<int, ulong, ulong, ulong>> GetTypeSizesInfo(ClrHeap heap, out string error)
		{
			error = null;
			try
			{
				ulong grandTotal = 0ul;
				int instanceCount = 0;
				var typeDct = new SortedDictionary<string, quadruple<int, ulong, ulong, ulong>>(StringComparer.Ordinal);
				var segs = heap.Segments;
				for (int i = 0, icnt = segs.Count; i < icnt; ++i)
				{
					var seg = segs[i];
					ulong addr = seg.FirstObject;
					while (addr != 0ul)
					{
						var clrType = heap.GetObjectType(addr);
						if (clrType == null) goto NEXT_OBJECT;
						++instanceCount;
						var sz = clrType.GetSize(addr);
						grandTotal += sz;
						quadruple<int, ulong, ulong, ulong> info;
						if (typeDct.TryGetValue(clrType.Name, out info))
						{
							var maxSz = info.Third < sz ? sz : info.Third;
							var minSz = info.Third > sz ? sz : info.Third;
							typeDct[clrType.Name] = new quadruple<int, ulong, ulong, ulong>(
								info.First + 1,
								info.Second + sz,
								maxSz,
								minSz
								);
						}
						else
						{
							typeDct.Add(clrType.Name, new quadruple<int, ulong, ulong, ulong>(1, sz, sz, sz));
						}
						NEXT_OBJECT:
						addr = seg.NextObject(addr);
					}
				}
				return typeDct;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public static ListingInfo GetAllTypesSizesInfo(ClrHeap heap, out string error)
		{
			error = null;
			try
			{
				ulong grandTotal = 0ul;
				int instanceCount = 0;
				var typeDct = new SortedDictionary<string, quadruple<int, ulong, ulong, ulong>>(StringComparer.Ordinal);
				var segs = heap.Segments;
				for (int i = 0, icnt = segs.Count; i < icnt; ++i)
				{
					var seg = segs[i];
					ulong addr = seg.FirstObject;
					while (addr != 0ul)
					{
						var clrType = heap.GetObjectType(addr);
						if (clrType == null) goto NEXT_OBJECT;
						++instanceCount;
						var sz = clrType.GetSize(addr);
						grandTotal += sz;
						quadruple<int, ulong, ulong, ulong> info;
						if (typeDct.TryGetValue(clrType.Name, out info))
						{
							var maxSz = info.Third < sz ? sz : info.Third;
							var minSz = info.Third > sz ? sz : info.Third;
							typeDct[clrType.Name] = new quadruple<int, ulong, ulong, ulong>(
								info.First + 1,
								info.Second + sz,
								maxSz,
								minSz
								);
						}
						else
						{
							typeDct.Add(clrType.Name, new quadruple<int, ulong, ulong, ulong>(1, sz, sz, sz));
						}
						NEXT_OBJECT:
						addr = seg.NextObject(addr);
					}
				}

				var dataAry = new string[typeDct.Count * 6];
				var infoAry = new listing<string>[typeDct.Count];
				int off = 0;
				int ndx = 0;
				foreach (var kv in typeDct)
				{
					infoAry[ndx++] = new listing<string>(dataAry, off, 6);
					int cnt = kv.Value.First;
					ulong totSz = kv.Value.Second;
					ulong maxSz = kv.Value.Third;
					ulong minSz = kv.Value.Forth;
					double avg = (double)totSz / (double)(cnt);
					ulong uavg = Convert.ToUInt64(avg);

					dataAry[off++] = Utils.LargeNumberString(cnt);
					dataAry[off++] = Utils.LargeNumberString(totSz);
					dataAry[off++] = Utils.LargeNumberString(maxSz);
					dataAry[off++] = Utils.LargeNumberString(minSz);
					dataAry[off++] = Utils.LargeNumberString(uavg);
					dataAry[off++] = kv.Key;
				}

				ColumnInfo[] colInfos = new[]
				{
					new ColumnInfo("Count", ReportFile.ColumnType.Int32,150,1,true),
					new ColumnInfo("Total Size", ReportFile.ColumnType.UInt64,150,2,true),
					new ColumnInfo("Max Size", ReportFile.ColumnType.UInt64,150,3,true),
					new ColumnInfo("Min Size", ReportFile.ColumnType.UInt64,150,4,true),
					new ColumnInfo("Avg Size", ReportFile.ColumnType.UInt64,150,5,true),
					new ColumnInfo("Type", ReportFile.ColumnType.String,500,6,true),
				};
				Array.Sort(infoAry, ReportFile.GetComparer(colInfos[0]));

				string descr = "Type Count: " + typeDct.Count + Environment.NewLine
							   + "Instance Count: " + instanceCount + Environment.NewLine
							   + "Grand Total Size: " + grandTotal + Environment.NewLine;

				return new ListingInfo(null, infoAry, colInfos, descr);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		//public 

		public static bool GetTypeStringUsage(ClrHeap heap, ulong[] addresses, out string error)
		{
			error = null;
			try
			{
				ClrType clrType = null;
				for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
				{
					clrType = heap.GetObjectType(addresses[i]);
					if (clrType != null) break;
				}

				ClrInstanceField[] fields = GetStringFields(clrType);
				if (fields.Length < 1) return true;
				string[] fldNames = GetFieldNames(fields);
				HashSet<ulong>[] fldStrAddresses = new HashSet<ulong>[fldNames.Length];
				for (int i = 0, icnt = fldStrAddresses.Length; i < icnt; ++i) fldStrAddresses[i] = new HashSet<ulong>();
				Dictionary<ulong, KeyValuePair<int, int>> addrDct = new Dictionary<ulong, KeyValuePair<int, int>>(addresses.Length * (fldNames.Length / 2));
				SortedDictionary<string, int> dupDct = new SortedDictionary<string, int>();

				int nullCount = 0;
				bool inner = clrType.IsValueClass;
				ulong strAddr;
				string str;
				int addrNdx, fldNdx, addrCnt = addresses.Length, fldCnt = fields.Length;
				try
				{
					for (addrNdx = 0; addrNdx < addrCnt; ++addrNdx)
					{
						for (fldNdx = 0; fldNdx < fldCnt; ++fldNdx)
						{
							object addrObj = fields[fldNdx].GetValue(addresses[addrNdx], inner, false);
							if (addrObj == null || ((ulong)addrObj) == 0)
							{
								strAddr = 0;
								str = null;
								fldStrAddresses[fldNdx].Add(strAddr);
								++nullCount;
								continue;
							}
							strAddr = (ulong)addrObj;
							fldStrAddresses[fldNdx].Add(strAddr);
							str = (string)fields[fldNdx].GetValue(addresses[addrNdx], inner, true);
							int cnt;
							if (dupDct.TryGetValue(str, out cnt))
							{
								dupDct[str] = cnt + 1;
							}
							else
							{
								dupDct.Add(str, 1);
							}
						}
					}
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					return false;
				}

				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}

		private static int[] GetStringFieldsIndices(ClrType clrType)
		{
			List<int> lst = new List<int>(clrType.Fields.Count);
			for (int i = 0, icnt = clrType.Fields.Count; i < icnt; ++i)
			{
				var fld = clrType.Fields[i];
				if (fld.IsObjectReference && fld.Type != null && fld.Type.IsString)
					lst.Add(i);
			}
			return lst.ToArray();
		}

		private static ClrInstanceField[] GetStringFields(ClrType clrType)
		{
			List<ClrInstanceField> lst = new List<ClrInstanceField>(clrType.Fields.Count);
			for (int i = 0, icnt = clrType.Fields.Count; i < icnt; ++i)
			{
				var fld = clrType.Fields[i];
				if (fld.IsObjectReference && fld.Type != null && fld.Type.IsString)
					lst.Add(fld);
			}
			return lst.ToArray();
		}

		private static string[] GetFieldNames(ClrInstanceField[] fields)
		{
			string[] names = new string[fields.Length];
			for (int i = 0, icnt = fields.Length; i < icnt; ++i)
				names[i] = fields[i].Name;
			return names;
		}

		#endregion Strings

		#endregion Queries

		#region Dispose

		volatile bool _disposed = false;

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (_disposed)
				return;

			if (disposing)
			{
				// Free any other managed objects here.
				//
				_dataTarget?.Dispose();
			}

			// Free any unmanaged objects here.
			//
			_disposed = true;
		}

		~ClrtDump()
		{
			Dispose(false);
		}

		#endregion Dispose
	}
}
