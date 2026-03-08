namespace DockAi.Api.Parsing;

/// <summary>
/// Extracts plain text content from a file.
/// </summary>
public interface IDocumentParser
{
    /// <summary>Supported file extensions (lowercase, with dot).</summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>Extract text content from the given file path.</summary>
    string Parse(string filePath);
}
