using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestApp
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("Started... Process Id: " + Process.GetCurrentProcess().Id);

            Dictionary<int, object> dct = new Dictionary<int, object>();

            dct.Add(0, TestEnumInt8.Second);
            dct.Add(1, TestEnumUInt8.Second);
            dct.Add(2, TestEnumInt16.Second);
            dct.Add(3, TestEnumUInt16.Second);
            dct.Add(4, TestEnumInt32.Second);
            dct.Add(5, TestEnumUInt32.Second);
            dct.Add(6, TestEnumInt64.Second);
            dct.Add(7, TestEnumUInt64.Second);

            var testClass = new TestClass(Process.GetCurrentProcess().Id);
			testClass.Init();

            TestEnumInt32[] _TestEnumInt32 = new TestEnumInt32[] { TestEnumInt32.Third, TestEnumInt32.Second, TestEnumInt32.First};

            ValueTuple<string, int, ValueTuple<string, int>, string>[] vtary = new ValueTuple<string, int, ValueTuple<string, int>, string>[]
            {
                ("aaa", 0, ("in-aaa",0),"l-aaa"),
                ("bbb", 1, ("in-bbb",0),"l-bbb"),
                ("ccc", 1, ("in-ccc",0),"l-ccc")
            };

            Guid guid1 = Guid.NewGuid();
            var cls = (object)guid1;

			Console.ReadLine();
			testClass.Stop();
		}
	}
}
