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
		private SortedDictionary<string, string> _sortedDictionary;
		private Dictionary<int, string> _dictionary;
		private HashSet<string> _stringHashSet;
		private HashSet<decimal> _decimalHashSet;
		private HashSet<DateTime> _dateTimeHashSet;
		private HashSet<TimeSpan> _timeSpanHashSet;
		private HashSet<Guid> _guidHashSet;
		private HashSet<float> _floatHashSet;
		private HashSet<double> _doubleHashSet;
		private HashSet<char> _charHashSet;
		private TestStruct _testStruct;
	    private Guid _myGuid;

		private string[] _stringArray;
		private TimeSpan[] _timeSpanArray;
		private DateTime[] _dateTimeArray;
		private Guid[] _guidArray;
	    private Decimal[] _decimalArray;

	    private int StructArrayLen = 11;

		public TestClass(int processId)
        {
            _processId = processId;
	        var name = "From TestClass_" + processId;
			TestStructClass testStructClass = new TestStructClass(name + "_toTestStructClass", 20);
			KeyValuePair<string, KeyValuePair<DateTime, TestStructClass>> kvPair = 
				new KeyValuePair<string, KeyValuePair<DateTime, TestStructClass>>(name + "key",new KeyValuePair<DateTime, TestStructClass>(DateTime.Now, testStructClass));
			_testStruct = new TestStruct(name + "_fromTestClass",20,kvPair);
			_myGuid = Guid.NewGuid();
			_stringArray = new string[StructArrayLen];
			_timeSpanArray = new TimeSpan[StructArrayLen];
			_dateTimeArray = new DateTime[StructArrayLen];
			_guidArray = new Guid[StructArrayLen];
			_decimalArray = new decimal[StructArrayLen];
			var tmDelta = new TimeSpan(0,0,1,0);
			DateTime dt = DateTime.Now;
			decimal dec = 0.5m;
			for (int i = 0; i < StructArrayLen; ++i)
			{
				_stringArray[i] = "_stringArrayItem_" + i;
				_timeSpanArray[i] = new TimeSpan(1,1,1,i);
				dt = dt + tmDelta;
				_dateTimeArray[i] = dt;
				_guidArray[i] = Guid.NewGuid();
				dec = dec + 0.5m;
				_decimalArray[i] = dec;
			}
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
			_sortedDictionary = new SortedDictionary<string, string>();
			_stringHashSet = new HashSet<string>();
			_decimalHashSet = new HashSet<decimal>();
			_dateTimeHashSet = new HashSet<DateTime>();
			_timeSpanHashSet = new HashSet<TimeSpan>();
			_guidHashSet = new HashSet<Guid>();
			_floatHashSet = new HashSet<float>();
			_doubleHashSet = new HashSet<double>();
			_charHashSet = new HashSet<char>();

			int count = 0;
			DateTime dt = DateTime.Now;
			TimeSpan tsd = new TimeSpan(0, 0, 1, 0);
			TimeSpan ts = new TimeSpan(0, 0, 1, 0);
	        decimal dec = 1.0m;
	        float fl = 1.0f;
			while (!calcelToken.IsCancellationRequested)
            {
                _concurrentDictionary.TryAdd("key_" + count,new KeyValuePair<string, string>("valueKey_" + count, "valueValue_" + count));
                _dictionary.Add(count, "value_" + count);
				_sortedDictionary.Add("sorted_key_" + count, "sorted_value_" + count);

				_stringHashSet.Add("hash_entry" + count);
	            _dateTimeHashSet.Add(dt);
	            dt = dt + tsd;
	            _timeSpanHashSet.Add(ts);
	            ts = ts + tsd;
	            _decimalHashSet.Add(dec);
	            dec = dec + 0.5m;
	            _guidHashSet.Add(Guid.NewGuid());
	            _floatHashSet.Add(fl);
	            _doubleHashSet.Add((double) fl);
	            if ((_charHashSet.Count & 1) == 0)
		            _charHashSet.Add('a');
	            else
		            _charHashSet.Add('b');
	            fl = fl + 0.5f;

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
