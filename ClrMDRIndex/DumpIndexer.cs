using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
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

		public bool CreateDumpIndex(Version version, IProgress<string> progress, IndexingArguments indexArguments, out string indexPath, out string error)
		{
			error = null;
			indexPath = _fileMoniker.MapFolder;
			var clrtDump = new ClrtDump(DumpPath);
			if (!clrtDump.Init(out error)) return false;
			Stopwatch stopWatch = new Stopwatch();
			string durationStr = string.Empty;
			_errors = new ConcurrentBag<string>[clrtDump.RuntimeCount];

			using (clrtDump)
			{
				try
				{
					if (DumpFileMoniker.GetAndCreateMapFolders(DumpPath, out error) == null) return false;
					indexPath = IndexFolder;
					// indexing
					//
					if (!GetPrerequisites(clrtDump, progress, out _stringIdDcts, out error)) return false;

					for (int r = 0, rcnt = clrtDump.RuntimeCount; r < rcnt; ++r)
					{
						_currentRuntimeIndex = r;
						string runtimeIndexHeader = Utils.RuntimeStringHeader(r);
						clrtDump.SetRuntime(r);
						ClrRuntime runtime = clrtDump.Runtime;
						ClrHeap heap = runtime.GetHeap();
						ConcurrentBag<string> errors = new ConcurrentBag<string>();
						_errors[r] = errors;


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
							var rootArys = ClrtRoots.GetRootAddresses(heap, typeNames);
							roots = rootArys.Key;
							objects = rootArys.Value;
							durationStr = Utils.StopAndGetDurationString(stopWatch);

							// get addresses and set roots
							//
							progress?.Report(runtimeIndexHeader + "Getting addresses and setting roots... Previous action duration: " + durationStr);
							stopWatch.Restart();
							addresses = new ulong[addrCount];
							typeIds = new int[addrCount];
							if (!GetAddressesSetRoots(heap, addresses, typeIds, typeNames, roots, objects, out error))
							{
								return false;
							}
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
								var cleanAddr = Utils.CleanAddress(addr);
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
										if (isArray && kcnt > 100)
										{
											int a = 1;
										}
										var fldAddr = fldRefList[k].Key;
										var addrnx = Utils.AddressSearch(addresses, fldAddr, 0, addressesLastNdx);
										if (fldAddr == 0UL || fldRefs.Contains(addrnx)) continue;
										if (addrnx < 0)
										{
											int a = 1;
										}
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

							// get roots info
							//
							{
								var rootInfos = ClrtRoots.GetRootInfos(heap, addresses, typeIds, _stringIdDcts[r]);
								if (!rootInfos.PersitRootInfos(r, _fileMoniker, out error))
								{
									AddError(r, "PersitRootInfos failed." + Environment.NewLine + error);
									return false;
								}
							}

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
							progress?.Report(runtimeIndexHeader + "Building instance reference map... Previous action duration: " + durationStr);

							InstanceReferences.InvertFieldRefs(new Tuple<int, DumpFileMoniker, ConcurrentBag<string>, IProgress<string>>(r, _fileMoniker, errors, null));

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



							//if (indexArguments == IndexingArguments.JustInstanceRefs) return true; // we only want instance refs
						}


						runtime.Flush();
						heap = null;
					}

					DumpIndexInfo(version, clrtDump);
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
				brOffsets = new BinaryWriter(File.Open(_fileMoniker.GetFilePath(rtNdx, Constants.MapTypeInstanceOffsetsFilePostfix), FileMode.Create));
				var indices = Utils.Iota(typeIds.Length);
				var ids = new int[typeIds.Length];
				Array.Copy(typeIds, 0, ids, 0, typeIds.Length);
				Array.Sort(ids, indices);
				brOffsets.Write((int)0);
				int prev = ids[0];
				brOffsets.Write(prev);
				brOffsets.Write((int)0);
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

		public bool GetAddressesSetRoots(ClrHeap heap, ulong[] addresses, int[] typeIds, string[] typeNames, ulong[] roots, ulong[] objects, out string error)
		{
			error = null;
			try
			{
				var segs = heap.Segments;
				var rootsLastNdx = roots.Length - 1;
				var objectsLastNdx = objects.Length - 1;
				int addrNdx = 0;
				int segIndex = 0;
				uint[] sizes = new uint[addresses.Length];
				uint[] baseSizes = new uint[addresses.Length];
				ClrtSegment[] mysegs = new ClrtSegment[segs.Count];

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
						if (clrType == null) goto NEXT_OBJECT;

						var isRoot = Utils.AddressSearch(roots, addr, 0, rootsLastNdx) >= 0;
						var isPointee = Utils.AddressSearch(objects, addr, 0, objectsLastNdx) >= 0;
						if (isRoot || isPointee)
						{
							addresses[addrNdx] = Utils.SetRooted(addr, isRoot, isPointee);
						}
						else
						{
							addresses[addrNdx] = addr;
						}
						var isFree = Utils.SameStrings(clrType.Name, Constants.FreeTypeName);
						var baseSize = clrType.BaseSize;
						baseSizes[addrNdx] = (uint)baseSize;
						var size = clrType.GetSize(addr);
						if (size > (ulong)UInt32.MaxValue) size = (ulong)UInt32.MaxValue;
						sizes[addrNdx] = (uint)size;

						// get generation stats
						//
						if (isFree)
							ClrtSegment.SetGenerationStats(seg, addr, size, genFreeCounts, genFreeSizes);
						else
							ClrtSegment.SetGenerationStats(seg, addr, size, genCounts, genSizes);


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
				if (!ClrtSegment.DumpSegments(_fileMoniker.GetFilePath(_currentRuntimeIndex,Constants.MapSegmentInfoFilePostfix), mysegs, out error))
				{
					_errors[_currentRuntimeIndex].Add("DumpSegments failed." + Environment.NewLine + error);
				}

				// dump sizes
				//
				if (!Utils.WriteUintArray(_fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapInstanceSizesFilePostfix), sizes,out error))
				{
					_errors[_currentRuntimeIndex].Add("Dumping sizes failed." + Environment.NewLine + error);
				}
				if (!Utils.WriteUintArray(_fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapInstanceBaseSizesFilePostfix), baseSizes, out error))
				{
					_errors[_currentRuntimeIndex].Add("Dumping base sizes failed." + Environment.NewLine + error);
				}

				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
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

		private void DumpIndexInfo(Version version, ClrtDump dump)
		{
			var path = _fileMoniker.GetFilePath(-1, Constants.TxtIndexInfoFilePostfix);
			StreamWriter txtWriter = null;
			int i = 0;
			try
			{
				ResetReadOnlyAttribute(path);
				txtWriter = new StreamWriter(path);
				txtWriter.WriteLine("MDR Version: [" + Utils.VersionValue(version).ToString() +
									"], this is this application version.");
				txtWriter.WriteLine("Dump Path: " + _fileMoniker.Path);
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

		#endregion indexing helpers
	}
}
