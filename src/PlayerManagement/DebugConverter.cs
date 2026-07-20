using System;
using System.Globalization;
using System.Windows.Data;

namespace Tailgrab.PlayerManagement
{
    public class DebugConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                Console.WriteLine($"DebugConverter: value is NULL");
                return null;
            }

            Console.WriteLine($"DebugConverter: value type = {value.GetType().Name}, value = {value}");

            if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                int count = 0;
                foreach (var item in enumerable)
                {
                    Console.WriteLine($"  Item {count}: {item.GetType().Name}");
                    count++;
                    if (count > 5) break; // Limit output
                }
                Console.WriteLine($"  Total items shown: {count}");
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
