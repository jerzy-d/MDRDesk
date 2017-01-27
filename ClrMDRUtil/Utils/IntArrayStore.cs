using System;
using System.Diagnostics;
using System.IO;
using ClrMDRIndex;

namespace ClrMDRUtil
{
	public class IntArrayStore
	{
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
				ary = new[] {val, Int32.MaxValue, Int32.MaxValue, Int32.MaxValue};
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

		public void InitSlots(int[] counts)
		{
			Debug.Assert(_array.Length == counts.Length);
			for (int i = 0, icnt = counts.Length; i < icnt; ++i)
			{
				int cnt = counts[i];
				if (cnt == 0) continue;
				var ary = new int[cnt];
				for (int j = 0; j < cnt; ++j)
					ary[j] = Int32.MaxValue;
				_array[i] = ary;
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

		public bool ListHaveNoDuplicates()
		{
			for (int i = 0, icnt = _array.Length; i < icnt; ++i)
			{
				if (_array[i] == null) continue;
				var ary = _array[i];
				var len = DataLen(ary);
				for (int j = 1; j < len; ++j)
				{
					if (ary[j - 1] == ary[j]) return false;
				}
			}
			return true;
		}
	}

	public class IntStore
	{
		private int[] _buffer;
		private int[] _offsets;
#if DEBUG
		public int[] Offsets => _offsets;
#endif
		private int[] _counts;
		private int _recordCount;

		public IntStore(RecordCounter counter)
		{
			int size = counter.Size;
			_recordCount = counter.RecordCount;
			int totCount = counter.TotalCount;
			_offsets = new int[size];
			_counts = counter.Counts;
			int count = 0;
			for (int i = 0, icnt = _counts.Length; i < icnt; ++i)
			{
				if (_counts[i] == 0) continue;
				_offsets[i] = count;
				count += _counts[i];
			}
			Debug.Assert(totCount == count);
			_buffer = new int[totCount];
		}

		public void AddItem(int ndx, int item)
		{
			_buffer[_offsets[ndx]] = item;
			_offsets[ndx] += 1;
		}

		public int[] GetOffsetsCopy()
		{
			int[] copy = new int[_offsets.Length];
			Buffer.BlockCopy(_offsets,0,copy,0,_offsets.Length*sizeof(int));
			return copy;
		}

		public void RestoreOffsets()
		{
			for (int i = 0, icnt = _counts.Length; i < icnt; ++i)
			{
				if (_counts[i] == 0) continue;
				_offsets[i] -= _counts[i];
			}
		}

		//public void AddItems(int ndx, int cnt, int[] items)
		//{
		//	Buffer.BlockCopy(items,0,_buffer,_offsets[ndx]*sizeof(int),cnt*sizeof(int));
		//	_offsets[ndx] += cnt;
		//}

		public bool Dump(string path, out string error)
		{
			FileWriter fw = null;
			error = null;
			try
			{
				fw = new FileWriter(path,1024*16);
				fw.Write(_recordCount);
				for (int i = 0, icnt = _offsets.Length; i < icnt; ++i)
				{
					if (_counts[i] == 0) continue;
					fw.WriteReferenceRecord(i, _counts[i], _offsets[i], _buffer);
				}

				return true;
			}
			catch (Exception ex)
			{
				error = ClrMDRIndex.Utils.GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				fw?.Dispose();
			}
		}

	}

}
