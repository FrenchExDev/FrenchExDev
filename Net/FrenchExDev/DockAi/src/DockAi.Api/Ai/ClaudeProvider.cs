using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DockAi.Api.Ai;

/// <summary>
/// Anthropic Claude API provider.
/// </summary>
public sealed partial class ClaudeProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly AiProviderOptions _options;
    private readonly ILogger<ClaudeProvider> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public int MaxContextTokens => 200_000;

    public ClaudeProvider(HttpClient http, AiProviderOptions options, ILogger<ClaudeProvider> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;

        _http.BaseAddress = new Uri(_options.Endpoint ?? "https://api.anthropic.com");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", _options.ApiKey ?? "");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version", "2023-06-01");
    }

    public async Task<string> CompleteAsync(string prompt, string? system = null, CancellationToken ct = default)
    {
        var body = new
        {
            model = _options.Model,
            max_tokens = _options.MaxOutputTokens,
            temperature = _options.Temperature,
            system = system ?? "",
            messages = new[] { new { role = "user", content = prompt } }
        };

        _logger.LogDebug("Claude request: model={Model}, prompt length={Len}", _options.Model, prompt.Length);

        var response = await _http.PostAsJsonAsync("/v1/messages", body, JsonOpts, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Claude API error {Status}: {Body}", response.StatusCode, errorBody);
            throw new HttpRequestException($"Claude API returned {response.StatusCode}: {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<ClaudeResponse>(JsonOpts, ct);
        var text = result?.Content?.FirstOrDefault()?.Text ?? "";

        _logger.LogDebug("Claude response: {Len} chars", text.Length);
        return text;
    }

    public async Task<T?> CompleteJsonAsync<T>(string prompt, string? system = null, CancellationToken ct = default) where T : class
    {
        var text = await CompleteAsync(prompt, system, ct);
        var json = ExtractJson(text);

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOpts);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON from Claude response. Raw text: {Text}", text[..Math.Min(500, text.Length)]);
            return null;
        }
    }

    public int EstimateTokens(string text) => text.Length / 4;

    private static string ExtractJson(string text)
    {
        // Try to extract JSON from markdown code blocks
        var match = JsonCodeBlockRegex().Match(text);
        if (match.Success)
            return match.Groups[1].Value.Trim();

        // Try plain code block
        match = CodeBlockRegex().Match(text);
        if (match.Success)
            return match.Groups[1].Value.Trim();

        // Assume the whole text is JSON — find first [ or { and last ] or }
        var start = text.IndexOfAny(['{', '[']);
        var end = text.LastIndexOfAny(['}', ']']);
        if (start >= 0 && end > start)
            return text[start..(end + 1)];

        return text.Trim();
    }

    [GeneratedRegex(@"```json\s*\n([\s\S]*?)\n\s*```", RegexOptions.Compiled)]
    private static partial Regex JsonCodeBlockRegex();

    [GeneratedRegex(@"```\s*\n([\s\S]*?)\n\s*```", RegexOptions.Compiled)]
    private static partial Regex CodeBlockRegex();

    // Claude API response models
    private sealed record ClaudeResponse(List<ContentBlock>? Content);
    private sealed record ContentBlock(string? Text, string? Type);
}
