namespace FrenchExDev.Net.Vos.Abstractions.IO;

/// <summary>
/// Abstraction over a single file. Enables in-memory fakes for testing.
/// </summary>
public interface IFile
{
    string Name { get; }
    string Extension { get; }
    string FullPath { get; }
    bool Exists { get; }
    Task<string> ReadContentAsync(CancellationToken ct = default);
    Task WriteContentAsync(string content, CancellationToken ct = default);
}
