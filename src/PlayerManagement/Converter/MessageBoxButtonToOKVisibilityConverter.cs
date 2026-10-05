using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Tailgrab.PlayerManagement.Converter
{
    public class MessageBoxButtonToOKVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MessageBoxButton buttonType)
            {
                return buttonType == MessageBoxButton.OK || buttonType == MessageBoxButton.OKCancel
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
