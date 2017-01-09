using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Configuration;
using System.Runtime;
using System.Text;
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
		private StringIdAsyncDct[] _stringIdDcts;

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
			MapOutputFolder = DumpFileMoniker.GetMapFolder(dmpPath);
		}

		#endregion Ctors/Initialization

		#region Indexing

		public bool Index(Version version, IProgress<string> progress, out string indexPath, out string error)
		{
			indexPath = null;
			var clrDump = new ClrtDump(DumpFilePath);
			if (!clrDump.Init(out error)) return false;
			using (clrDump)
			{
				try
				{
					if (DumpFileMoniker.GetAndCreateMapFolders(DumpFilePath, out error) == null) return false;
					indexPath = MapOutputFolder;
					// collection data
					//
					_errors = new ConcurrentBag<string>[clrDump.RuntimeCount];
					_typeNameIdDcts = new TypeIdDct[clrDump.RuntimeCount];

					// indexing
					//
					if (!GetPrerequisites(clrDump, progress, out _stringIdDcts, out error)) return false;

					// get module information
					//
					GetTargetModuleInfos(clrDump, progress, out error);

					var typesAndRoots = GetTypeInfos(clrDump, progress, _stringIdDcts, _errors, out _instances,
						out _sizes,
						out _typeIds, out error);
					if (typesAndRoots == null) return false;
					_types = typesAndRoots.Item1;
					_clrtRoots = typesAndRoots.Item2;
					if (_types == null) return false;

					var tnames = _types[0].Names;
					Tuple<string, string> badCouple;
					var sorted = Utils.IsSorted(tnames, out badCouple);

					int[] fdNotFoundCnt;
					var gotFields = GetFieldInfos(clrDump, progress, _types, _instances, _typeIds, _stringIdDcts,
						out fdNotFoundCnt, out error);
					if (!gotFields) return false;

					for (int r = 0, rcnt = clrDump.RuntimeCount; r < rcnt; ++r)
					{
						_types[r].GenerateNamespaceOrdering(_stringIdDcts[r]);
					}

					for (int r = 0, rcnt = clrDump.RuntimeCount; r < rcnt; ++r)
					{
						_types[r].Dump(
							DumpFileMoniker.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapTypeInfosFilePostfix),
							out error);
					}

					for (int r = 0, rcnt = clrDump.RuntimeCount; r < rcnt; ++r)
					{
						if (!_clrtRoots[r].Dump(r, MapOutputFolder, DumpFileName, out error)) return false;
					}

					for (int r = 0, rcnt = clrDump.RuntimeCount; r < rcnt; ++r)
					{
						Utils.WriteUlongUintIntArrays(
							DumpFileMoniker.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapInstanceFilePostfix),
							_instances[r], _sizes[r],
							_typeIds[r], out error);
						// get types map
						//
						int[] offsets;
						var typeMap = Utils.GetIntArrayMapping(_typeIds[r], out offsets);
						Utils.WriteIntArrays(
							DumpFileMoniker.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapTypeInstancesFilePostfix),
							typeMap,
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

					var fileMoniker = new DumpFileMoniker(DumpFilePath);

					for (int r = 0, rcnt = clrDump.RuntimeCount; r < rcnt; ++r)
					{
						List<string> errors = new List<string>(0);
						FieldDependency.SortFieldDependencies(
							new Tuple<string, string, string, List<string>, IProgress<string>>(
								DumpFileMoniker.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapFieldInstancesPostfix),
								DumpFileMoniker.GetFilePath(r, MapOutputFolder, DumpFileName,
									Constants.MapFieldParentOffsetsFilePostfix),
								DumpFileMoniker.GetFilePath(r, MapOutputFolder, DumpFileName,
									Constants.MapFieldParentInstancesPostfix),
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
		public bool GetPrerequisites(ClrtDump clrtDump, IProgress<string> progress, out StringIdAsyncDct[] strIds,
			out string error)
		{
			error = null;
			strIds = null;
			progress?.Report(Utils.TimeString(DateTime.Now) + " Getting prerequisites...");
			try
			{
				strIds = new StringIdAsyncDct[clrtDump.RuntimeCount];
				_clrtAppDomains = new ClrtAppDomains[clrtDump.RuntimeCount];
				for (int r = 0, rcnt = clrtDump.Runtimes.Length; r < rcnt; ++r)
				{
					_errors[r] = new ConcurrentBag<string>();
					_stringIdDcts[r] = new StringIdAsyncDct();
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

		public bool GetTargetModuleInfos(ClrtDump clrtDump, IProgress<string> progress, out string error)
		{
			error = null;
			progress?.Report(Utils.TimeString(DateTime.Now) + " Getting target module infos...");
			try
			{
				StringBuilder sb = new StringBuilder(1024);
				var target = clrtDump.DataTarget;
				var modules = target.EnumerateModules().ToArray();
				var dct = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				var lst = new string[5];
				for (int i = 0, icnt = modules.Length; i < icnt; ++i)
				{
					var module = modules[i];
					var moduleName = System.IO.Path.GetFileName(module.FileName);
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

					//try
					//{
					//	lst[2] = module.IsRuntime ? "T" : "F";
					//	lst[3] = module.IsManaged ? "T" : "F";
					//}
					//catch (AccessViolationException)
					{
						lst[2] = "?";
						lst[3] = "?";
					}
					lst[4] = module.Version.ToString();
					var entry = string.Join(Constants.HeavyGreekCrossPadded, lst);
					dct.Add(key, entry);
				}
				string[] extraInfos = new[]
				{
					DumpFilePath,
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

		/// <summary>
		/// Getting domains info.
		/// </summary>
		/// <param name="runtime">A dump runtime.</param>
		/// <param name="idDct">String cache.</param>
		/// <returns>Doamins information.</returns>
		/// <remarks>It is public and static for unit tests.</remarks>
		public static ClrtAppDomains GetAppDomains(ClrRuntime runtime, StringIdAsyncDct idDct)
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

		private static int AddTypeGetBase(string key, ClrType clrType, SortedDictionary<string, ClrtType> typeDct)
		{
			var name = clrType.Name;
			var mthdTbl = clrType.MethodTable;
			var elem = clrType.ElementType;
			var baseName = clrType.BaseType?.Name ?? Constants.NullTypeName;
			var id = typeDct.Count;
			typeDct.Add(key, new ClrtType(name, mthdTbl, elem, baseName, id));
			return id;
		}

		public static Tuple<ClrtTypes[], ClrtRoots[]> GetTypeInfos(ClrtDump clrtDump,
			IProgress<string> progress,
			StringIdAsyncDct[] stringIds,
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
					TempArena<int> mthdtbls = new TempArena<int>(10000000, 10);
					var clrRuntime = clrtDump.Runtimes[r];
					clrRuntime.Flush();
					ClrHeap heap = clrRuntime.GetHeap();
					var typeDct = new SortedDictionary<string, ClrtType>(StringComparer.Ordinal)
					{
						{
							ClrtType.GetKey(Constants.NullTypeName, Constants.InvalidAddress),
							new ClrtType(Constants.NullTypeName, Constants.InvalidAddress, ClrElementType.Unknown,
								Constants.NullTypeName, 0)
						}
					};
					duplicates[r] = new List<string>();

					int segIndex = 0;
					var segs = heap.Segments;
					ClrtSegment[] mysegs = new ClrtSegment[segs.Count];

					for (int i = 0, icnt = segs.Count; i < icnt; ++i)
					{
						progress?.Report("[Indexer.GetTypeInfos] Runtime: " + r + ", processing segment: " + i + "/" +
										 icnt);

						var seg = segs[i];

						var genCounts = new int[3];
						var genSizes = new ulong[3];
						var genFreeCounts = new int[3];
						var genFreeSizes = new ulong[3];

						ulong addr = seg.FirstObject;
						while (addr != 0ul)
						{
							instanceArena.Add(addr);
							var clrType = heap.GetObjectType(addr);
							if (clrType == null)
							{
								mthdtbls.Add(0);
								sizeArena.Add(0u);
								goto NEXT_OBJECT;
							}
							var isFree = Utils.SameStrings(clrType.Name, Constants.FreeTypeName);
							var sz = clrType.GetSize(addr);
							if (sz > (ulong)UInt32.MaxValue) sz = (ulong)UInt32.MaxValue;
							sizeArena.Add((uint)sz);

							// get generation stats
							//
							if (isFree)
								ClrtSegment.SetGenerationStats(seg, addr, sz, genFreeCounts, genFreeSizes);
							else
								ClrtSegment.SetGenerationStats(seg, addr, sz, genCounts, genSizes);

							var clrTypeName = clrType.Name;
							ulong mt = 0UL;

							mt = clrType.MethodTable;
							string key = ClrtType.GetKey(clrTypeName, mt);
							ClrtType clTp;
							if (!typeDct.TryGetValue(key, out clTp))
							{
								var id = AddTypeGetBase(key, clrType, typeDct);
								mthdtbls.Add(id);
							}
							else
							{
								mthdtbls.Add(clTp.Id);
							}
							NEXT_OBJECT:
							addr = seg.NextObject(addr);
						}
						var instanceCount = instanceArena.Count();
						mysegs[i] = new ClrtSegment(heap.Segments[i], instanceArena.GetItemAt(segIndex),
							instanceArena.LastItem(),
							segIndex, instanceCount - 1);
						segIndex = instanceCount;
						mysegs[i].SetGenerationStats(genCounts, genSizes, genFreeCounts, genFreeSizes);
					}

					// refresh heap
					//
					clrRuntime.Flush();
					heap = null;

					// dump segments info
					//
					if (
						!ClrtSegment.DumpSegments(
							DumpFileMoniker.GetFilePath(r, DumpFileMoniker.GetMapFolder(clrtDump.DumpPath), clrtDump.DumpFileNameNoExt,
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

					int[] instanceTps = mthdtbls.GetArrayAndClear();
					mthdtbls = null;


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
					int[] tempIds = new int[typeCount];

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
						tempIds[ndx] = kv.Value.Id;
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
					mthdtbls = null;

					Utils.ForceGcWithCompaction();

					var tempIdMap = Utils.Iota(tempIds.Length);
					Array.Sort(tempIds, tempIdMap);
					int[] tps = new int[instanceTps.Length];
					for (int j = 0, jcnt = tps.Length; j < jcnt; ++j)
					{
						tps[j] = tempIdMap[instanceTps[j]];
					}
					instanceTps = null;
					instanceTypes[r] = tps;

					progress?.Report("Runtime: " + r + ", getting roots.");
					clrtRoots[r] = ClrtRoots.GetRoots(clrRuntime.GetHeap(), instances[r], instanceTypes[r], stringIds[r]);
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

		private static KeyValuePair<string, ulong> ResolveFieldTypeName(ClrHeap heap, ClrType clrType, ulong clrTypeAddr,
			ClrInstanceField field, SortedDictionary<string, ClrtType> dct)
		{
			// TODO JRD
			if (field.Type == null)
				return new KeyValuePair<string, ulong>(Constants.FieldTypeNull, Constants.InvalidAddress);
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

		private static KeyValuePair<string, ulong> ResolveStaticFieldTypeName(ClrHeap heap, ClrType clrType,
			ulong clrTypeAddr, ClrStaticField field, SortedDictionary<string, ClrtType> dct)
		{
			// TODO JRD
			if (field.Type == null)
				return new KeyValuePair<string, ulong>(Constants.FieldTypeNull, Constants.InvalidAddress);
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
			StringIdAsyncDct[] stringIds,
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
					thread.Start(new Tuple
					<string, string, BlockingCollection<KeyValuePair<ulong, KeyValuePair<ulong, int>[]>>,
						List<string>>(
						DumpFileMoniker.GetFilePath(r, DumpFileMoniker.GetMapFolder(clrtDump.DumpPath), clrtDump.DumpFileNameNoExt,
							Constants.MapFieldOffsetsFilePostfix),
						DumpFileMoniker.GetFilePath(r, DumpFileMoniker.GetMapFolder(clrtDump.DumpPath), clrtDump.DumpFileNameNoExt,
							Constants.MapFieldInstancesPostfix),
						que,
						workerErrors
					));

					//// TODO JRD - remove
					//var testTypeId = allTypes[0].GetTypeId("System.ServiceModel.Channels.BindingElement[]");
					//var types = allInstanceTypes[0];
					//int testTypeCnt = 0;
					//for (int i = 0, icnt = instances.Length; i < icnt; ++i)
					//{
					//	if (types[i] == testTypeId)
					//		++testTypeCnt;
					//}

					//// TODO JRD -- remove above

					for (int i = 0, icnt = instances.Length; i < icnt; ++i)
					{
						if ((i % 100000) == 0)
							progress?.Report("Runtime: " + r + ", instance: " + Utils.LargeNumberString(i) + "/" +
											 Utils.LargeNumberString(icnt));
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
								que.Add(new KeyValuePair<ulong, KeyValuePair<ulong, int>[]>(addr,
									fldRefIdNameLst.ToArray()));
							continue;
						}

						if (clrType.Fields.Count < 1) continue;

						fldRefIdNameLst.Clear();
						for (int j = 0, jcnt = clrType.Fields.Count; j < jcnt; ++j)
						{
							var fld = clrType.Fields[j];
							if (!fld.IsObjectReference) continue;

							object fVal = fld.GetValue(addr, false, false);
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
							fldRefIdNameLst.Add(new KeyValuePair<ulong, int>(fAddr, fldNameId));
						}
						if (fldRefIdNameLst.Count > 0)
							que.Add(new KeyValuePair<ulong, KeyValuePair<ulong, int>[]>(addr, fldRefIdNameLst.ToArray()));
					}

					que.Add(new KeyValuePair<ulong, KeyValuePair<ulong, int>[]>(Constants.InvalidAddress, null));
					// signal worker thread to stop

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

		// old
		//public static Thread StartFieldRefInfoPersiter(string instancePath, BlockingCollection<KeyValuePair<int, int[]>> que, List<string> errors)
		//{
		//	Thread thread = new Thread(InstanceReferences.WriteFieldReferences) {Name = "FieldRefInfoPersiter_" + Utils.GetNewID()};
		//	thread.Start(new Tuple<string, BlockingCollection<KeyValuePair<int, int[]>>,List<string>>(
		//																					instancePath,
		//																					que,
		//																					errors
		//																				));
		//	return thread;
		//}

		// old
		//public static Thread StartFieldReference(string instancePath, BlockingCollection<KeyValuePair<int, int[]>> que, List<string> errors)
		//{
		//	Thread thread = new Thread(InstanceReferences.WriteFieldReferences) {Name = "FieldRefBuilder_" + Utils.GetNewID() };
		//	thread.Start(new Tuple<string, BlockingCollection<KeyValuePair<int, int[]>>, List<string>>(
		//																					instancePath,
		//																					que,
		//																					errors
		//																				));
		//	return thread;
		//}


		/// <summary>
		/// Used for testing only.
		/// </summary>
		/// <returns></returns>
		public static StringIdAsyncDct ConstructTypeIdDictionary(ClrRuntime runtime, out string error)
		{
			error = null;
			var heap = runtime.GetHeap();
			var dct = new StringIdAsyncDct();
			dct.AddKey(Constants.FreeTypeName);
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
						if (clrType == null || clrType.Name == null ||
							Utils.SameStrings(clrType.Name, Constants.FreeTypeName)) goto NEXT_OBJECT;
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
				txtWriter.WriteLine("MDR Version: [" + Utils.VersionValue(version).ToString() +
									"], this is this application version.");
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


					txtWriter.WriteLine("Instance Count: " + Utils.LargeNumberString(_instances[i].Length));
					txtWriter.WriteLine("Type Count: " + Utils.LargeNumberString(_types[i].Count));
					txtWriter.WriteLine("Finalizer Queue Count: " +
										Utils.LargeNumberString(_clrtRoots[i].FinalizerQueueCount));
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

		private void DumpModuleInfos(SortedDictionary<string, string> dct, IList<string> extraInfos)
		{
			var path = MapOutputFolder + Path.DirectorySeparatorChar + DumpFileName + Constants.TxtTargetModulesPostfix;
			StreamWriter txtWriter = null;
			try
			{
				ResetReadOnlyAttribute(path);
				txtWriter = new StreamWriter(path);
				txtWriter.WriteLine("### MDRDESK REPORT: Target Module Infos");
				txtWriter.WriteLine("### TITLE: Target Modules");
				txtWriter.WriteLine("### COUNT: " + Utils.LargeNumberString(dct.Count));
				txtWriter.WriteLine("### COLUMNS: Image Base|uint64" + Constants.HeavyGreekCrossPadded +
									"File Size|uint32"
									+ Constants.HeavyGreekCrossPadded + "Is Runtime|string" +
									Constants.HeavyGreekCrossPadded + "Is Managed|string"
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

		private bool WriteStringDct(out string error)
		{
			error = null;
			try
			{
				for (int r = 0; r < _stringIdDcts.Length; ++r)
				{
					var path = DumpFileMoniker.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.TxtStringIdsPostfix);
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

					var path = DumpFileMoniker.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.MapFinalizerFilePostfix);
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
					var path = DumpFileMoniker.GetFilePath(r, MapOutputFolder, DumpFileName, Constants.TxtIndexErrorsFilePostfix);
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

		//public static void MarkAsRooted(ulong addr, int addrNdx, ulong[] instances, int[][] references)
		//{
		//	if (Utils.IsRooted(addr)) return;
		//	instances[addrNdx] = Utils.SetAsRooted(addr);
		//	if (references[addrNdx] == null) return;
		//	for (int i = 0, icnt = references[addrNdx].Length; i < icnt; ++i)
		//	{
		//		var refr = references[addrNdx][i];
		//		var address = instances[refr];
		//		if (Utils.IsNotRooted(address))
		//			if (address != 0UL)
		//			{
		//				instances[refr] = Utils.SetAsRooted(address);
		//				MarkAsRooted(address, refr, instances, references);
		//			}
		//	}
		//}

		//public static string[] GetTypeNames(ClrHeap heap, out string error)
		//{
		//	error = null;
		//	try
		//	{
		//		List<string> typeNames = new List<string>(35000);
		//		AddStandardTypeNames(typeNames);
		//		var typeList = heap.EnumerateTypes();
		//		typeNames.AddRange(typeList.Select(clrType => clrType.Name));
		//		typeNames.Sort(StringComparer.Ordinal);
		//		string[] names = typeNames.Distinct().ToArray();
		//		return names;
		//	}
		//	catch (Exception ex)
		//	{
		//		error = Utils.GetExceptionErrorString(ex);
		//		return null;
		//	}
		//}

		//public static void AddStandardTypeNames(List<string> typeNames)
		//{
		//	typeNames.Add(ClrtType.GetKey(Constants.NullTypeName, 0));
		//	typeNames.Add(ClrtType.GetKey(Constants.UnknownTypeName, 0));
		//}

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

	//public class ClrThreadCmp : IComparer<ClrThread>
	//{
	//	public int Compare(ClrThread a, ClrThread b)
	//	{
	//		if (a.OSThreadId == b.OSThreadId)
	//		{
	//			return a.ManagedThreadId < b.ManagedThreadId ? -1 : (a.ManagedThreadId > b.ManagedThreadId ? 1 : 0);
	//		}
	//		return a.OSThreadId < b.OSThreadId ? -1 : 1;
	//	}
	//}


}