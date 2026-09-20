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
            print_id = p.PrintId,
            display_name = p.AuthorName,
            author_id = p.OwnerId,
            evaluation = p.AIEvaluation,
            print_uri = p.PrintUrl
        }).ToList();

        List<PlayerInventory> inventoryData = p.Inventory?.ToList() ?? new List<PlayerInventory>();
        var emojiStickerHistory = inventoryData.Select(i => new
        {
            inventory_id = i.InventoryId,
            inventory_type = i.InventoryType,
            evaluation = i.AIEvaluation,
            image_uri = i.ItemUrl
        }).ToList();

        List<PlayerEvent> eventData = p.Events?.ToList() ?? new List<PlayerEvent>();
        var eventHistory = eventData.OrderBy(e => e.EventTime).Select(e => new
        {
            event_type = e.Type.ToString(),
            event_time = e.EventTime,
            event_description = e.EventDescription
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
                ttlMs = 5000,
                cacheScope = "private",
                display_name = p.DisplayName,
                user_id = p.UserId,
                current_avatar_name = p.AvatarName ?? "",
                current_avatar_id = p.AvatarId ?? "",
                duration_seconds = (DateTime.Now - p.InstanceStartTime).TotalSeconds,
                account_age_in_days = elapsed.TotalDays,
                verified = ConvertBooleanToYesNo(p.AgeVerified != AgeVerificationEnum.UNVERIFIED),
                friend = ConvertBooleanToYesNo(p.IsFriend),
                watched = ConvertBooleanToYesNo(p.IsWatched),
                thumbnail_url = p.ProfileImage ?? "",
                bio = p.UserBio ?? "",
                evaluation = p.AIEval ?? "",
                print_history = printHistory,
                emoji_sticker_history = emojiStickerHistory,
                event_history = eventHistory
            }
        };

        return McpToolResult.FromSuccess(info);
    }

    protected string ConvertBooleanToYesNo(bool? value)
    {
        return value == true ? "yes" : "no";
    }
}
