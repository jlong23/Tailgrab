namespace Tailgrab.LineHandler;

using System.Text.RegularExpressions;

public class OnPlayerJoinHandler : AbstractLineHandler
{

    public static readonly string LOG_PATTERN = @"([\d]{4}.[\d]{2}.[\d]{2}\W[\d]{2}:[\d]{2}:[\d]{2})\W(Log[\W]{8}|Debug[\W]{6})-\W\W\[Behaviour\]\WOnPlayer([\d\w]+)\W([\d\w\W]+)\W\((usr_[\d\w\W]+)\)";
    public static readonly int VRC_DATETIME = 1;
    public static readonly int VRC_LOGTYPE = 2;
    public static readonly int VRC_ACTION = 3;
    public static readonly int VRC_DISPLAYNAME = 4;
    public static readonly int VRC_USERID = 5;


    public OnPlayerJoinHandler(string matchPattern, ServiceRegistry serviceRegistry) : base(matchPattern, serviceRegistry)
    {
        logger.Info($"** OnPlayer Join/Leave Handler:  Regular Expression: {Pattern}");
    }

    public override bool HandleLine(string line)
    {
        Match m = regex.Match(line);
        if (m.Success)
        {
            string timestamp = m.Groups[VRC_DATETIME].Value;
            string action = m.Groups[VRC_ACTION].Value;
            string userName = m.Groups[VRC_DISPLAYNAME].Value;
            string userId = m.Groups[VRC_USERID].Value;
            ExecuteActions();

            if (action.Equals("Joined"))
            {
                _serviceRegistry.GetPlayerManager().PlayerJoined(userId, userName, this);
            }
            else if (action.Equals("Left"))
            {
                _serviceRegistry.GetPlayerManager().PlayerLeft(userName, this);
            }

            return true;
        }
        return false;
    }
}