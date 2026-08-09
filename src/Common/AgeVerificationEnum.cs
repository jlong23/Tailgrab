using System.Windows.Media;
using Tailgrab.PlayerManagement;
using VRChat.API.Model;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;

namespace Tailgrab.Common
{
    public enum AgeVerificationEnum
    {
        UNVERIFIED = 0,
        VERIFIED = 1,
        HIDDEN = 2,
        PLUS18 = 3,
    }

    public class AgeVerificationEnumMapper
    {
        public static string MapEnumToString(AgeVerificationEnum ageVerificationStatus)
        {
            return ageVerificationStatus switch
            {
                AgeVerificationEnum.UNVERIFIED => "Unverified",
                AgeVerificationEnum.VERIFIED => "Verified",
                AgeVerificationEnum.HIDDEN => "Hidden",
                AgeVerificationEnum.PLUS18 => "18+",
                _ => "Unknown",
            };
        }

        public static AgeVerificationEnum MapStringToEnum(string ageVerificationStatus)
        {
            return ageVerificationStatus switch
            {
                "Unverified" => AgeVerificationEnum.UNVERIFIED,
                "Verified" => AgeVerificationEnum.VERIFIED,
                "Hidden" => AgeVerificationEnum.HIDDEN,
                "18+" => AgeVerificationEnum.PLUS18,
                _ => AgeVerificationEnum.UNVERIFIED,
            };
        }

        public static AgeVerificationEnum MapAgeVerificationStatusToEnum( AgeVerificationStatus? ageVerificationStatus)
        {
            return ageVerificationStatus switch
            {
                AgeVerificationStatus.verified => AgeVerificationEnum.VERIFIED,
                AgeVerificationStatus.hidden => AgeVerificationEnum.HIDDEN,
                AgeVerificationStatus.plus18 => AgeVerificationEnum.PLUS18,
                _ => AgeVerificationEnum.UNVERIFIED,
            };
        }

        public static AlertDisplayItem MapEnumToAlertDisplayItem(AgeVerificationEnum status)
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


        public static Geometry MapEnumToIcon(AgeVerificationEnum status)
        {
            return status switch
            {
                AgeVerificationEnum.VERIFIED => Geometry.Parse("M115.62 12.38H12.38A9.4 9.4 0 0 0 3 21.77v84.46a9.4 9.4 0 0 0 9.38 9.39h103.24a9.4 9.4 0 0 0 9.38-9.39V21.77a9.4 9.4 0 0 0-9.38-9.39zM62.29 92a4.68 4.68 0 0 1-5.71-3.37 14.09 14.09 0 0 0-27.27 0 4.69 4.69 0 1 1-9.09-2.34 23.38 23.38 0 0 1 9.6-13.58 18.77 18.77 0 1 1 26.54-0.29l-0.29 0.29a23.43 23.43 0 0 1 9.6 13.58 4.69 4.69 0 0 1-3.38 5.71zM101.54 78.08H78.08a4.7 4.7 0 1 1 0-9.39h23.46a4.7 4.7 0 0 1 0 9.39zM101.54 59.31H78.08a4.7 4.7 0 1 1 0-9.39h23.46a4.7 4.7 0 0 1 0 9.39zM52.33 59.31a9.39 9.39 0 1 1-9.39-9.39 9.38 9.38 0 0 1 9.39 9.39z"), // PLACEHOLDER: None/Circle icon,
                AgeVerificationEnum.HIDDEN => Geometry.Parse("M44.55 50a8 8 0 0 0-3.69 0.9l11.29 9.59a7.87 7.87 0 0 0 0.4-2.49 8 8 0 0 0-8-8z M43 65.85l-5.95-5a8 8 0 0 0 5.95 5z M45.88 42 17.64 18a8 8 0 0 0-7.09 7.1l24 20.37A16 16 0 0 1 45.88 42z M71.87 51a4 4 0 0 1 2.63-1h20a4 4 0 0 1 0 8H80.11l9.41 8h5a4 4 0 0 1 4 4 3.93 3.93 0 0 1-1.08 2.71l17.06 14.51V26a8 8 0 0 0-8-8H33z M106.5 106a8 8 0 0 0 7.71-5.9L83.49 74h-9a4 4 0 0 1-0.38-8L60.04 54a16 16 0 0 1-1.59 11.78L105.71 106z M63.75 83.47a4 4 0 0 1-2.75 2.4 4 4 0 0 1-4.83-2.87 12 12 0 0 0-23.24 0 4 4 0 0 1-7.75-2 20 20 0 0 1 8.18-11.58A16 16 0 0 1 29.07 54L10.5 38.23V98a8 8 0 0 0 8 8h71.77z M120.5 117a5 5 0 0 1-3.24-1.19l-113-96a5 5 0 0 1 6.48-7.62l113 96a5 5 0 0 1-3.24 8.81z"), // PLACEHOLDER: None/Circle icon,
                AgeVerificationEnum.PLUS18 => Geometry.Parse("M115.62 12.38H12.38A9.4 9.4 0 0 0 3 21.77v84.46a9.4 9.4 0 0 0 9.38 9.39h103.24a9.4 9.4 0 0 0 9.38-9.39V21.77a9.4 9.4 0 0 0-9.38-9.39zM62.29 92a4.68 4.68 0 0 1-5.71-3.37 14.09 14.09 0 0 0-27.27 0 4.69 4.69 0 1 1-9.09-2.34 23.38 23.38 0 0 1 9.6-13.58 18.77 18.77 0 1 1 26.54-0.29l-0.29 0.29a23.43 23.43 0 0 1 9.6 13.58 4.69 4.69 0 0 1-3.38 5.71zM101.54 78.08H78.08a4.7 4.7 0 1 1 0-9.39h23.46a4.7 4.7 0 0 1 0 9.39zM101.54 59.31H78.08a4.7 4.7 0 1 1 0-9.39h23.46a4.7 4.7 0 0 1 0 9.39zM52.33 59.31a9.39 9.39 0 1 1-9.39-9.39 9.38 9.38 0 0 1 9.39 9.39z"), // PLACEHOLDER: None/Circle icon,
                _ => Geometry.Empty,
            };
        }

        public static WpfBrush MapEnumToBrush(AgeVerificationEnum status)
        {
            return status switch
            {
                AgeVerificationEnum.VERIFIED => WpfBrushes.Green,  // PLACEHOLDER: Green
                AgeVerificationEnum.HIDDEN => WpfBrushes.Gray,     // PLACEHOLDER: Gray
                AgeVerificationEnum.PLUS18 => WpfBrushes.Purple,   // PLACEHOLDER: Purple
                _ => WpfBrushes.White,                             // PLACEHOLDER: White
            };
        }
    }
}
