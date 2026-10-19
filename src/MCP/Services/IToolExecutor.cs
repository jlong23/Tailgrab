using Tailgrab.MCP.Tools;

namespace Tailgrab.MCP.Services;

/// <summary>
/// Service for executing MCP tools by name with argument resolution.
/// </summary>
public interface IToolExecutor
{
    /// <summary>
    /// Executes a registered MCP tool by name with the provided arguments.
    /// </summary>
    /// <param name="toolName">Name of the tool to execute</param>
    /// <param name="arguments">Dictionary of tool arguments</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of tool execution</returns>
    Task<McpToolResult> ExecuteToolAsync(string toolName, Dictionary<string, object> arguments, CancellationToken cancellationToken = default);
}
