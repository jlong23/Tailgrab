using Tailgrab.Models;

namespace Tailgrab.MCP.Tools
{
    /// <summary>
    /// Example system info tool that demonstrates MCP tool pattern.
    /// </summary>
    public class WatchedGroups : McpToolBase
    {
        public override string Name => "watched_groups";
        public override string Description => "Returns the list of watched groups";
        public override object InputSchema => new
        {
            type = "object",
            properties = new { },
            required = new string[] { }
        };

        private readonly ServiceRegistry _serviceRegistry;  

        public WatchedGroups(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
            _serviceRegistry = serviceRegistry;
        }

        public override async Task<McpToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            await Task.Delay(0, cancellationToken);
            TailgrabDBContext dbContext = _serviceRegistry.GetDBContext();

            var totalCount = dbContext.GroupInfos.Count();
            var maxLastUpdate = dbContext.GroupInfos
                .AsEnumerable()
                .Max(g => (DateTime?)g.UpdatedAt) ?? DateTime.MinValue;

            var recentGroups = dbContext.GroupInfos
                .OrderByDescending(g => g.UpdatedAt)
                .Take(10)
                .AsEnumerable()
                .Select(g => new
                {
                    id = g.GroupId,
                    name = g.GroupName,
                    alert_level = g.AlertType.ToString()
                })
                .ToList();

            var info = new
            {
                timestamp = DateTime.UtcNow,
                application = "Tailgrab",
                version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
                environment = new
                {
                    total_groups = totalCount,
                    max_last_update_date_time = maxLastUpdate,
                    recently_updated_groups = recentGroups
                }
            };

            return McpToolResult.FromSuccess(info);
        }
    }
}

