namespace FrenchExDev.Net.Guard;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

/// <summary>
/// Guard clauses that throw standard .NET exceptions.
/// Every method returns the validated value for inline use:
/// <c>var name = Guard.Against.NullOrEmpty(input);</c>
/// </summary>
public sealed class GuardAgainst
{
    internal GuardAgainst() { }

    /// <summary>Guards against null reference values.</summary>
    /// <exception cref="ArgumentNullException">When <paramref name="value"/> is null.</exception>
    public T Null<T>(
        T? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : class
        => value ?? throw new ArgumentNullException(paramName);

    /// <summary>Guards against null nullable value types.</summary>
    /// <exception cref="ArgumentNullException">When <paramref name="value"/> is null.</exception>
    public T NullValue<T>(
        T? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : struct
        => value ?? throw new ArgumentNullException(paramName);

    /// <summary>Guards against null or empty strings.</summary>
    /// <exception cref="ArgumentNullException">When <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">When <paramref name="value"/> is empty.</exception>
    public string NullOrEmpty(
        string? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value is null) throw new ArgumentNullException(paramName);
        if (value.Length == 0) throw new ArgumentException("Value cannot be empty.", paramName);
        return value;
    }

    /// <summary>Guards against null, empty, or whitespace-only strings.</summary>
    /// <exception cref="ArgumentNullException">When <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">When <paramref name="value"/> is empty or whitespace.</exception>
    public string NullOrWhiteSpace(
        string? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value is null) throw new ArgumentNullException(paramName);
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be empty or whitespace.", paramName);
        return value;
    }

    /// <summary>Guards against null or empty collections.</summary>
    /// <exception cref="ArgumentNullException">When <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">When <paramref name="value"/> is empty.</exception>
    public IReadOnlyList<T> NullOrEmpty<T>(
        IReadOnlyList<T>? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value is null) throw new ArgumentNullException(paramName);
        if (value.Count == 0) throw new ArgumentException("Collection cannot be empty.", paramName);
        return value;
    }

    /// <summary>Guards against values outside [min, max].</summary>
    /// <exception cref="ArgumentOutOfRangeException">When value is outside range.</exception>
    public T OutOfRange<T>(
        T value,
        T min,
        T max,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
            throw new ArgumentOutOfRangeException(paramName, value,
                $"Value must be between {min} and {max}.");
        return value;
    }

    /// <summary>Guards against negative values.</summary>
    /// <exception cref="ArgumentOutOfRangeException">When value is negative.</exception>
    public T Negative<T>(
        T value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(default!) < 0)
            throw new ArgumentOutOfRangeException(paramName, value, "Value cannot be negative.");
        return value;
    }

    /// <summary>Guards against zero values.</summary>
    /// <exception cref="ArgumentOutOfRangeException">When value is zero.</exception>
    public T Zero<T>(
        T value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(default!) == 0)
            throw new ArgumentOutOfRangeException(paramName, value, "Value cannot be zero.");
        return value;
    }

    /// <summary>Guards against negative or zero values.</summary>
    /// <exception cref="ArgumentOutOfRangeException">When value &lt;= 0.</exception>
    public T NegativeOrZero<T>(
        T value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(default!) <= 0)
            throw new ArgumentOutOfRangeException(paramName, value,
                "Value must be greater than zero.");
        return value;
    }

    /// <summary>Guards against default(T) values.</summary>
    /// <exception cref="ArgumentException">When value equals default(T).</exception>
    public T Default<T>(
        T value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : struct
    {
        if (EqualityComparer<T>.Default.Equals(value, default))
            throw new ArgumentException("Value cannot be default.", paramName);
        return value;
    }

    /// <summary>Guards against values that fail a custom predicate.</summary>
    /// <exception cref="ArgumentException">When the predicate returns false.</exception>
    public T InvalidInput<T>(
        T value,
        Func<T, bool> predicate,
        string message,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!predicate(value))
            throw new ArgumentException(message, paramName);
        return value;
    }

    /// <summary>Guards against strings exceeding a maximum length.</summary>
    /// <exception cref="ArgumentException">When string length exceeds maxLength.</exception>
    public string LengthExceeded(
        string value,
        int maxLength,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        Null(value, paramName);
        if (value.Length > maxLength)
            throw new ArgumentException(
                $"String length {value.Length} exceeds maximum of {maxLength}.", paramName);
        return value;
    }

    /// <summary>Guards against empty GUIDs.</summary>
    /// <exception cref="ArgumentException">When value is Guid.Empty.</exception>
    public Guid EmptyGuid(
        Guid value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("GUID cannot be empty.", paramName);
        return value;
    }

    /// <summary>Guards against enum values not defined in the enum type.</summary>
    /// <exception cref="ArgumentOutOfRangeException">When value is not a defined enum member.</exception>
    public T UndefinedEnum<T>(
        T value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(typeof(T), value))
            throw new ArgumentOutOfRangeException(paramName, value,
                $"Value is not a defined member of {typeof(T).Name}.");
        return value;
    }
}
