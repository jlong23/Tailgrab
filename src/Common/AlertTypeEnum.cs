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
            return alertType switch
            {
                AlertTypeEnum.None => Geometry.Parse("M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2Z"), // PLACEHOLDER: None/Circle icon
                AlertTypeEnum.Watch => Geometry.Parse("M12,9A3,3 0 0,0 9,12A3,3 0 0,0 12,15A3,3 0 0,0 15,12A3,3 0 0,0 12,9M12,17A5,5 0 0,1 7,12A5,5 0 0,1 12,7A5,5 0 0,1 17,12A5,5 0 0,1 12,17M12,4.5C7,4.5 2.73,7.61 1,12C2.73,16.39 7,19.5 12,19.5C17,19.5 21.27,16.39 23,12C21.27,7.61 17,4.5 12,4.5Z"), // PLACEHOLDER: Watch/Eye icon
                AlertTypeEnum.Nuisance => Geometry.Parse("M12,4L9.91,6.09L12,8.18M4.27,3L3,4.27L7.73,9H3V15H7L12,20V13.27L16.25,17.53C15.58,18.04 14.83,18.46 14,18.7V20.77C15.38,20.45 16.63,19.82 17.68,18.96L19.73,21L21,19.73L12,10.73M19,12C19,12.94 18.8,13.82 18.46,14.64L19.97,16.15C20.62,14.91 21,13.5 21,12C21,7.72 18,4.14 14,3.23V5.29C16.89,6.15 19,8.83 19,12M16.5,12C16.5,10.23 15.5,8.71 14,7.97V10.18L16.45,12.63C16.5,12.43 16.5,12.21 16.5,12Z"), // PLACEHOLDER: Nuisance/Warning icon
                AlertTypeEnum.Crasher => Geometry.Parse("M11.25,6A3.25,3.25 0 0,1 14.5,2.75A3.25,3.25 0 0,1 17.75,6C17.75,6.42 18.08,6.75 18.5,6.75C18.92,6.75 19.25,6.42 19.25,6V5.25H20.75V6A2.25,2.25 0 0,1 18.5,8.25A2.25,2.25 0 0,1 16.25,6A1.75,1.75 0 0,0 14.5,4.25A1.75,1.75 0 0,0 12.75,6H14V7.29C16.89,8.15 19,10.83 19,14A7,7 0 0,1 12,21A7,7 0 0,1 5,14C5,10.83 7.11,8.15 10,7.29V6H11.25M22,6H24V7H22V6M19,4V2H20V4H19M20.91,4.38L22.33,2.96L23.04,3.67L21.62,5.09L20.91,4.38Z"), // PLACEHOLDER: Crasher/Alert icon
                _ => Geometry.Parse("M13,9H11V7H13M13,17H11V11H13M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2Z"), // PLACEHOLDER: Default/Info icon
            };
        }

        public static WpfBrush MapEnumToIconBrush(AlertTypeEnum alertType)
        {
            return alertType switch
            {
                AlertTypeEnum.None => WpfBrushes.White,         // PLACEHOLDER: White
                AlertTypeEnum.Watch => WpfBrushes.White,        // PLACEHOLDER: White
                AlertTypeEnum.Nuisance => WpfBrushes.White,     // PLACEHOLDER: Orange
                AlertTypeEnum.Crasher => WpfBrushes.White,      // PLACEHOLDER: Red
                _ => WpfBrushes.White,                          // PLACEHOLDER: White
            };
        }

    }
}
