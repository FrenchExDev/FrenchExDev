using System;

namespace FrenchExDev.Net.FiniteStateMachine.Attributes;

/// <summary>
/// Marks a partial class as a source-generated state machine.
/// The class will inherit from StateMachineBase{TState, TEvent}.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class StateMachineAttribute : Attribute
{
    public StateMachineAttribute(Type stateEnum, Type eventEnum)
    {
        StateEnum = stateEnum;
        EventEnum = eventEnum;
    }

    public Type StateEnum { get; }
    public Type EventEnum { get; }

    /// <summary>
    /// Name of the initial state enum member (e.g., "Closed"). Required.
    /// </summary>
    public string InitialState { get; set; } = "";
}
