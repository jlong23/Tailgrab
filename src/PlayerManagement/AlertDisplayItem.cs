using System.Windows.Media;
using Tailgrab.Common;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;

namespace Tailgrab.PlayerManagement
{
    /// <summary>
    /// Represents a display item for alert messages with icon and text
    /// </summary>
    public class AlertDisplayItem
    {
        public Geometry? IconGeometry { get; set; }
        public WpfBrush IconBrush { get; set; } = WpfBrushes.White;

        public string IconClass { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string AlertColor { get; set; } = string.Empty;
    }

    /// <summary>
    /// Helper class to map enums to icon placeholders
    /// </summary>
    public static class AlertIconMapper
    {
        // Placeholder icon data - to be replaced with actual system resource icons
        public static Geometry GetAlertClassIcon(AlertClassEnum alertClass)
        {
            return alertClass switch
            {
                AlertClassEnum.Avatar => Geometry.Parse("M10 4A4 4 0 0 1 14 8A4 4 0 0 1 10 12A4 4 0 0 1 6 8A4 4 0 0 1 10 4M10 14C14.42 14 18 15.79 18 18V20H2V18C2 15.79 5.58 14 10 14M20 12V7H22V13H20M20 17V15H22V17H20Z"), // PLACEHOLDER: Avatar icon
                AlertClassEnum.Group => Geometry.Parse("M12,5.5A3.5,3.5 0 0,1 15.5,9A3.5,3.5 0 0,1 12,12.5A3.5,3.5 0 0,1 8.5,9A3.5,3.5 0 0,1 12,5.5M5,8C5.56,8 6.08,8.15 6.53,8.42C6.38,9.85 6.8,11.27 7.66,12.38C7.16,13.34 6.16,14 5,14A3,3 0 0,1 2,11A3,3 0 0,1 5,8M19,8A3,3 0 0,1 22,11A3,3 0 0,1 19,14C17.84,14 16.84,13.34 16.34,12.38C17.2,11.27 17.62,9.85 17.47,8.42C17.92,8.15 18.44,8 19,8M5.5,18.25C5.5,16.18 8.41,14.5 12,14.5C15.59,14.5 18.5,16.18 18.5,18.25V20H5.5V18.25M0,20V18.5C0,17.11 1.89,15.94 4.45,15.6C3.86,16.28 3.5,17.22 3.5,18.25V20H0M24,20H20.5V18.25C20.5,17.22 20.14,16.28 19.55,15.6C22.11,15.94 24,17.11 24,18.5V20Z"), // PLACEHOLDER: Group icon
                AlertClassEnum.Profile => Geometry.Parse("M15,3H12V6H8V3H5A2,2 0 0,0 3,5V21A2,2 0 0,0 5,23H15A2,2 0 0,0 17,21V5A2,2 0 0,0 15,3M10,8A2,2 0 0,1 12,10A2,2 0 0,1 10,12A2,2 0 0,1 8,10A2,2 0 0,1 10,8M14,16H6V15C6,13.67 8.67,13 10,13C11.33,13 14,13.67 14,15V16M11,5H9V1H11V5M14,19H6V18H14V19M10,21H6V20H10V21M19,13V7H21V13H19M19,17V15H21V17H19Z"), // PLACEHOLDER: Profile icon
                AlertClassEnum.Print => Geometry.Parse("M8.5,13.5L11,16.5L14.5,12L19,18H5M21,19V5C21,3.89 20.1,3 19,3H5A2,2 0 0,0 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19Z"), // PLACEHOLDER: Print icon
                AlertClassEnum.EmojiSticker => Geometry.Parse("M18.5 2H5.5C3.6 2 2 3.6 2 5.5V18.5C2 20.4 3.6 22 5.5 22H16L22 16V5.5C22 3.6 20.4 2 18.5 2M13 17H11V15H13V16M13 13H11V7H13V12M15 20V18.5C15 16.6 16.6 15 18.5 15H20L15 20Z"), // PLACEHOLDER: Emoji/Sticker icon
                AlertClassEnum.Moderation => Geometry.Parse("M20 17H22V15H20V17M20 7V13H22V7H20M11 9H16.5L11 3.5V9M4 2H12L18 8V20C18 21.11 17.11 22 16 22H4C2.89 22 2 21.1 2 20V4C2 2.89 2.89 2 4 2M13 18V16H4V18H13M16 14V12H4V14H16Z"), // PLACEHOLDER: Moderation/Shield icon
                _ => Geometry.Parse("M13,9H11V7H13M13,17H11V11H13M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2Z"), // PLACEHOLDER: Default/Info icon
            };
        }

