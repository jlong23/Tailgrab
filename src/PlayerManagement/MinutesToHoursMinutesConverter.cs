using System;
using System.Globalization;
using System.Windows.Data;

namespace Tailgrab.PlayerManagement
{
    public class MinutesToHoursMinutesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double minutes)
            {
                int totalMinutes = (int)Math.Round(minutes);
                int hours = totalMinutes / 60;
                int mins = totalMinutes % 60;
                return $"{hours}:{mins:D2}";
            }
            return "0:00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}