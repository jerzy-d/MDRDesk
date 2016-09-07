using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	public class ThreadBlockMap
	{
		private Digraph _graph;
		private ClrtBlkObject[] _blockingObjects;
		private ClrtThread[] _threads;
		private int _threadNdxStart;
		private int[] _cycle;
		private KeyValuePair<int, int[]>[] _threadWaitingLists; 

		public ThreadBlockMap(ClrtBlkObject[] blks, ClrtThread[] thrds)
		{
			_blockingObjects = blks;
			_threads = thrds;
			_threadNdxStart = _blockingObjects.Length;
		}

		public bool CreateGrapByBlkObjects(out string error)
		{
			error = null;
			try
			{
				_graph = new Digraph(_blockingObjects.Length + _threads.Length);

				for (int i = 0, icnt = _blockingObjects.Length; i < icnt; ++i)
				{
					ClrtBlkObject blk = _blockingObjects[i];
					if (blk.Owner != Constants.InvalidIndex) _graph.AddDistinctEdge(i,blk.Owner+_threadNdxStart);
					for (int j = 0, jcnt = blk.Owners.Length; j < jcnt; ++j)
					{
						_graph.AddDistinctEdge(i, blk.Owners[j] + _threadNdxStart);
					}
					for (int j = 0, jcnt = blk.Waiters.Length; j < jcnt; ++j)
					{
						_graph.AddDistinctEdge(blk.Waiters[j] + _threadNdxStart,i);
					}


				}

				List<KeyValuePair<int,int[]>> waitList = new List<KeyValuePair<int, int[]>>(_threads.Length/2);
				List<int> blkList = new List<int>();
				for (int i = 0, icnt = _threads.Length; i < icnt; ++i)
				{
					ClrtThread thrd = _threads[i];
					if (thrd.BlockingObjects.Length < 1) continue;
					blkList.Clear();
					for (int j = 0, jcnt = thrd.BlockingObjects.Length; j < jcnt; ++j)
					{
						blkList.Add(thrd.BlockingObjects[j]);
					}
					waitList.Add(new KeyValuePair<int, int[]>(i,blkList.ToArray()));
				}

				_threadWaitingLists = waitList.Count > 0 ? waitList.ToArray() : Utils.EmptyArray<KeyValuePair<int, int[]>>.Value;

				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}


		public bool HasCycle(out string error)
		{
			error = null;
			try
			{
				DirectedCycle dcycle = new DirectedCycle(_graph);
				if (dcycle.HasCycle())
				{
					var threadNdxStart = _blockingObjects.Length;
					_cycle = dcycle.GetCycle();
					return true;
				}
				_cycle = Utils.EmptyArray<int>.Value;
				return false;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}

		}

	}
}
