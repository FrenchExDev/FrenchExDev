using Microsoft.CodeAnalysis;

using FrenchExDev.Net.QualityGate.Model;

namespace FrenchExDev.Net.QualityGate.Analysis;

/// <summary>
/// Analyzes a single Roslyn <see cref="Project"/> and produces <see cref="ProjectMetrics"/>.
/// </summary>
internal static class ProjectAnalyzer
{
    public static async Task<ProjectMetrics> AnalyzeAsync(Project project, Solution solution, CancellationToken ct)
    {
        var compilation = await project.GetCompilationAsync(ct).ConfigureAwait(false);
        if (compilation is null)
            return EmptyMetrics(project);

        var (interfaces, implementations) = await InterfaceAnalyzer.AnalyzeAsync(project).ConfigureAwait(false);
        var orphanInterfaces = FindOrphans(interfaces, implementations);
        var apiSurface = await ApiSurfaceAnalyzer.AnalyzeAsync(project).ConfigureAwait(false);
        var namespaceCoupling = await CouplingAnalyzer.AnalyzeNamespaceCouplingAsync(project).ConfigureAwait(false);
        var typeMetricsByNamespace = await TypeMetricsBuilder.BuildAsync(compilation, ct).ConfigureAwait(false);

        var namespaces = BuildNamespaceMetrics(typeMetricsByNamespace, namespaceCoupling);
        var dependencies = BuildDependencies(project, solution);

        return new ProjectMetrics
        {
            Name = project.Name,
            FilePath = project.FilePath ?? "",
            Namespaces = namespaces,
            Interfaces = interfaces,
            Implementations = implementations,
            OrphanInterfaces = orphanInterfaces,
            Dependencies = dependencies,
            PublicApi = apiSurface
        };
    }

    private static List<string> FindOrphans(List<InterfaceInfo> interfaces, List<InterfaceImplementation> implementations)
    {
        var implemented = new HashSet<string>(
            implementations.Select(i => i.InterfaceFullName),
            StringComparer.Ordinal);

        return interfaces
            .Where(i => !implemented.Contains(i.FullName))
            .Select(i => i.FullName)
            .ToList();
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage] // Coverlet tracks ternary TryGetValue as uncovered branch
    private static List<NamespaceMetrics> BuildNamespaceMetrics(
        Dictionary<string, List<TypeMetrics>> typeMetricsByNamespace,
        Dictionary<string, (int Ca, int Ce)> namespaceCoupling)
    {
        var result = new List<NamespaceMetrics>();

        foreach (var (nsName, types) in typeMetricsByNamespace)
        {
            var (ca, ce) = namespaceCoupling.TryGetValue(nsName, out var coupling) ? coupling : (0, 0);

            var abstractCount = types.Count(t =>
                t.Kind == Model.TypeKind.Interface
                || t.Kind == Model.TypeKind.Enum
                || t.Methods.Count == 0);

            result.Add(new NamespaceMetrics
            {
                Name = nsName,
                TypeCount = types.Count,
                AbstractTypeCount = abstractCount,
                AfferentCoupling = ca,
                EfferentCoupling = ce,
                Types = types
            });
        }

        return result;
    }

    private static List<ProjectDependency> BuildDependencies(Project project, Solution solution)
    {
        var dependencies = new List<ProjectDependency>();
        foreach (var refProject in project.ProjectReferences)
        {
            var referenced = solution.GetProject(refProject.ProjectId);
            if (referenced is not null)
                dependencies.Add(new ProjectDependency(project.Name, referenced.Name));
        }
        return dependencies;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static ProjectMetrics EmptyMetrics(Project project) => new()
    {
        Name = project.Name,
        FilePath = project.FilePath ?? "",
        Namespaces = [],
        Interfaces = [],
        Implementations = [],
        OrphanInterfaces = [],
        Dependencies = [],
        PublicApi = new ApiSurface()
    };
}
