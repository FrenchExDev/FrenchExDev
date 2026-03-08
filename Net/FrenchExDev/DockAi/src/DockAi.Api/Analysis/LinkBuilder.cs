using DockAi.Api.Models;

namespace DockAi.Api.Analysis;

/// <summary>
/// Builds symbolic links between documents based on shared entities, themes, and tracks.
/// </summary>
public sealed class LinkBuilder
{
    /// <summary>Minimum Jaccard similarity to create a link.</summary>
    public float MinJaccard { get; set; } = 0.15f;

    /// <summary>Build links for all document pairs. Populates doc.Links.</summary>
    public void BuildLinks(List<Document> documents)
    {
        // Clear existing links
        foreach (var doc in documents)
            doc.Links.Clear();

        // Build pairwise links
        for (var i = 0; i < documents.Count; i++)
        {
            for (var j = i + 1; j < documents.Count; j++)
            {
                var a = documents[i];
                var b = documents[j];

                // Shared entities
                var sharedEntities = a.Entities.Intersect(b.Entities).ToList();
                if (sharedEntities.Count > 0)
                {
                    var jaccard = Jaccard(a.Entities, b.Entities);
                    if (jaccard >= MinJaccard)
                    {
                        var link = new SymbolicLink
                        {
                            SourceId = a.Id,
                            TargetId = b.Id,
                            LinkType = "shared_entity",
                            LinkName = $"Share {sharedEntities.Count} entities",
                            Strength = jaccard,
                            Via = sharedEntities,
                            Reason = $"Common entities: {string.Join(", ", sharedEntities.Take(5))}"
                        };
                        a.Links.Add(link);
                        b.Links.Add(new SymbolicLink
                        {
                            SourceId = b.Id,
                            TargetId = a.Id,
                            LinkType = link.LinkType,
                            LinkName = link.LinkName,
                            Strength = link.Strength,
                            Via = link.Via,
                            Reason = link.Reason
                        });
                    }
                }

                // Shared taxonomy nodes (tracks + themes)
                BuildTaxonomyLinks(a, b, "track", "shared_track");
                BuildTaxonomyLinks(a, b, "theme", "shared_theme");
            }
        }
    }

    private void BuildTaxonomyLinks(Document a, Document b, string taxonomyId, string linkType)
    {
        var aTax = a.TaxonomyAssignments.GetValueOrDefault(taxonomyId);
        var bTax = b.TaxonomyAssignments.GetValueOrDefault(taxonomyId);
        if (aTax is null || bTax is null || aTax.Count == 0 || bTax.Count == 0) return;

        var shared = aTax.Intersect(bTax).ToList();
        if (shared.Count == 0) return;

        var jaccard = Jaccard(aTax, bTax);
        if (jaccard < MinJaccard) return;

        var link = new SymbolicLink
        {
            SourceId = a.Id,
            TargetId = b.Id,
            LinkType = linkType,
            LinkName = $"Share {taxonomyId}: {string.Join(", ", shared)}",
            Strength = jaccard,
            Via = shared,
            Reason = $"Common {taxonomyId}: {string.Join(", ", shared)}"
        };
        a.Links.Add(link);
        b.Links.Add(new SymbolicLink
        {
            SourceId = b.Id,
            TargetId = a.Id,
            LinkType = link.LinkType,
            LinkName = link.LinkName,
            Strength = link.Strength,
            Via = link.Via,
            Reason = link.Reason
        });
    }

    private static float Jaccard(IReadOnlyCollection<string> a, IReadOnlyCollection<string> b)
    {
        if (a is HashSet<string> setA && b is HashSet<string> setB)
        {
            var intersection = setA.Count(setB.Contains);
            var union = setA.Count + setB.Count - intersection;
            return union == 0 ? 0f : (float)intersection / union;
        }

        var aSet = a.ToHashSet();
        var bSet = b.ToHashSet();
        var inter = aSet.Count(bSet.Contains);
        var uni = aSet.Count + bSet.Count - inter;
        return uni == 0 ? 0f : (float)inter / uni;
    }
}
