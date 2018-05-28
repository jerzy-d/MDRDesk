using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClrMDRIndex;
using ClrMDRUtil.Utils;
using Markdown.Xaml;
using Microsoft.Diagnostics.Runtime;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace UnitTestMdr
{
    /// <summary>
    /// Summary description for MiscTests
    /// </summary>
    [TestClass]
    public class MiscTests
    {
        public MiscTests()
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
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
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

        [TestMethod]
        public void BitSetTest()
        {
            BitSet bs = new BitSet(130);
            bs.Set(0);
            bs.Set(2);
            bs.Set(3);
            bs.Set(4);
            bs.Set(6);
            bs.Set(8);
            bs.Unset(3);
            bs.Set(129);
            int[] ary = bs.GetSetBitIndices();
            Assert.IsTrue(
                ary.Length == 6
                && ary[0] == 0
                && ary[1] == 2
                && ary[2] == 4
                && ary[3] == 6
                && ary[4] == 8
                && ary[5] == 129
                );
        }

        [TestMethod]
        public void Conversions()
        {
            TimeSpan ts1 = new TimeSpan(0, 2, 38, 19);
            TimeSpan ts2 = new TimeSpan(0, 0, 3, 02);

            double gain = 100 * ((ts1.TotalMilliseconds - ts2.TotalMilliseconds) / ts2.TotalMilliseconds);
        }

        [TestMethod]
        public void TestMisc()
        {
            var ndx = Int32.MinValue;

            var s1 = String.Format("0x{0:x8}", ndx);
            var s2 = String.Format("0x{0:x8}", 0xC0000000);
            var s3 = String.Format("0x{0:x8}", 0x80000000);
            var s4 = String.Format("0x{0:x8}", 0x40000000);
            var s5 = String.Format("0x{0:x8}", -1);

            Assert.IsTrue(s1 == s3);
        }


        [TestMethod]
        public void TestHtmlDecoding()
        {
            const string txt = @"#### MDRDesk
[Up](../ README.md)
### Instance Value Views  
There are four types of windows to display an instance content.
Each one has the help button and F1 hot key to display this document.  

&#x2776; Class/structure treeview.  
&#x2777; A key/value collection view for dictionaries.  
&#x2778; A single item collection view for arrays, lists, sets, queues, etc..  
&#x2779; An instant value.  
Useful to display large strings, and string builders content.
";
            string newTxt = Markdown.Xaml.Markdown.Normalize(txt);
        }
    }
}
