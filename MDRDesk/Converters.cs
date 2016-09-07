using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace MDRDesk
{
	[ValueConversion(typeof(long), typeof(string))]
	public class LongHexConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			ulong val = (ulong)value;
			return val.ToString("x16");
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return Int64.Parse(value.ToString());
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
