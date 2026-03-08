namespace DockAi.Api.Ai;

/// <summary>
/// Abstraction for LLM providers (Claude, OpenAI, Ollama).
/// </summary>
public interface IAiProvider
{
    /// <summary>Send a prompt with optional system message, get text back.</summary>
    Task<string> CompleteAsync(string prompt, string? system = null, CancellationToken ct = default);

    /// <summary>Send a prompt, get structured JSON back (parsed to T).</summary>
    Task<T?> CompleteJsonAsync<T>(string prompt, string? system = null, CancellationToken ct = default) where T : class;

    /// <summary>Rough token estimate for a text (for chunking decisions).</summary>
    int EstimateTokens(string text);

    /// <summary>Max context window size in tokens.</summary>
    int MaxContextTokens { get; }
}
