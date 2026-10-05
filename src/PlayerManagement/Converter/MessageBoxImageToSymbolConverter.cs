using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Tailgrab.PlayerManagement.Converter
{
    public class MessageBoxImageToSymbolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MessageBoxImage imageType)
            {
                return imageType switch
                {
                    MessageBoxImage.Warning => "⚠",
                    MessageBoxImage.Error => "❌",
                    MessageBoxImage.Information => "ℹ",
                    MessageBoxImage.Question => "❓",
                    _ => string.Empty
                };
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
