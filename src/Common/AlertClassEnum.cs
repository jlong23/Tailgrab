using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;

namespace Tailgrab.Common
{
    public enum AlertClassEnum
    {
        Avatar = 0,
        Group = 1,
        Profile = 2,
        Print = 3,
        EmojiSticker = 4,
        Moderation = 5,
    }

    public class AlertClassEnumMapper
    {
        public static string MapEnumToDescription(AlertClassEnum alertClassEnum)
        {
            return alertClassEnum switch
            {
                AlertClassEnum.Avatar => "Avatar",
                AlertClassEnum.Group => "Group",
                AlertClassEnum.Profile => "Profile",
                AlertClassEnum.Print => "Print",
                AlertClassEnum.EmojiSticker => "Emoji Sticker",
                AlertClassEnum.Moderation => "Moderation",
                _ => "Unknown alert class",
            };
        }


        public static Geometry GetAlertClassIcon(AlertClassEnum alertClass)
        {
            return alertClass switch
            {
                AlertClassEnum.Avatar => Geometry.Parse("M10 4A4 4 0 0 1 14 8A4 4 0 0 1 10 12A4 4 0 0 1 6 8A4 4 0 0 1 10 4M10 14C14.42 14 18 15.79 18 18V20H2V18C2 15.79 5.58 14 10 14M20 12V7H22V13H20M20 17V15H22V17H20Z"), // PLACEHOLDER: Avatar icon
                AlertClassEnum.Group => Geometry.Parse("M12,5.5A3.5,3.5 0 0,1 15.5,9A3.5,3.5 0 0,1 12,12.5A3.5,3.5 0 0,1 8.5,9A3.5,3.5 0 0,1 12,5.5M5,8C5.56,8 6.08,8.15 6.53,8.42C6.38,9.85 6.8,11.27 7.66,12.38C7.16,13.34 6.16,14 5,14A3,3 0 0,1 2,11A3,3 0 0,1 5,8M19,8A3,3 0 0,1 22,11A3,3 0 0,1 19,14C17.84,14 16.84,13.34 16.34,12.38C17.2,11.27 17.62,9.85 17.47,8.42C17.92,8.15 18.44,8 19,8M5.5,18.25C5.5,16.18 8.41,14.5 12,14.5C15.59,14.5 18.5,16.18 18.5,18.25V20H5.5V18.25M0,20V18.5C0,17.11 1.89,15.94 4.45,15.6C3.86,16.28 3.5,17.22 3.5,18.25V20H0M24,20H20.5V18.25C20.5,17.22 20.14,16.28 19.55,15.6C22.11,15.94 24,17.11 24,18.5V20Z"), // PLACEHOLDER: Group icon
                AlertClassEnum.Profile => Geometry.Parse("M15,3H12V6H8V3H5A2,2 0 0,0 3,5V21A2,2 0 0,0 5,23H15A2,2 0 0,0 17,21V5A2,2 0 0,0 15,3M10,8A2,2 0 0,1 12,10A2,2 0 0,1 10,12A2,2 0 0,1 8,10A2,2 0 0,1 10,8M14,16H6V15C6,13.67 8.67,13 10,13C11.33,13 14,13.67 14,15V16M11,5H9V1H11V5M14,19H6V18H14V19M10,21H6V20H10V21M19,13V7H21V13H19M19,17V15H21V17H19Z"), // PLACEHOLDER: Profile icon
                AlertClassEnum.Print => Geometry.Parse("M8.5,13.5L11,16.5L14.5,12L19,18H5M21,19V5C21,3.89 20.1,3 19,3H5A2,2 0 0,0 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19Z"), // PLACEHOLDER: Print icon
                AlertClassEnum.EmojiSticker => Geometry.Parse("M18.5 2H5.5C3.6 2 2 3.6 2 5.5V18.5C2 20.4 3.6 22 5.5 22H16L22 16V5.5C22 3.6 20.4 2 18.5 2M13 17H11V15H13V16M13 13H11V7H13V12M15 20V18.5C15 16.6 16.6 15 18.5 15H20L15 20Z"), // PLACEHOLDER: Emoji/Sticker icon
                AlertClassEnum.Moderation => Geometry.Parse("M3 16.35A13.35 13.35 0 0 1 16.34 3h95.32A13.35 13.35 0 0 1 125 16.35v72.44a13.35 13.35 0 0 1-13.35 13.35H64.46l-19.62 19.62a11.12 11.12 0 0 1-19-7.86v-11.76h-9.5A13.35 13.35 0 0 1 3 88.79zM16.35 14.44a1.91 1.91 0 0 0-1.91 1.91v72.44a1.92 1.92 0 0 0 1.91 1.91h15.24a5.72 5.72 0 0 1 5.72 5.72v16.7l20.74-20.74a5.69 5.69 0 0 1 4-1.68h49.57a1.92 1.92 0 0 0 1.91-1.91V16.35a1.91 1.91 0 0 0-1.91-1.91zM69.72 31.6v19.06a5.72 5.72 0 0 1-11.44 0V31.6a5.72 5.72 0 0 1 11.44 0M71.63 71.6A7.63 7.63 0 1 1 64 64a7.64 7.64 0 0 1 7.63 7.62"), // PLACEHOLDER: Moderation/Shield icon
                _ => Geometry.Parse("M13,9H11V7H13M13,17H11V11H13M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2Z"), // PLACEHOLDER: Default/Info icon
            };
        }

        public static WpfBrush GetAlertClassIconBrush(AlertClassEnum alertClass)
        {
            return alertClass switch
            {
                AlertClassEnum.Avatar => WpfBrushes.White,
                AlertClassEnum.Group => WpfBrushes.White,
                AlertClassEnum.Profile => WpfBrushes.White,     // PLACEHOLDER: Purple
                AlertClassEnum.Print => WpfBrushes.White,       // PLACEHOLDER: Orange
                AlertClassEnum.EmojiSticker => WpfBrushes.White,// PLACEHOLDER: Yellow
                AlertClassEnum.Moderation => WpfBrushes.White,  // PLACEHOLDER: Red
                _ => WpfBrushes.White,                          // PLACEHOLDER: White
            };
        }


    }
}