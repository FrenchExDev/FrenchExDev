using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FrenchExDev.Net.FiniteStateMachine.Dynamic;

/// <summary>
/// Builder step: configures what happens when a string event is received.
/// </summary>
public sealed class DynamicOnBuilder
{
    private readonly DynamicStateMachineBuilder _parent;
    private readonly string _source;
    private readonly string _event;
    private readonly List<IGuard<string, string>> _guards = new List<IGuard<string, string>>();
    private readonly List<ITransitionAction<string, string>> _actions = new List<ITransitionAction<string, string>>();
    private string? _target;
    private bool _isInternal;

    internal DynamicOnBuilder(DynamicStateMachineBuilder parent, string source, string @event)
    {
        _parent = parent;
        _source = source;
        _event = @event;
    }

    public DynamicOnBuilder TransitionTo(string target)
    {
        _target = target;
        return this;
    }

    public DynamicOnBuilder InternalTransition()
    {
        _target = _source;
        _isInternal = true;
        return this;
    }

    public DynamicOnBuilder WithGuard(Func<string, string, string, CancellationToken, Task<bool>> guard)
    {
        _guards.Add(new DelegateGuard<string, string>(guard));
        return this;
    }

    public DynamicOnBuilder WithAction(Func<string, string, string, CancellationToken, Task> action)
    {
        _actions.Add(new DelegateTransitionAction<string, string>(action));
        return this;
    }

    public DynamicOnBuilder On(string @event)
    {
        Flush();
        return new DynamicOnBuilder(_parent, _source, @event);
    }

    public DynamicWhenBuilder When(string state)
    {
        Flush();
        return _parent.When(state);
    }

    public Result.Result<DynamicStateMachineDefinition> Build()
    {
        Flush();
        return _parent.Build();
    }

    internal void Flush()
    {
        if (_target == null) return;

        _parent.AddTransition(new TransitionDefinition<string, string>(
            _source, _event, _target, _guards, _actions, _isInternal));
        _target = null;
    }
}
