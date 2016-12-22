using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Threading;
using ClrMDRUtil.Utils;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	public sealed class DumpIndexer
	{
		[Flags]
		public enum IndexingArguments
		{
			All = 0xFFFFFFF,
			JustInstanceRefs = 1,
		}

		private DumpFileMoniker _fileMoniker;
		public string AdhocFolder => _fileMoniker.OutputFolder;
		public string OutputFolder => AdhocFolder;
		public string IndexFolder => _fileMoniker.MapFolder;
		public string DumpPath => _fileMoniker.Path;
		public string DumpFileName => _fileMoniker.FileName;

		/// <summary>
		/// Indexing errors, they all are written to the error file in the index folder.
		/// </summary>
		private ConcurrentBag<string>[] _errors;

		/// <summary>
		/// List of the unique string ids.
		/// </summary>
		private StringIdDct[] _stringIdDcts;

		/// <summary>
		/// Index of a runtime currently proccessed.
		/// </summary>
		private int _currentRuntimeIndex;


		public DumpIndexer(string dmpPath)
		{
			_fileMoniker = new DumpFileMoniker(dmpPath);
		}

		public bool CreateDumpIndex(Version version, IProgress<string> progress, IndexingArguments indexArguments,
			out string indexPath, out string error)
		{
			error = null;
			indexPath = _fileMoniker.MapFolder;
			Stopwatch indexingStopWatch = new Stopwatch();
			indexingStopWatch.Start();
			var clrtDump = new ClrtDump(DumpPath);
			if (!clrtDump.Init(out error)) return false;
			Stopwatch stopWatch = new Stopwatch();
			string durationStr = string.Empty;
			_errors = new ConcurrentBag<string>[clrtDump.RuntimeCount];
			Thread extraWorker = null;
			ClrtDump dumpClone = clrtDump.Clone(out error);

			using (clrtDump)
			{
				try
				{
					if (DumpFileMoniker.GetAndCreateMapFolders(DumpPath, out error) == null) return false;
					indexPath = IndexFolder;

					// indexing
					//
					if (!GetPrerequisites(clrtDump, progress, out _stringIdDcts, out error)) return false;
					if (!GetTargetModuleInfos(clrtDump, progress, out error)) return false;

					for (int r = 0, rcnt = clrtDump.RuntimeCount; r < rcnt; ++r)
					{
						_currentRuntimeIndex = r;
						string runtimeIndexHeader = Utils.RuntimeStringHeader(r);
						clrtDump.SetRuntime(r);
						ClrRuntime runtime = clrtDump.Runtime;
						ClrHeap heap = runtime.GetHeap();
						ConcurrentBag<string> errors = new ConcurrentBag<string>();
						_errors[r] = errors;
						var strIds = _stringIdDcts[r];


						string[] typeNames = null;
						ulong[] roots = null;
						ulong[] objects = null;
						ulong[] addresses = null;
						int[] typeIds = null;

						if ((indexArguments & IndexingArguments.JustInstanceRefs) > 0)
						{

							// get heap address count
							//
							progress?.Report(runtimeIndexHeader + "Getting instance count...");
							stopWatch.Restart();
							var addrCount = DumpIndexer.GetHeapAddressCount(heap);
							durationStr = Utils.StopAndGetDurationString(stopWatch);

							// get type names
							//
							progress?.Report(runtimeIndexHeader + "Getting type names... Previous action duration: " + durationStr);
							stopWatch.Restart();
							typeNames = DumpIndexer.GetTypeNames(heap, out error);
							Debug.Assert(error == null);
							durationStr = Utils.StopAndGetDurationString(stopWatch);

							// get roots
							//
							progress?.Report(runtimeIndexHeader + "Getting roots... Previous action duration: " + durationStr);
							stopWatch.Restart();
							var rootAddrInfo = ClrtRootInfo.GetRootAddresses(r, heap, typeNames, strIds, _fileMoniker, out error);
							Debug.Assert(error == null);
							durationStr = Utils.StopAndGetDurationString(stopWatch);

							// get addresses and set roots
							//
							progress?.Report(runtimeIndexHeader + "Getting addresses and setting roots... Previous action duration: " +
							                 durationStr);
							stopWatch.Restart();
							addresses = new ulong[addrCount];
							typeIds = new int[addrCount];
							if (!GetAddressesSetRoots(heap, addresses, typeIds, typeNames, rootAddrInfo, out error))
							{
								return false;
							}
							durationStr = Utils.StopAndGetDurationString(stopWatch);

							// threads and blocking objects
							//
							extraWorker = new Thread(GetThreadsInfos);
							extraWorker.Start(new Tuple<ClrtDump,ulong[],int[],string[]>(dumpClone,addresses,typeIds,typeNames));

							stopWatch.Restart();
							progress?.Report(runtimeIndexHeader +
							                 "Getting threads, blocking objecks information... Previous action duration: " + durationStr);
							//if (!GetThreadsInfos(clrtDump, addresses, typeIds, out error))
							//{
							//	return false;
							//}
							durationStr = Utils.StopAndGetDurationString(stopWatch);

							// field dependencies
							//
							stopWatch.Restart();
							progress?.Report(runtimeIndexHeader + "Getting field dependecies... Previous action duration: " + durationStr);

							var instanceFldRefs = _fileMoniker.GetFilePath(0, Constants.MapFieldRefInstancesPostfix);
							var fldRefQue = new BlockingCollection<KeyValuePair<int, int[]>>();
							var fldRefErrors = new List<string>(0);
							var threadFldRefPersister = Indexer.StartFieldRefInfoPersiter(instanceFldRefs, fldRefQue, fldRefErrors);

							var fldRefList = new List<KeyValuePair<ulong, int>>(64);
							var addressesLastNdx = addresses.Length - 1;
							var fieldsArrays = new int[addresses.Length][];
							var fldRefs = new HashSet<int>();

							for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
							{
								var addr = addresses[i];
								var cleanAddr = Utils.RealAddress(addr);
								var isRooted = Utils.IsRooted(addr);

								var clrType = heap.GetObjectType(cleanAddr);
								if (clrType == null || clrType.IsString || clrType.Fields == null) continue;

								var isArray = clrType.IsArray;
								fldRefs.Clear();
								fldRefList.Clear();
								clrType.EnumerateRefsOfObjectCarefully(cleanAddr, (address, off) =>
								{
									fldRefList.Add(new KeyValuePair<ulong, int>(address, off));
								});

								if (fldRefList.Count > 0)
								{
									for (int k = 0, kcnt = fldRefList.Count; k < kcnt; ++k)
									{
										var fldAddr = fldRefList[k].Key;
										if (fldAddr == 0UL) continue;
										var addrnx = Utils.AddressSearch(addresses, fldAddr, 0, addressesLastNdx);
										if (addrnx < 0 || fldRefs.Contains(addrnx)) continue;
										fldRefs.Add(addrnx);
										if (isRooted)
										{
											DumpIndexer.MarkAsRooted(fldAddr, addrnx, addresses, fieldsArrays);
										}
									}
								}

								if (fldRefs.Count > 0)
								{
									var refAry = fldRefs.ToArray();
									Array.Sort(refAry);
									fieldsArrays[i] = refAry;
									fldRefQue.Add(new KeyValuePair<int, int[]>(i, refAry));
								}
							}

							fldRefQue.Add(new KeyValuePair<int, int[]>(-1, null));

							threadFldRefPersister.Join();
							durationStr = Utils.StopAndGetDurationString(stopWatch);

							// build type/instance map
							//
							if (!BuildTypeInstanceMap(r, typeIds, out error))
							{
								AddError(r, "BuildTypeInstanceMap failed." + Environment.NewLine + error);
								return false;
							}
							durationStr = Utils.StopAndGetDurationString(stopWatch);

							// build instance reference map
							//
							stopWatch.Restart();
							progress?.Report(runtimeIndexHeader + "Building instance reference map... Previous action duration: " +
							                 durationStr);

							InstanceReferences.InvertFieldRefs(new Tuple<int, DumpFileMoniker, ConcurrentBag<string>, IProgress<string>>(r,
								_fileMoniker, errors, null));

							durationStr = Utils.StopAndGetDurationString(stopWatch);
							stopWatch.Restart();
							progress?.Report(runtimeIndexHeader + "Saving index data... Previous action duration: " + durationStr);

							string path = _fileMoniker.GetFilePath(r, Constants.MapInstancesFilePostfix);
							Utils.WriteUlongArray(path, addresses, out error);
							path = _fileMoniker.GetFilePath(r, Constants.MapInstanceTypesFilePostfix);
							Utils.WriteIntArray(path, typeIds, out error);
							// save type names
							path = _fileMoniker.GetFilePath(r, Constants.TxtTypeNamesFilePostfix);
							Utils.WriteStringList(path, typeNames, out error);
							{
								var reversedNames = Utils.ReverseTypeNames(typeNames);
								path = _fileMoniker.GetFilePath(r, Constants.TxtReversedTypeNamesFilePostfix);
								Utils.WriteStringList(path, reversedNames, out error);
							}
							// save string ids
							//
							path = _fileMoniker.GetFilePath(r, Constants.TxtCommonStringIdsPostfix);
							if (!strIds.DumpInIdOrder(path, out error))
							{
								AddError(_currentRuntimeIndex, "StringIdDct.DumpInIdOrder failed." + Environment.NewLine + error);
							}

							//if (indexArguments == IndexingArguments.JustInstanceRefs) return true; // we only want instance refs
						}


						runtime.Flush();
						heap = null;
					}

					progress?.Report("Waiting for thread info worker... Previous action duration: " + durationStr);
					extraWorker.Join();
					var indexingDuration = Utils.StopAndGetDurationString(indexingStopWatch);
					DumpIndexInfo(version, clrtDump, indexingDuration);
					return true;
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					AddError(_currentRuntimeIndex, "Exception in CreateDumpIndex." + Environment.NewLine + error);
					return false;
				}
				finally
				{
					dumpClone?.Dispose();
					DumpErrors();
					GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
					GC.Collect();
				}
			}
		}

		/// <summary>
		/// Save instances by type lookup.
		/// </summary>
		/// <param name="rtNdx">Current runtime index.</param>
		/// <param name="typeIds">Big array of type ids correspondig to instances.</param>
		/// <param name="error">Error message, out.</param>
		/// <returns>True if no exception.</returns>
		private bool BuildTypeInstanceMap(int rtNdx, int[] typeIds, out string error)
		{
			error = null;
			BinaryWriter brOffsets = null;
			try
			{
				brOffsets =
					new BinaryWriter(File.Open(_fileMoniker.GetFilePath(rtNdx, Constants.MapTypeInstanceOffsetsFilePostfix),
						FileMode.Create));
				var indices = Utils.Iota(typeIds.Length);
				var ids = new int[typeIds.Length];
				Array.Copy(typeIds, 0, ids, 0, typeIds.Length);
				Array.Sort(ids, indices);
				brOffsets.Write((int) 0);
				int prev = ids[0];
				brOffsets.Write(prev);
				brOffsets.Write((int) 0);
				int usedTypeCnt = 1;
				for (int i = 1, icnt = ids.Length; i < icnt; ++i)
				{
					int cur = ids[i];
					if (cur != prev)
					{
						brOffsets.Write(cur);
						brOffsets.Write(i);
						prev = cur;
						++usedTypeCnt;
					}
				}
				brOffsets.Write(int.MaxValue);
				brOffsets.Write(ids.Length);
				++usedTypeCnt;
				brOffsets.Seek(0, SeekOrigin.Begin);
				brOffsets.Write(usedTypeCnt);
				Utils.CloseStream(ref brOffsets);

				Utils.WriteIntArray(_fileMoniker.GetFilePath(rtNdx, Constants.MapTypeInstanceMapFilePostfix), indices, out error);

				return error == null;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				brOffsets?.Close();
			}
		}

		public static int GetHeapAddressCount(ClrHeap heap)
		{
			int count = 0;
			var segs = heap.Segments;
			for (int i = 0, icnt = segs.Count; i < icnt; ++i)
			{
				var seg = segs[i];
				ulong addr = seg.FirstObject;
				while (addr != 0ul)
				{
					++count;
					addr = seg.NextObject(addr);
				}
			}
			return count;
		}

		public bool GetAddressesSetRoots(ClrHeap heap, ulong[] addresses, int[] typeIds, string[] typeNames,
			Tuple<ulong[], ulong[]> rootInfos, out string error)
		{
			error = null;
			var roots = rootInfos.Item1;
			var finalizers = rootInfos.Item2;
			try
			{
				var segs = heap.Segments;
				ClrtSegment[] mysegs = new ClrtSegment[segs.Count];
				var rootsLastNdx = roots.Length - 1;
				int addrNdx = 0;
				int segIndex = 0;
				uint[] sizes = new uint[addresses.Length];
				uint[] baseSizes = new uint[addresses.Length];
				int[] elemetTypes = new int[addresses.Length];
				var arraySizes = new List<KeyValuePair<int, int>>(addresses.Length/25);

				for (int segNdx = 0, icnt = segs.Count; segNdx < icnt; ++segNdx)
				{
					var seg = segs[segNdx];
					var genCounts = new int[3];
					var genSizes = new ulong[3];
					var genFreeCounts = new int[3];
					var genFreeSizes = new ulong[3];

					ulong addr = seg.FirstObject;
					ulong firstAddr = addr, lastAddr = addr;
					while (addr != 0ul)
					{
						var clrType = heap.GetObjectType(addr);

						var typeNameKey = clrType == null ? Constants.NullTypeName : clrType.Name;
						int typeId = Array.BinarySearch(typeNames, typeNameKey, StringComparer.Ordinal);
						typeIds[addrNdx] = typeId;
						elemetTypes[addrNdx] = clrType == null ? (int) ClrElementType.Unknown : (int) clrType.ElementType;
						if (clrType == null) goto NEXT_OBJECT;

						int rootNdx = Utils.AddressSearch(roots, addr);
						int finlNdx = Utils.AddressSearch(finalizers, addr);
						if (rootNdx < 0 && finlNdx < 0)
						{
							addresses[addrNdx] = addr;
						}
						else if (rootNdx >= 0 && finlNdx >= 0)
						{
							addresses[addrNdx] = Utils.SetAsFinalizer(roots[rootNdx]);
						}
						else
						{
							if (rootNdx >= 0)
								addresses[addrNdx] = roots[rootNdx];
							else
								addresses[addrNdx] = Utils.SetAsFinalizer(addr);
						}
						var isFree = Utils.SameStrings(clrType.Name, Constants.FreeTypeName);
						var baseSize = clrType.BaseSize;
						baseSizes[addrNdx] = (uint) baseSize;
						var size = clrType.GetSize(addr);
						if (size > (ulong) UInt32.MaxValue) size = (ulong) UInt32.MaxValue;
						sizes[addrNdx] = (uint) size;

						// get generation stats
						//
						if (isFree)
							ClrtSegment.SetGenerationStats(seg, addr, size, genFreeCounts, genFreeSizes);
						else
							ClrtSegment.SetGenerationStats(seg, addr, size, genCounts, genSizes);
						if (clrType.IsArray)
						{
							int asz = clrType.GetArrayLength(addr);
							arraySizes.Add(new KeyValuePair<int, int>(addrNdx, asz));
						}

						NEXT_OBJECT:
						lastAddr = addr;
						addr = seg.NextObject(addr);
						++addrNdx;
					}
					// set segment info
					//
					mysegs[segNdx] = new ClrtSegment(heap.Segments[segNdx], firstAddr, lastAddr, segIndex, addrNdx - 1);
					segIndex = addrNdx;
					mysegs[segNdx].SetGenerationStats(genCounts, genSizes, genFreeCounts, genFreeSizes);
				}

				// dump segments info
				//
				if (
					!ClrtSegment.DumpSegments(_fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapSegmentInfoFilePostfix),
						mysegs, out error))
				{
					_errors[_currentRuntimeIndex].Add("DumpSegments failed." + Environment.NewLine + error);
				}

				// dump sizes
				//
				if (
					!Utils.WriteUintArray(_fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapInstanceSizesFilePostfix), sizes,
						out error))
				{
					_errors[_currentRuntimeIndex].Add("Dumping sizes failed." + Environment.NewLine + error);
				}
				if (
					!Utils.WriteUintArray(_fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapInstanceBaseSizesFilePostfix),
						baseSizes, out error))
				{
					_errors[_currentRuntimeIndex].Add("Dumping base sizes failed." + Environment.NewLine + error);
				}
				if (
					!Utils.WriteIntArray(_fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapInstanceElemTypesFilePostfix),
						elemetTypes, out error))
				{
					_errors[_currentRuntimeIndex].Add("Dumping element types failed." + Environment.NewLine + error);
				}
				if (
					!Utils.WriteKvIntIntArray(_fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapArraySizesFilePostfix),
						arraySizes, out error))
				{
					_errors[_currentRuntimeIndex].Add("Dumping array sizes failed." + Environment.NewLine + error);
				}

				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}

		private void GetThreadsInfos(object param)
		{
			var parameters = param as Tuple<ClrtDump, ulong[], int[],string[]>;
			var clrtDump = parameters.Item1;
			var instances = parameters.Item2;
			var typeIds = parameters.Item3;
			var typeNames = parameters.Item4;

			BinaryWriter bw = null;
			StreamWriter sw = null;
			try
			{
				var heap = clrtDump.Runtimes[_currentRuntimeIndex].GetHeap();
				var threads = DumpIndexer.GetThreads(clrtDump.Runtimes[_currentRuntimeIndex]);
				var blocks = DumpIndexer.GetBlockingObjects(heap);
				var threadSet = new HashSet<ClrThread>(new ClrThreadEqualityCmp());
				var blkGraph = new List<Tuple<BlockingObject, ClrThread[], ClrThread[]>>();
				var allBlkList = new List<BlockingObject>();
				var owners = new List<ClrThread>();
				var waiters = new List<ClrThread>();
				for (int i = 0, icnt = blocks.Length; i < icnt; ++i)
				{
					var blk = blocks[i];
					owners.Clear();
					waiters.Clear();
					ClrThread owner = null;
					if (blk.Taken && blk.HasSingleOwner)
					{
						owner = blk.Owner;
						if (owner != null)
						{
							threadSet.Add(owner);
							owners.Add(owner);
						}
					}

					if (blk.Owners != null && blk.Owners.Count > 0)
					{
						for (int j = 0, jcnt = blk.Owners.Count; j < jcnt; ++j)
						{
							var th = blk.Owners[j];
							if (th != null)
							{
								threadSet.Add(th);
								if (owner == null || owner.Address != th.Address)
									owners.Add(th);
							}
						}
					}

					if (blk.Waiters != null && blk.Waiters.Count > 0)
					{
						for (int j = 0, jcnt = blk.Waiters.Count; j < jcnt; ++j)
						{
							var th = blk.Waiters[j];
							if (th != null)
							{
								threadSet.Add(th);
								waiters.Add(th);
							}
						}
					}

					if (owners.Count > 0)
					{
						var ownerAry = owners.ToArray();
						var waiterAry = waiters.ToArray();
						blkGraph.Add(new Tuple<BlockingObject, ClrThread[], ClrThread[]>(blk, ownerAry, waiterAry));
						allBlkList.Add(blk);
					}
					else if (waiters.Count > 0)
					{
						blkGraph.Add(new Tuple<BlockingObject, ClrThread[], ClrThread[]>(blk, Utils.EmptyArray<ClrThread>.Value,
							waiters.ToArray()));
						allBlkList.Add(blk);
					}
				}

				var thrCmp = new ClrThreadCmp();
				var blkCmp = new BlockingObjectCmp();
				var blkInfoCmp = new BlkObjInfoCmp();

				// blocks and threads found in blocking objects
				//
				var blkThreadAry = threadSet.ToArray();
				Array.Sort(blkThreadAry, thrCmp);
				var blkBlockAry = allBlkList.ToArray();
				Array.Sort(blkBlockAry, blkCmp);
				var threadBlocksAry = blkGraph.ToArray();
				Array.Sort(threadBlocksAry, blkInfoCmp);

				// create maps
				//
				int[] blkMap = new int[blkBlockAry.Length];
				int[] thrMap = new int[blkThreadAry.Length];
				for (int i = 0, icnt = blkMap.Length; i < icnt; ++i)
				{
					blkMap[i] = Array.BinarySearch(blocks, blkBlockAry[i], blkCmp);
					Debug.Assert(blkMap[i] >= 0);
				}

				for (int i = 0, icnt = thrMap.Length; i < icnt; ++i)
				{
					thrMap[i] = Array.BinarySearch(threads, blkThreadAry[i], thrCmp);
					Debug.Assert(thrMap[i] >= 0);
				}

				int blkThreadCount = blkThreadAry.Length;
				Digraph graph = new Digraph(blkThreadCount + blkBlockAry.Length);

				for (int i = 0, cnt = threadBlocksAry.Length; i < cnt; ++i)
				{
					var blkInfo = threadBlocksAry[i];
					for (int j = 0, tcnt = blkInfo.Item2.Length; j < tcnt; ++j)
					{
						var ndx = Array.BinarySearch(blkThreadAry, blkInfo.Item2[j], thrCmp);
						Debug.Assert(ndx >= 0);
						graph.AddDistinctEdge(blkThreadCount + i, ndx);
					}
					for (int j = 0, tcnt = blkInfo.Item3.Length; j < tcnt; ++j)
					{
						var ndx = Array.BinarySearch(blkThreadAry, blkInfo.Item3[j], thrCmp);
						Debug.Assert(ndx >= 0);
						graph.AddDistinctEdge(ndx, blkThreadCount + i);
					}
				}

				var cycle = new DirectedCycle(graph);
				var cycles = cycle.GetCycle();

				// save graph
				//
				var path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapThreadsAndBlocksGraphFilePostfix);
				bw = new BinaryWriter(File.Open(path, FileMode.Create));
				bw.Write(cycles.Length);
				for (int i = 0, icnt = cycles.Length; i < icnt; ++i)
				{
					bw.Write(cycles[i]);
				}
				bw.Write(thrMap.Length);
				for (int i = 0, icnt = thrMap.Length; i < icnt; ++i)
				{
				bw.Write(thrMap[i]);
				}
				bw.Write(blkMap.Length);
				for (int i = 0, icnt = blkMap.Length; i < icnt; ++i)
				{
					bw.Write(blkMap[i]);
				}
				string error;
				graph.Dump(bw, out error);
				bw.Close();
				bw = null;
				AddError(_currentRuntimeIndex, "Exception in GetThreadsInfos." + Environment.NewLine + error);


				// get frames info
				//

				var frames = new StringIdDct();
				var stObjCmp = new Utils.KVIntUlongCmpAsc();
				var stackObject = new SortedDictionary<KeyValuePair<int, ulong>, int>(stObjCmp);
				var stackTraceLst = new List<ClrStackFrame>();
				var rootEqCmp = new ClrRootEqualityComparer();
				var frameIds = new List<int>();
				var aliveIds = new List<int>();
				var deadIds = new List<int>();


				// save threads and blocks, generate stack info
				//
				path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapThreadsAndBlocksFilePostfix);
				bw = new BinaryWriter(File.Open(path, FileMode.Create));
				bw.Write(threads.Length);
				for (int i = 0, icnt = threads.Length; i < icnt; ++i)
				{
					var thread = threads[i];
					stackTraceLst.Clear();
					foreach (var st in thread.EnumerateStackTrace())
					{
						stackTraceLst.Add(st);
						if (stackTraceLst.Count > 100) break;
					}

					var threadLocalAliveVars = thread.EnumerateStackObjects(false).ToArray();
					var all = thread.EnumerateStackObjects(true).ToArray();
					var threadLocalDeadVars = all.Except(threadLocalAliveVars, rootEqCmp).ToArray();
					var threadFrames = stackTraceLst.ToArray();

					aliveIds.Clear();
					for (int j = 0, jcnt = threadLocalAliveVars.Length; j < jcnt; ++j)
					{
						ClrRoot root = threadLocalAliveVars[j];
						ClrType clrType = heap.GetObjectType(root.Object);
						var typeName = clrType == null ? Constants.NullTypeName : clrType.Name;
						var typeId = Array.BinarySearch(typeNames, typeName, StringComparer.Ordinal);
						if (typeId < 0) typeId = Constants.InvalidIndex;
						int stackId;
						if (!stackObject.TryGetValue(new KeyValuePair<int, ulong>(typeId, root.Object), out stackId))
						{
							stackId = stackObject.Count;
							stackObject.Add(new KeyValuePair<int, ulong>(typeId, root.Object), stackId);
						}
						aliveIds.Add(stackId);
					}

					deadIds.Clear();
					for (int j = 0, jcnt = threadLocalDeadVars.Length; j < jcnt; ++j)
					{
						ClrRoot root = threadLocalDeadVars[j];
						ClrType clrType = heap.GetObjectType(root.Object);
						var typeName = clrType == null ? Constants.NullTypeName : clrType.Name;
						var typeId = Array.BinarySearch(typeNames, typeName, StringComparer.Ordinal);
						if (typeId < 0) typeId = Constants.InvalidIndex;
						int stackId;
						if (!stackObject.TryGetValue(new KeyValuePair<int, ulong>(typeId, root.Object), out stackId))
						{
							stackId = stackObject.Count;
							stackObject.Add(new KeyValuePair<int, ulong>(typeId, root.Object), stackId);
						}
						deadIds.Add(stackId);
					}

					frameIds.Clear();
					for (int j = 0, jcnt = threadFrames.Length; j < jcnt; ++j)
					{
						ClrStackFrame fr = threadFrames[j];
						if (fr.Method != null)
						{
							string fullSig = fr.Method.GetFullSignature();
							if (fullSig == null)
								fullSig = fr.Method.Name;
							if (fullSig == null) fullSig = "UKNOWN METHOD";
							var frameStr = Utils.RealAddressStringHeader(fr.InstructionPointer) + fullSig;
							var frId = frames.JustGetId(frameStr);
							frameIds.Add(frId);
						}
						else
						{
							string sig = string.IsNullOrEmpty(fr.DisplayString) ? "UKNOWN METHOD" : fr.DisplayString;
							var frameStr = Utils.RealAddressStringHeader(fr.InstructionPointer) + sig;
							var frId = frames.JustGetId(frameStr);
							frameIds.Add(frId);
						}
					}

					var clrtThread = new ClrtThread(thread, blocks, blkCmp,frameIds.ToArray(),aliveIds.ToArray(),deadIds.ToArray());
					Debug.Assert(clrtThread!=null);
					clrtThread.Dump(bw);
				}


				bw.Write(blocks.Length);
				for (int i = 0, icnt = blocks.Length; i < icnt; ++i)
				{
					var blk = blocks[i];
					var ndx = Array.BinarySearch(blkBlockAry, blk, blkCmp);
					var typeId = GetTypeId(blk.Object, instances, typeIds);
					var clrtBlock = new ClrtBlkObject(blk, ndx, typeId);
					Debug.Assert(clrtBlock!=null);
					clrtBlock.Dump(bw);
				}
				Utils.CloseStream(ref bw);

				path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapThreadFramesFilePostfix);
				bw = new BinaryWriter(File.Open(path, FileMode.Create));
				var ary = stackObject.ToArray();
				Array.Sort(ary, (a, b) => a.Value < b.Value ? -1 : (a.Value > b.Value ? 1 : 0));
				bw.Write(ary.Length);
				for (int i = 0, icnt = ary.Length; i < icnt; ++i)
				{
					bw.Write(ary[i].Key.Key);
					bw.Write(ary[i].Key.Value);
				}
				Utils.CloseStream(ref bw);
				path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.TxtThreadFrameDescriptionFilePostfix);
				frames.DumpInIdOrder(path, out error);
			}
			catch (Exception ex)
			{
				AddError(_currentRuntimeIndex, "Exception in GetThreadsInfos." + Environment.NewLine + Utils.GetExceptionErrorString(ex));
			}
			finally
			{
				bw?.Close();
				sw?.Close();
				clrtDump?.Dispose();
			}
		}

		public int GetTypeId(ulong addr, ulong[] instances, int[] typeIds)
		{
			int inst = Utils.AddressSearch(instances, addr);
			return inst < 0 ? Constants.InvalidIndex : typeIds[inst];
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="clrtDump">Our dump wrapper.</param>
		/// <param name="progress">Report progress if this is not null.</param>
		/// <param name="error">Output exception error.</param>
		/// <returns>True if successful.</returns>
		public bool GetPrerequisites(ClrtDump clrtDump, IProgress<string> progress, out StringIdDct[] strIds,
			out string error)
		{
			error = null;
			strIds = null;
			progress?.Report(Utils.TimeString(DateTime.Now) + " Getting prerequisites...");
			try
			{
				strIds = new StringIdDct[clrtDump.RuntimeCount];
				//_clrtAppDomains = new ClrtAppDomains[clrtDump.RuntimeCount];
				for (int r = 0, rcnt = clrtDump.Runtimes.Length; r < rcnt; ++r)
				{
					_errors[r] = new ConcurrentBag<string>();
					_stringIdDcts[r] = new StringIdDct();
					_stringIdDcts[r].AddKey("[]");
					_stringIdDcts[r].AddKey(Constants.Unknown);
					var clrRuntime = clrtDump.Runtimes[r];
					//_clrtAppDomains[r] = GetAppDomains(clrRuntime, _stringIdDcts[r]);
				}

				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}

        public bool GetTargetModuleInfos(ClrtDump clrtDump, IProgress<string> progress, out string error)
        {
            error = null;
            progress?.Report(Utils.TimeString(DateTime.Now) + " Getting target module infos...");
            try
            {
                var target = clrtDump.DataTarget;
                var modules = target.EnumerateModules().ToArray();
                var dct = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var lst = new string[3];
                for (int i = 0, icnt = modules.Length; i < icnt; ++i)
                {
                    var module = modules[i];
                    var moduleName = Path.GetFileName(module.FileName);
                    var key = moduleName + Constants.HeavyGreekCrossPadded + module.FileName;
                    int nameNdx = 1;
                    while (dct.ContainsKey(key))
                    {
                        moduleName = moduleName + "(" + nameNdx + ")";
                        key = moduleName + Constants.HeavyGreekCrossPadded + module.FileName;
                        ++nameNdx;
                    }
                    lst[0] = Utils.AddressString(module.ImageBase);
                    lst[1] = Utils.SizeString(module.FileSize);
                    lst[2] = module.Version.ToString();
                    var entry = string.Join(Constants.HeavyGreekCrossPadded, lst);
                    if (!dct.ContainsKey(key)) // just in case
                        dct.Add(key, entry);
                    else
                        AddError(0, "DataTarget.EnumerateModules return duplicate modules: " + key);
                }
                string[] extraInfos = new[]
                {
                    _fileMoniker.Path,
                    "Module Count: " + Utils.CountString(dct.Count)
                };
                DumpModuleInfos(dct, extraInfos);
                return true;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return false;
            }
        }

        private void DumpModuleInfos(SortedDictionary<string, string> dct, IList<string> extraInfos)
        {
            var path = _fileMoniker.GetFilePath(-1, Constants.TxtTargetModulesPostfix);
            StreamWriter txtWriter = null;
            try
            {
                ResetReadOnlyAttribute(path);
                txtWriter = new StreamWriter(path);
                txtWriter.WriteLine("### MDRDESK REPORT: Target Module Infos");
                txtWriter.WriteLine("### TITLE: Target Modules");
                txtWriter.WriteLine("### COUNT: " + Utils.LargeNumberString(dct.Count));
                txtWriter.WriteLine("### COLUMNS: Image Base|uint64"
                                    + Constants.HeavyGreekCrossPadded + "File Size|uint32"
                                    + Constants.HeavyGreekCrossPadded + "Version|string"
                                    + Constants.HeavyGreekCrossPadded + "File Name|string" +
                                    Constants.HeavyGreekCrossPadded + "Path|string");
                txtWriter.WriteLine("### SEPARATOR: " + Constants.HeavyGreekCrossPadded);

                for (int i = 0, icnt = extraInfos.Count; i < icnt; ++i)
                {
                    txtWriter.WriteLine(ReportFile.DescrPrefix + extraInfos[i]);
                }
                foreach (var kv in dct)
                {
                    txtWriter.Write(kv.Value);
                    txtWriter.Write(Constants.HeavyGreekCrossPadded);
                    txtWriter.WriteLine(kv.Key);
                }
            }
            catch (Exception ex)
            {
                AddError(-1, "[Indexer.DumpModuleInfos]" + Environment.NewLine + Utils.GetExceptionErrorString(ex));
            }
            finally
            {
                if (txtWriter != null)
                {
                    txtWriter.Close();
                    File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
                }
            }
        }

        ///// <summary>
        ///// Getting domains info.
        ///// </summary>
        ///// <param name="runtime">A dump runtime.</param>
        ///// <param name="idDct">String cache.</param>
        ///// <returns>Doamins information.</returns>
        ///// <remarks>It is public and static for unit tests.</remarks>
        //public static ClrtAppDomains GetAppDomains(ClrRuntime runtime, StringIdDct idDct)
        //{
        //	var systenDomain = runtime.SystemDomain == null
        //		? new ClrtAppDomain()
        //		: new ClrtAppDomain(runtime.SystemDomain, idDct);
        //	var sharedDomain = runtime.SharedDomain == null
        //		? new ClrtAppDomain()
        //		: new ClrtAppDomain(runtime.SharedDomain, idDct);
        //	var domains = new ClrtAppDomains(systenDomain, sharedDomain);
        //	var appDomainCnt = runtime.AppDomains.Count;
        //	ClrtAppDomain[] appDomains = new ClrtAppDomain[appDomainCnt];
        //	for (int i = 0; i < appDomainCnt; ++i)
        //	{
        //		appDomains[i] = new ClrtAppDomain(runtime.AppDomains[i], idDct);
        //	}
        //	domains.AddAppDomains(appDomains);
        //	return domains;
        //}

        private void DumpErrors()
		{
			StreamWriter errWriter = null;
			try
			{
				for (int r = 0; r < _errors.Length; ++r)
				{
					var path = _fileMoniker.GetFilePath(r, Constants.TxtIndexErrorsFilePostfix);
					errWriter = new StreamWriter(path);
					errWriter.WriteLine("ERROR COUNT: " + _errors[r].Count);
					errWriter.WriteLine();
					while (!_errors[r].IsEmpty)
					{
						string error;
						if (_errors[r].TryTake(out error))
						{
							errWriter.WriteLine(error);
						}
					}
					_errors[r] = null;
					Utils.CloseStream(ref errWriter);
				}
			}
			catch
			{
			}
			finally
			{
				errWriter?.Close();
			}
		}

		#region data dumping

		private void DumpIndexInfo(Version version, ClrtDump dump, string indexingDuration = null)
		{
			var path = _fileMoniker.GetFilePath(-1, Constants.TxtIndexInfoFilePostfix);
			StreamWriter txtWriter = null;
			int i = 0;
			try
			{
				ResetReadOnlyAttribute(path);
				txtWriter = new StreamWriter(path);
				txtWriter.WriteLine("MDR Version: [" 
									+ version 
									+ "], this is this application version.");
				txtWriter.WriteLine("Dump Path: " + _fileMoniker.Path);
				if (!string.IsNullOrWhiteSpace(indexingDuration))
				{
					txtWriter.WriteLine("Indexing Duration: " + indexingDuration);
				}
				var runtimes = dump.Runtimes;
				var clrinfos = dump.ClrInfos;
				txtWriter.WriteLine("Runtime Count: " + runtimes.Length);
				txtWriter.WriteLine();
				for (i = 0; i < runtimes.Length; ++i)
				{
					txtWriter.WriteLine("RUNTIME " + i);
					var runtime = runtimes[i];
					var clrinfo = clrinfos[i];
					txtWriter.WriteLine("Dac Path: " + dump.DacPaths[i]);
					txtWriter.WriteLine("Runtime version: " + clrinfo?.Version.ToString());
					txtWriter.WriteLine("Clr type: " + clrinfo?.Flavor.ToString());
					txtWriter.WriteLine("Module info, file name: " + clrinfo?.ModuleInfo?.FileName);
					txtWriter.WriteLine("Module info, image base: " + $"0x{clrinfo?.ModuleInfo?.ImageBase:x14}");
					txtWriter.WriteLine("Module info, file size: " + $"{clrinfo?.ModuleInfo?.FileSize:#,###}");
					txtWriter.WriteLine("Heap count: " + runtime.HeapCount);
					txtWriter.WriteLine("Pointer size: " + runtime.PointerSize);
					txtWriter.WriteLine("Is server GC : " + runtime.ServerGC);
					if (runtime.AppDomains != null)
					{
						for (int j = 0, jcnt = runtime.AppDomains.Count; j < jcnt; ++j)
						{
							var appDoamin = runtime.AppDomains[j];
							txtWriter.WriteLine("Application domain: " + (appDoamin.Name ?? "unnamed") + ", id: " +
												appDoamin.Id + ", module cnt: " + appDoamin.Modules.Count);
						}
					}
					if (runtime.SystemDomain != null)
						txtWriter.WriteLine("System domain: " + (runtime.SystemDomain.Name ?? "unnamed") + ", id: " +
											runtime.SystemDomain.Id + ", module cnt: " +
											runtime.SystemDomain.Modules.Count);
					if (runtime.SharedDomain != null)
						txtWriter.WriteLine("Shared domain: " + (runtime.SharedDomain.Name ?? "unnamed") + ", id: " +
											runtime.SharedDomain.Id + ", module cnt: " +
											runtime.SharedDomain.Modules.Count);

					// TODO JRD -- add this
					//txtWriter.WriteLine("Instance Count: " + Utils.LargeNumberString(_instances[i].Length));
					//txtWriter.WriteLine("Type Count: " + Utils.LargeNumberString(_types[i].Count));
					//txtWriter.WriteLine("Finalizer Queue Count: " +
					//					Utils.LargeNumberString(_clrtRoots[i].FinalizerQueueCount));
					//txtWriter.WriteLine("Roots Count: " + Utils.LargeNumberString(_clrtRoots[i].RootsCount));

					txtWriter.WriteLine();
				}
			}
			catch (Exception ex)
			{
				AddError(i, "[Indexer.DumpIndexInfo]" + Environment.NewLine + Utils.GetExceptionErrorString(ex));
			}
			finally
			{
				txtWriter?.Close();
				//if (txtWriter != null)
				//{
				//	txtWriter.Close();
				//	File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
				//}
			}
		}

		#endregion data dumping

		#region indexing helpers

		public static void MarkAsRooted(ulong addr, int addrNdx, ulong[] instances, int[][] references)
		{
			if (Utils.IsRooted(addr)) return;
			instances[addrNdx] = Utils.SetAsRooted(addr);
			if (references[addrNdx] == null) return;
			for (int i = 0, icnt = references[addrNdx].Length; i < icnt; ++i)
			{
				var refr = references[addrNdx][i];
				var address = instances[refr];
				if (Utils.IsNotRooted(address))
					if (address != 0UL)
					{
						instances[refr] = Utils.SetAsRooted(address);
						MarkAsRooted(address, refr, instances, references);
					}
			}
		}

		public static string[] GetTypeNames(ClrHeap heap, out string error)
		{
			error = null;
			try
			{
				List<string> typeNames = new List<string>(35000);
				AddStandardTypeNames(typeNames);
				var typeList = heap.EnumerateTypes();
				typeNames.AddRange(typeList.Select(clrType => clrType.Name));
				typeNames.Sort(StringComparer.Ordinal);
				string[] names = typeNames.Distinct().ToArray();
				return names;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}



		public static void AddStandardTypeNames(List<string> typeNames)
		{
			typeNames.Add(ClrtType.GetKey(Constants.NullTypeName, 0));
			typeNames.Add(ClrtType.GetKey(Constants.UnknownTypeName, 0));
		}

		private void AddError(int rtNdx, string error)
		{
			_errors[rtNdx].Add(DateTime.Now.ToString("s") + Environment.NewLine + error);
		}

		private void ResetReadOnlyAttribute(string path)
		{
			if (File.Exists(path))
			{
				File.SetAttributes(path, FileAttributes.Normal);
			}
		}

		public static ClrThread[] GetThreads(ClrRuntime runtime)
		{
			var lst = new List<ClrThread>(256);
			var thrdLst = runtime.Threads;
			for (int i = 0, icnt = thrdLst.Count; i < icnt; ++i)
			{
				lst.Add(thrdLst[i]);
			}
			lst.Sort(new ClrThreadCmp());
			return lst.ToArray();
		}

		public static int GetThreadId(ClrThread[] ary, ClrThread thrd)
		{
			if (thrd == null) return Constants.InvalidIndex;
			return Array.BinarySearch(ary, thrd, new ClrThreadCmp());
		}

		public static BlockingObject[] GetBlockingObjects(ClrHeap heap)
		{
			var lst = new List<BlockingObject>(1024);
			foreach (var block in heap.EnumerateBlockingObjects())
			{
				var clrType = heap.GetObjectType(block.Object);
				if (clrType == null || clrType.Name == "Free") continue;
				lst.Add(block);
			}
			var ary = lst.ToArray();
			Array.Sort(ary,new BlockingObjectCmp());
			return ary;
		}

		public static BlockingObject[] GetBlockingObjectsEx(ClrHeap heap, out BlockingObject[] free)
		{
			var lst = new List<BlockingObject>(1024);
			var freelst = new List<BlockingObject>(1024);
			foreach (var block in heap.EnumerateBlockingObjects())
			{
				var clrType = heap.GetObjectType(block.Object);
				if (clrType == null || clrType.Name == "Free")
				{
					freelst.Add(block);
					continue;
				}
				lst.Add(block);
			}
			var cmp = new BlockingObjectCmp();
			var ary = lst.ToArray();
			Array.Sort(ary, cmp);
			free = freelst.ToArray();
			Array.Sort(free,cmp);
			return ary;
		}


		#endregion indexing helpers
	}

	public class BlkObjInfoCmp : IComparer<Tuple<BlockingObject, ClrThread[], ClrThread[]>>
	{
		public int Compare(Tuple<BlockingObject, ClrThread[], ClrThread[]> a,
			Tuple<BlockingObject, ClrThread[], ClrThread[]> b)
		{
			return a.Item1.Object < b.Item1.Object ? -1 : (a.Item1.Object > b.Item1.Object ? 1 : 0);
		}
	}
}
