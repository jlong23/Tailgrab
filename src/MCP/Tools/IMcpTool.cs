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
}
