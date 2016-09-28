using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestApp
{
    public class TestClass
    {
        private int _processId;
        private CancellationTokenSource _cancelSource;
        private Thread _thread;
        private ConcurrentDictionary<string, KeyValuePair<string, string>> _concurrentDictionary;
        private Dictionary<int, string> _dictionary;
	    private TestStruct _testStruct;

        public TestClass(int processId)
        {
            _processId = processId;
	        var name = "From TestClass_" + processId;
			TestStructClass testStructClass = new TestStructClass(name + "_toTestStructClass", 20);
			KeyValuePair<string, KeyValuePair<DateTime, TestStructClass>> kvPair = 
				new KeyValuePair<string, KeyValuePair<DateTime, TestStructClass>>(name + "key",new KeyValuePair<DateTime, TestStructClass>(DateTime.Now, testStructClass));
			_testStruct = new TestStruct(name + "_fromTestClass",20,kvPair);


		}

        public void Init()
        {
            _cancelSource = new CancellationTokenSource();
            _thread = new Thread(this.ThreadProc) {Name = "TestClassThread"};
            _thread.Start(_cancelSource.Token);
        }

        public void Stop()
        {
            _cancelSource.Cancel();
        }

        private void ThreadProc(object obj)
        {
            Console.WriteLine("Started worker... Id: " + Thread.CurrentThread.ManagedThreadId);
            CancellationToken calcelToken = (CancellationToken)obj;
            StringBuilder sb = InitStringBuilder();
            _concurrentDictionary = new ConcurrentDictionary<string, KeyValuePair<string, string>>();
            _dictionary = new Dictionary<int, string>();

            int count = 0;
            while (!calcelToken.IsCancellationRequested)
            {
                _concurrentDictionary.TryAdd("key_" + count,new KeyValuePair<string, string>("valueKey_" + count, "valueValue_" + count));
                _dictionary.Add(count, "value_" + count);
                ++count;
                if ((count % 20) == 0)
                    Console.WriteLine("[procId: " + _processId + "] dct entries added " + count);
                Thread.Sleep(1000);
            }
        }

        private StringBuilder InitStringBuilder()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 10; ++i)
            {
                sb.Append(i + "aaaaaaaaaa");
            }
            return sb;
        }
    }
}
