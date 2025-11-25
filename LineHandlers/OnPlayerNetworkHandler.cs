namespace Tailgrab.LineHandler;

using System.Text.RegularExpressions;
using Tailgrab.PlayerManagement;

public class OnPlayerNetworkHandler : AbstractLineHandler
{

    public static readonly string LOG_PATTERN = @"([\d]{4}.[\d]{2}.[\d]{2}\W[\d]{2}:[\d]{2}:[\d]{2})\W(Log[\W]{8}|Debug[\W]{6})-\W\W\[AP\]\WPlayer\W\W([\d\w\W]+)\W joined with ID ([\d]+)";
    public static readonly int VRC_DATETIME = 1;
    public static readonly int VRC_LOGTYPE = 2;
    public static readonly int VRC_DISPLAYNAME = 3;
    public static readonly int VRC_NETWORKID = 4;


    public OnPlayerNetworkHandler(string matchPattern) : base(matchPattern)
    {
    }

    public override bool HandleLine(string line)
    {
        Match m = regex.Match(line);
        if( m.Success )
        {
            string timestamp = m.Groups[VRC_DATETIME].Value;
            string userName = m.Groups[VRC_DISPLAYNAME].Value;
            int networkId = int.Parse( m.Groups[VRC_NETWORKID].Value);
            if( LogOutput )
            {
                logger.Info($"{COLOR_PREFIX}Network_ID : {userName} ({networkId}){COLOR_RESET}");
                //Console.WriteLine($"{timestamp} - Network_ID : {userName} ({networkId})");
            }
            ExecuteActions();

            PlayerManager.AssignPlayerNetworkId(userName, networkId );

            return true;
        }
        
        return false;
    }
}