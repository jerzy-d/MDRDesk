using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
    public class IntSterta
    {
        private int[] B;
        private int L;
        public int Count => L;

        public IntSterta(int capacity)
        {
            if (capacity <= 0) capacity = 1024;
            B = new int[capacity];
            L = 0;
        }

        public IntSterta(int capacity, int[] ary)
        {
            Debug.Assert(capacity > 0 && ary != null && ary.Length > 0 && capacity >= ary.Length);
            B = new int[capacity+1];
            for (int i = 0, icnt = ary.Length; i < icnt; ++i)
                B[i + 1] = ary[i];
            L = ary.Length;
        }

        public void Push(int x)
        {
            if (++L == B.Length)
                MakeBigger();
            B[L] = x;
            Up();
        }

        public int Pop()
        {
            if (L == 0) return -1;
            int x = B[1];
            B[1] = B[L--];
            Down();
            return x;
        }

        private void Up()
        {
            int t = B[L];
            int n = L;
            while((n!=1) && (B[n/2] >= t))
            {
                B[n] = B[n / 2];
                n = n / 2;
            }
            B[n] = t;
        }

        private void Down()
        {
            int i = 1;
            while(true)
            {
                int p = 2 * i;
                if (p > L) break;
                if (p + 1 <= L)
                {
                    if (B[p] > B[p + 1]) ++p;
                }
                if (B[i] <= B[p]) break;
                int t = B[p];
                B[p] = B[i];
                B[i] = t;
                i = p;
            }
        }

        private void MakeBigger()
        {
            int[] b = new int[B.Length * 2];
            Buffer.BlockCopy(B, 0, b, 0, B.Length * sizeof(int));
            B = b;
        }
    }
}
