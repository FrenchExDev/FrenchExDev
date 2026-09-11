namespace FrenchExDev.Net.FiniteStateMachine.Dynamic;

/// <summary>
/// Builder step: configures transitions from a specific string state.
/// </summary>
public sealed class DynamicWhenBuilder
{
    private readonly DynamicStateMachineBuilder _parent;
    private readonly string _source;

    internal DynamicWhenBuilder(DynamicStateMachineBuilder parent, string source)
    {
        _parent = parent;
        _source = source;
    }

    public DynamicOnBuilder On(string @event)
    {
        return new DynamicOnBuilder(_parent, _source, @event);
    }
}
