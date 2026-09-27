using System;
using System.Collections.Generic;
using System.Text;
using Tailgrab.Common;
using Tailgrab.PlayerManagement;

namespace Tailgrab.MCP.Tools
{
    internal class UnsubscribeFromSelectedPlayerChanges : McpToolBase
    {
        public override string Name => "unsubscribe_from_selected_player_changes";
        public override string Description => "Terminates a subscription to selected player changes using its subscription ID.";
        public override object InputSchema => new
        {
            type = "object",
            properties = new
            {
                subscriptionId = new
                {
                    type = "string",
                    description = "The subscription ID to terminate, obtained from subscribe_to_selected_player_changes"
                }
            },
            required = new[] { "subscriptionId" }
        };

        private readonly ServiceRegistry _serviceRegistry;

        public UnsubscribeFromSelectedPlayerChanges(ServiceRegistry serviceRegistry) : base(serviceRegistry)
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
                bool terminated = subscriptionManager.TerminateSubscription(subscriptionId);

                if (!terminated)
                {
                    return McpToolResult.FromError($"Subscription '{subscriptionId}' not found");
                }

                var result = new
                {
                    subscriptionId = subscriptionId,
                    message = "Subscription terminated successfully"
                };

                return McpToolResult.FromSuccess(result);
            }
            catch (Exception ex)
            {
                return McpToolResult.FromError($"Failed to terminate subscription: {ex.Message}");
            }
        }
    }
}
