namespace Tailgrab.LineHandler;

using System.Text.RegularExpressions;
using Tailgrab.Common;
using Tailgrab.PlayerManagement;

public class AvatarSecurityUser : AbstractLineHandler
{

    public static readonly string LOG_PATTERN = @"([\d]{4}.[\d]{2}.[\d]{2}\W[\d]{2}:[\d]{2}:[\d]{2})\W(Log[\W]{8}|Debug[\W]{6})-\W\W\[API\]\W\[(\d+)\] Sending Get request to https:\/\/api.vrchat.cloud\/api\/1\/analysis\/([\d\w\W]+)";
    public static readonly int VRC_DATETIME = 1;
    public static readonly int VRC_LOGTYPE = 2;
    public static readonly int VRC_THREAD_ID = 3;
    public static readonly int VRC_SECURITY_URI = 4;


    public AvatarSecurityUser(string matchPattern, ServiceRegistry serviceRegistry) : base(matchPattern, serviceRegistry)
    {
        logger.Info($"** AvatarSecurityUser Handler:  Regular Expression: {Pattern}");
    }

    public override bool HandleLine(string line)
    {
        Match m = regex.Match(line);
        if (m.Success)
        {
            string timestamp = m.Groups[VRC_DATETIME].Value;
            string securityId = m.Groups[VRC_SECURITY_URI].Value;
            if (LogOutput)
            {
                logger.Info($"{COLOR_PREFIX}Security File URI : {securityId}{COLOR_RESET.GetAnsiEscape()}");
            }

            PlayerManager mgr = _serviceRegistry.GetPlayerManager();
            string? avatarId = mgr.SecurityFileURI(securityId);

            Dictionary<string, string> actionData = new Dictionary<string, string>
            {
                { "timestamp", timestamp },
                { "securityId", securityId },
                { "avatarId", avatarId ?? string.Empty },
            };

            ExecuteActions(actionData);

            return true;
        }
        return false;
    }
}