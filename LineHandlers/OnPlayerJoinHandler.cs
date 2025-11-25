namespace Tailgrab.LineHandler;

using System.Text.RegularExpressions;
using Tailgrab.PlayerManagement;

public class OnPlayerJoinHandler : AbstractLineHandler
{

    public static readonly string LOG_PATTERN = @"([\d]{4}.[\d]{2}.[\d]{2}\W[\d]{2}:[\d]{2}:[\d]{2})\W(Log[\W]{8}|Debug[\W]{6})-\W\W\[Behaviour\]\WOnPlayer([\d\w]+)\W([\d\w\W]+)\W\((usr_[\d\w\W]+)\)";
    public static readonly int VRC_DATETIME = 1;
    public static readonly int VRC_LOGTYPE = 2;
    public static readonly int VRC_ACTION = 3;
    public static readonly int VRC_DISPLAYNAME = 4;
    public static readonly int VRC_USERID = 5;


    public OnPlayerJoinHandler(string matchPattern) : base(matchPattern)
    {
    }

    public override bool HandleLine(string line)
    {
        Match m = regex.Match(line);
        if( m.Success )
        {
            string timestamp = m.Groups[VRC_DATETIME].Value;
            string action = m.Groups[VRC_ACTION].Value;
            string userName = m.Groups[VRC_DISPLAYNAME].Value;
            string userId = m.Groups[VRC_USERID].Value;
            ExecuteActions();

            if( action.Equals("Joined") )
            {
                PlayerManager.PlayerJoined(userId, userName);
            }
            else if( action.Equals("Left") )
            {
                PlayerManager.PlayerLeft(userName);
            }

            return true;
        }
        return false;
    }
}