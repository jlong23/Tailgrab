namespace Tailgrab.LineHandler;

using System.Text.RegularExpressions;
using Tailgrab.PlayerManagement;

public class QuitHandler : AbstractLineHandler
{

    public static readonly string LOG_PATTERN = @"([\d]{4}.[\d]{2}.[\d]{2}\W[\d]{2}:[\d]{2}:[\d]{2})\W(Log[\W]{8}|Debug[\W]{6})-\W\WVRCApplication: HandleApplicationQuit at ([\d\W]+)";
    public static readonly int VRC_DATETIME = 1;
    public static readonly int VRC_LOGTYPE = 2;
    public static readonly int VRC_TOTALSEC = 3;


    public QuitHandler(string matchPattern) : base(matchPattern)
    {
    }

    public override bool HandleLine(string line)
    {
        Match m = regex.Match(line);
        if( m.Success )
        {
            string timestamp = m.Groups[VRC_DATETIME].Value;
            string totalTime = m.Groups[VRC_TOTALSEC].Value;
            if( LogOutput )
            {
                logger.Info($"{COLOR_PREFIX}Application Stop : {totalTime} seconds{COLOR_RESET}");
            }

            PlayerManager.ClearAllPlayers();

            ExecuteActions();
            return true;
        }
        return false;
    }
}