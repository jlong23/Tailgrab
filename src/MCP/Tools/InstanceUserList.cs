using NLog;
using System;
using System.Collections.Generic;
using System.Text;
using Tailgrab.PlayerManagement;

namespace Tailgrab.MCP.Tools
{
    internal class InstanceUserList : McpToolBase
    {
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public override string Name => "instance_user_list";
        public override string Description => "Retrieves a paginated list of users in the current VRChat instance. Retrieves UserName, UserId, If they are a friend, If the user is watched.";
        public override object InputSchema => new
        {
            type = "object",
            properties = new
            {            
                page_number = new
                {
                    type = "integer",
                    description = "The page number of the user list to retrieve, 1 indexed"
                }
            },
            required = new[] { "page_number" }

        };
        private readonly ServiceRegistry _serviceRegistry;

        public InstanceUserList(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
            _serviceRegistry = serviceRegistry;
        }

        public override async Task<McpToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            await Task.Delay(0, cancellationToken);

            PlayerManager playerMgr = _serviceRegistry.GetPlayerManager();
            IEnumerable<Player> playerIter = PlayerManager.GetAllPlayers();
            logger.Info($"Page Number: {arguments["page_number"]}");
            int pageNumber = Convert.ToInt32(arguments["page_number"].ToString());
            int pageSize = 15;
            int totalPlayersInInstance = playerIter.Count(p => p.InstanceEndTime == null);
            bool hasMorePages = totalPlayersInInstance > pageNumber * pageSize;
            var playersInInstance = playerIter
                .Where(p => p.InstanceEndTime == null)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    user_name = p.DisplayName,
                    user_id = p.UserId,
                    friend = ConvertBooleanToYesNo(p.IsFriend),
                    watched = ConvertBooleanToYesNo(p.IsWatched)
                })
                .ToList();


            var info = new
            {
                timestamp = DateTime.UtcNow,
                application = "Tailgrab",
                version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
                environment = new
                {
                    ttlMs = 5000,
                    cacheScope = "private",
                    page_number = pageNumber,
                    page_size = pageSize,
                    total_count = totalPlayersInInstance,
                    has_more = hasMorePages,
                    players = playersInInstance
                }
            };

            return McpToolResult.FromSuccess(info);
        }
    }
}

