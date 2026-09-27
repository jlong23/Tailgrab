using System;
using System.Collections.Generic;
using System.Text;
using Tailgrab.Common;
using Tailgrab.PlayerManagement;

namespace Tailgrab.MCP.Tools
{
    internal class GetSelectedPlayerSubscriptionStatus : McpToolBase
    {
        public override string Name => "get_selected_player_subscription_status";
        public override string Description => "Gets the status and latest value for an active selected player subscription.";
        public override object InputSchema => new
        {
            type = "object",
            properties = new
            {
                subscriptionId = new
                {
                    type = "string",
                    description = "The subscription ID to check"
                }
            },
            required = new[] { "subscriptionId" }
        };

        private readonly ServiceRegistry _serviceRegistry;

        public GetSelectedPlayerSubscriptionStatus(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
            _serviceRegistry = serviceRegistry;
        }

        public override async Task<McpToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            await Task.Delay(0, cancellationToken);

            try
            {
                string subscriptionId = Convert.ToString(arguments["subscriptionId"]) ?? string.Empty;
                var subscriptionManager = _serviceRegistry.GetSubscriptionManager();

                if (!subscriptionManager.IsSubscriptionActive(subscriptionId))
                {
                    return McpToolResult.FromError($"Subscription '{subscriptionId}' is not active");
                }

                Player? currentPlayer = _serviceRegistry.GetPlayerManager().SelectedPlayer;
                var playerInfo = currentPlayer != null ? new
                {
                    displayName = currentPlayer.DisplayName,
                    userId = currentPlayer.UserId,
                    isFriend = currentPlayer.IsFriend,
                    isWatched = currentPlayer.IsWatched
                } : null;

                var result = new
                {
                    subscriptionId = subscriptionId,
                    isActive = true,
                    currentSelectedPlayer = playerInfo,
                    timestamp = DateTime.UtcNow
                };

                return McpToolResult.FromSuccess(result);
            }
            catch (Exception ex)
            {
                return McpToolResult.FromError($"Failed to get subscription status: {ex.Message}");
            }
        }
    }
}
