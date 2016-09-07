using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class DumpComparison
	{
		private string[] _dmpPaths;
		public int Count => _dmpPaths.Length;

		public DumpComparison()
		{
			_dmpPaths = Utils.EmptyArray<string>.Value;
		}

		public DumpComparison(IList<string> dmpPaths)
		{
			_dmpPaths = dmpPaths.ToArray();
		}

		/// <summary>
		/// Parse and combine flies with entries like:
		/// type instance count, total size, type name
		/// [2] (80) ECS.Cache.Common.Interface.ICacheProvider[]
		/// </summary>
		/// <param name="reportPaths">Paths of text files with entries like above.</param>
		/// <param name="error">Output error text if any.</param>
		/// <returns>True if successful.</returns>
		public bool MergeTypeCountAndSizeReports(IList<string> reportPaths, string outFilePath, out string error)
		{
			error = null;
			StreamReader rd = null;
			try
			{
				int columnCount = reportPaths.Count;
				SortedDictionary<string,KeyValuePair<int[],long[]>> dct = new SortedDictionary<string, KeyValuePair<int[], long[]>>(StringComparer.Ordinal);
				for (int i = 0, icnt = reportPaths.Count; i < icnt; ++i)
				{
					var path = reportPaths[i];
					using(rd = new StreamReader(path))
					{
						string ln = rd.ReadLine();
						int eCnt = Int32.Parse(ln);
						while ((ln = rd.ReadLine()) != null)
						{
							int begin = 1;
							int end = ln.IndexOf(']');
							int count = Utils.ConvertToInt(ln,begin,end);
							begin = ln.IndexOf('(', end) + 1;
							end = ln.IndexOf(')', begin);
							long sz = Utils.ConvertToLong(ln, begin, end);
							begin = Utils.SkipWhites(ln, end + 1);
							string name = ln.Substring(begin,ln.Length-begin);
							KeyValuePair<int[], long[]> kv;
							if (dct.TryGetValue(name, out kv))
							{
								kv.Key[i] = count;
								kv.Value[i] = sz;
								dct[name] = kv;
								continue;
							}
							kv = new KeyValuePair<int[], long[]>(
								new int[columnCount],
								new long[columnCount]
								);
							kv.Key[i] = count;
							kv.Value[i] = sz;
							dct.Add(name,kv);
						}
					}
				}

				StreamWriter wr = null;
				using (wr = new StreamWriter(outFilePath))
				{
					StringBuilder sb = new StringBuilder(256);
					foreach (var kv in dct)
					{
						sb.Clear();
						sb.Append(kv.Key).Append('\t');
						//wr.Write(kv.Key + ';');
						int totCnt = 0;
						for(int i=0; i <columnCount; ++i)
						{
							sb.Append(kv.Value.Key[i]).Append('\t');
							//wr.Write(kv.Value.Key[i] + ';');
							totCnt += kv.Value.Key[i];
						}
						sb.Append(totCnt).Append('\t');
						//wr.Write(totCnt + ';');
						long totSz = 0;
						for (int i = 0; i < columnCount; ++i)
						{
							sb.Append(kv.Value.Value[i]).Append('\t');
							//wr.Write(kv.Value.Value[i] + ';');
							totSz += kv.Value.Value[i];
						}
						sb.Append(totSz);
						wr.WriteLine(sb.ToString());

					}
				}


				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
			
		}
	}
}
