using System.Diagnostics.CodeAnalysis;

using Microsoft.CodeAnalysis;

using FrenchExDev.Net.QualityGate.Abstractions;

namespace FrenchExDev.Net.QualityGate.Analysis;

[ExcludeFromCodeCoverage]
internal sealed class MsBuildSolutionLoader : ISolutionLoader
{
    public Task<Solution> LoadAsync(string solutionPath)
        => SolutionLoader.LoadAsync(solutionPath);
}
