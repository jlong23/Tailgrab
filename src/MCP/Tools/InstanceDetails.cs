using System;
using System.Collections.Generic;
using System.Text;
using Tailgrab;
using Tailgrab.Common;
using Tailgrab.MCP.Tools;
using Tailgrab.Models;
using Tailgrab.PlayerManagement;

namespace Tailgrab.MCP.Tools
{
    internal class InstanceDetails : McpToolBase
    {
        public override string Name => "instance_details";
        public override string Description => "Returns a summary of the current VRChat instance and instance metadata.";
        public override object InputSchema => new
        {
            type = "object",
            properties = new { },
            required = new string[] { }
        };
        private readonly ServiceRegistry _serviceRegistry;

        public InstanceDetails(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
            _serviceRegistry = serviceRegistry;
        }

        public override async Task<McpToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            await Task.Delay(0, cancellationToken);

            PlayerManager playerMgr = _serviceRegistry.GetPlayerManager();
            IEnumerable<Player> playerIter = PlayerManager.GetAllPlayers();
            WorldInstanceInfo worldInfo = PlayerManager.CurrentSession;
            int playerCount = playerIter.Count(p => p.InstanceEndTime == null);
            var info = new
            {
                timestamp = DateTime.UtcNow,
                application = "Tailgrab",
                version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
                environment = new
                {
                    ttlMs = 5000,
                    cacheScope = "private",
                    player_count_in_instance = playerCount,
                    world_name = worldInfo?.WorldName ?? "unknown",
                    world_id = worldInfo?.WorldId ?? "unknown",
                    group_name = worldInfo?.GroupName ?? "unknown",
                    group_id = worldInfo?.GroupId ?? "unknown",
                    group_access_type = worldInfo?.GroupAccessType ?? "unknown",
                    user_name = worldInfo?.UserName ?? "unknown",
                    user_id = worldInfo?.UserId ?? "unknown",
                    private_user_access_type = worldInfo?.PrivateAccessType ?? "unknown",
                    region = worldInfo?.Region ?? "unknown",
                    age_gated = ConvertBooleanToYesNo(worldInfo?.AgeGated),
                    instance_start_time = worldInfo?.StartDateTime ?? DateTime.MinValue,
                    instance_duration_seconds = (worldInfo != null) ? (DateTime.Now - worldInfo.StartDateTime).TotalSeconds : 0
                }
            };

            return McpToolResult.FromSuccess(info);
        }
    }
}
