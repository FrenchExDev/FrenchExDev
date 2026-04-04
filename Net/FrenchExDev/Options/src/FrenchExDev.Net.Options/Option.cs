namespace FrenchExDev.Net.Options;

using System;

/// <summary>
/// Represents an optional value — either <see cref="Some"/> (value present)
/// or <see cref="None"/> (no value). Unlike <c>Result&lt;T&gt;</c>, absence
/// is not an error — it is a normal, expected state.
/// </summary>
/// <remarks>
/// Thread-safe: sealed record, immutable, no shared mutable state.
/// All instances are created via factory methods.
/// </remarks>
/// <typeparam name="T">The type of the contained value. Must be non-null.</typeparam>
public sealed record Option<T> where T : notnull
{
    private readonly T? _value;
    private readonly bool _isSome;

    private Option(T value)
    {
        _value = value;
        _isSome = true;
    }

    private Option()
    {
        _value = default;
        _isSome = false;
    }

    /// <summary>True when a value is present.</summary>
    public bool IsSome => _isSome;

    /// <summary>True when no value is present.</summary>
    public bool IsNone => !_isSome;

    /// <summary>
    /// Returns the contained value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the option is None.</exception>
    public T Value => _isSome
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on a None option.");

    /// <summary>Creates a Some option containing the given value.</summary>
    /// <param name="value">The value to wrap. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public static Option<T> Some(T value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        return new Option<T>(value);
    }

    /// <summary>Creates a None option (no value).</summary>
    public static Option<T> None() => new();

    /// <summary>
    /// Pattern-matches on the option, returning the result of the matching branch.
    /// Both branches must be provided — exhaustive matching is enforced at compile-time.
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone)
        => _isSome ? onSome(_value!) : onNone();

    /// <summary>
    /// Pattern-matches on the option, executing the matching branch for side effects.
    /// </summary>
    public void Switch(Action<T> onSome, Action onNone)
    {
        if (_isSome) onSome(_value!);
        else onNone();
    }

    /// <summary>Implicit conversion from a value to Some.</summary>
    public static implicit operator Option<T>(T value) => Some(value);

    public override string ToString()
        => _isSome ? $"Some({_value})" : "None";
}

/// <summary>Static helper for creating Option instances without specifying the type parameter.</summary>
public static class Option
{
    /// <summary>Creates a Some option from a non-null value.</summary>
    public static Option<T> Some<T>(T value) where T : notnull
        => Option<T>.Some(value);

    /// <summary>Creates a None option of the specified type.</summary>
    public static Option<T> None<T>() where T : notnull
        => Option<T>.None();

    /// <summary>
    /// Creates an option from a nullable value.
    /// Returns Some if the value is non-null, None otherwise.
    /// </summary>
    public static Option<T> From<T>(T? value) where T : class
        => value is not null ? Option<T>.Some(value) : Option<T>.None();

    /// <summary>
    /// Creates an option from a nullable value type.
    /// Returns Some if the value has a value, None otherwise.
    /// </summary>
    public static Option<T> FromNullable<T>(T? value) where T : struct
        => value.HasValue ? Option<T>.Some(value.Value) : Option<T>.None();

    /// <summary>
    /// Wraps a factory that may throw into an Option.
    /// Returns Some with the result if the factory succeeds, None if it throws.
    /// </summary>
    public static Option<T> FromTry<T>(Func<T> factory) where T : notnull
    {
        try { return Option<T>.Some(factory()); }
        catch { return Option<T>.None(); }
    }

    /// <summary>
    /// Wraps a factory that may throw into an Option, catching only the specified exception type.
    /// </summary>
    public static Option<T> FromTry<T, TException>(Func<T> factory)
        where T : notnull
        where TException : Exception
    {
        try { return Option<T>.Some(factory()); }
        catch (TException) { return Option<T>.None(); }
    }
}
