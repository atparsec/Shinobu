using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Shinobu.Helpers.Services;

public sealed record LlmChatMessage(
    string Role,
    string Content,
    string? ToolCallId = null,
    IReadOnlyList<LlmToolCall>? ToolCalls = null);

public sealed record LlmToolCall(string Id, string Name, string ArgumentsJson);

public sealed record LlmChatResult(string Content, IReadOnlyList<LlmToolCall> ToolCalls);

public class LLMProvider
{
    private readonly string _baseUrl;
    private readonly string? _apiKey;
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    public LLMProvider(string url, string? apiKey = null)
    {
        _baseUrl = NormalizeBaseUrl(url);
        _apiKey = apiKey;
    }

    private static string NormalizeBaseUrl(string url)
    {
        string trimmed = url.Trim().TrimEnd('/');

        if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^3];
        }

        return trimmed;
    }

    private string ApiRoot => $"{_baseUrl}/v1";

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath)
    {
        var request = new HttpRequestMessage(method, $"{ApiRoot}/{relativePath.TrimStart('/')}");

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        return request;
    }

    public async Task<List<string>> GetModelsAsync()
    {
        using var response = await _httpClient.SendAsync(CreateRequest(HttpMethod.Get, "models"));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);

        var modelList = new List<string>();
        if (jsonDoc.RootElement.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var modelElement in dataElement.EnumerateArray())
            {
                if (modelElement.TryGetProperty("id", out var idElement))
                {
                    modelList.Add(idElement.GetString() ?? string.Empty);
                }
                else if (modelElement.TryGetProperty("name", out var nameElement))
                {
                    modelList.Add(nameElement.GetString() ?? string.Empty);
                }
            }
        }

        return modelList;
    }

    public async Task<LlmChatResult> GetChatCompletionAsync(
        IReadOnlyList<LlmChatMessage> messages,
        string model,
        IReadOnlyList<object>? tools = null)
    {
        var requestBody = new
        {
            model,
            messages = messages.Select(message => new
            {
                role = message.Role,
                content = message.Content,
                tool_call_id = message.ToolCallId,
                tool_calls = message.ToolCalls is { Count: > 0 }
                    ? message.ToolCalls.Select(toolCall => new
                    {
                        id = toolCall.Id,
                        type = "function",
                        function = new
                        {
                            name = toolCall.Name,
                            arguments = toolCall.ArgumentsJson
                        }
                    })
                    : null
            }),
            stream = false,
            tools = tools is { Count: > 0 } ? tools : null,
            tool_choice = tools is { Count: > 0 } ? "auto" : null,
            thinking = !new Uri(_baseUrl).IsDefaultPort
        };

        var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        using var requestMessage = CreateRequest(HttpMethod.Post, "chat/completions");
        requestMessage.Content = requestContent;

        using var response = await _httpClient.SendAsync(requestMessage);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        using var responseDoc = JsonDocument.Parse(responseContent);

        var choice = responseDoc.RootElement.GetProperty("choices")[0].GetProperty("message");

        string content = string.Empty;
        if (choice.TryGetProperty("content", out var contentElement) && contentElement.ValueKind != JsonValueKind.Null)
        {
            content = contentElement.GetString() ?? string.Empty;
        }

        var toolCalls = new List<LlmToolCall>();
        if (choice.TryGetProperty("tool_calls", out var toolCallsElement) && toolCallsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var toolCallElement in toolCallsElement.EnumerateArray())
            {
                string id = toolCallElement.TryGetProperty("id", out var idElement)
                    ? idElement.GetString() ?? string.Empty
                    : string.Empty;

                if (!toolCallElement.TryGetProperty("function", out var functionElement))
                {
                    continue;
                }

                string name = functionElement.TryGetProperty("name", out var nameElement)
                    ? nameElement.GetString() ?? string.Empty
                    : string.Empty;

                string arguments = functionElement.TryGetProperty("arguments", out var argsElement)
                    ? argsElement.GetString() ?? string.Empty
                    : string.Empty;

                if (!string.IsNullOrWhiteSpace(name))
                {
                    toolCalls.Add(new LlmToolCall(id, name, arguments));
                }
            }
        }

        return new LlmChatResult(content, toolCalls);
    }

    public static object CreateWebSearchTool() => new
    {
        type = "function",
        function = new
        {
            name = "web_search",
            description = "Search the web for current information and return a few concise results.",
            parameters = new
            {
                type = "object",
                properties = new
                {
                    query = new
                    {
                        type = "string",
                        description = "The search query to look up on the web."
                    }
                },
                required = new[] { "query" },
                additionalProperties = false
            }
        }
    };

    public static object CreateCurrentPageSearchTool() => new
    {
        type = "function",
        function = new
        {
            name = "current_page_search",
            description = "Search the currently selected reader page text for matching terms or phrases.",
            parameters = new
            {
                type = "object",
                properties = new
                {
                    query = new
                    {
                        type = "string",
                        description = "The term or phrase to search for on the current page."
                    }
                },
                required = new[] { "query" },
                additionalProperties = false
            }
        }
    };
}

