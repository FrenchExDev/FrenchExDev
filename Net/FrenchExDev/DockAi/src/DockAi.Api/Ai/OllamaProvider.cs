using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DockAi.Api.Ai;

/// <summary>
/// Local Ollama API provider.
/// </summary>
public sealed partial class OllamaProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly AiProviderOptions _options;
    private readonly ILogger<OllamaProvider> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public int MaxContextTokens => 32_000; // varies by model, conservative default

    public OllamaProvider(HttpClient http, AiProviderOptions options, ILogger<OllamaProvider> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;

        _http.BaseAddress = new Uri(_options.Endpoint ?? "http://localhost:11434");
    }

    public async Task<string> CompleteAsync(string prompt, string? system = null, CancellationToken ct = default)
    {
        var body = new
        {
            model = _options.Model,
            prompt,
            system = system ?? "",
            stream = false,
            options = new { temperature = _options.Temperature }
        };

        _logger.LogDebug("Ollama request: model={Model}, prompt length={Len}", _options.Model, prompt.Length);

        var response = await _http.PostAsJsonAsync("/api/generate", body, JsonOpts, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Ollama API error {Status}: {Body}", response.StatusCode, errorBody);
            throw new HttpRequestException($"Ollama API returned {response.StatusCode}: {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(JsonOpts, ct);
        return result?.Response ?? "";
    }

    public async Task<T?> CompleteJsonAsync<T>(string prompt, string? system = null, CancellationToken ct = default) where T : class
    {
        var text = await CompleteAsync(prompt, system, ct);
        var json = ExtractJson(text);

        try
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON from Ollama response");
            return null;
        }
    }

    public int EstimateTokens(string text) => text.Length / 4;

    private static string ExtractJson(string text)
    {
        var match = JsonBlockRegex().Match(text);
        if (match.Success) return match.Groups[1].Value.Trim();

        var start = text.IndexOfAny(['{', '[']);
        var end = text.LastIndexOfAny(['}', ']']);
        if (start >= 0 && end > start) return text[start..(end + 1)];

        return text.Trim();
    }

    [GeneratedRegex(@"```(?:json)?\s*\n([\s\S]*?)\n\s*```", RegexOptions.Compiled)]
    private static partial Regex JsonBlockRegex();

    private sealed record OllamaResponse(string? Response, bool? Done);
}
