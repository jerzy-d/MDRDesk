﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRUtil
{
	public class IntArrayStore
	{
		const int InitialSize = 4;

		private int[][] _array;

		public IntArrayStore(int count)
		{
			_array = new int[count][];
		}

		public int[] GetEntry(int ndx)
		{
			Debug.Assert(ndx >= 0 && ndx < _array.Length);
			return _array[ndx];
		}

		public void Add(int ndx, int val)
		{
			Debug.Assert(ndx >= 0 && val != Int32.MaxValue);
			int[] ary = _array[ndx];
			if (ary == null) // empty slot
			{
				ary = new int[] {val, Int32.MaxValue, Int32.MaxValue, Int32.MaxValue};
				_array[ndx] = ary;
				return;
			}
			int len = ary.Length;
			var pos = Array.BinarySearch(ary, val);
			if (pos >= 0) return; // no duplicates allowed
			pos = ~pos;
			if (ary[len - 1] == Int32.MaxValue) // have room
			{
				InsertAt(ary,pos,val);
				return;
			}
			// need to create new slot
			var newLen = IncSlotLength(len);
			var newAry = new int[newLen];
			Buffer.BlockCopy(ary,0,newAry,0,ary.Length*sizeof(int));
			for (int i = len, icnt = newAry.Length; i < icnt; ++i)
				newAry[i] = Int32.MaxValue;
			InsertAt(newAry, pos, val);
			_array[ndx] = newAry;
		}

		private void InsertAt(int[] ary, int pos, int val)
		{
			var temp = ary[pos];
			ary[pos] = val;
			for (int i = pos + 1,icnt =ary.Length; i < icnt && temp != Int32.MaxValue; ++i)
			{
				var t = ary[i];
				ary[i] = temp;
				temp = t;
			}
		}

		public void Add(int ndx, int[] data)
		{
			Debug.Assert(ndx >= 0 && ndx < _array.Length);
			_array[ndx] = data;
		}

		private int IncSlotLength(int len)
		{
			return len > 1024*4 ? len + 1024*4 : len*2;
		}

		private int DataLen(int[] ary)
		{
			for (int j = 0, jcnt = ary.Length; j < jcnt; ++j)
			{
				if (ary[j] == Int32.MaxValue) return j;
			}
			return ary.Length;
		}

		public bool Dump(string path, out string error)
		{
			error = null;
			BinaryWriter bw = null;
			try
			{
				bw = new BinaryWriter(File.Open(path, FileMode.Create));
				int count = 0;
				bw.Write(count);
				for (int i = 0, icnt = _array.Length; i < icnt; ++i)
				{
					if (_array[i] == null) continue;
					++count;
					bw.Write(i);
					var ary = _array[i];
					var len = DataLen(ary);
					bw.Write(len);
					for (int j = 0; j < len; ++j)
					{
						bw.Write(ary[j]);
					}
				}
				bw.Seek(0, SeekOrigin.Begin);
				bw.Write(count);
				return true;
			}
			catch (Exception ex)
			{
				error = ClrMDRIndex.Utils.GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				bw?.Close();
			}
		}
	}
}
