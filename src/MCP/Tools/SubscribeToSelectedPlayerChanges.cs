using System;
using System.Collections.Generic;
using System.Text;
using Tailgrab.Common;
using Tailgrab.PlayerManagement;

namespace Tailgrab.MCP.Tools
{
    internal class SubscribeToSelectedPlayerChanges : McpToolBase
    {
        public override string Name => "subscribe_to_selected_player_changes";
        public override string Description => "Creates a subscription to monitor changes to the selected player. Returns a subscription ID that can be monitored for updates.";
        public override object InputSchema => new
        {
            type = "object",
            properties = new
            {
            },
            required = new string[] { }
        };

        private readonly ServiceRegistry _serviceRegistry;

        public SubscribeToSelectedPlayerChanges(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
            _serviceRegistry = serviceRegistry;
        }

        public override async Task<McpToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            await Task.Delay(0, cancellationToken);

            try
            {
                var subscriptionManager = _serviceRegistry.GetSubscriptionManager();
                string subscriptionId = subscriptionManager.CreateSubscription("selected_player_changed");

                var result = new
                {
                    subscriptionId = subscriptionId,
                    subscriptionType = "selected_player_changed",
                    message = "Successfully subscribed to selected player changes. Use get_subscription_status to monitor updates."
                };

                return McpToolResult.FromSuccess(result);
            }
            catch (Exception ex)
            {
                return McpToolResult.FromError($"Failed to create subscription: {ex.Message}");
            }
        }
    }
}
