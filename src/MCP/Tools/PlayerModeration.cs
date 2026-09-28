using Tailgrab.Common;
using Tailgrab.PlayerManagement;

namespace Tailgrab.MCP.Tools
{
    internal class PlayerModeration : McpToolBase
    {
        public override string Name => "player_moderation";
        public override string Description => "Moderates the selected player by user_id or set of players by the avatar_id. The possible moderation actions are \"block\", \"hideavatar\", \"interactoff\", \"interacton\", \"mute\", \"mutechat\", \"showavatar\", \"unmute\", \"unmutechat\" ";
        public override object InputSchema => new
        {
            type = "object",
            properties = new
            {
                user_user = new { type = "string" },
                avatar_id = new { type = "string" },
                action = new
                {
                    type = "string",
                    @enum = new[] { "block", "hideavatar", "interactoff", "interacton", "mute", "mutechat", "showavatar", "unmute", "unmutechat" }
                }
            },
            required = new string[] { "user_id", "action" }
        };

        public PlayerModeration(ServiceRegistry serviceRegistry) : base(serviceRegistry)
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

                if ( isAvatarIdProvided)
                {
                    string avatarId = avatarIdObj as string ?? string.Empty;
                    string results = ModeratePlayerByAvatarId(avatarId, action);
                    return McpToolResult.FromSuccess(results);
                }

                bool moderationStatus = await ServiceRegistry.GetVRChatAPIClient().PersonalModeration(player.UserId, PlayerModerationTypeMapper.MapStringToEnum(action));
                if(!moderationStatus)
                {
                    return McpToolResult.FromError($"Failed to moderate " + FormatPlayerLine(player, action));
                }
                return McpToolResult.FromSuccess($"Successfully moderated " + FormatPlayerLine(player, action));
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
                VRChat.API.Model.PlayerModerationType moderationTypeEnum = PlayerModerationTypeMapper.MapStringToEnum(moderationType);

                IEnumerable<Player> players = PlayerManager.GetAllPlayersByAvatarId(avatarId);
                foreach (Player player in players)
                {
                    bool success = Task.Run(() => ServiceRegistry.GetVRChatAPIClient().PersonalModeration(player.UserId, moderationTypeEnum)).GetAwaiter().GetResult();
                    if (success)
                    {

                        results += $"Successfully moderated " + FormatPlayerLine(player, moderationType);
                    }
                    else
                    {
                        results += $"Failed to moderate " + FormatPlayerLine(player, moderationType);
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                return $"An error occurred while moderating players with avatar ID {avatarId}: {ex.Message}";
            }
        }

        private string FormatPlayerLine(Player player, string moderationType)
        {
            return $"player {player.DisplayName} (ID: {player.UserId}) with moderation type: {moderationType}\n";
        }
    }
}
