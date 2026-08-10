using System.Windows.Media;
using Tailgrab.PlayerManagement;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;

namespace Tailgrab.Common
{
    public enum TrustClassEnum { 
        VISITOR = 0,
        NEW_USER = 1,
        USER = 2,
        KNOWN_USER = 3,
        TRUSTED_USER = 4,
        PROBABLE_TROLL = 5,
        NUISANCE = 6,
    }

    public class TrustClassEnumMapper
    {
        public static string MapTagsToString(List<string> tags, bool ageVerified, string ageVerificationStatus)
        {
            string verifiedStatus = String.Empty;
            if (ageVerified)
                verifiedStatus = $" / {ageVerificationStatus}";

            string trustLevel = "Visitor" + verifiedStatus;
            foreach (string tag in tags.ToArray().Reverse())
            {
                switch (tag)
                {
                    case CommonConst.SYSTEM_USER_TRUST_PROBABLE_TROLL:
                        trustLevel = "Probable Troll" + verifiedStatus;
                        return trustLevel;
                    case CommonConst.SYSTEM_USER_TRUST_TROLL:
                        trustLevel = "Nuisance" + verifiedStatus;
                        return trustLevel;
                    case CommonConst.SYSTEM_USER_TRUST_BASIC:
                        trustLevel = "New User" + verifiedStatus;
                        return trustLevel;
                    case CommonConst.SYSTEM_USER_TRUST_KNOWN:
                        trustLevel = "User" + verifiedStatus;
                        return trustLevel;
                    case CommonConst.SYSTEM_USER_TRUST_TRUSTED:
                        trustLevel = "Known User" + verifiedStatus;
                        return trustLevel;
                    case CommonConst.SYSTEM_USER_TRUST_VETERAN:
                        trustLevel = "Trusted User" + verifiedStatus;
                        return trustLevel;
                }
            }

            return trustLevel;
        }

        public static TrustClassEnum MapTagsToEnum(List<string> tags)
        {
            TrustClassEnum trustLevel = TrustClassEnum.VISITOR;
            foreach (string tag in tags.ToArray().Reverse())
            {
                switch (tag)
                {
                    case CommonConst.SYSTEM_USER_TRUST_PROBABLE_TROLL:
                        trustLevel = TrustClassEnum.PROBABLE_TROLL;
                        return trustLevel;
                    case CommonConst.SYSTEM_USER_TRUST_TROLL:
                        trustLevel = TrustClassEnum.NUISANCE;
                        return trustLevel;
                    case CommonConst.SYSTEM_USER_TRUST_BASIC:
                        trustLevel = TrustClassEnum.NEW_USER;
                        return trustLevel;
                    case CommonConst.SYSTEM_USER_TRUST_KNOWN:
                        trustLevel = TrustClassEnum.USER;
                        return trustLevel;
                    case CommonConst.SYSTEM_USER_TRUST_TRUSTED:
                        trustLevel = TrustClassEnum.KNOWN_USER;
                        return trustLevel;
                    case CommonConst.SYSTEM_USER_TRUST_VETERAN:
                        trustLevel = TrustClassEnum.TRUSTED_USER;
                        return trustLevel;
                }
            }

            return trustLevel;
        }

        public static string MapEnumToDescription(TrustClassEnum userTrust)
        {
            return userTrust switch
            {
                TrustClassEnum.VISITOR => "Visitor",
                TrustClassEnum.NEW_USER => "New User",
                TrustClassEnum.USER => "User",
                TrustClassEnum.KNOWN_USER => "Known User",
                TrustClassEnum.TRUSTED_USER => "Trusted User",
                TrustClassEnum.PROBABLE_TROLL => "Probable Troll",
                TrustClassEnum.NUISANCE => "Nuisance",
                _ => "Unknown"
            };
        }

        public static AlertDisplayItem MapEnumToAlertDisplayItem(TrustClassEnum userTrust)
        {
            return new AlertDisplayItem {
                IconGeometry = MapEnumToIcon(userTrust),
                IconBrush = MapEnumToBrush(userTrust),
                IconClass = "User Trust",
                Description = MapEnumToDescription(userTrust),
                AlertColor = MapEnumToBrush(userTrust).ToString()
            };
        }

        public static Geometry MapEnumToIcon(TrustClassEnum userTrust)
        {
            var resources = System.Windows.Application.Current.Resources;
            string key = userTrust switch
            {
                TrustClassEnum.VISITOR => "Icon.TrustClassEnum.VISITOR",
                TrustClassEnum.NEW_USER => "Icon.TrustClassEnum.NEW_USER",
                TrustClassEnum.USER => "Icon.TrustClassEnum.USER",
                TrustClassEnum.KNOWN_USER => "Icon.TrustClassEnum.KNOWN_USER",
                TrustClassEnum.TRUSTED_USER => "Icon.TrustClassEnum.TRUSTED_USER",
                TrustClassEnum.PROBABLE_TROLL => "Icon.TrustClassEnum.PROBABLE_TROLL",
                TrustClassEnum.NUISANCE => "Icon.TrustClassEnum.NUISANCE",
                _ => "Icon.TrustClassEnum.Default",
            };
            return (Geometry)resources[key];
        }

        public static WpfBrush MapEnumToBrush(TrustClassEnum userTrust)
        {
            var resources = System.Windows.Application.Current.Resources;
            string key = userTrust switch
            {
                TrustClassEnum.VISITOR => "Brush.TrustClassEnum.VISITOR",
                TrustClassEnum.NEW_USER => "Brush.TrustClassEnum.NEW_USER",
                TrustClassEnum.USER => "Brush.TrustClassEnum.USER",
                TrustClassEnum.KNOWN_USER => "Brush.TrustClassEnum.KNOWN_USER",
                TrustClassEnum.TRUSTED_USER => "Brush.TrustClassEnum.TRUSTED_USER",
                TrustClassEnum.PROBABLE_TROLL => "Brush.TrustClassEnum.PROBABLE_TROLL",
                TrustClassEnum.NUISANCE => "Brush.TrustClassEnum.NUISANCE",
                _ => "Brush.TrustClassEnum.Default",
            };
            return (WpfBrush)resources[key];
        }
    }
}
