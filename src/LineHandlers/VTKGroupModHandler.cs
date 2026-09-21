using System.Text.RegularExpressions;
using Tailgrab.Common;
using Tailgrab.PlayerManagement;

namespace Tailgrab.LineHandler
{
    public class VTKGroupModHandler : AbstractLineHandler
    {

        public static readonly string LOG_PATTERN = @"([\d]{4}.[\d]{2}.[\d]{2}\W[\d]{2}:[\d]{2}:[\d]{2})\W(Log[\W]{8}|Debug[\W]{6})-\W\W\[ModerationManager\] A vote kick has been initiated against ([\S\W]+) by ([\S\W]+), do you agree\?";
        public static readonly int VRC_DATETIME = 1;
        public static readonly int VRC_LOGTYPE = 2;
        public static readonly int VRC_DISPLAYNAME = 3;
        public static readonly int VRC_INITIATED_DISPLAYNAME = 4;


        public VTKGroupModHandler(string matchPattern, ServiceRegistry serviceRegistry) : base(matchPattern, serviceRegistry)
        {
            logger.Info($"** Vote to Kick Group Moderation Handler:  Regular Expression: {Pattern}");
        }

        public override bool HandleLine(string line)
        {
            Match m = regex.Match(line);
            if (m.Success)
            {
                string timestamp = m.Groups[VRC_DATETIME].Value;
                string userName = m.Groups[VRC_DISPLAYNAME].Value;
                string initiatedBy = m.Groups[VRC_INITIATED_DISPLAYNAME].Value;
                if (LogOutput)
                {
                    logger.Info($"{COLOR_PREFIX}VTK : {userName} initiated by {initiatedBy}{COLOR_RESET.GetAnsiEscape()}");
                }

                Player? player = PlayerManager.VoteToKickEventGroup(userName, initiatedBy, $"Vote kick requested against player by {initiatedBy}.");
                Player? initatedPlayer = PlayerManager.GetPlayerByDisplayName(initiatedBy);
                Dictionary<string, string> actionData = new Dictionary<string, string>
                {
                    { "timestamp", timestamp },
                    { "userName", userName },
                    { "userId", player?.UserId.ToString() ?? string.Empty  },
                    { "initiatedBy", initiatedBy },
                    { "initiatedByUserId", initatedPlayer?.UserId.ToString() ?? string.Empty }
                };

                ExecuteActions(actionData);

                return true;
            }
            return false;
        }
    }
}
