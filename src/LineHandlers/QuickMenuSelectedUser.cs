namespace Tailgrab.LineHandler;

using System.Text.RegularExpressions;
using Tailgrab.Common;
using Tailgrab.PlayerManagement;

public class QuickMenuSelectedUser : AbstractLineHandler
{

    public static readonly string LOG_PATTERN = @"([\d]{4}.[\d]{2}.[\d]{2}\W[\d]{2}:[\d]{2}:[\d]{2})\W(Log[\W]{8}|Warning[\W]{4})-\W\WVP QuickMenuSelectedUser AboutToNavigate\(\) QuickMenuSelectedUser\?id=([\S\W]+)";
    public static readonly int VRC_DATETIME = 1;
    public static readonly int VRC_LOGTYPE = 2;
    public static readonly int VRC_USERID = 3;


    public QuickMenuSelectedUser(string matchPattern, ServiceRegistry serviceRegistry) : base(matchPattern, serviceRegistry)
    {
        logger.Info($"** QuickMenuSelectedUser Handler:  Regular Expression: {Pattern}");
    }

    public override bool HandleLine(string line)
    {
        Match m = regex.Match(line);
        if (m.Success)
        {
            string timestamp = m.Groups[VRC_DATETIME].Value;
            string userId = m.Groups[VRC_USERID].Value;
            if (LogOutput)
            {
                logger.Info($"{COLOR_PREFIX}UserSelected : {userId}{COLOR_RESET.GetAnsiEscape()}");
            }

            Player? player = PlayerManager.SelectedUser(userId);
            Dictionary<string, string> actionData = new Dictionary<string, string>
            {
                { "timestamp", timestamp },
                { "userId", userId },
                { "userName", player?.DisplayName ?? string.Empty  },
            };

            ExecuteActions(actionData);

            return true;
        }
        return false;
    }
}