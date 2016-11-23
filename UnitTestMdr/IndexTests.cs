using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClrMDRIndex;
using Microsoft.Diagnostics.Runtime;
using DmpNdxQueries;


namespace UnitTestMdr
{
	[TestClass]
	public class IndexTests
	{
		#region test context/initialization

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

		#endregion test context/initialization

		#region threads

		[TestMethod]
		public void ThreadInformation()
		{
			string error = null;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			var index = OpenIndex();
			TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

			using (index)
			{
				stopWatch.Restart();
				var threads = DumpIndexer.GetThreads(index.Runtime);
				TestContext.WriteLine(index.DumpFileName + " THREAD COUNT: " + threads.Length + ", GETTING THREAD LIST DURATION: " + Utils.StopAndGetDurationString(stopWatch));
				stopWatch.Restart();
				var heap = index.Dump.Heap;
				var blocks = DumpIndexer.GetBlockingObjects(heap);
				TestContext.WriteLine(index.DumpFileName + " BLOCKING OBJECT COUNT: " + blocks.Length + ", GETTING BLOCKING OBJECTS DURATION: " + Utils.StopAndGetDurationString(stopWatch));

				var blksSingleOwned = new List<BlockingObject>();
				var blksOwnedByMany = new List<BlockingObject>();
				var blksWithWaiters = new List<BlockingObject>();

				for (int i = 0, icnt = blocks.Length; i < icnt; ++i)
				{
					var blk = blocks[i];
					if (blk.Taken && blk.HasSingleOwner)
					{
						blksSingleOwned.Add(blk);
					}
					if (blk.Owners != null && blk.Owners.Count > 0)
					{
						blksOwnedByMany.Add(blk);
					}
					if (blk.Waiters != null && blk.Waiters.Count > 0)
					{
						blksWithWaiters.Add(blk);
					}
				}


				int a = 1;
			}

			Assert.IsNull(error,error);
		}


		#endregion threads

		#region get list of specific clr objects



		#endregion get list of specific clr objects

		#region template

		[TestMethod]
		public void TestSnippet()
		{
			string error = null;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			var index = OpenIndex();
			TestContext.WriteLine(index.DumpFileName + " INDEX OPEN DURATION: " + Utils.StopAndGetDurationString(stopWatch));

			using (index)
			{

			}

			Assert.IsNull(error,error);
		}

		#endregion template

		#region open index

		public static DumpIndex OpenIndex(int indexNdx=0)
		{
			string error;
			var indexPath = Setup.GetRecentIndexPath(indexNdx);
			Assert.IsNotNull(indexPath,"Setup returned null when asked for index " + indexNdx + ".");
			var version = Assembly.GetExecutingAssembly().GetName().Version;
			var index = DumpIndex.OpenIndexInstanceReferences(version, indexPath, 0, out error);
			Assert.IsNotNull(index, error);
			return index;
		}

		public static DumpIndex OpenIndex(string mapPath)
		{
			string error;
			var version = Assembly.GetExecutingAssembly().GetName().Version;
			var index = DumpIndex.OpenIndexInstanceReferences(version, mapPath, 0, out error);
			Assert.IsNotNull(index, error);
			return index;
		}

		#endregion open index

	}
}
