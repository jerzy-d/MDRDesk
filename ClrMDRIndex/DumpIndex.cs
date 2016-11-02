using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
		public string DumpName => _fileMoniker.FileNameNoExt;

		private int _runtimeCount;
		private int _currentRuntimeIndex;

		private ulong[] _instances;
		public ulong[] Instances => _instances;
		private int[] _instanceTypes;
		public int[] InstanceTypes => _instanceTypes;
		private string[] _typeNames;
		public string[] TypeNames;
		private int[] _typeInstanceMap;
		private KeyValuePair<int, int>[] _typeInstanceOffsets;

		private InstanceReferences _instanceReferences;


		private ClrtDump _clrtDump;
		public ClrtDump Dump => _clrtDump;
		
		#endregion fields/properties

		#region ctors/initialization

		private DumpIndex(string dumpOrIndexPath, int runtimeIndex, IndexType type = IndexType.All)
		{
			Type = IndexType.All;
			if (dumpOrIndexPath.EndsWith(".map", StringComparison.OrdinalIgnoreCase))
			{
				dumpOrIndexPath = dumpOrIndexPath.Substring(0, dumpOrIndexPath.Length - 3) + "dmp";
			}
			_fileMoniker = new DumpFileMoniker(dumpOrIndexPath);
			Is64Bit = Environment.Is64BitOperatingSystem;
			_currentRuntimeIndex = runtimeIndex;
		}

		public static DumpIndex OpenIndexInstanceReferences(Version version, string dumpPath, int runtimeNdx, out string error, IProgress<string> progress = null)
		{
			error = null;
			try
			{
				var index = new DumpIndex(dumpPath, runtimeNdx,IndexType.InstanceRefrences);
				
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
				path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.TxtTypeNamesFilePostfix);
				_typeNames = Utils.GetStringListFromFile(path, out error);
				if (error != null) return false;

				path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapTypeInstanceMapFilePostfix);
				_typeInstanceMap = Utils.ReadIntArray(path, out error);
				if (error != null) return false;
				path = _fileMoniker.GetFilePath(_currentRuntimeIndex, Constants.MapTypeInstanceOffsetsFilePostfix);
				_typeInstanceOffsets = Utils.ReadKvIntIntArray(path,out error);
				if (error != null) return false;

				_instanceReferences = new InstanceReferences(_fileMoniker.Path,_currentRuntimeIndex,_instances,_instanceTypes,_typeNames);
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

		#endregion ctors/initialization

		#region utils

		private int GetInstanceIndex(ulong address)
		{
			return Utils.AddressSearch(_instances, address);
		}

		#endregion utils

		#region queries


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
			var count = _typeInstanceOffsets[offNdx+1].Value - _typeInstanceOffsets[offNdx].Value;
			ulong[] addresses = new ulong[count];
			int mapIndex = _typeInstanceOffsets[offNdx].Value;
			for (int i = 0; i < count; ++i)
			{
				addresses[i] = _instances[_typeInstanceMap[mapIndex++]];
			}
			return addresses;
		}

		#endregion types

		#region instance references

		public KeyValuePair<string, int>[] GetParents(ulong address, out string error)
		{
			var parents = _instanceReferences.GetFieldParents(address, out error);
			if (error != null) return Utils.EmptyArray<KeyValuePair<string, int>>.Value;
			SortedDictionary<string,int> dct = new SortedDictionary<string, int>();
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
					dct.Add(typeName,1);
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
				result.Add(new KeyValuePair<string, ulong>(typeName,paddr));
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
		public Tuple<IndexNode, int, int[]> GetFieldReferences(ulong address, out string error, int maxLevel = Int32.MaxValue)
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


		#endregion instance references

		#endregion queries

		#region Dispose

		volatile bool _disposed = false;

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

		#endregion Dispose

	}

	public class KvIntIntKeyCmp : IComparer<KeyValuePair<int, int>>
	{
		public int Compare(KeyValuePair<int, int> a, KeyValuePair<int, int> b)
		{
			return a.Key < b.Key ? -1 : (a.Key > b.Key ? 1 : 0);
		}
	}
}
