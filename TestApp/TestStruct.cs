using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestApp
{
	public struct TestStruct
	{
		private string _name;
		private int _someCount;
		KeyValuePair<string, KeyValuePair<DateTime, TestStructClass>> _kvPair;
		private decimal _decimal;
		private DateTime _dateTime;

		public TestStruct(string name, int someCount, KeyValuePair<string, KeyValuePair<DateTime, TestStructClass>> kvPair)
		{
			_name = name;
			_someCount = someCount;
			_kvPair = kvPair;
			_decimal = 0.1m;
			_dateTime = DateTime.Now;
		}
	}


	public class TestStructClass
	{
		string _name;
		KeyValuePair<string, int>[] _kvKeyValuePairs;

		public TestStructClass(string name, int count)
		{
			_name = name;
			_kvKeyValuePairs = new KeyValuePair<string, int>[count];
			for (int i = 0; i < count; ++i)
			{
				_kvKeyValuePairs[i] = new KeyValuePair<string, int>(name + "_" + i, i);
			}

		}
	}
}
