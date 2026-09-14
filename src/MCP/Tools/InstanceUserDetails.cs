using Tailgrab.Common;
using Tailgrab.PlayerManagement;

namespace Tailgrab.MCP.Tools
{
    internal class InstanceUserDetails : McpToolBase
    {
        public override string Name => "instance_user_details";
        public override string Description => "Returns a detailed metadata about a user in the instance or recently in the instance. Use the UserId to guarantee a successful return.";
        public override object InputSchema => new
        {
            type = "object",
            properties = new
            {
                userName = new
                {
                    type = "string",
                    description = "The UTF-8 encoded display name of the user"
                },
                userId = new
                {
                    type = "string",
                    description = "The userId of the user"
                }

            },
            required = new[] { "userName" }
        };
        private readonly ServiceRegistry _serviceRegistry;

        public InstanceUserDetails(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
            _serviceRegistry = serviceRegistry;
        }

        public override async Task<McpToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            await Task.Delay(0, cancellationToken);

            try
            {
                string userId = arguments.ContainsKey("userId") ? arguments["userId"].ToString() ?? "" : "";
                string userName = arguments.ContainsKey("userName") ? arguments["userName"].ToString() ?? "" : "";
                Player? p = null;

                if(!string.IsNullOrEmpty(userId))
                {
                    p = PlayerManager.GetPlayerByUserId(userId);
                }
                else if (!string.IsNullOrEmpty(userName))
                {
                    p = PlayerManager.GetPlayerByDisplayName(userName);
                }

                return FormatPlayerCurrentInfo(p);
            }
            catch (Exception ex)
            {
                return McpToolResult.FromError($"An error occurred while executing the tool: {ex.Message}");
            }
        }
    }
}
