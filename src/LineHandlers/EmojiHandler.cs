namespace Tailgrab.LineHandler;

using System.Text.RegularExpressions;
using Tailgrab.Common;

public class EmojiHandler : AbstractLineHandler
{

    public static readonly string LOG_PATTERN = @"([\d]{4}.[\d]{2}.[\d]{2}\W[\d]{2}:[\d]{2}:[\d]{2})\W(Log[\W]{8}|Debug[\W]{6})-\W\W\[API\]\W\[\d+\]\WSending\WGet\Wrequest\Wto\Whttps://api.vrchat.cloud/api/1/user/(usr_[\d\w\W]+)/inventory/(inv_[\d\w\W]+)";
    public static readonly int VRC_DATETIME = 1;
    public static readonly int VRC_LOGTYPE = 2;
    public static readonly int VRC_USERID = 3;
    public static readonly int VRC_INVENTORYID = 4;


    public EmojiHandler(string matchPattern, ServiceRegistry serviceRegistry) : base(matchPattern, serviceRegistry)
    {
        logger.Info($"** Emoji/Inventory Handler:  Regular Expression: {Pattern}");
    }

    public override bool HandleLine(string line)
    {
        Match m = regex.Match(line);
        if (m.Success)
        {
            string timestamp = m.Groups[VRC_DATETIME].Value;
            string userId = m.Groups[VRC_USERID].Value;
            string inventoryId = m.Groups[VRC_INVENTORYID].Value;
            _serviceRegistry.GetPlayerManager().AddInventorySpawn(userId, inventoryId);
            if (LogOutput)
            {
                logger.Info($"{COLOR_PREFIX}Emoji/Inventory : {userId} / {inventoryId}{COLOR_RESET.GetAnsiEscape()}");
            }
            ExecuteActions();
            return true;
        }
        return false;
    }
}