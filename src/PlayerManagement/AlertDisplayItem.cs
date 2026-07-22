using System.Windows.Media;
using Tailgrab.Common;
using VRChat.API.Model;
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
                AlertClassEnum.Moderation => Geometry.Parse("M3 16.35A13.35 13.35 0 0 1 16.34 3h95.32A13.35 13.35 0 0 1 125 16.35v72.44a13.35 13.35 0 0 1-13.35 13.35H64.46l-19.62 19.62a11.12 11.12 0 0 1-19-7.86v-11.76h-9.5A13.35 13.35 0 0 1 3 88.79zM16.35 14.44a1.91 1.91 0 0 0-1.91 1.91v72.44a1.92 1.92 0 0 0 1.91 1.91h15.24a5.72 5.72 0 0 1 5.72 5.72v16.7l20.74-20.74a5.69 5.69 0 0 1 4-1.68h49.57a1.92 1.92 0 0 0 1.91-1.91V16.35a1.91 1.91 0 0 0-1.91-1.91zM69.72 31.6v19.06a5.72 5.72 0 0 1-11.44 0V31.6a5.72 5.72 0 0 1 11.44 0M71.63 71.6A7.63 7.63 0 1 1 64 64a7.64 7.64 0 0 1 7.63 7.62"), // PLACEHOLDER: Moderation/Shield icon
                _ => Geometry.Parse("M13,9H11V7H13M13,17H11V11H13M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2Z"), // PLACEHOLDER: Default/Info icon
            };
        }

        public static Geometry GetAlertTypeIcon(AlertTypeEnum alertType)
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

        public static Geometry GetUserTrustIcon(TrustClassEnum userTrust)
        {
            return userTrust switch
            {
                TrustClassEnum.VISITOR => Geometry.Parse("M100 1.5195c-3.21 0-6.14 1.7412-12 5.2012l-2 1.1699c-5.81 3.47-8.7806 5.1986-10.3906 8.0586S74 22.2809 74 29.2109v2.3496c0 6.94-6e-4 10.4098 1.6094 13.2598S80.15 49.4109 86 52.8809l2 1.1699c5.86 3.47 8.78 5.1992 12 5.1992s6.14-1.7292 12-5.1992l2-1.1699c5.86-3.47 8.7806-5.2006 10.3906-8.0606 1.61-2.86 1.6094-6.3298 1.6094-13.2598v-2.3496c0-6.93 6e-4-10.4117-1.6094-13.2617S119.86 11.3606 114 7.8906l-2-1.1699c-5.86-3.47-8.79-5.2012-12-5.2012zM100 13.7891A2.17 2.17 0 0 1 102.1894 16l0 17.2695a2.17 2.17 0 1 1-4.3301 0l0-17.2695A2.16 2.16 0 0 1 100 13.7891zM45 30a23.34 23.34 0 1 0 23.3301 23.3301A23.33 23.33 0 0 0 45 30zM100 39.0508a2.89 2.89 0 0 1 2.8906 2.8789A2.89 2.89 0 1 1 100 39.0508zM35.6699 81.3301A32.71 32.71 0 0 0 3 114v4.6699a4.68 4.68 0 0 0 4.6699 4.6602l74.6602 0A4.68 4.68 0 0 0 87 118.6699V114a32.71 32.71 0 0 0-32.6699-32.6699z"),
                TrustClassEnum.NEW_USER => Geometry.Parse("M75.5508 4.3008A2.55 2.55 0 0 0 73 6.8496v47.25l11.3809-8.9297 37.1699 0a2.56 2.56 0 0 0 2.5488-2.5605l0-35.7598a2.55 2.55 0 0 0-2.5488-2.5488zM96 14.5195l5.0996 0 0 7.6602 7.6699 0 0 5.1094-7.6601 0 0 7.7109H96v-7.7109l-7.6797 0 0-5.1094 7.6797 0zM45 30a23.34 23.34 0 1 0 23.3301 23.3301A23.33 23.33 0 0 0 45 30zM35.6699 81.3301A32.71 32.71 0 0 0 3 114v4.6699a4.68 4.68 0 0 0 4.6699 4.6602l74.6602 0A4.68 4.68 0 0 0 87 118.6699V114a32.71 32.71 0 0 0-32.6699-32.6699z"),
                TrustClassEnum.USER => Geometry.Parse("M45 30a23.34 23.34 0 1 0 23.33 23.33A23.33 23.33 0 0 0 45 30M7.67 123.33h74.66a4.68 4.68 0 0 0 4.67-4.66V114a32.71 32.71 0 0 0-32.67-32.67H35.67A32.71 32.71 0 0 0 3 114v4.67a4.68 4.68 0 0 0 4.67 4.66"),
                TrustClassEnum.KNOWN_USER => Geometry.Parse("M83.2402 5a5.69 5.69 0 0 0-4.1699 1.6992 5.69 5.69 0 0 0-1.7305 4.1699l0 47.2305A5.91 5.91 0 0 0 83.2402 64h35.42a5.92 5.92 0 0 0 5.9102-5.9004l0-47.1894a5.69 5.69 0 0 0-1.7402-4.1699A5.66 5.66 0 0 0 118.6602 5zM98 10.8691l14.7598 0 0 20.6602-7.3789-4.4297L98 31.5293zM45 30a23.34 23.34 0 1 0 23.3301 23.3301A23.33 23.33 0 0 0 45 30zM35.6699 81.3301A32.71 32.71 0 0 0 3 114v4.6699a4.68 4.68 0 0 0 4.6699 4.6602l74.6602 0A4.68 4.68 0 0 0 87 118.6699V114a32.71 32.71 0 0 0-32.6699-32.6699z"),
                TrustClassEnum.TRUSTED_USER => Geometry.Parse("M83.2402 5a5.69 5.69 0 0 0-4.1699 1.6992 5.69 5.69 0 0 0-1.7305 4.1699l0 47.2305A5.91 5.91 0 0 0 83.2402 64h35.42a5.92 5.92 0 0 0 5.9102-5.9004l0-47.1894a5.69 5.69 0 0 0-1.7402-4.1699A5.66 5.66 0 0 0 118.6602 5zM98 10.8691l14.7598 0 0 20.6602-7.3789-4.4297L98 31.5293zM45 30a23.34 23.34 0 1 0 23.3301 23.3301A23.33 23.33 0 0 0 45 30zM35.6699 81.3301A32.71 32.71 0 0 0 3 114v4.6699a4.68 4.68 0 0 0 4.6699 4.6602l74.6602 0A4.68 4.68 0 0 0 87 118.6699V114a32.71 32.71 0 0 0-32.6699-32.6699z"),
                TrustClassEnum.PROBABLE_TROLL => Geometry.Parse("M12,4L9.91,6.09L12,8.18M4.27,3L3,4.27L7.73,9H3V15H7L12,20V13.27L16.25,17.53C15.58,18.04 14.83,18.46 14,18.7V20.77C15.38,20.45 16.63,19.82 17.68,18.96L19.73,21L21,19.73L12,10.73M19,12C19,12.94 18.8,13.82 18.46,14.64L19.97,16.15C20.62,14.91 21,13.5 21,12C21,7.72 18,4.14 14,3.23V5.29C16.89,6.15 19,8.83 19,12M16.5,12C16.5,10.23 15.5,8.71 14,7.97V10.18L16.45,12.63C16.5,12.43 16.5,12.21 16.5,12Z"),
                TrustClassEnum.NUISANCE => Geometry.Parse("M12,4L9.91,6.09L12,8.18M4.27,3L3,4.27L7.73,9H3V15H7L12,20V13.27L16.25,17.53C15.58,18.04 14.83,18.46 14,18.7V20.77C15.38,20.45 16.63,19.82 17.68,18.96L19.73,21L21,19.73L12,10.73M19,12C19,12.94 18.8,13.82 18.46,14.64L19.97,16.15C20.62,14.91 21,13.5 21,12C21,7.72 18,4.14 14,3.23V5.29C16.89,6.15 19,8.83 19,12M16.5,12C16.5,10.23 15.5,8.71 14,7.97V10.18L16.45,12.63C16.5,12.43 16.5,12.21 16.5,12Z"),
                _ => Geometry.Empty,
            };
        }

        public static Geometry GetUserVerifiedStatusIcon(AgeVerificationStatus status)
        {
            return status switch
            {
                AgeVerificationStatus.verified => Geometry.Parse("M115.62 12.38H12.38A9.4 9.4 0 0 0 3 21.77v84.46a9.4 9.4 0 0 0 9.38 9.39h103.24a9.4 9.4 0 0 0 9.38-9.39V21.77a9.4 9.4 0 0 0-9.38-9.39zM62.29 92a4.68 4.68 0 0 1-5.71-3.37 14.09 14.09 0 0 0-27.27 0 4.69 4.69 0 1 1-9.09-2.34 23.38 23.38 0 0 1 9.6-13.58 18.77 18.77 0 1 1 26.54-0.29l-0.29 0.29a23.43 23.43 0 0 1 9.6 13.58 4.69 4.69 0 0 1-3.38 5.71zM101.54 78.08H78.08a4.7 4.7 0 1 1 0-9.39h23.46a4.7 4.7 0 0 1 0 9.39zM101.54 59.31H78.08a4.7 4.7 0 1 1 0-9.39h23.46a4.7 4.7 0 0 1 0 9.39zM52.33 59.31a9.39 9.39 0 1 1-9.39-9.39 9.38 9.38 0 0 1 9.39 9.39z"), // PLACEHOLDER: None/Circle icon,
                AgeVerificationStatus.hidden => Geometry.Parse("M44.55 50a8 8 0 0 0-3.69 0.9l11.29 9.59a7.87 7.87 0 0 0 0.4-2.49 8 8 0 0 0-8-8z M43 65.85l-5.95-5a8 8 0 0 0 5.95 5z M45.88 42 17.64 18a8 8 0 0 0-7.09 7.1l24 20.37A16 16 0 0 1 45.88 42z M71.87 51a4 4 0 0 1 2.63-1h20a4 4 0 0 1 0 8H80.11l9.41 8h5a4 4 0 0 1 4 4 3.93 3.93 0 0 1-1.08 2.71l17.06 14.51V26a8 8 0 0 0-8-8H33z M106.5 106a8 8 0 0 0 7.71-5.9L83.49 74h-9a4 4 0 0 1-0.38-8L60.04 54a16 16 0 0 1-1.59 11.78L105.71 106z M63.75 83.47a4 4 0 0 1-2.75 2.4 4 4 0 0 1-4.83-2.87 12 12 0 0 0-23.24 0 4 4 0 0 1-7.75-2 20 20 0 0 1 8.18-11.58A16 16 0 0 1 29.07 54L10.5 38.23V98a8 8 0 0 0 8 8h71.77z M120.5 117a5 5 0 0 1-3.24-1.19l-113-96a5 5 0 0 1 6.48-7.62l113 96a5 5 0 0 1-3.24 8.81z"), // PLACEHOLDER: None/Circle icon,
                AgeVerificationStatus.plus18 => Geometry.Parse("M115.62 12.38H12.38A9.4 9.4 0 0 0 3 21.77v84.46a9.4 9.4 0 0 0 9.38 9.39h103.24a9.4 9.4 0 0 0 9.38-9.39V21.77a9.4 9.4 0 0 0-9.38-9.39zM62.29 92a4.68 4.68 0 0 1-5.71-3.37 14.09 14.09 0 0 0-27.27 0 4.69 4.69 0 1 1-9.09-2.34 23.38 23.38 0 0 1 9.6-13.58 18.77 18.77 0 1 1 26.54-0.29l-0.29 0.29a23.43 23.43 0 0 1 9.6 13.58 4.69 4.69 0 0 1-3.38 5.71zM101.54 78.08H78.08a4.7 4.7 0 1 1 0-9.39h23.46a4.7 4.7 0 0 1 0 9.39zM101.54 59.31H78.08a4.7 4.7 0 1 1 0-9.39h23.46a4.7 4.7 0 0 1 0 9.39zM52.33 59.31a9.39 9.39 0 1 1-9.39-9.39 9.38 9.38 0 0 1 9.39 9.39z"), // PLACEHOLDER: None/Circle icon,
                _ => Geometry.Empty,
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
                AlertTypeEnum.None => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#FFFFFF")),         // PLACEHOLDER: Gray
                AlertTypeEnum.Watch => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#FFFFFF")),        // PLACEHOLDER: Blue
                AlertTypeEnum.Nuisance => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#FFFFFF")),     // PLACEHOLDER: Orange
                AlertTypeEnum.Crasher => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#FFFFFF")),      // PLACEHOLDER: Red
                _ => WpfBrushes.White,                          // PLACEHOLDER: White
            };
        }

        public static WpfBrush GetUserTrustIconBrush(TrustClassEnum userTrust)
        {
            return userTrust switch
            {
                TrustClassEnum.VISITOR => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#9E9E9E")),         // PLACEHOLDER: Gray
                TrustClassEnum.NEW_USER => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#2b5dfb")),        // PLACEHOLDER: Blue
                TrustClassEnum.USER => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#59fb2b")),            // PLACEHOLDER: Green          
                TrustClassEnum.KNOWN_USER => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#fb9d2b")),      // PLACEHOLDER: Orange
                TrustClassEnum.TRUSTED_USER => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#a12bfb")),    // PLACEHOLDER: Purple
                TrustClassEnum.PROBABLE_TROLL => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#9E9E9E")),  // PLACEHOLDER: Gray
                TrustClassEnum.NUISANCE => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#D5FF00")),        // PLACEHOLDER: Yellow
                _ => WpfBrushes.White,                                                                                           // PLACEHOLDER: White
            };
        }

        public static WpfBrush GetUserVerifiedStatusBrush(AgeVerificationStatus status)
        {
            return status switch
            {
                AgeVerificationStatus.verified => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#59fb2b")),  // PLACEHOLDER: Green
                AgeVerificationStatus.hidden => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#9E9E9E")),    // PLACEHOLDER: Gray
                AgeVerificationStatus.plus18 => new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#a12bfb")),    // PLACEHOLDER: Purple
                _ => WpfBrushes.White,                                                                                            // PLACEHOLDER: White
            };
        }

        public static WpfBrush GetIconBrushFromColorString(string color)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(color))
                {
                    return WpfBrushes.White;
                }
                return new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(color));
            }
            catch (FormatException)
            {
                // Return white brush if color string is invalid
                return WpfBrushes.White;
            }
        }
    }
}
