namespace Tailgrab.LineHandler;

using System.Text.RegularExpressions;
using Tailgrab.PlayerManagement;

public class AvatarChangeHandler : AbstractLineHandler
{

    public static readonly string LOG_PATTERN = @"([\d]{4}.[\d]{2}.[\d]{2}\W[\d]{2}:[\d]{2}:[\d]{2})\W(Log[\W]{8}|Debug[\W]{6})-\W\W\[Behaviour\]\WSwitching\W([\S\W]+)\Wto\Wavatar\W([\S\W]+)";
    public static readonly int VRC_DATETIME = 1;
    public static readonly int VRC_LOGTYPE = 2;
    public static readonly int VRC_DISPLAYNAME = 3;
    public static readonly int VRC_AVATARNAME = 4;


    public AvatarChangeHandler(string matchPattern) : base(matchPattern)
    {
    }

    public override bool HandleLine(string line)
    {
        Match m = regex.Match(line);
        if( m.Success )
        {
            string timestamp = m.Groups[VRC_DATETIME].Value;
            string userName = m.Groups[VRC_DISPLAYNAME].Value;
            string avatarName = m.Groups[VRC_AVATARNAME].Value;
            if( LogOutput )
            {
                logger.Info($"{COLOR_PREFIX}Avatar Change : {userName} to {avatarName}{COLOR_RESET}");
            }
            PlayerManager.SetAvatarForPlayer(userName, avatarName);
            PlayerManager.AddPlayerEventByDisplayName(userName, PlayerEvent.EventType.AvatarChange, $"Changed avatar to: {avatarName}");

            ExecuteActions();
            return true;
        }
        return false;
    }
}