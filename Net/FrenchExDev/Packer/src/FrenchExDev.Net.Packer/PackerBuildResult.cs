using FrenchExDev.Net.BinaryWrapper;

namespace FrenchExDev.Net.Packer;

public sealed record PackerArtifact(string BuilderType, string BuildName, string? ArtifactId);

public sealed record PackerBuildResult(
    IReadOnlyList<PackerArtifact> Artifacts,
    IReadOnlyList<string> Errors,
    bool Success);

/// <summary>
/// Collects <see cref="PackerEvent"/> instances from a build run
/// and produces an aggregated <see cref="PackerBuildResult"/>.
/// </summary>
public sealed class PackerBuildCollector : IResultCollector<PackerEvent, PackerBuildResult>
{
    private readonly List<PackerArtifact> _artifacts = [];
    private readonly List<string> _errors = [];
    private bool _hasFailure;

    public void OnEvent(PackerEvent @event)
    {
        switch (@event)
        {
            case PackerBuildFinished { Success: true, ArtifactId: not null } finished:
                _artifacts.Add(new PackerArtifact("unknown", finished.BuildName, finished.ArtifactId));
                break;

            case PackerBuildFinished { Success: false } finished:
                _hasFailure = true;
                break;

            case PackerBuildError error:
                _errors.Add($"{error.BuildName}: {error.Message}");
                break;
        }
    }

    public PackerBuildResult Complete() =>
        new(_artifacts.AsReadOnly(), _errors.AsReadOnly(), !_hasFailure && _errors.Count == 0);
}
