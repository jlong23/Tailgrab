using System;
using System.Collections.Generic;
using System.Reactive.Joins;
using System.Text;
using System.Text.RegularExpressions;
using Tailgrab;
using Tailgrab.LineHandler;
using Tailgrab.PlayerManagement;
using static Tailgrab.Clients.VRChat.VRChatClient;

namespace Tailgrab.LineHandlers
{
    internal class PlayerModerationHandler : AbstractLineHandler
    {

        public static readonly string LOG_PATTERN = @"([\d]{4}.[\d]{2}.[\d]{2}\W[\d]{2}:[\d]{2}:[\d]{2})\W(Log[\W]{8}|Debug[\W]{6})-\W\W\[API\]\WRequesting Post auth\/user\/playermoderations (\{\{""type"":""([^""]+)"",\s*""moderated"":""([^""]+)""[^}]*\}\}) retryCount\:";
        public static readonly int VRC_DATETIME = 1;
        public static readonly int VRC_LOGTYPE = 2;

        public static readonly int VRC_JSON_PAYLOAD = 3;
        public static readonly int VRC_MODERATION_TYPE = 4;
        public static readonly int VRC_USER_ID = 5;


        public PlayerModerationHandler(string matchPattern, ServiceRegistry serviceRegistry) : base(matchPattern, serviceRegistry)
        {
            logger.Info($"** PlayerModeration Handler:  Regular Expression: {Pattern}");
        }

        public override bool HandleLine(string line)
        {
            Match m = regex.Match(line);
            if (m.Success)
            {
                string timestamp = m.Groups[VRC_DATETIME].Value;

                string userId = m.Groups[VRC_USER_ID].Value;
                string moderationType = m.Groups[VRC_MODERATION_TYPE].Value;

                Player? player = PlayerManager.PlayerModerationEvent(userId, moderationType);
                if (player == null)
                {
                    logger.Warn($"PlayerModerationHandler: Could not find user profile entry for the player that has user moderation.");
                }

                Dictionary<string, string> actionData = new Dictionary<string, string>
                {
                    { "timestamp", timestamp },
                    { "userName", player?.DisplayName ?? "Unknown" },
                    { "userId", player?.UserId ?? "Unknown" },
                    { "moderationType", moderationType  }

                };
                ExecuteActions(actionData);

                return true;
            }

            return false;
        }
    }
}