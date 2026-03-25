using System;

namespace FrenchExDev.Net.FiniteStateMachine.Attributes;

/// <summary>
/// Marks an enum member as a terminal (final) state.
/// Suppresses dead-end warnings for this state.
/// </summary>
[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class TerminalStateAttribute : Attribute
{
}
