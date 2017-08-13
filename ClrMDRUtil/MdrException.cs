using System;

namespace ClrMDRIndex
{
    class MdrException : Exception
    {
        public MdrException()
        {
        }

        public MdrException(string message)
        : base(Constants.HeavyCheckMarkHeader + message)
        {
        }

        public MdrException(string message, Exception inner)
        : base(Constants.HeavyCheckMarkHeader + message, inner)
        {
        }
    }
}