        public static Geometry GetAlertTypeIcon(AlertTypeEnum alertType)
        {
            return alertType switch
            {
                AlertTypeEnum.None => Geometry.Parse("M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2Z"), // PLACEHOLDER: None/Circle icon
                AlertTypeEnum.Watch => Geometry.Parse("M6 8C6 5.79 7.79 4 10 4S14 5.79 14 8 12.21 12 10 12 6 10.21 6 8M9.14 19.75L8.85 19L9.14 18.25C9.84 16.5 11.08 15.14 12.61 14.22C11.79 14.08 10.92 14 10 14C5.58 14 2 15.79 2 18V20H9.27C9.23 19.91 9.18 19.83 9.14 19.75M17 18C16.44 18 16 18.44 16 19S16.44 20 17 20 18 19.56 18 19 17.56 18 17 18M23 19C22.06 21.34 19.73 23 17 23S11.94 21.34 11 19C11.94 16.66 14.27 15 17 15S22.06 16.66 23 19M19.5 19C19.5 17.62 18.38 16.5 17 16.5S14.5 17.62 14.5 19 15.62 21.5 17 21.5 19.5 20.38 19.5 19Z"), // PLACEHOLDER: Watch/Eye icon
                AlertTypeEnum.Nuisance => Geometry.Parse("M5,3H19A2,2 0 0,1 21,5V19A2,2 0 0,1 19,21H5A2,2 0 0,1 3,19V5A2,2 0 0,1 5,3M13,13V7H11V13H13M13,17V15H11V17H13Z"), // PLACEHOLDER: Nuisance/Warning icon
                AlertTypeEnum.Crasher => Geometry.Parse("M9.9 6.6L9.1 7.4L10.3 8.6C9.8 8.9 9.4 9.4 9.2 10H7V11H9V12H7V13H9V14H7V15H9.2C9.6 16.2 10.7 17 12 17S14.4 16.2 14.8 15H17V14H15V13H17V12H15V11H17V10H14.8C14.6 9.4 14.2 8.9 13.7 8.5L14.9 7.3L14.2 6.6L12.8 8H12C11.8 8 11.5 8 11.3 8.1L9.9 6.6M11 11H13V12H11V11M11 13H13V14H11V13M21 11C21 16.5 17.2 21.7 12 23C6.8 21.7 3 16.5 3 11V5L12 1L21 5V11M12 21C15.8 20 19 15.5 19 11.2V6.3L12 3.2L5 6.3V11.2C5 15.5 8.2 20 12 21Z"), // PLACEHOLDER: Crasher/Alert icon
                _ => Geometry.Parse("M13,9H11V7H13M13,17H11V11H13M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2Z"), // PLACEHOLDER: Default/Info icon
            };
        }

        public static WpfBrush GetAlertClassIconBrush(AlertClassEnum alertClass)
        {
            return alertClass switch
            {
                AlertClassEnum.Avatar => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#FFFFFF")),      // PLACEHOLDER: Green
                AlertClassEnum.Group => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#FFFFFF")),       // PLACEHOLDER: Blue
                AlertClassEnum.Profile => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#FFFFFF")),     // PLACEHOLDER: Purple
                AlertClassEnum.Print => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#FFFFFF")),       // PLACEHOLDER: Orange
                AlertClassEnum.EmojiSticker => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#FFFFFF")),// PLACEHOLDER: Yellow
                AlertClassEnum.Moderation => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#FFFFFF")),  // PLACEHOLDER: Red
                _ => WpfBrushes.White,                          // PLACEHOLDER: White
            };
        }

        public static WpfBrush GetAlertTypeIconBrush(AlertTypeEnum alertType)
        {
            return alertType switch
            {
                AlertTypeEnum.None => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#9E9E9E")),         // PLACEHOLDER: Gray
                AlertTypeEnum.Watch => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#EBDB34")),        // PLACEHOLDER: Blue
                AlertTypeEnum.Nuisance => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#EBDB34")),     // PLACEHOLDER: Orange
                AlertTypeEnum.Crasher => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#EBDB34")),      // PLACEHOLDER: Red
                _ => WpfBrushes.White,                          // PLACEHOLDER: White
            };
        }
    }
}
