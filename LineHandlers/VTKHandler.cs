namespace Tailgrab.LineHandler;

using System.Text.RegularExpressions;
using Tailgrab.PlayerManagement;

public class VTKHandler : AbstractLineHandler
{

    public static readonly string LOG_PATTERN = @"([\d]{4}.[\d]{2}.[\d]{2}\W[\d]{2}:[\d]{2}:[\d]{2})\W(Log[\W]{8}|Debug[\W]{6})-\W\W\[ModerationManager\] A vote kick has been initiated against ([\S\W]+), do you agree";
    public static readonly int VRC_DATETIME = 1;
    public static readonly int VRC_LOGTYPE = 2;
    public static readonly int VRC_DISPLAYNAME = 3;


    public VTKHandler(string matchPattern) : base(matchPattern)
    {
    }

    public override bool HandleLine(string line)
    {
        Match m = regex.Match(line);
        if( m.Success )
        {
            string timestamp = m.Groups[VRC_DATETIME].Value;
            string userName = m.Groups[VRC_DISPLAYNAME].Value;
            if( LogOutput )
            {
                logger.Info($"{COLOR_PREFIX}VTK : {userName}{COLOR_RESET}");
            }

            PlayerManager.AddPlayerEventByDisplayName(userName, PlayerEvent.EventType.Moderation, "Vote kick initiated against player.");   

            ExecuteActions();
            return true;
        }
        return false;
    }
}