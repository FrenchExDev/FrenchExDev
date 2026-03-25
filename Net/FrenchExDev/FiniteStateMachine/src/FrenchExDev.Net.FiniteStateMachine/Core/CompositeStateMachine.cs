using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FrenchExDev.Net.Result;

namespace FrenchExDev.Net.FiniteStateMachine;

/// <summary>
/// Default implementation of a composite state machine with parallel regions.
/// </summary>
public sealed class CompositeStateMachine<TState, TEvent> : ICompositeStateMachine<TState, TEvent>
{
    private readonly List<Region> _regions;
    private readonly Func<TState, bool> _isTerminal;

    public CompositeStateMachine(
        IReadOnlyList<(string Name, IStateMachine<TState, TEvent> Machine)> regions,
        Func<TState, bool> isTerminal)
    {
        _regions = regions.Select(r => new Region(r.Name, r.Machine)).ToList();
        _isTerminal = isTerminal;
    }

    public IReadOnlyList<IRegion<TState, TEvent>> Regions => _regions;

    public bool AllRegionsTerminal
    {
        get
        {
            foreach (var region in _regions)
            {
                if (!_isTerminal(region.Machine.CurrentState))
                    return false;
            }
            return true;
        }
    }

    public async Task<IReadOnlyList<Result<Transition<TState>>>> FireAllAsync(
        TEvent @event, CancellationToken ct = default)
    {
        var results = new List<Result<Transition<TState>>>();
        foreach (var region in _regions)
        {
            var result = await region.Machine.FireAsync(@event, ct).ConfigureAwait(false);
            results.Add(result);
        }
        return results;
    }

    public IRegion<TState, TEvent>? GetRegion(string name)
    {
        foreach (var region in _regions)
        {
            if (string.Equals(region.Name, name, StringComparison.Ordinal))
                return region;
        }
        return null;
    }

    private sealed class Region : IRegion<TState, TEvent>
    {
        public Region(string name, IStateMachine<TState, TEvent> machine)
        {
            Name = name;
            Machine = machine;
        }

        public string Name { get; }
        public IStateMachine<TState, TEvent> Machine { get; }
    }
}
