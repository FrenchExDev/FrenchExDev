namespace FrenchExDev.Net.Options;

using System;

/// <summary>
/// LINQ query syntax support for <see cref="Option{T}"/>.
/// Enables: <c>from x in option from y in other select x + y</c>
/// </summary>
public static class OptionLinqExtensions
{
    /// <summary>LINQ Select = Map.</summary>
    public static Option<TOut> Select<T, TOut>(
        this Option<T> option,
        Func<T, TOut> selector)
        where T : notnull
        where TOut : notnull
        => option.Map(selector);

    /// <summary>LINQ SelectMany = Bind.</summary>
    public static Option<TOut> SelectMany<T, TIntermediate, TOut>(
        this Option<T> option,
        Func<T, Option<TIntermediate>> binder,
        Func<T, TIntermediate, TOut> resultSelector)
        where T : notnull
        where TIntermediate : notnull
        where TOut : notnull
        => option.Bind(x => binder(x).Map(y => resultSelector(x, y)));

    /// <summary>LINQ Where = Filter. Enables <c>where</c> clause in query syntax.</summary>
    public static Option<T> Where<T>(
        this Option<T> option,
        Func<T, bool> predicate)
        where T : notnull
        => option.IsSome && predicate(option.Value) ? option : Option<T>.None();
}
