using System;

namespace FrenchExDev.Net.FiniteStateMachine.Attributes;

/// <summary>
/// Marks a partial interface as a rich state machine state interface.
/// The Design SG generates visitor, Match, Accept, factory from this.
/// </summary>
[AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
public sealed class RichStateMachineAttribute : Attribute
{
    public Type? InitialState { get; set; }
}

/// <summary>
/// Marks a partial interface as the event interface for a rich state machine.
/// </summary>
[AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
public sealed class RichStateMachineEventsAttribute : Attribute
{
    public Type? StateMachine { get; set; }
}

/// <summary>
/// Marks a partial record as a state implementation in a rich state machine.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class StateAttribute : Attribute
{
    public bool Terminal { get; set; }
}

/// <summary>
/// Marks a partial record as an event implementation in a rich state machine.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class EventAttribute : Attribute
{
}

/// <summary>
/// Defines a transition on an event record: from source state type to target state type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class RichTransitionAttribute : Attribute
{
    public RichTransitionAttribute(Type from, Type to)
    {
        From = from;
        To = to;
    }

    public Type From { get; }
    public Type To { get; }
}
