namespace FrenchExDev.Net.Options;

using System;
using System.Collections.Generic;

/// <summary>
/// Functor, monad, and utility extensions for <see cref="Option{T}"/>.
/// </summary>
public static class OptionExtensions
{
    // -- Functor --

    /// <summary>
    /// Transforms the contained value using <paramref name="mapper"/>.
    /// If the option is None, returns None without invoking the mapper.
    /// </summary>
    public static Option<TOut> Map<T, TOut>(this Option<T> option, Func<T, TOut> mapper)
        where T : notnull
        where TOut : notnull
        => option.IsSome ? Option<TOut>.Some(mapper(option.Value)) : Option<TOut>.None();

    // -- Monad --

    /// <summary>
    /// Chains an option-returning function. If the option is None, returns None.
    /// This is the monadic bind (>>=) operation.
    /// </summary>
    public static Option<TOut> Bind<T, TOut>(this Option<T> option, Func<T, Option<TOut>> binder)
        where T : notnull
        where TOut : notnull
        => option.IsSome ? binder(option.Value) : Option<TOut>.None();

    /// <summary>Alias for <see cref="Bind{T,TOut}"/>.</summary>
    public static Option<TOut> Then<T, TOut>(this Option<T> option, Func<T, Option<TOut>> binder)
        where T : notnull
        where TOut : notnull
        => option.Bind(binder);

    // -- Filtering --

    /// <summary>
    /// Returns the option unchanged if it is Some and the predicate holds.
    /// Returns None if the option is None or the predicate fails.
    /// </summary>
    public static Option<T> Filter<T>(this Option<T> option, Func<T, bool> predicate)
        where T : notnull
        => option.IsSome && predicate(option.Value) ? option : Option<T>.None();

    // -- Side effects --

    /// <summary>
    /// Executes <paramref name="action"/> if the option is Some, then returns the option unchanged.
    /// </summary>
    public static Option<T> Tap<T>(this Option<T> option, Action<T> action)
        where T : notnull
    {
        if (option.IsSome) action(option.Value);
        return option;
    }

    /// <summary>
    /// Executes <paramref name="action"/> if the option is None, then returns the option unchanged.
    /// </summary>
    public static Option<T> TapNone<T>(this Option<T> option, Action action)
        where T : notnull
    {
        if (option.IsNone) action();
        return option;
    }

    // -- Unwrapping --

    /// <summary>
    /// Returns the contained value if Some, or <paramref name="fallback"/> if None.
    /// </summary>
    public static T OrDefault<T>(this Option<T> option, T fallback)
        where T : notnull
        => option.IsSome ? option.Value : fallback;

    /// <summary>
    /// Returns the contained value if Some, or invokes <paramref name="fallbackFactory"/> if None.
    /// </summary>
    public static T OrElse<T>(this Option<T> option, Func<T> fallbackFactory)
        where T : notnull
        => option.IsSome ? option.Value : fallbackFactory();

    /// <summary>
    /// Returns the contained value if Some, or the alternative option if None.
    /// </summary>
    public static Option<T> Or<T>(this Option<T> option, Option<T> alternative)
        where T : notnull
        => option.IsSome ? option : alternative;

    /// <summary>
    /// Returns the contained value if Some, or invokes <paramref name="alternativeFactory"/> if None.
    /// </summary>
    public static Option<T> Or<T>(this Option<T> option, Func<Option<T>> alternativeFactory)
        where T : notnull
        => option.IsSome ? option : alternativeFactory();

    /// <summary>
    /// Returns the value if Some, or null if None. Useful at interop boundaries.
    /// </summary>
    public static T? ToNullable<T>(this Option<T> option)
        where T : class
        => option.IsSome ? option.Value : null;

    /// <summary>
    /// Returns the value if Some, or null if None. For value types.
    /// </summary>
    public static T? ToNullableStruct<T>(this Option<T> option)
        where T : struct
        => option.IsSome ? option.Value : null;

    // -- Zipping --

    /// <summary>
    /// Combines two options. Returns Some with both values if both are Some, None otherwise.
    /// </summary>
    public static Option<(T1, T2)> Zip<T1, T2>(this Option<T1> first, Option<T2> second)
        where T1 : notnull
        where T2 : notnull
        => first.IsSome && second.IsSome
            ? Option<(T1, T2)>.Some((first.Value, second.Value))
            : Option<(T1, T2)>.None();

    /// <summary>
    /// Combines two options using a selector. Returns Some with the combined result if both
    /// are Some, None otherwise.
    /// </summary>
    public static Option<TOut> Zip<T1, T2, TOut>(
        this Option<T1> first,
        Option<T2> second,
        Func<T1, T2, TOut> selector)
        where T1 : notnull
        where T2 : notnull
        where TOut : notnull
        => first.IsSome && second.IsSome
            ? Option<TOut>.Some(selector(first.Value, second.Value))
            : Option<TOut>.None();

    // -- Boolean operations --

    /// <summary>
    /// Returns true if the option is Some and the predicate holds.
    /// </summary>
    public static bool Contains<T>(this Option<T> option, Func<T, bool> predicate)
        where T : notnull
        => option.IsSome && predicate(option.Value);

    /// <summary>
    /// Returns true if the option is Some and the value equals <paramref name="value"/>.
    /// </summary>
    public static bool Contains<T>(this Option<T> option, T value)
        where T : notnull
        => option.IsSome && EqualityComparer<T>.Default.Equals(option.Value, value);
}
