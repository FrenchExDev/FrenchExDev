using System.Collections.Generic;

namespace FrenchExDev.Net.FiniteStateMachine.SourceGenerator.Lib;

public sealed class StateMachineEmitModel
{
    public StateMachineEmitModel(
        string ns,
        string className,
        string stateEnumFull,
        string eventEnumFull,
        string initialStateMember,
        IReadOnlyList<string> stateMembers,
        IReadOnlyList<string> eventMembers,
        IReadOnlyList<TransitionEmitModel> transitions,
        IReadOnlyList<string> userDefinedMethods)
    {
        Namespace = ns;
        ClassName = className;
        StateEnumFull = stateEnumFull;
        EventEnumFull = eventEnumFull;
        InitialStateMember = initialStateMember;
        StateMembers = stateMembers;
        EventMembers = eventMembers;
        Transitions = transitions;
        UserDefinedMethods = userDefinedMethods;
    }

    public string Namespace { get; }
    public string ClassName { get; }
    public string StateEnumFull { get; }
    public string EventEnumFull { get; }
    public string InitialStateMember { get; }
    public IReadOnlyList<string> StateMembers { get; }
    public IReadOnlyList<string> EventMembers { get; }
    public IReadOnlyList<TransitionEmitModel> Transitions { get; }

    /// <summary>
    /// Method names already defined by the user in their partial class.
    /// The emitter will skip generating these to avoid duplicates.
    /// </summary>
    public IReadOnlyList<string> UserDefinedMethods { get; }
}

public sealed class TransitionEmitModel
{
    public TransitionEmitModel(string fromMember, string eventMember, string toMember, string? guardMethod)
    {
        FromMember = fromMember;
        EventMember = eventMember;
        ToMember = toMember;
        GuardMethod = guardMethod;
    }

    public string FromMember { get; }
    public string EventMember { get; }
    public string ToMember { get; }
    public string? GuardMethod { get; }
}
