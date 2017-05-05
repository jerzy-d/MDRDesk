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
            int eq = StringComparer.OrdinalIgnoreCase.GetHashCode(string.Empty);
            int cmp = string.Compare(null, "bbhb", StringComparison.OrdinalIgnoreCase);
            int x = -1 % 4;
            int y = 2 % 4;

            Console.WriteLine("Started... Process Id: " + Process.GetCurrentProcess().Id);

            var testClass = new TestClass(Process.GetCurrentProcess().Id);
            testClass.Init();

            Console.ReadLine();
            testClass.Stop();
        }



    }
}
