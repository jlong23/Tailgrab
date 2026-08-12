using NLog;
using System.Windows.Media;
using Tailgrab.PlayerManagement;
using WpfBrush = System.Windows.Media.Brush;

namespace Tailgrab.Common
{
    public enum AvatarPerformanceEnum
    {
        UNRATED = 0,
        EXCELLENT = 1,
        GOOD = 2,
        MEDIUM = 3,
        POOR = 4,
        VERY_POOR = 5,
    }

    public class AvatarPerformanceEnumMapper
    {
        public static string MapEnumToString(AvatarPerformanceEnum avatarPerformanceStatus)
        {
            var resources = System.Windows.Application.Current.Resources;
            string key = avatarPerformanceStatus switch
            {
                AvatarPerformanceEnum.UNRATED => "Text.AvatarPerformanceEnum.UNRATED",
                AvatarPerformanceEnum.EXCELLENT => "Text.AvatarPerformanceEnum.EXCELLENT",
                AvatarPerformanceEnum.GOOD => "Text.AvatarPerformanceEnum.GOOD",
                AvatarPerformanceEnum.MEDIUM => "Text.AvatarPerformanceEnum.MEDIUM",
                AvatarPerformanceEnum.POOR => "Text.AvatarPerformanceEnum.POOR",
                AvatarPerformanceEnum.VERY_POOR => "Text.AvatarPerformanceEnum.VERY_POOR",
                _ => "Text.AvatarPerformanceEnum.Default",
            };
            return (string)resources[key];
        }

        public static AvatarPerformanceEnum MapStringToEnum(string avatarPerformanceStatus)
        {
            return avatarPerformanceStatus.ToLower() switch
            {
                "unrated" => AvatarPerformanceEnum.UNRATED,
                "excellent" => AvatarPerformanceEnum.EXCELLENT,
                "good" => AvatarPerformanceEnum.GOOD,
                "medium" => AvatarPerformanceEnum.MEDIUM,
                "poor" => AvatarPerformanceEnum.POOR,
                "verypoor" => AvatarPerformanceEnum.VERY_POOR,
                "very poor" => AvatarPerformanceEnum.VERY_POOR,
                _ => AvatarPerformanceEnum.UNRATED,
            };
        }

        public static AlertDisplayItem MapEnumToAlertDisplayItem(string value)
        {
            var status = MapStringToEnum(value);
            return MapEnumToAlertDisplayItem(status);
        }

        public static AlertDisplayItem MapEnumToAlertDisplayItem(AvatarPerformanceEnum status)
        {
            return new AlertDisplayItem
            {
                IconGeometry = MapEnumToIcon(status),
                IconBrush = MapEnumToBrush(status),
                IconClass = "User Trust",
                Description = MapEnumToString(status),
                AlertColor = MapEnumToBrush(status).ToString()
            };
        }


        public static Geometry MapEnumToIcon(AvatarPerformanceEnum status)
        {
            var resources = System.Windows.Application.Current.Resources;
            string key = status switch
            {
                AvatarPerformanceEnum.UNRATED => "Icon.AvatarPerformanceEnum.UNRATED",
                AvatarPerformanceEnum.EXCELLENT => "Icon.AvatarPerformanceEnum.EXCELLENT",
                AvatarPerformanceEnum.GOOD => "Icon.AvatarPerformanceEnum.GOOD",
                AvatarPerformanceEnum.MEDIUM => "Icon.AvatarPerformanceEnum.MEDIUM",
                AvatarPerformanceEnum.POOR => "Icon.AvatarPerformanceEnum.POOR",
                AvatarPerformanceEnum.VERY_POOR => "Icon.AvatarPerformanceEnum.VERY_POOR",
                _ => "Icon.AvatarPerformanceEnum.Default",
            };
            return (Geometry)resources[key];
        }

        public static WpfBrush MapEnumToBrush(AvatarPerformanceEnum status)
        {
            var resources = System.Windows.Application.Current.Resources;
            string key = status switch
            {
                AvatarPerformanceEnum.UNRATED => "Brush.AvatarPerformanceEnum.UNRATED",
                AvatarPerformanceEnum.EXCELLENT => "Brush.AvatarPerformanceEnum.EXCELLENT",
                AvatarPerformanceEnum.GOOD => "Brush.AvatarPerformanceEnum.GOOD",
                AvatarPerformanceEnum.MEDIUM => "Brush.AvatarPerformanceEnum.MEDIUM",
                AvatarPerformanceEnum.POOR => "Brush.AvatarPerformanceEnum.POOR",
                AvatarPerformanceEnum.VERY_POOR => "Brush.AvatarPerformanceEnum.VERY_POOR",
                _ => "Brush.AvatarPerformanceEnum.Default",
            };
            return (WpfBrush)resources[key];
        }
    }

}
