namespace Tailgrab.MCP.Tools
{
    /// <summary>
    /// Example system info tool that demonstrates MCP tool pattern.
    /// </summary>
    public class SystemInfoTool : McpToolBase
    {
        public override string Name => "system_info";
        public override string Description => "Returns basic system and application information";
        public override object InputSchema => new
        {
            type = "object",
            properties = new { },
            required = new string[] { }
        };

        public SystemInfoTool(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        public override async Task<McpToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            await Task.Delay(0, cancellationToken);

            var info = new
            {
                timestamp = DateTime.UtcNow,
                application = "Tailgrab",
                version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
                environment = new
                {
                    os_version = Environment.OSVersion.VersionString,
                    processor_count = Environment.ProcessorCount,
                    total_memory = GC.GetTotalMemory(false) / (1024 * 1024) + " MB"
                }
            };

            return McpToolResult.FromSuccess(info);
        }
    }
}

