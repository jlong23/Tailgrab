using Tailgrab.Common;
using Tailgrab.PlayerManagement;

namespace Tailgrab.MCP.Tools;

/// <summary>
/// Base interface for all MCP tools.
/// </summary>
public interface IMcpTool
{
    /// <summary>
    /// Unique name identifier for this tool.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description of what this tool does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// JSON Schema for tool input parameters (MCP standard format).
    /// </summary>
    object InputSchema { get; }

    /// <summary>
    /// Executes the tool with the provided arguments.
    /// </summary>
    Task<McpToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result returned from tool execution.
/// </summary>
public class McpToolResult
{
    public bool Success { get; set; }
    public object? Data { get; set; }
    public string? Error { get; set; }

    public static McpToolResult FromSuccess(object? data = null) => new() { Success = true, Data = data };
    public static McpToolResult FromError(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// Base class for MCP tools with ServiceRegistry access.
/// </summary>
public abstract class McpToolBase : IMcpTool
{
    protected readonly ServiceRegistry ServiceRegistry;

    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract object InputSchema { get; }

    protected McpToolBase(ServiceRegistry serviceRegistry)
    {
        ServiceRegistry = serviceRegistry;
    }

    public abstract Task<McpToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken = default);

    protected McpToolResult FormatPlayerCurrentInfo(Player? p)
    {
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
}
