namespace Tailgrab.LineHandler;

using System.Text.RegularExpressions;
using Tailgrab.Common;
using Tailgrab.PlayerManagement;

public class WarnKickHandler : AbstractLineHandler
{

    public static readonly string LOG_PATTERN = @"([\d]{4}.[\d]{2}.[\d]{2}\W[\d]{2}:[\d]{2}:[\d]{2})\W(Log[\W]{8}|Debug[\W]{6})-\W\W\[ModerationManager\]\W([\S\W]+)\Whas\Wbeen\W(warned|kicked)";
    public static readonly int VRC_DATETIME = 1;
    public static readonly int VRC_LOGTYPE = 2;
    public static readonly int VRC_DISPLAYNAME = 3;
    public static readonly int VRC_ACTION = 4;


    public WarnKickHandler(string matchPattern, ServiceRegistry serviceRegistry) : base(matchPattern, serviceRegistry)
    {
        logger.Info($"** Moderation Warn/Kick Handler:  Regular Expression: {Pattern}");        
    }

    public override bool HandleLine(string line)
    {
        Match m = regex.Match(line);
        if( m.Success )
        {
            string timestamp = m.Groups[VRC_DATETIME].Value;
            string userName = m.Groups[VRC_DISPLAYNAME].Value;
            string action = m.Groups[VRC_ACTION].Value;
            if( LogOutput )
            {
                logger.Info($"{COLOR_PREFIX}User Moderation : {userName} to {action}{COLOR_RESET.GetAnsiEscape()}");
            }

            _serviceRegistry.GetPlayerManager().AddPlayerEventByDisplayName(userName, PlayerEvent.EventType.Moderation, $"User has been {action}.");

            ExecuteActions();
            return true;
        }
        return false;
    }
}