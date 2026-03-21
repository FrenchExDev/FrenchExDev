namespace FrenchExDev.Net.Diem.Pages.Routing;

/// <summary>
/// Resolves URLs to pages via materialized paths.
/// </summary>
public class PageRouter
{
    /// <summary>
    /// Resolves a URL path to a page and optional entity slug.
    /// </summary>
    public PageRouteResult? Resolve(string path, IReadOnlyList<Layouts.Page> pages)
    {
        // Exact match
        var exact = pages.FirstOrDefault(p => p.MaterializedPath == path && p.IsPublished);
        if (exact != null)
            return new PageRouteResult { Page = exact };

        // Parent match with entity slug (e.g., /products/running-shoes -> page=/products, slug=running-shoes)
        var lastSlash = path.LastIndexOf('/');
        if (lastSlash > 0)
        {
            var parentPath = path[..lastSlash];
            var slug = path[(lastSlash + 1)..];
            var parent = pages.FirstOrDefault(p => p.MaterializedPath == parentPath && p.IsPublished);
            if (parent != null)
                return new PageRouteResult { Page = parent, EntitySlug = slug };
        }

        return null;
    }
}

public class PageRouteResult
{
    public required Layouts.Page Page { get; init; }
    public string? EntitySlug { get; init; }
}
