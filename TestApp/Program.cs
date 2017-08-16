﻿using System;
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

			var testClass = new TestClass(Process.GetCurrentProcess().Id);
			testClass.Init();

            Guid guid1 = Guid.NewGuid();
            var cls = (object)guid1;

			Console.ReadLine();
			testClass.Stop();
		}
	}
}
