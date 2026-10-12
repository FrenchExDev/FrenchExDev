using System.Collections.Generic;

namespace FrenchExDev.Net.FiniteStateMachine.SourceGenerator.Lib;

public sealed class RichEmitModel
{
    public RichEmitModel(
        string ns,
        string stateInterfaceName,
        string eventInterfaceName,
        string? initialStateTypeName,
        IReadOnlyList<RichStateEmitModel> states,
        IReadOnlyList<RichEventEmitModel> events,
        IReadOnlyList<RichTransitionEmitModel> transitions)
    {
        Namespace = ns;
        StateInterfaceName = stateInterfaceName;
        EventInterfaceName = eventInterfaceName;
        InitialStateTypeName = initialStateTypeName;
        States = states;
        Events = events;
        Transitions = transitions;
    }

    public string Namespace { get; }
    public string StateInterfaceName { get; }
    public string EventInterfaceName { get; }
    public string? InitialStateTypeName { get; }
    public IReadOnlyList<RichStateEmitModel> States { get; }
    public IReadOnlyList<RichEventEmitModel> Events { get; }
    public IReadOnlyList<RichTransitionEmitModel> Transitions { get; }

    /// <summary>Name prefix for generated types (e.g., "Order" from "IOrderState").</summary>
    public string DomainName
    {
        get
        {
            var name = StateInterfaceName;
            if (name.StartsWith("I") && name.Length > 1 && char.IsUpper(name[1]))
                name = name.Substring(1);
            if (name.EndsWith("State"))
                name = name.Substring(0, name.Length - 5);
            return name;
        }
    }
}

public sealed class RichStateEmitModel
{
    public RichStateEmitModel(string typeName, string fullTypeName, bool isTerminal)
    {
        TypeName = typeName;
        FullTypeName = fullTypeName;
        IsTerminal = isTerminal;
    }

    public string TypeName { get; }
    public string FullTypeName { get; }
    public bool IsTerminal { get; }
}

public sealed class RichEventEmitModel
{
    public RichEventEmitModel(string typeName, string fullTypeName)
    {
        TypeName = typeName;
        FullTypeName = fullTypeName;
    }

    public string TypeName { get; }
    public string FullTypeName { get; }
}

public sealed class RichTransitionEmitModel
{
    public RichTransitionEmitModel(string fromTypeName, string toTypeName, string eventTypeName)
    {
        FromTypeName = fromTypeName;
        ToTypeName = toTypeName;
        EventTypeName = eventTypeName;
    }

    public string FromTypeName { get; }
    public string ToTypeName { get; }
    public string EventTypeName { get; }
}
