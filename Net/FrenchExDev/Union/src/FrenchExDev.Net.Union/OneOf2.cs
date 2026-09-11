namespace FrenchExDev.Net.Union;

/// <summary>
/// A discriminated union that holds exactly one value of type <typeparamref name="T1"/> or <typeparamref name="T2"/>.
/// </summary>
public sealed class OneOf<T1, T2>
    : IEquatable<OneOf<T1, T2>>
    where T1 : notnull
    where T2 : notnull
{
    private readonly object _value;
    private readonly byte _index;

    private OneOf(object value, byte index)
    {
        _value = value;
        _index = index;
    }

    /// <summary>Creates a <see cref="OneOf{T1,T2}"/> holding a <typeparamref name="T1"/> value.</summary>
    public static OneOf<T1, T2> From(T1 value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        return new OneOf<T1, T2>(value, 0);
    }

    /// <summary>Creates a <see cref="OneOf{T1,T2}"/> holding a <typeparamref name="T2"/> value.</summary>
    public static OneOf<T1, T2> From(T2 value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        return new OneOf<T1, T2>(value, 1);
    }

    /// <summary>Implicitly converts a <typeparamref name="T1"/> value to <see cref="OneOf{T1,T2}"/>.</summary>
    public static implicit operator OneOf<T1, T2>(T1 value) => From(value);

    /// <summary>Implicitly converts a <typeparamref name="T2"/> value to <see cref="OneOf{T1,T2}"/>.</summary>
    public static implicit operator OneOf<T1, T2>(T2 value) => From(value);

    /// <summary>Returns <c>true</c> if this instance holds a <typeparamref name="T1"/> value.</summary>
    public bool IsT1 => _index == 0;

    /// <summary>Returns <c>true</c> if this instance holds a <typeparamref name="T2"/> value.</summary>
    public bool IsT2 => _index == 1;

    /// <summary>Gets the <typeparamref name="T1"/> value, or throws <see cref="InvalidOperationException"/>.</summary>
    public T1 AsT1 => _index == 0
        ? (T1)_value
        : throw new InvalidOperationException($"Cannot access T1 when the union holds T{_index + 1}.");

    /// <summary>Gets the <typeparamref name="T2"/> value, or throws <see cref="InvalidOperationException"/>.</summary>
    public T2 AsT2 => _index == 1
        ? (T2)_value
        : throw new InvalidOperationException($"Cannot access T2 when the union holds T{_index + 1}.");

    /// <summary>Exhaustively matches the union, invoking the function corresponding to the held type.</summary>
    public TResult Match<TResult>(Func<T1, TResult> withT1, Func<T2, TResult> withT2)
    {
        if (withT1 is null) throw new ArgumentNullException(nameof(withT1));
        if (withT2 is null) throw new ArgumentNullException(nameof(withT2));

        return _index switch
        {
            0 => withT1((T1)_value),
            1 => withT2((T2)_value),
            _ => throw new InvalidOperationException("Invalid union state.")
        };
    }

    /// <summary>Exhaustively switches on the union, invoking the action corresponding to the held type.</summary>
    public void Switch(Action<T1> withT1, Action<T2> withT2)
    {
        if (withT1 is null) throw new ArgumentNullException(nameof(withT1));
        if (withT2 is null) throw new ArgumentNullException(nameof(withT2));

        switch (_index)
        {
            case 0:
                withT1((T1)_value);
                break;
            case 1:
                withT2((T2)_value);
                break;
            default:
                throw new InvalidOperationException("Invalid union state.");
        }
    }

    /// <summary>Tries to get the value as <typeparamref name="T"/>.</summary>
    public bool TryGet<T>(out T value) where T : notnull
    {
        if (_value is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }

    /// <inheritdoc />
    public bool Equals(OneOf<T1, T2>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _index == other._index && _value.Equals(other._value);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as OneOf<T1, T2>);

    /// <inheritdoc />
    public override int GetHashCode()
    {
#if NETSTANDARD2_0
        unchecked
        {
            return (_index * 397) ^ _value.GetHashCode();
        }
#else
        return HashCode.Combine(_index, _value);
#endif
    }

    /// <inheritdoc />
    public override string ToString() => FormattableString.Invariant($"T{_index + 1}({_value})");
}
