namespace FrenchExDev.Net.Options.Testing;

using System;
using System.Collections.Generic;

/// <summary>
/// Assertion helpers for <see cref="Option{T}"/> in xUnit tests.
/// </summary>
public static class OptionAssertions
{
    /// <summary>
    /// Asserts the option is Some and returns the value for further assertions.
    /// </summary>
    /// <exception cref="Exception">Thrown when the option is None.</exception>
    public static T ShouldBeSome<T>(this Option<T> option) where T : notnull
    {
        if (option.IsNone)
            throw new Exception($"Expected Some but got None for Option<{typeof(T).Name}>.");
        return option.Value;
    }

    /// <summary>
    /// Asserts the option is Some and the contained value equals the expected value.
    /// </summary>
    public static void ShouldBeSome<T>(this Option<T> option, T expected) where T : notnull
    {
        var actual = option.ShouldBeSome();
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            throw new Exception(
                $"Expected Some({expected}) but got Some({actual}) for Option<{typeof(T).Name}>.");
    }

    /// <summary>
    /// Asserts the option is None.
    /// </summary>
    /// <exception cref="Exception">Thrown when the option is Some.</exception>
    public static void ShouldBeNone<T>(this Option<T> option) where T : notnull
    {
        if (option.IsSome)
            throw new Exception(
                $"Expected None but got Some({option.Value}) for Option<{typeof(T).Name}>.");
    }

    /// <summary>
    /// Asserts the option is Some and the value satisfies the predicate.
    /// </summary>
    public static T ShouldBeSomeAnd<T>(this Option<T> option, Func<T, bool> predicate) where T : notnull
    {
        var value = option.ShouldBeSome();
        if (!predicate(value))
            throw new Exception(
                $"Option is Some({value}) but the predicate returned false for Option<{typeof(T).Name}>.");
        return value;
    }
}
