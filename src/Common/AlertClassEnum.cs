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
            var resources = System.Windows.Application.Current.Resources;
            string key = alertClassEnum switch
            {
                AlertClassEnum.Avatar => "Text.AlertClassEnum.Avatar",
                AlertClassEnum.Group => "Text.AlertClassEnum.Group",
                AlertClassEnum.Profile => "Text.AlertClassEnum.Profile",
                AlertClassEnum.Print => "Text.AlertClassEnum.Print",
                AlertClassEnum.EmojiSticker => "Text.AlertClassEnum.EmojiSticker",
                AlertClassEnum.Moderation => "Text.AlertClassEnum.Moderation",
                _ => "Text.AlertClassEnum.Default",
            };
            return (string)resources[key];  
        }


        public static Geometry GetAlertClassIcon(AlertClassEnum alertClass)
        {
            var resources = System.Windows.Application.Current.Resources;
            string key = alertClass switch
            {
                AlertClassEnum.Avatar => "Icon.AlertClass.Avatar",
                AlertClassEnum.Group => "Icon.AlertClass.Group",
                AlertClassEnum.Profile => "Icon.AlertClass.Profile",
                AlertClassEnum.Print => "Icon.AlertClass.Print",
                AlertClassEnum.EmojiSticker => "Icon.AlertClass.EmojiSticker",
                AlertClassEnum.Moderation => "Icon.AlertClass.Moderation",
                _ => "Icon.AlertClass.Default",
            };
            return (Geometry)resources[key];
        }

        public static WpfBrush GetAlertClassIconBrush(AlertClassEnum alertClass)
        {
            var resources = System.Windows.Application.Current.Resources;
            string key = alertClass switch
            {
                AlertClassEnum.Avatar => "Brush.AlertClassEnum.Avatar",
                AlertClassEnum.Group => "Brush.AlertClassEnum.Group",
                AlertClassEnum.Profile => "Brush.AlertClassEnum.Profile",
                AlertClassEnum.Print => "Brush.AlertClassEnum.Print",
                AlertClassEnum.EmojiSticker => "Brush.AlertClassEnum.EmojiSticker",
                AlertClassEnum.Moderation => "Brush.AlertClassEnum.Moderation",
                _ => "Brush.AlertClassEnum.Default",
            };
            return (WpfBrush)resources[key];    
        }
    }
}