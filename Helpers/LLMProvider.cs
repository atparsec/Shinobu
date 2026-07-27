using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Shinobu.Helpers;

public class LLMProvider
{
    private readonly string _baseUrl;
    private readonly string? _apiKey;
    private readonly List<string> _chatContext = [];
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    public LLMProvider(string url, string? apiKey = null)
    {
        _baseUrl = url.TrimEnd('/');
        _apiKey = apiKey;
    }

    public async Task<List<string>> GetModelsAsync()
    {
        var tagsResponse = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
        tagsResponse.EnsureSuccessStatusCode();

        var tagsContent = await tagsResponse.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(tagsContent);

        var modelList = new List<string>();
        foreach (var modelElement in jsonDoc.RootElement.GetProperty("models").EnumerateArray())
        {
            modelList.Add(modelElement.GetProperty("name").GetString() ?? string.Empty);
        }

        return modelList;
    }

    public async IAsyncEnumerable<string> GetCompletionStreamAsync(string prompt, string model)
    {
        var fullPrompt = _chatContext.Count > 0 ? $"{string.Join("\n", _chatContext)}\n{prompt}" : prompt;

        var requestBody = new
        {
            model = model,
            prompt = fullPrompt,
            stream = true
        };

        var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/generate")
        {
            Content = requestContent
        };

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new System.IO.StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            using var responseDoc = JsonDocument.Parse(line);
            if (responseDoc.RootElement.TryGetProperty("response", out var responseProperty))
            {
                var chunk = responseProperty.GetString();
                if (!string.IsNullOrEmpty(chunk))
                {
                    yield return chunk;
                }
            }
        }
    }

    public async Task<string> GetCompletionAsync(string prompt, string model)
    {
        var fullPrompt = _chatContext.Count > 0 ? $"{string.Join("\n", _chatContext)}\n{prompt}" : prompt;

        var requestBody = new
        {
            model = model,
            prompt = fullPrompt,
            stream = false
        };

        var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/generate")
        {
            Content = requestContent
        };

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        var response = await _httpClient.SendAsync(requestMessage);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        using var responseDoc = JsonDocument.Parse(responseContent);
        if (responseDoc.RootElement.TryGetProperty("response", out var responseProperty))
        {
            return responseProperty.GetString() ?? string.Empty;
        }

        return responseContent;
    }

    public void AddChatContext(string context)
    {
        _chatContext.Add(context);
    }
    public void ClearChatContext()
    {
        _chatContext.Clear();
    }
}