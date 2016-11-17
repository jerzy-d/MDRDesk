using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace UnitTestMdr
{
	public class Lou
	{
		private int MyInt = 0;

		public void RegularMethod()
		{
			Console.WriteLine("t2 -- begin RegularMethod on " + DateTime.Now.TimeOfDay);
			MyInt = -2;
			Console.WriteLine("t2 -- MyInt is now " + MyInt);
			Console.WriteLine("t2 -- end RegularMethod after " + DateTime.Now.TimeOfDay);
		}

		public void LockingMethod()
		{
			Console.WriteLine("t1 -- begin LockingMethod on " + DateTime.Now.TimeOfDay);
			lock (this)
			{
				MyInt = 1;
				Console.WriteLine("t1 -- MyInt is now " + MyInt);
				Thread.Sleep(10000);
				MyInt = 3;
				Console.WriteLine("t1 -- MyInt is now " + MyInt);
			}
			Console.WriteLine("t1 -- end LockingMethod " + DateTime.Now.TimeOfDay);
		}
	}

}
