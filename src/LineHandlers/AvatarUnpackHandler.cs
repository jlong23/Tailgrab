namespace Tailgrab.LineHandler;

using System.Text.RegularExpressions;
using Tailgrab.Common;

public class AvatarUnpackHandler : AbstractLineHandler
{

    public static readonly string LOG_PATTERN = @"([\d]{4}.[\d]{2}.[\d]{2}\W[\d]{2}:[\d]{2}:[\d]{2})\W(Log[\W]{8}|Debug[\W]{6})-\W\W\[AssetBundleDownloadManager\]\W\[\d+\] Unpacking Avatar \(([\S\W]+) by ([\S\W]+)\)";
    public static readonly int VRC_DATETIME = 1;
    public static readonly int VRC_LOGTYPE = 2;
    public static readonly int VRC_DISPLAYNAME = 4;
    public static readonly int VRC_AVATARNAME = 3;


    public AvatarUnpackHandler(string matchPattern, ServiceRegistry serviceRegistry) : base(matchPattern, serviceRegistry)
    {
        logger.Info($"** AvatarUnpack Handler:  Regular Expression: {Pattern}");
    }

    public override bool HandleLine(string line)
    {
        Match m = regex.Match(line);
        if (m.Success)
        {
            string timestamp = m.Groups[VRC_DATETIME].Value;
            string userName = m.Groups[VRC_DISPLAYNAME].Value;
            string avatarName = m.Groups[VRC_AVATARNAME].Value;
            if (LogOutput)
            {
                logger.Info($"{COLOR_PREFIX}Avatar Unpack : {avatarName} by {userName}{COLOR_RESET.GetAnsiEscape()}");
            }
            ExecuteActions();
            return true;
        }
        return false;
    }
}