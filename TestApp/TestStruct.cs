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

		public TestStruct(string name, int someCount, KeyValuePair<string, KeyValuePair<DateTime, TestStructClass>> kvPair)
		{
			_name = name;
			_someCount = someCount;
			_kvPair = kvPair;
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
