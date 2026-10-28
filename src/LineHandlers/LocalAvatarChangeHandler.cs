namespace Tailgrab.LineHandler;

using System.Text.RegularExpressions;
using Tailgrab.Common;
using Tailgrab.PlayerManagement;

public class LocalAvatarChangeHandler : AbstractLineHandler
{

    public static readonly string LOG_PATTERN = @"([\d]{4}.[\d]{2}.[\d]{2}\W[\d]{2}:[\d]{2}:[\d]{2})\W(Log[\W]{8}|Debug[\W]{6})-\W\W\[API\]\W\[(\d+)\] Sending Put request to https:\/\/api.vrchat.cloud\/api\/1\/avatars\/([\d\w\W]+)\/select";
    public static readonly int VRC_DATETIME = 1;
    public static readonly int VRC_LOGTYPE = 2;
    public static readonly int VRC_THREAD_ID = 3;
    public static readonly int VRC_AVATAR_ID = 4;


    public LocalAvatarChangeHandler(string matchPattern, ServiceRegistry serviceRegistry) : base(matchPattern, serviceRegistry)
    {
        logger.Info($"** LocalAvatarChangeHandler Handler:  Regular Expression: {Pattern}");
    }

    public override bool HandleLine(string line)
    {
        Match m = regex.Match(line);
        if (m.Success)
        {
            string timestamp = m.Groups[VRC_DATETIME].Value;
            string avatarId = m.Groups[VRC_AVATAR_ID].Value;
            if (LogOutput)
            {
                logger.Info($"{COLOR_PREFIX}Local User Avatar ID : {avatarId}{COLOR_RESET.GetAnsiEscape()}");
            }

            PlayerManager mgr = _serviceRegistry.GetPlayerManager();
            mgr.LocalAvatarId = avatarId;

            Dictionary<string, string> actionData = new Dictionary<string, string>
            {
                { "timestamp", timestamp },
                { "avatarId", avatarId ?? string.Empty },
            };

            ExecuteActions(actionData);

            return true;
        }
        return false;
    }
}