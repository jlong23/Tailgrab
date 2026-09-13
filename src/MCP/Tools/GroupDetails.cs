using Tailgrab.Common;
using Tailgrab.Clients.VRChat;
using VRChat.API.Model;
using Tailgrab.Models;

namespace Tailgrab.MCP.Tools
{
    internal class GroupDetails : McpToolBase
    {
        public override string Name => "group_details";
        public override string Description => "Returns a detailed metadata about a VRChat group.";
        public override object InputSchema => new
        {
            type = "object",
            properties = new
            {
                groupId = new
                {
                    type = "string",
                    description = "The groupId of the VRChat group"
                }
            },
            required = new[] { "groupId" }
        };
        private readonly ServiceRegistry _serviceRegistry;

        public GroupDetails(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
            _serviceRegistry = serviceRegistry;
        }

        public override async Task<McpToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            await Task.Delay(0, cancellationToken);

            try
            {
                string groupId = arguments.ContainsKey("groupId") ? arguments["groupId"].ToString() ?? "" : "";
                // Use 'var' to avoid referencing the unknown Result<>/Group types directly
                VRChatClient.Result<Group?> results = _serviceRegistry.GetVRChatAPIClient().GetGroupById(groupId);

                if (results == null || results.Value == null || results.HasException)
                    return McpToolResult.FromError("Group not found");


                Group g = results.Value;
                TimeSpan elapsed = DateTime.Now - g.CreatedAt;

                TailgrabDBContext dBContext = _serviceRegistry.GetDBContext();
                GroupInfo? groupInfo = dBContext.GroupInfos.Find(groupId);

                User u = _serviceRegistry.GetVRChatAPIClient().GetProfile(g.OwnerId);

                var info = new
                {
                    timestamp = DateTime.UtcNow,
                    application = "Tailgrab",
                    version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
                    environment = new
                    {
                        name = g.Name,
                        groupId = g.Id,
                        ownerId = g.OwnerId,
                        ownerName = u.DisplayName,
                        groupAgeInDays = elapsed.TotalDays,
                        isVerified = g.IsVerified,
                        isWatched = groupInfo?.AlertType > AlertTypeEnum.None,
                        iconUrl = g.IconUrl ?? "",
                        bannerUrl = g.BannerUrl ?? "",
                        description = g.Description ?? "",
                        rules = g.Rules ?? ""
                    }
                };

                return McpToolResult.FromSuccess(info);
            }
            catch (Exception ex)
            {
                return McpToolResult.FromError($"An error occurred while executing the tool: {ex.Message}");
            }
        }
    }
}
