namespace Tailgrab.LineHandler;

using System.Text.RegularExpressions;
using Tailgrab.Common;
using Tailgrab.PlayerManagement;

public class QuitHandler : AbstractLineHandler
{

    public static readonly string LOG_PATTERN = @"([\d]{4}.[\d]{2}.[\d]{2}\W[\d]{2}:[\d]{2}:[\d]{2})\W(Log[\W]{8}|Debug[\W]{6})-\W\WVRCApplication: HandleApplicationQuit at ([\d\W]+)";
    public static readonly int VRC_DATETIME = 1;
    public static readonly int VRC_LOGTYPE = 2;
    public static readonly int VRC_TOTALSEC = 3;


    public QuitHandler(string matchPattern, ServiceRegistry serviceRegistry) : base(matchPattern, serviceRegistry)
    {
        logger.Info($"** VRC Quit Handler:  Regular Expression: {Pattern}");
    }

    public override bool HandleLine(string line)
    {
        Match m = regex.Match(line);
        if (m.Success)
        {
            string timestamp = m.Groups[VRC_DATETIME].Value;
            string totalTime = m.Groups[VRC_TOTALSEC].Value;

            // Create a TimeSpan object from the total number of seconds
            TimeSpan time = TimeSpan.FromSeconds(Double.Parse(totalTime));

            if (LogOutput)
            {
                //string formattedTime = string.Format("{0:D2}:{1:D2}:{2:D2}", time.Hours, time.Minutes, time.Seconds);
                //logger.Info($"{COLOR_PREFIX}Application Stop :  {totalTime} seconds{COLOR_RESET.GetAnsiEscape()}");
                logger.Info($"{COLOR_PREFIX}Application Stop :  {time} {COLOR_RESET.GetAnsiEscape()}");
            }

            PlayerManager.ClearAllPlayers(this);

            ExecuteActions();
            return true;
        }
        return false;
    }
}