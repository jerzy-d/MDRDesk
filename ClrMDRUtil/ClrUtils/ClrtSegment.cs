using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	public class ClrtSegment
	{
		public enum SegType
		{
			Uknown,
			Gen0,
			Gen1,
			Gen2,
			Large
		}

		public enum Generation
		{
			Gen0,
			Gen1,
			Gen2,
			Large,
			Uknown
		}

		public ulong CommittedEnd;
		public ulong ReservedEnd;
		public ulong Start;
		public ulong End;
		public ulong Gen0Start;

		public ulong Gen0Length;
		public ulong Gen1Start;
		public ulong Gen1Length;
		public ulong Gen2Start;
		public ulong Gen2Length;
		public ulong FirstObject;
		public int ProcessorAffinity;
		public bool Ephemeral;
		public bool Large;

		public ulong FirstAddress, LastAddress;
		public int FirstIndex, EndIndex;

		private int _gen0Count;
		public int Gen0Count => _gen0Count;
		private int _gen1Count;
		public int Gen1Count => _gen1Count;
		private int _gen2Count;
		public int Gen2Count => _gen2Count;

		private ulong _gen0Size;
		public ulong Gen0Size => _gen0Size;
		private ulong _gen1Size;
		public ulong Gen1Size => _gen1Size;
		private ulong _gen2Size;
		public ulong Gen2Size => _gen2Size;

		private int _gen0FreeCount;
		public int Gen0FreeCount => _gen0FreeCount;
		private int _gen1FreeCount;
		public int Gen1FreeCount => _gen1FreeCount;
		private int _gen2FreeCount;
		public int Gen2FreeCount => _gen2FreeCount;

		private ulong _gen0FreeSize;
		public ulong Gen0FreeSize => _gen0FreeSize;
		private ulong _gen1FreeSize;
		public ulong Gen1FreeSize => _gen1FreeSize;
		private ulong _gen2FreeSize;
		public ulong Gen2FreeSize => _gen2FreeSize;

		private ClrtSegment() { }

		public ClrtSegment(ClrSegment seg, ulong firstAddr, ulong lastAddr, int firstNdx, int endNdx)
		{
			CommittedEnd = seg.CommittedEnd;
			ReservedEnd = seg.ReservedEnd;
			Start = seg.Start;
			End = seg.End;
			Gen0Start = seg.Gen0Start;
			Gen0Length = seg.Gen0Length;
			Gen1Start = seg.Gen1Start;
			Gen1Length = seg.Gen1Length;
			Gen2Start = seg.Gen2Start;
			Gen2Length = seg.Gen2Length;
			FirstObject = seg.FirstObject;
			ProcessorAffinity = seg.ProcessorAffinity;
			Ephemeral = seg.IsEphemeral;
			Large = seg.IsLarge;
			FirstAddress = firstAddr;
			LastAddress = lastAddr;
			FirstIndex = firstNdx;
			EndIndex = endNdx;
		}

		public int Count()
		{
			return EndIndex - FirstIndex;
		}

		public static void SetGenerationStats(ClrSegment clrSeg, ulong addr, ulong size, int[] cnts, ulong[] sizes)
		{
			if (clrSeg.IsLarge)
			{
				cnts[0] += 1;
				sizes[0] += size;
			}
			else
			{
				var gen = clrSeg.GetGeneration(addr);
				if (gen >= 0)
				{
					cnts[gen] += 1;
					sizes[gen] += size;
				}
			}
		}

		public void SetGenerationStats(int[] cnts, ulong[] sizes, int[] freeCnts, ulong[] freeSizes)
		{
			_gen0Count = cnts[0];
			_gen1Count = cnts[1];
			_gen2Count = cnts[2];
			_gen0Size = sizes[0];
			_gen1Size = sizes[1];
			_gen2Size = sizes[2];

			_gen0FreeCount = freeCnts[0];
			_gen1FreeCount = freeCnts[1];
			_gen2FreeCount = freeCnts[2];
			_gen0FreeSize = freeSizes[0];
			_gen1FreeSize = freeSizes[1];
			_gen2FreeSize = freeSizes[2];
		}

		public void ToShortString(StringBuilder sb)
		{
			if (Ephemeral) sb.Append("Ephemeral    ");
			else if (Large) sb.Append("Large        ");
			else sb.Append("Generation 2 ");
			sb.Append(string.Format("  Range [0x{0:x14}, 0x{1:x14}] Size {2:#,###}", Start, CommittedEnd, CommittedEnd - Start + 1));
			sb.AppendLine();
			if (Ephemeral)
			{
				sb.Append(string.Format("  Gen0:  Range [0x{0:x14}, 0x{1:x14}] Size {2:#,###}", Gen0Start, Gen0Start + Gen0Length - 1, Gen0Length));
				sb.Append(string.Format("  Gen1:  Range [0x{0:x14}, 0x{1:x14}] Size {2:#,###}", Gen1Start, Gen1Start + Gen1Length - 1, Gen1Length));
				sb.Append(string.Format("  Gen2:  Range [0x{0:x14}, 0x{1:x14}] Size {2:#,###}", Gen2Start, Gen2Start + Gen2Length - 1, Gen2Length));
			}
			else if (Large)
			{
				sb.Append(string.Format("  LOH :  Range [0x{0:x14}, 0x{1:x14}] Size {2:#,###}", Start, CommittedEnd, CommittedEnd - Start + 1));
			}
			else
			{
				sb.Append(string.Format("  Gen2:  Range [0x{0:x14}, 0x{1:x14}] Size {2:#,###}", Gen2Start, Gen2Start + Gen2Length - 1, Gen2Length));
			}
		}

		public bool IsInSegment(ulong addr)
		{
			return (addr >= FirstAddress && addr <= LastAddress);
		}

		public Generation GetGeneration(ulong addr)
		{
			if (Large) return Generation.Large;

			if (Gen0Start <= addr && addr < (Gen0Start + Gen0Length))
			{
				return Generation.Gen0;
			}

			if (Gen1Start <= addr && addr < (Gen1Start + Gen1Length))
			{
				return Generation.Gen1;
			}

			if (Gen2Start <= addr && addr < (Gen2Start + Gen2Length))
			{
				return Generation.Gen2;
			}

			return Generation.Uknown;
		}

		public static int[] GetGenerationHistogram(ClrtSegment[] segments, IList<ulong> addresses)
		{
			int[] histogram = new int[5];
			int segLen = segments.Length;
			for (int i = 0, icnt = addresses.Count; i < icnt; ++i)
			{
				var addr = addresses[i];
				bool found = false;
				for (var j = 0; j < segLen; ++j)
				{
					if (segments[j].IsInSegment(addr))
					{
						found = true;
						var gen = segments[j].GetGeneration(addr);
						histogram[(int) gen] += 1;
						break;
					}
				}
				if (!found)
				{
					histogram[(int) Generation.Uknown] += 1;
				}
			}
			return histogram;
		}


		public static int FindSegment(ClrtSegment[] segments, ulong addr)
		{
			for (var i = 0; i < segments.Length; ++i)
			{
				if (segments[i].IsInSegment(addr)) return i;
			}
			return Constants.InvalidIndex;
		}


	    public static Tuple<int[],ulong[],int[],ulong[]> GetTotalGenerationDistributions(ClrtSegment[] segments)
	    {
	        var genTotalCount = new int[4];
	        var genTotalSize = new ulong[4];
	        var freeTotalGenCount = new int[4];
	        var freeTotalGenSize = new ulong[4];

	        for (int i = 0, icnt = segments.Length; i < icnt; ++i)
	        {
	            var seg = segments[i];
		        if (seg.Large)
		        {
			        genTotalCount[(int) Generation.Large] += seg.Gen0Count;
			        genTotalSize[(int) Generation.Large] += seg.Gen0Size;
			        freeTotalGenCount[(int) Generation.Large] += seg.Gen0FreeCount;
			        freeTotalGenSize[(int) Generation.Large] += seg.Gen0FreeSize;
					continue;
		        }
				genTotalCount[(int)Generation.Gen0] += seg.Gen0Count;
				genTotalSize[(int)Generation.Gen0] += seg.Gen0Size;
				freeTotalGenCount[(int)Generation.Gen0] += seg.Gen0FreeCount;
				freeTotalGenSize[(int)Generation.Gen0] += seg.Gen0FreeSize;

				genTotalCount[(int)Generation.Gen1] += seg.Gen1Count;
				genTotalSize[(int)Generation.Gen1] += seg.Gen1Size;
				freeTotalGenCount[(int)Generation.Gen1] += seg.Gen1FreeCount;
				freeTotalGenSize[(int)Generation.Gen1] += seg.Gen1FreeSize;

				genTotalCount[(int)Generation.Gen2] += seg.Gen2Count;
				genTotalSize[(int)Generation.Gen2] += seg.Gen2Size;
				freeTotalGenCount[(int)Generation.Gen2] += seg.Gen2FreeCount;
				freeTotalGenSize[(int)Generation.Gen2] += seg.Gen2FreeSize;
			}

	        return Tuple.Create(genTotalCount, genTotalSize, freeTotalGenCount, freeTotalGenSize);
	    }

        public static string GetGenerationHistogramSimpleString(int[] histogram)
		{
			Debug.Assert(histogram.Length == 5);
			StringBuilder sb = new StringBuilder(64);
			sb.Append(Generation.Gen0).Append(" [").Append(Utils.LargeNumberString(histogram[0])).Append("]  ");
			sb.Append(Generation.Gen1).Append(" [").Append(Utils.LargeNumberString(histogram[1])).Append("]  ");
			sb.Append(Generation.Gen2).Append(" [").Append(Utils.LargeNumberString(histogram[2])).Append("]  ");
			sb.Append("LOH").Append(" [").Append(Utils.LargeNumberString(histogram[3])).Append("]  ");
			if (histogram[4] > 0)
			{
				sb.Append(Generation.Uknown)
					.Append(" [")
					.Append(Utils.LargeNumberString(histogram[4])).Append("]");
			}
			return sb.ToString();
		}

		public static Tuple<string,long>[] GetGenerationHistogramTuples(int[] histogram)
		{
			Debug.Assert(histogram.Length == 5);
			int cnt = histogram[4] > 0 ? 5 : 4;
			Tuple<string, long>[] ary = new Tuple<string, long>[cnt];
			ary[0] = new Tuple<string, long>("Generation 0", histogram[0]);
			ary[1] = new Tuple<string, long>("Generation 1", histogram[1]);
			ary[2] = new Tuple<string, long>("Generation 2", histogram[2]);
			ary[3] = new Tuple<string, long>("Large Obj Heap", histogram[3]);
			if (cnt>4)
				ary[4] = new Tuple<string, long>("Uknown", histogram[4]);

			return ary;
		}

		private void Dump(BinaryWriter writer)
		{
			writer.Write(CommittedEnd);
			writer.Write(ReservedEnd);
			writer.Write(Start);
			writer.Write(End);
			writer.Write(Gen0Start);
			writer.Write(Gen0Length);
			writer.Write(Gen1Start);
			writer.Write(Gen1Length);
			writer.Write(Gen2Start);
			writer.Write(Gen2Length);
			writer.Write(FirstObject);
			writer.Write(FirstAddress);
			writer.Write(LastAddress);

			writer.Write(ProcessorAffinity);
			writer.Write(FirstIndex);
			writer.Write(EndIndex);
			writer.Write(Ephemeral);
			writer.Write(Large);

			writer.Write(_gen0Count);
			writer.Write(_gen1Count);
			writer.Write(_gen2Count);

			writer.Write(_gen0Size);
			writer.Write(_gen1Size);
			writer.Write(_gen2Size);

			writer.Write(_gen0FreeCount);
			writer.Write(_gen1FreeCount);
			writer.Write(_gen2FreeCount);

			writer.Write(_gen0FreeSize);
			writer.Write(_gen1FreeSize);
			writer.Write(_gen2FreeSize);
		}

		private static ClrtSegment Read(BinaryReader reader)
		{

			var seg = new ClrtSegment
			{
				CommittedEnd = reader.ReadUInt64(),
				ReservedEnd = reader.ReadUInt64(),
				Start = reader.ReadUInt64(),
				End = reader.ReadUInt64(),
				Gen0Start = reader.ReadUInt64(),
				Gen0Length = reader.ReadUInt64(),
				Gen1Start = reader.ReadUInt64(),
				Gen1Length = reader.ReadUInt64(),
				Gen2Start = reader.ReadUInt64(),
				Gen2Length = reader.ReadUInt64(),
				FirstObject = reader.ReadUInt64(),
				FirstAddress = reader.ReadUInt64(),
				LastAddress = reader.ReadUInt64(),

				ProcessorAffinity = reader.ReadInt32(),
				FirstIndex = reader.ReadInt32(),
				EndIndex = reader.ReadInt32(),
				Ephemeral = reader.ReadBoolean(),
				Large = reader.ReadBoolean(),

				_gen0Count = reader.ReadInt32(),
				_gen1Count = reader.ReadInt32(),
				_gen2Count = reader.ReadInt32(),

				_gen0Size = reader.ReadUInt64(),
				_gen1Size = reader.ReadUInt64(),
				_gen2Size = reader.ReadUInt64(),

				_gen0FreeCount = reader.ReadInt32(),
				_gen1FreeCount = reader.ReadInt32(),
				_gen2FreeCount = reader.ReadInt32(),

				_gen0FreeSize = reader.ReadUInt64(),
				_gen1FreeSize = reader.ReadUInt64(),
				_gen2FreeSize = reader.ReadUInt64(),

			};
			return seg;
		}

		public static bool DumpSegments(string path, ClrtSegment[] segments, out string error)
		{
			error = null;
			BinaryWriter bw = null;
			try
			{
				bw = new BinaryWriter(File.Open(path, FileMode.Create));
				DumpSegments(segments, bw);
				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				bw?.Close();
			}
		}

		public static void DumpSegments(ClrtSegment[] segments, BinaryWriter writer)
		{
			var count = segments.Length;
			writer.Write(count);
			for (var i = 0; i < count; ++i)
			{
				segments[i].Dump(writer);
			}
		}

		public static ClrtSegment[] ReadSegments(string path, out string error)
		{
			error = null;
			BinaryReader br = null;
			if (!File.Exists(path))
			{
				error = "Segment file not available: " + path;
				return Utils.EmptyArray<ClrtSegment>.Value;
			}
			try
			{
				br = new BinaryReader(File.Open(path, FileMode.Open));
				return ReadSegments(br);
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

		public static string GetGenerationsString(string[] titles, Tuple<string, long>[][] histogr)
		{
			StringBuilder sb = StringBuilderCache.Acquire(256);
			Debug.Assert(titles.Length == histogr.Length);
			string[] genPrefix = new[] {" 0[", " 1[", " 2[", " L["};
			for (int i = 0, icnt = titles.Length; i < icnt; ++i)
			{
				sb.Append(titles[i]);
				for (int j = 0, jcnt = histogr[i].Length; j < jcnt; ++j)
				{
					sb.Append(" ").Append(genPrefix[j]).Append(Utils.LargeNumberString(histogr[i][j].Item2)).Append("]");
				}
				sb.Append("  ");
			}
			return StringBuilderCache.GetStringAndRelease(sb);
		}

		public static ClrtSegment[] ReadSegments(BinaryReader reader)
		{
			var count = reader.ReadInt32();
			ClrtSegment[] segments = new ClrtSegment[count];
			for (var i = 0; i < count; ++i)
			{
				segments[i] = Read(reader);
			}
			return segments;
		}
	}
}
