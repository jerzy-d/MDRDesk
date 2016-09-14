using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
	public class Reports
	{

		public static Tuple<string,string> DumpTypeSizeDetails(string folder, string typeName,
														ulong total, ulong[] notFound,
														SortedDictionary<string, KeyValuePair<int, ulong>> typeDct,
														SortedDictionary<string, List<int>> aryDct,
														out string error)
		{
			error = null;
			StreamWriter sw = null;
			try
			{
				var baseTypeName = Utils.BaseTypeName(typeName);
				var typePath = folder + Path.DirectorySeparatorChar + Utils.GetValidName(baseTypeName) + ".SIZE.DETAILS.TYPES.txt";
				sw = new StreamWriter(typePath);

				sw.WriteLine("### MDRDESK REPORT: SIZE DETAILS TYPES");
				sw.WriteLine("### TITLE: SIZES " + baseTypeName);
				sw.WriteLine("### COUNT: " + Utils.LargeNumberString(typeDct.Count));
				sw.WriteLine("### SEPARATOR: " + Constants.HeavyGreekCrossPadded);
				sw.WriteLine("### COLUMNS: Count|int|150 " + Constants.HeavyGreekCrossPadded + "Total Size|ulong|150" +
					Constants.HeavyGreekCrossPadded + "Type|string|500");

				sw.WriteLine(ReportFile.DescrPrefix + "Size Details for : " + typeName);
				sw.WriteLine(ReportFile.DescrPrefix + "Total Size: " + Utils.LargeNumberString(total));
				sw.WriteLine(ReportFile.DescrPrefix + "Type Count: " + Utils.LargeNumberString(typeDct.Count));
				sw.WriteLine(ReportFile.DescrPrefix + "INVALID ADDRESSES COUNT: : " + Utils.LargeNumberString(notFound.Length) + " (will be resolved)");

				foreach (var kv in typeDct)
				{
					var name = kv.Key;
					var cnt = kv.Value.Key;
					var sz = kv.Value.Value;
					sw.WriteLine(Utils.LargeNumberString(cnt) + Constants.HeavyGreekCrossPadded + Utils.LargeNumberString(sz) + Constants.HeavyGreekCrossPadded + name);
				}
				sw.Close();
				sw = null;

				var aryPath = folder + Path.DirectorySeparatorChar + Utils.GetValidName(baseTypeName) + ".SIZE.DETAILS.ARRAYS.txt";
				sw = new StreamWriter(aryPath);

				sw.WriteLine("### MDRDESK REPORT: SIZE DETAILS ARRAYS");
				sw.WriteLine("### TITLE: ARRAYS " + baseTypeName);
				sw.WriteLine("### COUNT: " + Utils.LargeNumberString(typeDct.Count));
				sw.WriteLine("### SEPARATOR: " + Constants.HeavyGreekCrossPadded);
				sw.WriteLine("### COLUMNS: Number of Arrays|int|100"
											+ Constants.HeavyGreekCrossPadded + "Min Count|int|100"
											+ Constants.HeavyGreekCrossPadded + "Max Count|int|100"
											+ Constants.HeavyGreekCrossPadded + "Avg Count|int|100"
											+ Constants.HeavyGreekCrossPadded + "Total Count|ulong|150"
											+ Constants.HeavyGreekCrossPadded + "Type|string|400");

				sw.WriteLine(ReportFile.DescrPrefix + "Array Details for : " + typeName);
				sw.WriteLine(ReportFile.DescrPrefix + "Total Array Count: " + Utils.LargeNumberString(aryDct.Count));

				foreach (var kv in aryDct)
				{
					var name = kv.Key;
					var lst = kv.Value;
					var acnt = lst.Count;
					var totalElemCount = 0;
					var minElemCount = Int32.MaxValue;
					var maxElemCount = 0;
					var avgElemCount = 0;
					for (int i = 0, icnt = lst.Count; i < icnt; ++i)
					{
						var val = lst[i];
						totalElemCount += val;
						if (val < minElemCount) minElemCount = val;
						if (val > maxElemCount) maxElemCount = val;
					}
					avgElemCount = (int) Math.Round((double) totalElemCount/(double) acnt);
					sw.Write(Utils.LargeNumberString(acnt) + Constants.HeavyGreekCrossPadded);
					sw.Write(Utils.LargeNumberString(minElemCount) + Constants.HeavyGreekCrossPadded);
					sw.Write(Utils.LargeNumberString(maxElemCount) + Constants.HeavyGreekCrossPadded);
					sw.Write(Utils.LargeNumberString(avgElemCount) + Constants.HeavyGreekCrossPadded);
					sw.Write(Utils.LargeNumberString(totalElemCount) + Constants.HeavyGreekCrossPadded);
					sw.WriteLine(name);
				}
				return new Tuple<string, string>(typePath,aryPath);
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return null;
			}
			finally
			{
				sw?.Close();
			}
		}
	}
}

