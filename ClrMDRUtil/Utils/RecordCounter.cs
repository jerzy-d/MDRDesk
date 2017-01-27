namespace ClrMDRIndex
{
	public class RecordCounter
	{
		private int[] _counts;
		public int[] Counts => _counts;
		private int _recordCount;
		public int RecordCount => _recordCount;
		private int _totalCount;
		public int TotalCount => _totalCount;
		public int Size => _counts.Length;

		public RecordCounter(int size)
		{
			_counts = new int[size];
			_recordCount = 0;
			_totalCount = 0;
		}

		public void Add(int ndx)
		{
			++_totalCount;
			if (_counts[ndx] == 0) ++_recordCount;
			_counts[ndx] += 1;
		}
	}
}
