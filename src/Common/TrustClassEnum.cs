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

        public static WpfBrush MapEnumToBrush(TrustClassEnum userTrust)
        {
            return userTrust switch
            {
                TrustClassEnum.VISITOR => WpfBrushes.Gray,
                TrustClassEnum.NEW_USER => WpfBrushes.Blue,
                TrustClassEnum.USER => WpfBrushes.Green,
                TrustClassEnum.KNOWN_USER => WpfBrushes.Orange,
                TrustClassEnum.TRUSTED_USER => WpfBrushes.Purple,
                TrustClassEnum.PROBABLE_TROLL => WpfBrushes.Gray,
                TrustClassEnum.NUISANCE => WpfBrushes.Yellow,
                _ => WpfBrushes.White,
            };
        }
    }
}
