using System;
using System.Collections.Generic;
using System.Text;
using Tailgrab.Common;
using Tailgrab.PlayerManagement;

namespace Tailgrab.MCP.Tools
{
    internal class GroupModeration : McpToolBase
    {

        public override string Name => "group_moderation";
        public override string Description => "Bans the selected player by user_id or set of players by the avatar_id.";
        public override object InputSchema => new
        {
            type = "object",
            properties = new
            {
                user_id = new { type = "string" },
                avatar_id = new { type = "string" }
            },
            required = new string[] { "user_id" }
        };

        public GroupModeration(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        public override async Task<McpToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            await Task.Delay(0, cancellationToken);
            try
            {
                bool isAvatarIdProvided = arguments.TryGetValue("avatar_id", out var avatarIdObj) && avatarIdObj is string;
                if (!arguments.TryGetValue("action", out var actionObj) || actionObj is not string action)
                {
                    return McpToolResult.FromError("Missing or invalid 'action' argument.");
                }

                if (!arguments.TryGetValue("user_id", out var userIdObj) || userIdObj is not string userId)
                {
                    return McpToolResult.FromError("Missing or invalid 'user_id' argument.");
                }

                Player? player = PlayerManager.GetPlayerByUserId(userId);
                if (player == null)
                {
                    return McpToolResult.FromError($"Player with ID {userId} not found.");
                }

                if (isAvatarIdProvided)
                {
                    string avatarId = avatarIdObj as string ?? string.Empty;
                    string results = ModeratePlayerByAvatarId(avatarId, action);
                    return McpToolResult.FromSuccess(results);
                }

                ActionResultSet moderationStatus = await ServiceRegistry.GetModerationManager().BanUserFromGroups(userId);
                return McpToolResult.FromSuccess(moderationStatus.Message);
            }
            catch (Exception ex)
            {
                return McpToolResult.FromError($"An error occurred while executing the tool: {ex.Message}");
            }
        }

        private string ModeratePlayerByAvatarId(string avatarId, string moderationType)
        {
            string results = string.Empty;
            try
            {
                IEnumerable<Player> players = PlayerManager.GetAllPlayersByAvatarId(avatarId);
                foreach (Player player in players)
                {
                    ActionResultSet success = Task.Run(() => ServiceRegistry.GetModerationManager().BanUserFromGroups(player.UserId)).GetAwaiter().GetResult();
                    results += success.Message;
                }
                return results;
            }
            catch (Exception ex)
            {
                return $"An error occurred while moderating players with avatar ID {avatarId}: {ex.Message}";
            }
        }

    }
}