using Tailgrab.PlayerManagement;

namespace Tailgrab.MCP.Tools
{
    internal class SelectedPlayer : McpToolBase
    {
        public override string Name => "selected_user_details";
        public override string Description => "Returns a detailed metadata about the current selected user in the instance or recently in the instance.";
        public override object InputSchema => new
        {
            type = "object",
            properties = new
            {
            },
            required = new string[] { }
        };
        private readonly ServiceRegistry _serviceRegistry;

        public SelectedPlayer(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
            _serviceRegistry = serviceRegistry;
        }

        public override async Task<McpToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            await Task.Delay(0, cancellationToken);

            try
            {                
                Player? p = _serviceRegistry.GetPlayerManager().SelectedPlayer;
                return FormatPlayerCurrentInfo(p);
            }
            catch (Exception ex)
            {
                return McpToolResult.FromError($"An error occurred while executing the tool: {ex.Message}");
            }
        }
    }
}
