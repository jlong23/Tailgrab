using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tailgrab.MCP.Tools;

namespace Tailgrab.MCP;

/// <summary>
/// MCP Server that exposes tools via HTTP transport on port 7575 using ASP.NET Core Kestrel.
/// Accessible from the local network.
/// </summary>
public class McpServer
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private ServiceRegistry? _serviceRegistry;
    private IServiceProvider? _serviceProvider;
    private WebApplication? _webApplication;
    private CancellationTokenSource? _cancellationTokenSource;
    private Dictionary<string, Type>? _toolRegistry;

    public int Port { get; } = 7575;
    public bool IsRunning { get; private set; } = false;

    public McpServer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Initializes the MCP server with the service registry.
    /// </summary>
    public void Initialize(ServiceRegistry serviceRegistry)
    {
        _serviceRegistry = serviceRegistry;
        DiscoverTools();
    }

    /// <summary>
    /// Starts the MCP server and begins listening for HTTP requests.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(async () => await RunAsync(cancellationToken), cancellationToken);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var builder = WebApplication.CreateBuilder();

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(Port);
            });

            builder.Services.AddSingleton(_serviceRegistry ?? throw new InvalidOperationException("ServiceRegistry not initialized"));
            if (_serviceProvider != null)
            {
                builder.Services.AddSingleton(_serviceProvider);
            }

            _webApplication = builder.Build();

            _webApplication.MapPost("/", HandleJsonRpcAsync);
            _webApplication.MapPost("/rpc", HandleJsonRpcAsync);
            _webApplication.MapGet("/", HandleStatusAsync);
            _webApplication.MapGet("/tools", HandleToolsListAsync);

            Logger.Info($"Starting MCP Server on port {Port}...");
            IsRunning = true;
            Logger.Info($"MCP Server started on port {Port}, accessible from local network");
            Logger.Info($"Access via: http://<your-machine-ip>:{Port}/");

            using (_cancellationTokenSource)
            {
                await _webApplication.RunAsync();
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Info("MCP Server shutdown requested");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "MCP Server error");
        }
        finally
        {
            IsRunning = false;
            if (_webApplication != null)
            {
                await _webApplication.DisposeAsync();
            }
            _cancellationTokenSource?.Dispose();
        }
    }

    private async Task HandleStatusAsync(HttpContext context)
    {
        var response = new { status = "MCP Server running (JSON-RPC 2.0)" };
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }

    private async Task HandleToolsListAsync(HttpContext context)
    {
        try
        {
            var tools = GetAvailableToolsList();
            await context.Response.WriteAsJsonAsync(new { tools = tools.Select(t => new { t.Name, t.Description }) });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error listing tools");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
    }

    private async Task HandleJsonRpcAsync(HttpContext context)
    {
        try
        {
            using var reader = new StreamReader(context.Request.Body);
            string requestBody = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                context.Response.StatusCode = 400;
                var errorResponse = new JsonRpcResponse
                {
                    Error = JsonRpcError.InvalidRequest("Empty request body"),
                    Id = null
                };
                await RespondJsonRpcAsync(context, 400, errorResponse);
                return;
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            JsonRpcRequest? jsonRpcRequest;

            try
            {
                jsonRpcRequest = JsonSerializer.Deserialize<JsonRpcRequest>(requestBody, options);
            }
            catch (JsonException ex)
            {
                var errorResponse = new JsonRpcResponse
                {
                    Error = JsonRpcError.InvalidRequest($"Invalid JSON: {ex.Message}"),
                    Id = null
                };
                await RespondJsonRpcAsync(context, 400, errorResponse);
                return;
            }

            if (jsonRpcRequest == null || string.IsNullOrWhiteSpace(jsonRpcRequest.Method))
            {
                var errorResponse = new JsonRpcResponse
                {
                    Error = JsonRpcError.InvalidRequest("Missing method"),
                    Id = null
                };
                await RespondJsonRpcAsync(context, 400, errorResponse);
                return;
            }

            Logger.Info($"JSON-RPC Method: {jsonRpcRequest.Method}");

            JsonRpcResponse response = jsonRpcRequest.Method switch
            {
                "initialize" => await HandleInitializeAsync(jsonRpcRequest),
                "tools/list" => await HandleToolsListJsonRpcAsync(jsonRpcRequest),
                "tools/call" => await HandleToolsCallAsync(jsonRpcRequest),
                _ => new JsonRpcResponse
                {
                    Error = JsonRpcError.MethodNotFound(jsonRpcRequest.Method),
                    Id = jsonRpcRequest.Id
                }
            };

            await RespondJsonRpcAsync(context, response.Error != null ? 400 : 200, response);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling JSON-RPC request");
            var errorResponse = new JsonRpcResponse
            {
                Error = JsonRpcError.InternalError(ex.Message),
                Id = null
            };
            await RespondJsonRpcAsync(context, 500, errorResponse);
        }
    }

    private async Task<JsonRpcResponse> HandleInitializeAsync(JsonRpcRequest request)
    {
        try
        {
            var protocolVersion = "2024-11-05";
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

            var result = new InitializeResult
            {
                ProtocolVersion = protocolVersion,
                Capabilities = new ServerCapabilities
                {
                    Tools = new ToolsCapability { ListChanged = false }
                },
                ServerInfo = new ServerInfo
                {
                    Name = "Tailgrab",
                    Version = version
                }
            };

            Logger.Info($"MCP Initialize: protocol={protocolVersion}, server=Tailgrab v{version}");

            return new JsonRpcResponse
            {
                Result = result,
                Id = request.Id
            };
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling initialize");
            return new JsonRpcResponse
            {
                Error = JsonRpcError.InternalError($"Failed to initialize: {ex.Message}"),
                Id = request.Id
            };
        }
    }

    private async Task<JsonRpcResponse> HandleToolsListJsonRpcAsync(JsonRpcRequest request)
    {
        try
        {
            var tools = GetAvailableToolsList();
            var response = new ToolsListResponse { Tools = tools };

            return new JsonRpcResponse
            {
                Result = response,
                Id = request.Id
            };
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error listing tools");
            return new JsonRpcResponse
            {
                Error = JsonRpcError.InternalError($"Failed to list tools: {ex.Message}"),
                Id = request.Id
            };
        }
    }

    private async Task<JsonRpcResponse> HandleToolsCallAsync(JsonRpcRequest request)
    {
        try
        {
            if (request.Params == null)
            {
                return new JsonRpcResponse
                {
                    Error = JsonRpcError.InvalidParams("Missing params"),
                    Id = request.Id
                };
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var paramsJson = JsonSerializer.Serialize(request.Params);
            var toolParams = JsonSerializer.Deserialize<ToolCallParams>(paramsJson, options);

            if (toolParams == null || string.IsNullOrWhiteSpace(toolParams.Name))
            {
                return new JsonRpcResponse
                {
                    Error = JsonRpcError.InvalidParams("Missing tool name"),
                    Id = request.Id
                };
            }

            var result = await ExecuteToolByNameAsync(toolParams.Name, toolParams.Arguments ?? new());

            if (!result.Success)
            {
                return new JsonRpcResponse
                {
                    Error = JsonRpcError.ServerError(-32000, result.Error ?? "Tool execution failed"),
                    Id = request.Id
                };
            }

            var content = new List<ToolCallContent>
            {
                new() { Type = "text", Text = JsonSerializer.Serialize(result.Data) }
            };

            var response = new ToolCallResponse { Content = content };

            return new JsonRpcResponse
            {
                Result = response,
                Id = request.Id
            };
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error calling tool");
            return new JsonRpcResponse
            {
                Error = JsonRpcError.InternalError($"Failed to execute tool: {ex.Message}"),
                Id = request.Id
            };
        }
    }

    private async Task<McpToolResult> ExecuteToolByNameAsync(string toolName, Dictionary<string, object> arguments)
    {
        try
        {
            Logger.Info($"Executing tool: {toolName}");

            if (_toolRegistry == null || !_toolRegistry.ContainsKey(toolName))
            {
                return McpToolResult.FromError($"Tool '{toolName}' not found");
            }

            var toolType = _toolRegistry[toolName];
            var tool = (IMcpTool?)ActivatorUtilities.CreateInstance(_serviceProvider, toolType);

            if (tool == null)
            {
                return McpToolResult.FromError($"Failed to instantiate tool '{toolName}'");
            }

            return await tool.ExecuteAsync(arguments);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error executing tool: {toolName}");
            return McpToolResult.FromError(ex.Message);
        }
    }

    private List<McpToolDefinition> GetAvailableToolsList()
    {
        DiscoverTools();

        var tools = new List<McpToolDefinition>();

        if (_toolRegistry == null || _toolRegistry.Count == 0)
            return tools;

        foreach (var kvp in _toolRegistry)
        {
            try
            {
                var tool = (IMcpTool?)ActivatorUtilities.CreateInstance(_serviceProvider, kvp.Value);
                if (tool != null)
                {
                    tools.Add(new McpToolDefinition
                    {
                        Name = tool.Name,
                        Description = tool.Description,
                        InputSchema = tool.InputSchema
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to instantiate tool {kvp.Value.Name} for schema retrieval");
            }
        }

        return tools;
    }

    private void DiscoverTools()
    {
        if (_toolRegistry != null)
            return;

        _toolRegistry = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var toolNamespace = "Tailgrab.MCP.Tools";
            var assembly = Assembly.GetExecutingAssembly();

            var toolTypes = assembly.GetTypes()
                .Where(t => !t.IsInterface && !t.IsAbstract &&
                           t.Namespace == toolNamespace &&
                           typeof(IMcpTool).IsAssignableFrom(t))
                .ToList();

            Logger.Debug($"Discovered {toolTypes.Count} MCP tools in namespace '{toolNamespace}'");

            foreach (var toolType in toolTypes)
            {
                try
                {
                    var tempInstance = (IMcpTool?)ActivatorUtilities.CreateInstance(_serviceProvider, toolType);
                    if (tempInstance != null)
                    {
                        _toolRegistry[tempInstance.Name] = toolType;
                        Logger.Info($"Registered tool: {tempInstance.Name} ({toolType.Name})");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Failed to register tool {toolType.Name}");
                }
            }

            if (_toolRegistry.Count > 0)
            {
                Logger.Info($"MCP Tool Registry initialized with {_toolRegistry.Count} tools: {string.Join(", ", _toolRegistry.Keys)}");
            }
            else
            {
                Logger.Warn("No MCP tools discovered");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during tool discovery");
        }
    }

    private async Task RespondJsonRpcAsync(HttpContext context, int statusCode, JsonRpcResponse response)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        string json = JsonSerializer.Serialize(response, options);
        await context.Response.WriteAsync(json);
    }

    /// <summary>
    /// Requests graceful shutdown of the MCP server.
    /// </summary>
    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
    }
}
