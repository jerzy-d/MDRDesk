using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	public class ElementTypeCount
	{
		public ClrElementType Type;
		public int Count;
		private List<int> _typeIds;

		public List<int> TypeIds => _typeIds;

		public ElementTypeCount(ClrElementType tp, int cnt)
		{
			Type = tp;
			Count = cnt;
			_typeIds = Utils.EmptyList<int>.Value;
		}

		public static ElementTypeCount[] CreateElemTypeArray(bool emptyListsOnly = false)
		{
			List<ElementTypeCount> lst = new List<ElementTypeCount>();
			for (var i = 0; i < Constants.ClrElementTypeMaxValue + 1; ++i)
			{
				lst.Add(new ElementTypeCount(ClrElementType.Unknown, 0));
			}
			var ary = lst.ToArray();
			foreach (ClrElementType elem in Enum.GetValues(typeof(ClrElementType)))
			{
				int ndx = (int)elem;
				ary[ndx].Type = elem;
				if (elem != ClrElementType.Unknown && !emptyListsOnly)
					ary[ndx]._typeIds = new List<int>(256);
			}
			return ary;
		}

		public static void IncElemCount(ElementTypeCount[] ary, ClrElementType tp)
		{
			int ndx = (int)tp;
			ary[ndx].Count += 1;
		}

		public static void IncElemCounts(ElementTypeCount[] ary, ClrElementType tp, int count)
		{
			int ndx = (int)tp;
			ary[ndx].Count += count;
		}

		public static void AddElemType(ElementTypeCount[] ary, ClrElementType tp, int typeId)
		{
			if (tp == ClrElementType.Unknown) return;
			int ndx = (int)tp;
			ary[ndx]._typeIds.Add(typeId);
		}

		public static bool Dump(string path, ElementTypeCount[] ary, string[] typeNames, out string error)
		{
			error = null;
			StreamWriter wr = null;
			try
			{
				wr = new StreamWriter(path);
				for (int i = 0, icnt = ary.Length; i < icnt; ++i)
				{
					if (ary[i].Type == ClrElementType.Unknown) continue;
					wr.WriteLine(ary[i].Type + "   instance count " + ary[i].Count + ", type count " + ary[i]._typeIds.Count);

					for (int j = 0, jcnt = ary[i]._typeIds.Count; j < jcnt; ++j)
					{
						var id = ary[i]._typeIds[j];
						wr.WriteLine("   [" + id + "] " + typeNames[id]);
					}
				}
				return true;
			}
			catch (Exception ex)
			{
				Utils.GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				wr?.Close();
			}
		}

		public static bool DumpMap(string path, ElementTypeCount[] ary, out string error)
		{
			error = null;
			BinaryWriter bw = null;
			try
			{
				bw = new BinaryWriter(File.Open(path, FileMode.Create));
				bw.Write(ary.Length);
				for (int i = 0, icnt = ary.Length; i < icnt; ++i)
				{
					bw.Write((int)ary[i].Type);
					bw.Write(ary[i].Count);
					bw.Write(ary[i]._typeIds.Count);
					ary[i].TypeIds.Sort();
					for (int j = 0, jcnt = ary[i]._typeIds.Count; j < jcnt; ++j)
					{
						bw.Write(ary[i]._typeIds[j]);
					}
				}
				bw.Close();
				bw = null;
				return true;
			}
			catch (Exception ex)
			{
				Utils.GetExceptionErrorString(ex);
				return false;
			}
			finally
			{
				bw?.Close();
			}
		}

		public static ElementTypeCount[] LoadMap(string path, out string error)
		{
			error = null;
			BinaryReader br = null;
			try
			{
				ElementTypeCount[] ary = CreateElemTypeArray(true);
				br = new BinaryReader(File.Open(path, FileMode.Open));
				var acnt = br.ReadInt32();
				Debug.Assert(acnt == ary.Length);
				for (int i = 0; i < acnt; ++i)
				{
					ClrElementType et = (ClrElementType)br.ReadInt32();
					Debug.Assert(ary[i].Type == et);
					ary[i].Count = br.ReadInt32();
					var typeIdCount = br.ReadInt32();
					if (typeIdCount > 0)
					{
						ary[i]._typeIds = new List<int>(typeIdCount);
						for (int j = 0; j < typeIdCount; ++j)
						{
							var tid = br.ReadInt32();
							ary[i]._typeIds.Add(tid);
						}
					}
				}
				br.Close();
				br = null;
				return ary;
			}
			catch (Exception ex)
			{
				Utils.GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				br?.Close();
			}
		}
	}
}
