using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.ICorDebug;
using RequestType = System.Tuple<string, object>;
using DumpParamType = System.Tuple<string, System.Collections.Concurrent.BlockingCollection<System.Tuple<string, object>>, System.Collections.Concurrent.BlockingCollection<System.Collections.Generic.KeyValuePair<string, object>>, int, System.Threading.CancellationToken>;

namespace ClrMDRIndex
{
	public class Map : IDisposable
	{
		#region Fields/Properties

		private string _dumpInfo;

		private string _mapFolder;
		private string _dumpBaseName;

		private int _runtimeCount;
		private int _currentRuntime;

		public string DumpInfo => _dumpInfo;

		public string MapFolder => _mapFolder;
		public string DumpBaseName => _dumpBaseName;
		public string DumpPath => string.Concat(_mapFolder.Substring(0, _mapFolder.Length - 4), ".dmp");
		public string ReportPath => MapFolder + Path.DirectorySeparatorChar + Constants.ReportPath;

		public int CurrentRuntime => _currentRuntime;

		private ClrtTypes[] _clrtTypes; // type informations, see ClrtTypes class for details

		public ClrtTypes Types => _clrtTypes[_currentRuntime];
		private string[][] _stringIds; // ordered by string ids
		public string[] StringIds => _stringIds[_currentRuntime];

		private ulong[][] _instances; // list of all heap addresses
		private uint[][] _sizes; // size of instances from Microsoft.Diagnostics.Runtime GetSize(Address objRef) method
		private int[][] _instTypes; // ids of instance types
		public int[] InstanceTypeIds => _instTypes[_currentRuntime];
		private int[][] _instSortedByTypes; // addresses sorted by types, for faster lookups
		private int[][] _instTypeOffsets; // to speed up type addresses lookup 
		private FieldDependency[] _fieldDependencies;
		public FieldDependency FieldDependencies => _fieldDependencies[_currentRuntime];

		private ClrtSegment[][] _segments; // segment infos, for instances generation histograms

		private ClrtRoots[] _roots; // root informations, see ClrtRoots for details
		public ClrtRoots Roots => _roots[_currentRuntime];

		// Dump opened with Microsoft.Runtime.Diagnostics 
		//
		private ClrtDump _clrtDump;
		public ClrtDump Dump => _clrtDump;
		private Thread _dumpThread;
		private BlockingCollection<RequestType> _clrDumpRequests;
		private BlockingCollection<RequestType> _clrDumpResponses;

		// TODO JRD -- check rest of fields

		/// <summary>
		/// Type names, instance counts, bases, etc.
		/// </summary>
		//private string[][] _typeNames; // ordered by type ids
		//private string[][] _typeNamesOrdered; // ordered by names
		//private string[][] _typeReversedNamesOrdered; // ordered by reversed names
		//private int[][] _typeNamesMap; // map from ordered by names to ordered by ids
		//private int[][] _typesReversedNamesMap; //  // map from ordered by reveresed names to ordered by ids

		private ClrElementType[][] _typeElements; // ClrElementType of each type

		/// <summary>
		/// Field informations.
		/// </summary>
		private KeyValuePair<int, int>[][] _typeFieldIndices;

		private KeyValuePair<int, string>[][] _typeFieldInfos; // Key: field type id, Value: field name

		public bool Is64Bit;

		public WeakReference<StringStats>[] _stringStats;


		private IndexCurrentInfo _currentInfo;
		public IndexCurrentInfo CurrentInfo => _currentInfo;

		#endregion Fields/Properties

		#region Ctors/Dtors/Initialization

		public Map(string mapFolder)
		{
			_mapFolder = mapFolder;
			_dumpBaseName = Utils.GetDumpBaseName(mapFolder);
			Is64Bit = Environment.Is64BitOperatingSystem;
		}

