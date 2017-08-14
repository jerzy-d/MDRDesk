using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using ClrMDRIndex;
using ClrMDRUtil;
using ClrMDRUtil.Utils;
using DmpNdxQueries;
using Microsoft.Diagnostics.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestMdr
{
    /// <summary>
    /// Summary description for CrashDumpTests
    /// </summary>
    [TestClass]
    public class CrashDumpTests
    {
        #region ctrs/context/initialization

        public CrashDumpTests()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get { return testContextInstance; }
            set { testContextInstance = value; }
        }

        #region Additional test attributes

        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //

        #endregion

        #endregion ctrs/context/initialization

        #region roots

        [TestMethod]
        public void TestStaticVars()
        {
            string error = null;
            using (var clrDump = OpenDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Cowen\Cowen.Analytics.Svc_170717_165238.dmp"))
            {
                try
                {
                    var runtime = clrDump.Runtimes[0];
                    var heap = runtime.Heap;


                    var segs = heap.Segments;
                    for (int i = 0, icnt = segs.Count; i < icnt; ++i)
                    {
                        var seg = segs[i];
                        ulong addr = seg.FirstObject;
                        while (addr != 0ul)
                        {
                            var clrType = heap.GetObjectType(addr);
                            if (clrType == null) goto NEXT_OBJECT;


                            NEXT_OBJECT:
                            addr = seg.NextObject(addr);
                        }
                    }
                }
                catch (Exception ex)
                {
                    error = Utils.GetExceptionErrorString(ex);
                    Assert.IsTrue(false, error);
                }
            }
        }

        #endregion roots

        #region misc

        [TestMethod]
        public void TestMisc()
        {

            string fancy = Utils.GetFancyIntStr(140, 6);


            decimal dd = 0m;
            var ddstr = dd.ToString();

            ulong x = Utils.RootBits.Finalizer;
            ulong y = Utils.RootBits.Rooted;

            string typeName = @"ECS.Common.HierarchyCache.Execute.HierarchyCacheController +<> c__DisplayClass10 < ECS.Cache.Transport.Notifications.FilteredPriceNotification,ECS.Common.HierarchyCache.MarketData.FullPriceOrUnsubscribeNotification >";
            string reversedTypeName = Utils.ReverseTypeName(typeName);


            List<int[]> lst = new List<int[]>();
            int[] ary = new[] { 1, 2, 3, 16, 17 };
            Utils.GetPermutations(ary, 0, ary.Length - 1, lst);
            IntArrayStore rToF = new IntArrayStore(lst.Count + 2);
            for (int i = 0, icnt = lst.Count; i < icnt; ++i)
            {
                var lary = lst[i];
                for (int j = 0, jcnt = lary.Length; j < jcnt; ++j)
                {
                    rToF.Add(i, lary[j]);
                }
            }

            for (int i = 0, icnt = lst.Count; i < icnt; ++i)
            {
                var lary = lst[i];
                var sary = rToF.GetEntry(i);
                Assert.IsTrue(Utils.IsSorted(sary));
            }

            //rToF.Add(1, 16);
            //rToF.Add(1, 2);
            //rToF.Add(1, 3);
            //rToF.Add(1, 1);
            //rToF.Add(1, 0);

            //rToF.Add(1, 16);

        }

        [TestMethod]
        public void GetWinDbgObjects()
        {
            //string path = @"D:\Jerzy\WinDbgStuff\dumps\Compliance\Meka\Eze.Compliance.Svc_170503_131515.HEA2E7F.tmp";
            //string path = @"D:\Jerzy\WinDbgStuff\dumps\Compliance\Meka\Eze.Compliance.Svc_170503_121139.HEA29CE.tmp";
            string path = @"D:\Jerzy\WinDbgStuff\dumps\Compliance\Meka\Eze.Compliance.Svc_170504_085523.HEAB8F5.tmp";
            const string classLineBegin = " {\"00";
            SortedDictionary<string, int> dct = new SortedDictionary<string, int>();

            StreamReader rd = null;
            StreamWriter sw = null;
            try
            {
                int totalCnt = 0;
                int dupCnt = 0;
                rd = new StreamReader(path);
                string ln = rd.ReadLine();
                while (ln != null)
                {
                    if (!ln.StartsWith(classLineBegin)) goto NEXT_LINE;

                    int pos = Utils.SkipNonWhites(ln, classLineBegin.Length);
                    pos = Utils.SkipWhites(ln, pos);
                    int endPos = Utils.SkipNonWhites(ln, pos);
                    string typeName = ln.Substring(pos, endPos - pos);
                    pos = Utils.SkipWhites(ln, endPos);
                    ++pos;
                    endPos = Utils.SkipDecimalDigits(ln, pos);
                    int cnt = Int32.Parse(ln.Substring(pos, endPos - pos));
                    totalCnt += cnt;
                    int dctCnt;
                    if (dct.TryGetValue(typeName, out dctCnt))
                    {
                        dct[typeName] = dctCnt + cnt;
                        dupCnt += cnt;
                    }
                    else
                    {
                        dct.Add(typeName, cnt);
                    }

                    NEXT_LINE:
                    ln = rd.ReadLine();
                }
                rd.Close();
                rd = null;

                sw = new StreamWriter(path + ".Cleaned.txt");
                foreach (var kv in dct)
                {
                    sw.WriteLine(kv.Key + "  " + kv.Value);
                }

            }
            catch (Exception ex)
            {
                Assert.IsTrue(false, ex.ToString());
            }
            finally
            {
                rd?.Close();
                sw?.Close();
            }
        }

        [TestMethod]
        public void CheckObjectCountDiff()
        {
            string mdrObjListPath = @"D:\Jerzy\WinDbgStuff\dumps\Compliance\Meka\Eze.Compliance.Svc_170503_131515.dmp.map\ad-hoc.queries\TypesAndCounts.txt";
            string winDbgListPath = @"D:\Jerzy\WinDbgStuff\dumps\Compliance\Meka\Eze.Compliance.Svc_170503_131515.HEA2E7F.tmp.Cleaned.txt";
            var dct = new SortedDictionary<string, int[]>();
            List<string> _1lst = new List<string>();
            List<string> _1lstmore = new List<string>();
            int[] objectCounts = new int[3];

            StreamReader rd = null;
            StreamWriter sw = null;
            try
            {
                rd = new StreamReader(mdrObjListPath);
                string ln = rd.ReadLine();
                while (ln != null)
                {
                    if (ln.Length < 1 || ln[0] == '#') goto NEXT_LINE;

                    int pos = Utils.SkipNonWhites(ln, 0);
                    int cnt = Int32.Parse(ln.Substring(0, pos));
                    pos = Utils.SkipWhites(ln, pos);
                    int endPos = Utils.SkipNonWhites(ln, pos);
                    string typeName = ln.Substring(pos, endPos - pos);
                    typeName = typeName.Replace('+', '_');

                    int[] objCnt;
                    if (dct.TryGetValue(typeName, out objCnt))
                    {
                        objCnt[0] += cnt;
                    }
                    else
                    {
                        dct.Add(typeName, new int[] { cnt, 0 });
                    }

                    NEXT_LINE:
                    ln = rd.ReadLine();
                }
                rd.Close();
                rd = null;

                rd = new StreamReader(winDbgListPath);
                ln = rd.ReadLine();
                while (ln != null)
                {
                    if (ln.Length < 1) goto NEXT_LINE;

                    int pos = Utils.SkipNonWhites(ln, 0);
                    string typeName = ln.Substring(0, pos);
                    pos = Utils.SkipWhites(ln, pos);
                    int endPos = Utils.SkipNonWhites(ln, pos);
                    int cnt = Int32.Parse(ln.Substring(pos, endPos - pos));

                    int[] objCnt;
                    if (dct.TryGetValue(typeName, out objCnt))
                    {
                        objCnt[1] += cnt;
                    }
                    else
                    {
                        dct.Add(typeName, new int[] { 0, cnt });
                    }

                    NEXT_LINE:
                    ln = rd.ReadLine();
                }
                rd.Close();
                rd = null;

                int totalCnt0 = 0;
                int totalCnt1 = 0;

                int cnt0cnt = 0;
                int cnt1cnt = 0;
                int cnt01diff = 0;


                int cnt0cntCnt = 0;
                int cnt1cntCnt = 0;
                int cnt01diffCnt0 = 0;
                int cnt01diffCnt1 = 0;



                foreach (var kv in dct)
                {
                    string type = kv.Key;
                    int cnt0 = kv.Value[0];
                    int cnt1 = kv.Value[1];
                    totalCnt0 += cnt0;
                    totalCnt1 += cnt1;

                    if (cnt0 == 0)
                    {
                        ++cnt1cnt;
                        cnt1cntCnt += cnt1;
                        _1lst.Add(type);

                    }
                    else if (cnt1 == 0)
                    {
                        ++cnt0cnt;
                        cnt0cntCnt += cnt0;
                    }
                    else if (cnt0 != cnt1)
                    {
                        ++cnt01diff;
                        if (cnt0 > cnt1)
                            cnt01diffCnt0 += cnt0 - cnt1;
                        else
                        {
                            _1lstmore.Add(type);
                            cnt01diffCnt1 += cnt1 - cnt0;
                        }
                    }
                }

                _1lst.Sort();
                _1lstmore.Sort();
            }
            catch (Exception ex)
            {
                Assert.IsTrue(false, ex.ToString());
            }
            finally
            {
                rd?.Close();
                sw?.Close();
            }
        }

        [TestMethod]
        public void MergeWinDbgObjects()
        {
            string[] paths = new string[] {
                @"D:\Jerzy\WinDbgStuff\dumps\Compliance\Meka\Eze.Compliance.Svc_170503_131515.HEA2E7F.tmp.Cleaned.txt",
                @"D:\Jerzy\WinDbgStuff\dumps\Compliance\Meka\Eze.Compliance.Svc_170503_121139.HEA29CE.tmp.Cleaned.txt",
                @"D:\Jerzy\WinDbgStuff\dumps\Compliance\Meka\Eze.Compliance.Svc_170504_085523.HEAB8F5.tmp.Cleaned.txt"
                };
            var dct = new SortedDictionary<string, int[]>();

            StreamReader rd = null;
            StreamWriter sw = null;
            int[] objectCounts = new int[3];
            try
            {
                for (int i = 0; i < 3; ++i)
                {
                    rd = new StreamReader(paths[i]);
                    string ln = rd.ReadLine();
                    while (ln != null)
                    {
                        int pos = Utils.SkipNonWhites(ln, 0);
                        string type = ln.Substring(0, pos);
                        pos = Utils.SkipWhites(ln, pos);
                        int cnt = Int32.Parse(ln.Substring(pos));
                        objectCounts[i] += cnt;
                        int[] ary;
                        if (dct.TryGetValue(type, out ary))
                        {
                            ary[i] = cnt;
                        }
                        else
                        {
                            ary = new int[3];
                            ary[i] = cnt;
                            dct.Add(type, ary);
                        }
                        ln = rd.ReadLine();
                    }
                    rd.Close();
                    rd = null;
                }


                sw = new StreamWriter(@"D:\Jerzy\WinDbgStuff\dumps\Compliance\Meka\33-20-6.TypeCounts.txt");

                sw.WriteLine("### MDRDESK REPORT: TYPECOUNTS");
                sw.WriteLine("### TITLE: Type Counts: " + Utils.CountString(dct.Count));
                sw.WriteLine("### COUNT: " + Utils.CountString(dct.Count));
                sw.WriteLine("### SEPARATOR: " + Constants.HeavyGreekCrossPadded);
                sw.WriteLine("### Type Counts: " + Utils.CountString(dct.Count));
                sw.WriteLine("### COLUMNS: 33GB|int|100"
                 + Constants.HeavyGreekCrossPadded + "20GB|int|100"
                  + Constants.HeavyGreekCrossPadded + "6GB|int|100"
                 + Constants.HeavyGreekCrossPadded + "33-20|int|100"
                 + Constants.HeavyGreekCrossPadded + "33-6|int|100"
                 + Constants.HeavyGreekCrossPadded + "20-6|int|100"
                  + Constants.HeavyGreekCrossPadded + "Type|string|400");


                sw.WriteLine(ReportFile.DescrPrefix + " Object counts");
                sw.WriteLine(ReportFile.DescrPrefix + " 33GB: " + Utils.CountString(objectCounts[0]));
                sw.WriteLine(ReportFile.DescrPrefix + " 20GB: " + Utils.CountString(objectCounts[1]));
                sw.WriteLine(ReportFile.DescrPrefix + "  6GB: " + Utils.CountString(objectCounts[2]));

                foreach (var kv in dct)
                {
                    var ary = kv.Value;
                    sw.Write(Utils.CountString(ary[0]) + Constants.HeavyGreekCrossPadded);
                    sw.Write(Utils.CountString(ary[1]) + Constants.HeavyGreekCrossPadded);
                    sw.Write(Utils.CountString(ary[2]) + Constants.HeavyGreekCrossPadded);
                    sw.Write(Utils.CountString(ary[0] - ary[1]) + Constants.HeavyGreekCrossPadded);
                    sw.Write(Utils.CountString(ary[0] - ary[2]) + Constants.HeavyGreekCrossPadded);
                    sw.Write(Utils.CountString(ary[1] - ary[2]) + Constants.HeavyGreekCrossPadded);
                    sw.WriteLine(kv.Key);
                }

            }
            catch (Exception ex)
            {
                Assert.IsTrue(false, ex.ToString());
            }
            finally
            {
                rd?.Close();
                sw?.Close();
            }
        }

        [TestMethod]
        public void TestClassFieldValue()
        {
            string dumpPath = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Viking\AnalyticsLatencyDump05022017 04345672.dmp";
            string typeName = "Eze.Server.Common.Pulse.Common.ServerColumn";
            List<ulong> lst = new List<ulong>();
            string error = null;
            int typeCount = 0;
            int noFieldCount = 0;
            using (var clrDump = OpenDump(dumpPath))
            {
                try
                {
                    var runtime = clrDump.Runtimes[0];
                    var heap = runtime.Heap;
                    var segs = heap.Segments;
                    for (int i = 0, icnt = segs.Count; i < icnt; ++i)
                    {
                        var seg = segs[i];
                        ulong addr = seg.FirstObject;
                        while (addr != 0ul)
                        {
                            var clrType = heap.GetObjectType(addr);
                            if (clrType == null) goto NEXT_OBJECT;
                            if (typeName != clrType.Name) goto NEXT_OBJECT;
                            ++typeCount;
                            var fld = clrType.GetFieldByName("<Cacheable>k__BackingField");
                            if (fld == null)
                            {
                                ++noFieldCount;
                                continue;
                            }

                            bool val = (bool)fld.GetValue(addr);

                            if (val == false)
                            {
                                lst.Add(addr);
                            }

                            NEXT_OBJECT:
                            addr = seg.NextObject(addr);
                        }
                    }
                }
                catch (Exception ex)
                {
                    error = Utils.GetExceptionErrorString(ex);
                    Assert.IsTrue(false, error);
                }
            }
        }
        [TestMethod]
        public void TestRevert()
        {
            string dataPath = Setup.DumpsFolder + Path.DirectorySeparatorChar + "indexData.txt";
            string indexPath = Setup.DumpsFolder + Path.DirectorySeparatorChar + "index.txt";
            StreamWriter sw = null;
            StreamReader sr = null;
            Random rnd = new Random(17);
            RecordCounter counter = new RecordCounter(100);
            Bitset bitset = new Bitset(100);
            List<int> valLst = new List<int>(20);

            try
            {
                sw = new StreamWriter(dataPath);
                for (int i = 0; i < 1000; ++i)
                {
                    var rndx = rnd.Next(0, 100);
                    if ((rndx % 5) == 0) continue;
                    if (bitset.IsSet(rndx)) continue;
                    bitset.Set(rndx);
                    sw.Write(rndx + ",");
                    var cnt = rnd.Next(1, 21);
                    valLst.Clear();
                    for (int j = 0; j < cnt; ++j)
                    {
                        var val = rnd.Next(0, 100);
                        valLst.Add(val);
                    }
                    valLst.Sort();
                    Utils.RemoveDuplicates(valLst, Comparer<int>.Default);
                    sw.Write(valLst.Count);
                    for (int j = 0; j < valLst.Count; ++j)
                    {
                        counter.Add(valLst[j]);
                        sw.Write("," + valLst[j]);
                    }
                    sw.WriteLine();
                }
                sw.Close();
                sw = null;

                IntStore store = new IntStore(counter);
                Dictionary<int, int[]> dct = new Dictionary<int, int[]>();
                sr = new StreamReader(dataPath);
                string ln = sr.ReadLine();
                int recCount = 0;
                while (ln != null)
                {
                    ++recCount;
                    string[] vals = ln.Split(',');
                    var rndx = Int32.Parse(vals[0]);
                    var cnt = Int32.Parse(vals[1]);
                    Assert.IsTrue(cnt == vals.Length - 2);
                    int[] ints = new int[cnt];
                    for (int i = 2; i < vals.Length; ++i)
                    {
                        ints[i - 2] = Int32.Parse(vals[i]);
                    }
                    dct.Add(rndx, ints);
                    ln = sr.ReadLine();
                }
                foreach (var kv in dct)
                {
                    for (int i = 0; i < kv.Value.Length; ++i)
                    {
                        store.AddItem(kv.Value[i], kv.Key);
                    }
                }
                store.RestoreOffsets();
                Debug.Assert(store.CheckOffsets());
                string error;
                store.DumpTest(indexPath, out error);
                Assert.IsNull(error, error);
            }
            catch (Exception ex)
            {
                Assert.IsTrue(false, ex.ToString());
            }
            finally
            {
                sw?.Close();
                sr?.Close();
            }
        }


        [TestMethod]
        public void TestInstanceCount()
        {
            string error = null;
            int count = 0;
            int nullCount = 0;
            SortedDictionary<string, int> dct = new SortedDictionary<string, int>();
            using (var clrDump = OpenDump(@"D:\Jerzy\WinDbgStuff\dumps\Compliance\Meka\Eze.Compliance.Svc_170503_131515.dmp"))
            {
                try
                {
                    var runtime = clrDump.Runtimes[0];
                    var heap = runtime.Heap;
                    var segs = heap.Segments;
                    for (int i = 0, icnt = segs.Count; i < icnt; ++i)
                    {
                        var seg = segs[i];
                        ulong addr = seg.FirstObject;
                        while (addr != 0ul)
                        {
                            var clrType = heap.GetObjectType(addr);
                            if (clrType == null)
                            {
                                ++nullCount;
                                goto NEXT_OBJECT;
                            }
                            int tcnt;
                            if (dct.TryGetValue(clrType.Name, out tcnt))
                            {
                                dct[clrType.Name] = tcnt + 1;
                            }
                            else
                            {
                                dct.Add(clrType.Name, 1);
                            }
                            ++count;
                            NEXT_OBJECT:
                            addr = seg.NextObject(addr);
                        }
                    }
                    TestContext.WriteLine("SEGMENT Count: " + heap.Segments.Count);
                    TestContext.WriteLine("OBJECTS Count: " + count);
                    TestContext.WriteLine("OBJECTS Null Count: " + nullCount);


                    StreamWriter sw = null;
                    try
                    {
                        var path = DumpFileMoniker.GetAndCreateOutFolder(clrDump.DumpPath, out error) + Path.DirectorySeparatorChar + "TypesAndCounts.txt";
                        sw = new StreamWriter(path);
                        sw.WriteLine("#### SEGMENT COUNT: " + heap.Segments.Count);
                        sw.WriteLine("#### OBJECT COUNT: " + count);
                        foreach (var kv in dct)
                        {
                            sw.Write(kv.Value + "  ");
                            sw.WriteLine(kv.Key);
                        }

                    }
                    finally
                    {
                        sw?.Close();
                    }


                }
                catch (Exception ex)
                {
                    error = Utils.GetExceptionErrorString(ex);
                    Assert.IsTrue(false, error);
                }
            }
        }


        [TestMethod]
        public void TestInstCount()
        {
            var dmp = OpenDump(@"C:\WinDbgStuff\dumps\Compliance\Eze.Compliance.Svc_170503_131515.dmp");
            using (dmp)
            {
                var heap = dmp.Heap;
                int cnt = 0;
                foreach (var obj in heap.EnumerateObjects())
                {
                    ++cnt;
                }

            }
        }

        #endregion misc

        #region collection content

        #region array content

        [TestMethod]
        public void TestGetStringArrayContent()
        {
            ulong aryAddr = 0x0002189f5af6a0;
            var dmp = OpenDump(1);
            using (dmp)
            {
                var heap = dmp.Heap;
                var result = CollectionContent.aryInfo(heap, aryAddr);
                Assert.IsNull(result.Item1, result.Item1);
                string[] strings = new string[result.Item4];
                for (int i = 0, icnt = result.Item4; i < icnt; ++i)
                {
                    strings[i] = CollectionContent.aryElemString(heap, aryAddr, result.Item2, result.Item3, i);
                }
                Assert.IsTrue(NoNullEntries(strings));
                var aryresult = CollectionContent.getAryContent(heap, aryAddr);
                Assert.IsNull(aryresult.Item1);
                Assert.IsTrue(Utils.SameStringArrays(strings, aryresult.Item5));
            }
        }

        [TestMethod]
        public void TestGetDecimalArrayContent()
        {
            ulong aryAddr = 0x0002189f5af8b8;
            var dmp = OpenDump(1);
            using (dmp)
            {
                var heap = dmp.Heap;
                var result = CollectionContent.aryInfo(heap, aryAddr);
                Assert.IsNull(result.Item1, result.Item1);
                string[] strings = new string[result.Item4];
                for (int i = 0, icnt = result.Item4; i < icnt; ++i)
                {
                    strings[i] = CollectionContent.aryElemDecimal(heap, aryAddr, result.Item2, result.Item3, i);
                }
                Assert.IsTrue(NoNullEntries(strings));
                var aryresult = CollectionContent.getAryContent(heap, aryAddr);
                Assert.IsNull(aryresult.Item1);
                Assert.IsTrue(Utils.SameStringArrays(strings, aryresult.Item5));
            }
        }

        [TestMethod]
        public void TestGetDateTimeArrayContent()
        {
            ulong aryAddr = 0x0002189f5af780;
            var dmp = OpenDump(1);
            using (dmp)
            {
                var heap = dmp.Heap;
                var result = CollectionContent.aryInfo(heap, aryAddr);
                Assert.IsNull(result.Item1, result.Item1);
                string[] strings = new string[result.Item4];
                for (int i = 0, icnt = result.Item4; i < icnt; ++i)
                {
                    strings[i] = CollectionContent.aryElemDatetimeR(heap, aryAddr, result.Item2, result.Item3, i);
                }
                Assert.IsTrue(NoNullEntries(strings));
                var aryresult = CollectionContent.getAryContent(heap, aryAddr);
                Assert.IsNull(aryresult.Item1);
                Assert.IsTrue(Utils.SameStringArrays(strings, aryresult.Item5));
            }
        }

        [TestMethod]
        public void TestGetTimespanArrayContent()
        {
            ulong aryAddr = 0x0002189f5af710;
            var dmp = OpenDump(1);
            using (dmp)
            {
                var heap = dmp.Heap;
                var result = CollectionContent.aryInfo(heap, aryAddr);
                Assert.IsNull(result.Item1, result.Item1);
                string[] strings = new string[result.Item4];
                for (int i = 0, icnt = result.Item4; i < icnt; ++i)
                {
                    strings[i] = CollectionContent.aryElemTimespanR(heap, aryAddr, result.Item2, result.Item3, i);
                }
                Assert.IsTrue(NoNullEntries(strings));
                var aryresult = CollectionContent.getAryContent(heap, aryAddr);
                Assert.IsNull(aryresult.Item1);
                Assert.IsTrue(Utils.SameStringArrays(strings, aryresult.Item5));
            }
        }

        [TestMethod]
        public void TestGetGuidArrayContent()
        {
            ulong aryAddr = 0x0002189f5af7f0;
            var dmp = OpenDump(1);
            using (dmp)
            {
                var heap = dmp.Heap;
                var result = CollectionContent.aryInfo(heap, aryAddr);
                Assert.IsNull(result.Item1, result.Item1);
                string[] strings = new string[result.Item4];
                for (int i = 0, icnt = result.Item4; i < icnt; ++i)
                {
                    strings[i] = CollectionContent.aryElemGuid(heap, aryAddr, result.Item2, result.Item3, i);
                }
                Assert.IsTrue(NoNullEntries(strings));
                var aryresult = CollectionContent.getAryContent(heap, aryAddr);
                Assert.IsNull(aryresult.Item1);
                Assert.IsTrue(Utils.SameStringArrays(strings, aryresult.Item5));
            }
        }

        [TestMethod]
        public void TestGetBooleanArrayContent()
        {
            ulong aryAddr = 0x0002189f5a3e68;
            var dmp = OpenDump(1);
            using (dmp)
            {
                var heap = dmp.Heap;
                var result = CollectionContent.aryInfo(heap, aryAddr);
                Assert.IsNull(result.Item1, result.Item1);
                string[] strings = new string[result.Item4];
                for (int i = 0, icnt = result.Item4; i < icnt; ++i)
                {
                    strings[i] = CollectionContent.aryElemPrimitive(heap, aryAddr, result.Item2, result.Item3, i);
                }
                Assert.IsTrue(NoNullEntries(strings));
                var aryresult = CollectionContent.getAryContent(heap, aryAddr);
                Assert.IsNull(aryresult.Item1);
                Assert.IsTrue(Utils.SameStringArrays(strings, aryresult.Item5));
            }
        }


        [TestMethod]
        public void TestGetObjectArrayContent()
        {
            ulong aryAddr = 0x000000018097eea8; //0x0000000380C0E090; // 0x0000000380C67E70;
            var dmp = OpenDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Centurion\AnalyticsCenturion.4.18.17.dmp");
            int defCnt = 0;
            int oneCnt = 0;
            int twoCnt = 0;
            int nullCnt = 0;
            int somethingCnt = 0;
            using (dmp)
            {
                var heap = dmp.Heap;
                var result = CollectionContent.aryInfo(heap, aryAddr);
                int aryLen = result.Item4;
                ClrType clrType = result.Item2;
                string[] values = new string[aryLen];
                for (int i = 0; i < aryLen; ++i)
                {
                    var obj = clrType.GetArrayElementValue(aryAddr, i);
                    if (obj != null)
                    {
                        ulong oaddr = (ulong)obj;
                        var oType = heap.GetObjectType(oaddr);
                        if (oType != null)
                        {
                            if (oType.Name == "Eze.Server.Common.Pulse.Common.Types.CachedValue+DefaultOnly<System.Decimal>")
                            {
                                values[i] = "0 default";
                                ++defCnt;
                            }
                            else if (oType.Name == "Eze.Server.Common.Pulse.Common.Types.CachedValue+One<System.Decimal>")
                            {
                                var fld = oType.GetFieldByName("one");
                                values[i] = ValueExtractor.GetDecimalValue(oaddr, fld, false);
                                ++oneCnt;
                            }
                            else if (oType.Name == "Eze.Server.Common.Pulse.Common.Types.CachedValue+Two<System.Decimal>")
                            {
                                var fld = oType.GetFieldByName("one");
                                var val1 = ValueExtractor.GetDecimalValue(oaddr, fld, false);
                                fld = oType.GetFieldByName("two");
                                var val2 = ValueExtractor.GetDecimalValue(oaddr, fld, false);
                                values[i] = val1 + " | " + val2;
                                ++twoCnt;
                            }
                            else
                            {
                                values[i] = "unknown";
                                ++somethingCnt;
                            }
                        }
                        else
                        {
                            values[i] = "null  " + Utils.RealAddressString(oaddr);
                            ++nullCnt;
                        }
                    }
                }

                Assert.IsNull(result.Item1, result.Item1);

            }
        }


        private bool NoNullEntries(string[] ary)
        {
            for (int i = 0, icnt = ary.Length; i < icnt; ++i)
            {
                if (ary[i] == null) return false;
            }
            return true;
        }

        #endregion array content

        #region System.Collections.Generic.Dictionary<TKey,TValue> content

        //[TestMethod]
        //public void TestGetDictionaryContent()
        //{
        //    ulong dctAddr = 0x6aa051f410; //0xe0859ab3e8; // 0x0000e0859ab388; // 0x000084d7ce3938;
        //    var dmp = OpenDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Ellerston\Eze.Analytics.Svc_170607_214916.dmp");
        //    using (dmp)
        //    {
        //        var heap = dmp.Heap;
        //        var clrType = heap.GetObjectType(dctAddr);
        //        if (!clrType.Name.StartsWith("System.Collections.Generic.Dictionary<")
        //            && !clrType.BaseType.Name.StartsWith("System.Collections.Generic.Dictionary<")) return;

        //        var dctAry = ValueExtractor.GetDictionaryInfo(heap, dctAddr, clrType);
        //        var lst = new List<KeyValuePair<ulong, ulong>>();
        //        for (int i = 0, icnt = dctAry.Length; i < icnt; ++i)
        //        {
        //            if (dctAry[i].Key == 0ul) continue;
        //            lst.Add(dctAry[i]);
        //        }



        //        return;

        //        var result = CollectionContent.getDictionaryInfo(heap, dctAddr, clrType);
        //        Assert.IsNotNull(result);




        //        var dctResult = CollectionContent.dictionaryContent(heap, dctAddr);
        //        var entries = dctResult.Item7;

        //        StreamWriter sw = null;
        //        string error;
        //        try
        //        {
        //            var path = DumpFileMoniker.GetAndCreateOutFolder(dmp.DumpPath, out error) + Path.DirectorySeparatorChar + "DctHashesContent.txt";
        //            sw = new StreamWriter(path);

        //            for (int i = 0; i < entries.Length; ++i)
        //            {
        //                var entry = entries[i];
        //                var hsetAddr = Convert.ToUInt64(entry.Value, 16);

        //                //                        var hcontent = CollectionContent.getHashSetContent(heap, hsetAddr);
        //                sw.Write(entry.Key);
        //                sw.Write(" : ");
        //                sw.Write(Utils.RealAddressString(hsetAddr));
        //                var entryType = heap.GetObjectType(hsetAddr);
        //                var typeName = entryType == null ? "{null type}" : entryType.Name;
        //                sw.Write("  ");
        //                sw.Write(typeName);
        //                //if (hcontent == null)
        //                //{
        //                //    sw.Write("{null}");
        //                //}
        //                //else
        //                //{
        //                //    for (int j = 0, jcnt = hcontent.Item2.Length; j < jcnt; ++j)
        //                //    {
        //                //        sw.Write(" ");
        //                //        sw.Write(hcontent.Item2[j]);
        //                //    }
        //                //}
        //                sw.WriteLine();
        //            }
        //        }
        //        finally
        //        {
        //            sw?.Close();
        //        }



        //        Assert.IsNotNull(dctResult);
        //        Assert.IsNull(dctResult.Item1, dctResult.Item1);
        //    }
        //}

        ulong[] relatedViews = new ulong[] {
 0x000006ca17ed798
,0x000006ad489cb40
,0x000006aad02a798
,0x000006cd768f120
,0x000006ba9fbd8a0
,0x0000069aa47b8d0
,0x000006ca9e22d28
,0x000006caa86a758
,0x000006aaf7cfae0
,0x000006cd77c79c0
,0x000006aab741598
,0x000006aab82b870
,0x000006ca186a8c0
,0x000006aa3832058
,0x000006bad8b0a78
,0x000006cd7740968
,0x000006bacc68088
,0x000006bacceb288
,0x0000069a26aa500
,0x000006aa6227800
,0x000006aafbf5260
,0x000006aafe945a8
,0x000006ca17df130
,0x000006aa3762250
,0x000006aafc094f8
,0x000006cd7759988
,0x000006aa225ed68
,0x000006ca6fa09c8
,0x0000069aaeea3a8
,0x000006cd765bcb0
,0x0000069a2a49838
,0x0000069a4c48090
,0x000006caa919710
,0x000006caa9ac098
,0x000006ba2c3b3a0
,0x000006ba4ce4e80
,0x000006baca98750
,0x000006ad492b598
,0x000006bacc7f758
,0x000006cd7705e08
,0x000006aac668098
,0x000006aac6afc28
,0x0000069ae842b28
,0x000006cd7727850
,0x000006cac56daf0
,0x000006cd7675990
,0x000006ba1911340
,0x000006ca2c1d060
,0x000006ab002bcc0
,0x000006ab02edf88
,0x000006aab79de78
,0x000006ad496bed8
,0x000006ba9d249c8
,0x000006bd5096e20
,0x000006aaeaef7b8
,0x000006ad4916248
,0x0000069ad5a6fb0
,0x0000069ad5fdc78
,0x000006ba2c48c20
,0x000006aa61bf370
,0x0000069aeb9cc60
,0x000006cd7773450
,0x000006baca96378
,0x000006baca9b788
,0x0000069a184ec30
,0x000006ca3399c40
,0x000006cafb97f40
,0x000006cd77a5f38
,0x0000069a0fb76b0
,0x0000069a28eaae0
,0x000006ca9e331a0
,0x000006ad4948f38
,0x0000069ad5eb468
,0x000006ad493a198
,0x000006cac412dc0
,0x000006ad20ea050
,0x000006aaceccdf0
,0x000006aad094f58
,0x000006caffbf3c0
,0x000006cd77d8dd0
,0x000006caa92eec0
,0x000006ad4959cb8
,0x000006ba231c990
,0x000006ba31cb460
,0x000006bad863e58
,0x000006bad9a8080
,0x0000069a1235658
,0x0000069a2833d48
,0x000006ca17138c8
,0x000006ad4888b80
,0x000006aaeccf5f0
,0x000006aaecf0ee0
,0x000006bac650568
,0x000006bac701118
,0x000006caf5efc98
,0x000006ad4268790
,0x0000069ae832838
,0x0000069ae938080
,0x0000069a0f87af0
,0x000006ba4f0c860
,0x000006aadcb3418
,0x000006cad471db0
,0x000006cae93ea00
,0x000006bace2f238
,0x0000069a2a40678
,0x000006cd77e7ab8
,0x0000069a2658a48
,0x000006ba4b9e9e0
,0x0000069af15d138
,0x000006cd778c470
,0x000006aa1c0e668
,0x000006ad4874320
,0x000006aaf5c2578
,0x000006aaf7e7dc8
,0x000006aa6659658
,0x000006aaea1cc58
,0x000006aaeae8880
,0x000006aaeaf2860
,0x000006cafb608e8
,0x000006cafbfb168
,0x000006bac69d7b0
,0x000006cd76e4600
,0x000006baa11e198
,0x000006ad497dfd8
,0x000006cae9f7858
,0x000006cd7716ad0
,0x000006cad3f7418
,0x000006cd76c0b50
,0x0000069aeb7bf40
,0x0000069af0599b8
,0x000006aa665d4f8
,0x000006cd77b6c40
,0x0000069abf4eb78
,0x000006ad136cac8
,0x0000069ac0a9480
,0x000006cd76a7818
    };

        ulong[] _RelatedViewsCacheValid;
        ulong[] _RelatedViewsCacheNonValid;

        [TestMethod]
        public void TestRelatedViews()
        {
            string error;
            ulong dctAddr = 0x6aa051f410; //0xe0859ab3e8; // 0x0000e0859ab388; // 0x000084d7ce3938;
            var dmp = OpenDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Ellerston\Eze.Analytics.Svc_170607_214916.dmp");
            using (dmp)
            {
                var heap = dmp.Heap;
                ClrType rv = heap.GetObjectType(relatedViews[0]);
                ClrInstanceField rvfld = rv.GetFieldByName("relatedViewsID");
                ClrType rvc = heap.GetObjectType(relatedViews[1]);
                List<string> lst = new List<string>(relatedViews.Length / 2);
                for (int i = 0, icnt = relatedViews.Length; i < icnt; i += 2)
                {
                    string name = (string)rvfld.GetValue(relatedViews[i], false, true);
                    lst.Add(name);
                }
                lst.Sort(StringComparer.OrdinalIgnoreCase);

                string path = DumpFileMoniker.GetAndCreateOutFolder(dmp.DumpPath, out error) + System.IO.Path.DirectorySeparatorChar + "relatedViewsID.txt";

                Utils.WriteStringList(path, lst, out error);

                var validSet = new HashSet<ulong>();
                for (int i = 0, icnt = relatedViews.Length; i < icnt; i += 2)
                {
                    validSet.Add(relatedViews[i + 1]);
                }

                List<ulong> nonValid = new List<ulong>();
                string rvcName = rvc.Name;
                var segs = heap.Segments;
                for (int i = 0, icnt = segs.Count; i < icnt; ++i)
                {
                    var seg = segs[i];
                    ulong addr = seg.FirstObject;
                    while (addr != 0ul)
                    {
                        var clrType = heap.GetObjectType(addr);
                        if (clrType == null) goto NEXT_OBJECT;
                        if (!Utils.SameStrings(rvcName, clrType.Name)) goto NEXT_OBJECT;

                        if (!validSet.Contains(addr))
                            nonValid.Add(addr);

                        NEXT_OBJECT:
                        addr = seg.NextObject(addr);
                    }
                }


                path = DumpFileMoniker.GetAndCreateOutFolder(dmp.DumpPath, out error) + System.IO.Path.DirectorySeparatorChar + "ValidRelatedViewsCache.txt";
                var valid = validSet.ToArray();
                Array.Sort(valid);
                Utils.WriteAddressAsStringArray(path, valid, out error);
                nonValid.Sort();
                var nonValidUnique = nonValid.Distinct().ToArray();
                path = DumpFileMoniker.GetAndCreateOutFolder(dmp.DumpPath, out error) + System.IO.Path.DirectorySeparatorChar + "NonValidRelatedViewsCache.txt";

                Utils.WriteAddressAsStringArray(path, nonValidUnique, out error);

                _RelatedViewsCacheValid = valid;
                _RelatedViewsCacheNonValid = nonValid.ToArray();

                path = DumpFileMoniker.GetAndCreateOutFolder(dmp.DumpPath, out error) + System.IO.Path.DirectorySeparatorChar + "ValidRelatedViewsCache.bin";
                Utils.WriteUlongArray(path, valid, out error);

                var xlst1 = GetStuff(heap, _RelatedViewsCacheValid);
                path = DumpFileMoniker.GetAndCreateOutFolder(dmp.DumpPath, out error) + System.IO.Path.DirectorySeparatorChar + "ValidRelatedViewsCacheToRelatedViews.txt";
                Utils.WriteStringList(path, xlst1, out error);

                var xlst2 = GetStuff(heap, _RelatedViewsCacheNonValid);
                path = DumpFileMoniker.GetAndCreateOutFolder(dmp.DumpPath, out error) + System.IO.Path.DirectorySeparatorChar + "NonValidRelatedViewsCacheToRelatedViews.txt";
                Utils.WriteStringList(path, xlst2, out error);

            }
        }

        private string[] GetStuff(ClrHeap heap, ulong[] addresses)
        {
            List<string> lst = new List<string>(256);
            var xaddr = addresses[0];
            var xclrType = heap.GetObjectType(xaddr);
            var xfld = xclrType.GetFieldByName("relatedViews");
            var xrvAddr = (ulong)xfld.GetValue(xaddr, false, false);
            var xrvType = heap.GetObjectType(xrvAddr);
            var xrvIdFld = xrvType.GetFieldByName("relatedViewsID");
            for (int i = 0, icnt = addresses.Length; i < icnt; ++i)
            {
                xaddr = addresses[i];
                xclrType = heap.GetObjectType(xaddr);
                xrvAddr = (ulong)xfld.GetValue(xaddr, false, false);
                string id = (string)xrvIdFld.GetValue(xrvAddr, false, true);
                lst.Add(id + Constants.HeavyGreekCrossPadded + Utils.RealAddressString(xaddr) + Constants.HeavyGreekCrossPadded + Utils.RealAddressString(xrvAddr));
            }
            lst.Sort(StringComparer.OrdinalIgnoreCase);
            return lst.ToArray();
        }


        [TestMethod]
        public void TestGetDictionaryOfDictionaryContent()
        {
            StringBuilder sb = new StringBuilder(1024);
            ulong dctAddr = 0x00000011ec33cea8;
            //ulong dctAddr = 0x000000ea94048eb8;
            var dmp = OpenDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Viking\AnalyticsVikingSR9.dmp");
            //var dmp = OpenDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Viking\AnalyticsVikingSR10.dmp");
            using (dmp)
            {
                var heap = dmp.Heap;
                var clrType = heap.GetObjectType(dctAddr);
                if (!clrType.Name.StartsWith("System.Collections.Generic.Dictionary<")
                    && !clrType.BaseType.Name.StartsWith("System.Collections.Generic.Dictionary<")) return;
                var result = CollectionContent.getDictionaryInfo(heap, dctAddr, clrType);
                Assert.IsNotNull(result);

                var dctResult = CollectionContent.dictionaryContent(heap, dctAddr);
                var entries = dctResult.Item7;
                sb.AppendLine(clrType.Name);


                StreamWriter sw = null;
                try
                {
                    List<ulong> dctAddrs = new List<ulong>();
                    for (int i = 0; i < entries.Length; ++i)
                    {
                        var entry = entries[i];
                        var hsetAddr = Convert.ToUInt64(entry.Value, 16);
                        dctAddrs.Add(hsetAddr);
                    }

                    for (int i = 0; i < dctAddrs.Count; ++i)
                    {
                        var dctaddr = dctAddrs[i];
                        var clrtype = heap.GetObjectType(dctaddr);
                        sb.AppendLine("   " + clrtype.Name);
                        var res = CollectionContent.getDictionaryInfo(heap, dctaddr, clrtype);
                        var cont = CollectionContent.dictionaryContent(heap, dctaddr);
                        for (int j = 0, jcnt = cont.Item7.Length; j < jcnt; ++j)
                        {
                            var entry = cont.Item7[j];
                            var hsetAddr = Convert.ToUInt64(entry.Value, 16);
                            var clrtp = heap.GetObjectType(hsetAddr);
                            sb.AppendLine("      " + clrtp.Name);

                            var resu = CollectionContent.getDictionaryInfo(heap, hsetAddr, clrtp);
                            sb.AppendLine("      count: " + resu.Item2);

                        }

                    }

                    string output = sb.ToString();
                }
                finally
                {
                    sw?.Close();
                }



                Assert.IsNotNull(dctResult);
                Assert.IsNull(dctResult.Item1, dctResult.Item1);
            }
        }

        [TestMethod]
        public void TestGetDictionaryViewLinks()
        {
            StringBuilder sb = new StringBuilder(1024);
            ulong[] dctAddrs = new ulong[] { 0x000011ec4b82f0,
                                                0x000011ecd15ce8,
                                                0x000012ec6f0328,
                                                0x000014ef8b3b10 };
            //ulong[] dctAddrs = new ulong[] { 0x0000ea9480f770,
            //                                    0x0000ec93cd24f0,
            //                                    0x0000ec9425f018,
            //                                    0x0000ec94cdbf48
            //                                     };

            var dmp = OpenDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Viking\AnalyticsVikingSR9.dmp");
            //var dmp = OpenDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Viking\AnalyticsVikingSR10.dmp");
            var counts = new List<KeyValuePair<string, int>>(4);
            int[] totcounts = new int[4];
            int gtotal = 0;
            using (dmp)
            {
                try
                {
                    var heap = dmp.Heap;
                    for (int i = 0, icnt = dctAddrs.Length; i < icnt; ++i)
                    {
                        ulong addr = dctAddrs[i];
                        var clrType = heap.GetObjectType(addr);
                        var dctResult = CollectionContent.dictionaryContent(heap, addr);
                        foreach (var entry in dctResult.Item7)
                        {
                            var eaddr = Convert.ToUInt64(entry.Value, 16);
                            var clrtp = heap.GetObjectType(eaddr);
                            var fld = clrtp.GetFieldByName("dependentRelatedViewsSet");
                            ulong fldAddr = (ulong)fld.GetValue(eaddr, false, false);
                            var fldType = heap.GetObjectType(fldAddr);
                            var hfld = fldType.GetFieldByName("m_count");
                            int count = (int)hfld.GetValue(fldAddr, false, false);
                            counts.Add(new KeyValuePair<string, int>(Utils.RealAddressString(addr), count));
                            totcounts[i] += count;
                            gtotal += count;

                        }

                    }
                }
                catch (Exception ex)
                {
                    Assert.IsTrue(false, ex.ToString());

                }
                finally
                {
                }
            }
        }

        [TestMethod]
        public void TestGetDictionaryRelatedViews()
        {
            StringBuilder sb = new StringBuilder(1024);
            ulong[] calcCacheAddrs = new ulong[] { 0x000014ec59fef8, 0x0000eb92fffe28 };
            List<string> lst0 = new List<string>(256);
            List<string> lst1 = new List<string>(256);
            ClrtDump dmp = null;
            for (int d = 0; d < 2; ++d)
            {
                dmp = d == 0 ? OpenDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Viking\AnalyticsVikingSR9.dmp")
                             : OpenDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Viking\AnalyticsVikingSR10.dmp");
                ulong addr = calcCacheAddrs[d];
                List<string> lst = d == 0 ? lst0 : lst1;
                using (dmp)
                {
                    try
                    {
                        var heap = dmp.Heap;
                        var clrType = heap.GetObjectType(addr);
                        var fld = clrType.GetFieldByName("calculationCache");
                        var cacheAddr = (ulong)fld.GetValue(addr, false, false);
                        var clrCache = heap.GetObjectType(cacheAddr);
                        var fldData = clrCache.GetFieldByName("data");
                        ulong fldDataAddr = (ulong)fldData.GetValue(cacheAddr, false, false);
                        var clrDct = heap.GetObjectType(fldDataAddr);
                        var dctCont = CollectionContent.dictionaryContent(heap, fldDataAddr);
                        for (int i = 0, icnt = dctCont.Item7.Length; i < icnt; ++i)
                        {
                            var vaddr = Convert.ToUInt64(dctCont.Item7[i].Value, 16);
                            var clrView = heap.GetObjectType(vaddr);
                            var fldRv = clrView.GetFieldByName("relatedViews");
                            ulong fldRvAddr = (ulong)fldRv.GetValue(vaddr, false, false);
                            var clrRv = heap.GetObjectType(fldRvAddr);
                            var fldRvId = clrRv.GetFieldByName("relatedViewsID");
                            string id = (string)fldRvId.GetValue(fldRvAddr, false, true);
                            lst.Add(id);
                        }
                    }
                    catch (Exception ex)
                    {
                        Assert.IsTrue(false, ex.ToString());

                    }
                }
            }
            lst0.Sort(StringComparer.OrdinalIgnoreCase);
            lst1.Sort(StringComparer.OrdinalIgnoreCase);
            var sb0 = new StringBuilder(256 * 32);
            foreach (var s in lst0)
            {
                sb0.AppendLine(s);
            }
            var sb1 = new StringBuilder(256 * 32);
            foreach (var s in lst1)
            {
                sb1.AppendLine(s);
            }
            var s0 = sb0.ToString();
            var s1 = sb1.ToString();

        }


        #endregion System.Collections.Generic.Dictionary<TKey,TValue> content

        #region System.Collections.Generic.List<T>

        [TestMethod]
        public void TestGetListContentSpecialCase()
        {

            ulong[] addrs = new ulong[]
            {
            0x0000e0859ab590,
            0x0000e0859ab638,
            0x0000e0859ab688,
            0x0000e084de8308,
            0x0000df8651f278,
            0x0000e0859c0d40,
            0x0000e0859e0310,
            0x0000e085a1bce0,
            0x0000e085a3ba08,
            0x0000e085a51f68,
            0x0000e085a655a8,
            0x0000e085a7a7f8,
            0x0000e085a7de48,
            0x0000df86557490,
            0x0000e085a81050,
            0x0000e085a837b0,
            0x0000e085a86860,
            0x0000e085a8a7e8,
            0x0000e085a8d290,
            0x0000df869589d8,
            0x0000e085a8fc08,
            0x0000e085a910f0,
            0x0000df8695a1c8,
            0x0000e085a97198,
            0x0000e085a99b30,
            0x0000e085aa79e0,
            0x0000e085ab2318,
            0x0000df87624818,
            0x0000df87631240,
            0x0000df87638828,
            0x0000df8766cac8,
            0x0000e085b65140,
            0x0000df87683920,
            0x0000e085bd95b0,
            0x0000e085bfa268,
            0x0000e1060eee00,
            0x0000df06e62bb8,
            0x0000df06e7ad00,
            0x0000e185958e50,
            0x0000e185959be8,
            0x0000e18595cc10,
            0x0000e18595ea60,
            0x0000e1060f6cd0,
            0x0000e1061478c0,
            0x0000e10614a808,
            0x0000e1061e61e8,
            0x0000e1061e6238,
            0x0000e1061e6288,
            0x0000e1061e62d8,
            0x0000e1061e6328,
            0x0000e106292458,

        };
            var dmp = OpenDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Viking\AnalyticsLatencyDump05022017 04345672.dmp");
            List<int> hashes = new List<int>();
            List<int[]> ids = new List<int[]>();

            using (dmp)
            {
                var heap = dmp.Heap;

                for (int k = 0, kcnt = addrs.Length; k < kcnt; ++k)
                {
                    var result = ValueExtractor.ListInfo(heap, addrs[k]);


                    int aryLen = result.Item6;
                    ClrType clrType = result.Item3;
                    ulong aryAddr = result.Item5;

                    int len = clrType.GetArrayLength(aryAddr);

                    //int[] values = new int[aryLen];

                    int[] values = ValueExtractor.ReadIntAryAtAddress(aryAddr + 16, aryLen, heap);



                    //for (int i = 0; i < aryLen; ++i)
                    //{


                    //    var obj = clrType.GetArrayElementValue(aryAddr, i);
                    //    var oaddr = clrType.GetArrayElementAddress(aryAddr, i);
                    //    if (obj != null && obj is Int32)
                    //    {
                    //        values[i] = (int)obj;
                    //    }
                    //    else
                    //    {
                    //        values[i] = 0;
                    //    }
                    //}

                    ids.Add(values);
                    unchecked
                    {
                        var hash = values.Aggregate(0, (current, id) => (current * 397) ^ id);
                        hashes.Add(hash);
                    }


                }
            }

            StringBuilder sb = new StringBuilder(1024);

            sb.Append("int[][] ids = new int[").Append(ids.Count).AppendLine("]");
            sb.AppendLine("{");
            for (int i = 0, icnt = ids.Count; i < icnt; ++i)
            {
                sb.Append("   new int[] { ");
                for (int j = 0, jcnt = ids[i].Length; j < jcnt; ++j)
                {
                    sb.Append(ids[i][j]).Append(", ");
                }
                sb.AppendLine("},");
            }
            sb.AppendLine("};");

            string str = sb.ToString();
        }

        #endregion System.Collections.Generic.List<T>

        #region System.Collections.Generic.SortedDictionary<TKey,TValue> content

        [TestMethod]
        public void TestGetSortedDictionaryContent()
        {
            ulong dctAddr = 0x00015e80013030;
            var dmp = OpenDump(1);
            using (dmp)
            {
                var heap = dmp.Heap;
                var result = CollectionContent.getSortedDicionaryContent(heap, dctAddr);
                Assert.IsNotNull(result);
                Assert.IsNull(result.Item1, result.Item1);
            }
        }

        #endregion System.Collections.Generic.SortedDictionary<TKey,TValue> content

        #region System.Collections.Generic.HashSet<T> content

        [TestMethod]
        public void TestGetHashSetContent()
        {
            ulong[] dctAddrs = new ulong[]
            {
                0x00023f00013130, // string
				0x00023f00013170, // decimal
				0x00023f00013238, // DateTime
				0x00023f00013300, // TimeSpan
				0x00023f000133c8, // Guid
				0x00023f00013490, // float, System.Single
				0x00023f00013558, // double
				0x00023f00013620, // char

			};

            string[] valTypeNames = new string[dctAddrs.Length];
            string[][] values = new string[dctAddrs.Length][];
            var dmp = OpenDump(1);
            int index = 0;
            using (dmp)
            {
                var heap = dmp.Heap;
                FQry.heapWarmup(heap);
                ClrType setType = null;
                goto GET_CONTENT;
                for (int i = 3, icnt = dctAddrs.Length; i < 4; ++i)
                {
                    setType = heap.GetObjectType(dctAddrs[i]);

                    ClrInstanceField slotsFld = setType.GetFieldByName("m_slots");
                    ulong slotsAddr = (ulong)slotsFld.GetValue(dctAddrs[i]);
                    ClrType slotsType = heap.GetObjectType(slotsAddr);
                    var lastIndex = Auxiliaries.getFieldIntValue(heap, dctAddrs[i], setType, "m_lastIndex");
                    var setCount = Auxiliaries.getFieldIntValue(heap, dctAddrs[i], setType, "m_count");
                    ClrType compType = slotsType.ComponentType;

                    var hashCodeFld = compType.GetFieldByName("hashCode");
                    var valueFld = compType.GetFieldByName("value");


                    var valType = valueFld.Type;
                    if (valType.Name == "ERROR" || valType.Name == "System.__Canon")
                    {
                        var mt = ValueExtractor.ReadUlongAtAddress(dctAddrs[i] + 96, heap);
                        var tp = heap.GetTypeByMethodTable(mt);
                        if (tp != null)
                        {
                            valType = tp;
                        }
                        else
                        {
                            index = 0;
                            while (index < lastIndex)
                            {

                                var elemAddr = slotsType.GetArrayElementAddress(slotsAddr, index);
                                var hash = Auxiliaries.getIntValue(elemAddr, hashCodeFld, true);
                                if (hash >= 0)
                                {
                                    var valAddr = Auxiliaries.getReferenceFieldAddress(elemAddr, valueFld, true);
                                    tp = heap.GetObjectType(valAddr);
                                    if (tp != null)
                                    {
                                        valType = tp;
                                        break;
                                    }
                                }

                                ++index;
                            }
                        }
                    }
                    var kind = TypeKinds.GetTypeKind(valType);
                    values[i] = new string[setCount];
                    index = 0;
                    var valIndex = 0;
                    while (index < lastIndex)
                    {

                        var elemAddr = slotsType.GetArrayElementAddress(slotsAddr, index);
                        var hash = Auxiliaries.getIntValue(elemAddr, hashCodeFld, true);
                        if (hash >= 0)
                        {
                            string value = Types.getFieldValue(heap, elemAddr, true, valueFld, kind);
                            values[i][valIndex++] = value;
                        }

                        ++index;
                    }

                    valTypeNames[i] = valType.Name;
                }

                return;
                GET_CONTENT:
                for (int i = 0, icnt = dctAddrs.Length; i < icnt; ++i)
                {
                    var setResult = CollectionContent.getHashSetContent(heap, dctAddrs[i]);
                    Assert.IsNotNull(setResult);
                    Assert.IsNull(setResult.Item1);
                    values[i] = setResult.Item2;
                }

            }
        }

        #endregion  System.Collections.Generic.HashSet<T> content

        [TestMethod]
        public void TestGetConcurrentDictionaryContent()
        {
            string dumpPath = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Viking\AnalyticsLatencyDump05022017 04345672.dmp";

            var dmp = OpenDump(dumpPath);
            using (dmp)
            {
                var heap = dmp.Heap;
                ulong dAddr = 0x00e105255b98;
                var result2 = CollectionContent.dictionaryContent(heap, dAddr);
            }
        }


        #region System.Text.StringBuilder

        [TestMethod]
        public void TestStringBuilderContent()
        {
            ulong addr = 0x0001e7e526c388;
            var dmp = OpenDump(1);
            using (dmp)
            {
                var heap = dmp.Heap;

                var str = CollectionContent.getStringBuilderString(heap, addr);

                Assert.IsNotNull(str);
            }
        }

        #endregion System.Text.StringBuilder

        #endregion collection content

        #region roots/instances/references

        //[TestMethod]
        //public void TestIndexing()
        //{
        //	string error;
        //	string dumpPath = Setup.RecentAdhocList[0];
        //	var dmp = OpenDump(dumpPath);
        //	var fileMoniker = new DumpFileMoniker(dumpPath);
        //	var strIds = new StringIdDct();

        //	using (dmp)
        //	{
        //		var heap = dmp.Heap;
        //		ulong[] instances = DumpIndexer.GetHeapAddressesCount(heap);
        //		string[] typeNames = DumpIndexer.GetTypeNames(heap, out error);
        //		var rootAddrInfo = ClrtRootInfo.GetRootAddresses(0, dmp.Runtimes[0], heap, typeNames, strIds, fileMoniker, out error);

        //		Assert.IsNull(error);
        //		Assert.IsTrue(Utils.IsSorted(rootAddrInfo.Item1));
        //		Assert.IsTrue(Utils.IsSorted(rootAddrInfo.Item2));

        //		var rootAddresses = Utils.MergeAddressesRemove0s(rootAddrInfo.Item1, rootAddrInfo.Item2);

        //		Assert.IsTrue(Utils.AreAllInExcept0(rootAddresses, rootAddrInfo.Item1));
        //		Assert.IsTrue(Utils.AreAllInExcept0(rootAddresses, rootAddrInfo.Item2));
        //		Assert.IsTrue(Utils.AreAllDistinct(rootAddresses));
        //		Assert.IsTrue(Utils.IsSorted(rootAddresses));

        //		rootAddresses = Utils.GetRealAddressesInPlace(rootAddresses);

        //		HashSet<ulong> done = new HashSet<ulong>();
        //		SortedDictionary<ulong, List<ulong>> objRefs = new SortedDictionary<ulong, List<ulong>>();
        //		SortedDictionary<ulong, List<ulong>> fldRefs = new SortedDictionary<ulong, List<ulong>>();

        //		bool result = References.GetRefrences(heap, rootAddresses, objRefs, fldRefs, done, out error);

        //		Assert.IsTrue(result);
        //		Assert.IsNull(error);

        //		string path = fileMoniker.GetFilePath(0, Constants.MapParentFieldsRootedPostfix);
        //		result = DumpReferences(path, objRefs, instances, out error);
        //		Assert.IsTrue(result);
        //		Assert.IsNull(error);
        //		path = fileMoniker.GetFilePath(0, Constants.MapFieldParentsRootedPostfix);
        //		result = DumpReferences(path, fldRefs, instances, out error);
        //		Assert.IsTrue(result);
        //		Assert.IsNull(error);

        //		var rootedAry = done.ToArray(); // for later
        //		Array.Sort(rootedAry);
        //		done.Clear();
        //		objRefs.Clear();
        //		fldRefs.Clear();
        //		var remaining = Utils.Difference(instances, rootedAry);
        //		result = References.GetRefrences(heap, remaining, objRefs, fldRefs, done, out error);
        //		Assert.IsTrue(result);
        //		Assert.IsNull(error);
        //		path = fileMoniker.GetFilePath(0, Constants.MapParentFieldsNotRootedPostfix);
        //		result = DumpReferences(path, objRefs, instances, out error);
        //		Assert.IsTrue(result);
        //		Assert.IsNull(error);
        //		path = fileMoniker.GetFilePath(0, Constants.MapFieldParentsNotRootedPostfix);
        //		result = DumpReferences(path, fldRefs, instances, out error);
        //		Assert.IsTrue(result);
        //		Assert.IsNull(error);

        //		var roots = ClrtRootInfo.Load(0, fileMoniker, out error);
        //		Assert.IsNull(error);
        //		Assert.IsNotNull(roots);

        //		ulong[] finalizer = Utils.GetRealAddressesInPlace(roots.FinalizerAddresses);
        //		Assert.IsTrue(Utils.IsSorted(finalizer));

        //		int fcnt = 0;
        //		for (int i = 0, icnt = finalizer.Length; i < icnt; ++i)
        //		{
        //			if (Utils.AddressSearch(instances,finalizer[i]) < 0) continue;
        //			++fcnt;
        //		}

        //		Utils.SetAddressBit(rootedAry, instances, Utils.RootBits.Rooted);
        //		Utils.SetAddressBit(finalizer, instances, Utils.RootBits.Finalizer);

        //		TestContext.WriteLine("INSTANCE COUNT: " + Utils.LargeNumberString(instances.Length));
        //		TestContext.WriteLine("ROOTED ARY COUNT: " + Utils.LargeNumberString(rootedAry.Length));
        //		TestContext.WriteLine("UNROOTED ARY COUNT: " + Utils.LargeNumberString(done.Count));
        //		TestContext.WriteLine("FINALIZER COUNT: " + Utils.LargeNumberString(finalizer.Length));

        //		var markedRoooted = 0;
        //		var markedFinalizer = 0;
        //		for (int i = 0, icnt = instances.Length; i < icnt; ++i)
        //		{
        //			var addr = instances[i];
        //			if (Utils.IsRooted(addr)) ++markedRoooted;
        //			if (Utils.IsFinalizer(addr)) ++markedFinalizer;
        //		}
        //		TestContext.WriteLine("MARKED ROOTED COUNT: " + Utils.LargeNumberString(markedRoooted));
        //		TestContext.WriteLine("MARKED FINALIZER COUNT: " + Utils.LargeNumberString(markedFinalizer));



        //	} // using dump
        //}

        //[TestMethod]
        //public void TestIndexing2()
        //{
        //	string error;
        //	string dumpPath = Setup.RecentAdhocList[0];
        //	var dmp = OpenDump(dumpPath);
        //	var fileMoniker = new DumpFileMoniker(dumpPath);
        //	var strIds = new StringIdDct();

        //	using (dmp)
        //	{
        //		var heap = dmp.Heap;
        //		var runtm = dmp.Runtimes[0];
        //		ulong[] instances = DumpIndexer.GetHeapAddressesCount(heap);
        //		string[] typeNames = DumpIndexer.GetTypeNames(heap, out error);
        //		var rootAddrInfo = ClrtRootInfo.GetRootAddresses(0, runtm, heap, typeNames, strIds, fileMoniker, out error);
        //		Utils.GetRealAddressesInPlace(rootAddrInfo.Item1);
        //		Utils.GetRealAddressesInPlace(rootAddrInfo.Item2);

        //		Assert.IsNull(error);
        //		Assert.IsTrue(Utils.AreAddressesSorted(rootAddrInfo.Item1));
        //		Assert.IsTrue(Utils.AreAddressesSorted(rootAddrInfo.Item2));

        //		var rootAddresses = rootAddrInfo.Item1;

        //		//Assert.IsTrue(Utils.AreAllInExcept0(rootAddresses, rootAddrInfo.Item1));
        //		//Assert.IsTrue(Utils.AreAllInExcept0(rootAddresses, rootAddrInfo.Item2));
        //		Assert.IsTrue(Utils.AreAllDistinct(rootAddresses));
        //		Assert.IsTrue(Utils.IsSorted(rootAddresses));

        //		rootAddresses = Utils.GetRealAddressesInPlace(rootAddresses);
        //		Bitset bitset = new Bitset(instances.Length);
        //		var rootAddressNdxs = Utils.GetAddressIndices(rootAddresses, instances);

        //		string path1 = fileMoniker.GetFilePath(0, Constants.MapParentFieldsRootedPostfix);
        //		string path2 = fileMoniker.GetFilePath(0, Constants.MapFieldParentsRootedPostfix);
        //		//bool result = References.GetRefrences(heap, rootAddressNdxs, instances, bitset, path1, path2, out error);
        //		bool result = References.CreateReferences2(0, heap, rootAddrInfo.Item1, instances, bitset, fileMoniker, null, out error);

        //		int markRootCount = Utils.SetAddressBitIfSet(instances, rootAddrInfo.Item2, Utils.RootBits.Rooted);
        //		int markFnlzCount = Utils.SetAddressBit(rootAddrInfo.Item2, instances, Utils.RootBits.Finalizer);

        //		Assert.IsTrue(result);
        //		Assert.IsNull(error);

        //		int[] head1;
        //		int[][] lists1;
        //		result = References.LoadReferences(path1, out head1, out lists1, out error);
        //		Assert.IsTrue(result);
        //		Assert.IsNull(error);
        //		Assert.IsTrue(Utils.IsSorted(head1));

        //		for (int i = 0, icnt = lists1.Length; i < icnt; ++i)
        //		{
        //			var lst = lists1[i];
        //			Assert.IsTrue(Utils.IsSorted(lst));
        //			Assert.IsTrue(Utils.AreAllDistinct(lst));
        //			Assert.IsTrue(Utils.DoesNotContain(lst,Int32.MaxValue));
        //		}

        //		string path1a = fileMoniker.GetFilePath(0, ".`PARENTFIELDSROOTED[0].BEFORE.NOFIN.bin");
        //		int[] head1a;
        //		int[][] lists1a;
        //		result = References.LoadReferences(path1a, out head1a, out lists1a, out error);
        //		Assert.IsTrue(result);
        //		Assert.IsNull(error);

        //		for (int i = 0, icnt = lists1a.Length; i < icnt; ++i)
        //		{
        //			var lst = lists1a[i];
        //			Assert.IsTrue(Utils.IsSorted(lst));
        //			//Assert.IsTrue(Utils.AreAllDistinct(lst));
        //			Assert.IsTrue(Utils.DoesNotContain(lst, Int32.MaxValue));
        //			Assert.IsTrue(Utils.Contains(lst,lists1[i]));
        //		}

        //		int[] head2;
        //		int[][] lists2;
        //		result = References.LoadReferences(path2, out head2, out lists2, out error);
        //		Assert.IsTrue(result);
        //		Assert.IsNull(error);
        //		Assert.IsTrue(Utils.IsSorted(head2));

        //		for (int i = 0, icnt = lists2.Length; i < icnt; ++i)
        //		{
        //			var lst = lists2[i];
        //			Assert.IsTrue(Utils.IsSorted(lst));
        //			Assert.IsTrue(Utils.AreAllDistinct(lst));
        //			Assert.IsTrue(Utils.DoesNotContain(lst, Int32.MaxValue));
        //		}


        //		string path2a = fileMoniker.GetFilePath(0, ".`FIELDPARENTSROOTED[0].BEFORE.NOFIN.bin");
        //		int[] head2a;
        //		int[][] lists2a;
        //		result = References.LoadReferences(path2a, out head2a, out lists2a, out error);
        //		Assert.IsTrue(result);
        //		Assert.IsNull(error);

        //		for (int i = 0, icnt = lists2a.Length; i < icnt; ++i)
        //		{
        //			var lst = lists2a[i];
        //			Assert.IsTrue(Utils.IsSorted(lst));
        //			Assert.IsTrue(Utils.DoesNotContain(lst, Int32.MaxValue));
        //			Assert.IsTrue(Utils.Contains(lst, lists2[i]));
        //		}

        //		TestContext.WriteLine("INSTANCE COUNT: " + Utils.LargeNumberString(instances.Length));
        //		//TestContext.WriteLine("ROOTED ARY COUNT: " + Utils.LargeNumberString(rootedAry.Length));
        //		//TestContext.WriteLine("UNROOTED ARY COUNT: " + Utils.LargeNumberString(done.Count));
        //		//TestContext.WriteLine("FINALIZER COUNT: " + Utils.LargeNumberString(finalizer.Length));

        //		var markedRoooted = 0;
        //		var markedFinalizer = 0;
        //		for (int i = 0, icnt = instances.Length; i < icnt; ++i)
        //		{
        //			var addr = instances[i];
        //			if (Utils.IsRooted(addr)) ++markedRoooted;
        //			if (Utils.IsFinalizer(addr)) ++markedFinalizer;
        //		}
        //		TestContext.WriteLine("MARKED ROOTED COUNT: " + Utils.LargeNumberString(markedRoooted));
        //		TestContext.WriteLine("MARKED FINALIZER COUNT: " + Utils.LargeNumberString(markedFinalizer));

        //	} // using dump
        //}

        //[TestMethod]
        //public void TestIndexingBig()
        //{
        //	string error;
        //	string dumpPath = @"C:\WinDbgStuff\Dumps\Analytics\BigOne\Analytics11_042015_2.Big.dmp";
        //	var dmp = OpenDump(dumpPath);
        //	var fileMoniker = new DumpFileMoniker(dumpPath);
        //	var strIds = new StringIdDct();

        //	using (dmp)
        //	{
        //		var heap = dmp.Heap;
        //		var runtm = dmp.Runtimes[0];
        //		ulong[] instances = DumpIndexer.GetHeapAddressesCount(heap);
        //		string[] typeNames = DumpIndexer.GetTypeNames(heap, out error);
        //		var rootAddrInfo = ClrtRootInfo.GetRootAddresses(0, runtm, heap, typeNames, strIds, fileMoniker, out error);
        //		Utils.GetRealAddressesInPlace(rootAddrInfo.Item1);
        //		Utils.GetRealAddressesInPlace(rootAddrInfo.Item2);

        //		Assert.IsNull(error);
        //		Assert.IsTrue(Utils.AreAddressesSorted(rootAddrInfo.Item1));
        //		Assert.IsTrue(Utils.AreAddressesSorted(rootAddrInfo.Item2));

        //		var rootAddresses = rootAddrInfo.Item1;

        //		//Assert.IsTrue(Utils.AreAllInExcept0(rootAddresses, rootAddrInfo.Item1));
        //		//Assert.IsTrue(Utils.AreAllInExcept0(rootAddresses, rootAddrInfo.Item2));
        //		Assert.IsTrue(Utils.AreAllDistinct(rootAddresses));
        //		Assert.IsTrue(Utils.IsSorted(rootAddresses));

        //		rootAddresses = Utils.GetRealAddressesInPlace(rootAddresses);
        //		Bitset bitset = new Bitset(instances.Length);
        //		var rootAddressNdxs = Utils.GetAddressIndices(rootAddresses, instances);

        //		string path1 = fileMoniker.GetFilePath(0, Constants.MapParentFieldsRootedPostfix);
        //		string path2 = fileMoniker.GetFilePath(0, Constants.MapFieldParentsRootedPostfix);

        //		//bool result = References.CreateReferences2(0, heap, rootAddrInfo.Item1, instances, bitset, fileMoniker, null, out error);
        //		//Assert.IsTrue(result);
        //		Assert.IsNull(error);
        //	} // using dump
        //}

        private bool DumpReferences(string path, SortedDictionary<ulong, List<ulong>> refs, ulong[] instances,
            out string error)
        {
            error = null;
            BinaryWriter bw = null;
            try
            {
                bw = new BinaryWriter(File.Open(path, FileMode.Create));
                bw.Write(refs.Count);
                foreach (var kv in refs)
                {
                    var ndx = Array.BinarySearch(instances, kv.Key);
                    Debug.Assert(ndx >= 0);
                    var lst = kv.Value;
                    bw.Write(ndx);
                    bw.Write(lst.Count);
                    for (int i = 0, icnt = lst.Count; i < icnt; ++i)
                    {
                        ndx = Array.BinarySearch(instances, lst[i]);
                        Debug.Assert(ndx >= 0);
                        bw.Write(ndx);
                    }
                }
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

        #endregion roots/instances/references

        #region threads

        [TestMethod]
        public void TestBlocking()
        {
            var dmp = OpenDump(@"C:\WinDbgStuff\Dumps\Analytics\AnalyticsMemory\A2_noDF.dmp");
            uint ThreadId = 13680;
            using (dmp)
            {
                ClrThread thread;
                var threads = DumpIndexer.GetThreads(dmp.Runtime);
                for (int i = 0, icnt = threads.Length; i < icnt; ++i)
                {
                    if (threads[i].OSThreadId == ThreadId)
                    {
                        thread = threads[i];
                    }
                }

                var heap = dmp.Heap;
                BlockingObject[] freeBlks;
                var blocks = DumpIndexer.GetBlockingObjectsEx(heap, out freeBlks);

                List<BlockingObject> obloks = new List<BlockingObject>(256);
                List<BlockingObject> wbloks = new List<BlockingObject>(256);
                List<BlockingObject> wwbloks = new List<BlockingObject>(256);
                HashSet<string> typeSet = new HashSet<string>();
                int nullTypeCnt = 0;
                for (int i = 0, icnt = blocks.Length; i < icnt; ++i)
                {
                    var block = blocks[i];

                    if (block.HasSingleOwner && block.Taken && block.Owner != null && block.Owner.OSThreadId == ThreadId)
                    {
                        wwbloks.Add(block);
                        var clrType = heap.GetObjectType(block.Object);
                        if (clrType != null)
                            typeSet.Add(clrType.Name);
                        else
                            ++nullTypeCnt;
                    }
                    for (int j = 0, jcnt = block.Owners.Count; j < jcnt; ++j)
                    {
                        if (block.Owners[j] == null) continue;
                        if (block.Owners[j].OSThreadId == ThreadId)
                        {
                            obloks.Add(block);
                            var clrType = heap.GetObjectType(block.Object);
                            if (clrType != null)
                                typeSet.Add(clrType.Name);
                            else
                                ++nullTypeCnt;
                        }
                    }
                    for (int j = 0, jcnt = block.Waiters.Count; j < jcnt; ++j)
                    {
                        if (block.Waiters[j] == null) continue;
                        if (block.Waiters[j].OSThreadId == ThreadId)
                        {
                            wbloks.Add(block);
                            var clrType = heap.GetObjectType(block.Object);
                            if (clrType != null)
                                typeSet.Add(clrType.Name);
                            else
                                ++nullTypeCnt;

                        }
                    }
                }
                int notFreeCnt = 0;
                for (int i = 0, icnt = freeBlks.Length; i < icnt; ++i)
                {
                    var blk = freeBlks[i];
                    var clrType = heap.GetObjectType(blk.Object);
                    if (clrType != null && clrType.Name != "Free")
                    {
                        ++notFreeCnt;
                    }
                }
                Assert.IsTrue(notFreeCnt == 0);
            }
        }

        [TestMethod]
        public void TestThreadInfo()
        {
            string dumpPath = Setup.DumpsFolder + @"\Analytics\Ellerston\Eze.Analytics.Svc_170309_130146.BIG.dmp";
            string error = null;
            StreamWriter sw = null;
            var rootEqCmp = new ClrRootEqualityComparer();
            var rootCmp = new ClrRootObjCmp();

            using (var clrDump = OpenDump(dumpPath))
            {
                try
                {
                    var runtime = clrDump.Runtimes[0];
                    var heap = runtime.Heap;
                    var stackTraceLst = new List<ClrStackFrame>();
                    var threads = DumpIndexer.GetThreads(runtime);
                    var threadLocalDeadVars = new ClrRoot[threads.Length][];
                    var threadLocalAliveVars = new ClrRoot[threads.Length][];
                    var threadFrames = new ClrStackFrame[threads.Length][];

                    for (int i = 0, icnt = threads.Length; i < icnt; ++i)
                    {
                        var t = threads[i];
                        stackTraceLst.Clear();
                        foreach (var st in t.EnumerateStackTrace())
                        {
                            stackTraceLst.Add(st);
                            if (stackTraceLst.Count > 100) break;
                        }
                        threadLocalAliveVars[i] = t.EnumerateStackObjects(false).ToArray();
                        var all = t.EnumerateStackObjects(true).ToArray();
                        threadLocalDeadVars[i] = all.Except(threadLocalAliveVars[i], rootEqCmp).ToArray();
                        threadFrames[i] = stackTraceLst.ToArray();
                    }

                    var path = DumpFileMoniker.GetAndCreateOutFolder(dumpPath, out error) + Path.DirectorySeparatorChar + "TestThreadsInfo.txt";
                    sw = new StreamWriter(path);
                    HashSet<ulong> locals = new HashSet<ulong>();
                    var localAliveDups = new HashSet<ClrRoot>(rootEqCmp);
                    var localDeadDups = new HashSet<ClrRoot>(rootEqCmp);
                    int dupLocals = 0;
                    for (int i = 0, icnt = threads.Length; i < icnt; ++i)
                    {
                        ClrThread th = threads[i];
                        var frm = threadFrames[i];
                        var vars = threadLocalDeadVars[i];
                        sw.WriteLine(th.OSThreadId.ToString() + "/" + th.ManagedThreadId + "  " + Utils.RealAddressString(th.Address));
                        sw.WriteLine("    alive locals");
                        for (int j = 0, jcnt = threadLocalAliveVars[i].Length; j < jcnt; ++j)
                        {
                            ClrRoot root = threadLocalAliveVars[i][j];
                            if (!locals.Add(root.Object))
                            {
                                localAliveDups.Add(root);
                                ++dupLocals;
                            }
                            ClrType clrType = heap.GetObjectType(root.Object);
                            sw.Write("    " + Utils.RealAddressStringHeader(root.Address) + "    " + Utils.RealAddressStringHeader(root.Object));
                            sw.Write(" " + root.Kind.ToString() + " ");
                            if (clrType != null)
                                sw.WriteLine(clrType.Name);
                            else
                                sw.WriteLine();
                        }

                        sw.WriteLine("    dead locals");
                        for (int j = 0, jcnt = threadLocalDeadVars[i].Length; j < jcnt; ++j)
                        {
                            ClrRoot root = threadLocalDeadVars[i][j];
                            if (!locals.Add(root.Object))
                            {
                                localDeadDups.Add(root);
                                ++dupLocals;
                            }
                            ClrType clrType = heap.GetObjectType(root.Object);
                            sw.Write("    " + Utils.RealAddressStringHeader(root.Address) + "    " + Utils.RealAddressStringHeader(root.Object));
                            if (clrType != null)
                                sw.WriteLine(clrType.Name);
                            else
                                sw.WriteLine();
                        }
                        for (int j = 0, jcnt = threadFrames[i].Length; j < jcnt; ++j)
                        {
                            ClrStackFrame fr = threadFrames[i][j];
                            if (fr.Method != null)
                            {
                                string fullSig = fr.Method.GetFullSignature();
                                if (fullSig == null)
                                    fullSig = fr.Method.Name;
                                if (fullSig == null) fullSig = "UNKNOWN";
                                sw.WriteLine("  " + Utils.RealAddressStringHeader(fr.StackPointer) + "  " + Utils.RealAddressStringHeader(fr.InstructionPointer)
                                                + Utils.RealAddressStringHeader(fr.Method.NativeCode)
                                                + fullSig);

                            }
                            else
                            {
                                sw.WriteLine("  METHOD UKNOWN");
                            }
                            if (!string.IsNullOrEmpty(fr.DisplayString))
                                sw.WriteLine("  " + fr.DisplayString);
                            else
                                sw.WriteLine("  ???");
                        }
                    }

                    var localAliveDupsAry = localAliveDups.ToArray();
                    var localDeadDupsAry = localDeadDups.ToArray();



                    TestContext.WriteLine("LOCAL OBJECT DUPLICATE COUNT: " + dupLocals);

                }
                catch (Exception ex)
                {
                    error = Utils.GetExceptionErrorString(ex);
                    Assert.IsTrue(false, error);
                }
                finally
                {
                    sw?.Close();
                }
            }
        }

        static (int count, double sum) Tally(IEnumerable<double> values)
        {
            int count = 0;
            double sum = 0.0;
            foreach (var value in values)
            {
                count++;
                sum += value;
            }
            return (count, sum);
        }


        #endregion threads

        #region types

        [TestMethod]
        public void TestTypeSizesAndGenerations()
        {
            string dumpPath = Setup.DumpsFolder + @"\Analytics\Ellerston\Eze.Analytics.Svc_170309_130146.BIG.dmp";
            string typeName = "Free";

            var dmp = OpenDump(dumpPath);

            using (dmp)
            {
                var heap = dmp.Heap;
                var segs = heap.Segments;
                var addresses = new List<ulong>(1024 * 1024);
                var sizes = new List<ulong>(1024 * 1024);
                int[] genHistogram = new int[4];
                int[] sizeHistogram = new int[Utils.SizeDistributionLenght];

                for (int i = 0, icnt = segs.Count; i < icnt; ++i)
                {
                    var seg = segs[i];
                    ulong addr = seg.FirstObject;
                    while (addr != 0ul)
                    {
                        var clrType = heap.GetObjectType(addr);
                        if (clrType == null || !Utils.SameStrings(clrType.Name, typeName)) goto NEXT_OBJECT;
                        var sz = clrType.GetSize(addr);
                        Utils.AddSizeDistribution(sizeHistogram, sz);
                        var gen = heap.GetGeneration(addr);
                        genHistogram[gen] += 1;
                        addresses.Add(addr);
                        sizes.Add(sz);
                        NEXT_OBJECT:
                        addr = seg.NextObject(addr);
                    }
                }
                var adrAry = addresses.ToArray();
                addresses = null;
                var szAry = sizes.ToArray();
                sizes = null;
                Array.Sort(szAry, adrAry, new Utils.AddressCmpDesc());

                string outPath = Setup.DumpsFolder + @"\Analytics\Ellerston\Eze.Analytics.Svc_170309_130146.BIG.dmp.map\ad-hoc.queries\TypeSizesAndGenerations."
                                        + DumpFileMoniker.GetValidFileName(typeName) + ".txt";
                StreamWriter sw = null;
                try
                {
                    sw = new StreamWriter(outPath);
                    sw.Write("GENERATION COUNTS (0 1 2 LOH): ");
                    for (int i = 0; i < 4; ++i)
                        sw.Write(" " + genHistogram[i]);
                    sw.WriteLine();
                    sw.WriteLine("SIZE DISTRIBUTION:");
                    ulong twoPower = 32;
                    for (int i = 0; i < sizeHistogram.Length; ++i)
                    {
                        sw.WriteLine("<= " + Utils.SizeString(twoPower) + ": " + Utils.CountString(sizeHistogram[i]));
                        twoPower *= 2;
                    }

                    sw.WriteLine();
                    sw.WriteLine("ADDRESSES AND SIZES:");

                    for (int i = 0; i < szAry.Length; ++i)
                    {
                        sw.WriteLine(Utils.RealAddressStringHeader(adrAry[i]) + Utils.SizeString(szAry[i]));
                    }
                }
                catch (Exception ex)
                {
                    Assert.IsTrue(false, ex.ToString());
                }
                finally
                {
                    sw?.Close();
                }
            } // using dump
        }

        [TestMethod]
        public void TestGetTypeAddressList()
        {
            string typeName = "ECS.Common.HierarchyCache.Structure.WTPortfolio";
            string error = null;
            using (var clrDump = OpenDump(@"D:\Jerzy\WinDbgStuff\dumps\Modeling\Local\Eze.Modeling.Svc.exe_170421_141853.dmp"))
            {
                try
                {
                    var runtime = clrDump.Runtimes[0];
                    var heap = runtime.Heap;
                    var segs = heap.Segments;
                    for (int i = 0, icnt = segs.Count; i < icnt; ++i)
                    {
                        var seg = segs[i];
                        ulong addr = seg.FirstObject;
                        while (addr != 0ul)
                        {
                            var clrType = heap.GetObjectType(addr);
                            if (clrType == null || !Utils.SameStrings(typeName, clrType.Name)) goto NEXT_OBJECT;
                            var fld = clrType.GetFieldByName("prtName");
                            string val = (string)fld.GetValue(addr, false, true);

                            NEXT_OBJECT:
                            addr = seg.NextObject(addr);
                        }
                    }
                }
                catch (Exception ex)
                {
                    error = Utils.GetExceptionErrorString(ex);
                    Assert.IsTrue(false, error);
                }
            }
        }

        [TestMethod]
        public void TestGetTypeAddressListWithFieldValue()
        {
            string[] typeNames = new string[] {
                "ECS.Common.HierarchyCache.Structure.CashPosition",
                "ECS.Common.HierarchyCache.Structure.CashEffectPosition",
                "ECS.Common.HierarchyCache.Structure.RealPosition",
                 };
            string[] fldValues = new string[] { "T846078023", "T845520386" };
            string fldName = "posID";
            string altfldName = "positionID";
            string error = null;
            var addrTypes = new List<Tuple<string, string, string>>();
            var posIDs = new List<Tuple<string, string, string>>();
            var dct = new SortedDictionary<string, List<KeyValuePair<string, string>>>(StringComparer.OrdinalIgnoreCase);

            using (var clrDump = OpenDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Viking\AnalyticsVikingForAlex.dmp"))
            {
                try
                {
                    var runtime = clrDump.Runtimes[0];
                    var heap = runtime.Heap;
                    var segs = heap.Segments;
                    for (int i = 0, icnt = segs.Count; i < icnt; ++i)
                    {
                        var seg = segs[i];
                        ulong addr = seg.FirstObject;
                        while (addr != 0ul)
                        {
                            var clrType = heap.GetObjectType(addr);
                            if (clrType == null) goto NEXT_OBJECT;
                            if (!typeNames.Contains(clrType.Name)) goto NEXT_OBJECT;

                            var fld = clrType.GetFieldByName(fldName);
                            if (fld == null)
                                fld = clrType.GetFieldByName(altfldName);
                            string val = (string)fld.GetValue(addr, false, true);
                            var info = new KeyValuePair<string, string>(clrType.Name, Utils.RealAddressString(addr));
                            List<KeyValuePair<string, string>> lst;
                            if (dct.TryGetValue(val, out lst))
                            {
                                lst.Add(info);
                            }
                            else
                            {
                                dct.Add(val, new List<KeyValuePair<string, string>>() { info });
                            }



                            //posIDs.Add(new Tuple<string, string, string>(clrType.Name,val, Utils.RealAddressString(addr)));
                            //if(fldValues.Contains(val,StringComparer.OrdinalIgnoreCase))
                            //{
                            //    addrTypes.Add(new Tuple<string, string,string>(clrType.Name, Utils.RealAddressString(addr), val));
                            //}
                            NEXT_OBJECT:
                            addr = seg.NextObject(addr);
                        }
                    }

                    int maxCnt = 0;
                    int dupCnt = 0;
                    int totDupCnt = 0;
                    int[] typeCnts = new int[3];
                    StringBuilder sb = new StringBuilder(4096);
                    foreach (var kv in dct)
                    {
                        if (kv.Value.Count > 1)
                        {
                            sb.AppendLine(kv.Key);

                            totDupCnt += kv.Value.Count;
                            ++dupCnt;
                            if (maxCnt < kv.Value.Count)
                                maxCnt = kv.Value.Count;
                            foreach (var kvi in kv.Value)
                            {
                                switch (kvi.Key)
                                {
                                    case "ECS.Common.HierarchyCache.Structure.CashPosition":
                                        sb.Append("   CashPosition: ").AppendLine(kvi.Value);
                                        typeCnts[0] += 1;
                                        break;
                                    case "ECS.Common.HierarchyCache.Structure.CashEffectPosition":
                                        sb.Append("   CashEffectPosition: ").AppendLine(kvi.Value);
                                        typeCnts[1] += 1;
                                        break;
                                    case "ECS.Common.HierarchyCache.Structure.RealPosition":
                                        sb.Append("   RealPosition: ").AppendLine(kvi.Value);
                                        typeCnts[2] += 1;
                                        break;
                                }
                            }
                        }
                    }
                    var rep = sb.ToString();
                }
                catch (Exception ex)
                {
                    error = Utils.GetExceptionErrorString(ex);
                    Assert.IsTrue(false, error);
                }
            }
        }

        [TestMethod]
        public void TestGetTypeAddressList2()
        {
            string typeName = "ECS.Common.HierarchyCache.Structure.IndirectRealtimePrice";
            string error = null;
            List<Tuple<string, int, string>> lst = new List<Tuple<string, int, string>>();
            using (var clrDump = OpenDump(@"D:\Jerzy\WinDbgStuff\dumps\Analytics\Centurion\AnalyticsCenturion.4.18.17.dmp"))
            {
                try
                {
                    var runtime = clrDump.Runtimes[0];
                    var heap = runtime.Heap;
                    var segs = heap.Segments;
                    for (int i = 0, icnt = segs.Count; i < icnt; ++i)
                    {
                        var seg = segs[i];
                        ulong addr = seg.FirstObject;
                        while (addr != 0ul)
                        {
                            var clrType = heap.GetObjectType(addr);
                            if (clrType == null || !Utils.SameStrings(typeName, clrType.Name)) goto NEXT_OBJECT;
                            var fld = clrType.GetFieldByName(@"<FieldSourceId>k__BackingField");
                            var val = fld.GetValue(addr, false, false);
                            if (val is Int32)
                            {
                                var intVal = (int)val;
                                if (intVal == 16 || intVal == 18)
                                {
                                    var symbol = "???";
                                    var fld2 = clrType.GetFieldByName(@"positionSpecificPriceDetails");
                                    var val2 = fld2.GetValue(addr, false, false);
                                    ulong addrVal2 = val2 != null ? (ulong)val2 : 0UL;
                                    var typeVal2 = heap.GetObjectType(addrVal2);
                                    if (typeVal2 != null)
                                    {
                                        var fld3 = typeVal2.Name.EndsWith(".RealPosition")
                                            ? typeVal2.GetFieldByName(@"sec")
                                            : typeVal2.GetFieldByName(@"_sec");
                                        if (fld3 == null)
                                        {
                                            goto SET_RESULT;
                                        }
                                        var val3 = fld3.GetValue(addrVal2, false, false);
                                        var addrVal3 = val3 != null ? (ulong)val3 : 0UL;
                                        var typeVal3 = heap.GetObjectType(addrVal3);
                                        if (typeVal3 != null)
                                        {
                                            var fld4 = typeVal3.GetFieldByName(@"securitySimpleState");
                                            var val4 = fld4.GetValue(addrVal3, false, false);
                                            var addrVal4 = val4 != null ? (ulong)val4 : 0UL;
                                            var typeVal4 = heap.GetObjectType(addrVal4);
                                            if (typeVal4 != null)
                                            {
                                                var fld5 = typeVal4.GetFieldByName(@"Symbol");
                                                symbol = fld5.GetValue(addrVal4, false, true).ToString();
                                            }
                                        }
                                    }

                                    SET_RESULT:
                                    var result = new Tuple<string, int, string>(Utils.RealAddressString(addr), intVal, symbol);
                                    lst.Add(result);
                                }
                            }
                            NEXT_OBJECT:
                            addr = seg.NextObject(addr);
                        }
                    }
                }
                catch (Exception ex)
                {
                    error = Utils.GetExceptionErrorString(ex);
                    Assert.IsTrue(false, error);
                }
            }
        }
        #endregion types

        #region type references

        [TestMethod]
        public void TestTypeReferences()
        {
            string dumpPath = Setup.DumpsFolder + @"\Analytics\Ellerstone\Eze.Analytics.Svc_170309_130146.BIG.map";
            string typeName = "System.Collections.Concurrent.ConcurrentDictionary<";

            var dmp = OpenDump(dumpPath);
            using (dmp)
            {
                var heap = dmp.Heap;
                var segs = heap.Segments;
                var fieldAddrOffsetList = new List<KeyValuePair<ulong, int>>(64);
                for (int i = 0, icnt = segs.Count; i < icnt; ++i)
                {
                    var seg = segs[i];
                    ulong addr = seg.FirstObject;
                    while (addr != 0ul)
                    {
                        var clrType = heap.GetObjectType(addr);
                        if (clrType == null) goto NEXT_OBJECT;
                        if (clrType.Name.StartsWith(typeName, StringComparison.Ordinal))
                        {
                            fieldAddrOffsetList.Clear();
                            clrType.EnumerateRefsOfObjectCarefully(addr, (address, off) =>
                            {
                                fieldAddrOffsetList.Add(new KeyValuePair<ulong, int>(address, off));
                            });
                        }

                        int fieldCount = clrType.Fields.Count;

                        NEXT_OBJECT:
                        addr = seg.NextObject(addr);
                    }
                }
            } // using dump
        }

        [TestMethod]
        public void TestArrayReferences()
        {
            string dumpPath = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.dmp";
            var dmp = OpenDump(dumpPath);
            using (dmp)
            {
                var heap = dmp.Heap;
                var segs = heap.Segments;
                var fieldAddrOffsetList = new List<KeyValuePair<ulong, int>>(64);
                for (int i = 0, icnt = segs.Count; i < icnt; ++i)
                {
                    var seg = segs[i];
                    ulong addr = seg.FirstObject;
                    while (addr != 0ul)
                    {
                        var clrType = heap.GetObjectType(addr);
                        if (clrType == null || !clrType.IsArray) goto NEXT_OBJECT;
                        fieldAddrOffsetList.Clear();
                        clrType.EnumerateRefsOfObjectCarefully(addr, (address, off) =>
                        {
                            fieldAddrOffsetList.Add(new KeyValuePair<ulong, int>(address, off));
                        });

                        NEXT_OBJECT:
                        addr = seg.NextObject(addr);
                    }
                }
            } // using dump
        }

        [TestMethod]
        public void TestEnumerateTypeReferences()
        {
            string dumpPath = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.dmp";
            ulong addr = 0x00000000800a8c30;

            var dmp = OpenDump(dumpPath);
            using (dmp)
            {
                var heap = dmp.Heap;
                var segs = heap.Segments;
                var fieldAddrOffsetList = new List<KeyValuePair<ulong, int>>(64);
                fieldAddrOffsetList.Clear();
                var clrType = heap.GetObjectType(addr);
                Assert.IsNotNull(clrType);
                clrType.EnumerateRefsOfObjectCarefully(addr, (address, off) =>
                {
                    fieldAddrOffsetList.Add(new KeyValuePair<ulong, int>(address, off));
                });

                int fieldCount = clrType.Fields.Count;

            } // using dump
        }

        [TestMethod]
        public void TestAlternativeTypeReferences()
        {
            string typeName = "Eze.Server.Common.Pulse.Common.Types.PositionLevelCache";
            string dumpPath = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.dmp";
            var dmp = OpenDump(dumpPath);
            HashSet<string> set = new HashSet<string>();
            using (dmp)
            {
                var heap = dmp.Heap;
                var segs = heap.Segments;
                for (int i = 0, icnt = segs.Count; i < icnt; ++i)
                {
                    var seg = segs[i];
                    ulong addr = seg.FirstObject;
                    while (addr != 0ul)
                    {
                        var clrType = heap.GetObjectType(addr);
                        if (clrType == null || !Utils.SameStrings(clrType.Name,typeName)) goto NEXT_OBJECT;

                        //var fld = clrType.GetFieldByName("owner");
                        var fld = clrType.Fields[5];
                        (ClrType fldType, ClrElementKind fldKind) = TypeExtractor.GetReferenceFieldRealTypeAndKind(heap, addr, fld);
                        if (set.Add(fldType.Name))
                        {
                            int a = 1;
                        }

                        NEXT_OBJECT:
                        addr = seg.NextObject(addr);
                    }
                }

            } // using dump
        }


        [TestMethod]
        public void TestCompareInstanceFiles()
        {
            string path1 = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.dmp.map\analyticsdump111.dlk.dmp.`INSTANCES[0].bin";
            string path2 = @"D:\Jerzy\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.new2.dmp.map\analyticsdump111.dlk.new2.dmp.`INSTANCES[0].bin";

            Assert.IsTrue(File.Exists(path1) && File.Exists(path2));

            FileInfo fi1 = new FileInfo(path1);
            long file1len = fi1.Length;
            FileInfo fi2 = new FileInfo(path2);
            long file2len = fi2.Length;
            Assert.AreEqual(file1len, file2len);

            string error;
            ulong[] ary1 = Utils.ReadUlongArray(path1, out error);
            Assert.IsNull(error);
            ulong[] ary2 = Utils.ReadUlongArray(path2, out error);
            Assert.IsNull(error);
            Assert.AreEqual(ary1.Length, ary2.Length);

            (int rootedCount1, int finalizerCount1) = get_flags_counts(ary1);
            (int rootedCount2, int finalizerCount2) = get_flags_counts(ary2);
            bool sameAddresses = same_addresses(ary1, ary2);

            TestContext.WriteLine("[1] : " + path1);
            TestContext.WriteLine("[2] : " + path2);
            TestContext.WriteLine("SAME INSTANCE ADDRESSES: " + sameAddresses);
            TestContext.WriteLine("[1] INSTANCE COUNT: " + Utils.CountString(ary1.Length));
            TestContext.WriteLine("[2] INSTANCE COUNT: " + Utils.CountString(ary1.Length));
            TestContext.WriteLine("[1] ROOTED    COUNT: " + Utils.CountString(rootedCount1));
            TestContext.WriteLine("[2] ROOTED    COUNT: " + Utils.CountString(rootedCount2));
            TestContext.WriteLine("[1] FINALIZER COUNT: " + Utils.CountString(finalizerCount1));
            TestContext.WriteLine("[2] FINALIZER COUNT: " + Utils.CountString(finalizerCount2));


        }


        bool has_rooted_flag(ulong addr)
        {
            return (addr & Utils.RootBits.Rooted) > 0;
        }

        bool has_finalizer_flag(ulong addr)
        {
            return (addr & Utils.RootBits.Finalizer) > 0;
        }

        ValueTuple<int, int> get_flags_counts(ulong[] ary)
        {
            int rootedCount = 0, finalizerCount = 0;
            for (int i = 0, icnt = ary.Length; i < icnt; ++i)
            {
                ulong val = ary[i];
                if (has_rooted_flag(val)) ++rootedCount;
                if (has_finalizer_flag(val)) ++finalizerCount;
            }
            return (rootedCount, finalizerCount);
        }

        bool same_addresses(ulong[] ary1, ulong[] ary2)
        {
            if (ary1.Length != ary2.Length) return false;
            for (int i = 0, icnt = ary1.Length; i < icnt; ++i)
            {
                ulong val1 = ary1[i] & Utils.RootBits.AddressMask;
                ulong val2 = ary2[i] & Utils.RootBits.AddressMask;
                if (val1 != val2) return false;
            }
            return true;
        }

        [TestMethod]
        public void TestCompareReferenceFiles()
        {
            string error;
            BinaryReader br = null;
            string path0 = @"C:\WinDbgStuff\Dumps\Analytics\Highline\analyticsdump111.dlk.dmp.map\analyticsdump111.dlk.dmp.`INSTANCES[0].bin";
            string path1 = @"C:\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.dmp.map\analyticsdump111.dlk.dmp.`REFSOBJECTFIELD[0].bin";
            string path2 = @"C:\WinDbgStuff\Dumps\Analytics\Highline\analyticsdump111.dlk.new2.dmp.map\analyticsdump111.dlk.new2.dmp.`FWDREFS[0].bin";
            string path3 = @"C:\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.new2.dmp.map\analyticsdump111.dlk.new2.dmp.`FWDREFOFFSETS[0].bin";
            string path4 = @"C:\WinDbgStuff\dumps\Analytics\Highline\analyticsdump111.dlk.new2.dmp.map\analyticsdump111.dlk.new2.dmp.`INSTANCES[0].bin";

			Assert.IsTrue(File.Exists(path0));
			Assert.IsTrue(File.Exists(path1));
			Assert.IsTrue(File.Exists(path2));
			Assert.IsTrue(File.Exists(path3));
			Assert.IsTrue(File.Exists(path4));

            StreamWriter sw = null, sw1 = null;
            List<int> notEqualCounts = new List<int>();
            List<int> diffsRefs = new List<int>();
            List<int> diffsFixedRefs = new List<int>();
            ulong[] instances_O = null;
            ulong[] instances_N = null;
            try
            {
                instances_O = Utils.ReadUlongArray(path0, out error);
                instances_N = Utils.ReadUlongArray(path4, out error);
                int[] headAry_O;
                int[][] lists_O;
                References_old.LoadReferences(path1, out headAry_O, out lists_O, out error);
                long[] offsets_N = Utils.ReadLongArray(path3, instances_N.Length + 1, out error);
                int[] counts_N = new int[instances_N.Length];
                for (int i = 0, icnt = instances_N.Length; i < icnt; ++i)
                {
                    counts_N[i] = (int)(offsets_N[i + 1] - offsets_N[i]) / sizeof(int);
                }
                int[] counts_O = new int[instances_O.Length];
                int nx_O = 0;
                int ii = 0;
                for (int icnt = instances_N.Length; ii < icnt;)
                {
                    if (nx_O == headAry_O.Length)
                    {
                        counts_O[ii++] = 0;
                        continue;
                    }
                    int ndx_O = headAry_O[nx_O];
                    if (ndx_O > ii)
                    {
                        try
                        {
                            while (ii < ndx_O) counts_O[ii++] = 0;
                        }
                        catch
                        {
                            int a = 1;
                        }
                    }
                    counts_O[ii++] = lists_O[nx_O].Length;
                    ++nx_O;
                }

                int rootedCount_O = 0;
                int rootedCount_N = 0;
                int fnlzerCount_O = 0;
                int fnlzerCount_N = 0;
                for (int i = 0, icnt = instances_N.Length; i < icnt; ++i)
                {
                    if ((instances_O[i] & Utils.RootBits.Rooted) > 0) ++rootedCount_O;
                    if ((instances_N[i] & Utils.RootBits.Rooted) > 0) ++rootedCount_N;
                    if ((instances_O[i] & Utils.RootBits.Finalizer) > 0) ++fnlzerCount_O;
                    if ((instances_N[i] & Utils.RootBits.Finalizer) > 0) ++fnlzerCount_N;
                }
                TestContext.WriteLine("instances_O rooted " + Utils.CountString(rootedCount_O));
                TestContext.WriteLine("instances_N rooted " + Utils.CountString(rootedCount_N));
                TestContext.WriteLine("instances_O finalizer " + Utils.CountString(fnlzerCount_O));
                TestContext.WriteLine("instances_N finalizer " + Utils.CountString(fnlzerCount_N));

                Assert.AreEqual(instances_O.Length, instances_N.Length);
                for (int i = 0, icnt = instances_N.Length; i < icnt; ++i)
                {
                    if (counts_O[i] != counts_N[i])
                    {
                        notEqualCounts.Add(i);
                    }
                }

                if (notEqualCounts.Count > 0)
                {
                    try
                    {
                        sw = new StreamWriter(@"C:\WinDbgStuff\dumps\Analytics\Highline\COUNTDIFFS.TXT");
                        foreach (int inst in notEqualCounts)
                        {
                            sw.Write(Utils.CountStringHeader(inst));
                            sw.Write(Utils.SmallIdHeader(counts_O[inst]));
                            sw.Write(Utils.SmallIdHeader(counts_N[inst]));
                            sw.Write(Utils.AddressStringHeader(instances_O[inst]));
                            sw.Write(Utils.AddressStringHeader(instances_N[inst]));
                            sw.WriteLine();
                        }
                    }
                    finally
                    {
                        sw?.Close();
                    }

                    //return;
                }


                br = new BinaryReader(File.Open(path2, FileMode.Open));

                int ndx_N = 0;
                for (int i = 0, icnt = headAry_O.Length; i < icnt; ++i)
                {
                    int ndx_O = headAry_O[i];
                    int cnt_O = lists_O[i].Length;
                    long offset_N = offsets_N[ndx_O];
                    int cnt_N = counts_N[ndx_O];

                    if (cnt_O != cnt_N)
                    {
                        int a = 1;
                    }

                    ulong parent_O = instances_O[ndx_O];
                    ulong parent_N = instances_N[ndx_O];
                    ulong[] children_O = new ulong[cnt_O];
                    for (int j = 0, jcnt = lists_O[i].Length; j < jcnt; ++j)
                    {
                        children_O[j] = instances_O[lists_O[i][j]];
                    }

                    ulong[] children_N = new ulong[cnt_N];
                    br.BaseStream.Seek(offset_N, SeekOrigin.Begin);
                    for (int j = 0, jcnt = cnt_N; j < jcnt; ++j)
                    {
                        int nx_N = br.ReadInt32();
                        children_N[j] = instances_N[nx_N];
                    }

                    if (notEqualCounts.BinarySearch(ndx_O) < 0)
                    {
                        if (!Utils.SameRealAddresses(children_O, children_N))
                        {
                            diffsRefs.Add(ndx_O);
                        }
                    }
                    else
                    {
                        var seq = children_O.Where((src, ndx) => src != parent_O).Distinct().ToArray();
                        if (!Utils.SameRealAddresses(seq, children_N))
                        {
                            diffsFixedRefs.Add(ndx_O);
                        }
                    }
                }

                try
                {
                    sw = new StreamWriter(@"C:\WinDbgStuff\dumps\Analytics\Highline\O_N_FLAGDIFFS.TXT");
                    sw1 = new StreamWriter(@"C:\WinDbgStuff\dumps\Analytics\Highline\N_O_FLAGDIFFS.TXT");
                    Assert.IsTrue(instances_O != null && instances_N != null && instances_O.Length == instances_N.Length);
                    for (int k = 0, kcnt = instances_N.Length; k < kcnt; ++k)
                    {
                        ulong addr_O = instances_O[k];
                        ulong addr_N = instances_N[k];
                        Assert.IsTrue(Utils.RealAddress(addr_O) == Utils.RealAddress(addr_N));
                        bool addr_O_rooted = Utils.IsRooted(addr_O);
                        //bool addr_O_fnlzer = Utils.IsFinalizer(addr_O);
                        bool addr_N_rooted = Utils.IsRooted(addr_N);
                        //bool addr_N_fnlzer = Utils.IsFinalizer(addr_N);

                        if ((addr_O_rooted && !addr_N_rooted))
                        {
                            sw.Write(Utils.CountStringHeader(k));
                            sw.Write(Utils.AddressStringHeader(addr_O));
                            sw.Write(Utils.AddressStringHeader(addr_N));
                            sw.WriteLine();
                        }

                        if ((addr_N_rooted && !addr_O_rooted))
                        {
                            sw1.Write(Utils.CountStringHeader(k));
                            sw1.Write(Utils.AddressStringHeader(addr_N));
                            sw1.Write(Utils.AddressStringHeader(addr_O));
                            sw1.WriteLine();
                        }
                    }


                }
                catch (Exception ex)
                {
                    Assert.IsTrue(false, ex.ToString());
                }
                finally
                {
                    sw?.Close();
                    sw = null;
                    sw1.Close();
                    sw1 = null;
                }
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                TestContext.WriteLine("EXCEPTION");
                TestContext.WriteLine(error);
            }
            finally
            {
                br?.Close();
                sw?.Close();
                sw1?.Close();
            }


            //TestContext.WriteLine("[1] : " + path1);
            //TestContext.WriteLine("[2] : " + path2);
            //TestContext.WriteLine("SAME INSTANCE ADDRESSES: " + sameAddresses);
            //TestContext.WriteLine("[1] INSTANCE COUNT: " + Utils.CountString(ary1.Length));
            //TestContext.WriteLine("[2] INSTANCE COUNT: " + Utils.CountString(ary1.Length));
            //TestContext.WriteLine("[1] ROOTED    COUNT: " + Utils.CountString(rootedCount1));
            //TestContext.WriteLine("[2] ROOTED    COUNT: " + Utils.CountString(rootedCount2));
            //TestContext.WriteLine("[1] FINALIZER COUNT: " + Utils.CountString(finalizerCount1));
            //TestContext.WriteLine("[2] FINALIZER COUNT: " + Utils.CountString(finalizerCount2));


        }

        #endregion type references

        #region exceptions

        [TestMethod]
        public void TestExceptionList()
        {
            string dumpPath = Setup.DumpsFolder + @"\OMS\Redskull\Analytics_170330_163202.bad.dmp";

            var dmp = OpenDump(dumpPath);
            using (dmp)
            {
                var heap = dmp.Heap;
                var segs = heap.Segments;
                var addresses = new List<ulong>(32);
                var strings = new List<string>(32);
                for (int i = 0, icnt = segs.Count; i < icnt; ++i)
                {
                    var seg = segs[i];
                    ulong addr = seg.FirstObject;
                    while (addr != 0ul)
                    {
                        var clrType = heap.GetObjectType(addr);
                        if (clrType == null || !clrType.IsException) goto NEXT_OBJECT;
                        addresses.Add(addr);
                        strings.Add(ValueExtractor.GetShortExceptionValue(addr, clrType, heap));
                        NEXT_OBJECT:
                        addr = seg.NextObject(addr);
                    }
                }


            }
        }

        #endregion exceptions

        //[TestMethod]
        //public void Test_ServiceModel_Description_MessageDescription()
        //{
        //    string typeName = "System.ServiceModel.Description.MessageDescription";
        //    string dumpPath = Setup.DumpsFolder + @"\OMS\Redskull\Analytics_170330_163202.bad.dmp";

        //    ClrInstanceField itemsFld = null, bodyFld = null, wrapperNameFld = null, encodedFld = null, decodedFld = null;
        //    ClrType itemsType = null, bodyType = null, wrapperNameType = null;
        //    ulong itemsAddr, bodyAddr, wrapperNameAddr;

        //    var dmp = OpenDump(dumpPath);
        //    using (dmp)
        //    {
        //        var heap = dmp.Heap;
        //        var segs = heap.Segments;
        //        var addresses = new List<ulong>(32);
        //        var strings = new List<string>(32);
        //        for (int i = 0, icnt = segs.Count; i < icnt; ++i)
        //        {
        //            var seg = segs[i];
        //            ulong addr = seg.FirstObject;
        //            while (addr != 0ul)
        //            {
        //                var clrType = heap.GetObjectType(addr);
        //                if (clrType == null || !Utils.SameStrings(typeName, clrType.Name)) goto NEXT_OBJECT;

        //                (itemsType, itemsFld, itemsAddr) = TypeExtractor.GetReferenceTypeField(heap, clrType, addr, "items");
        //                Assert.IsNotNull(itemsType);
        //                Assert.IsNotNull(itemsFld);
        //                (bodyType, bodyFld, bodyAddr) = TypeExtractor.GetReferenceTypeField(heap, itemsType, itemsAddr, "body");
        //                Assert.IsNotNull(itemsType);
        //                Assert.IsNotNull(itemsFld);
        //                (wrapperNameType, wrapperNameFld, wrapperNameAddr) = TypeExtractor.GetReferenceTypeField(heap, bodyType, bodyAddr, "wrapperName");
        //                Assert.IsNotNull(wrapperNameFld);
        //                if (wrapperNameType == null) wrapperNameType = wrapperNameFld.Type;
        //                Assert.IsNotNull(wrapperNameFld);

        //                encodedFld = wrapperNameType.GetFieldByName("encoded");
        //                Assert.IsNotNull(encodedFld);
        //                string encodedVal = (string)encodedFld.GetValue(wrapperNameAddr, false, true) ?? Constants.NullValue;
        //                decodedFld = wrapperNameType.GetFieldByName("decoded");
        //                Assert.IsNotNull(decodedFld);
        //                string decodedVal = (string)encodedFld.GetValue(wrapperNameAddr, false, true) ?? Constants.NullValue;



        //                addresses.Add(addr);
        //                strings.Add(encodedVal);
        //                NEXT_OBJECT:
        //                addr = seg.NextObject(addr);
        //            }
        //        }


        //    }
        //}

        #region network



        #endregion network

        #region open dump

        public static ClrtDump OpenDump(int indexNdx = 0)
        {
            string error;
            var path = Setup.GetRecentAdhocPath(indexNdx);
            Assert.IsNotNull(path, "Setup returned null when asked for index " + indexNdx + ".");
            var clrDump = new ClrtDump(path);
            var initOk = clrDump.Init(out error);
            Assert.IsTrue(initOk, "ClrtDump.Init failed for dump: " + path + Environment.NewLine + error);
            return clrDump;
        }

        public static ClrtDump OpenDump(string path)
        {
            string error;
            var clrDump = new ClrtDump(path);
            bool initOk = clrDump.Init(out error);
            Assert.IsTrue(initOk, "ClrtDump.Init failed for dump: " + path + Environment.NewLine + error);
            return clrDump;
        }

        #endregion open dump

        #region template

        [TestMethod]
        public void TestTemplate()
        {
            var dmp = OpenDump();
            using (dmp)
            {
                var heap = dmp.Heap;

            }
        }

        [TestMethod]
        public void TestSnippet()
        {
            string error = null;
            using (var clrDump = OpenDump(""))
            {
                try
                {
                    var runtime = clrDump.Runtimes[0];
                    var heap = runtime.Heap;
                    var segs = heap.Segments;
                    for (int i = 0, icnt = segs.Count; i < icnt; ++i)
                    {
                        var seg = segs[i];
                        ulong addr = seg.FirstObject;
                        while (addr != 0ul)
                        {
                            var clrType = heap.GetObjectType(addr);
                            if (clrType == null) goto NEXT_OBJECT;


                            NEXT_OBJECT:
                            addr = seg.NextObject(addr);
                        }
                    }
                }
                catch (Exception ex)
                {
                    error = Utils.GetExceptionErrorString(ex);
                    Assert.IsTrue(false, error);
                }
            }
        }

        #endregion template
    }
}
