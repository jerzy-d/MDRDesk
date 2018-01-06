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

        public static bool IsLargeString(string s)
        {
            return s != null && s.Length > MaxLength;
        }

        public DisplayableString(string str)
        {
            _content = str;
            if (str == null) str = Constants.NullValueOld;
        }

        public bool IsNull()
        {
            return _content == null;
        }

        public bool IsLong()
        {
            return _content != null && _content.Length > MaxLength;
        }

        public string FullContent => (_content == null) ? Constants.NonValueChar + Constants.NullValueOld : _content;


        public int SizeInBytes =>  _content == null ? 0 : _content.Length * sizeof(char);

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
            if (_content == null) return Constants.NonValueChar + Constants.NullValueOld;
            if (_content.Length > MaxLength)
            {
                var newStr = _content.Substring(0, MaxLength - 1) + Constants.HorizontalEllipsisChar;
                return ReplaceNewlines(newStr);
            }
            else
                return ReplaceNewlines(_content);
        }
    }

    public struct StringSet2
    {
        ValueTuple<string, string> Data;
    }
    public struct DisplayableStringSet2
    {
        ValueTuple<DisplayableString, DisplayableString> Data;
    }

    //public struct StringEx : IComparer<StringEx>, IEquatable<StringEx>
    //{
    //    private const int MaxLength = 40;
    //    private readonly string _content;
    //    private readonly string _expo;

    //    public StringEx(string str)
    //    {
    //        _content = str;
    //        _expo = GetExpo(str);
    //    }

    //    public bool IsNull()
    //    {
    //        return _content == null;
    //    }

    //    public bool IsLong()
    //    {
    //        return _content != null && _content.Length > MaxLength;
    //    }

    //    public string FullContent => (_content == null) ? Constants.NonValueChar + Constants.NullValueOld : _content;

    //    public int SizeInBytes => _content == null ? 0 : _content.Length * sizeof(char);

    //    public static string GetExpo(string str)
    //    {
    //        if (str == null) return Constants.NonValueChar + Constants.NullValueOld;
    //        if (str.Length > MaxLength)
    //        {
    //            var newStr = str.Substring(0, MaxLength - 1) + Constants.HorizontalEllipsisChar;
    //            return DisplayableString.ReplaceNewlines(newStr);
    //        }
    //        else
    //            return DisplayableString.ReplaceNewlines(str);
    //    }

    //    public override string ToString()
    //    {
    //        return _expo;
    //    }

    //    public int Compare(StringEx str1, StringEx str2)
    //    {
    //        return string.Compare(str1._content, str2._content, StringComparison.Ordinal);
    //    }

    //    public bool Equals(StringEx other)
    //    {
    //        if ((Object)_content == (Object)(other._content)) return true;
    //        if ((Object)_content == null || (Object)(other._content) == null) return false;
    //        return string.Compare(_content, other._content, StringComparison.Ordinal) == 0;
    //    }
    //}
}


