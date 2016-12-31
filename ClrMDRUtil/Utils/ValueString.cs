using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
    public class ValueString
    {
        public const int MaxLength = 40;
        public readonly string Content;
        public readonly string DisplayString;

        public ValueString(string str)
        {
            Content = str;
            if (str == null) str = Constants.NullValue;
            if (str.Length > MaxLength)
                DisplayString = str.Substring(0, MaxLength - 3) + "...";
            else
                DisplayString = str;
        }

        public override string ToString()
        {
            return DisplayString;
        }
    }
}
