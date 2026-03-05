using System;
using System.Globalization;
using System.Windows.Data;

namespace Tailgrab.PlayerManagement
{
    public class SoundNotNoneConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length > 0 && values[0] is string soundValue)
            {
                return !string.IsNullOrWhiteSpace(soundValue) && 
                       !soundValue.Equals("*NONE", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
