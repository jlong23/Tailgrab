using Tailgrab.Models;

namespace Tailgrab.MCP.Tools
{
    /// <summary>
    /// Example system info tool that demonstrates MCP tool pattern.
    /// </summary>
    public class WatchedAvatars : McpToolBase
    {
        public override string Name => "watched_avatars";
        public override string Description => "Returns the list of watched avatars";
        public override object InputSchema => new
        {
            type = "object",
            properties = new { },
            required = new string[] { }
        };

        private readonly ServiceRegistry _serviceRegistry;  

        public WatchedAvatars(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
            _serviceRegistry = serviceRegistry;
        }

        public override async Task<McpToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            await Task.Delay(0, cancellationToken);
            TailgrabDBContext dbContext = _serviceRegistry.GetDBContext();

            var totalCount = dbContext.AvatarInfos.Count();
            var maxLastUpdate = dbContext.AvatarInfos
                .AsEnumerable()
                .Max(a => (DateTime?)a.UpdatedAt) ?? DateTime.MinValue;

            var recentAvatars = dbContext.AvatarInfos
                .OrderByDescending(a => a.UpdatedAt)
                .Take(10)
                .AsEnumerable()
                .Select(a => new
                {
                    id = a.AvatarId,
                    name = a.AvatarName,
                    alertLevel = a.AlertType
                })
                .ToList();

            var info = new
            {
                timestamp = DateTime.UtcNow,
                application = "Tailgrab",
                version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
                environment = new
                {
                    totalAvatars = totalCount,
                    maxLastUpdateDateTime = maxLastUpdate,
                    recentlyUpdatedAvatars = recentAvatars
                }
            };

            return McpToolResult.FromSuccess(info);
        }
    }
}

