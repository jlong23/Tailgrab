using Tailgrab.PlayerManagement;

namespace Tailgrab.MCP.Tools
{
    internal class InstanceUserDetails : McpToolBase
    {
        public override string Name => "instance_user_details";
        public override string Description => "Retrieves detailed information for a specific user in the current instance. Use the User_Id (typically obtained from an instance_user_list) to ensure a guaranteed and accurate retrieval of that individual's details.";
        public override object InputSchema => new
        {
            type = "object",
            properties = new
            {
                user_name = new
                {
                    type = "string",
                    description = "The UTF-8 encoded display name of the user"
                },
                user_id = new
                {
                    type = "string",
                    description = "The userId of the user"
                }

            },
            required = new[] { "user_name" }
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
                string userId = arguments.ContainsKey("user_id") ? arguments["user_id"].ToString() ?? "" : "";
                string userName = arguments.ContainsKey("user_name") ? arguments["user_name"].ToString() ?? "" : "";
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
