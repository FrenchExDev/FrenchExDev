using System.Diagnostics.CodeAnalysis;

using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace FrenchExDev.Net.QualityGate.Analysis;

/// <summary>
/// Loads a .NET solution into a Roslyn <see cref="Solution"/> via MSBuildWorkspace.
/// </summary>
[ExcludeFromCodeCoverage]
public static class SolutionLoader
{
    private static readonly object s_lock = new();

    /// <summary>
    /// Opens the solution at <paramref name="solutionPath"/> and returns the Roslyn
    /// <see cref="Solution"/> snapshot.  MSBuild is registered on first call.
    /// </summary>
    public static async Task<Solution> LoadAsync(string solutionPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);

        // MSBuildLocator.RegisterDefaults() must be called exactly once.
        lock (s_lock)
        {
            if (!MSBuildLocator.IsRegistered)
                MSBuildLocator.RegisterDefaults();
        }

        var workspace = MSBuildWorkspace.Create();

        // Surface diagnostics without throwing so partial loads still succeed.
#pragma warning disable CS0618 // WorkspaceFailed is obsolete — RegisterWorkspaceFailedHandler not available in all versions
        workspace.WorkspaceFailed += (_, e) =>
        {
            System.Diagnostics.Trace.TraceWarning(
                $"MSBuildWorkspace failure: {e.Diagnostic.Message}");
        };
#pragma warning restore CS0618

        var solution = await workspace.OpenSolutionAsync(solutionPath).ConfigureAwait(false);
        return solution;
    }
}
