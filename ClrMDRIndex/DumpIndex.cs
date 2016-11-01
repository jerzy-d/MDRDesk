using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class DumpIndex : IDisposable
	{
		[Flags]
		public enum IndexType
		{
			InstanceRefrences = 1,

			All = 0xFFFFFFF,
		}

		#region fields/properties

		const int MaxNodes = 10000;

		public IndexType Type { get; private set; }

		private DumpFileMoniker _fileMoniker;
		public static bool Is64Bit;



		private int _runtimeCount;
		private int _currentRuntimeIndex;

		private ulong[] _instances;
		private int[] _instanceTypes;
		private string[] _typeNames;
		private InstanceReferences _instanceReferences;


		private ClrtDump _clrtDump;
		public ClrtDump Dump => _clrtDump;

		#endregion fields/properties

		#region ctors/initialization

		private DumpIndex(string dumpPath, int runtimeIndex, IndexType type = IndexType.All)
		{
			Type = IndexType.All;
			_fileMoniker = new DumpFileMoniker(dumpPath);
			Is64Bit = Environment.Is64BitOperatingSystem;
			_currentRuntimeIndex = runtimeIndex;
		}

		public static DumpIndex OpenIndexInstanceReferences(Version version, string dumpPath, int runtimeNdx, out string error,
			IProgress<string> progress = null)
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



		public string GetTypeName(ulong address)
		{
			var instanceIndex = GetInstanceIndex(address);
			return instanceIndex == Constants.InvalidIndex
				? Constants.UnknownTypeName
				: _typeNames[_instanceTypes[instanceIndex]];
		}

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

		public Tuple<InstanceNode, int, KeyValuePair<ulong, ulong>[]> GetFieldReferences(ulong address, out string error, int maxLevel = Int32.MaxValue)
		{

			error = null;
			
			if (error != null) return null;

			int level = 0;
			var instAddr = address;
			var rootNode = new InstanceNode(instAddr, 0); // we at level 0
			var uniqueAddrSet = new HashSet<ulong>();
			var que = new Queue<InstanceNode>(256);
			var nodeLst = new List<InstanceNode>(64);
			var errorLst = new List<string>();
			var backReferences = new List<KeyValuePair<ulong, ulong>>();
			var extraInfoQue = new BlockingCollection<InstanceNode>();

			//var extraTask =
			//	Task.Factory.StartNew(
			//		() =>
			//			GetInstanceNodeExtraInfo(new Tuple<ulong[], ClrtRoots, BlockingCollection<InstanceNode>>(instances, roots,
			//				extraInfoQue)));

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
				var parents = _instanceReferences.GetFieldParents(curNode.Address, out error);

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


		#endregion instance references

		#endregion queries

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

		~DumpIndex()
		{
			Dispose(false);
		}

		#endregion Dispose

	}
}
