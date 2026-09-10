using Microsoft.Extensions.Logging;

namespace FrenchExDev.Net.BinaryWrapper.Design.Lib;

/// <summary>Snapshots a resolver's catalog and binds each selected version to its own recipe cache.</summary>
internal sealed class ResolvedDesignImagePlans
{
    private readonly IDesignImagePlanResolver _resolver;
    private readonly Dictionary<DesignImagePlan, DesignImageCache> _caches = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, DesignImageCache> _versions = new(StringComparer.Ordinal);

    public ResolvedDesignImagePlans(IDesignImagePlanResolver resolver, string runtime,
        Func<string[], Task<string>> run, ILogger logger, string outputDirectory, bool streamBuildOutput)
    {
        _resolver = resolver;
        var plans = resolver.Plans?.ToArray();
        if (plans is null || plans.Length == 0)
            throw new ArgumentException("The image resolver must declare at least one recipe.", nameof(resolver));

        string? imageName = null;
        foreach (var plan in plans)
        {
            ArgumentNullException.ThrowIfNull(plan);
            plan.Validate();
            imageName ??= plan.ImageName;
            if (plan.ImageName != imageName)
                throw new ArgumentException("All resolver recipes must share one ImageName.", nameof(resolver));
            _caches.TryAdd(plan, new DesignImageCache(plan, runtime, run, logger, outputDirectory, streamBuildOutput));
        }
    }

    public async Task PrepareAllAsync()
    {
        foreach (var cache in _caches.Values)
            await cache.PrepareAsync();
    }

    public async Task PrepareAsync(IEnumerable<string> versions)
    {
        // Resolve everything before touching the engine. Workers only read this mapping.
        foreach (var version in versions.Distinct(StringComparer.Ordinal))
        {
            var plan = _resolver.Resolve(version);
            if (plan is null || !_caches.TryGetValue(plan, out var cache))
                throw new InvalidOperationException($"The image resolver returned an undeclared recipe for version {version}.");
            _versions.Add(version, cache);
        }

        // Equal base recipes still share the existing content-addressed image and build lock.
        foreach (var cache in _versions.Values.Distinct())
            await cache.PrepareAsync();
    }

    public Task<string> GetVersionImageAsync(string version) =>
        _versions[version].GetVersionImageAsync(version);

    public Task<DesignImageCache.VersionImageLease> AcquireVersionImageAsync(string version, bool removeAfterUse) =>
        _versions[version].AcquireVersionImageAsync(version, removeAfterUse);

    // All recipes have the same namespace; one label-based cleanup covers current and older recipes.
    public Task CleanAsync() => _caches.Values.First().CleanAsync();
}
