using VRChat.API.Model;

namespace Tailgrab.Common
{
    internal class PlayerModerationTypeMapper
    {
        public static string MapEnumToDescription(PlayerModerationType moderationType)
        {
            string key = moderationType switch
            {
                PlayerModerationType.Block => "block",
                PlayerModerationType.HideAvatar => "hideavatar",
                PlayerModerationType.InteractOff => "interactoff",
                PlayerModerationType.InteractOn => "interacton",
                PlayerModerationType.Mute => "mute",
                PlayerModerationType.MuteChat => "mutechat",
                PlayerModerationType.ShowAvatar => "showavatar",
                PlayerModerationType.Unmute => "unmute",
                PlayerModerationType.UnmuteChat => "unmutechat",
                _ => "mute",
            };
            return key;
        }

        public static PlayerModerationType MapStringToEnum(string moderationType)
        {
            return moderationType.ToLower() switch
            {
                "block" => PlayerModerationType.Block,
                "hideavatar" => PlayerModerationType.HideAvatar,
                "interactoff" => PlayerModerationType.InteractOff,
                "interacton" => PlayerModerationType.InteractOn,
                "mute" => PlayerModerationType.Mute,
                "mutechat" => PlayerModerationType.MuteChat,
                "showavatar" => PlayerModerationType.ShowAvatar,
                "unmute" => PlayerModerationType.Unmute,
                "unmutechat" => PlayerModerationType.UnmuteChat,
                _ => PlayerModerationType.Mute
            };
        }
    }
}
