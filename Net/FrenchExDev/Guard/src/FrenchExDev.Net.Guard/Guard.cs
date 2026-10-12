namespace FrenchExDev.Net.Guard;

/// <summary>
/// Entry point for guard clause validation.
/// Two modes: <see cref="Against"/> (throws exceptions) and <see cref="ToResult"/> (returns Result&lt;T&gt;).
/// </summary>
public static class Guard
{
    /// <summary>
    /// Guard clauses that throw exceptions. Use at system boundaries
    /// (public API entry points, constructors, controller actions).
    /// </summary>
    public static GuardAgainst Against { get; } = new();

    /// <summary>
    /// Guard clauses that return Result&lt;T&gt;. Use inside functional pipelines
    /// where exceptions are avoided.
    /// </summary>
    public static GuardToResult ToResult { get; } = new();

    /// <summary>
    /// Assertions for invariants and postconditions. Throws InvalidOperationException
    /// when the condition is not met.
    /// </summary>
    public static GuardEnsure Ensure { get; } = new();
}
