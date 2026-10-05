using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Tailgrab.PlayerManagement.Converter
{
    public class MessageBoxImageToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is MessageBoxImage imageType)
            {
                return imageType switch
                {
                    MessageBoxImage.Warning => (System.Windows.Media.Brush)new BrushConverter().ConvertFrom("#FFFFCC00")!,
                    MessageBoxImage.Error => (System.Windows.Media.Brush)new BrushConverter().ConvertFrom("#FFFF4444")!,
                    MessageBoxImage.Information => (System.Windows.Media.Brush)new BrushConverter().ConvertFrom("#FF4488FF")!,
                    MessageBoxImage.Question => (System.Windows.Media.Brush)new BrushConverter().ConvertFrom("#FF88CCFF")!,
                    _ => (System.Windows.Media.Brush)new BrushConverter().ConvertFrom("#FFE6E6E6")!
                };
            }
            return (System.Windows.Media.Brush)new BrushConverter().ConvertFrom("#FFE6E6E6")!;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
