using FrenchExDev.Net.QualityGate.Analysis;

using Microsoft.CodeAnalysis;

using Shouldly;

namespace FrenchExDev.Net.QualityGate.Tests;

public class DependencyGraphBuilderTests
{
    [Fact]
    public void BuildProjectDependencies_NullSolution_Throws()
    {
        Should.Throw<ArgumentNullException>(
            () => DependencyGraphBuilder.BuildProjectDependencies(null!));
    }

    [Fact]
    public void BuildProjectDependencies_EmptySolution_ReturnsEmptyList()
    {
        using var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;

        var deps = DependencyGraphBuilder.BuildProjectDependencies(solution);

        deps.ShouldBeEmpty();
    }

    [Fact]
    public void BuildProjectDependencies_SingleProjectNoRefs_ReturnsEmpty()
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(projectId, VersionStamp.Default, "ProjectA", "ProjectA", LanguageNames.CSharp);
        var solution = workspace.CurrentSolution.AddProject(projectInfo);

        var deps = DependencyGraphBuilder.BuildProjectDependencies(solution);

        deps.ShouldBeEmpty();
    }

    [Fact]
    public void BuildProjectDependencies_WithProjectReference_ReturnsDependency()
    {
        using var workspace = new AdhocWorkspace();

        var projectAId = ProjectId.CreateNewId();
        var projectBId = ProjectId.CreateNewId();

        var projectAInfo = ProjectInfo.Create(projectAId, VersionStamp.Default, "ProjectA", "ProjectA", LanguageNames.CSharp);
        var projectBInfo = ProjectInfo.Create(projectBId, VersionStamp.Default, "ProjectB", "ProjectB", LanguageNames.CSharp);

        var solution = workspace.CurrentSolution
            .AddProject(projectAInfo)
            .AddProject(projectBInfo)
            .AddProjectReference(projectAId, new ProjectReference(projectBId));

        var deps = DependencyGraphBuilder.BuildProjectDependencies(solution);

        deps.Count.ShouldBe(1);
        deps[0].FromProject.ShouldBe("ProjectA");
        deps[0].ToProject.ShouldBe("ProjectB");
    }

    [Fact]
    public void BuildProjectDependencies_MultipleReferences_ReturnsAll()
    {
        using var workspace = new AdhocWorkspace();

        var idA = ProjectId.CreateNewId();
        var idB = ProjectId.CreateNewId();
        var idC = ProjectId.CreateNewId();

        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(idA, VersionStamp.Default, "A", "A", LanguageNames.CSharp))
            .AddProject(ProjectInfo.Create(idB, VersionStamp.Default, "B", "B", LanguageNames.CSharp))
            .AddProject(ProjectInfo.Create(idC, VersionStamp.Default, "C", "C", LanguageNames.CSharp))
            .AddProjectReference(idA, new ProjectReference(idB))
            .AddProjectReference(idA, new ProjectReference(idC));

        var deps = DependencyGraphBuilder.BuildProjectDependencies(solution);

        deps.Count.ShouldBe(2);
        deps.ShouldAllBe(d => d.FromProject == "A");
    }
}
