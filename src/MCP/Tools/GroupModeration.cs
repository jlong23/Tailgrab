using NLog;
using Tailgrab.Common;
using Tailgrab.PlayerManagement;

namespace Tailgrab.MCP.Tools
{
    internal class GroupModeration : McpToolBase
    {

        private static Logger logger = LogManager.GetCurrentClassLogger();
        public override string Name => "group_moderation";
        public override string Description => "Bans the selected player by user_id or set of players by the user_id and avatar_id.";
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
            string argumentsString = string.Join(", ", arguments.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            logger.Info($"Executing GroupModeration tool. Arguments: {argumentsString}");
            try
            {
                string userId = arguments.ContainsKey("user_id") ? arguments["user_id"].ToString() ?? "" : "";
                string avatarId = arguments.ContainsKey("avatar_id") ? arguments["avatar_id"].ToString() ?? "" : "";

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
                    string results = ModeratePlayerByAvatarId(avatarId);
                    return McpToolResult.FromSuccess(results);
                }

                ActionResultSet moderationStatus = await ServiceRegistry.GetModerationManager().BanUserFromGroups(userId);
                return McpToolResult.FromSuccess(moderationStatus.Message);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"An error occurred while executing the GroupModeration tool. {argumentsString}");
                return McpToolResult.FromError($"An error occurred while executing the tool: {ex.Message}");
            }
        }

        private string ModeratePlayerByAvatarId(string avatarId)
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
                logger.Error(ex, $"An error occurred while moderating players with avatar ID {avatarId}.");
                return $"An error occurred while moderating players with avatar ID {avatarId}: {ex.Message}";
            }
        }

    }
}