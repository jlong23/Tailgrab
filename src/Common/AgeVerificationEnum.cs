using System.Windows.Media;
using Tailgrab.PlayerManagement;
using VRChat.API.Model;
using WpfBrush = System.Windows.Media.Brush;

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
            var resources = System.Windows.Application.Current.Resources;
            string key = ageVerificationStatus switch
            {
                AgeVerificationEnum.UNVERIFIED => "Text.AgeVerificationEnum.UNVERIFIED",
                AgeVerificationEnum.VERIFIED => "Text.AgeVerificationEnum.VERIFIED",
                AgeVerificationEnum.HIDDEN => "Text.AgeVerificationEnum.HIDDEN",
                AgeVerificationEnum.PLUS18 => "Text.AgeVerificationEnum.PLUS18",
                _ => "Text.AgeVerificationEnum.Default",
            };
            return (string)resources[key];
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
            var resources = System.Windows.Application.Current.Resources;
            string key = status switch
            {
                AgeVerificationEnum.VERIFIED => "Icon.AgeVerificationEnum.VERIFIED",
                AgeVerificationEnum.HIDDEN => "Icon.AgeVerificationEnum.HIDDEN",
                AgeVerificationEnum.PLUS18 => "Icon.AgeVerificationEnum.PLUS18",
                _ => "Icon.AgeVerificationEnum.Default",
            };
            return (Geometry)resources[key];
        }

        public static WpfBrush MapEnumToBrush(AgeVerificationEnum status)
        {
            var resources = System.Windows.Application.Current.Resources;
            string key = status switch
            {
                AgeVerificationEnum.VERIFIED => "Brush.AgeVerificationEnum.VERIFIED",
                AgeVerificationEnum.HIDDEN => "Brush.AgeVerificationEnum.HIDDEN",
                AgeVerificationEnum.PLUS18 => "Brush.AgeVerificationEnum.PLUS18",
                _ => "Brush.AgeVerificationEnum.Default",
            };
            return (WpfBrush)resources[key];
        }
    }
}
