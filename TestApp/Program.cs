using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Collections.Concurrent;
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

            dct.Add(0, TestEnumUInt8.First);
            dct.Add(1, TestEnumUInt8.Second);
            dct.Add(2, TestEnumUInt8.Third);
            dct.Add(3, TestEnumUInt8.First);
            dct.Add(4, TestEnumUInt8.Second);
            dct.Add(5, TestEnumUInt8.Third);
            dct.Add(6, TestEnumUInt8.First);
            dct.Add(7, TestEnumUInt8.Second);

            Dictionary<int, TestEnumUInt8> dct2 = new Dictionary<int, TestEnumUInt8>();

            dct2.Add(0, TestEnumUInt8.First);
            dct2.Add(1, TestEnumUInt8.Second);
            dct2.Add(2, TestEnumUInt8.Third);
            dct2.Add(3, TestEnumUInt8.First);
            dct2.Add(4, TestEnumUInt8.Second);
            dct2.Add(5, TestEnumUInt8.Third);
            dct2.Add(6, TestEnumUInt8.First);
            dct2.Add(7, TestEnumUInt8.Second);

            var structDct = new Dictionary<(string, string), (int, string, (int, string))>();
            structDct.Add(("key1_a", "key1_b"), (1, "value1_a", (1, "value1_b")));
            structDct.Add(("key2_a", "key2_b"), (2, "value2_a", (2, "value2_b")));
            structDct.Add(("key3_a", "key3_b"), (3, "value3_a", (3, "value3_b")));
            structDct.Add(("key4_a", "key4_b"), (4, "value4_a", (4, "value4_b")));
            structDct.Add(("key5_a", "key5_b"), (5, "value5_a", (5, "value5_b")));
            structDct.Add(("key6_a", "key6_b"), (6, "value6_a", (6, "value6_b")));
            structDct.Add(("key7_a", "key7_b"), (7, "value7_a", (7, "value7_b")));

            var testClass = new TestClass(Process.GetCurrentProcess().Id);
			testClass.Init();

            TestEnumInt32[] _TestEnumInt32 = new TestEnumInt32[] { TestEnumInt32.Third, TestEnumInt32.Second, TestEnumInt32.First};

            ValueTuple<string, int, ValueTuple<string, int>, string>[] vtary = new ValueTuple<string, int, ValueTuple<string, int>, string>[]
            {
                ("aaa", 0, ("in-aaa",0),"l-aaa"),
                ("bbb", 1, ("in-bbb",0),"l-bbb"),
                ("ccc", 1, ("in-ccc",0),"l-ccc")
            };

            var cdct = new ConcurrentDictionary<int, TestEnum>();
            cdct.TryAdd(0, TestEnum.First);
            cdct.TryAdd(1, TestEnum.Second);
            cdct.TryAdd(2, TestEnum.Third);
            cdct.TryAdd(3, TestEnum.Forth);
            cdct.TryAdd(4, TestEnum.Fifth);

            var concstructDct = new ConcurrentDictionary<(string, string), (int, string, (int, string))>();
            concstructDct.TryAdd(("key1_a", "key1_b"), (1, "value1_a", (1, "value1_b")));
            concstructDct.TryAdd(("key2_a", "key2_b"), (2, "value2_a", (2, "value2_b")));
            concstructDct.TryAdd(("key3_a", "key3_b"), (3, "value3_a", (3, "value3_b")));
            concstructDct.TryAdd(("key4_a", "key4_b"), (4, "value4_a", (4, "value4_b")));
            concstructDct.TryAdd(("key5_a", "key5_b"), (5, "value5_a", (5, "value5_b")));
            concstructDct.TryAdd(("key6_a", "key6_b"), (6, "value6_a", (6, "value6_b")));
            concstructDct.TryAdd(("key7_a", "key7_b"), (7, "value7_a", (7, "value7_b")));


            var stack = new Stack<TestEnum>(8);
            stack.Push(TestEnum.First);
            stack.Push(TestEnum.Second);
            stack.Push(TestEnum.Third);
            stack.Push(TestEnum.Forth);
            stack.Push(TestEnum.First);

            var que1 = new Queue<TestEnum>(8);
            que1.Enqueue(TestEnum.First);
            que1.Enqueue(TestEnum.Second);
            que1.Enqueue(TestEnum.Third);
            que1.Enqueue(TestEnum.Forth);
            que1.Enqueue(TestEnum.Fifth);

            var que2 = new Queue<int>(10);
            for (int i = 0; i < 100; ++i)
            {
                que2.Enqueue(i);
                que2.Dequeue();
                que2.Enqueue(i + 100);
            }

            Guid guid1 = Guid.NewGuid();
            var cls = (object)guid1;

			Console.ReadLine();
			testClass.Stop();
		}
	}
}
