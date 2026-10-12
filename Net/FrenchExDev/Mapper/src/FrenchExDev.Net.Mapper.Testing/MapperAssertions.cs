using System;

namespace FrenchExDev.Net.Mapper.Testing;

/// <summary>
/// Provides assertion helpers for testing <see cref="IMapper{TSource, TTarget}"/> implementations.
/// </summary>
public static class MapperAssertions
{
    /// <summary>
    /// Maps <paramref name="source"/> using the mapper and asserts the result equals <paramref name="expected"/>.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="mapper">The mapper to test.</param>
    /// <param name="source">The source instance.</param>
    /// <param name="expected">The expected target instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mapper"/> is null.</exception>
    /// <exception cref="MapperAssertionException">Thrown when the mapped result does not equal the expected value.</exception>
    public static void ShouldMapTo<TSource, TTarget>(
        this IMapper<TSource, TTarget> mapper,
        TSource source,
        TTarget expected)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(mapper);
#else
        if (mapper is null) throw new ArgumentNullException(nameof(mapper));
#endif

        var actual = mapper.Map(source);

        if (!Equals(actual, expected))
        {
            throw new MapperAssertionException(
                $"Expected mapped result to equal '{expected}', but got '{actual}'.");
        }
    }

    /// <summary>
    /// Maps <paramref name="source"/> using the mapper and asserts a specific property equals <paramref name="expected"/>.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <typeparam name="TProp">The property type.</typeparam>
    /// <param name="mapper">The mapper to test.</param>
    /// <param name="source">The source instance.</param>
    /// <param name="selector">A function to select the property from the mapped target.</param>
    /// <param name="expected">The expected property value.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mapper"/> or <paramref name="selector"/> is null.</exception>
    /// <exception cref="MapperAssertionException">Thrown when the selected property does not equal the expected value.</exception>
    public static void ShouldMapProperty<TSource, TTarget, TProp>(
        this IMapper<TSource, TTarget> mapper,
        TSource source,
        Func<TTarget, TProp> selector,
        TProp expected)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(selector);
#else
        if (mapper is null) throw new ArgumentNullException(nameof(mapper));
        if (selector is null) throw new ArgumentNullException(nameof(selector));
#endif

        var target = mapper.Map(source);
        var actual = selector(target);

        if (!Equals(actual, expected))
        {
            throw new MapperAssertionException(
                $"Expected mapped property to equal '{expected}', but got '{actual}'.");
        }
    }
}
