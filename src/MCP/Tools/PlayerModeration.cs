using NLog;
using Tailgrab.Common;
using Tailgrab.PlayerManagement;
using VRChat.API.Model;

namespace Tailgrab.MCP.Tools
{
    internal class PlayerModeration : McpToolBase
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public override string Name => "player_moderation";
        public override string Description => "Moderates the selected player by user_id or set of players by the avatar_id. The possible action values are \"block\", \"hideavatar\", \"interactoff\", \"interacton\", \"mute\", \"mutechat\", \"showavatar\", \"unmute\", \"unmutechat\" ";
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
            string argumentsString = string.Join(", ", arguments.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            logger.Info($"Executing PlayerModeration tool. Arguments: {argumentsString}");

            try
            {
                string userId = arguments.ContainsKey("user_id") ? arguments["user_id"].ToString() ?? "" : "";
                string avatarId = arguments.ContainsKey("avatar_id") ? arguments["avatar_id"].ToString() ?? "" : "";
                string action = arguments.ContainsKey("action") ? arguments["action"].ToString() ?? "" : "";
                PlayerModerationType moderationType = PlayerModerationTypeMapper.MapStringToEnum(action);

                if (string.IsNullOrEmpty(action))
                {
                    logger.Error($"Missing or invalid 'action' argument. Arguments: {argumentsString}");
                    return McpToolResult.FromError("Missing or invalid 'action' argument.");
                }

                if (string.IsNullOrEmpty(userId))
                {
                    logger.Error($"Missing or invalid 'user_id' argument. Arguments: {argumentsString}");
                    return McpToolResult.FromError("Missing or invalid 'user_id' argument.");
                }

                Player? player = PlayerManager.GetPlayerByUserId(userId);
                if (player == null)
                {
                    logger.Error($"Player with ID {userId} not found. Arguments: {argumentsString}");
                    return McpToolResult.FromError($"Player with ID {userId} not found.");
                }

                if (!string.IsNullOrEmpty(avatarId))
                {
                    string results = ModeratePlayerByAvatarId(avatarId, moderationType);
                    return McpToolResult.FromSuccess(results);
                }

                bool moderationStatus = await ServiceRegistry.GetVRChatAPIClient().PersonalModeration(player.UserId, PlayerModerationType.Block);
                if(!moderationStatus)
                {
                    logger.Error($"Failed to moderate " + FormatPlayerLine(player, moderationType) + $". Arguments: {argumentsString}");
                    return McpToolResult.FromError($"Failed to moderate " + FormatPlayerLine(player, moderationType));
                }

                logger.Info($"Successfully moderated " + FormatPlayerLine(player, moderationType) + $". Arguments: {argumentsString}");
                return McpToolResult.FromSuccess($"Successfully moderated " + FormatPlayerLine(player, moderationType));
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"An error occurred while executing the tool. Arguments: {argumentsString}");
                return McpToolResult.FromError($"An error occurred while executing the tool: {ex.Message}");
            }
        }

        private string ModeratePlayerByAvatarId(string avatarId, PlayerModerationType moderationType)
        {
            string results = string.Empty;
            try
            {
                logger.Info($"Moderating players with avatar ID {avatarId} using moderation type {moderationType}. This may take some time depending on the number of players with this avatar.");

                moderationType = PlayerModerationType.Block;

                IEnumerable<Player> players = PlayerManager.GetAllPlayersByAvatarId(avatarId);
                foreach (Player player in players)
                {
                    bool success = Task.Run(() => ServiceRegistry.GetVRChatAPIClient().PersonalModeration(player.UserId, moderationType)).GetAwaiter().GetResult();
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

        private string FormatPlayerLine(Player player, PlayerModerationType moderationType)
        {
            return $"player {player.DisplayName} (ID: {player.UserId}) with moderation type: {moderationType}\n";
        }
    }
}