		public static Map OpenMap(Version version, string mapFolder, out string error, IProgress<string> progress=null)
		{
			error = null;
			try
			{
				var map = new Map(mapFolder);

				map._dumpInfo = map.LoadDumpInfo(out error);
				if (map._dumpInfo == null)
				{
					return null;
				}
				if (!Utils.IsIndexVersionCompatible(version, map._dumpInfo))
				{
					error = Utils.GetErrorString("Failed to Open Index", mapFolder, "Index version is not compatible with this application's version."
						+ Environment.NewLine + "Please reindex the corresponding crash dump.");
					return null;
				}

				map._runtimeCount = GetRuntimeCount(map._dumpInfo);
				map._stringStats = new WeakReference<StringStats>[map._runtimeCount];
				if (!map.Load(out error,progress)) return null;
				//if (!map.LoadTypeFields(out error)) return null;
				//if (!map.LoadInstancesAndReferences(out error)) return null;
				//if (!map.LoadFinalizerObjectAddresses(out error)) return null; // TODO JRD -- remove
				//if (!map.LoadRoots(out error)) return null;

				if (!map.InitDump(out error, progress)) return null;

				map.SetCurrentInfo(map.CurrentRuntime);
				return map;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		private static int GetRuntimeCount(string dumpinfo)
		{
			var startPos = dumpinfo.IndexOf("Runtime Count: ", StringComparison.Ordinal) + "Runtime Count: ".Length;
			var endPos = dumpinfo.IndexOf(Environment.NewLine, startPos, StringComparison.Ordinal);
			Debug.Assert(endPos > startPos);
			return Int32.Parse(dumpinfo.Substring(startPos, endPos - startPos));

		}

		private bool InitDump(out string error, IProgress<string> progress)
		{
			error = null;
			try
			{
				_clrtDump = new ClrtDump(DumpPath);
				return _clrtDump.Init(out error);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}

		private string LoadDumpInfo(out string error)
		{
			var path = MapFolder + Path.DirectorySeparatorChar + DumpBaseName + Constants.TxtIndexInfoFilePostfix;

			error = null;
			StreamReader rd = null;
			try
			{
				rd = new StreamReader(path);
				return rd.ReadToEnd();
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


		//private bool LoadTypeFields(out string error)
		//{
		//	error = null;
		//	BinaryReader br = null;
		//	try
		//	{
		//		_typeFieldIndices = new KeyValuePair<int, int>[_runtimeCount][];
		//		_typeFieldInfos = new KeyValuePair<int, string>[_runtimeCount][];
		//		for (int r = 0, rcnt = _runtimeCount; r < rcnt; ++r)
		//		{
		//			var ndxPath = Utils.GetFilePath(r, MapFolder, DumpBaseName, Constants.MapTypeFieldIndexFilePostfix);
		//			br = new BinaryReader(File.Open(ndxPath, FileMode.Open));
		//			var cnt = br.ReadInt32();
		//			_typeFieldIndices[r] = new KeyValuePair<int, int>[cnt];
		//			for (int i = 0; i < cnt; ++i)
		//			{
		//				var key = br.ReadInt32();
		//				var val = br.ReadInt32();
		//				_typeFieldIndices[r][i] = new KeyValuePair<int, int>(key, val);
		//			}
		//			br.Close();
		//			br = null;

		//			var fldPath = Utils.GetFilePath(r, MapFolder, DumpBaseName, Constants.MapFieldTypeMapFilePostfix);
		//			br = new BinaryReader(File.Open(fldPath, FileMode.Open));
		//			cnt = br.ReadInt32();
		//			_typeFieldInfos[r] = new KeyValuePair<int, string>[cnt];
		//			for (int i = 0; i < cnt; ++i)
		//			{
		//				var fldTypeId = br.ReadInt32();
		//				var fldNameId = br.ReadInt32();
		//				_typeFieldInfos[r][i] = new KeyValuePair<int, string>(fldTypeId, _stringIds[r][fldNameId]);
		//			}
		//			br.Close();
		//			br = null;
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
		//		br?.Close();
		//	}
		//}

		private bool Load(out string error, IProgress<string> progress)
		{
			error = null;
			try
			{
				_typeElements = new ClrElementType[_runtimeCount][];
				_roots = new ClrtRoots[_runtimeCount];
				_stringIds = new string[_runtimeCount][];
				_clrtTypes = new ClrtTypes[_runtimeCount];
				_instances = new ulong[_runtimeCount][];
				_sizes = new uint[_runtimeCount][];
				_instTypes = new int[_runtimeCount][];
				_instSortedByTypes = new int[_runtimeCount][];
				_instTypeOffsets = new int[_runtimeCount][];
				_segments = new ClrtSegment[_runtimeCount][];
				_fieldDependencies = new FieldDependency[_runtimeCount];

				for (int r = 0, rcnt = _runtimeCount; r < rcnt; ++r)
				{
					_stringIds[r] =
						Utils.GetStringListFromFile(Utils.GetFilePath(r, MapFolder, DumpBaseName, Constants.TxtStringIdsPostfix),
							out error);
					_clrtTypes[r] = ClrtTypes.Load(Utils.GetFilePath(r, MapFolder, DumpBaseName, Constants.MapTypeInfosFilePostfix),
						out error);
					Utils.ReadUlongUintIntArrays(Utils.GetFilePath(r, MapFolder, DumpBaseName, Constants.MapInstanceFilePostfix),
						out _instances[r], out _sizes[r], out _instTypes[r], out error);
					Utils.ReadIntArrays(Utils.GetFilePath(r, MapFolder, DumpBaseName, Constants.MapTypeInstancesFilePostfix),
						out _instSortedByTypes[r], out _instTypeOffsets[r], out error);
					_segments[r] =
						ClrtSegment.ReadSegments(Utils.GetFilePath(r, MapFolder, DumpBaseName, Constants.MapSegmentFilePostfix), out error);
					_roots[r] = ClrtRoots.Load(r, MapFolder, DumpBaseName, out error);

					_fieldDependencies[r] = new FieldDependency(
									Utils.GetFilePath(r, MapFolder, DumpBaseName, Constants.MapFieldParentOffsetsFilePostfix),
									Utils.GetFilePath(r, MapFolder, DumpBaseName, Constants.MapFieldParentInstancesPostfix),
									FieldDependency.MappedFileName(Utils.GetValidName(DumpBaseName))
						);

					_fieldDependencies[r].Init(out error); // TODO JRD handle this error

					//_typeNames[r] = Utils.GetStringListFromFile(Utils.GetFilePath(r, MapFolder, DumpBaseName, Constants.TxtTypeFilePostfix), out error);
					//_typeReversedNamesOrdered[r] = Utils.GetStringListFromFile(Utils.GetFilePath(r, MapFolder, DumpBaseName, Constants.TxtReversedTypeNameFilePostfix), out error);
					//_typeNamesOrdered[r] = Utils.CloneIntArray(_typeNames[r]);
					//_typeNamesMap[r] = Utils.SortAndGetMap(_typeNamesOrdered[r]);
					//_typesReversedNamesMap[r] = Utils.SortAndGetMap(_typeReversedNamesOrdered[r]);

					//uint[] uary = Utils.ReadUintArray(Utils.GetFilePath(r, MapFolder, DumpBaseName, Constants.MapTypeBaseAndElementFilePostfix), out error);
					//_typeElements[r] = new ClrElementType[uary.Length];
					//_typeBases[r] = new int[uary.Length];

					//for (int i = 0, icnt = uary.Length; i < icnt; ++i)
					//{
					//	_typeBases[r][i] = (int)(uary[i] & 0x00FFFFFF);
					//	_typeElements[r][i] = (ClrElementType)(uary[i] >> 24);
					//}

					//uary = Utils.ReadUintArray(Utils.GetFilePath(r, MapFolder, DumpBaseName, Constants.MapTypeFieldCountsFilePostfix), out error);
					//_typeFieldCounts[r] = new int[uary.Length];
					//_typeStaticFieldCounts[r] = new int[uary.Length];
					//for (int i = 0, icnt = uary.Length; i < icnt; ++i)
					//{
					//	_typeFieldCounts[r][i] = (int)(uary[i] & 0x0000FFFF);
					//	_typeStaticFieldCounts[r][i] = (int)(uary[i] >> 16);
					//}

					//_typeInstanceCounts[r] = Utils.ReadIntArray(Utils.GetFilePath(r, MapFolder, DumpBaseName, Constants.MapTypeIntanceCountsFilePostfix), out error);

				}
				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}

		//private bool LoadFinalizerObjectAddresses(out string error)
		//{
		//	error = null;
		//	try
		//	{
		//		_finalizerObjectAddresses = new ulong[_runtimeCount][];
		//		for (int r = 0, rcnt = _runtimeCount; r < rcnt; ++r)
		//		{
		//			var path = Utils.GetFilePath(r, MapFolder, DumpBaseName, Constants.MapFinalizerFilePostfix);
		//			_finalizerObjectAddresses[r] = Utils.ReadUlongArray(path, out error);
		//			if (error != null) return false;
		//		}
		//		return true;
		//	}
		//	catch (Exception ex)
		//	{
		//		error = Utils.GetExceptionErrorString(ex);
		//		return false;
		//	}
		//}

		//private bool LoadXs(out string error)
		//{
		//	error = null;
		//	try
		//	{
		//		return true;
		//	}
		//	catch (Exception ex)
		//	{
		//		error = Utils.GetExceptionErrorString(ex);
		//		return false;
		//	}
		//}

		//private bool LoadInstancesAndReferences(out string error)
		//{
		//	error = null;
		//	BinaryReader br = null;
		//	try
		//	{
		//		_instances = new ulong[_runtimeCount][];
		//		_sizes = new ulong[_runtimeCount][];
		//		_instTypes = new int[_runtimeCount][];
		//		_instTypesOrdered = new int[_runtimeCount][];
		//		_instTypesOrderedMap = new int[_runtimeCount][];
		//		_parents = new int[_runtimeCount][];
		//		_multiParents = new Multiparents[_runtimeCount];
		//		_arrays = new Arrays[_runtimeCount];

		//		for (int r = 0, rcnt = _runtimeCount; r < rcnt; ++r)
		//		{
		//			br = new BinaryReader(File.Open(Utils.GetFilePath(r, _mapFolder, _dumpBaseName, Constants.MapInstanceFilePostfix),
		//												FileMode.Open));
		//			var cnt = br.ReadInt32();
		//			_instances[r] = new ulong[cnt];
		//			_sizes[r] = new ulong[cnt];
		//			_instTypes[r] = new int[cnt];
		//			_parents[r] = new int[cnt];
		//			for (int i = 0; i < cnt; ++i)
		//			{
		//				_instances[r][i] = br.ReadUInt64();
		//				_sizes[r][i] = br.ReadUInt64();
		//				_instTypes[r][i] = br.ReadInt32();
		//				_parents[r][i] = br.ReadInt32();
		//			}
		//			br.Close();
		//			br = null;

		//			_multiParents[r] = new Multiparents(_instances[r],_parents[r]);
		//			if (!_multiParents[r].LoadMultiparents(r, _mapFolder, _dumpBaseName, out error)) return false;

		//			_instTypesOrdered[r] = Utils.CloneIntArray(_instTypes[r]);
		//			_instTypesOrderedMap[r] = Utils.SortAndGetMap(_instTypesOrdered[r]);

		//			br = new BinaryReader(File.Open(Utils.GetFilePath(r, _mapFolder, _dumpBaseName, Constants.MapArrayInstanceFilePostfix),
		//							FileMode.Open));
		//			_arrays[r] = Arrays.Load(br);
		//			br.Close();
		//			br = null;
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
		//		br?.Close();
		//	}
		//}

		//private bool LoadArrayInstances(out string error)
		//{
		//	error = null;
		//	BinaryReader br = null;
		//	try
		//	{
		//		for (int r = 0, rcnt = _runtimeCount; r < rcnt; ++r)
		//		{
		//			br = new BinaryReader(File.Open(Utils.GetFilePath(r, _mapFolder, _dumpBaseName, Constants.MapArrayInstanceFilePostfix),
		//												FileMode.Open));
		//			var cnt = br.ReadInt32();

		//			ulong[] instances = new ulong[cnt];
		//			int[] types = new int[cnt];
		//			int[] sizes = new int[cnt];
		//			int[] componentTypes = new int[cnt];

		//			for (int i = 0; i < cnt; ++i)
		//			{
		//				_instances[r][i] = br.ReadUInt64();
		//				_sizes[r][i] = br.ReadUInt64();
		//				_instTypes[r][i] = br.ReadInt32();
		//				_parents[r][i] = br.ReadInt32();
		//			}
		//			br.Close();
		//			br = null;

		//			_multiParents[r] = new Multiparents(_instances[r], _parents[r]);
		//			if (!_multiParents[r].LoadMultiparents(r, _mapFolder, _dumpBaseName, out error)) return false;

		//			_instTypesOrdered[r] = Utils.CloneIntArray(_instTypes[r]);
		//			_instTypesOrderedMap[r] = Utils.SortAndGetMap(_instTypesOrdered[r]);

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
		//		br?.Close();
		//	}
		//}

		//private bool ReadFieldReferences(out pair<int, int>[][] fldRefs, out int[][] parents, out string error)
		//{
		//	error = null;
		//	fldRefs = new pair<int, int>[_runtimeCount][];
		//	parents = new int[_runtimeCount][];
		//	for (int r = 0, rcnt = _runtimeCount; r < rcnt; ++r)
		//	{
		//		var path = Utils.GetFilePath(r, _mapFolder, _dumpBaseName, Constants.MapFieldsAndParentsFilePostfix);
		//		BinaryReader br = null;
		//		try
		//		{
		//			var flds = fldRefs[r];
		//			var prnts = parents[r];
		//			br = new BinaryReader(File.Open(path, FileMode.Open));
		//			var fldCnt = br.ReadInt32();
		//			var prnCnt = br.ReadInt32();
		//			var fldAry = new pair<int, int>[fldCnt];
		//			var prnAry = new int[prnCnt];
		//			for (int i = 0; i < fldCnt; ++i)
		//			{
		//				var first = br.ReadInt32();
		//				var second = br.ReadInt32();
		//				fldAry[i] = new pair<int, int>(first, second);
		//			}
		//			for (int i = 0; i < prnCnt; ++i)
		//			{
		//				prnAry[i] = br.ReadInt32();
		//			}
		//			br.Close();
		//			br = null;
		//			fldRefs[r] = fldAry;
		//			parents[r] = prnAry;
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


		public void SetCurrentInfo(int runtimeIndex)
		{
			_currentInfo = new IndexCurrentInfo(
				_instances[runtimeIndex],
				_sizes[runtimeIndex],
				_instTypes[runtimeIndex],
				_instSortedByTypes[runtimeIndex],
				_instTypeOffsets[runtimeIndex],
				_clrtTypes[runtimeIndex],
				_stringIds[runtimeIndex]
				);
		}

		#endregion Ctors/Dtors/Initialization

		#region Queries

		#region Instances

		public ulong[] Instances => _instances[_currentRuntime];

		public int GetInstanceIdAtAddr(ulong addr)
		{
			var ndx = Array.BinarySearch(_instances[_currentRuntime], addr);
			return ndx < 0 ? Constants.InvalidIndex : ndx;
		}

		#endregion Instances

		#region Types

		public string[] TypeNames => _clrtTypes[_currentRuntime].Names;
		public string[] ReversedTypeNames => _clrtTypes[_currentRuntime].ReversedNames;

		public ulong[] GetTypeAddressesFromSortedIndex(int ndx, out ClrElementType elem)
		{
			elem = _clrtTypes[_currentRuntime].GetElementType(ndx);
			return GetTypeAddresses(ndx);
		}
		public ulong[] GetTypeAddressesFromReversedNameIndex(int ndx, out ClrElementType elem)
		{
			ndx = _clrtTypes[_currentRuntime].ReversedNamesMap[ndx];
			elem = _clrtTypes[_currentRuntime].GetElementType(ndx);
			return GetTypeAddresses(ndx);
		}

		public ulong[] GetTypeAddressesFromMt(ulong mt, out ClrElementType elem)
		{
			if (mt == 0)
			{
				elem = ClrElementType.Unknown;
				return Utils.EmptyArray<ulong>.Value;
			}
			var ndx = _clrtTypes[_currentRuntime].GetTypeId(mt);
			elem = _clrtTypes[_currentRuntime].GetElementType(ndx);
			return GetTypeAddresses(ndx);
		}

		public int TypeCount()
		{
			return _clrtTypes[_currentRuntime].Count;
		}

		public int GetTypeId(string typeName)
		{
			return _clrtTypes[_currentRuntime].GetTypeId(typeName);
		}

		public string GetTypeNameFromInstanceId(int instId)
		{
			if (Constants.InvalidIndex == instId) return Constants.NullTypeName;
			var typeId = _instTypes[_currentRuntime][instId];
			return GetTypeName(typeId);
		}

		public string GetTypeName(int typeId)
		{
			return _clrtTypes[_currentRuntime].GetName(typeId);
		}

		public string GetTypeNameAtAddr(ulong addr)
		{
			var ndx = Array.BinarySearch(_instances[_currentRuntime], addr);
			return ndx < 0 ? Constants.Unknown : _clrtTypes[_currentRuntime].GetName(_instTypes[_currentRuntime][ndx]);
		}


        public KeyValuePair<string,int> GetTypeNameAndIdAtAddr(ulong addr)
        {
            var ndx = Array.BinarySearch(_instances[_currentRuntime], addr);
            if (ndx < 0)
                return new KeyValuePair<string, int>(Constants.Unknown,Constants.InvalidIndex);
            int typeId = _instTypes[_currentRuntime][ndx];
            string typeName = GetTypeName(typeId);
            return new KeyValuePair<string, int>(typeName,typeId);
        }

	    public AncestorDispRecord[] GroupAddressesByTypesForDisplay(KeyValuePair<ulong, int>[] infos)
	    {
	        var dct = new SortedDictionary<triple<int, string, string>, List<ulong>>(new Utils.TripleIntStrStrCmp());
	        for (int i = 0, icnt = infos.Length; i < icnt; ++i)
	        {
	            var addr = infos[i].Key;
                KeyValuePair<string, int> typeInfo = GetTypeNameAndIdAtAddr(addr);
	            var typeName = typeInfo.Key;
	            var typeId = typeInfo.Value;
	            var fldName = GetString(infos[i].Value);
                var key = new triple<int,string,string>(typeId,typeName,fldName);
	            List<ulong> lst;
	            if (dct.TryGetValue(key, out lst))
	            {
	                lst.Add(addr);
	            }
	            else
	            {
	                dct.Add(key,new List<ulong>(8) {addr});
	            }
	        }
            var ary = new AncestorDispRecord[dct.Count];
	        int ndx = 0;
	        foreach (var kv in dct)
	        {
	            ary[ndx++] = new AncestorDispRecord(kv.Key.First,kv.Key.Second,kv.Key.Third,kv.Value.ToArray());
	        }
            Array.Sort(ary,new AncestorDispRecordCmp());
	        return ary;
	    }

        public KeyValuePair<int, string>[] GetElementTypes(ClrElementType et) // TODO JRD
		{
			//var etinfo = _elementTypeCounts[_currentRuntime][(int)et];
			//if (etinfo.TypeIds.Count == 0) return Utils.EmptyArray<KeyValuePair<int, string>>.Value;
			//var ary = new KeyValuePair<int,string>[etinfo.TypeIds.Count];
			//for (int i = 0, icnt = etinfo.TypeIds.Count; i < icnt; ++i)
			//{
			//	var id = etinfo.TypeIds[i];
			//	var typeName = _typeNames[_currentRuntime][id];
			//	ary[i] = new KeyValuePair<int, string>(id,typeName);
			//}
			//return ary;
			return null;
		}

		public KeyValuePair<string, KeyValuePair<string, int>[]>[] GetNamespaceDisplay()
		{
			return _clrtTypes[_currentRuntime].GetNamespaceDisplay(_stringIds[_currentRuntime]);
		}


		public ClrElementType[] GetNonemptyElementTypes() // TODO JRD
		{
			//var etinfo = _elementTypeCounts[_currentRuntime];
			//var lst = new List<ClrElementType>();
			//for (int i = 0, icnt = etinfo.Length; i < icnt; ++i)
			//{

			//	if (etinfo[i].TypeIds.Count == 0) continue;
			//	lst.Add(etinfo[i].Type);
			//}
			//return lst.ToArray();
			return null;
		}

		public ulong[] GetTypeAddresses(int typeId)
		{
			if (_instTypeOffsets[_currentRuntime].Length <= typeId + 1) return Utils.EmptyArray<ulong>.Value;
			var addrCnt = _instTypeOffsets[_currentRuntime][typeId + 1] - _instTypeOffsets[_currentRuntime][typeId];
			if (addrCnt < 1) return Utils.EmptyArray<ulong>.Value;
			var addresses = new ulong[addrCnt];
			var offset = _instTypeOffsets[_currentRuntime][typeId];
			var instances = _instances[_currentRuntime];
			var instSortedByTypes = _instSortedByTypes[_currentRuntime];
			for (int i = 0; i < addrCnt; ++i)
			{
				addresses[i] = instances[instSortedByTypes[offset]];
				++offset;
			}
			Array.Sort(addresses);
			return addresses;
		}

		public KeyValuePair<int,ulong[]>[] GetTypeAddresses(int[] typeIds, out int totalCount)
		{
			totalCount = 0;
			KeyValuePair<int, ulong[]>[] result = new KeyValuePair<int, ulong[]>[typeIds.Length];
			for (int i = 0, icnt = typeIds.Length; i < icnt; ++i)
			{
				var addrAry = GetTypeAddresses(typeIds[i]);
				totalCount += addrAry.Length;
				result[i] = new KeyValuePair<int, ulong[]>(typeIds[i], addrAry);
			}
			return result;
		}

		public int[] GetTypeIds(string prefix)
		{
			return Types.GetTypeIds(prefix);
		}

		public KeyValuePair<int, ulong[]>[] GetTypeWithPrefixAddresses(string prefix, bool includeArrays, out int totalCount)
		{
			int[] ids = Types.GetTypeIds(prefix);
			if (!includeArrays)
			{
				ids = RemoveArrayType(ids);
			}
			return GetTypeAddresses(ids,out totalCount);
		}

		private int[] RemoveArrayType(int[] typeIds)
		{
			int cntToRemove = 0;
			for (int i = 0, icnt = typeIds.Length; i < icnt; ++i)
			{
				if (Types.IsArray(typeIds[i]))
				{
					typeIds[i] = Int32.MaxValue;
					++cntToRemove;
				}
			}
			if (cntToRemove > 0)
			{
				int[] newAry = new int[typeIds.Length-cntToRemove];
				int ndx = 0;
				for (int i = 0, icnt = typeIds.Length; i < icnt; ++i)
				{
					if (typeIds[i] != Int32.MaxValue)
						newAry[ndx++] = typeIds[i];
				}
				return newAry;
			}
			return typeIds;
		}

		public int GetBaseId(int id)
		{
			return _clrtTypes[_currentRuntime].GetBaseId(id);
		}

		public int[] GetSameBaseTypeIds(int id)
		{
			return _clrtTypes[_currentRuntime].GetSameBaseTypeIds(id);
		}

		/// <summary>
		/// Returns field list for a given type.
		/// </summary>
		/// <param name="typeId">Internal type id.</param>
		/// <returns>Array of tuples (field type id, field name, field type name).</returns>
		public triple<int, string, string>[] GetFieldNamesAndTypes(int typeId)
		{

			//var kv = _typeFieldIndices[_currentRuntime][typeId];
			//if (kv.Value == 0) return Utils.EmptyArray<triple<int, string, string>>.Value;
			//var result = new triple<int, string, string>[kv.Value];
			//for (int i = 0, begin = kv.Key; i < kv.Value; ++begin,++i)
			//{
			//	var fldTypeName = _typeNames[_currentRuntime][_typeFieldInfos[_currentRuntime][begin].Key];
			//	result[i] = new triple<int, string, string>(
			//		_typeFieldInfos[_currentRuntime][begin].Key,
			//		_typeFieldInfos[_currentRuntime][begin].Value,
			//		fldTypeName
			//		);

			//}
			//return result;
			return null; // TODO JRD
		}

		//public InstanceTypeNode GetParentReferences(ulong addr, out string error, int maxLevel = Int32.MaxValue)
		//{

		//	error = null;
		//	int instId = GetInstanceIdAtAddr(addr);
		//	if (instId == Constants.InvalidIndex)
		//	{
		//		error = "Address: " + Utils.AddressString(addr) + ", not found.";
		//		return null;
		//	}

		//	int level = 0;
		//	var instances = _instances[_currentRuntime];
		//	var instTypes = _instTypes[_currentRuntime];
		//	var clrtTypes = _clrtTypes[_currentRuntime];
		//	var fields = FieldDependencies;
		//	var parents = _fieldParents[_currentRuntime];
		//	var roots = _roots[_currentRuntime];
		//	var stringIds = _stringIds[_currentRuntime];


		//	var instAddr = addr;
		//	var typeId = instTypes[instId];
		//	var typeName = clrtTypes.GetName(typeId);
		//	var rootNode = new InstanceTypeNode(typeId, typeName, instId, instAddr, 0);

		//	int fldNdx = Array.BinarySearch(fields, new pair<int, int>(instId, 0), new Utils.IntPairCmp());
		//	if (fldNdx < 0)
		//	{
		//		rootNode.AddRoot(GetRootString(instAddr, roots, stringIds));
		//		return rootNode; // no parents
		//	}

		//	HashSet<ulong> uniqueParentSet = new HashSet<ulong>();
		//	Queue<InstanceTypeNode> que = new Queue<InstanceTypeNode>(256);
		//	que.Enqueue(rootNode);
		//	List<InstanceTypeNode> nodeLst = new List<InstanceTypeNode>(64);

		//	while (que.Count > 0)
		//	{
		//		var curNode = que.Dequeue();
		//		curNode.AddRoot(GetRootString(curNode.Address, roots, stringIds));

		//		level = curNode.Level + 1;
		//		if (level > maxLevel) break;

		//		fldNdx = Array.BinarySearch(fields, new pair<int, int>(curNode.Id, 0), new Utils.IntPairCmp());
		//		if (fldNdx < 0) continue;
		//		var off = fields[fldNdx].Second;
		//		var parentCount = fields[fldNdx + 1].Second - off;
		//		nodeLst.Clear();
		//		//var nodes = new InstanceTypeNode[parentCount];
		//		for (int i = 0; i < parentCount; ++i, ++off)
		//		{
		//			instId = parents[off];
		//			instAddr = instances[instId];
		//			if (!uniqueParentSet.Add(instAddr)) continue;
		//			typeId = instTypes[instId];
		//			typeName = clrtTypes.GetName(typeId);
		//			var node = new InstanceTypeNode(typeId, typeName, instId, instAddr, level);
		//			nodeLst.Add(node);
		//			que.Enqueue(node);
		//		}
		//		curNode.AddNodes(nodeLst.ToArray());
		//	}

		//	return rootNode;
		//}

		/// <summary>
		/// Get sorted list of type id for a given element type.
		/// </summary>
		/// <param name="etype"></param>
		/// <returns>etype type id list, this list can be empty.</returns>
		public IList<int> GetElementTypeTypes(ClrElementType etype)
		{
			List<int> lst = new List<int>(1024);
			var elems = _typeElements[_currentRuntime];
			for (int i = 0, icnt = elems.Length; i < icnt; ++i)
			{
				if (etype == elems[i]) lst.Add(i);
			}
			lst.Sort();
			lst.TrimExcess();
			return lst;
		}

	    public Tuple<DependencyNode,int>  GetAddressesDescendants(int typeId, ulong[] addresses, int maxLevel, out string error)
	    {
	        error = null;
		    int nodeCount = 0;
 	        try
	        {
	            string typeName = GetTypeName(typeId);
                DependencyNode root = new DependencyNode(0,typeId,typeName,string.Empty,addresses);
		        ++nodeCount;
                HashSet<ulong> addrSet = new HashSet<ulong>();
				Queue<DependencyNode> que = new Queue<DependencyNode>(1024);

				DependencyNode[] nodes = DependencyNode.BuildBranches(Types, Instances, InstanceTypeIds, StringIds, FieldDependencies, root, addresses, out error);
		        for (int i = 0, icnt = nodes.Length; i < icnt; ++i)
		        {
			        que.Enqueue(nodes[i]);
			        ++nodeCount;
		        }

				while (que.Count > 0)
	            {
	                var node = que.Dequeue();
                    if (node.Level==maxLevel) continue;

					nodes = DependencyNode.BuildBranches(Types, Instances, InstanceTypeIds, StringIds, FieldDependencies, node, node.Addresses, out error);
		            for (int i = 0, icnt = nodes.Length; i < icnt; ++i)
		            {
			            que.Enqueue(nodes[i]);
			            ++nodeCount;
		            }
				}

                return Tuple.Create(root,nodeCount);
	        }
	        catch (Exception ex)
	        {
	            error = Utils.GetExceptionErrorString(ex);
	            return null;
	        }
	    }

		//private KeyValuePair<ulong, int>[][] GetDescendants(ulong[] addresses, List<KeyValuePair<ulong, int>[]> lst,
		//    HashSet<ulong> addrSet, out string error)
		//{
		//    error = null;
		//       var fldDpnds = _fieldDependencies[_currentRuntime];
		//       lst.Clear();
		//       for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
		//       {
		//           if (!addrSet.Add(addresses[i])) continue;
		//           var result = fldDpnds.GetFieldParents(addresses[i], out error);
		//           if (result != null && result.Length > 0)
		//               lst.Add(result);
		//       }
		//    return lst.ToArray();
		//}

		//public ClrtDisplayableType GetDisplayableType(ClrHeap heap, ulong addr, int typeId, string fieldName, out string error)
		//{
		//	error = null;
		//	try
		//	{
		//		var clrType = heap.GetObjectType(addr);
		//		var category = ValueExtractor.GetTypeCategory(clrType);
		//		var dispType = new ClrtDisplayableType(typeId, clrType.Name, fieldName, category);
		//		if (clrType.Fields.Count == 0) return dispType;
		//		bool internalAddr = ClrtTypes.HasInternalAddresses(clrType);
		//		for (int i = 0, icnt = clrType.Fields.Count; i < icnt; ++i)
		//		{
		//			ClrInstanceField fld = clrType.Fields[i];
		//			ClrType fldType = null;
		//			if (fld.IsObjectReference)
		//			{
		//				var obj = fld.GetValue(addr, internalAddr, false);
		//				if (obj == null) // use field type
		//				{
		//					fldType = fld.Type;
		//				}
		//				else
		//				{
		//					fldType = heap.GetObjectType((ulong) obj);
		//				}
		//				// var fldTypeId = GetTypeNameAndIdAtAddr() // TODO JRD
		//			}
		//			else
		//			{
						
		//			}

		//		}

		//		return dispType;
		//	}
		//	catch (Exception ex)
		//	{
		//		error = Utils.GetExceptionErrorString(ex);
		//		return null;
		//	}
		//}
		
        #endregion Types

        #region Instance Walk

	   // public Tuple<InstanceValue, AncestorDispRecord[]> GetInstanceInfoOld(ulong addr, out string error)
	   // {
	   //     error = null;
	   //     try
	   //     {
	   //         var typeInfo = GetTypeNameAndIdAtAddr(addr);

    //            // get ancestors
    //            //
	   //         var ancestors = _fieldDependencies[_currentRuntime].GetFieldParents(addr, out error);
				//AncestorDispRecord[] ancestorInfos = GroupAddressesByTypesForDisplay(ancestors);

    //            // get instance info: fields and values
    //            //
    //            var heap = GetFreshHeap();
	   //         ClrType clrType = heap.GetObjectType(addr);
		  //      bool hasInternalAddresses = ClrtTypes.HasInternalAddresses(clrType);
	   //         InstanceValue root;
	   //         string clrValue = null;
	   //         if (clrType.HasSimpleValue && !clrType.IsObjectReference)
	   //         {
	   //             var obj = clrType.GetValue(addr);
	   //             clrValue = ValueExtractor.GetPrimitiveValue(obj, clrType);
    //                root = new InstanceValue(typeInfo.Value,addr,typeInfo.Key,string.Empty,clrValue);
	   //         }
	   //         else
	   //         {
	   //             if (clrType.IsArray)
	   //             {
	   //                 var acnt = clrType.GetArrayLength(addr);
	   //                 clrValue = "[" + acnt.ToString() + "]";
    //                    root = new InstanceValue(typeInfo.Value, addr, typeInfo.Key, string.Empty, clrValue);
    //                }
    //                else
	   //             {
    //                    root = new InstanceValue(typeInfo.Value, addr, typeInfo.Key, string.Empty, Constants.NonValue);
    //                    for (int i = 0, icnt = clrType.Fields.Count; i < icnt; ++i)
    //                    {
    //                        InstanceValue instVal = null;
    //                        var fld = clrType.Fields[i];
    //                        clrValue = ValueExtractor.TryGetPrimitiveValue(heap, addr, clrType.Fields[i], hasInternalAddresses);
    //                        if (Utils.IsNonValue(clrValue))
    //                        {
    //                            if (clrType.IsValueClass)
    //                            {
    //                                var typeName = fld.Type != null ? fld.Type.Name : Constants.Unknown;
    //                                var typeId = GetTypeId(typeName);
    //                                instVal = new InstanceValue(typeId, Constants.InvalidAddress, typeName, fld.Name, clrValue);

    //                            }
    //                            else
    //                            {
				//					ulong typeAddr = Constants.InvalidAddress;
				//					ulong valAddr = Constants.InvalidAddress;
				//					object typeAddrObj = null;
				//					if (fld.Type != null)
	   //                             {
		  //                              if (fld.Type.IsValueClass)
		  //                              {
			 //                               typeAddr = fld.GetAddress(addr, hasInternalAddresses);
				//						}
				//						else
		  //                              {
				//							typeAddrObj = fld.GetValue(addr, hasInternalAddresses, false);
				//							typeAddr = (ulong?)typeAddrObj ?? Constants.InvalidAddress;
				//						}
				//					}
				//					else
				//					{
				//						typeAddrObj = fld.GetValue(addr, hasInternalAddresses, false);
				//						typeAddr = (ulong?)typeAddrObj ?? Constants.InvalidAddress;
				//					}
				//					KeyValuePair<string,int> typeInformation = GetTypeNameAndIdAtAddr(typeAddr);
				//					if (typeAddr == Constants.InvalidAddress)
	   //                             {
				//						var typeName = fld.Type != null ? fld.Type.Name : Constants.Unknown;
		  //                              var typeId = GetTypeId(typeName);
				//						typeInformation = new KeyValuePair<string, int>(typeName,typeId);
	   //                             }
    //                                instVal = new InstanceValue(typeInformation.Value, typeAddr, typeInformation.Key, fld.Name, clrValue);
    //                            }
    //                        }
    //                        else
    //                        {
    //                            var typeName = fld.Type != null ? fld.Type.Name : Constants.NullName;
    //                            var typeId = GetTypeId(typeName);
    //                            instVal = new InstanceValue(typeId, Constants.InvalidAddress, typeName, fld.Name, clrValue);
    //                        }

    //                        root.Addvalue(instVal);
    //                    }
    //                }
    //            }

	   //         return new Tuple<InstanceValue, AncestorDispRecord[]>(root,ancestorInfos);
	   //     }
	   //     catch (Exception ex)
	   //     {
    //            error = Utils.GetExceptionErrorString(ex);
	   //         return null;
	   //     }
	   // }

		/// <summary>
		/// Get instance information for hierarchy walk.
		/// </summary>
		/// <param name="addr">Instance address.</param>
		/// <param name="fldNdx">Field index, this is used for struct types, in this case addr is of the parent.</param>
		/// <param name="error">Output error.</param>
		/// <returns>Instance information, and list of its parents.</returns>
		public Tuple<InstanceValue, AncestorDispRecord[]> GetInstanceInfo(ulong addr, int fldNdx,  out string error)
		{
			try
			{
				// get ancestors
				//
				var ancestors = _fieldDependencies[_currentRuntime].GetFieldParents(addr, out error);
				AncestorDispRecord[] ancestorInfos = GroupAddressesByTypesForDisplay(ancestors);

				// get instance info: fields and values
				//
				var heap = GetFreshHeap();
				var result = DmpNdxQueries.FQry.getInstanceValue(_currentInfo, heap, addr, fldNdx);
				error = result.Item1;
				result.Item2?.SortByFieldName();
				return new Tuple<InstanceValue, AncestorDispRecord[]>(result.Item2, ancestorInfos);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		#endregion Instance Walk

		#region Type Value Reports

		public ClrtDisplayableType GetTypeDisplayableRecord(int typeId, out string error)
		{
			error = null;
			try
			{
				ulong[] instances = GetTypeAddresses(typeId);
				if (instances == null || instances.Length < 1)
				{
					error = "Type instances not found.";
					return null;
				}
				return DmpNdxQueries.FQry.getDisplayableType(_currentInfo, Dump.Heap, instances[0]);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}

		}

		public ClrtDisplayableType GetTypeDisplayableRecord(ClrtDisplayableType dispType, ClrtDisplayableType dispTypeField, out string error)
		{
			error = null;
			try
			{
				ulong[] instances = GetTypeAddresses(dispTypeField.TypeId);
				if (instances != null && instances.Length > 0)
					return DmpNdxQueries.FQry.getDisplayableType(_currentInfo, Dump.Heap, instances[0]);
				instances = GetTypeAddresses(dispType.TypeId);
				if (instances == null || instances.Length < 1)
				{
					error = "Type instances not found.";
					return null;
				}

				var result = DmpNdxQueries.FQry.getDisplayableFieldType(_currentInfo, Dump.Heap, instances[0], dispTypeField.FieldIndex);
			    if (result.Item1 != null)
			    {
				    error = Constants.InformationSymbolHeader + result.Item1;
			        return null;
			    }
				dispType.AddFields(result.Item2);
				return dispType;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}

		}

		#endregion Type Value Reports

		#region Field References

		public KeyValuePair<ulong, int>[] GetParents(ulong addr, out string error)
		{
			return _fieldDependencies[_currentRuntime].GetFieldParents(addr, out error);
		}

		public Tuple<string,triple<string, string, string>[], triple<string, string, string>[]> GetInstanceParentsAndChildren(ulong address, out string error)
		{
			error = null;
			var parents = _fieldDependencies[_currentRuntime].GetFieldParents(address, out error);
			if (error!=null) return new Tuple<string, triple<string, string, string>[], triple<string, string, string>[]>(error,null,null);
			triple<string, string, string>[] parentInfos = new triple<string, string, string>[parents.Length];
			var types = _clrtTypes[_currentRuntime];
			for (int i = 0, icnt = parents.Length; i < icnt; ++i)
			{
				var parent = parents[i];
				var fldName = GetString(parent.Value);
				var typeName = GetTypeNameAtAddr(parent.Key);
				triple<string, string, string> entry = new triple<string, string, string>(Utils.AddressString(parent.Key),fldName,typeName);
				parentInfos[i] = entry;
			}
			var fields = GetFields(address, out error);
			if (error != null) return new Tuple<string, triple<string, string, string>[], triple<string, string, string>[]>(error, null, null);
			return new Tuple<string,triple<string, string, string>[], triple<string, string, string>[]>(null,parentInfos,fields);
		}


		const int MaxNodes = 1000;

		public Tuple<InstanceNode, int, KeyValuePair<ulong, ulong>[]> GetFieldReferences(ulong address, out string error, int maxLevel = Int32.MaxValue)
		{

			error = null;
			int instId = GetInstanceIdAtAddr(address);
			if (instId == Constants.InvalidIndex)
			{
				error = "Address: " + Utils.AddressString(address) + ", not found.";
				return null;
			}

			var instances = _instances[_currentRuntime];
			var roots = _roots[_currentRuntime];
			var fieldDependencies = _fieldDependencies[_currentRuntime];

			int level = 0;
			var instAddr = address;
			var rootNode = new InstanceNode(instAddr, 0); // we at level 0
			var uniqueAddrSet = new HashSet<ulong>();
			var que = new Queue<InstanceNode>(256);
			var nodeLst = new List<InstanceNode>(64);
			var errorLst = new List<string>();
			var backReferences = new List<KeyValuePair<ulong, ulong>>();
			var extraInfoQue = new BlockingCollection<InstanceNode>();

			var extraTask =
				Task.Factory.StartNew(
					() =>
						GetInstanceNodeExtraInfo(new Tuple<ulong[], ClrtRoots, BlockingCollection<InstanceNode>>(instances, roots,
							extraInfoQue)));

			que.Enqueue(rootNode);
			uniqueAddrSet.Add(instAddr);
			int nodeCount = 0;
			while (que.Count > 0)
			{
				nodeLst.Clear();
				var curNode = que.Dequeue();
				++nodeCount;
				extraInfoQue.Add(curNode);
				if (curNode.Level >= maxLevel || nodeCount > MaxNodes) continue;
				var parents = fieldDependencies.GetFieldParents(curNode.Address, out error);
				if (parents == null)
				{
					errorLst.Add(Constants.FailureSymbolHeader + "GetFieldParents for " + curNode.Address + Environment.NewLine + error);
					continue;
				}

				for (int i = 0, icnt = parents.Length; i < icnt; ++i)
				{
					var info = parents[i];
					if (!uniqueAddrSet.Add(info.Key))
					{
						backReferences.Add(new KeyValuePair<ulong, ulong>(curNode.Address, info.Key));
						continue;
					}
					var cnode = new InstanceNode(info.Value, info.Key, curNode.Level + 1);
					nodeLst.Add(cnode);
					que.Enqueue(cnode);
				}

				if (nodeLst.Count > 0)
				{
					curNode.SetNodes(nodeLst.ToArray());
				}

			}
			extraInfoQue.Add(null);
			extraTask.Wait();

			return Tuple.Create(rootNode, nodeCount, backReferences.ToArray());
		}

		private void GetInstanceNodeExtraInfo(object data)
		{
			var args = data as Tuple<ulong[], ClrtRoots, BlockingCollection<InstanceNode>>;
			var instances = args.Item1;
			var roots = args.Item2;
			var que = args.Item3;

			while (true)
			{
				InstanceNode node = que.Take();
				if (node == null) break;
				var instNdx = Array.BinarySearch(instances, node.Address);
				if (instNdx >= 0) node.SetInstanceIndex(instNdx);
				ClrtRoot root;
				bool inFinalizerQueue;
				if (roots.GetRootInfo(node.Address, out root, out inFinalizerQueue))
				{
					node.SetRootInfo(new RootInfo(root, inFinalizerQueue));
				}
			}
		}

		#endregion Field References

		#region Strings

		public string GetString(int id)
		{
			var strs = _stringIds[_currentRuntime];
			if (id < 0 || id >= strs.Length) return Constants.Unknown;
			return strs[id];
		}

		public bool AreStringDataFilesAvailable()
		{
			return StringStats.StringStatsFilesExist(_currentRuntime, DumpPath);
		}

		public static ulong[] GetStringAddresses(ClrHeap heap, string str, ulong[] addresses, out string error)
		{
			error = null;
			try
			{
				List<ulong> lst = new List<ulong>(10 * 1024);
				for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
				{
					var heapStr = ValueExtractor.GetStringAtAddress(addresses[i], heap);
					if (Utils.SameStrings(str, heapStr))
						lst.Add(addresses[i]);
				}
				return lst.ToArray();
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="error"></param>
		public ListingInfo GetStringStats(int minReferenceCount, out string error, bool includeGenerations=false)
		{
			error = null;
			try
			{
				if (StringStats.StringStatsFilesExist(_currentRuntime, DumpPath))
				{
					StringStats strStats = null;
					if (_stringStats[_currentRuntime] == null || !_stringStats[_currentRuntime].TryGetTarget(out strStats))
					{
						strStats = StringStats.GetStringsInfoFromFiles(_currentRuntime, DumpPath, out error);
						if (strStats == null)
							return new ListingInfo(error);
						if (_stringStats[_currentRuntime] == null)
							_stringStats[_currentRuntime] = new WeakReference<StringStats>(strStats);
						else
							_stringStats[_currentRuntime].SetTarget(strStats);
					}
					ListingInfo data = null;
					if (includeGenerations)
						data = strStats.GetGridData(minReferenceCount,_segments[_currentRuntime],out error);
					else
						data = strStats.GetGridData(minReferenceCount, out error);

					return data ?? new ListingInfo(error);
				}

				var strTypeId = _clrtTypes[_currentRuntime].GetTypeId("System.String");
				Debug.Assert(strTypeId != Constants.InvalidIndex);
				ulong[] addresses = GetTypeAddresses(strTypeId);
				var runtime = Dump.Runtime;
				runtime.Flush();
				var heap = runtime.GetHeap();


				var stats = ClrtDump.GetStringStats(heap, addresses, DumpPath, out error);
				if (stats == null) return null;
				ListingInfo lstData = null;
				if (includeGenerations)
					lstData = stats.GetGridData(minReferenceCount, _segments[_currentRuntime], out error);
				else
					lstData = stats.GetGridData(minReferenceCount, out error);

				return lstData ?? new ListingInfo(error);
			}
			catch (Exception ex)
			{
				return new ListingInfo(Utils.GetExceptionErrorString(ex));
			}
		}

		public StringStats GetStringStats(out string error)
		{
			error = null;
			try
			{
				if (StringStats.StringStatsFilesExist(_currentRuntime, DumpPath))
				{
					StringStats strStats = null;
					if (_stringStats[_currentRuntime] == null || !_stringStats[_currentRuntime].TryGetTarget(out strStats))
					{
						strStats = StringStats.GetStringsInfoFromFiles(_currentRuntime, DumpPath, out error);
						if (strStats == null)
							return null;
						if (_stringStats[_currentRuntime] == null)
							_stringStats[_currentRuntime] = new WeakReference<StringStats>(strStats);
						else
							_stringStats[_currentRuntime].SetTarget(strStats);
					}
					return strStats;
				}

				var strTypeId = _clrtTypes[_currentRuntime].GetTypeId("System.String");
				Debug.Assert(strTypeId != Constants.InvalidIndex);
				ulong[] addresses = GetTypeAddresses(strTypeId);
				var runtime = Dump.Runtime;
				runtime.Flush();
				var heap = runtime.GetHeap();


				var stats = ClrtDump.GetStringStats(heap, addresses, DumpPath, out error);
				if (stats == null) return null;

				if (_stringStats[_currentRuntime] == null)
					_stringStats[_currentRuntime] = new WeakReference<StringStats>(stats);
				else
					_stringStats[_currentRuntime].SetTarget(stats);

				return stats;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}


		public StringStats GetCurrentStringStats(out string error)
		{
			error = null;
			try
			{
				StringStats strStats = null;
				if (_stringStats[_currentRuntime] == null || !_stringStats[_currentRuntime].TryGetTarget(out strStats))
				{
					strStats = StringStats.GetStringsInfoFromFiles(_currentRuntime, DumpPath, out error);
					if (strStats == null) return null;
					if (_stringStats[_currentRuntime] == null)
						_stringStats[_currentRuntime] = new WeakReference<StringStats>(strStats);
					else
						_stringStats[_currentRuntime].SetTarget(strStats);
				}
				return strStats;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public long GetSizeOfStringsWithPrefix(string str, out string error)
		{
			error = null;
			var strStats = GetCurrentStringStats(out error);
			if (error != null) return -1L;
			return strStats.GetSizeOfStringsWithPrefix(str);
		}

		public ListingInfo GetTypesWithSpecificStringFieldListing(string strContent)
		{
			string error;
			var result = GetTypesWithSpecificStringField(strContent, out error);
			if (error != null)
				return new ListingInfo(error);
			var dct = result.Item1;
			if (dct.Count < 1)
			{
				return new ListingInfo();
			}
			var itemCount = result.Item2;
			ColumnInfo[] columns = new ColumnInfo[]
			{
				new ColumnInfo("String Addr", ReportFile.ColumnType.UInt64, 200,1,true),
				new ColumnInfo("Parent Addr", ReportFile.ColumnType.UInt64, 200,2,true),
				new ColumnInfo("Parent Count", ReportFile.ColumnType.Int32, 200,3,true),
				new ColumnInfo("Field", ReportFile.ColumnType.String, 200,4,true),
				new ColumnInfo("Type", ReportFile.ColumnType.String, 500,5,true)
			};
			listing<string>[] listAry = new listing<string>[itemCount];
			string[] dataAry = new string[itemCount * columns.Length];
			int lisAryNdx = 0;
			int dataAryNdx = 0;
			foreach (var kv in dct)
			{
				var parts = kv.Key.Split(new string[] { Constants.FieldSymbolPadded }, StringSplitOptions.None);
				Debug.Assert(parts.Length == 2);
				var addresses = kv.Value;
				for (int i = 0, icnt = addresses.Count; i < icnt; ++i)
				{
					listAry[lisAryNdx++] = new listing<string>(dataAry, dataAryNdx, columns.Length);
					dataAry[dataAryNdx++] = Utils.AddressString(addresses[i].Key);
					dataAry[dataAryNdx++] = Utils.AddressString(addresses[i].Value);
					dataAry[dataAryNdx++] = Utils.LargeNumberString(icnt);
					dataAry[dataAryNdx++] = parts[1];
					dataAry[dataAryNdx++] = parts[0];
				}
			}
			StringBuilder sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
			sb.Append(ReportFile.DescrPrefix).Append("Type instances containing with string field: ").Append(strContent).AppendLine();
			sb.Append(ReportFile.DescrPrefix).Append("Total reference count: ").Append(itemCount).AppendLine();
			return new ListingInfo(null, listAry, columns, StringBuilderCache.GetStringAndRelease(sb), dct);
		}

		public Tuple<SortedDictionary<string, List<KeyValuePair<ulong, ulong>>>, int> GetTypesWithSpecificStringField(string strContent, out string error)
		{
			error = null;
			try
			{
				var strStats = GetCurrentStringStats(out error);
				var addresses = strStats.GetStringAddresses(strContent, out error);
				if (error != null)
				{
					error = Utils.GetErrorString("Types with Specific String Field", "StringStats.GetStringAddresses failed.",
						error);
					return null;
				}

				List<string> errors = new List<string>();
				KeyValuePair<ulong, KeyValuePair<ulong, int>[]>[] parentInfos = _fieldDependencies[_currentRuntime].GetMultiFieldParents(addresses, errors);
				var dct = new SortedDictionary<string, List<KeyValuePair<ulong, ulong>>>(StringComparer.Ordinal);
				int addrCount = 0;
				for (int i = 0, icnt = parentInfos.Length; i < icnt; ++i)
				{
					for (int j = 0, jcnt = parentInfos[i].Value.Length; j < jcnt; ++j)
					{

						var typeName = GetTypeNameAtAddr(parentInfos[i].Value[j].Key);
						var fldName = GetString(parentInfos[i].Value[j].Value);
						var typeEntry = typeName + Constants.FieldSymbolPadded + fldName;
						List<KeyValuePair<ulong, ulong>> lst;
						if (dct.TryGetValue(typeEntry, out lst))
						{
							lst.Add(new KeyValuePair<ulong, ulong>(parentInfos[i].Key, parentInfos[i].Value[j].Key));
						}
						else
						{
							dct.Add(typeEntry, new List<KeyValuePair<ulong, ulong>>() { new KeyValuePair<ulong, ulong>(parentInfos[i].Key, parentInfos[i].Value[j].Key) });
						}

						++addrCount;
					}

				}
				return new Tuple<SortedDictionary<string, List<KeyValuePair<ulong, ulong>>>, int>(dct, addrCount);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}


		public Tuple<SortedDictionary<string, List<KeyValuePair<ulong, ulong>>>, int> GetTypesWithSpecificStringFieldOld(string strContent, out string error)
		{
			error = null;
			try
			{
				var strStats = GetCurrentStringStats(out error);
				var addresses = strStats.GetStringAddresses(strContent, out error);
				if (error != null)
				{
					error = Utils.GetErrorString("Types with Specific String Field", "StringStats.GetStringAddresses failed.",
						error);
					return null;
				}

				var dct = new SortedDictionary<string, List<KeyValuePair<ulong, ulong>>>(StringComparer.Ordinal);
				var fldRefList = new List<KeyValuePair<ulong, int>>(64);
				int addrCount = 0;
				ulong[] instances = Instances;
				var heap = Dump.Runtime.GetHeap();
				for (int i = 0, icnt = instances.Length; i < icnt; ++i)
				{
					ulong addr = instances[i];
					if (Array.BinarySearch(addresses, addr) >= 0) continue;

					var clrType = heap.GetObjectType(addr);
					if (clrType == null || clrType.IsString) continue;


					fldRefList.Clear();
					clrType.EnumerateRefsOfObjectCarefully(addr, (address, off) =>
					{
						fldRefList.Add(new KeyValuePair<ulong, int>(address, off));
					});

					bool isArray = clrType.IsArray;
					for (int j = 0, jcnt = fldRefList.Count; j < jcnt; ++j)
					{
						var fldAddr = fldRefList[j].Key;
						if (Array.BinarySearch(addresses, fldAddr) < 0) continue; // not my string
						string fldName = string.Empty;
						if (!isArray)
						{
							ClrInstanceField fld;
							int childFieldOffset;
							if (clrType.GetFieldForOffset(fldRefList[j].Value, clrType.IsValueClass, out fld,
								out childFieldOffset))
							{
								fldName = fld.Name;
							}
						}
						else
						{
							fldName = "[]";
						}
						string lookupName = clrType.Name + Constants.FieldSymbolPadded + fldName;
						List<KeyValuePair<ulong, ulong>> lst;
						++addrCount;
						if (dct.TryGetValue(lookupName, out lst))
						{
							lst.Add(new KeyValuePair<ulong, ulong>(addr, fldAddr));
						}
						else
						{
							dct.Add(lookupName, new List<KeyValuePair<ulong, ulong>>() { new KeyValuePair<ulong, ulong>(addr, fldAddr) });
						}
					}
				}
				return new Tuple<SortedDictionary<string, List<KeyValuePair<ulong, ulong>>>, int>(dct, addrCount);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public SortedDictionary<string, KeyValuePair<int, uint>> GetStringsSizesInfo(string path, out long totalSize, out long totalUniqueSize, out int totalCount, out string error)
		{
			totalSize = totalUniqueSize = 0;
			totalCount = 0;
			try
			{
				var dump = ClrtDump.OpenDump(path, out error);
				if (dump == null) return null;
				return ClrtDump.GetStringCountsAndSizes(dump.Runtime.GetHeap(), out totalSize, out totalUniqueSize, out totalCount, out error);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				Dump?.Dispose();
			}
		}


		#endregion Strings

		#region Roots/Finalization

		public ClrtRoot GetRoot(ulong addr)
		{
			ClrtRoot root;
			_roots[_currentRuntime].GetRootInfoByObject(addr, out root);
			return root;
		}

		public ulong[] FinalizerQueue => _roots[_currentRuntime].FinalizerQue;

		public Tuple<KeyValuePair<string, string>[], KeyValuePair<string, string>[]> GetDisplayableFinalizationQueue(bool sortByTypeNames = false)
		{
			var que = _roots[_currentRuntime].GetDisplayableFinalizationQueue(_instTypes[_currentRuntime],
				_clrtTypes[_currentRuntime].Names);
			if (que == null || que.Length < 1)
				return new Tuple<KeyValuePair<string, string>[], KeyValuePair<string, string>[]>
									(Utils.EmptyArray<KeyValuePair<string, string>>.Value, Utils.EmptyArray<KeyValuePair<string, string>>.Value);
			if (sortByTypeNames)
			{
				Array.Sort(que, new Utils.KVStrStrCmp());
			}

			SortedDictionary<string, int> dct = new SortedDictionary<string, int>(StringComparer.Ordinal);
			for (int i = 1, icnt = que.Length; i < icnt; ++i)
			{
				string typeName = que[i].Value;
				int cnt;
				if (dct.TryGetValue(typeName, out cnt))
				{
					dct[typeName] = cnt + 1;
					continue;
				}
				dct.Add(typeName, 1);
			}

			KeyValuePair<int, string>[] hist = Utils.GetHistogram(dct);
			KeyValuePair<string, string>[] histogram = new KeyValuePair<string, string>[hist.Length + 1];
			histogram[0] = new KeyValuePair<string, string>(Utils.SizeString(que.Length), "Total Finalizer Queue Count");
			for (int i = 0, icnt = hist.Length; i < icnt; ++i)
			{
				histogram[i + 1] = new KeyValuePair<string, string>(Utils.SizeString(hist[i].Key), hist[i].Value);
			}
			return new Tuple<KeyValuePair<string, string>[], KeyValuePair<string, string>[]>(que, histogram);
		}

		//		public Tuple<ulong[], ulong[]> GetNotRooted(ulong[] addreses)
		//		{
		//			var instances = _instances[_currentRuntime];
		////			var fields = _fieldReferences[_currentRuntime];
		//			var parents = _fieldParents[_currentRuntime];
		//			var roots = _roots[_currentRuntime];

		//			var notRootedLst = new List<ulong>(Math.Max(addreses.Length / 2, 256));
		//			var notFoundInstances = new List<ulong>();

		//			HashSet<ulong> uniqueParentSet = new HashSet<ulong>();
		//			var que = new Queue<KeyValuePair<int, ulong>>(256);
		//			var pairCmp = new Utils.IntPairCmp();

		//			for (int i = 0, icnt = addreses.Length; i < icnt; ++i)
		//			{
		//				var addr = addreses[i];
		//				if (roots.IsRootedOutsideFinalization(addr)) continue;
		//				var instId = Array.BinarySearch(instances, addr);
		//				if (instId < 0)
		//				{
		//					notFoundInstances.Add(addr);
		//					continue;
		//				}
		//				uniqueParentSet.Clear();
		//				que.Clear();
		//				que.Enqueue(new KeyValuePair<int, ulong>(instId, addr));
		//				while (que.Count > 0)
		//				{
		//					var curInfo = que.Dequeue();
		//					if (roots.IsRootedOutsideFinalization(curInfo.Value)) goto NEXT_ADDRESS;
		//					var fldNdx = Array.BinarySearch(fields, new pair<int, int>(curInfo.Key, 0), pairCmp);
		//					if (fldNdx < 0) continue;
		//					var off = fields[fldNdx].Second;
		//					var parentCount = fields[fldNdx + 1].Second - off;
		//					for (int j = 0; j < parentCount; ++j, ++off)
		//					{
		//						instId = parents[off];
		//						var instAddr = instances[instId];
		//						if (!uniqueParentSet.Add(instAddr)) continue;
		//						if (roots.IsRootedOutsideFinalization(instAddr)) goto NEXT_ADDRESS;
		//						que.Enqueue(new KeyValuePair<int, ulong>(instId, instAddr));
		//					}
		//				}
		//				notRootedLst.Add(addr);
		//				NEXT_ADDRESS: continue;
		//			}

		//			return new Tuple<ulong[], ulong[]>(notRootedLst.ToArray(), notFoundInstances.ToArray());
		//		}

		private string GetRootString(ulong addr, ClrtRoots roots, string[] stringIds)
		{
			ClrtRoot root;
			bool inFinalizerQueue;
			bool rootFound = roots.GetRootInfo(addr, out root, out inFinalizerQueue);
			if (rootFound)
			{
				var rootName = stringIds[root.NameId];
				var rootAdress = Utils.AddressStringHeader(root.Address);
				var rootType = root.GetKindString();
				return (inFinalizerQueue ? Constants.HeavyCheckMarkPadded : string.Empty)
					   + Constants.HeavyRightArrowPadded
					   + rootAdress + "{" + rootType + "} " + rootName;
			}
			return inFinalizerQueue ? Constants.HeavyCheckMarkPadded : string.Empty;
		}

		public bool GetRootInfo(ulong addr, out ClrtRoot info, out bool inFinilizerQueue)
		{
			return _roots[_currentRuntime].GetRootInfo(addr, out info, out inFinilizerQueue);
		}

		#endregion Roots/Finalization

		public static void MapsDiff(Map map0, Map map1)
		{
			var instCntDiff = map0._instances[map0._currentRuntime].Length - map1._instances.Length;
		}

		#region Segments/Generations

		public Tuple<string, long>[][] GetGenerationTotals()
		{
			Tuple<int[], ulong[], int[], ulong[]> histograms =
				ClrtSegment.GetTotalGenerationDistributions(_segments[_currentRuntime]);

			Tuple<string, long>[] ary0 = new Tuple<string, long>[4];
			ary0[0] = new Tuple<string, long>("G0", histograms.Item1[0]);
			ary0[1] = new Tuple<string, long>("G1", histograms.Item1[1]);
			ary0[2] = new Tuple<string, long>("G2", histograms.Item1[2]);
			ary0[3] = new Tuple<string, long>("LOH", histograms.Item1[3]);

			Tuple<string, long>[] ary1 = new Tuple<string, long>[4];
			ary1[0] = new Tuple<string, long>("G0", (long)histograms.Item2[0]);
			ary1[1] = new Tuple<string, long>("G1", (long)histograms.Item2[1]);
			ary1[2] = new Tuple<string, long>("G2", (long)histograms.Item2[2]);
			ary1[3] = new Tuple<string, long>("LOH", (long)histograms.Item2[3]);

			Tuple<string, long>[] ary2 = new Tuple<string, long>[4];
			ary2[0] = new Tuple<string, long>("G0", histograms.Item3[0]);
			ary2[1] = new Tuple<string, long>("G1", histograms.Item3[1]);
			ary2[2] = new Tuple<string, long>("G2", histograms.Item3[2]);
			ary2[3] = new Tuple<string, long>("LOH", histograms.Item3[3]);

			Tuple<string, long>[] ary3 = new Tuple<string, long>[4];
			ary3[0] = new Tuple<string, long>("G0", (long)histograms.Item4[0]);
			ary3[1] = new Tuple<string, long>("G1", (long)histograms.Item4[1]);
			ary3[2] = new Tuple<string, long>("G2", (long)histograms.Item4[2]);
			ary3[3] = new Tuple<string, long>("LOH", (long)histograms.Item4[3]);

			return new Tuple<string, long>[][]
			{
				ary0,
				ary1,
				ary2,
				ary3,
			};
		}

		public int[] GetGenerationHistogram(ulong[] addresses)
		{
			return ClrtSegment.GetGenerationHistogram(_segments[_currentRuntime], addresses);
		}

		public int[] GetStringGcGenerationHistogram(string strContent, out string error)
		{
			error = null;
			var strStats = GetCurrentStringStats(out error);
			var addresses = strStats.GetStringAddresses(strContent, out error);
			if (error != null) return null;
			return ClrtSegment.GetGenerationHistogram(_segments[_currentRuntime], addresses);
		}

		public int[] GetTypeGcGenerationHistogram(string typeName, out string error)
		{
			error = null;
			var typeId = GetTypeId(typeName);
			if (typeId == Constants.InvalidIndex)
			{
				error = "Cannot find type: " + typeName;
				return null;
			}
			ulong[] addresses = GetTypeAddresses(typeId);
			return ClrtSegment.GetGenerationHistogram(_segments[_currentRuntime], addresses);
		}

		public static bool GetLohInfo(ClrRuntime runtime, out string error)
		{
			error = null;
			try
			{
				runtime.Flush();
				var heap = runtime.GetHeap();

				var segs = heap.Segments;
				var segMemFragments = new List<triple<bool, ulong, ulong>[]>();
				var stringsLsts = new List<string[]>(segs.Count);
				var typeInfoLst = new List<SortedDictionary<string, List<KeyValuePair<ulong, ulong>>>>(segs.Count);
				var countAndSize = new List<KeyValuePair<ulong, int>>(segs.Count);

				for (int i = 0, icnt = segs.Count; i < icnt; ++i)
				{
					var seg = segs[i];
					if (!seg.IsLarge) continue;

					int cnt = 0;
					var dct = new SortedDictionary<string, List<KeyValuePair<ulong, ulong>>>(StringComparer.Ordinal);
					var strings = new List<string>(128);
					var fragments = new List<triple<bool, ulong, ulong>>(1024);
					fragments.Add(new triple<bool, ulong, ulong>(true, seg.Start, 0));

					ulong addr = seg.FirstObject;
					while (addr != 0ul)
					{
						var clrType = heap.GetObjectType(addr);
						if (clrType == null) goto NEXT_OBJECT;
						var sz = clrType.GetSize(addr);
						if (!clrType.IsFree)
						{
							++cnt;
							if (clrType.IsString)
							{
								strings.Add(ClrMDRIndex.ValueExtractor.GetStringAtAddress(addr, heap));
							}
							List<KeyValuePair<ulong, ulong>> lst;
							if (dct.TryGetValue(clrType.Name, out lst))
							{
								lst.Add(new KeyValuePair<ulong, ulong>(addr, sz));
							}
							else
							{
								dct.Add(clrType.Name, new List<KeyValuePair<ulong, ulong>>(128) { new KeyValuePair<ulong, ulong>(addr, sz) });
							}
						}
						ClrMDRIndex.ValueExtractor.SetSegmentInterval(fragments, addr, sz, clrType.IsFree);
						NEXT_OBJECT:
						addr = seg.NextObject(addr);
					}
					var lastFragment = fragments[fragments.Count - 1];
					var lastAddr = lastFragment.Second + lastFragment.Third;
					if (seg.End > lastAddr)
						ClrMDRIndex.ValueExtractor.SetSegmentInterval(fragments, lastAddr, seg.End - lastAddr, true);

					// collect segment info
					//
					countAndSize.Add(new KeyValuePair<ulong, int>(seg.End + 1ul - seg.Start, cnt));
					typeInfoLst.Add(dct);
					stringsLsts.Add(strings.ToArray());
					segMemFragments.Add(fragments.ToArray());
				}

				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}

		#endregion Segments/Generations

		#region Instance Values

		//public InstanceValue GetValue(ulong address, out string error)
		//{
		//	error = null;
		//	int typeId = GetInstanceIdAtAddr(address);
		//	if (Utils.IsIndexInvalid(typeId))
		//	{
		//		error = "Cannot get instance type at address: " + Utils.AddressString(address);
		//		return null;
		//	}

		//	var heap = GetFreshHeap();
		//	ClrType clrType = heap.GetObjectType(address);
		//	if (clrType == null)
		//	{
		//		error = "Cannot get object type at address: " + Utils.AddressString(address);
		//		return null;
		//	}

		//	InstanceValue value = new InstanceValue(typeId,address);





		//	return value;

		//}

		#endregion Instance Values

		#endregion Queries

		#region Extra Info/Utilities

		public Tuple<ulong, KeyValuePair<ulong, ulong>[]> GetFree(int runtimeIndex, out string error)
		{
			error = null;
			BinaryReader br = null;
			try
			{
				var path = Utils.GetFilePath(runtimeIndex, _mapFolder, _dumpBaseName, Constants.MapHeapFreeFilePostfix);
				br = new BinaryReader(File.Open(path, FileMode.Open));
				var count = br.ReadInt32();
				var frees = new KeyValuePair<ulong, ulong>[count];
				ulong totSize = 0;
				for (int i = 0; i < count; ++i)
				{
					var addr = br.ReadUInt64();
					var size = br.ReadUInt64();
					totSize += size;
					frees[i] = new KeyValuePair<ulong, ulong>(addr, size);
				}
				return Tuple.Create(totSize, frees);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				br?.Close();
			}
		}

		public bool DumpReversedNames(string filePath, out string error)
		{
			error = null;
			StreamWriter sw = null;
			try
			{
				sw = new StreamWriter(filePath);
				var names = _clrtTypes[_currentRuntime].Names;
				var reversedNames = _clrtTypes[_currentRuntime].ReversedNames;
				var reversedNamesMap = _clrtTypes[_currentRuntime].ReversedNamesMap;
				sw.WriteLine("### count = " + reversedNames.Length);
				for (int i = 0, icnt = reversedNames.Length; i < icnt; ++i)
				{
					sw.Write(Utils.SmallIdHeader(reversedNamesMap[i]));
					sw.Write(reversedNames[i]);
					sw.Write(Constants.HeavyRightArrowPadded);
					sw.WriteLine(names[reversedNamesMap[i]]);
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
				sw?.Close();
			}
		}

		#endregion Extra Info/Utilities

		#region Dump Interface

		public ClrHeap GetFreshHeap()
		{
			return Dump.GetFreshHeap();
		}

		public static void DumpQueries(object parameters)
		{
			var info = parameters as DumpParamType;
			var dumpPath = info.Item1;
			var requests = info.Item2;
			var responses = info.Item3;
			var currentRuntime = info.Item4;
			var cancelToken = info.Item5;
			string error;

			var dump = ClrtDump.OpenDump(dumpPath, out error);
			if (dump == null)
			{
				responses.Add(new KeyValuePair<string, object>("Failed to open: " + dumpPath, error));
				return;
			}
			responses.Add(new KeyValuePair<string, object>("Opened: " + dumpPath, null));

			while (!cancelToken.IsCancellationRequested)
			{
				var request = requests.Take(cancelToken);

				switch (request.Item1)
				{
					case "GetObjectsTotalSize":
						ulong[] addreses = request.Item2 as ulong[];

						break;
				}
				request = null;
			}

		}


		public triple<string,string,string>[] GetFields(ulong address, out string error)
		{
			error = null;
			var heap = GetFreshHeap();
			try
			{
				ClrType clrType = heap.GetObjectType(address);

				if (clrType.IsString)
				{
					var str = ValueExtractor.GetStringAtAddress(address, heap);
					return new[] { new triple<string, string, string>(Utils.AddressStringHeader(address) + str, "", clrType.Name) };
				}
				if (clrType.IsArray)
				{
					return new[] {new triple<string, string, string>(Utils.AddressStringHeader(address), "[]", clrType.Name)};
				}

				bool isStruct = clrType.IsValueClass;

				List<triple<string,string,string>> fldList = new List<triple<string, string, string>>(64);
				var fldRefList = new List<KeyValuePair<ulong, int>>(64);
				var fldRefSet = new HashSet<ulong>();

				clrType.EnumerateRefsOfObjectCarefully(address, (addr, off) =>
				{
					fldRefList.Add(new KeyValuePair<ulong, int>(addr, off));
				});
				string curFldName;
				if (fldRefList.Count > 0)
				{
					for (int k = 0, kcnt = fldRefList.Count; k < kcnt; ++k)
					{
						var fldAddr = fldRefList[k].Key;
						if (fldAddr != 0UL)
						{
							int childFieldOffset;
							int curOffset = fldRefList[k].Value;
							ClrInstanceField iFld;
							if (clrType.GetFieldForOffset(curOffset, isStruct, out iFld, out childFieldOffset))
							{
								curFldName = iFld.Name;
								var fldEntry = new triple<string,string,string>(Utils.AddressString(fldAddr),curFldName, (iFld.Type!=null) ? iFld.Type.Name : "<unknown type>");
								fldList.Add(fldEntry);
								while (childFieldOffset != 0 && curOffset != childFieldOffset)
								{
									curOffset = childFieldOffset;
									if (clrType.GetFieldForOffset(curOffset, isStruct, out iFld, out childFieldOffset))
									{
										curFldName = curFldName + "." + iFld.Name;
										fldEntry = new triple<string, string, string>(Utils.AddressString(fldAddr), curFldName, (iFld.Type != null) ? iFld.Type.Name : "<unknown type>");

									}
									else break;
								}
							}
							else
							{


							}
						}
					}
				}
				return fldList.ToArray();
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}

		}

		//public Tuple<ulong, sextuple<int, ulong, ulong, ulong, ulong, string>[]> GetAllTypesSizesInfo(out string error)
		//{
		//	error = null;
		//	try
		//	{
		//		var runtime = Dump.Runtime;
		//		runtime.Flush();
		//		var heap = runtime.GetHeap();
		//		int typeCount = _clrtTypes[_currentRuntime].Count;
		//		ulong grandTotal = 0ul;
		//		var sizeInfosLst = new List<sextuple<int, ulong, ulong, ulong, ulong, string>>(typeCount);

		//		for (int i = 0; i < typeCount; ++i)
		//		{
		//			ulong[] addresses = GetTypeAddresses(i);
		//			int cnt = addresses.Length;
		//			if (cnt < 1) continue;
		//			string typeName = _clrtTypes[_currentRuntime].GetName(i);
		//			ulong totalSize = 0ul;
		//			ulong maxSize = 0ul;
		//			ulong minSize = ulong.MaxValue;
		//			for (int j = 0; j < cnt; ++j)
		//			{
		//				var addr = addresses[j];
		//				if (addr == 0ul) continue;
		//				ClrType clrType = heap.GetObjectType(addr);
		//				if(clrType == null) continue;
		//				var sz = clrType.GetSize(addr);
		//				totalSize += sz;
		//				if (sz > maxSize) maxSize = sz;
		//				if (sz < minSize) minSize = sz;
		//			}
		//			double avg = (double) totalSize/(double)(cnt);
		//			ulong uavg = Convert.ToUInt64(avg);

		//			sizeInfosLst.Add(new sextuple<int, ulong, ulong, ulong, ulong, string>(
		//				cnt, totalSize, maxSize, minSize, uavg, typeName
		//				));
		//			grandTotal += totalSize;
		//		}
		//		// sort by total size, descending
		//		var sizeInfos = sizeInfosLst.ToArray();
		//		Array.Sort(sizeInfos, (a, b) => a.Second < b.Second ? 1 : (a.Second > b.Second ? -1 : 0));

		//		return Tuple.Create(grandTotal,sizeInfos);
		//	}
		//	catch (Exception ex)
		//	{
		//		error = Utils.GetExceptionErrorString(ex);
		//		return null;
		//	}
		//}

		public ListingInfo CompareTypesWithOther(string otherDumpPath)
		{
			const int ColumnCount = 6;
			string error;
			var runtime = Dump.Runtime;
			runtime.Flush();
			var heap = runtime.GetHeap();
			var myTypeDCT = ClrtDump.GetTypeSizesInfo(heap, out error);
			if (error != null) return new ListingInfo(error);
			var otherTypeDct = GetTypeSizesInfo(otherDumpPath, out error);
			if (error != null) return new ListingInfo(error);
			// merge dictionaries
			HashSet<string> set = new HashSet<string>(myTypeDCT.Keys);
			set.UnionWith(otherTypeDct.Keys);
			listing<string>[] dataListing = new listing<string>[set.Count];
			string[] data = new string[set.Count * ColumnCount];
			int totalCount0 = 0;
			ulong grandTotalSize0 = 0UL;
			int totalCount1 = 0;
			ulong grandTotalSize1 = 0UL;

			int listNdx = 0;
			int dataNdx = 0;
			foreach (var str in set)
			{
				int count0 = 0;
				int count1 = 0;
				ulong totSize0 = 0UL;
				ulong totSize1 = 0UL;
				quadruple<int, ulong, ulong, ulong> info0;
				if (myTypeDCT.TryGetValue(str, out info0))
				{
					count0 = info0.First;
					totSize0 = info0.Second;
					totalCount0 += count0;
					grandTotalSize0 += totSize0;
				}
				quadruple<int, ulong, ulong, ulong> info1;
				if (otherTypeDct.TryGetValue(str, out info1))
				{
					count1 = info1.First;
					totSize1 = info1.Second;
					totalCount1 += count1;
					grandTotalSize1 += totSize1;
				}
				dataListing[listNdx++] = new listing<string>(data, dataNdx, ColumnCount);
				data[dataNdx++] = Utils.LargeNumberString(count0);
				data[dataNdx++] = Utils.LargeNumberString(count1);
				data[dataNdx++] = Utils.LargeNumberString(count0-count1);
				data[dataNdx++] = Utils.LargeNumberString(totSize0);
				data[dataNdx++] = Utils.LargeNumberString(totSize1);
				data[dataNdx++] = str;
			}

			myTypeDCT.Clear();
			myTypeDCT = null;
			otherTypeDct.Clear();
			otherTypeDct = null;
			set.Clear();
			set = null;
			Utils.ForceGcWithCompaction();

			ColumnInfo[] colInfos = new[]
			{
				new ColumnInfo(Constants.BlackDiamond + " Count", ReportFile.ColumnType.Int32,150,1,true),
				new ColumnInfo(Constants.AdhocQuerySymbol + " Count", ReportFile.ColumnType.Int32,150,2,true),
				new ColumnInfo(Constants.BlackDiamond + " Count Diff", ReportFile.ColumnType.Int32,150,3,true),
				new ColumnInfo(Constants.BlackDiamond + " Total Size", ReportFile.ColumnType.UInt64,150,4,true),
				new ColumnInfo(Constants.AdhocQuerySymbol + " Total Size", ReportFile.ColumnType.UInt64,150,5,true),
				new ColumnInfo("Type", ReportFile.ColumnType.String,500,6,true),
			};

			Array.Sort(dataListing, ReportFile.GetComparer(colInfos[4]));

			var otherDmpName = Path.GetFileName(otherDumpPath);
			StringBuilder sb = new StringBuilder(512);
			sb.Append(Constants.BlackDiamond).Append(" Index Dump: ").Append(DumpBaseName).AppendLine();
			sb.Append(Constants.AdhocQuerySymbol).Append(" Adhoc Dump: ").Append(otherDmpName).AppendLine();
			sb.Append(Constants.BlackDiamond).Append(" Total Instance Count: ").Append(Utils.LargeNumberString(totalCount0)).AppendLine();
			sb.Append(Constants.AdhocQuerySymbol).Append(" Total Instance Count: ").Append(Utils.LargeNumberString(totalCount1)).AppendLine();
			sb.Append(Constants.BlackDiamond).Append(" Total Instance Size: ").Append(Utils.LargeNumberString(grandTotalSize0)).AppendLine();
			sb.Append(Constants.AdhocQuerySymbol).Append(" Total Instance Size: ").Append(Utils.LargeNumberString(grandTotalSize1)).AppendLine();

			return new ListingInfo(null, dataListing, colInfos, sb.ToString());
		}

		public ListingInfo CompareStringsWithOther(string otherDumpPath)
		{
			string error;

			const int ColumnCount = 7;

			StringStats myStrStats = GetStringStats(out error);
			if (error != null) return new ListingInfo(error);

			long otherTotalSize, otherTotalUniqueSize;
			int otherTotalCount;
			SortedDictionary<string, KeyValuePair<int, uint>> otherInfo = GetStringsSizesInfo(otherDumpPath, out otherTotalSize, out otherTotalUniqueSize, out otherTotalCount, out error);
			if (error != null) return new ListingInfo(error);

			string[] otStrings = new string[otherInfo.Count];
			int[] otCounts = new int[otherInfo.Count];
			uint[] otSizes = new uint[otherInfo.Count];

			int ndx = 0;
			foreach (var kv in otherInfo)
			{
				otStrings[ndx] = kv.Key;
				otCounts[ndx] = kv.Value.Key;
				otSizes[ndx] = kv.Value.Value;
				++ndx;
			}
			otherInfo.Clear();
			otherInfo = null;

			var maxCnt = Math.Max(otStrings.Length, myStrStats.Count);
			int myNdx = 0;
			int myCnt = myStrStats.Count;
			int otherNdx = 0;
			int otherCnt = otStrings.Length;

			var myStrings = myStrStats.Strings;
			var myCounts = myStrStats.Counts;
			var mySizes = myStrStats.Sizes;


			List<string> data = new List<string>(100000);
			while (true)
			{
				if (myNdx < myCnt && otherNdx < otherCnt)
				{
					var cmp = string.Compare(myStrings[myNdx], otStrings[otherNdx], StringComparison.Ordinal);
					if (cmp == 0)
					{
						var myCount = myCounts[myNdx];
						var otCount = otCounts[otherNdx];
						var mySize = mySizes[myNdx];
						GetStringsDiffLine(myStrings[myNdx], myCount, otCount, mySize, data);
						++myNdx;
						++otherNdx;
					}
					else if (cmp < 0)
					{
						var myCount = myCounts[myNdx];
						var mySize = mySizes[myNdx];
						GetStringsDiffLine(myStrings[myNdx], myCount, 0, mySize, data);
						++myNdx;
					}
					else
					{
						Debug.Assert(cmp > 0);
						var otCount = otCounts[otherNdx];
						var otSize = otSizes[otherNdx];
						GetStringsDiffLine(otStrings[otherNdx], 0, otCount, otSize, data);
						++otherNdx;
					}
				}
				else if (myNdx < myCnt)
				{
					var myCount = myCounts[myNdx];
					var mySize = mySizes[myNdx];
					GetStringsDiffLine(myStrings[myNdx], myCount, 0, mySize, data);
					++myNdx;
				}
				else if (otherNdx < otherCnt)
				{
					var otCount = otCounts[otherNdx];
					var otSize = otSizes[otherNdx];
					GetStringsDiffLine(otStrings[otherNdx], 0, otCount, otSize, data);
					++otherNdx;
				}
				else
				{
					break;
				}
			}

			string[] dataAry = data.ToArray();
			listing<string>[] listing = new listing<string>[dataAry.Length / ColumnCount];
			Debug.Assert((dataAry.Length % ColumnCount) == 0);

			data.Clear();
			data = null;
			int dataNdx = 0;
			for (int i = 0, icnt = listing.Length; i < icnt; ++i)
			{
				listing[i] = new listing<string>(dataAry, dataNdx, 6);
				dataNdx += ColumnCount;
			}

			ColumnInfo[] colInfos = new[]
			{
				new ColumnInfo(Constants.BlackDiamond + " Count", ReportFile.ColumnType.Int32,150,1,true),
				new ColumnInfo(Constants.AdhocQuerySymbol + " Count", ReportFile.ColumnType.Int32,150,2,true),
				new ColumnInfo(Constants.BlackDiamond + " Count Diff", ReportFile.ColumnType.Int32,150,3,true),
				new ColumnInfo(Constants.BlackDiamond + " Total Size", ReportFile.ColumnType.UInt64,150,4,true),
				new ColumnInfo(Constants.AdhocQuerySymbol + " Total Size", ReportFile.ColumnType.UInt64,150,5,true),
				new ColumnInfo("Size", ReportFile.ColumnType.UInt32,150,6,true),
				new ColumnInfo("Type", ReportFile.ColumnType.String,500,7,true),
			};

			//Array.Sort(dataListing, ReportFile.GetComparer(colInfos[6]));

			var otherDmpName = Path.GetFileName(otherDumpPath);
			StringBuilder sb = new StringBuilder(512);
			sb.Append(Constants.BlackDiamond).Append(" Index Dump: ").Append(DumpBaseName).AppendLine();
			sb.Append(Constants.AdhocQuerySymbol).Append(" Adhoc Dump: ").Append(otherDmpName).AppendLine();

			sb.Append(Constants.BlackDiamond).Append(" Total String Count: ").Append(Utils.LargeNumberString(myStrStats.TotalCount))
				.Append(", Unique String Count: ").Append(Utils.LargeNumberString(myStrings.Length)).AppendLine();
			sb.Append(Constants.AdhocQuerySymbol).Append(" Total String Count: ").Append(Utils.LargeNumberString(otherTotalCount))
				.Append(", Unique String Count: ").Append(otherCnt).AppendLine();

			sb.Append(Constants.BlackDiamond).Append(" Total Size: ").Append(Utils.LargeNumberString(myStrStats.TotalSize))
				.Append(" Unique Total Size: ").Append(Utils.LargeNumberString(myStrStats.TotalUniqueSize)).AppendLine();
			sb.Append(Constants.AdhocQuerySymbol).Append(" Total Size: ").Append(Utils.LargeNumberString(otherTotalSize))
				.Append(" Unique Total Size: ").Append(Utils.LargeNumberString(otherTotalUniqueSize)).AppendLine();

			return new ListingInfo(null, listing, colInfos, sb.ToString());
		}

		private void GetStringsDiffLine(string str, int myCount, int otCount, uint size, List<string> data)
		{

			var myCountStr = Utils.LargeNumberString(myCount);
			var otCntStr = Utils.LargeNumberString(otCount);
			var cntDiffStr = Utils.LargeNumberString(myCount-otCount);
			var myTotSizeStr = Utils.LargeNumberString((ulong)myCount * (ulong)size);
			var otTotSizeStr = Utils.LargeNumberString((ulong)otCount * (ulong)size);
			var mySizeStr = Utils.LargeNumberString(size);
			data.Add(myCountStr);
			data.Add(otCntStr);
			data.Add(cntDiffStr);
			data.Add(myTotSizeStr);
			data.Add(otTotSizeStr);
			data.Add(mySizeStr);
			data.Add(str);
		}

		public SortedDictionary<string, quadruple<int, ulong, ulong, ulong>> GetTypeSizesInfo(string path, out string error)
		{
			try
			{
				var dump = ClrtDump.OpenDump(path, out error);
				if (dump == null) return null;
				return ClrtDump.GetTypeSizesInfo(dump.Runtime.GetHeap(), out error);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				Dump?.Dispose();
			}
		}

		public ListingInfo GetAllTypesSizesInfo(out string error)
		{
			error = null;
			try
			{
				var runtime = Dump.Runtime;
				runtime.Flush();
				var heap = runtime.GetHeap();
				return ClrtDump.GetAllTypesSizesInfo(heap, out error);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public ListingInfo GetWeakReferenceInfo(out string error)
		{
			error = null;
			try
			{

				int totalCount;
				KeyValuePair<int, ulong[]>[] weakReferenceAddresses = GetTypeWithPrefixAddresses("System.WeakReference", false, out totalCount);
				KeyValuePair<int, triple<ulong, ulong, string>[]>[] results = new KeyValuePair<int, triple<ulong, ulong, string>[]>[weakReferenceAddresses.Length];

				var heap = Dump.GetFreshHeap();
				for (int i = 0, icnt = weakReferenceAddresses.Length; i < icnt; ++i)
				{
					var addresses = weakReferenceAddresses[i].Value;
					ClrType weakReferenceType = heap.GetObjectType(addresses[0]); // System.WeakReference or System.WeakReference<T>
					ClrInstanceField m_handleField = weakReferenceType.Fields[0];
					object m_handleValue = m_handleField.GetValue(addresses[0], false, false);
					ClrType m_handleType = m_handleField.Type; //  System.IntPtr
					ClrInstanceField m_valueField = m_handleType.Fields[0];
					ulong m_valueValue = (ulong)m_valueField.GetValue((ulong)(long)m_handleValue, true, false);
					ClrType eeferencedType = heap.GetObjectType(m_valueValue); // type this WeakReference points to
					var result = DmpNdxQueries.SpecializedQueries.getWeakReferenceInfos(heap, addresses, m_handleField, m_valueField);
					results[i] = new KeyValuePair<int, triple<ulong, ulong, string>[]>(weakReferenceAddresses[i].Key,result.Item2);
				}

				HashSet<ulong> objects = new HashSet<ulong>();
				var objTypes = new SortedDictionary<string, int>(StringComparer.Ordinal);
				var objDups = new SortedDictionary<ulong, KeyValuePair<string,int>>();

				if (weakReferenceAddresses.Length == 1) // only one type of WeakReference
				{
					var typeName = GetTypeName(weakReferenceAddresses[0].Key);
					int recCount = results[0].Value.Length;
					var dataAry = new string[recCount * 3];
					var infoAry = new listing<string>[recCount];
					int off = 0;
					for (int i = 0; i < recCount; ++i)
					{
						var rec = results[0].Value[i];
						infoAry[i] = new listing<string>(dataAry, off, 3);
						dataAry[off++] = Utils.AddressString(rec.First);
						dataAry[off++] = Utils.AddressString(rec.Second);
						dataAry[off++] = rec.Third;

						// get some stats
						objects.Add(rec.Second);
						KeyValuePair<string, int> kv;
						if (objDups.TryGetValue(rec.Second, out kv))
							objDups[rec.Second] = new KeyValuePair<string, int>(kv.Key,kv.Value+1);
						else
							objDups.Add(rec.Second, new KeyValuePair<string, int>(rec.Third,1));
						int objCnt;
						if (objTypes.TryGetValue(rec.Third, out objCnt))
							objTypes[rec.Third] = objCnt + 1;
						else
							objTypes.Add(rec.Third,1);
					}

					ColumnInfo[] colInfos = new[]
					{
						new ColumnInfo("WeakReference Address", ReportFile.ColumnType.UInt64,150,1,true),
						new ColumnInfo("Object Address", ReportFile.ColumnType.UInt64,150,2,true),
						new ColumnInfo("Object Type", ReportFile.ColumnType.UInt64,400,3,true),
					};

					Array.Sort(infoAry, ReportFile.GetComparer(colInfos[0]));

					var objCountAry = Utils.GetOrderedByValueDesc(objTypes);
					var objDupCountAry = Utils.GetOrderedByValueDesc(objDups);

					StringBuilder sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
					sb.AppendLine("WeakReference type: " + typeName)
						.AppendLine("WeakReference Count: " + Utils.CountString(recCount))
						.AppendLine("Pointed instances Count: " + Utils.CountString(objects.Count));
					sb.AppendLine("Types top 50");
					for (int i = 0, icnt = Math.Min(50, objCountAry.Length); i < icnt; ++i)
					{
						sb.Append(Utils.CountStringHeader(objCountAry[i].Value));
						sb.AppendLine(objCountAry[i].Key);
					}

					sb.AppendLine("Instances duplicates, top 50");
					for (int i = 0, icnt = Math.Min(50, objDupCountAry.Length); i < icnt; ++i)
					{
						sb.Append(Utils.AddressStringHeader(objDupCountAry[i].Key));
						sb.Append(Utils.CountStringHeader(objDupCountAry[i].Value.Value));
						sb.AppendLine(objDupCountAry[i].Value.Key);
					}

					string descr = StringBuilderCache.GetStringAndRelease(sb);

					return new ListingInfo(null, infoAry, colInfos, descr);
				}
				else
				{
					var dataAry = new string[totalCount * 4];
					var infoAry = new listing<string>[totalCount];
					for (int i = 0, icnt = weakReferenceAddresses.Length; i < icnt; ++i)
					{
						var typeName = GetTypeName(weakReferenceAddresses[i].Key);
						int recCount = results[i].Value.Length;
						int off = 0;
						for (int j = 0; j < recCount; ++j)
						{
							var rec = results[i].Value[j];
							infoAry[i] = new listing<string>(dataAry, off, 4);
							dataAry[off++] = Utils.AddressString(rec.First);
							dataAry[off++] = typeName;
							dataAry[off++] = Utils.AddressString(rec.Second);
							dataAry[off++] = rec.Third;

							// get some stats
							objects.Add(rec.Second);
							KeyValuePair<string, int> kv;
							if (objDups.TryGetValue(rec.Second, out kv))
								objDups[rec.Second] = new KeyValuePair<string, int>(kv.Key, kv.Value + 1);
							else
								objDups.Add(rec.Second, new KeyValuePair<string, int>(rec.Third, 1));
							int objCnt;
							if (objTypes.TryGetValue(rec.Third, out objCnt))
								objTypes[rec.Third] = objCnt + 1;
							else
								objTypes.Add(rec.Third, 1);
						}
					}

					ColumnInfo[] colInfos = new[]
						{
						new ColumnInfo("WeakReference Address", ReportFile.ColumnType.UInt64,100,1,true),
						new ColumnInfo("WeakReference Type", ReportFile.ColumnType.UInt64,300,2,true),
						new ColumnInfo("Object Address", ReportFile.ColumnType.UInt64,100,3,true),
						new ColumnInfo("Object Type", ReportFile.ColumnType.UInt64,400,4,true),
						};

						Array.Sort(infoAry, ReportFile.GetComparer(colInfos[0]));

						string descr =  "WeakReference Count: " + Utils.CountString(totalCount) + Environment.NewLine
										+ "Pointed instances Count: " + Utils.CountString(objects.Count) + Environment.NewLine;
						return new ListingInfo(null, infoAry, colInfos, descr);

				}
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="error"></param>
		/// <returns>
		/// Tuple (total array count, list of 
		/// </returns>
		public Tuple<int, triple<string, int, pair<ulong, int>[]>[]> GetArrayCounts(bool skipFree, out string error)
		{
			error = null;
			try
			{
				var instances = _instances[_currentRuntime];
				var insttypes = _instTypes[_currentRuntime];
				var types = _clrtTypes[_currentRuntime];

				var runtime = Dump.Runtime;
				runtime.Flush();
				var heap = runtime.GetHeap();
				SortedDictionary<int, List<pair<ulong, int>>> dct = new SortedDictionary<int, List<pair<ulong, int>>>();

				for (int i = 0, icnt = instances.Length; i < icnt; ++i)
				{
					var typeId = insttypes[i];
					if (types.GetElementType(typeId) != ClrElementType.SZArray) continue;
					var addr = instances[i];
					var clrType = heap.GetObjectType(addr);
					if (skipFree && Utils.SameStrings(clrType.Name, "Free")) continue;
					var aryCnt = clrType.GetArrayLength(addr);
					List<pair<ulong, int>> lst;
					if (dct.TryGetValue(typeId, out lst))
					{
						lst.Add(new pair<ulong, int>(addr, aryCnt));
					}
					else
					{
						dct.Add(typeId, new List<pair<ulong, int>>(32) { new pair<ulong, int>(addr, aryCnt) });
					}
				}

				var result = new triple<string, int, pair<ulong, int>[]>[dct.Count];
				int ndx = 0;
				int totalAryCnt = 0;
				var names = types.Names;
				foreach (var kv in dct)
				{
					var typeName = names[kv.Key];
					var ary = kv.Value.ToArray();
					totalAryCnt += ary.Length;
					result[ndx++] = new triple<string, int, pair<ulong, int>[]>(typeName, kv.Key, ary);
				}
				return Tuple.Create(totalAryCnt, result);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public Tuple<ulong, ulong[], SortedDictionary<string, KeyValuePair<int, ulong>>, SortedDictionary<string, List<int>>, triple<int, ulong,string>[]> GetTypeSizeDetails(int typeId, out string error)
		{
			ulong[] addresses = GetTypeAddresses(typeId);
			return ClrtDump.GetTotalSizeDetail(Dump, addresses, out error);
		}

        #endregion Dump Interface

        #region Reports

        public void DebugFieldDependencyDump(string path, out string error)
        {
            error = null;
            StreamWriter sw = null;
            try
            {
                sw = new StreamWriter(path);

                var instances = _instances[_currentRuntime];
                var dependencies = _fieldDependencies[_currentRuntime];
                var parents = _fieldDependencies[_currentRuntime].ParentOffsets.Item1;
                var fieldOffs = _fieldDependencies[_currentRuntime].ParentOffsets.Item2;

                for (int instId = 0, instCnt = parents.Length-1; instId < instCnt; ++instId)
                {
                    var addr = parents[instId];
                    var info = dependencies.ReadFieldParents(fieldOffs[instId], fieldOffs[instId + 1], out error);
                    var typeName = GetTypeNameAtAddr(addr);
                    sw.WriteLine(Utils.SizeStringHeader(instId) + Utils.AddressStringHeader(addr) + typeName);
                    for (int i = 0, icnt = info.Length; i < icnt; ++i)
                    {
                        var finfo = info[i];
                        var ftypeName = GetTypeNameAtAddr(finfo.Key);
                        var fName = GetString(finfo.Value);
                        sw.Write("   ");
                        sw.WriteLine(Utils.AddressStringHeader(finfo.Key) + Utils.SmallNumberHeader(finfo.Value) + fName + Constants.HeavyGreekCrossPadded + ftypeName);
                    }
                }
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
            }
            finally
            {
                sw?.Close();
            }
        }

        public Tuple<listing<string>[], int> GetReportListing(InstanceNode rootNode, int nodeCnt)
		{
			int[] depthFirstOrder = new int[nodeCnt];
			Stack<InstanceNode> stack = new Stack<InstanceNode>(Math.Max(4, nodeCnt / 4));
			int treeNdx = 0;
			stack.Push(rootNode);
			Dictionary<InstanceNode, int> dct = new Dictionary<InstanceNode, int>();
			while (stack.Count > 0)
			{
				var node = stack.Pop();
				if (!dct.ContainsKey(node))
				{
					dct.Add(node, treeNdx);
					++treeNdx;
				}
				else
				{
					Debug.Assert(false, "Map.GetReportListing -- Duplicate nodes!");
				}
				for (int i = 0, icnt = node.Nodes.Length; i < icnt; ++i)
				{
					stack.Push(node.Nodes[i]);
				}
			}

			string[] data = new string[nodeCnt * 5];
			listing<string>[] items = new listing<string>[nodeCnt];
			Queue<InstanceNode> que = new Queue<InstanceNode>();
			que.Enqueue(rootNode);
			treeNdx = 0;
			int dataNdx = 0;
			int itemNdx = 0;
			int treeDepth = 0;
			while (que.Count > 0)
			{
				var node = que.Dequeue();
				int depthFirstIndex;
				if (!dct.TryGetValue(node, out depthFirstIndex))
				{
					depthFirstIndex = -1;
					Debug.Assert(false);
				}
				if (treeDepth < node.Level) treeDepth = node.Level;
				items[itemNdx++] = new listing<string>(data, dataNdx, 5);
				data[dataNdx++] = node.Level.ToString();
				data[dataNdx++] = depthFirstIndex.ToString();
				data[dataNdx++] = Utils.AddressString(node.Address);
				data[dataNdx++] = node.RootInfo?.ToString() ?? string.Empty;
				data[dataNdx++] = GetTypeNameFromInstanceId(node.InstanceId);
				for (int i = 0, icnt = node.Nodes.Length; i < icnt; ++i)
				{
					que.Enqueue(node.Nodes[i]);
				}
			}

			return new Tuple<listing<string>[], int>(items, treeDepth);
		}

		public ListingInfo GetFieldReferencesReport(ulong addr, int level = Int32.MaxValue)
		{
			string error;
			Tuple<InstanceNode, int, KeyValuePair<ulong, ulong>[]> result = GetFieldReferences(addr, out error, level);
			if (!string.IsNullOrEmpty(error) && error[0] != Constants.InformationSymbol)
			{
				return new ListingInfo(error);
			}
			var listingInfo = GetReportListing(result.Item1, result.Item2);
			var listing = listingInfo.Item1;
			var treeDepth = listingInfo.Item2;

			ColumnInfo[] colInfos = new[]
			{
					new ColumnInfo("BF Order", ReportFile.ColumnType.Int32,150,1,true),
					new ColumnInfo("DF Order", ReportFile.ColumnType.Int32,150,2,true),
					new ColumnInfo("Address", ReportFile.ColumnType.String,150,3,true),
					new ColumnInfo("Root Info", ReportFile.ColumnType.String,300,4,true),
					new ColumnInfo("Type", ReportFile.ColumnType.String,500,5,true),
			};

			var sb = new StringBuilder(256);
			sb.Append(ReportFile.DescrPrefix).Append("Parents of ").Append(listing[0].Fifth).AppendLine();
			sb.Append(ReportFile.DescrPrefix).Append("Instance at address: ").Append(listing[0].Third).AppendLine();
			sb.Append(ReportFile.DescrPrefix).Append("NOTE. The queried instance is displayed in the row where BF and DF orders are '0'").AppendLine();
			var backReferences = result.Item3;
			if (backReferences != null && backReferences.Length > 0)
			{
				sb.Append(ReportFile.DescrPrefix).Append("Back references found: ");
				for (int i = 0, icnt = backReferences.Length; i < icnt; ++i)
				{
					sb.Append(Utils.AddressString(backReferences[i].Key))
						.Append(Constants.HeavyRightArrowPadded)
						.Append(Utils.AddressString(backReferences[i].Value));
					if (i > 0 && (i % 10) == 0) sb.AppendLine();
					else sb.Append(" ");
				}
			}
			else
			{
				sb.Append(ReportFile.DescrPrefix).Append("Back references not found").AppendLine();
			}

			return new ListingInfo(null, listing, colInfos, sb.ToString());
		}

		#endregion Reports

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
				_clrtDump?.Dispose();
			}

			// Free any unmanaged objects here.
			//
			_disposed = true;
		}

		~Map()
		{
			Dispose(false);
		}

		#endregion Dispose
	}
}
