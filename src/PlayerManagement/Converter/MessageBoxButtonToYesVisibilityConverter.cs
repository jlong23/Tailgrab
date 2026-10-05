using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Tailgrab.PlayerManagement.Converter
{
    public class MessageBoxButtonToYesVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MessageBoxButton buttonType)
            {
                return buttonType == MessageBoxButton.YesNo || buttonType == MessageBoxButton.YesNoCancel
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
