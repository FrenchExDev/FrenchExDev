using Microsoft.CodeAnalysis;

using FrenchExDev.Net.QualityGate.Model;

namespace FrenchExDev.Net.QualityGate.Analysis;

/// <summary>
/// Builds a list of project-to-project dependencies from a Roslyn <see cref="Solution"/>.
/// </summary>
public static class DependencyGraphBuilder
{
    /// <summary>
    /// Enumerates all <see cref="ProjectReference"/> edges in the solution and returns
    /// them as a flat list of <see cref="ProjectDependency"/> records.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage] // Defensive: GetProject returns null only for orphaned ProjectReference IDs
    public static List<ProjectDependency> BuildProjectDependencies(Solution solution)
    {
        ArgumentNullException.ThrowIfNull(solution);

        var dependencies = new List<ProjectDependency>();

        foreach (var project in solution.Projects)
        {
            foreach (var projectReference in project.ProjectReferences)
            {
                var referencedProject = solution.GetProject(projectReference.ProjectId);
                if (referencedProject is null)
                    continue;

                dependencies.Add(new ProjectDependency(
                    FromProject: project.Name,
                    ToProject: referencedProject.Name));
            }
        }

        return dependencies;
    }
}
