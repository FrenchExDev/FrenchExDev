namespace FrenchExDev.Net.Options;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Extensions for collections of <see cref="Option{T}"/>.
/// </summary>
public static class OptionCollectionExtensions
{
    /// <summary>
    /// Extracts all Some values from a sequence of Options, discarding Nones.
    /// </summary>
    public static IEnumerable<T> Values<T>(this IEnumerable<Option<T>> options)
        where T : notnull
        => options.Where(o => o.IsSome).Select(o => o.Value);

    /// <summary>
    /// Returns the first Some value from a sequence of Options, or None if all are None.
    /// </summary>
    public static Option<T> FirstOrNone<T>(this IEnumerable<Option<T>> options)
        where T : notnull
    {
        foreach (var option in options)
        {
            if (option.IsSome) return option;
        }
        return Option<T>.None();
    }

    /// <summary>
    /// Returns the first element matching the predicate wrapped in Some,
    /// or None if no element matches.
    /// </summary>
    public static Option<T> FirstOrNone<T>(
        this IEnumerable<T> source,
        Func<T, bool> predicate)
        where T : notnull
    {
        foreach (var item in source)
        {
            if (predicate(item)) return Option<T>.Some(item);
        }
        return Option<T>.None();
    }

    /// <summary>
    /// Returns the single element matching the predicate wrapped in Some,
    /// or None if zero or more than one element matches.
    /// </summary>
    public static Option<T> SingleOrNone<T>(
        this IEnumerable<T> source,
        Func<T, bool> predicate)
        where T : notnull
    {
        T? found = default;
        var count = 0;
        foreach (var item in source)
        {
            if (predicate(item))
            {
                found = item;
                count++;
                if (count > 1) return Option<T>.None();
            }
        }
        return count == 1 ? Option<T>.Some(found!) : Option<T>.None();
    }

    /// <summary>
    /// Looks up a key in a dictionary and returns Some if found, None otherwise.
    /// </summary>
    public static Option<TValue> GetValueOrNone<TKey, TValue>(
        this IReadOnlyDictionary<TKey, TValue> dictionary,
        TKey key)
        where TKey : notnull
        where TValue : notnull
        => dictionary.TryGetValue(key, out var value) ? Option<TValue>.Some(value) : Option<TValue>.None();

    /// <summary>
    /// Converts a sequence of Options into an Option of sequence.
    /// Returns Some with all values if ALL are Some, None if ANY is None.
    /// This is the "sequence" operation from Haskell.
    /// </summary>
    public static Option<IReadOnlyList<T>> Sequence<T>(this IEnumerable<Option<T>> options)
        where T : notnull
    {
        var result = new List<T>();
        foreach (var option in options)
        {
            if (option.IsNone) return Option<IReadOnlyList<T>>.None();
            result.Add(option.Value);
        }
        return Option<IReadOnlyList<T>>.Some(result);
    }

    /// <summary>
    /// Maps each element with a function returning Option, then sequences the results.
    /// Returns Some with all values if ALL mappings return Some, None if ANY returns None.
    /// This is the "traverse" operation from Haskell.
    /// </summary>
    public static Option<IReadOnlyList<TOut>> Traverse<T, TOut>(
        this IEnumerable<T> source,
        Func<T, Option<TOut>> mapper)
        where T : notnull
        where TOut : notnull
        => source.Select(mapper).Sequence();
}
