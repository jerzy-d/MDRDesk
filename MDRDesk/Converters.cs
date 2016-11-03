using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using ClrMDRIndex;

namespace MDRDesk
{
	[ValueConversion(typeof(long), typeof(string))]
	public class LongHexConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return Utils.AddressString((ulong) value);
		}

		// TODO JRD check if not preserving root flag is meaningfull here
		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			var str = value.ToString();
			if (str[1] == '\u2714')
			{
				ulong addr;
				string hexNumWithoutPrefix = str.Substring(2);
				ulong.TryParse(hexNumWithoutPrefix, System.Globalization.NumberStyles.HexNumber, null, out addr);
				return Utils.SetAsRooted(addr);
			}
			return ulong.Parse(value.ToString());
		}
	}

	[ValueConversion(typeof(ulong), typeof(string))]
	public class UlongSizeConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return string.Format("{0:#,###,###,###}", (ulong)value); ;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return UInt64.Parse(value.ToString(),NumberStyles.AllowThousands);
		}
	}

	[ValueConversion(typeof(int), typeof(string))]
	public class IntSizeConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return string.Format("{0:#,###,###,###}", (int)value); ;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return Int32.Parse(value.ToString(), NumberStyles.AllowThousands);
		}
	}
}
