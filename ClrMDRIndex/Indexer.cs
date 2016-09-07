﻿//#define CODETEST
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Threading;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	public class Indexer
	{
		#region Fields/Properties

		/// <summary>
		/// Indexing errors, they all are written to the error file in the index folder.
		/// </summary>
		private ConcurrentBag<string>[] _errors;

		/// <summary>
		/// Dump file name, no extention.
		/// </summary>
		public string DumpFileName { get; private set; }

		/// <summary>
		/// Full dump file path.
		/// </summary>
		public string DumpFilePath { get; private set; }

		/// <summary>
		/// Index output folder.
		/// </summary>
		public string MapOutputFolder { get; private set; }

		private ClrtTypes[] _types;

		/// <summary>
		/// List of the unique type names.
		/// </summary>
		private TypeIdDct[] _typeNameIdDcts;

		/// <summary>
		/// List of the unique field names.
		/// </summary>
		private StringIdDct[] _stringIdDcts;

		/// <summary>
		/// Addresses of all heap instances, for each runtime.
		/// </summary>
		private ulong[][] _instances;

		private uint[][] _sizes;

		/// <summary>
		/// Above instances corresponding type ids.
		/// </summary>
		private int[][] _typeIds;

		/// <summary>
		/// Indices of instances parents.
		/// </summary>
		//private int[][] _parents;

		//private MultiparentIndexer[] _multiparents;

		private KeyValuePair<ClrAppDomain, ClrAppDomain>[] _systemAndSharedDomains;

		private ClrtAppDomains[] _clrtAppDomains;

		private ClrtRoots[] _clrtRoots;

		//		private ArrayIndexer[] _arrayIndexers;


		#endregion Fields/Properties

		#region Ctors/Initialization

		public Indexer(string dmpPath)
		{
			DumpFilePath = dmpPath;
			DumpFileName = Path.GetFileNameWithoutExtension(dmpPath);
			MapOutputFolder = Utils.GetMapFolder(dmpPath);
		}

		#endregion Ctors/Initialization

		#region Indexing

		public bool Index(Version version, IProgress<string> progress, out string error)
		{
			var clrDump = new ClrtDump(DumpFilePath);
			if (!clrDump.Init(out error)) return false;
			using (clrDump)
			{
				try
				{
					if (DumpFileMoniker.GetAndCreateMapFolders(DumpFilePath, out error) == null) return false;

					// collection data
					//
					_errors = new ConcurrentBag<string>[clrDump.RuntimeCount];
					_typeNameIdDcts = new TypeIdDct[clrDump.RuntimeCount];

					// indexing
					//
					if (!GetPrerequisites(clrDump, progress, out _stringIdDcts, out error)) return false;

					var typesAndRoots = GetTypeInfos(clrDump, progress, _stringIdDcts, _errors, out _instances, out _sizes,
						out _typeIds, out error);
					if (typesAndRoots == null) return false;
					_types = typesAndRoots.Item1;
					_clrtRoots = typesAndRoots.Item2;
					if (_types == null) return false;

					var tnames = _types[0].Names;
					Tuple<string, string> badCouple;
					var sorted = Utils.IsSorted(tnames, out badCouple);

					int[] fdNotFoundCnt;
					var gotFields = GetFieldInfos(clrDump, progress, _types, _instances, _typeIds, _stringIdDcts, out fdNotFoundCnt, out error);
					if (!gotFields) return false;

					for (int r = 0, rcnt = clrDump.RuntimeCount; r < rcnt; ++r)
					{
						_types[r].GenerateNamespaceOrdering(_stringIdDcts[r]);
					}

					for (int r = 0, rcnt = clrDump.RuntimeCount; r < rcnt; ++r)
					{
						_types[r].Dump(Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapTypeInfosFilePostfix), out error);
					}

					for (int r = 0, rcnt = clrDump.RuntimeCount; r < rcnt; ++r)
					{
						if (!_clrtRoots[r].Dump(r, MapOutputFolder, DumpFileName, out error)) return false;
					}

					for (int r = 0, rcnt = clrDump.RuntimeCount; r < rcnt; ++r)
					{
						Utils.WriteUlongUintIntArrays(
							Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapInstanceFilePostfix), _instances[r], _sizes[r],
							_typeIds[r], out error);
						// get types map
						//
						int[] offsets;
						var typeMap = Utils.GetIntArrayMapping(_typeIds[r], out offsets);
						Utils.WriteIntArrays(
							Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapTypeInstancesFilePostfix), typeMap,
							offsets, out error);
					}


					//if (!WriteFieldReferences(clrDump, fldInfos.Item1, fldInfos.Item2, out error)) return false;
					if (!WriteStringDct(out error)) return false;

					//progress.Report(Utils.TimeString(DateTime.Now) + " Building instances reference graph...");
					//if (!BuildInstanceReferenceGraph(clrDump, progress, out error)) return false;

					//progress.Report(Utils.TimeString(DateTime.Now) + " Getting roots...");
					//if (!IndexRoots(clrDump, progress, out error)) return false;

					//// saving index maps
					////
					//progress.Report(Utils.TimeString(DateTime.Now) + " Saving index...");
					//if (!WriteFinalizerObjectAddresses(clrDump, out error)) return false;
					//if (!DumpTypeAndFieldNames(out error)) return false;


					for (int r = 0, rcnt = clrDump.RuntimeCount; r < rcnt; ++r)
					{
						List<string> errors = new List<string>(0);
						FieldDependency.SortFieldDependencies(
							new Tuple<string, string, string, List<string>, IProgress<string>>(
								Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapFieldInstancesPostfix),
								Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapFieldParentOffsetsFilePostfix),
								Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapFieldParentInstancesPostfix),
								errors,
								progress
								));
						for (int i = 0, icnt = errors.Count; i < icnt; ++i)
							_errors[r].Add("[FieldDependency.WriteFieldsDependencies]" + Environment.NewLine + errors[i]);
					}

					DumpIndexInfo(version, clrDump);
					Utils.ForceGcWithCompaction();
					return true;
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
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
		/// 
		/// </summary>
		/// <param name="clrtDump">Our dump wrapper.</param>
		/// <param name="progress">Report progress if this is not null.</param>
		/// <param name="error">Output exception error.</param>
		/// <returns>True if successful.</returns>
		public bool GetPrerequisites(ClrtDump clrtDump, IProgress<string> progress, out StringIdDct[] strIds, out string error)
		{
			error = null;
			strIds = null;
			progress?.Report(Utils.TimeString(DateTime.Now) + " Getting prerequisites...");
			try
			{
				strIds = new StringIdDct[clrtDump.RuntimeCount];
				//for (int r = 0, rcnt = clrtDump.RuntimeCount; r < rcnt; ++r)
				//{
				//	var clrRuntime = clrtDump.Runtimes[r];
				//	progress?.Report(Utils.TimeString(DateTime.Now) + " Getting finalize queue addresses...");
				//	var finalizeQueue = clrRuntime.EnumerateFinalizerQueueObjectAddresses().ToArray();
				//	if (!Utils.IsSorted(finalizeQueue)) Array.Sort(finalizeQueue);
				//	var path = Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapFinalizerFilePostfix);
				//	if (!Utils.WriteUlongArray(path, finalizeQueue, out error)) return false;
				//}
				_clrtAppDomains = new ClrtAppDomains[clrtDump.RuntimeCount];
				for (int r = 0, rcnt = clrtDump.Runtimes.Length; r < rcnt; ++r)
				{
					_errors[r] = new ConcurrentBag<string>();
					_stringIdDcts[r] = new StringIdDct();
					_stringIdDcts[r].AddKey("[]");
					_stringIdDcts[r].AddKey(Constants.Unknown);
					var clrRuntime = clrtDump.Runtimes[r];
					_clrtAppDomains[r] = GetAppDomains(clrRuntime, _stringIdDcts[r]);
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
		/// Getting domains info.
		/// </summary>
		/// <param name="runtime">A dump runtime.</param>
		/// <param name="idDct">String cache.</param>
		/// <returns>Doamins information.</returns>
		/// <remarks>It is public and static for unit tests.</remarks>
		public static ClrtAppDomains GetAppDomains(ClrRuntime runtime, StringIdDct idDct)
		{
			var systenDomain = runtime.SystemDomain == null
				? new ClrtAppDomain()
				: new ClrtAppDomain(runtime.SystemDomain, idDct);
			var sharedDomain = runtime.SharedDomain == null
				? new ClrtAppDomain()
				: new ClrtAppDomain(runtime.SharedDomain, idDct);
			var domains = new ClrtAppDomains(systenDomain, sharedDomain);
			var appDomainCnt = runtime.AppDomains.Count;
			ClrtAppDomain[] appDomains = new ClrtAppDomain[appDomainCnt];
			for (int i = 0; i < appDomainCnt; ++i)
			{
				appDomains[i] = new ClrtAppDomain(runtime.AppDomains[i], idDct);
			}
			domains.AddAppDomains(appDomains);
			return domains;
		}

		//public bool GetTypeInstances(ClrtDump clrtDump, IProgress<string> progress, out string error)
		//{
		//	progress?.Report(Utils.TimeString(DateTime.Now) + " Getting type instances...");
		//	error = null;
		//	BinaryWriter freeWriter = null;
		//	BlockingStringCache strCache = new BlockingStringCache(10000);
		//	_clrAppDomains = new ClrAppDomain[clrtDump.Runtimes.Length][];
		//	_systemAndSharedDomains = new KeyValuePair<ClrAppDomain, ClrAppDomain>[clrtDump.Runtimes.Length];

		//	try
		//	{
		//		for (int r = 0, rcnt = clrtDump.Runtimes.Length; r < rcnt; ++r)
		//		{
		//			var clrRuntime = clrtDump.Runtimes[r];
		//			clrRuntime.Flush();
		//			var heap = clrRuntime.GetHeap();

		//			_clrAppDomains[r] = clrRuntime.AppDomains.ToArray();
		//			_systemAndSharedDomains[r] = new KeyValuePair<ClrAppDomain, ClrAppDomain>(clrRuntime.SystemDomain, clrRuntime.SharedDomain);
		//			_arrayIndexers[r] = new ArrayIndexer();
		//			_typeNameIdDcts[r] = new TypeIdDct();
		//			AddStandardStringIds(_typeNameIdDcts[r]);
		//			_stringIdDcts[r] = new StringIdDct();
		//			AddStandardStringIds(_stringIdDcts[r]);


		//			var instances = new TempArena<ulong>(100, 1000000);
		//			var types = new TempArena<int>(100, 1000000);
		//			var sizes = new TempArena<int>(100, 1000000);

		//			_clrtAppDomains[r] = GetAppDomains(clrRuntime, _stringIdDcts[r], out error);

		//			// for Free type collection
		//			int freeCount = 0;
		//			freeWriter = new BinaryWriter(File.Open(Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapHeapFreeFilePostfix), FileMode.Create));
		//			freeWriter.Write(0);
		//			// for segment info collection
		//			var segments = new ClrtSegment[heap.Segments.Count];
		//			int segIndex = 0;

		//			for (int s = 0, scnt = heap.Segments.Count; s < scnt; ++s)
		//			{
		//				progress?.Report(Utils.TimeString(DateTime.Now) +  " Runtime[" + r + "] " + "Processing segment " + (s + 1) + " / " + scnt);
		//				var seg = heap.Segments[s];

		//				ulong firstSegAddr = seg.FirstObject;
		//				ulong addr = firstSegAddr;

		//				while (addr != 0ul)
		//				{
		//					var clrType = heap.GetObjectType(addr);
		//					if (clrType == null)
		//					{
		//						AddError(r, $"[Indexer.Index] ClrHeap.GetObjectType returned null at address 0x{addr:x14}");
		//						goto NEXT_OBJECT;
		//					}

		//					if (clrType.Name == Constants.Free)
		//					{
		//						++freeCount;
		//						freeWriter.Write(addr);
		//						freeWriter.Write(clrType.GetSize(addr));
		//						goto NEXT_OBJECT;
		//					}

		//					var typeName = strCache.GetCachedString(clrType.Name);
		//					bool newType;
		//					int typeId = _typeNameIdDcts[r].GetId(typeName, out newType);
		//					var sz = clrType.GetSize(addr);
		//					instances.Add(addr);
		//					types.Add(typeId);
		//					sizes.Add((int)sz);
		//					if (newType)
		//					{
		//						SetTypeInfo(_typeNameIdDcts[r], clrType, typeId);
		//					}
		//					_typeNameIdDcts[r].AddCount(typeId);

		//					if (clrType.ElementType == ClrElementType.SZArray)
		//					{
		//						var asz = clrType.GetArrayLength(addr);
		//						var componentType = clrType.ComponentType;
		//						int compId = _typeNameIdDcts[r].GetId(componentType == null ? Constants.NullName : componentType.Name, out newType);
		//						_arrayIndexers[r].Add(addr, typeId, asz, compId);
		//						if (newType)
		//						{
		//							SetTypeInfo(_typeNameIdDcts[r], componentType, compId);
		//						}
		//					}

		//					NEXT_OBJECT:
		//					addr = seg.NextObject(addr);
		//				}

		//				var instCount = instances.Count();
		//				segments[s] = new ClrtSegment(heap.Segments[s], firstSegAddr, instances.LastItem(), segIndex, instCount - 1);
		//				segIndex = instCount;
		//			}

		//			freeWriter.Seek(0, SeekOrigin.Begin);
		//			freeWriter.Write(freeCount);
		//			Utils.CloseStream(ref freeWriter);

		//			_instances[r] = instances.GetArray();
		//			_typeIds[r] = types.GetArray();
		//			_sizes[r] = sizes.GetArray();

		//			instances.Clear();
		//			instances = null;
		//			types.Clear();
		//			types = null;
		//			sizes.Clear();
		//			sizes = null;
		//		}

		//		GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
		//		GC.Collect();
		//		return true;
		//	}
		//	catch (Exception ex)
		//	{
		//		error = Utils.GetExceptionErrorString(ex);
		//		return false;
		//	}
		//	finally
		//	{
		//		freeWriter?.Close();
		//	}
		//}

		//private void SetTypeInfo(TypeIdDct idDct, ClrType clrType, int typeId)
		//{
		//	Debug.Assert(clrType != null);
		//	var curType = clrType;
		//	while (curType != null)
		//	{
		//		var baseTypeName = curType.BaseType == null ? Constants.NullName : curType.BaseType.Name;
		//		bool newType;
		//		var baseTypeId = idDct.GetId(baseTypeName, out newType);
		//		var elemType = curType.ElementType;
		//		var fldCnt = curType.Fields.Count;
		//		var staticFldCount = clrType.StaticFields.Count;
		//		idDct.AddRefs(typeId, baseTypeId, elemType, staticFldCount, fldCnt);
		//		if (newType)
		//		{
		//			curType = curType.BaseType;
		//			typeId = baseTypeId;
		//		}
		//		else
		//		{
		//			break;
		//		}
		//	}
		//}

		private static ClrType AddTypeGetBase(string key, ClrType clrType, SortedDictionary<string, ClrtType> typeDct)
		{
			var name = clrType.Name;
			var mthdTbl = clrType.MethodTable;
			var elem = clrType.ElementType;
			var baseName = clrType.BaseType?.Name ?? Constants.NullTypeName;


			typeDct.Add(key, new ClrtType(name, mthdTbl, elem, baseName));
			return clrType.BaseType;
		}

		public static Tuple<ClrtTypes[], ClrtRoots[]> GetTypeInfos(ClrtDump clrtDump,
			IProgress<string> progress,
			StringIdDct[] stringIds,
			ConcurrentBag<string>[] errors,
			out ulong[][] instances,
			out uint[][] sizes,
			out int[][] instanceTypes,
			out string error)
		{
			error = null;
			instances = null;
			instanceTypes = null;
			sizes = null;
			int r = 0;
			try
			{
				progress?.Report("Get type information starting...");
				List<string>[] duplicates = new List<string>[clrtDump.RuntimeCount];
				bool haveDups = false;
				ClrtTypes[] clrtTypes = new ClrtTypes[clrtDump.RuntimeCount];
				ClrtRoots[] clrtRoots = new ClrtRoots[clrtDump.RuntimeCount];

				instances = new ulong[clrtDump.RuntimeCount][];
				instanceTypes = new int[clrtDump.RuntimeCount][];
				sizes = new uint[clrtDump.RuntimeCount][];

				for (int rcnt = clrtDump.Runtimes.Length; r < rcnt; ++r)
				{
					TempArena<ulong> instanceArena = new TempArena<ulong>(10000000, 10);
					TempArena<uint> sizeArena = new TempArena<uint>(10000000, 10);
					TempArena<ulong> mthdtbls = new TempArena<ulong>(10000000, 10);
					var clrRuntime = clrtDump.Runtimes[r];
					clrRuntime.Flush();
					ClrHeap heap = clrRuntime.GetHeap();
					var typeDct = new SortedDictionary<string, ClrtType>(StringComparer.Ordinal)
					{
						{
							ClrtType.GetKey(Constants.NullTypeName, Constants.InvalidAddress),
							new ClrtType(Constants.NullTypeName, Constants.InvalidAddress, ClrElementType.Unknown, Constants.NullTypeName)
						}
					};
					duplicates[r] = new List<string>();

					int segIndex = 0;
					var segs = heap.Segments;
					ClrtSegment[] mysegs = new ClrtSegment[segs.Count];
					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						progress?.Report("[Indexer.GetTypeInfos] Runtime: " + r + ", processing segment: " + i + "/" + icnt);

						var seg = segs[i];
						int segInstCount = 0;
						int segFreeCount = 0;
						ulong segInstSize = 0;
						ulong segFreeSize = 0;

						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							instanceArena.Add(addr);
							var clrType = heap.GetObjectType(addr);
							if (clrType == null)
							{
								mthdtbls.Add(Constants.InvalidAddress);
								sizeArena.Add(0u);
								goto NEXT_OBJECT;
							}
							var sz = clrType.GetSize(addr);
							if (sz > (ulong)UInt32.MaxValue) sz = (ulong)UInt32.MaxValue;
							sizeArena.Add((uint)sz);

							if (Utils.SameStrings(clrType.Name, Constants.Free))
							{
								++segFreeCount;
								segFreeSize += sz;
							}
							else
							{
								++segInstCount;
								segInstSize += sz;
							}


							mthdtbls.Add(clrType.MethodTable);
							string key = ClrtType.GetKey(clrType.Name, clrType.MethodTable);
							if (!typeDct.ContainsKey(key))
							{
								var baseType = AddTypeGetBase(key, clrType, typeDct);
							}

							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
						var instanceCount = instanceArena.Count();
						mysegs[i] = new ClrtSegment(heap.Segments[i], instanceArena.GetItemAt(segIndex), instanceArena.LastItem(),
							segIndex, instanceCount - 1,
							segInstCount, segInstSize, segFreeCount, segFreeSize);
						segIndex = instanceCount;
					}

					// refresh heap
					//
					clrRuntime.Flush();
					heap = null;

					// dump segments info
					//
					if (
						!ClrtSegment.DumpSegments(Utils.GetFilePath(r, Utils.GetMapFolder(clrtDump.DumpPath), clrtDump.DumpFileNameNoExt,
							Constants.MapSegmentFilePostfix), mysegs, out error))
					{
						errors[r].Add("DumpSegments failed." + Environment.NewLine + error);
					}

					// setting fields information
					//
					progress?.Report("Runtime: " + r + ", adding type fields.");
					instances[r] = instanceArena.GetArrayAndClear();
					instanceArena = null;
					Utils.ForceGcWithCompaction();

					sizes[r] = sizeArena.GetArrayAndClear();
					sizeArena = null;
					Utils.ForceGcWithCompaction();

					// get fresh heap
					//
					heap = clrRuntime.GetHeap();

					List<string> fldTypeNames = new List<string>(32);
					List<ulong> fldMts = new List<ulong>(32);
					List<int> fldNameIds = new List<int>(32);
					List<string> statFldTypeNames = new List<string>(8);
					List<ulong> statFldMts = new List<ulong>(8);
					List<int> statFldNameIds = new List<int>(8);

					for (int i = 0, icnt = instances[r].Length; i < icnt; ++i)
					{
						var addr = instances[r][i];
						var clrType = heap.GetObjectType(addr);
						string key = ClrtType.GetKey(clrType.Name, clrType.MethodTable);

						ClrtType ourType;
						if (typeDct.TryGetValue(key, out ourType))
						{
							if (ourType.HasFieldInfo()) continue;
							fldTypeNames.Clear();
							fldMts.Clear();
							fldNameIds.Clear();
							statFldTypeNames.Clear();
							statFldMts.Clear();
							statFldNameIds.Clear();
							if (clrType.Fields != null || clrType.Fields.Count > 0)
							{
								for (int f = 0, fcnt = clrType.Fields.Count; f < fcnt; ++f)
								{
									ClrInstanceField fld = clrType.Fields[f];

									var fldNameId = stringIds[r].JustGetId(fld.Name);
									var fldTypeInfo = ResolveFieldTypeName(heap, clrType, addr, fld, typeDct);
									fldTypeNames.Add(fldTypeInfo.Key);
									fldMts.Add(fldTypeInfo.Value);
									fldNameIds.Add(fldNameId);
								}
							}
							if (clrType.StaticFields != null && clrType.StaticFields.Count > 0)
							{
								for (int f = 0, fcnt = clrType.StaticFields.Count; f < fcnt; ++f)
								{
									ClrStaticField fld = clrType.StaticFields[f];
									var fldNameId = stringIds[r].JustGetId(fld.Name);
									var fldTypeInfo = ResolveStaticFieldTypeName(heap, clrType, addr, fld, typeDct);
									string fkey = ClrtType.GetKey(fldTypeInfo.Key, fldTypeInfo.Value);
									statFldTypeNames.Add(fldTypeInfo.Key);
									statFldMts.Add(fldTypeInfo.Value);
									statFldNameIds.Add(fldNameId);
								}
							}
							ourType.AddFieldInfo(
								fldTypeNames.Count > 0 ? fldTypeNames.ToArray() : Utils.EmptyArray<string>.Value,
								fldMts.Count > 0 ? fldMts.ToArray() : Utils.EmptyArray<ulong>.Value,
								fldNameIds.Count > 0 ? fldNameIds.ToArray() : Utils.EmptyArray<int>.Value,
								statFldTypeNames.Count > 0 ? statFldTypeNames.ToArray() : Utils.EmptyArray<string>.Value,
								statFldMts.Count > 0 ? statFldMts.ToArray() : Utils.EmptyArray<ulong>.Value,
								statFldNameIds.Count > 0 ? statFldNameIds.ToArray() : Utils.EmptyArray<int>.Value
								);
						}
						else
						{
							Debug.Assert(false, "Type not in type dictionary!");
						}
					}

					fldTypeNames = null;
					fldMts = null;
					fldNameIds = null;
					statFldTypeNames = null;
					statFldMts = null;
					statFldNameIds = null;

					// getting other stuff
					//
					progress?.Report("Runtime: " + r + ", constructing instance and type arrays.");

					int typeCount = typeDct.Count;
					ClrtTypes types = new ClrtTypes(typeCount);
					string[] baseNames = new string[typeCount];
					ulong[] mthdTbls = new ulong[typeCount];
					int[][] fieldTypeIds = new int[typeCount][];
					int[][] fieldNameIds = new int[typeCount][];
					ulong[][] fieldMts = new ulong[typeCount][];
					int[][] staticFieldTypeIds = new int[typeCount][];
					int[][] staticFieldNameIds = new int[typeCount][];
					ulong[][] staticFieldMts = new ulong[typeCount][];

					int ndx = 0;
					foreach (var kv in typeDct)
					{
						baseNames[ndx] = kv.Value.BaseName;
						mthdTbls[ndx] = kv.Value.MthdTbl;
						types.AddType(ndx, kv.Value.Name, Utils.ReverseTypeName(kv.Value.Name), kv.Value.Element);
						fieldTypeIds[ndx] = new int[kv.Value.FieldNameIds.Length];
						fieldNameIds[ndx] = kv.Value.FieldNameIds;
						fieldMts[ndx] = kv.Value.FieldMts;
						staticFieldTypeIds[ndx] = new int[kv.Value.StaticFieldMts.Length];
						staticFieldNameIds[ndx] = kv.Value.StaticFieldNameIds;
						staticFieldMts[ndx] = kv.Value.StaticFieldMts;
						++ndx;
					}

					typeDct.Clear();
					typeDct = null;

					int[] baseIds = new int[typeCount];
					int[] mthdTblsMap = new int[typeCount];
					Utils.Iota(mthdTblsMap);
					Array.Sort(mthdTbls, mthdTblsMap);

					for (int j = 0; j < typeCount; ++j)
					{
						baseIds[j] = types.GetTypeId(baseNames[j]);
					}
					types.AddAdditionalInfos(baseIds, mthdTbls, mthdTblsMap);

					// add field info
					//
					for (int j = 0; j < typeCount; ++j)
					{
						for (int k = 0, kcnt = fieldTypeIds[j].Length; k < kcnt; ++k)
						{
							fieldTypeIds[j][k] = types.GetTypeId(fieldMts[j][k]);
						}
						for (int k = 0, kcnt = staticFieldTypeIds[j].Length; k < kcnt; ++k)
						{
							staticFieldTypeIds[j][k] = types.GetTypeId(staticFieldMts[j][k]);
						}
					}
					types.AddFieldInfos(fieldTypeIds, fieldNameIds, staticFieldTypeIds, staticFieldNameIds);

					clrtTypes[r] = types;

					mthdTbls = null;
					fieldTypeIds = null;
					fieldMts = null;
					staticFieldTypeIds = null;
					staticFieldNameIds = null;
					staticFieldMts = null;

					ulong[] instanceTps = mthdtbls.GetArrayAndClear();
					mthdtbls = null;


					Utils.ForceGcWithCompaction();

					int[] tps = new int[instanceTps.Length];
					for (int j = 0, jcnt = tps.Length; j < jcnt; ++j)
					{
						tps[j] = types.GetTypeId(instanceTps[j]);
					}
					instanceTps = null;
					instanceTypes[r] = tps;

					progress?.Report("Runtime: " + r + ", getting roots.");
					clrtRoots[r] = ClrtRoots.GetRoots(clrRuntime, instances[r], instanceTypes[r], stringIds[r]);
					Utils.ForceGcWithCompaction();

				}

				return new Tuple<ClrtTypes[], ClrtRoots[]>(clrtTypes, clrtRoots);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				errors[r].Add("[Indexer.GetTypeInfos]" + Environment.NewLine + error);
				return null;
			}
		}

		private static KeyValuePair<string, ulong> ResolveFieldTypeName(ClrHeap heap, ClrType clrType, ulong clrTypeAddr, ClrInstanceField field, SortedDictionary<string, ClrtType> dct)
		{
			// TODO JRD
			if (field.Type == null) return new KeyValuePair<string, ulong>(Constants.FieldTypeNull, Constants.InvalidAddress);
			string ftpName = field.Type.Name;
			ulong fmt = field.Type.MethodTable;
			string key = ClrtType.GetKey(ftpName, fmt);
			ClrtType ourType;
			if (!dct.TryGetValue(key, out ourType))
			{
				AddTypeGetBase(key, field.Type, dct);
			}
			return new KeyValuePair<string, ulong>(ftpName, fmt);
		}

		private static KeyValuePair<string, ulong> ResolveStaticFieldTypeName(ClrHeap heap, ClrType clrType, ulong clrTypeAddr, ClrStaticField field, SortedDictionary<string, ClrtType> dct)
		{
			// TODO JRD
			if (field.Type == null) return new KeyValuePair<string, ulong>(Constants.FieldTypeNull, Constants.InvalidAddress);
			string ftpName = field.Type.Name;
			ulong fmt = field.Type.MethodTable;
			string key = ClrtType.GetKey(ftpName, fmt);
			ClrtType ourType;
			if (!dct.TryGetValue(key, out ourType))
			{
				AddTypeGetBase(key, field.Type, dct);
			}
			return new KeyValuePair<string, ulong>(ftpName, fmt);
		}

		public static bool GetFieldInfos(ClrtDump clrtDump,
											IProgress<string> progress,
											ClrtTypes[] allTypes, ulong[][] allInstances,
											int[][] allInstanceTypes,
											StringIdDct[] stringIds,
											out int[] fldNotFoundCnt,
											out string error)
		{
			error = null;
			fldNotFoundCnt = new int[clrtDump.RuntimeCount];
			var fldRefList = new List<KeyValuePair<ulong, int>>(64);
			var fldRefIdNameLst = new List<KeyValuePair<ulong, int>>(32);
			progress?.Report("Getting field information starts...");
			var que = new BlockingCollection<KeyValuePair<ulong, KeyValuePair<ulong, int>[]>>();
			List<string> workerErrors = new List<string>(0);
			try
			{
				for (int r = 0, rcnt = clrtDump.Runtimes.Length; r < rcnt; ++r)
				{
					int notFoundCount = 0;

					var clrRuntime = clrtDump.Runtimes[r];
					clrRuntime.Flush();
					var heap = clrRuntime.GetHeap();
					var instances = allInstances[r];
					var fieldIds = stringIds[r];


					Thread thread = new Thread(FieldDependency.WriteFieldsDependencies);
					thread.Start(new Tuple<string, string, BlockingCollection<KeyValuePair<ulong, KeyValuePair<ulong, int>[]>>, List<string>>(
							Utils.GetFilePath(r, Utils.GetMapFolder(clrtDump.DumpPath), clrtDump.DumpFileNameNoExt,
									Constants.MapFieldOffsetsFilePostfix),
							Utils.GetFilePath(r, Utils.GetMapFolder(clrtDump.DumpPath), clrtDump.DumpFileNameNoExt,
									Constants.MapFieldInstancesPostfix),
							que,
							workerErrors
						));

					for (int i = 0, icnt = instances.Length; i < icnt; ++i)
					{
						if ((i % 100000) == 0) progress?.Report("Runtime: " + r + ", instance: " + Utils.LargeNumberString(i) + "/" + Utils.LargeNumberString(icnt));
						var addr = instances[i];

						var clrType = heap.GetObjectType(addr);
						if (clrType == null || clrType.IsString || clrType.Fields == null) continue;

						if (clrType.IsArray)
						{
							fldRefIdNameLst.Clear();
							fldRefList.Clear();
							int fldNameId = fieldIds.JustGetId("[]");
							clrType.EnumerateRefsOfObjectCarefully(addr, (address, off) =>
							{
								fldRefList.Add(new KeyValuePair<ulong, int>(address, off));
							});

							for (int k = 0, kcnt = fldRefList.Count; k < kcnt; ++k)
							{
								var fldAddr = fldRefList[k].Key;
								if (fldAddr == 0) continue;
								fldRefIdNameLst.Add(new KeyValuePair<ulong, int>(fldAddr, fldNameId));
							}
							if (fldRefIdNameLst.Count > 0)
								que.Add(new KeyValuePair<ulong, KeyValuePair<ulong, int>[]>(addr, fldRefIdNameLst.ToArray()));
							continue;
						}

						if (clrType.Fields.Count < 1) continue;

						fldRefIdNameLst.Clear();
						for (int j = 0, jcnt = clrType.Fields.Count; j < jcnt; ++j)
						{
							var fld = clrType.Fields[j];
							if (!fld.IsObjectReference) continue;
							
							object fVal = fld.GetValue(addr,false,false);
							if (fVal == null) continue;
							ulong fAddr = 0;
							try
							{
								fAddr = (ulong)fVal;
							}
							catch (Exception ex)
							{
								int a = 1;
							}
							int fldNameId = fieldIds.JustGetId(fld.Name);
							fldRefIdNameLst.Add(new KeyValuePair<ulong, int>(fAddr,fldNameId));
						}
						if (fldRefIdNameLst.Count > 0)
							que.Add(new KeyValuePair<ulong, KeyValuePair<ulong, int>[]>(addr, fldRefIdNameLst.ToArray()));
					}

					que.Add(new KeyValuePair<ulong, KeyValuePair<ulong, int>[]>(Constants.InvalidAddress, null)); // signal worker thread to stop

					Utils.ForceGcWithCompaction();
					fldNotFoundCnt[r] = notFoundCount;

					progress?.Report("Waiting for field writer...");
					thread.Join();
				}
				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}


		//public static bool GetFieldInfos(ClrtDump clrtDump,
		//							IProgress<string> progress,
		//							ClrtTypes[] allTypes, ulong[][] allInstances,
		//							int[][] allInstanceTypes,
		//							StringIdDct[] stringIds,
		//							out int[] fldNotFoundCnt,
		//							out string error)
		//{
		//	error = null;
		//	fldNotFoundCnt = new int[clrtDump.RuntimeCount];
		//	var fldRefList = new List<KeyValuePair<ulong, int>>(64);
		//	var fldRefSet = new HashSet<ulong>();
		//	//var fldRefIdLst = new List<ulong>(32);
		//	var fldRefIdNameLst = new List<KeyValuePair<ulong, int>>(32);
		//	progress?.Report("Getting field information starts...");
		//	var que = new BlockingCollection<KeyValuePair<ulong, KeyValuePair<ulong, int>[]>>();
		//	List<string> workerErrors = new List<string>(0);
		//	try
		//	{
		//		for (int r = 0, rcnt = clrtDump.Runtimes.Length; r < rcnt; ++r)
		//		{
		//			int notFoundCount = 0;

		//			var clrRuntime = clrtDump.Runtimes[r];
		//			clrRuntime.Flush();
		//			var heap = clrRuntime.GetHeap();
		//			var instances = allInstances[r];
		//			var fieldIds = stringIds[r];


		//			Thread thread = new Thread(FieldDependency.WriteFieldsDependencies);
		//			thread.Start(new Tuple<string, string, BlockingCollection<KeyValuePair<ulong, KeyValuePair<ulong, int>[]>>, List<string>>(
		//					Utils.GetFilePath(r, Utils.GetMapFolder(clrtDump.DumpPath), clrtDump.DumpFileNameNoExt,
		//							Constants.MapFieldOffsetsFilePostfix),
		//					Utils.GetFilePath(r, Utils.GetMapFolder(clrtDump.DumpPath), clrtDump.DumpFileNameNoExt,
		//							Constants.MapFieldInstancesPostfix),
		//					que,
		//					workerErrors
		//				));

		//			for (int i = 0, icnt = instances.Length; i < icnt; ++i)
		//			{
		//				if ((i % 100000) == 0) progress?.Report("Runtime: " + r + ", instance: " + Utils.LargeNumberString(i) + "/" + Utils.LargeNumberString(icnt));
		//				var addr = instances[i];

		//				var clrType = heap.GetObjectType(addr);
		//				if (clrType == null) continue;

		//				fldRefList.Clear();
		//				fldRefSet.Clear();
		//				clrType.EnumerateRefsOfObjectCarefully(addr, (address, off) =>
		//				{
		//					fldRefList.Add(new KeyValuePair<ulong, int>(address, off));
		//				});

		//				// match fields with offsets
		//				//
		//				bool isArray = clrType.IsArray;
		//				bool isStruct = clrType.IsValueClass;
		//				string curFldName;
		//				if (fldRefList.Count > 0)
		//				{
		//					fldRefIdNameLst.Clear();
		//					for (int k = 0, kcnt = fldRefList.Count; k < kcnt; ++k)
		//					{
		//						var fldAddr = fldRefList[k].Key;
		//						if (fldAddr != 0UL) // TODO JRD -- we need that?
		//						{
		//							var fldNdx = Array.BinarySearch(instances, fldAddr);
		//							if (fldNdx < 0)
		//							{
		//								++notFoundCount;
		//							}
		//							int fldNameNdx;
		//							int childFieldOffset;
		//							int curOffset;
		//							if (!isArray)
		//							{
		//								curOffset = fldRefList[k].Value;
		//								ClrInstanceField iFld;
		//								if (clrType.GetFieldForOffset(curOffset, isStruct, out iFld, out childFieldOffset))
		//								{
		//									curFldName = iFld.Name;
		//									if (addr == 0x00000002c433a0)
		//									{
		//										if (iFld.Type.IsString)
		//										{
		//											var str = (string)iFld.GetValue(addr, false, true);
		//										}
		//									}
		//									fldNameNdx = fieldIds.JustGetId(curFldName);
		//									if (iFld.IsObjectReference)
		//										fldRefIdNameLst.Add(new KeyValuePair<ulong, int>(fldAddr, fldNameNdx));
		//									while (childFieldOffset != 0 && curOffset != childFieldOffset)
		//									{
		//										curOffset = childFieldOffset;
		//										if (clrType.GetFieldForOffset(curOffset, isStruct, out iFld, out childFieldOffset))
		//										{
		//											if (iFld.IsObjectReference)
		//											{
		//												curFldName = curFldName + "." + iFld.Name;
		//												fldNameNdx = fieldIds.JustGetId(curFldName);
		//												fldRefIdNameLst.Add(new KeyValuePair<ulong, int>(fldAddr, fldNameNdx));
		//											}
		//										}
		//										else break;
		//									}
		//								}
		//								//else
		//								//{
		//								//	fldNameNdx = fieldIds.JustGetId(Constants.Unknown);
		//								//	fldRefIdNameLst.Add(new KeyValuePair<ulong, int>(fldAddr, fldNameNdx));
		//								//}
		//							}
		//							else
		//							{
		//								fldNameNdx = fieldIds.JustGetId("[]");
		//								fldRefIdNameLst.Add(new KeyValuePair<ulong, int>(fldAddr, fldNameNdx));
		//							}
		//						}
		//					}
		//					que.Add(new KeyValuePair<ulong, KeyValuePair<ulong, int>[]>(
		//						addr,
		//						fldRefIdNameLst.Count == 0 ? Utils.EmptyArray<KeyValuePair<ulong, int>>.Value : fldRefIdNameLst.ToArray()));

		//				}


		//			}

		//			que.Add(new KeyValuePair<ulong, KeyValuePair<ulong, int>[]>(Constants.InvalidAddress, null)); // signal worker thread to stop

		//			// augment types info with fields
		//			//



		//			//progress?.Report("Runtime: " + r + ", preparing objects and fields reference map: " + arenaFlds.Count());


		//			//var flds = arenaFlds.GetArrayAndClear();
		//			//arenaFlds = null;
		//			//Utils.ForceGcWithCompaction();
		//			//var prnts = arenaPrnts.GetArrayAndClear();
		//			//arenaPrnts = null;
		//			//Utils.ForceGcWithCompaction();
		//			//Array.Sort(flds, prnts);
		//			//List<pair<int, int>> prnCnts = new List<pair<int, int>>(flds.Length / 2);
		//			//int curFld = flds[0];
		//			//prnCnts.Add(new pair<int, int>(curFld, 0));
		//			//for (int i = 1, icnt = flds.Length; i < icnt; ++i)
		//			//{
		//			//	if (curFld != flds[i])
		//			//	{
		//			//		curFld = flds[i];
		//			//		prnCnts.Add(new pair<int, int>(curFld, i));
		//			//	}
		//			//}
		//			////prnCnts.Add(new pair<int, int>(Constants.InvalidIndex, flds.Length));
		//			//fields[r] = prnCnts.ToArray();
		//			//prnCnts = null;
		//			Utils.ForceGcWithCompaction();
		//			//parents[r] = prnts;
		//			fldNotFoundCnt[r] = notFoundCount;

		//			progress?.Report("Waiting for field writer...");
		//			thread.Join();

		//		}

		//		//long refCount = 0;
		//		//for (int r = 0, rcnt = fields.Length; r < rcnt; ++r) refCount += fields[r].Length;
		//		return true;
		//	}
		//	catch (Exception ex)
		//	{
		//		error = Utils.GetExceptionErrorString(ex);
		//		return false;
		//	}
		//}



		//public static Tuple<uint[][], int[][]> GetFieldReference(ClrtDump clrtDump, ClrtTypes[] allTypes, ulong[][] allInstances, int[][] allInstanceTypes, out int[] fldNotFoundCnt, out string error)
		//{
		//	error = null;
		//	error = null;
		//	fldNotFoundCnt = new int[clrtDump.RuntimeCount];
		//	var fldRefList = new List<KeyValuePair<ulong, int>>(64);
		//	uint[][] fields = new uint[clrtDump.RuntimeCount][];
		//	int[][] fieldOffs = new int[clrtDump.RuntimeCount][];
		//	try
		//	{
		//		for (int r = 0, rcnt = clrtDump.Runtimes.Length; r < rcnt; ++r)
		//		{
		//			TempArena<uint> arena = new TempArena<uint>(10000000, 10);
		//			TempArena<int> fldOffs = new TempArena<int>(10000000, 10);
		//			int fldOff = 0;
		//			int notFoundCount = 0;

		//			var clrRuntime = clrtDump.Runtimes[r];
		//			clrRuntime.Flush();
		//			var heap = clrRuntime.GetHeap();
		//			var instances = allInstances[r];
		//			var types = allInstanceTypes[r];

		//			var segs = heap.Segments;
		//			for (int i = 0, icnt = instances.Length; i < icnt; ++i)
		//			{
		//				var addr = instances[i];

		//				var clrType = heap.GetObjectType(addr);
		//				if (clrType == null) continue;
		//				fldOffs.Add(fldOff);

		//				fldRefList.Clear();
		//				clrType.EnumerateRefsOfObjectCarefully(addr, (address, off) => fldRefList.Add(new KeyValuePair<ulong, int>(address, off)));
		//				for (int k = 0, kcnt = fldRefList.Count; k < kcnt; ++k)
		//				{
		//					++fldOff;
		//					var fldAddr = fldRefList[k].Key;
		//					if (fldAddr != 0UL)
		//					{
		//						var fldNdx = Array.BinarySearch(instances, fldAddr);
		//						if (fldNdx >= 0)
		//						{
		//							arena.Add((uint)((fldNdx << 16) | i));
		//						}
		//						else
		//						{
		//							arena.Add(Constants.InvalidHalfIndexMSB | (uint)i);
		//							++notFoundCount;
		//						}
		//					}
		//				}
		//				//for (int j = 0, jcnt = clrType.Fields.Count; j < jcnt; ++j)
		//				//{
		//				//    var fldType = clrType.Fields[j].Type;
		//				//    if (fldType == null) continue;
		//				//    var fldId0 = types[r].GetTypeId(clrType.Name);
		//				//    var fldId1 = types[r].GetTypeId(fldType.MethodTable);
		//				//    Debug.Assert(fldId0==fldId1);
		//				//}

		//			}

		//			fields[r] = arena.GetArray();
		//			fieldOffs[r] = fldOffs.GetArray();
		//			fldNotFoundCnt[r] = notFoundCount;


		//		}
		//		return new Tuple<uint[][], int[][]>(fields, fieldOffs);
		//	}
		//	catch (Exception ex)
		//	{
		//		error = Utils.GetExceptionErrorString(ex);
		//		return null;
		//	}
		//}

		// TODO JRD -- remove
		//public bool BuildInstanceReferenceGraph(ClrtDump clrtDump, IProgress<string> progress, out string error)
		//{
		//	error = null;
		//	BinaryWriter fieldInfoWriter = null;
		//	BinaryWriter fieldIndexWriter = null;
		//	_parents = new int[clrtDump.Runtimes.Length][];
		//	_multiparents = new MultiparentIndexer[clrtDump.Runtimes.Length];
		//	var fldRefList = new List<KeyValuePair<ulong, int>>(64);
		//	var typesWithNewTypeFields = new List<KeyValuePair<int, ClrType>>(32);

		//	try
		//	{
		//		for (int r = 0, rcnt = clrtDump.Runtimes.Length; r < rcnt; ++r)
		//		{
		//			var clrRuntime = clrtDump.Runtimes[r];
		//			var heap = clrRuntime.GetHeap();
		//			var alreadyDone = new HashSet<int>();
		//			var rtInstances = _instances[r];
		//			_parents[r] = new int[rtInstances.Length];
		//			var rtParents = _parents[r];
		//			Utils.InitArray(rtParents, IndexValue.InvalidIndex);
		//			var rtTypes = _typeIds[r];
		//			var typeIdDct = _typeNameIdDcts[r];
		//			bool newId;
		//			KeyValuePair<int, int>[] fldIndices = new KeyValuePair<int, int>[typeIdDct.Count];
		//			List<KeyValuePair<int, int>> fldInfos = new List<KeyValuePair<int, int>>(typeIdDct.Count * 10);
		//			_multiparents[r] = new MultiparentIndexer(rtInstances.Length / 5);
		//			var rtMultiparents = _multiparents[r];

		//			for (int i = 0, icnt = rtInstances.Length; i < icnt; ++i)
		//			{
		//				if ((i % 100000) == 0) progress.Report("Runtime[" + r + "] Building instance references " + Utils.LargeNumberString(i + 1) + " / " + Utils.LargeNumberString(icnt));
		//				var instAddr = rtInstances[i];
		//				if (alreadyDone.Contains(rtTypes[i])) continue;
		//				alreadyDone.Add(rtTypes[i]);
		//				var clrType = heap.GetObjectType(instAddr);
		//				Debug.Assert(clrType != null);

		//				fldIndices[rtTypes[i]] = new KeyValuePair<int, int>(fldInfos.Count, clrType.Fields.Count);

		//				for (int f = 0, fcnt = clrType.Fields.Count; f < fcnt; ++f)
		//				{
		//					var fld = clrType.Fields[f];
		//					var fldTypeName = fld.Type != null ? (fld.Type.Name ?? Constants.FieldTypeNull) : Constants.FieldTypeNull;
		//					var fldTypeId = typeIdDct.GetId(fldTypeName, out newId);

		//					if (newId && fld.Type != null)
		//					{
		//						typesWithNewTypeFields.Add(new KeyValuePair<int, ClrType>(fldTypeId, fld.Type));
		//						SetTypeInfo(typeIdDct, fld.Type, fldTypeId);
		//					}
		//					var fldNameId = _stringIdDcts[r].GetId(fld.Name ?? Constants.FieldNameNull, out newId);
		//					fldInfos.Add(new KeyValuePair<int, int>(fldTypeId, fldNameId));
		//				}

		//				fldRefList.Clear();
		//				clrType.EnumerateRefsOfObjectCarefully(instAddr, (addr, off) => fldRefList.Add(new KeyValuePair<ulong, int>(addr, off)));
		//				for (int k = 0, kcnt = fldRefList.Count; k < kcnt; ++k)
		//				{
		//					var fldAddr = fldRefList[k].Key;
		//					if (fldAddr != 0UL)
		//					{
		//						var fldNdx = Array.BinarySearch(rtInstances, fldAddr);
		//						if (fldNdx >= 0)
		//						{
		//							var pndx = rtParents[fldNdx];
		//							if (pndx == IndexValue.InvalidIndex)
		//							{
		//								rtParents[fldNdx] = i;
		//							}
		//							else if (IndexValue.IsMultiparentIndex(pndx))
		//							{
		//								rtMultiparents.AddParent(pndx, i);
		//							}
		//							else
		//							{
		//								rtParents[fldNdx] = rtMultiparents.NewList(pndx, i);
		//							}
		//						}
		//					}
		//				}
		//			}

		//			List<KeyValuePair<int, int>> extraFldIndices = new List<KeyValuePair<int, int>>(typesWithNewTypeFields.Count);

		//			int j = 0;
		//			while (j < typesWithNewTypeFields.Count)
		//			{
		//				var fldType = typesWithNewTypeFields[j].Value;
		//				var parentId = typesWithNewTypeFields[j].Key;
		//				extraFldIndices.Add(new KeyValuePair<int, int>(fldInfos.Count, fldType.Fields.Count));
		//				for (int f = 0, fcnt = fldType.Fields.Count; f < fcnt; ++f)
		//				{
		//					var fld = fldType.Fields[f];
		//					var fldTypeName = fld.Type != null ? (fld.Type.Name ?? Constants.FieldTypeNull) : Constants.FieldTypeNull;
		//					var fldTypeId = typeIdDct.GetId(fldTypeName, out newId);
		//					if (newId)
		//					{
		//						if (fld.Type != null && (typesWithNewTypeFields.Count < 1 ||
		//													fldType.Name != typesWithNewTypeFields[typesWithNewTypeFields.Count - 1].Value.Name))
		//						{
		//							typesWithNewTypeFields.Add(new KeyValuePair<int, ClrType>(parentId, fld.Type));
		//						}
		//					}
		//					var fldNameId = _stringIdDcts[r].GetId(fld.Name ?? Constants.FieldNameNull, out newId);
		//					fldInfos.Add(new KeyValuePair<int, int>(fldTypeId, fldNameId));
		//				}
		//				++j;
		//			}

		//			// dump field info

		//			fieldInfoWriter =
		//				new BinaryWriter(File.Open(
		//					Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapFieldTypeMapFilePostfix), FileMode.Create));
		//			fieldIndexWriter =
		//				new BinaryWriter(
		//					File.Open(Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapTypeFieldIndexFilePostfix),
		//						FileMode.Create));

		//			fieldInfoWriter.Write(fldInfos.Count);
		//			for (int i = 0, icnt = fldInfos.Count; i < icnt; ++i)
		//			{
		//				fieldInfoWriter.Write(fldInfos[i].Key);
		//				fieldInfoWriter.Write(fldInfos[i].Value);
		//			}
		//			Utils.CloseStream(ref fieldInfoWriter);

		//			fieldIndexWriter.Write(fldIndices.Length + extraFldIndices.Count);
		//			for (int i = 0, icnt = fldIndices.Length; i < icnt; ++i)
		//			{
		//				var index = fldIndices[i].Value > 0 ? fldIndices[i].Key : Constants.InvalidIndex;
		//				fieldIndexWriter.Write(index);
		//				fieldIndexWriter.Write(fldIndices[i].Value);
		//			}
		//			for (int i = 0, icnt = extraFldIndices.Count; i < icnt; ++i)
		//			{
		//				var index = extraFldIndices[i].Value > 0 ? extraFldIndices[i].Key : Constants.InvalidIndex;
		//				fieldIndexWriter.Write(index);
		//				fieldIndexWriter.Write(extraFldIndices[i].Value);
		//			}
		//			Utils.CloseStream(ref fieldIndexWriter);
		//		}

		//		return true;
		//	}
		//	catch (Exception ex)
		//	{
		//		error = Utils.GetExceptionErrorString(ex);
		//		return false;
		//	}
		//	finally
		//	{
		//		fieldInfoWriter?.Close();
		//		fieldIndexWriter?.Close();
		//	}
		//}

		// TODO JRD -- remove
		//public bool IndexRoots(ClrtDump clrtDump, IProgress<string> progress, out string error)
		//{
		//	error = null;
		//	try
		//	{
		//		for (int r = 0, rcnt = clrtDump.Runtimes.Length; r < rcnt; ++r)
		//		{
		//			List<KeyValuePair<int, string>> newTypes = new List<KeyValuePair<int, string>>();
		//			var clrRuntime = clrtDump.Runtimes[r];
		//			var threadIds = Indexer.GetThreadIds(clrRuntime);

		//			// TODO
		//			//_roots[r] = GetRoots(clrRuntime, _typeNameIdDcts[r], _stringIdDcts[r],_clrtAppDomains[r], threadIds,
		//			//				newTypes, out error);
		//		}
		//		return true;
		//	}
		//	catch (Exception ex)
		//	{
		//		error = Utils.GetExceptionErrorString(ex);
		//		return false;
		//	}
		//}



		public static bool ImplementsInterface(ClrType tp, string interfaceName)
		{
			if (tp == null || tp.Interfaces == null) return false;
			var lst = tp.Interfaces;
			for (int i = 0, icnt = lst.Count; i < icnt; ++i)
			{
				if (Utils.SameStrings(lst[i].Name, interfaceName)) return true;
			}
			return false;
		}


		public bool GetArraysInfo(out string error)
		{
			error = null;
			try
			{

				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}

		/// <summary>
		/// Used for testing only.
		/// </summary>
		/// <returns></returns>
		public static StringIdDct ConstructTypeIdDictionary(ClrRuntime runtime, out string error)
		{
			error = null;
			var heap = runtime.GetHeap();
			var dct = new StringIdDct();
			dct.AddKey(Constants.Free);
			dct.AddKey(Constants.ErrorStr);
			dct.AddKey(Constants.NullName);

			try
			{
				for (int i = 0, icnt = heap.Segments.Count; i < icnt; ++i)
				{
					var seg = heap.Segments[i];
					var addr = seg.FirstObject;
					while (addr != 0UL)
					{
						var clrType = heap.GetObjectType(addr);
						if (clrType == null || clrType.Name == null || Utils.SameStrings(clrType.Name, Constants.Free)) goto NEXT_OBJECT;
						dct.JustGetId(clrType.Name);
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

		public static KeyValuePair<uint, int>[] GetThreadIds(ClrRuntime runtime)
		{
			var lst = new List<KeyValuePair<uint, int>>(128);
			var thrdLst = runtime.Threads;
			for (int i = 0, icnt = thrdLst.Count; i < icnt; ++i)
			{
				lst.Add(new KeyValuePair<uint, int>(thrdLst[i].OSThreadId, thrdLst[i].ManagedThreadId));
			}
			lst.Sort((a, b) =>
			{
				if (a.Key == b.Key)
				{
					return a.Value < b.Value ? -1 : (a.Value > b.Value ? 1 : 0);
				}
				return a.Key < b.Key ? -1 : 1;
			});
			return lst.ToArray();
		}

		public static ClrThread[] GetThreads(ClrRuntime runtime)
		{
			var lst = new List<ClrThread>(128);
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

		public static BlockingObject[] GetBlockingObjects(ClrRuntime runtime)
		{
			var heap = runtime.GetHeap();
			var blks = heap.EnumerateBlockingObjects().ToArray();
			Array.Sort(blks, new BlockingObjectCmp());
			return blks;
		}

		#endregion Indexing

		#region Write Data

		// TODO JRD -- remove
		//private bool DumpTypeAndFieldNames(out string error)
		//{
		//	error = null;
		//	StreamWriter wr1 = null;
		//	StreamWriter wr2 = null;
		//	int r = 0;
		//	try
		//	{
		//		for (r = 0; r < _typeNameIdDcts.Length; ++r)
		//		{
		//			wr1 = new StreamWriter(Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.TxtTypeFilePostfix));
		//			wr2 = new StreamWriter(Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.TxtReversedTypeNameFilePostfix));

		//			var typeNames = _typeNameIdDcts[r].GetNamesSortedById();
		//			var icount = typeNames.Length;
		//			wr1.WriteLine(icount.ToString());
		//			wr2.WriteLine(icount.ToString());
		//			for (var i = 0; i < icount; ++i)
		//			{
		//				try
		//				{
		//					var reversedName = Utils.ReverseTypeName(typeNames[i]);
		//					wr2.WriteLine(reversedName);
		//				}
		//				catch (Exception ex)
		//				{
		//					error = ex.ToString();
		//					return false;
		//				}
		//				wr1.WriteLine(typeNames[i]);
		//			}
		//			Utils.CloseStream(ref wr1);
		//			Utils.CloseStream(ref wr2);

		//			_typeNameIdDcts[r].DumpBasesAndElementTypes(Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapTypeBaseAndElementFilePostfix), out error);
		//			_typeNameIdDcts[r].DumpTypeFieldCounts(Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapTypeFieldCountsFilePostfix), out error);
		//			_typeNameIdDcts[r].DumpTypeInstanceCounts(Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapTypeIntanceCountsFilePostfix), out error);

		//		}
		//		return true;
		//	}
		//	catch (Exception ex)
		//	{
		//		error = Utils.GetExceptionErrorString(ex);
		//		AddError(r, "[Indexer.DumpTypeNames]" + Environment.NewLine + error);
		//		return false;
		//	}
		//	finally
		//	{
		//		wr1?.Close();
		//		wr2?.Close();
		//	}
		//}

		private void DumpIndexInfo(Version version, ClrtDump dump)
		{
			var path = MapOutputFolder + Path.DirectorySeparatorChar + DumpFileName + Constants.TxtIndexInfoFilePostfix;
			StreamWriter txtWriter = null;
			int i = 0;
			try
			{
				ResetReadOnlyAttribute(path);
				txtWriter = new StreamWriter(path);
				txtWriter.WriteLine("MDR Version: [" + Utils.VersionValue(version).ToString() + "], this is this application version.");
				txtWriter.WriteLine("Dump Path: " + DumpFilePath);
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
							txtWriter.WriteLine("Application domain: " + (appDoamin.Name ?? "unnamed") + ", id: " + appDoamin.Id + ", module cnt: " + appDoamin.Modules.Count);
						}
					}
					if (runtime.SystemDomain != null)
						txtWriter.WriteLine("System domain: " + (runtime.SystemDomain.Name ?? "unnamed") + ", id: " + runtime.SystemDomain.Id + ", module cnt: " + runtime.SystemDomain.Modules.Count);
					if (runtime.SharedDomain != null)
						txtWriter.WriteLine("Shared domain: " + (runtime.SharedDomain.Name ?? "unnamed") + ", id: " + runtime.SharedDomain.Id + ", module cnt: " + runtime.SharedDomain.Modules.Count);


					txtWriter.WriteLine("Instance Count: " + Utils.LargeNumberString(_instances[i].Length));
					txtWriter.WriteLine("Type Count: " + Utils.LargeNumberString(_types[i].Count));
					txtWriter.WriteLine("Finalizer Queue Count: " + Utils.LargeNumberString(_clrtRoots[i].FinalizerQueueCount));
					txtWriter.WriteLine("Roots Count: " + Utils.LargeNumberString(_clrtRoots[i].RootsCount));

					txtWriter.WriteLine();
				}
			}
			catch (Exception ex)
			{
				AddError(i, "[Indexer.DumpIndexInfo]" + Environment.NewLine + Utils.GetExceptionErrorString(ex));
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

		// TODO JRD -- remove
		//private bool DumpInstanceInfo(int rtCount, out string error)
		//{
		//	error = null;
		//	BinaryWriter bw = null;
		//	try
		//	{
		//		for (int r = 0; r < rtCount; ++r)
		//		{
		//			bw = new BinaryWriter(File.Open(Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapInstanceFilePostfix), FileMode.Create));
		//			var instances = _instances[r];
		//			var typeids = _typeIds[r];
		//			var parents = _parents[r];
		//			var sizes = _sizes[r];
		//			Debug.Assert(instances.Length == typeids.Length && instances.Length == parents.Length && sizes.Length == parents.Length);
		//			bw.Write(instances.Length);
		//			for (int i = 0, icnt = instances.Length; i < icnt; ++i)
		//			{
		//				bw.Write(instances[i]);
		//				bw.Write(sizes[i]);
		//				bw.Write(typeids[i]);
		//				bw.Write(parents[i]);
		//			}
		//			Utils.CloseStream(ref bw);
		//			bw = new BinaryWriter(File.Open(Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapMultiParentsFilePostfix), FileMode.Create));
		//			if (!_multiparents[r].Dump(bw, out error)) return false;
		//			Utils.CloseStream(ref bw);
		//			bw = new BinaryWriter(File.Open(Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapArrayInstanceFilePostfix), FileMode.Create));
		//			_arrayIndexers[r].Dump(bw);
		//			Utils.CloseStream(ref bw);
		//		}
		//		return true;
		//	}
		//	catch (Exception ex)
		//	{
		//		error = Utils.GetExceptionErrorString(ex);
		//		return false;
		//	}
		//	finally
		//	{
		//		bw?.Close();
		//	}

		//}

		private bool WriteStringDct(out string error)
		{
			error = null;
			try
			{
				for (int r = 0; r < _stringIdDcts.Length; ++r)
				{
					var path = Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.TxtStringIdsPostfix);
					var fldNames = _stringIdDcts[r].GetNamesSortedById();
					if (!Utils.WriteStringList(path, fldNames, out error)) return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}

		private bool WriteFinalizerObjectAddresses(ClrtDump dump, out string error)
		{
			error = null;
			try
			{
				for (int r = 0; r < dump.RuntimeCount; ++r)
				{
					var ary = dump.Runtimes[r].EnumerateFinalizerQueueObjectAddresses().ToArray();

					var path = Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapFinalizerFilePostfix);
					if (!Utils.WriteUlongArray(path, ary, out error)) return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}

		//private bool WriteRoots(ClrtDump dump, out string error)
		//{
		//	error = null;
		//	BinaryWriter bw = null;
		//	try
		//	{
		//		for (int r = 0; r < dump.RuntimeCount; ++r)
		//		{
		//			var path = Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapRootsFilePostfix);
		//			bw = new BinaryWriter(File.Open(path, FileMode.Create));
		//			//_clrtRoots[r].Dump(bw);
		//			Utils.CloseStream(ref bw);
		//		}
		//		return true;
		//	}
		//	catch (Exception ex)
		//	{
		//		error = Utils.GetExceptionErrorString(ex);
		//		return false;
		//	}
		//	finally
		//	{
		//		bw?.Close();
		//	}
		//}


		private void DumpErrors()
		{
			StreamWriter errWriter = null;
			try
			{
				for (int r = 0; r < _errors.Length; ++r)
				{
					var path = Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.TxtIndexErrorsFilePostfix);
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

		//private bool WriteFieldReferences(ClrtDump dump, pair<int, int>[][] fldRefs, int[][] parents, out string error)
		//{
		//	error = null;
		//	for (int r = 0, rcnt = dump.RuntimeCount; r < rcnt; ++r)
		//	{
		//		var path = Utils.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapFieldsAndParentsFilePostfix);
		//		BinaryWriter br = null;
		//		try
		//		{
		//			var flds = fldRefs[r];
		//			var prnts = parents[r];
		//			br = new BinaryWriter(File.Open(path, FileMode.Create));
		//			br.Write(flds.Length);
		//			br.Write(prnts.Length);
		//			for (int i = 0, icnt = flds.Length; i < icnt; ++i)
		//			{
		//				var fld = flds[i];
		//				br.Write(fld.First);
		//				br.Write(fld.Second);
		//			}
		//			for (int i = 0, icnt = prnts.Length; i < icnt; ++i)
		//			{
		//				br.Write(prnts[i]);
		//			}
		//			br.Close();
		//			br = null;
		//		}
		//		catch (Exception ex)
		//		{
		//			error = Utils.GetExceptionErrorString(ex);
		//			return false;
		//		}
		//		finally
		//		{
		//			br?.Close();
		//		}

		//	}
		//	return true;
		//}


		#endregion Write Data

		#region Indexing Helpers

		public static void AddStandardStringIds(TypeIdDct dct)
		{
			dct.AddKey(Constants.NullName);
			dct.AddKey(Constants.ErrorStr);
			dct.AddKey(Constants.Free);
			dct.AddKey(Constants.SystemObject);
			dct.AddKey(Constants.System__Canon);
		}

		public static void AddStandardStringIds(StringIdDct dct)
		{
			dct.AddKey(Constants.NullName);
			dct.AddKey(Constants.ErrorStr);
			dct.AddKey(Constants.Free);
			dct.AddKey(Constants.SystemObject);
			dct.AddKey(Constants.System__Canon);
		}

		private static ulong[][] GetDataFromArena(Arena<triple<ulong, int, ulong>>[] data, out int[][] ints, out ulong[][] sizes)
		{
			ulong[][] ulongs = new ulong[data.Length][];
			ints = new int[data.Length][];
			sizes = new ulong[data.Length][];

			for (int r = 0, rcnt = data.Length; r < rcnt; ++r)
			{
				ulongs[r] = new ulong[data[r].Count];
				sizes[r] = new ulong[data[r].Count];
				ints[r] = new int[data[r].Count];
				for (int i = 0, icnt = data[r].Count; i < icnt; ++i)
				{
					var triple = data[r].Get(i);
					ulongs[r][i] = triple.First;
					ints[r][i] = triple.Second;
					sizes[r][i] = triple.Third;
				}
			}
			return ulongs;
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

		#endregion Indexing Helpers
	}

	public class ClrThreadCmp : IComparer<ClrThread>
	{
		public int Compare(ClrThread a, ClrThread b)
		{
			if (a.OSThreadId == b.OSThreadId)
			{
				return a.ManagedThreadId < b.ManagedThreadId ? -1 : (a.ManagedThreadId > b.ManagedThreadId ? 1 : 0);
			}
			return a.OSThreadId < b.OSThreadId ? -1 : 1;
		}
	}

	public class BlockingObjectCmp : IComparer<BlockingObject>
	{
		public int Compare(BlockingObject a, BlockingObject b)
		{
			return a.Object < b.Object ? -1 : (a.Object > b.Object ? 1 : 0);
		}
	}
}