using Microsoft.CodeAnalysis;

using FrenchExDev.Net.QualityGate.Abstractions;

namespace FrenchExDev.Net.QualityGate.Tests.Fakes;

internal sealed class FakeSolutionLoader : ISolutionLoader
{
    private readonly Solution _solution;

    public FakeSolutionLoader(Solution solution) => _solution = solution;

    public Task<Solution> LoadAsync(string solutionPath) => Task.FromResult(_solution);

    public static FakeSolutionLoader Empty()
    {
        var workspace = new AdhocWorkspace();
        return new FakeSolutionLoader(workspace.CurrentSolution);
    }

    public static FakeSolutionLoader WithSource(string source)
    {
        var project = RoslynTestHelper.CreateProject(source);
        return new FakeSolutionLoader(project.Solution);
    }
}
