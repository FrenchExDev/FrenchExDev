namespace DockAi.Viewer.Rendering;

/// <summary>
/// Converts a document file into viewable HTML.
/// Unlike DockAi.Api's IDocumentParser (which extracts plain text for indexing),
/// this produces formatted HTML with structure preservation.
/// </summary>
public interface IDocumentRenderer
{
    IReadOnlyList<string> SupportedExtensions { get; }
    RenderResult Render(string filePath);
}

public sealed class RenderResult
{
    /// <summary>The HTML content to display in the viewer.</summary>
    public required string Html { get; init; }

    /// <summary>Hint for the frontend: "html", "pdf-embed", "iframe".</summary>
    public required string RenderMode { get; init; }

    /// <summary>Optional metadata (sheet names, warnings, etc.).</summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}
