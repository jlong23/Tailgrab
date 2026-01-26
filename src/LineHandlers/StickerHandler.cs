namespace Tailgrab.LineHandler;

using System.Text.RegularExpressions;
using Tailgrab.Common;
using Tailgrab.PlayerManagement;

public class StickerHandler : AbstractLineHandler
{

    public static readonly string LOG_PATTERN = @"([\d]{4}.[\d]{2}.[\d]{2}\W[\d]{2}:[\d]{2}:[\d]{2})\W(Log[\W]{8}|Debug[\W]{6})-\W\W\[StickersManager\]\WUser\W(usr_[\d\w\W]+)\W\(([\d\w\W]+)\)\Wspawned\Wsticker\W([\d\w\W]+)";
    public static readonly int VRC_DATETIME = 1;
    public static readonly int VRC_LOGTYPE = 2;
    public static readonly int VRC_USERID = 3;
    public static readonly int VRC_DISPLAYNAME = 4;
    public static readonly int VRC_FILEURL = 5;


    public StickerHandler(string matchPattern, ServiceRegistry serviceRegistry) : base(matchPattern, serviceRegistry)
    {
        logger.Info($"** Sticker Handler:  Regular Expression: {Pattern}");        
    }

    public override bool HandleLine(string line)
    {
        Match m = regex.Match(line);
        if( m.Success )
        {
            string timestamp = m.Groups[VRC_DATETIME].Value;
            string fileURL = m.Groups[VRC_FILEURL].Value;
            string userName = m.Groups[VRC_DISPLAYNAME].Value;
            string userId = m.Groups[VRC_USERID].Value;
            if( LogOutput )
            {
                logger.Info($"{COLOR_PREFIX}{userName} ({userId}) - {fileURL}{COLOR_RESET.GetAnsiEscape()}");
            }
            _serviceRegistry.GetPlayerManager().AddStickerEvent( userName, userId, fileURL );

            ExecuteActions();
            return true;
        }
        return false;
    }
}