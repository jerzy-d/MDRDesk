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
using ClrMDRUtil.Utils;
using DmpNdxQueries;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.ICorDebug;

namespace ClrMDRIndex
{
	public sealed class DumpIndex : IDisposable
	{
		[Flags]
		public enum IndexType
		{
			InstanceRefrences = 1,

			All = 0xFFFFFFF,
		}

		#region fields/properties

		public static KvIntIntKeyCmp kvIntIntKeyCmp = new KvIntIntKeyCmp();

		const int MaxNodes = 10000;

		public IndexType Type { get; private set; }
		public static bool Is64Bit;

		private DumpFileMoniker _fileMoniker;
		public string AdhocFolder => _fileMoniker.OutputFolder;
		public string OutputFolder => AdhocFolder;
		public string IndexFolder => _fileMoniker.MapFolder;
		public string DumpPath => _fileMoniker.Path;
		public string DumpFileName => _fileMoniker.FileName;

		private string _dumpInfo;
		public string DumpInfo => _dumpInfo;

		private int _runtimeCount;
		private int _currentRuntimeIndex;

		private ulong[] _instances;
		public ulong[] Instances => _instances;
		private int[] _instanceTypes;
		public int[] InstanceTypes => _instanceTypes;
		private int[] _typeInstanceMap;
		private KeyValuePair<int, int>[] _typeInstanceOffsets;

		private string[] _typeNames;
		public string[] TypeNames;
		private KeyValuePair<string, int>[] _displayableTypeNames;
		public KeyValuePair<string, int>[] DisplayableTypeNames => _displayableTypeNames;
		private KeyValuePair<string, int>[] _reversedTypeNames;
		public KeyValuePair<string, int>[] ReversedTypeNames => _reversedTypeNames;
		private KeyValuePair<string, KeyValuePair<string, int>[]>[] _typeNamespaces;
		public KeyValuePair<string, KeyValuePair<string, int>[]>[] TypeNamespaces => _typeNamespaces;
		public int UsedTypeCount => _displayableTypeNames.Length;

		private InstanceReferences _instanceReferences;

		public WeakReference<StringStats> _stringStats;
		public WeakReference<uint[]> _sizes;
		public WeakReference<uint[]> _baseSizes;

		private ClrtSegment[] _segments; // segment infos, for instances generation histograms

		private ClrtDump _clrtDump;
		public ClrtDump Dump => _clrtDump;

		private string[] _stringIds; // ordered by string ids TODO JRD
		public string[] StringIds => _stringIds;

		private ClrtRoots _roots;

		#endregion fields/properties

		#region ctors/initialization

		private DumpIndex(string dumpOrIndexPath, int runtimeIndex, IndexType type = IndexType.All)
		{
			Type = IndexType.All;
			if (dumpOrIndexPath.EndsWith(".map", StringComparison.OrdinalIgnoreCase))
			{
				dumpOrIndexPath = dumpOrIndexPath.Substring(0, dumpOrIndexPath.Length - 4);
			}
			_fileMoniker = new DumpFileMoniker(dumpOrIndexPath);
			Is64Bit = Environment.Is64BitOperatingSystem;
			_currentRuntimeIndex = runtimeIndex;
		}

		public static DumpIndex OpenIndexInstanceReferences(Version version, string dumpPath, int runtimeNdx,
			out string error, IProgress<string> progress = null)
		{
			error = null;
			try
			{
				var index = new DumpIndex(dumpPath, runtimeNdx, IndexType.InstanceRefrences);
				index._dumpInfo = index.LoadDumpInfo(out error);
				return !index.LoadInstanceReferences(out error) ? null : index;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		private bool LoadInstanceReferences(out string error)
		{
			error = null;
			try
			{
				string path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapInstancesFilePostfix);
				_instances = Utils.ReadUlongArray(path, out error);
				if (error != null) return false;
				path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapInstanceTypesFilePostfix);
				_instanceTypes = Utils.ReadIntArray(path, out error);
				if (error != null) return false;

				// types/instances map
				//
				path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapTypeInstanceMapFilePostfix);
				_typeInstanceMap = Utils.ReadIntArray(path, out error);
				if (error != null) return false;
				path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapTypeInstanceOffsetsFilePostfix);
				_typeInstanceOffsets = Utils.ReadKvIntIntArray(path, out error);
				if (error != null) return false;

				// segments -- generation info
				//
				path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapSegmentInfoFilePostfix);
				_segments = ClrtSegment.ReadSegments(path, out error);

