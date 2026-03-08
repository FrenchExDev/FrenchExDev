namespace DockAi.Api.Ai;

/// <summary>
/// Configuration for the AI provider.
/// </summary>
public sealed class AiProviderOptions
{
    /// <summary>"claude", "openai", or "ollama".</summary>
    public string Provider { get; set; } = "claude";

    public string Model { get; set; } = "claude-sonnet-4-5-20250514";
    public string? ApiKey { get; set; }

    /// <summary>Custom endpoint URL (for Ollama or OpenAI-compatible).</summary>
    public string? Endpoint { get; set; }

    public float Temperature { get; set; } = 0.3f;
    public int MaxOutputTokens { get; set; } = 8192;
}
