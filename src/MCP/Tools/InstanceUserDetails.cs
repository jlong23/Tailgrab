using Tailgrab.Common;
using Tailgrab.PlayerManagement;

namespace Tailgrab.MCP.Tools
{
    internal class InstanceUserDetails : McpToolBase
    {
        public override string Name => "instance_user_details";
        public override string Description => "Returns a detailed metadata about a user in the instance or reciently in the instance.";
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

                if (p == null)
                    return McpToolResult.FromError("Player not found");

                List<PlayerPrint> printData = p.PrintData?.Values?.ToList() ?? new List<PlayerPrint>();
                var printHistory = printData.Select(p => new
                {
                    printId = p.PrintId,
                    displayName = p.AuthorName,
                    authorId = p.OwnerId,
                    evaluation = p.AIEvaluation,
                    printUri = p.PrintUrl
                }).ToList();

                List<PlayerInventory> inventoryData = p.Inventory?.ToList() ?? new List<PlayerInventory>();
                var emojiStickerHistory = inventoryData.Select(i => new
                {
                    inventoryId = i.InventoryId,
                    inventoryType = i.InventoryType,
                    evaluation = i.AIEvaluation,
                    imageUri = i.ItemUrl
                }).ToList();

                List<PlayerEvent> eventData = p.Events?.ToList() ?? new List<PlayerEvent>();
                var eventHistory = eventData.OrderBy(e => e.EventTime).Select(e => new
                {
                    eventType = e.Type.ToString(),
                    eventTime = e.EventTime,
                    eventDescription = e.EventDescription
                }).ToList();

                DateTime joinDate = DateTime.Parse(p.DateJoined.ToString() ?? new DateTime().ToString());
                TimeSpan elapsed = DateTime.Now - joinDate;

                var info = new
                {
                    timestamp = DateTime.UtcNow,
                    application = "Tailgrab",
                    version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
                    environment = new
                    {
                        displayName = p.DisplayName,
                        userId = p.UserId,
                        currentAvatarName = p.AvatarName ?? "",
                        currentAvatarId = p.AvatarId ?? "",
                        durationSeconds = (DateTime.Now - p.InstanceStartTime).TotalSeconds,
                        accountAgeInDays = elapsed.TotalDays,
                        isVerified = p.AgeVerified != AgeVerificationEnum.UNVERIFIED,
                        isFriend = p.IsFriend,
                        isWatched = p.IsWatched,
                        thumbnailUrl = p.ProfileImage ?? "",
                        bio = p.UserBio ?? "",
                        evaluation = p.AIEval ?? "",
                        printHistory = printHistory,
                        emojiStickerHistory = emojiStickerHistory,
                        eventHistory = eventHistory
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
