namespace FrenchExDev.Net.Guard;

using System;

/// <summary>
/// Invariant and postcondition assertions.
/// Throws <see cref="InvalidOperationException"/> (not ArgumentException)
/// because these guard internal state, not input parameters.
/// </summary>
public sealed class GuardEnsure
{
    internal GuardEnsure() { }

    /// <summary>Asserts that a condition is true.</summary>
    /// <exception cref="InvalidOperationException">When the condition is false.</exception>
    public void That(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    /// <summary>Asserts that a condition is true, with a lazy message.</summary>
    /// <exception cref="InvalidOperationException">When the condition is false.</exception>
    public void That(bool condition, Func<string> messageFactory)
    {
        if (!condition)
            throw new InvalidOperationException(messageFactory());
    }

    /// <summary>Asserts that a value is not null. Returns the value for chaining.</summary>
    /// <exception cref="InvalidOperationException">When the value is null.</exception>
    public T NotNull<T>(T? value, string message) where T : class
        => value ?? throw new InvalidOperationException(message);

    /// <summary>
    /// Asserts that an operation has not already been performed.
    /// Useful for single-use operations (e.g., Build, Dispose).
    /// </summary>
    /// <exception cref="InvalidOperationException">When <paramref name="alreadyDone"/> is true.</exception>
    public void NotAlreadyDone(bool alreadyDone, string operationName)
    {
        if (alreadyDone)
            throw new InvalidOperationException($"{operationName} has already been performed.");
    }
}
