namespace Tailgrab.LineHandler;

using System.Text.RegularExpressions;
using Tailgrab.Common;

public class WorldChangeHandler : AbstractLineHandler
{

    public static readonly string LOG_PATTERN = @"([\d]{4}.[\d]{2}.[\d]{2}\W[\d]{2}:[\d]{2}:[\d]{2})\W(Log[\W]{8}|Debug[\W]{6})-\W\W\[Behaviour\]\WJoining\W(wrld_[0-9a-f\-]+)\:([\S\W]+)";
    public static readonly int VRC_DATETIME = 1;
    public static readonly int VRC_LOGTYPE = 2;
    public static readonly int VRC_WORLDID = 3;
    public static readonly int VRC_INSTANCEID = 4;


    public WorldChangeHandler(string matchPattern, ServiceRegistry serviceRegistry) : base(matchPattern, serviceRegistry)
    {
        logger.Info($"** World Join Handler: Regular Expression: {Pattern}");        
    }

    public override bool HandleLine(string line)
    {
        Match m = regex.Match(line);
        if( m.Success )
        {
            string timestamp = m.Groups[VRC_DATETIME].Value;
            string worldId = m.Groups[VRC_WORLDID].Value;
            string instanceId = m.Groups[VRC_INSTANCEID].Value;
            if( LogOutput )
            {
                logger.Info($"{COLOR_PREFIX}World Join : {worldId} as instance {instanceId}{COLOR_RESET.GetAnsiEscape()}");
            }

            _serviceRegistry.GetPlayerManager().UpdateCurrentSession(worldId, instanceId);
            _serviceRegistry.GetPlayerManager().ClearAllPlayers(this);

            ExecuteActions();
            return true;
        }
        return false;
    }
}