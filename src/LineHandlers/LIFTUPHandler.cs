namespace Tailgrab.LineHandler;

using System.Text.RegularExpressions;
using static Tailgrab.Clients.VRChat.VRChatClient;

public class LIFTUPHandler : AbstractLineHandler
{

    public static readonly string LOG_PATTERN = @"([\d]{4}.[\d]{2}.[\d]{2}\W[\d]{2}:[\d]{2}:[\d]{2})\W(Log[\W]{8}|Error[\W]{6})-\W\W\[LIFTUP\]\WERROR\: Couldn't find the player that left";
    public static readonly int VRC_DATETIME = 1;
    public static readonly int VRC_LOGTYPE = 2;


    public LIFTUPHandler(string matchPattern, ServiceRegistry serviceRegistry) : base(matchPattern, serviceRegistry)
    {
        logger.Info($"** LIFTUP Error Handler:  Regular Expression: {Pattern}");
    }

    public override bool HandleLine(string line)
    {
        Match m = regex.Match(line);
        if (m.Success)
        {
            string timestamp = m.Groups[VRC_DATETIME].Value;

            VRChatUserProfileEntry?  userProfileEntry  = _serviceRegistry.GetPlayerManager().LiftUpErrorHandler(this);

            Dictionary<string, string> actionData = new Dictionary<string, string>
            {
                { "timestamp", timestamp },
                { "userName", userProfileEntry?.DisplayName },
                { "userId", userProfileEntry?.UserId }
            };
            ExecuteActions(actionData);

            return true;
        }
        return false;
    }
}