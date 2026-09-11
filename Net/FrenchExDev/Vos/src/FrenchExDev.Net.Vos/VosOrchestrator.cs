using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos;

/// <summary>
/// Orchestrates Vos actions across multiple instances.
/// Handles <c>--all</c> flag, group operations, and sequential/parallel execution.
/// </summary>
public sealed class VosOrchestrator
{
    private readonly IVosBackend _backend;
    private readonly VosConfig _config;

    public VosOrchestrator(IVosBackend backend, VosConfig config)
    {
        _backend = backend;
        _config = config;
    }

    /// <summary>
    /// Resolves target instances: specific name, or all if <paramref name="all"/> is true.
    /// </summary>
    public List<ResolvedInstance> ResolveTargets(string? instanceName, bool all)
    {
        var resolved = VosConfigMerger.ResolveAll(_config);

        if (all || instanceName is null)
            return resolved.Select(x => x.Instance).ToList();

        var matched = resolved
            .Where(x => x.Instance.Name.Equals(instanceName, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Instance)
            .ToList();

        return matched;
    }

    /// <summary>
    /// Executes an action on all resolved targets sequentially.
    /// </summary>
    public async Task<List<(string Name, VosActionResult Result)>> ExecuteAsync(
        string? instanceName,
        bool all,
        Func<IVosBackend, ResolvedInstance, CancellationToken, Task<VosActionResult>> action,
        CancellationToken ct = default)
    {
        var targets = ResolveTargets(instanceName, all);
        var results = new List<(string, VosActionResult)>();

        foreach (var inst in targets)
        {
            ct.ThrowIfCancellationRequested();
            var result = await action(_backend, inst, ct);
            results.Add((inst.Name, result));
        }

        return results;
    }

    /// <summary>
    /// Executes an action on a machine group (all instances of a given machine name).
    /// </summary>
    public async Task<List<(string Name, VosActionResult Result)>> ExecuteGroupAsync(
        string machineName,
        Func<IVosBackend, ResolvedInstance, CancellationToken, Task<VosActionResult>> action,
        CancellationToken ct = default)
    {
        if (!_config.Machines.TryGetValue(machineName, out var machine))
            return new List<(string, VosActionResult)>
            {
                (machineName, new VosActionResult(false, "", $"Machine '{machineName}' not found"))
            };

        var results = new List<(string, VosActionResult)>();
        foreach (var instance in machine.Instances)
        {
            ct.ThrowIfCancellationRequested();
            var resolved = VosConfigMerger.Resolve(_config, machineName, instance);
            var result = await action(_backend, resolved, ct);
            results.Add((instance.Name, result));
        }

        return results;
    }
}
