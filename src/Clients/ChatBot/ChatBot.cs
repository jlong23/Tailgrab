using NLog;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Tailgrab.MCP.Services;

namespace Tailgrab.Clients.ChatBot
{
    internal class ChatBot
    {
        protected readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly HttpClient _http;
        private readonly IToolExecutor? _toolExecutor;

        public ChatBot()
        {
            _http = new HttpClient();
            _http.Timeout = TimeSpan.FromMinutes(2);
        }

        public ChatBot(IToolExecutor toolExecutor)
        {
            _http = new HttpClient();
            _http.Timeout = TimeSpan.FromMinutes(2);
            _toolExecutor = toolExecutor;
        }

        // ─── Tool definition ───────────────────────────────────────────────
        public record ToolDefinition(
            string Name,
            string Description,
            string ParametersJsonSchema  // JSON Schema string
        );

        // ─── 1. AskLLMAsync with embedded tool calling ─────────────────────
        /// <summary>
        /// Calls the LLM with optional local tools. Implements the full
        /// tool-calling loop: LLM → tool_call → execute locally → feed back → repeat.
        /// </summary>
        /// <param name="endpoint">llama.cpp /v1/chat/completions URL</param>
        /// <param name="model">Model name (e.g. "local-model")</param>
        /// <param name="text">User question</param>
        /// <param name="tools">Local C# methods exposed as tools to the LLM</param>
        public async Task<string> AskLLMAsync(
            string endpoint,
            string model,
            string text,
            IReadOnlyList<ToolDefinition> tools = null)
        {
            // Build the message list (conversation history)
            var messages = new List<Dictionary<string, object>>
            {
                new() { ["role"] = "user", ["content"] = text }
            };

            // Build the tools array for the request (OpenAI format)
            List<Dictionary<string, object>> toolsArray = null;
            if (tools != null && tools.Count > 0)
            {
                toolsArray = tools.Select(t => new Dictionary<string, object>
                {
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object>
                    {
                        ["name"] = t.Name,
                        ["description"] = t.Description,
                        ["parameters"] = JsonSerializer.Deserialize<JsonElement>(t.ParametersJsonSchema)
                    }
                }).ToList();
            }

            // ─── Agentic loop ──────────────────────────────────────────────
            int maxIterations = 10; // safety limit
            for (int i = 0; i < maxIterations; i++)
            {
                var payload = new Dictionary<string, object>
                {
                    ["model"] = model,
                    ["messages"] = messages
                };
                if (toolsArray != null)
                    payload["tools"] = toolsArray;

                string json = JsonSerializer.Serialize(payload);
                var resp = await _http.PostAsync(
                    endpoint,
                    new StringContent(json, Encoding.UTF8, "application/json"));

                resp.EnsureSuccessStatusCode();
                var body = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(body);

                var choice = doc.RootElement.GetProperty("choices")[0];
                var message = choice.GetProperty("message");

                // ── Check if the LLM wants to call a tool ──────────────────
                if (message.TryGetProperty("tool_calls", out JsonElement toolCalls))
                {
                    // Append the assistant's tool-call message to history
                    messages.Add(new Dictionary<string, object>
                    {
                        ["role"] = "assistant",
                        ["tool_calls"] = toolCalls.Clone()
                    });

                    // Execute each tool call locally
                    foreach (var tc in toolCalls.EnumerateArray())
                    {
                        string toolName = tc.GetProperty("function").GetProperty("name").GetString()!;
                        string argsJson = tc.GetProperty("function").GetProperty("arguments").GetString()!;
                        string toolCallId = tc.GetProperty("id").GetString()!;

                        // Dispatch to the registered C# method
                        string result = await ExecuteToolAsync(tools, toolName, argsJson);

                        // Append the tool result to history
                        messages.Add(new Dictionary<string, object>
                        {
                            ["role"] = "tool",
                            ["tool_call_id"] = toolCallId,
                            ["content"] = result
                        });
                    }

                    // Loop back → LLM sees the tool results and continues
                    continue;
                }

                // ── No tool call → this is the final answer ───────────────
                return message.GetProperty("content").GetString() ?? "";
            }

            throw new InvalidOperationException("Tool-calling loop exceeded max iterations.");
        }

        // ─── Tool dispatcher ───────────────────────────────────────────────
        private async Task<string> ExecuteToolAsync(
            IReadOnlyList<ToolDefinition> tools,
            string toolName,
            string argsJson)
        {
            if (_toolExecutor != null)
            {
                try
                {
                    var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argsJson) 
                        ?? new Dictionary<string, JsonElement>();

                    var arguments = args.ToDictionary(
                        kvp => kvp.Key,
                        kvp => ConvertJsonElementToObject(kvp.Value));

                    var result = await _toolExecutor.ExecuteToolAsync(toolName, arguments);

                    if (result.Success)
                    {
                        return JsonSerializer.Serialize(new { data = result.Data });
                    }
                    else
                    {
                        return JsonSerializer.Serialize(new { error = result.Error });
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Error executing tool via IToolExecutor: {toolName}");
                    return JsonSerializer.Serialize(new { error = $"Tool execution failed: {ex.Message}" });
                }
            }

            return ExecuteToolLocalAsync(toolName, argsJson);
        }

        private string ExecuteToolLocalAsync(string toolName, string argsJson)
        {
            switch (toolName)
            {
                case "get_current_time":
                    return JsonSerializer.Serialize(new { time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });

                case "calculate":
                    {
                        var args = JsonSerializer.Deserialize<JsonElement>(argsJson);
                        string expr = args.GetProperty("expression").GetString()!;
                        return JsonSerializer.Serialize(new { result = expr });
                    }

                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown tool: {toolName}" });
            }
        }

        private static object ConvertJsonElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.TryGetInt32(out var intVal) ? (object)intVal : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null!,
                JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElementToObject).ToList(),
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElementToObject(p.Value)),
                _ => element.ToString()
            };
        }
    }
}
