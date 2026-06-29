using System;
using System.Globalization;
using System.Windows.Data;

namespace Tailgrab.PlayerManagement
{
    public class StringNotNullOrEmptyConverter : IValueConverter
    {
        public static readonly StringNotNullOrEmptyConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is string str && !string.IsNullOrWhiteSpace(str);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
