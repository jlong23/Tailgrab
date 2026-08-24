using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Tailgrab.PlayerManagement
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (parameter is null || value is null)
                return false;

            return value.Equals(parameter);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is true && parameter is not null)
                return parameter;

            return DependencyProperty.UnsetValue;
        }
    }
}
