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

		public int InstanceCount; // free instances are not included
		public ulong InstanceSize; // sizes of free instances are not included
		public int FreeCount;
		public ulong FreeSize;


		private ClrtSegment() { }

		public ClrtSegment(ClrSegment seg, ulong firstAddr, ulong lastAddr, int firstNdx, int endNdx,
			int instCount, ulong instSize, int freeCount, ulong freeSize)
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
			InstanceCount = instCount;
			InstanceSize = instSize;
			FreeCount = freeCount;
			FreeSize = freeSize;
		}

		public int Count()
		{
			return EndIndex - FirstIndex;
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

		public int GetGenerationOld(ulong addr)
		{
			if (Ephemeral)
			{
				if (addr > Gen1Start) return 0;
				if (addr > Gen2Start) return 1;
				return 2;
			}
			else if (Large)
			{
				return 3;
			}
			else
			{
				return 2;
			}
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
	        var genTotal = new int[4];
	        var genSize = new ulong[4];
	        var freeTotal = new int[4];
	        var freeSize = new ulong[4];

	        for (int i = 0, icnt = segments.Length; i < icnt; ++i)
	        {
	            var seg = segments[i];
	            int genNdx = (int)seg.GetGeneration(seg.FirstAddress);
	            genTotal[genNdx] += seg.InstanceCount;
	            genSize[genNdx] += seg.InstanceSize;
	            freeTotal[genNdx] += seg.FreeCount;
	            freeSize[genNdx] += seg.FreeSize;
	        }
	        return Tuple.Create(genTotal, genSize, freeTotal, freeSize);
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

			writer.Write(InstanceCount);
			writer.Write(InstanceSize);
			writer.Write(FreeCount);
			writer.Write(FreeSize);
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

				InstanceCount = reader.ReadInt32(),
				InstanceSize = reader.ReadUInt64(),
				FreeCount = reader.ReadInt32(),
				FreeSize = reader.ReadUInt64(),
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
