using System.Text.Json.Serialization;

namespace Tailgrab.MCP;

/// <summary>
/// JSON-RPC 2.0 request envelope.
/// </summary>
public class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("method")]
    public required string Method { get; set; }

    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Params { get; set; }

    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Id { get; set; }
}

/// <summary>
/// JSON-RPC 2.0 response envelope.
/// </summary>
public class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; set; }

    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Id { get; set; }
}

/// <summary>
/// JSON-RPC 2.0 error object.
/// </summary>
public class JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }

    public static JsonRpcError InvalidRequest(string message = "Invalid Request") =>
        new() { Code = -32600, Message = message };

    public static JsonRpcError MethodNotFound(string method) =>
        new() { Code = -32601, Message = $"Method not found: {method}" };

    public static JsonRpcError InvalidParams(string message) =>
        new() { Code = -32602, Message = message };

    public static JsonRpcError InternalError(string message) =>
        new() { Code = -32603, Message = message };

    public static JsonRpcError ServerError(int code, string message) =>
        new() { Code = code, Message = message };
}

/// <summary>
/// MCP Tool definition in JSON-RPC format.
/// </summary>
public class McpToolDefinition
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public required string Description { get; set; }

    [JsonPropertyName("inputSchema")]
    public required object InputSchema { get; set; }
}

/// <summary>
/// Parameters for tools/call method.
/// </summary>
public class ToolCallParams
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("arguments")]
    public Dictionary<string, object>? Arguments { get; set; }
}

/// <summary>
/// Response for tools/list method.
/// </summary>
public class ToolsListResponse
{
    [JsonPropertyName("tools")]
    public required List<McpToolDefinition> Tools { get; set; }
}

/// <summary>
/// Response for tools/call method.
/// </summary>
public class ToolCallResponse
{
    [JsonPropertyName("content")]
    public required List<ToolCallContent> Content { get; set; }
}

/// <summary>
/// Content item in tool call response.
/// </summary>
public class ToolCallContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public required string Text { get; set; }
}

/// <summary>
/// Parameters for initialize method.
/// </summary>
public class InitializeParams
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = "2024-11-05";

    [JsonPropertyName("capabilities")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Capabilities { get; set; }

    [JsonPropertyName("clientInfo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ClientInfo? ClientInfo { get; set; }
}

/// <summary>
/// Server initialization result.
/// </summary>
public class InitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public required string ProtocolVersion { get; set; }

    [JsonPropertyName("capabilities")]
    public required ServerCapabilities Capabilities { get; set; }

    [JsonPropertyName("serverInfo")]
    public required ServerInfo ServerInfo { get; set; }
}

/// <summary>
/// Server capabilities.
/// </summary>
public class ServerCapabilities
{
    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ToolsCapability? Tools { get; set; }
}

/// <summary>
/// Tools capability.
/// </summary>
public class ToolsCapability
{
    [JsonPropertyName("listChanged")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ListChanged { get; set; } = false;
}

/// <summary>
/// Server information.
/// </summary>
public class ServerInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("version")]
    public required string Version { get; set; }
}

/// <summary>
/// Client information.
/// </summary>
public class ClientInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}
