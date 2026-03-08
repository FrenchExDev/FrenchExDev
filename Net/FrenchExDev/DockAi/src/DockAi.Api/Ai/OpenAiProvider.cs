using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DockAi.Api.Ai;

/// <summary>
/// OpenAI-compatible API provider (works with OpenAI, Azure OpenAI, and compatible APIs).
/// </summary>
public sealed partial class OpenAiProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly AiProviderOptions _options;
    private readonly ILogger<OpenAiProvider> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public int MaxContextTokens => _options.Model switch
    {
        var m when m.Contains("gpt-4o") => 128_000,
        var m when m.Contains("gpt-4-turbo") => 128_000,
        var m when m.Contains("gpt-4") => 8_192,
        _ => 128_000
    };

    public OpenAiProvider(HttpClient http, AiProviderOptions options, ILogger<OpenAiProvider> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;

        _http.BaseAddress = new Uri(_options.Endpoint ?? "https://api.openai.com");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {_options.ApiKey ?? ""}");
    }

    public async Task<string> CompleteAsync(string prompt, string? system = null, CancellationToken ct = default)
    {
        var messages = new List<object>();
        if (!string.IsNullOrEmpty(system))
            messages.Add(new { role = "system", content = system });
        messages.Add(new { role = "user", content = prompt });

        var body = new
        {
            model = _options.Model,
            max_tokens = _options.MaxOutputTokens,
            temperature = _options.Temperature,
            messages
        };

        _logger.LogDebug("OpenAI request: model={Model}, prompt length={Len}", _options.Model, prompt.Length);

        var response = await _http.PostAsJsonAsync("/v1/chat/completions", body, JsonOpts, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("OpenAI API error {Status}: {Body}", response.StatusCode, errorBody);
            throw new HttpRequestException($"OpenAI API returned {response.StatusCode}: {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<OpenAiResponse>(JsonOpts, ct);
        return result?.Choices?.FirstOrDefault()?.Message?.Content ?? "";
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
            _logger.LogWarning(ex, "Failed to parse JSON from OpenAI response");
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

    private sealed record OpenAiResponse(List<Choice>? Choices);
    private sealed record Choice(Message? Message);
    private sealed record Message(string? Content, string? Role);
}
