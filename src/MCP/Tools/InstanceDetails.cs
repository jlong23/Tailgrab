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
                    playersInInstance = playerCount,
                    worldName = worldInfo?.WorldName ?? "unknown",
                    worldId = worldInfo?.WorldId ?? "unknown",
                    groupName = worldInfo?.GroupName ?? "unknown",
                    groupId = worldInfo?.GroupId ?? "unknown",
                    groupAccessType = worldInfo?.GroupAccessType ?? "unknown",
                    userName = worldInfo?.UserName ?? "unknown",
                    userId = worldInfo?.UserId ?? "unknown",
                    privateUserAccessType = worldInfo?.PrivateAccessType ?? "unknown",
                    region = worldInfo?.Region ?? "unknown",
                    isAgeGated = worldInfo?.AgeGated ?? false,
                    instanceStartTime = worldInfo?.StartDateTime ?? DateTime.MinValue,
                    instanceDurationSeconds = (worldInfo != null) ? (DateTime.Now - worldInfo.StartDateTime).TotalSeconds : 0
                }
            };

            return McpToolResult.FromSuccess(info);
        }
    }
}
