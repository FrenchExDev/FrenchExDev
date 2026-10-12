using System;

namespace FrenchExDev.Net.FiniteStateMachine.Attributes;

/// <summary>
/// Defines a state transition. Applied to the DefineTransitions() partial method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class TransitionAttribute : Attribute
{
    public TransitionAttribute(object from, object @event, object to)
    {
        From = from;
        Event = @event;
        To = to;
    }

    public object From { get; }
    public object Event { get; }
    public object To { get; }

    /// <summary>
    /// Optional name of a partial guard method on the class.
    /// </summary>
    public string? Guard { get; set; }
}
