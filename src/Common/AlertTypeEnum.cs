using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;

namespace Tailgrab.Common
{
    public enum AlertTypeEnum
    {
        None = 0,
        Watch = 1,
        Nuisance = 2,
        Crasher = 3

    }

    public class AlertTypeEnumMapper
    {
        public static string MapEnumToDescription(AlertTypeEnum alertTypeEnum)
        {
            return alertTypeEnum switch
            {
                AlertTypeEnum.None => "None",
                AlertTypeEnum.Watch => "Watch",
                AlertTypeEnum.Nuisance => "Nuisance",
                AlertTypeEnum.Crasher => "Crasher",
                _ => "None",
            };
        }

        public static AlertTypeEnum MapStringToEnum(string alertType)
        {
            return alertType switch
            {
                "Watch" => AlertTypeEnum.Watch,
                "Nuisance" => AlertTypeEnum.Nuisance,
                "Crasher" => AlertTypeEnum.Crasher,
                _ => AlertTypeEnum.None
            };
        }

        public static Geometry MapEnumToIcon(AlertTypeEnum alertType)
        {
            var resources = System.Windows.Application.Current.Resources;
            string key = alertType switch
            {
                AlertTypeEnum.None => "Icon.AlertTypeEnum.None",
                AlertTypeEnum.Watch => "Icon.AlertTypeEnum.Watch",
                AlertTypeEnum.Nuisance => "Icon.AlertTypeEnum.Nuisance",
                AlertTypeEnum.Crasher => "Icon.AlertTypeEnum.Crasher",
                _ => "Icon.AlertTypeEnum.Default",
            };
            return (Geometry)resources[key];
        }

        public static WpfBrush MapEnumToIconBrush(AlertTypeEnum alertType)
        {
            var resources = System.Windows.Application.Current.Resources;
            string key = alertType switch
            {
                AlertTypeEnum.None => "Brush.AlertTypeEnum.None",
                AlertTypeEnum.Watch => "Brush.AlertTypeEnum.Watch",
                AlertTypeEnum.Nuisance => "Brush.AlertTypeEnum.Nuisance",
                AlertTypeEnum.Crasher => "Brush.AlertTypeEnum.Crasher",
                _ => "Brush.AlertTypeEnum.Default",
            };
            return (WpfBrush)resources[key];
        }

    }
}
