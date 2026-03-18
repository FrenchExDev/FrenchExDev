using FrenchExDev.Net.QualityGate.Analysis;

using Microsoft.CodeAnalysis;

using Shouldly;

namespace FrenchExDev.Net.QualityGate.Tests;

public class ProjectAnalyzerTests
{
    [Fact]
    public async Task AnalyzeAsync_SimpleProject_ReturnsMetrics()
    {
        var source = @"
namespace TestNs
{
    public interface IFoo { void Bar(); }
    public class Foo : IFoo { public void Bar() {} }
    internal class Hidden { }
}";
        var project = RoslynTestHelper.CreateProject(source);
        var solution = project.Solution;

        var metrics = await ProjectAnalyzer.AnalyzeAsync(project, solution, CancellationToken.None);

        metrics.Name.ShouldBe("TestProject");
        metrics.Namespaces.Count.ShouldBeGreaterThan(0);
        metrics.Interfaces.Count.ShouldBe(1);
        metrics.Implementations.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AnalyzeAsync_NoInterfaces_OrphansEmpty()
    {
        var source = "namespace Ns { public class C { public void M() {} } }";
        var project = RoslynTestHelper.CreateProject(source);

        var metrics = await ProjectAnalyzer.AnalyzeAsync(project, project.Solution, CancellationToken.None);

        metrics.OrphanInterfaces.ShouldBeEmpty();
        metrics.Interfaces.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_OrphanInterface_Detected()
    {
        var source = "namespace Ns { public interface IOrphan { void Do(); } }";
        var project = RoslynTestHelper.CreateProject(source);

        var metrics = await ProjectAnalyzer.AnalyzeAsync(project, project.Solution, CancellationToken.None);

        metrics.Interfaces.Count.ShouldBe(1);
        metrics.Implementations.ShouldBeEmpty();
        metrics.OrphanInterfaces.Count.ShouldBe(1);
        metrics.OrphanInterfaces[0].ShouldContain("IOrphan");
    }

    [Fact]
    public async Task AnalyzeAsync_ProjectDependencies_Listed()
    {
        var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace();
        var projInfo1 = Microsoft.CodeAnalysis.ProjectInfo.Create(
            ProjectId.CreateNewId(), VersionStamp.Create(), "ProjA", "ProjA",
            LanguageNames.CSharp);
        var projInfo2 = Microsoft.CodeAnalysis.ProjectInfo.Create(
            ProjectId.CreateNewId(), VersionStamp.Create(), "ProjB", "ProjB",
            LanguageNames.CSharp)
            .WithProjectReferences([new ProjectReference(projInfo1.Id)]);

        var solution = workspace.CurrentSolution
            .AddProject(projInfo1)
            .AddProject(projInfo2);

        var projB = solution.GetProject(projInfo2.Id)!;
        var metrics = await ProjectAnalyzer.AnalyzeAsync(projB, solution, CancellationToken.None);

        metrics.Dependencies.Count.ShouldBe(1);
        metrics.Dependencies[0].FromProject.ShouldBe("ProjB");
        metrics.Dependencies[0].ToProject.ShouldBe("ProjA");
    }

    [Fact]
    public async Task AnalyzeAsync_NullCompilation_ReturnsEmptyMetrics()
    {
        // A project with no references and no documents will have null compilation
        var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace();
        var projInfo = Microsoft.CodeAnalysis.ProjectInfo.Create(
            ProjectId.CreateNewId(), VersionStamp.Create(), "Empty", "Empty",
            LanguageNames.CSharp);
        var solution = workspace.CurrentSolution.AddProject(projInfo);
        var project = solution.GetProject(projInfo.Id)!;

        // This project may return null compilation or empty compilation
        var metrics = await ProjectAnalyzer.AnalyzeAsync(project, solution, CancellationToken.None);

        metrics.Name.ShouldBe("Empty");
        metrics.Namespaces.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_NamespaceMetrics_AbstractnessComputed()
    {
        var source = @"
namespace Ns {
    public interface IFoo {}
    public enum Color { Red }
    public class Bar { public void M() {} }
}";
        var project = RoslynTestHelper.CreateProject(source);
        var metrics = await ProjectAnalyzer.AnalyzeAsync(project, project.Solution, CancellationToken.None);

        var ns = metrics.Namespaces.FirstOrDefault(n => n.Name == "Ns");
        ns.ShouldNotBeNull();
        // Interface + Enum are abstract; Bar has methods so it's concrete
        ns.AbstractTypeCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task AnalyzeAsync_ApiSurface_Populated()
    {
        var source = @"
namespace Ns {
    public class Pub {
        public void Do() {}
        public int Prop { get; set; }
    }
}";
        var project = RoslynTestHelper.CreateProject(source);
        var metrics = await ProjectAnalyzer.AnalyzeAsync(project, project.Solution, CancellationToken.None);

        metrics.PublicApi.PublicTypeCount.ShouldBe(1);
        metrics.PublicApi.PublicMethodCount.ShouldBe(1);
        metrics.PublicApi.PublicPropertyCount.ShouldBe(1);
    }
}
