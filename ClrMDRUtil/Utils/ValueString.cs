using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
    public struct DisplayableString
    {
        private const int MaxLength = 40;
        public readonly string Content;

        public DisplayableString(string str)
        {
            Content = str;
            if (str == null) str = Constants.NullValue;
        }

        public bool IsNull()
        {
            return Content == null;
        }

        public bool IsLong()
        {
            return Content != null && Content.Length > MaxLength;
        }

        public static string ReplaceNewlines(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            for (int i = 0, icnt = str.Length; i < icnt; ++i)
            {
                if (str[i] == '\n')
                {
                    var newStr = str.Replace("\r\n", Constants.WindowsNewLine);
                    newStr = newStr.Replace("\n", Constants.UnixNewLine);
                    return newStr;
                }
            }
            return str;
        }

        public override string ToString()
        {
            if (Content == null) return Constants.NullValue;
            if (Content.Length > MaxLength)
            {
                var newStr = Content.Substring(0, MaxLength - 3) + "...";
                return ReplaceNewlines(Content);
            }
            else
                return ReplaceNewlines(Content);
        }
    }
}