				// typenames
				//
				path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.TxtTypeNamesFilePostfix);
				_typeNames = Utils.GetStringListFromFile(path, out error);
				if (error != null) return false;
				{
					path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.TxtReversedTypeNamesFilePostfix);
					var dispNames = GetDisplayableTypeNames(path, _typeNames, _typeInstanceOffsets, out error);
					_displayableTypeNames = dispNames.Item1;
					_reversedTypeNames = dispNames.Item2;
					if (error != null) return false;
					Array.Sort(_reversedTypeNames, new KvStrIntKeyCmp());
					_typeNamespaces = GetTypeNamespaceOrdering(_reversedTypeNames);
				}

				_instanceReferences = new InstanceReferences(_fileMoniker.Path, _currentRuntimeIndex, _instances,
					_instanceTypes, _typeNames);
				_instanceReferences.Init(out error);
				if (error != null) return false;

				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}

		private Tuple<KeyValuePair<string, int>[], KeyValuePair<string, int>[]> GetDisplayableTypeNames(string path,
			string[] allTypeNames, KeyValuePair<int, int>[] knownTypes, out string error)
		{
			error = null;
			StreamReader rd = null;
			try
			{
				int knownTypesCount = knownTypes.Length - 1; // skip last dummy type
				KeyValuePair<string, int>[] revtTypeNames = new KeyValuePair<string, int>[knownTypesCount];
				KeyValuePair<string, int>[] dispTypeNames = new KeyValuePair<string, int>[knownTypesCount];
				rd = new StreamReader(path);
				var ln = rd.ReadLine();
				var count = Int32.Parse(ln);
				int curKnownTypeNdx = 0;
				int curKnownTypeId = knownTypes[0].Key;
				for (int i = 0; i < count && curKnownTypeNdx < knownTypesCount; ++i)
				{
					ln = rd.ReadLine();
					if (i < curKnownTypeId) continue;
					Debug.Assert(i == curKnownTypeId);
					revtTypeNames[curKnownTypeNdx] = new KeyValuePair<string, int>(ln, i);
					dispTypeNames[curKnownTypeNdx] = new KeyValuePair<string, int>(allTypeNames[i], i);

					curKnownTypeId = knownTypes[++curKnownTypeNdx].Key;
				}
				Debug.Assert(curKnownTypeNdx == knownTypesCount);
				return new Tuple<KeyValuePair<string, int>[], KeyValuePair<string, int>[]>(dispTypeNames, revtTypeNames);

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

		private KeyValuePair<string, KeyValuePair<string, int>[]>[] GetTypeNamespaceOrdering(
			KeyValuePair<string, int>[] reversedNames)
		{
			Debug.Assert(reversedNames != null && reversedNames.Length > 0);
			string[] splitter = new[] {Constants.NamespaceSepPadded};
			Dictionary<string, string> strCache = new Dictionary<string, string>(StringComparer.Ordinal);
			var resultDct = new SortedDictionary<string, List<KeyValuePair<string, int>>>(StringComparer.Ordinal);

			string[] items = reversedNames[0].Key.Split(splitter, StringSplitOptions.RemoveEmptyEntries);
			Debug.Assert(items.Length > 0);
			string name = Utils.GetCachedString(items[0], strCache);
			string prevNamesp = items.Length > 1 ? items[1] : string.Empty;
			var names = new List<KeyValuePair<string, int>>(128)
			{
				new KeyValuePair<string, int>(name, reversedNames[0].Value)
			};
			var namespInfos = new List<KeyValuePair<string, KeyValuePair<string, int>[]>>();
			for (int i = 1, icnt = reversedNames.Length; i < icnt; ++i)
			{
				var reversedNameInfo = reversedNames[i];
				items = reversedNameInfo.Key.Split(splitter, StringSplitOptions.RemoveEmptyEntries);
				Debug.Assert(items.Length > 0);
				name = Utils.GetCachedString(items[0], strCache);
				string namesp = items.Length > 1 ? Utils.GetCachedString(items[1], strCache) : string.Empty;
				List<KeyValuePair<string, int>> lst;
				if (resultDct.TryGetValue(namesp, out lst))
				{
					lst.Add(new KeyValuePair<string, int>(name, reversedNameInfo.Value));
					continue;
				}
				resultDct.Add(namesp,
					new List<KeyValuePair<string, int>>() {new KeyValuePair<string, int>(name, reversedNameInfo.Value)});
			}
			var result = new KeyValuePair<string, KeyValuePair<string, int>[]>[resultDct.Count];
			int ndx = 0;
			foreach (var kv in resultDct)
			{
				result[ndx++] = new KeyValuePair<string, KeyValuePair<string, int>[]>(
					kv.Key,
					kv.Value.ToArray()
				);
			}

			return result;
		}


		/// <summary>
		/// Read a text file with general dump information.
		/// </summary>
		/// <param name="error">Error message if io operation fails.</param>
		/// <returns>General dump information as string.</returns>
		private string LoadDumpInfo(out string error)
		{
			var path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.TxtIndexInfoFilePostfix);

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

		#endregion ctors/initialization

		#region utils

		private int GetInstanceIndex(ulong address)
		{
			return Utils.AddressSearch(_instances, address);
		}

		#endregion utils

		#region queries

		#region io

		public string GetFilePath(int r, string postfix)
		{
			return _fileMoniker.GetFilePath(r, postfix);
		}

		#endregion io

		#region heap

		public ClrHeap GetFreshHeap()
		{
			return Dump.GetFreshHeap();
		}

		#endregion heap

		#region types

		public string GetTypeName(ulong address)
		{
			var instanceIndex = GetInstanceIndex(address);
			return instanceIndex == Constants.InvalidIndex
				? Constants.UnknownTypeName
				: _typeNames[_instanceTypes[instanceIndex]];
		}

		public string GetTypeName(int id)
		{
			return (id >= 0 && id < _typeNames.Length) ? _typeNames[id] : Constants.UnknownTypeName;
		}

		public int GetTypeId(string typeName)
		{
			var id = Array.BinarySearch(_typeNames, typeName, StringComparer.Ordinal);
			return id < 0 ? Constants.InvalidIndex : id;
		}

		public ulong[] GetTypeInstances(int typeId)
		{
			var offNdx = Array.BinarySearch(_typeInstanceOffsets, new KeyValuePair<int, int>(typeId, 0), kvIntIntKeyCmp);
			if (offNdx < 0) return Utils.EmptyArray<ulong>.Value;
			var count = _typeInstanceOffsets[offNdx + 1].Value - _typeInstanceOffsets[offNdx].Value;
			ulong[] addresses = new ulong[count];
			int mapIndex = _typeInstanceOffsets[offNdx].Value;
			for (int i = 0; i < count; ++i)
			{
				addresses[i] = _instances[_typeInstanceMap[mapIndex++]];
			}
			Utils.SortAddresses(addresses);
			return addresses;
		}

		public int[] GetTypeInstanceIndices(int typeId)
		{
			var offNdx = Array.BinarySearch(_typeInstanceOffsets, new KeyValuePair<int, int>(typeId, 0), kvIntIntKeyCmp);
			if (offNdx < 0) return Utils.EmptyArray<int>.Value;
			var count = _typeInstanceOffsets[offNdx + 1].Value - _typeInstanceOffsets[offNdx].Value;
			int[] indices = new int[count];
			int mapIndex = _typeInstanceOffsets[offNdx].Value;
			for (int i = 0; i < count; ++i)
			{
				indices[i] = _typeInstanceMap[mapIndex++];
			}
			return indices;
		}

		public int GetTypeInstanceCount(int typeId)
		{
			var offNdx = Array.BinarySearch(_typeInstanceOffsets, new KeyValuePair<int, int>(typeId, 0), kvIntIntKeyCmp);
			if (offNdx < 0) return 0;
			var count = _typeInstanceOffsets[offNdx + 1].Value - _typeInstanceOffsets[offNdx].Value;
			return count;
		}

		public KeyValuePair<string, KeyValuePair<string, int>[]>[] GetNamespaceDisplay()
		{
			return _typeNamespaces;
		}

		#endregion types

		#region instance references

		public KeyValuePair<string, int>[] GetParents(ulong address, out string error)
		{
			var parents = _instanceReferences.GetFieldParents(address, out error);
			if (error != null) return Utils.EmptyArray<KeyValuePair<string, int>>.Value;
			SortedDictionary<string, int> dct = new SortedDictionary<string, int>();
			for (int i = 0, icnt = parents.Length; i < icnt; ++i)
			{
				var typeName = _typeNames[_instanceTypes[parents[i]]];
				int cnt;
				if (dct.TryGetValue(typeName, out cnt))
				{
					dct[typeName] = cnt + 1;
				}
				else
				{
					dct.Add(typeName, 1);
				}
			}
			return dct.ToArray();
		}

		public KeyValuePair<string, ulong>[] GetParentDetails(ulong address, out string error)
		{
			var parents = _instanceReferences.GetFieldParents(address, out error);
			if (error != null) return Utils.EmptyArray<KeyValuePair<string, ulong>>.Value;
			var result = new List<KeyValuePair<string, ulong>>(32);
			for (int i = 0, icnt = parents.Length; i < icnt; ++i)
			{
				var typeName = _typeNames[_instanceTypes[parents[i]]];
				var paddr = _instances[parents[i]];
				result.Add(new KeyValuePair<string, ulong>(typeName, paddr));
			}
			return result.ToArray();
		}


		/// <summary>
		/// Get parents of an instance, all or up to given maxlevel.
		/// </summary>
		/// <param name="address">Instance address</param>
		/// <param name="error">Error message if any.</param>
		/// <param name="maxLevel">Output tree height limit. </param>
		/// <returns>Tuple of: (IndexNode tree, node count, back reference list).</returns>
		public Tuple<IndexNode, int, int[]> GetParentReferences(ulong address, out string error,
			int maxLevel = Int32.MaxValue)
		{
			error = null;
			try
			{
				var addrNdx = GetInstanceIndex(address);
				if (addrNdx < 0)
				{
					error = "No object found at: " + Utils.AddressString(address);
					return null;
				}
				var rootNode = new IndexNode(addrNdx, 0); // we at level 0
				var uniqueSet = new HashSet<int>();
				var que = new Queue<IndexNode>(256);
				var backReferences = new List<int>();
				que.Enqueue(rootNode);
				uniqueSet.Add(addrNdx);
				int nodeCount = 0; // do not count root node
				while (que.Count > 0)
				{
					var curNode = que.Dequeue();
					++nodeCount;
					if (curNode.Level >= maxLevel || nodeCount > MaxNodes) continue;
					var parents = _instanceReferences.GetFieldParents(curNode.Index, out error);
					if (parents.Length > 0)
					{
						var nodes = new IndexNode[parents.Length];
						curNode.AddNodes(nodes);
						for (int i = 0, icnt = parents.Length; i < icnt; ++i)
						{
							var parentNdx = parents[i];
							var cnode = new IndexNode(parentNdx, curNode.Level + 1);
							nodes[i] = cnode;
							if (!uniqueSet.Add(parentNdx))
							{
								backReferences.Add(parentNdx);
								++nodeCount;
								continue;
							}
							que.Enqueue(cnode);
						}
					}
				}
				return Tuple.Create(rootNode, nodeCount, backReferences.ToArray());
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public KeyValuePair<IndexNode, int>[] GetParentReferences(int[] instIndices, out string error,
			int maxLevel = Int32.MaxValue)
		{
			error = null;
			try
			{
				var uniqueSet = new HashSet<int>();
				var que = new Queue<IndexNode>(256);
				List<KeyValuePair<IndexNode, int>> lst = new List<KeyValuePair<IndexNode, int>>(instIndices.Length);
				for (int inst = 0, instCnt = instIndices.Length; inst < instCnt; ++inst)
				{
					var instNdx = instIndices[inst];
					var rootNode = new IndexNode(instNdx, 0); // we at level 0
					uniqueSet.Clear();
					que.Clear();
					que.Enqueue(rootNode);
					uniqueSet.Add(instNdx);
					int nodeCount = 0; // do not count root node
					while (que.Count > 0)
					{
						var curNode = que.Dequeue();
						++nodeCount;
						if (curNode.Level >= maxLevel || nodeCount > MaxNodes) continue;
						var parents = _instanceReferences.GetFieldParents(curNode.Index, out error);
						if (parents.Length > 0)
						{
							var nodes = new IndexNode[parents.Length];
							curNode.AddNodes(nodes);
							for (int i = 0, icnt = parents.Length; i < icnt; ++i)
							{
								var parentNdx = parents[i];
								var cnode = new IndexNode(parentNdx, curNode.Level + 1);
								nodes[i] = cnode;
								if (!uniqueSet.Add(parentNdx))
								{
									++nodeCount;
									continue;
								}
								que.Enqueue(cnode);
							}
						}
					}
					lst.Add(new KeyValuePair<IndexNode, int>(rootNode, nodeCount));
				}

				return lst.ToArray();
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public Tuple<string, AncestorNode> GetParentTree(ulong address, int levelMax)
		{
			string error = null;
			try
			{
				var instanceId = Utils.AddressSearch(_instances, address);
				if (instanceId < 0)
				{
					error = "Cannot find instance at address: " + Utils.AddressString(address);
					return new Tuple<string, AncestorNode>(error, null);
				}
				var typeId = _instanceTypes[instanceId];
				var typeName = _typeNames[typeId];
				AncestorNode rootNode = new AncestorNode(0, typeId, typeName, new int[] {instanceId});

				return GetParentTree(rootNode, levelMax);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return new Tuple<string, AncestorNode>(error, null);
			}
		}

		public Tuple<string, AncestorNode> GetParentTree(int typeId, int levelMax)
		{
			string error = null;
			try
			{
				string typeName = GetTypeName(typeId);
				var rootNode = new AncestorNode(0, typeId, typeName, GetTypeInstanceIndices(typeId));
				return GetParentTree(rootNode, levelMax);

				//HashSet<int> set = new HashSet<int>();
				//Queue<AncestorNode> que = new Queue<AncestorNode>(Math.Min(1000,rootNode.Instances.Length));
				//var dct = new SortedDictionary<int, KeyValuePair<string, List<int>>>();
				//que.Enqueue(rootNode);
				//while (que.Count > 0)
				//{
				//    AncestorNode node = que.Dequeue();
				//    dct.Clear();
				//    int currentNodeLevel = node.Level + 1;
				//    if (currentNodeLevel >= levelMax) continue;
				//    var instances = node.Instances;
				//    for (int i = 0, icnt = instances.Length; i < icnt; ++i)
				//    {
				//        var inst = instances[i];
				//        if (!set.Add(inst)) continue;
				//        var ancestors = _instanceReferences.GetFieldParents(inst, out error);
				//        for (int j = 0, jcnt = ancestors.Length; j < jcnt; ++j)
				//        {
				//            var ancestor = ancestors[j];
				//            var typeid = _instanceTypes[ancestor];
				//            var typename = _typeNames[typeid];
				//            KeyValuePair<string, List<int>> kv;
				//            if (dct.TryGetValue(typeid, out kv))
				//            {
				//                kv.Value.Add(ancestor);
				//                continue;
				//            }
				//            dct.Add(typeid, new KeyValuePair<string, List<int>>(typename, new List<int>(16) { ancestor }));
				//        }
				//    }
				//    var nodes = new AncestorNode[dct.Count];
				//    int n = 0;
				//    foreach (var kv in dct)
				//    {
				//        nodes[n] = new AncestorNode(currentNodeLevel,kv.Key,kv.Value.Key,kv.Value.Value.ToArray());
				//        que.Enqueue(nodes[n]);
				//        ++n;
				//    }
				//    node.AddNodes(nodes);
				//}
				//return new Tuple<string, AncestorNode>(null,rootNode);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return new Tuple<string, AncestorNode>(error, null);
			}
		}

		public Tuple<string, AncestorNode> GetParentTree(AncestorNode rootNode, int levelMax)
		{
			string error = null;
			try
			{
				HashSet<int> set = new HashSet<int>();
				Queue<AncestorNode> que = new Queue<AncestorNode>(Math.Min(1000, rootNode.Instances.Length));
				var dct = new SortedDictionary<int, KeyValuePair<string, List<int>>>();
				que.Enqueue(rootNode);
				while (que.Count > 0)
				{
					AncestorNode node = que.Dequeue();
					dct.Clear();
					int currentNodeLevel = node.Level + 1;
					if (currentNodeLevel >= levelMax) continue;
					var instances = node.Instances;
					for (int i = 0, icnt = instances.Length; i < icnt; ++i)
					{
						var inst = instances[i];
						if (!set.Add(inst)) continue;
						var ancestors = _instanceReferences.GetFieldParents(inst, out error);
						for (int j = 0, jcnt = ancestors.Length; j < jcnt; ++j)
						{
							var ancestor = ancestors[j];
							var typeid = _instanceTypes[ancestor];
							var typename = _typeNames[typeid];
							KeyValuePair<string, List<int>> kv;
							if (dct.TryGetValue(typeid, out kv))
							{
								kv.Value.Add(ancestor);
								continue;
							}
							dct.Add(typeid, new KeyValuePair<string, List<int>>(typename, new List<int>(16) {ancestor}));
						}
					}
					var nodes = new AncestorNode[dct.Count];
					int n = 0;
					foreach (var kv in dct)
					{
						nodes[n] = new AncestorNode(currentNodeLevel, kv.Key, kv.Value.Key, kv.Value.Value.ToArray());
						que.Enqueue(nodes[n]);
						++n;
					}
					node.AddNodes(nodes);
				}
				return new Tuple<string, AncestorNode>(null, rootNode);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return new Tuple<string, AncestorNode>(error, null);
			}
		}

		//public Tuple<DependencyNode, int> GetAddressesDescendants(int typeId, ulong[] addresses, int maxLevel, out string error)
		//{
		//    error = null;
		//    int nodeCount = 0;
		//    try
		//    {
		//        string typeName = GetTypeName(typeId);
		//        DependencyNode root = new DependencyNode(0, typeId, typeName, string.Empty, addresses);
		//        ++nodeCount;
		//        HashSet<ulong> addrSet = new HashSet<ulong>();
		//        Queue<DependencyNode> que = new Queue<DependencyNode>(1024);

		//        DependencyNode[] nodes = DependencyNode.BuildBranches(Types, Instances, InstanceTypeIds, StringIds, FieldDependencies, root, addresses, out error);
		//        for (int i = 0, icnt = nodes.Length; i < icnt; ++i)
		//        {
		//            que.Enqueue(nodes[i]);
		//            ++nodeCount;
		//        }

		//        while (que.Count > 0)
		//        {
		//            var node = que.Dequeue();
		//            if (node.Level == maxLevel) continue;

		//            nodes = DependencyNode.BuildBranches(Types, Instances, InstanceTypeIds, StringIds, FieldDependencies, node, node.Addresses, out error);
		//            for (int i = 0, icnt = nodes.Length; i < icnt; ++i)
		//            {
		//                que.Enqueue(nodes[i]);
		//                ++nodeCount;
		//            }
		//        }

		//        return Tuple.Create(root, nodeCount);
		//    }
		//    catch (Exception ex)
		//    {
		//        error = Utils.GetExceptionErrorString(ex);
		//        return null;
		//    }
		//}

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

		public ListingInfo GetParentReferencesReport(ulong addr, int level = Int32.MaxValue)
		{
			string error;
			Tuple<IndexNode, int, int[]> result = GetParentReferences(addr, out error, level);
			if (!string.IsNullOrEmpty(error) && error[0] != Constants.InformationSymbol)
			{
				return new ListingInfo(error);
			}
			return OneInstanceParentsReport(result.Item1, result.Item2);
		}

		public ListingInfo GetParentReferencesReport(int typeId, int level = Int32.MaxValue)
		{
			string error;

			int[] typeInstances = GetTypeInstanceIndices(typeId);

			KeyValuePair<IndexNode, int>[] result = GetParentReferences(typeInstances, out error, level);
			if (!string.IsNullOrEmpty(error) && error[0] != Constants.InformationSymbol)
			{
				return new ListingInfo(error);
			}
			return MultiInstanceParentsReport(result);
		}

		public ListingInfo OneInstanceParentsReport(IndexNode rootNode, int nodeCnt)
		{
			const int ColumnCount = 4;
			string[] data = new string[nodeCnt*ColumnCount];
			listing<string>[] items = new listing<string>[nodeCnt];
			var que = new Queue<IndexNode>();
			que.Enqueue(rootNode);
			int dataNdx = 0;
			int itemNdx = 0;
			while (que.Count > 0)
			{
				var node = que.Dequeue();
				int instNdx = node.Index;
				ulong address = _instances[instNdx];
				string typeName = _typeNames[_instanceTypes[instNdx]];
				string rootInfo = Utils.IsRooted(address) ? string.Empty : "not rooted";

				items[itemNdx++] = new listing<string>(data, dataNdx, ColumnCount);
				data[dataNdx++] = node.Level.ToString();
				data[dataNdx++] = Utils.AddressString(address);
				data[dataNdx++] = rootInfo;
				data[dataNdx++] = typeName;
				for (int i = 0, icnt = node.Nodes.Length; i < icnt; ++i)
				{
					que.Enqueue(node.Nodes[i]);
				}
			}

			ColumnInfo[] colInfos = new[]
			{
				new ColumnInfo("Tree Level", ReportFile.ColumnType.Int32, 150, 1, true),
				new ColumnInfo("Address", ReportFile.ColumnType.String, 150, 2, true),
				new ColumnInfo("Root Info", ReportFile.ColumnType.String, 300, 3, true),
				new ColumnInfo("Type", ReportFile.ColumnType.String, 700, 4, true),
			};

			var sb = new StringBuilder(256);
			sb.Append(ReportFile.DescrPrefix).Append("Parents of ").Append(items[0].Forth).AppendLine();
			sb.Append(ReportFile.DescrPrefix).Append("Instance at address: ").Append(items[0].Second).AppendLine();
			sb.Append(ReportFile.DescrPrefix)
				.Append("Total reference count: ")
				.Append(Utils.LargeNumberString(nodeCnt))
				.AppendLine();
			sb.Append(ReportFile.DescrPrefix)
				.Append("NOTE. The queried instance is displayed in the row where Tree Level is '0'")
				.AppendLine();

			return new ListingInfo(null, items, colInfos, sb.ToString());
		}

		public ListingInfo MultiInstanceParentsReport(KeyValuePair<IndexNode, int>[] nodes)
		{
			const int ColumnCount = 4;
			int totalNodes = nodes.Sum(kv => kv.Value);
			string[] data = new string[totalNodes*ColumnCount];
			listing<string>[] items = new listing<string>[totalNodes];
			var que = new Queue<IndexNode>();
			int dataNdx = 0;
			int itemNdx = 0;
			for (int i = 0, icnt = nodes.Length; i < icnt; ++i)
			{
				var rootNode = nodes[i].Key;
				que.Enqueue(rootNode);
				ulong rootAddr = _instances[rootNode.Index];
				while (que.Count > 0)
				{
					var node = que.Dequeue();
					int instNdx = node.Index;
					ulong address = _instances[instNdx];
					string typeName = _typeNames[_instanceTypes[instNdx]];
					string rootInfo = Utils.IsRooted(address) ? string.Empty : "not rooted";

					items[itemNdx++] = new listing<string>(data, dataNdx, ColumnCount);
					data[dataNdx++] = Utils.AddressString(rootAddr);
					data[dataNdx++] = Utils.AddressString(address);
					data[dataNdx++] = rootInfo;
					data[dataNdx++] = typeName;
					for (int j = 0, jcnt = node.Nodes.Length; j < jcnt; ++j)
					{
						que.Enqueue(node.Nodes[j]);
					}
				}
			}




			ColumnInfo[] colInfos = new[]
			{
				new ColumnInfo("Type Instances", ReportFile.ColumnType.Int32, 150, 1, true),
				new ColumnInfo("Parents", ReportFile.ColumnType.String, 150, 2, true),
				new ColumnInfo("Root Info", ReportFile.ColumnType.String, 300, 3, true),
				new ColumnInfo("Type", ReportFile.ColumnType.String, 700, 4, true),
			};

			var sb = new StringBuilder(256);
			sb.Append(ReportFile.DescrPrefix).Append("Parents of ").Append(items[0].Forth).AppendLine();
			sb.Append(ReportFile.DescrPrefix).Append("Instance at address: ").Append(items[0].Second).AppendLine();
			sb.Append(ReportFile.DescrPrefix)
				.Append("Total reference count: ")
				.Append(Utils.LargeNumberString(items.Length))
				.AppendLine();

			return new ListingInfo(null, items, colInfos, sb.ToString());
		}

		#endregion instance references



		#endregion queries

		#region roots

		public ClrtRoots GetRoots(out string error)
		{
			error = null;
			if (_roots == null)
			{
				try
				{
					_roots = ClrtRoots.LoadRoots(_currentRuntimeIndex,_fileMoniker,out error);
					if (error != null) return null;
				}
				catch (Exception ex)
				{
					error = Utils.GetExceptionErrorString(ex);
					return null;
				}
			}
			return _roots;
		}

		#endregion roots

		#region strings

		public string GetString(int id)
		{
			if (id < 0 || id >= _stringIds.Length) return Constants.Unknown;
			return _stringIds[id];
		}

		public bool AreStringDataFilesAvailable()
		{
			var strDatPath = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapDumpStringsInfoPostfix);
			return File.Exists(strDatPath);
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
		public ListingInfo GetStringStats(int minReferenceCount, out string error, bool includeGenerations = false)
		{
			error = null;
			try
			{
				if (AreStringDataFilesAvailable())
				{
					StringStats strStats = null;
					if (_stringStats == null || !_stringStats.TryGetTarget(out strStats))
					{
						strStats = StringStats.GetStringsInfoFromFiles(_currentRuntimeIndex, DumpPath, out error);
						if (strStats == null)
							return new ListingInfo(error);
						if (_stringStats == null)
							_stringStats = new WeakReference<StringStats>(strStats);
						else
							_stringStats.SetTarget(strStats);
					}
					ListingInfo data = null;
					if (includeGenerations)
						data = strStats.GetGridData(minReferenceCount, _segments, out error);
					else
						data = strStats.GetGridData(minReferenceCount, out error);

					return data ?? new ListingInfo(error);
				}

				var strTypeId = GetTypeId("System.String");
				Debug.Assert(strTypeId != Constants.InvalidIndex);
				ulong[] addresses = GetTypeInstances(strTypeId);
				var runtime = Dump.Runtime;
				runtime.Flush();
				var heap = runtime.GetHeap();


				var stats = ClrtDump.GetStringStats(heap, addresses, DumpPath, out error);
				if (stats == null) return null;
				ListingInfo lstData = null;
				if (includeGenerations)
					lstData = stats.GetGridData(minReferenceCount, _segments, out error);
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
				if (AreStringDataFilesAvailable())
				{
					StringStats strStats = null;
					if (_stringStats == null || !_stringStats.TryGetTarget(out strStats))
					{
						strStats = StringStats.GetStringsInfoFromFiles(_currentRuntimeIndex, DumpPath, out error);
						if (strStats == null)
							return null;
						if (_stringStats == null)
							_stringStats = new WeakReference<StringStats>(strStats);
						else
							_stringStats.SetTarget(strStats);
					}
					return strStats;
				}

				var strTypeId = GetTypeId("System.String");
				Debug.Assert(strTypeId != Constants.InvalidIndex);
				ulong[] addresses = GetTypeInstances(strTypeId);
				var runtime = Dump.Runtime;
				runtime.Flush();
				var heap = runtime.GetHeap();


				var stats = ClrtDump.GetStringStats(heap, addresses, DumpPath, out error);
				if (stats == null) return null;

				if (_stringStats == null)
					_stringStats = new WeakReference<StringStats>(stats);
				else
					_stringStats.SetTarget(stats);

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
				if (_stringStats == null || !_stringStats.TryGetTarget(out strStats))
				{
					strStats = StringStats.GetStringsInfoFromFiles(_currentRuntimeIndex, DumpPath, out error);
					if (strStats == null) return null;
					if (_stringStats == null)
						_stringStats = new WeakReference<StringStats>(strStats);
					else
						_stringStats.SetTarget(strStats);
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
				KeyValuePair<int, int[]>[] parentInfos = _instanceReferences.GetMultiFieldParents(addresses, errors);


				var dct = new SortedDictionary<string, List<KeyValuePair<ulong, ulong>>>(StringComparer.Ordinal);
				int addrCount = 0;
				for (int i = 0, icnt = parentInfos.Length; i < icnt; ++i)
				{
					var childAddr = _instances[parentInfos[i].Key];
					var parentIds = parentInfos[i].Value;
					for (int j = 0, jcnt = parentIds.Length; j < jcnt; ++j)
					{
						var parentId = parentIds[j];
						var parentAddr = _instances[parentId];
						var typeName = _typeNames[_instanceTypes[parentId]];
						List<KeyValuePair<ulong, ulong>> lst;
						if (dct.TryGetValue(typeName, out lst))
						{
							lst.Add(new KeyValuePair<ulong, ulong>(childAddr, parentAddr));
						}
						else
						{
							dct.Add(typeName, new List<KeyValuePair<ulong, ulong>>() { new KeyValuePair<ulong, ulong>(childAddr, parentAddr) });
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


		#endregion strings

		#region segments/generations/sizes

		public Tuple<string, long>[][] GetGenerationTotals()
		{
			Tuple<int[], ulong[], int[], ulong[]> histograms =
				ClrtSegment.GetTotalGenerationDistributions(_segments);

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
			return ClrtSegment.GetGenerationHistogram(_segments, addresses);
		}

		public int[] GetStringGcGenerationHistogram(string strContent, out string error)
		{
			error = null;
			var strStats = GetCurrentStringStats(out error);
			var addresses = strStats.GetStringAddresses(strContent, out error);
			if (error != null) return null;
			return ClrtSegment.GetGenerationHistogram(_segments, addresses);
		}

		public int[] GetTypeGcGenerationHistogram(int typeId)
		{
			ulong[] addresses = GetTypeInstances(typeId);
			return ClrtSegment.GetGenerationHistogram(_segments, addresses);
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

		private uint[] GetSizeArray(bool baseSize, out string error)
		{
			error = null;
			try
			{
				uint[] sizes = null;
				if (baseSize)
				{
					if (_baseSizes == null || !_baseSizes.TryGetTarget(out sizes))
					{
						sizes = Utils.ReadUintArray(_fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapInstanceBaseSizesFilePostfix),
							out error);
						if (sizes == null) return null;
						if (_baseSizes == null)
							_baseSizes = new WeakReference<uint[]>(sizes);
						else
							_baseSizes.SetTarget(sizes);
					}
				}
				else
				{
					if (_sizes == null || !_sizes.TryGetTarget(out sizes))
					{
						sizes = Utils.ReadUintArray(_fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapInstanceSizesFilePostfix),
							out error);
						if (sizes == null) return null;
						if (_sizes == null)
							_sizes = new WeakReference<uint[]>(sizes);
						else
							_sizes.SetTarget(sizes);
					}
				}
				return sizes;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public Tuple<ulong, KeyValuePair<uint, ulong>[]> GetTypeTotalSizes(int typeId, bool baseSizes, out string error)
		{
			error = null;
			try
			{
				uint[] sizes = GetSizeArray(baseSizes, out error);
				int[] instIndices = GetTypeInstanceIndices(typeId);
				ulong totalSize = 0UL;
				var sizeInfos = new KeyValuePair<uint, ulong>[instIndices.Length];
				for (int i = 0, icnt = instIndices.Length; i < icnt; ++i)
				{
					var ndx = instIndices[i];
					var sz = sizes[ndx];
					totalSize += sz;
					sizeInfos[i] = new KeyValuePair<uint, ulong>(sz, _instances[ndx]);
				}
				return new Tuple<ulong, KeyValuePair<uint, ulong>[]>(totalSize, sizeInfos);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		public Tuple<ulong, KeyValuePair<uint, ulong>[]> GetTypeBaseSizes(int typeId, out string error)
		{
			error = null;
			try
			{
				uint[] sizes = null;
				if (_baseSizes == null || !_baseSizes.TryGetTarget(out sizes))
				{
					sizes = Utils.ReadUintArray(_fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapInstanceBaseSizesFilePostfix),
						out error);
					if (sizes == null) return null;
					if (_baseSizes == null)
						_baseSizes = new WeakReference<uint[]>(sizes);
					else
						_baseSizes.SetTarget(sizes);
				}

				int[] instIndices = GetTypeInstanceIndices(typeId);
				ulong totalSize = 0UL;
				var sizeInfos = new KeyValuePair<uint, ulong>[instIndices.Length];
				for (int i = 0, icnt = instIndices.Length; i < icnt; ++i)
				{
					var ndx = instIndices[i];
					var sz = sizes[ndx];
					totalSize += sz;
					sizeInfos[i] = new KeyValuePair<uint, ulong>(sz, _instances[ndx]);
				}
				return new Tuple<ulong, KeyValuePair<uint, ulong>[]>(totalSize, sizeInfos);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
		}

		#endregion segments/generations/sizes

		#region dispose

		volatile
		bool _disposed = false;

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected void Dispose(bool disposing)
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

		~DumpIndex()
		{
			Dispose(false);
		}

		#endregion dispose

	}

	public class KvIntIntKeyCmp : IComparer<KeyValuePair<int, int>>
	{
		public int Compare(KeyValuePair<int, int> a, KeyValuePair<int, int> b)
		{
			return a.Key < b.Key ? -1 : (a.Key > b.Key ? 1 : 0);
		}
	}

	public class KvStrIntKeyCmp : IComparer<KeyValuePair<string, int>>
	{
		public int Compare(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
		{
			return string.Compare(a.Key, b.Key, StringComparison.Ordinal);
		}
	}
}
