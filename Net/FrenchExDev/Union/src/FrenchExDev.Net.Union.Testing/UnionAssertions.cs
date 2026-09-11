namespace FrenchExDev.Net.Union.Testing;

/// <summary>
/// Assertion helpers for <see cref="OneOf{T1,T2}"/> discriminated unions.
/// </summary>
public static class UnionAssertions
{
    /// <summary>Asserts that the union holds a <typeparamref name="T1"/> value and returns it.</summary>
    public static T1 ShouldBeT1<T1, T2>(this OneOf<T1, T2> union)
        where T1 : notnull
        where T2 : notnull
    {
        if (union is null) throw new ArgumentNullException(nameof(union));
        if (!union.IsT1)
            throw new InvalidOperationException(
                $"Expected union to hold T1 ({typeof(T1).Name}) but it holds T2 ({typeof(T2).Name}).");
        return union.AsT1;
    }

    /// <summary>Asserts that the union holds a <typeparamref name="T2"/> value and returns it.</summary>
    public static T2 ShouldBeT2<T1, T2>(this OneOf<T1, T2> union)
        where T1 : notnull
        where T2 : notnull
    {
        if (union is null) throw new ArgumentNullException(nameof(union));
        if (!union.IsT2)
            throw new InvalidOperationException(
                $"Expected union to hold T2 ({typeof(T2).Name}) but it holds T1 ({typeof(T1).Name}).");
        return union.AsT2;
    }
}
