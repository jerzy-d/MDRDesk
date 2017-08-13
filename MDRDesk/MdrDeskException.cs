using System;
using ClrMDRIndex;

namespace MDRDesk
{
    class MdrDeskException : Exception
    {
        public MdrDeskException()
        {
        }

        public MdrDeskException(string message)
        : base(Constants.HeavyAsteriskHeader + message)
        {
        }

        public MdrDeskException(string message, Exception inner)
        : base(Constants.HeavyAsteriskHeader + message, inner)
        {
        }
    }
}
