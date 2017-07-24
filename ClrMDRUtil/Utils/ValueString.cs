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
        private readonly string _content;

        public DisplayableString(string str)
        {
            _content = str;
            if (str == null) str = Constants.NullValue;
        }

        public bool IsNull()
        {
            return _content == null;
        }

        public bool IsLong()
        {
            return _content != null && _content.Length > MaxLength;
        }

        public string FullContent => (_content == null) ? Constants.NonValueChar + Constants.NullValue : _content;


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
            if (_content == null) return Constants.NonValueChar + Constants.NullValue;
            if (_content.Length > MaxLength)
            {
                var newStr = _content.Substring(0, MaxLength - 3) + "...";
                return ReplaceNewlines(newStr);
            }
            else
                return ReplaceNewlines(_content);
        }
    }
}
