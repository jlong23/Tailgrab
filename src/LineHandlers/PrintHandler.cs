namespace Tailgrab.LineHandler;

using System.Text.RegularExpressions;
using Tailgrab.Common;
using VRChat.API.Model;

public class PrintHandler : AbstractLineHandler
{

    public static readonly string LOG_PATTERN = @"([\d]{4}.[\d]{2}.[\d]{2}\W[\d]{2}:[\d]{2}:[\d]{2})\W(Log[\W]{8}|Debug[\W]{6})-\W\W\[API\]\WRequesting\WGet\Wprints/(prnt_[\d\w\W]+)\W\{\{\}\}";
    public static readonly int VRC_DATETIME = 1;
    public static readonly int VRC_LOGTYPE = 2;
    public static readonly int VRC_FILEURL = 3;


    public PrintHandler(string matchPattern, ServiceRegistry serviceRegistry) : base(matchPattern, serviceRegistry)
    {
        logger.Info($"** Print Handler:  Regular Expression: {Pattern}");
    }

    public override bool HandleLine(string line)
    {
        Match m = regex.Match(line);
        if (m.Success)
        {
            string timestamp = m.Groups[VRC_DATETIME].Value;
            string fileUrl = m.Groups[VRC_FILEURL].Value;
            Print? printInfo = UserPrintSpawn(fileUrl);
            if (LogOutput)
            {
                logger.Info($"{COLOR_PREFIX}Print : {fileUrl}{COLOR_RESET.GetAnsiEscape()}");
            }

            Dictionary<string, string> actionData = new Dictionary<string, string>
            {
                { "timestamp", timestamp },
                { "fileUrl", fileUrl },
                { "ownerId", printInfo?.OwnerId ?? string.Empty },
                { "authorName", printInfo?.AuthorName ?? string.Empty },
                { "worldId", printInfo?.WorldId ?? string.Empty },
                { "worldName", printInfo?.WorldName ?? string.Empty },
                { "createdAt", printInfo?.CreatedAt.ToString() ?? string.Empty },
                { "files", printInfo != null ? string.Join(", ", printInfo.Files) : string.Empty  }
            };

            ExecuteActions(actionData);
            return true;
        }
        return false;
    }

    private Print? UserPrintSpawn(string fileUrl)
    {
        return Task.Run(() => _serviceRegistry.GetPrintManager().AddPrintSpawn(fileUrl)).Result;
    }

}