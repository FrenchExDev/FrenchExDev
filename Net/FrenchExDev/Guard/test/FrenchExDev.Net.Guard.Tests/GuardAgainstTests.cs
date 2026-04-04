namespace FrenchExDev.Net.Guard.Tests;

public class GuardAgainst_Null
{
    [Fact]
    public void Null_with_value_returns_value() =>
        Assert.Equal("hello", Guard.Against.Null("hello"));

    [Fact]
    public void Null_with_null_throws_ArgumentNullException()
    {
        string? value = null;
        Assert.Throws<ArgumentNullException>(() => Guard.Against.Null(value));
    }
}

public class GuardAgainst_NullValue
{
    [Fact]
    public void NullValue_with_value_returns_value()
    {
        int? value = 42;
        Assert.Equal(42, Guard.Against.NullValue(value));
    }

    [Fact]
    public void NullValue_with_null_throws()
    {
        int? value = null;
        Assert.Throws<ArgumentNullException>(() => Guard.Against.NullValue(value));
    }
}

public class GuardAgainst_NullOrEmpty
{
    [Fact]
    public void NullOrEmpty_with_value_returns_value() =>
        Assert.Equal("hello", Guard.Against.NullOrEmpty("hello"));

    [Fact]
    public void NullOrEmpty_with_null_throws_ArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => Guard.Against.NullOrEmpty(null));

    [Fact]
    public void NullOrEmpty_with_empty_throws_ArgumentException() =>
        Assert.Throws<ArgumentException>(() => Guard.Against.NullOrEmpty(""));
}

public class GuardAgainst_NullOrWhiteSpace
{
    [Fact]
    public void NullOrWhiteSpace_with_value_returns_value() =>
        Assert.Equal("hello", Guard.Against.NullOrWhiteSpace("hello"));

    [Fact]
    public void NullOrWhiteSpace_with_null_throws() =>
        Assert.Throws<ArgumentNullException>(() => Guard.Against.NullOrWhiteSpace(null));

    [Fact]
    public void NullOrWhiteSpace_with_whitespace_throws() =>
        Assert.Throws<ArgumentException>(() => Guard.Against.NullOrWhiteSpace("   "));
}

public class GuardAgainst_NullOrEmpty_Collection
{
    [Fact]
    public void NullOrEmpty_collection_with_items_returns_collection()
    {
        IReadOnlyList<int> list = new[] { 1, 2, 3 };
        Assert.Same(list, Guard.Against.NullOrEmpty(list));
    }

    [Fact]
    public void NullOrEmpty_collection_null_throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            Guard.Against.NullOrEmpty<int>(null));

    [Fact]
    public void NullOrEmpty_collection_empty_throws() =>
        Assert.Throws<ArgumentException>(() =>
            Guard.Against.NullOrEmpty<int>(Array.Empty<int>()));
}

public class GuardAgainst_OutOfRange
{
    [Fact]
    public void OutOfRange_in_range_returns_value() =>
        Assert.Equal(5, Guard.Against.OutOfRange(5, 1, 10));

    [Fact]
    public void OutOfRange_at_min_boundary_returns_value() =>
        Assert.Equal(1, Guard.Against.OutOfRange(1, 1, 10));

    [Fact]
    public void OutOfRange_at_max_boundary_returns_value() =>
        Assert.Equal(10, Guard.Against.OutOfRange(10, 1, 10));

    [Fact]
    public void OutOfRange_below_min_throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Guard.Against.OutOfRange(0, 1, 10));

    [Fact]
    public void OutOfRange_above_max_throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Guard.Against.OutOfRange(11, 1, 10));
}

public class GuardAgainst_Negative
{
    [Fact]
    public void Negative_positive_returns_value() =>
        Assert.Equal(5, Guard.Against.Negative(5));

    [Fact]
    public void Negative_zero_returns_zero() =>
        Assert.Equal(0, Guard.Against.Negative(0));

    [Fact]
    public void Negative_negative_throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.Against.Negative(-1));
}

public class GuardAgainst_NegativeOrZero
{
    [Fact]
    public void NegativeOrZero_positive_returns_value() =>
        Assert.Equal(5, Guard.Against.NegativeOrZero(5));

    [Fact]
    public void NegativeOrZero_zero_throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.Against.NegativeOrZero(0));

    [Fact]
    public void NegativeOrZero_negative_throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.Against.NegativeOrZero(-1));
}

public class GuardAgainst_Default
{
    [Fact]
    public void Default_non_default_returns_value() =>
        Assert.Equal(42, Guard.Against.Default(42));

    [Fact]
    public void Default_with_default_int_throws() =>
        Assert.Throws<ArgumentException>(() => Guard.Against.Default(0));

    [Fact]
    public void Default_with_default_guid_throws() =>
        Assert.Throws<ArgumentException>(() => Guard.Against.Default(Guid.Empty));
}

public class GuardAgainst_InvalidInput
{
    [Fact]
    public void InvalidInput_passing_predicate_returns_value() =>
        Assert.Equal("test@example.com",
            Guard.Against.InvalidInput("test@example.com", s => s.Contains('@'), "Invalid email"));

    [Fact]
    public void InvalidInput_failing_predicate_throws() =>
        Assert.Throws<ArgumentException>(() =>
            Guard.Against.InvalidInput("not-an-email", s => s.Contains('@'), "Invalid email"));
}

public class GuardAgainst_LengthExceeded
{
    [Fact]
    public void LengthExceeded_within_limit_returns_value() =>
        Assert.Equal("hello", Guard.Against.LengthExceeded("hello", 10));

    [Fact]
    public void LengthExceeded_over_limit_throws() =>
        Assert.Throws<ArgumentException>(() =>
            Guard.Against.LengthExceeded("hello world", 5));
}

public class GuardAgainst_EmptyGuid
{
    [Fact]
    public void EmptyGuid_with_valid_returns_value()
    {
        var guid = Guid.NewGuid();
        Assert.Equal(guid, Guard.Against.EmptyGuid(guid));
    }

    [Fact]
    public void EmptyGuid_with_empty_throws() =>
        Assert.Throws<ArgumentException>(() => Guard.Against.EmptyGuid(Guid.Empty));
}

public class GuardAgainst_UndefinedEnum
{
    private enum Color { Red = 0, Blue = 1, Green = 2 }

    [Fact]
    public void UndefinedEnum_defined_returns_value() =>
        Assert.Equal(Color.Blue, Guard.Against.UndefinedEnum(Color.Blue));

    [Fact]
    public void UndefinedEnum_undefined_throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Guard.Against.UndefinedEnum((Color)99));
}

public class GuardAgainst_Zero
{
    [Fact]
    public void Zero_non_zero_returns_value() =>
        Assert.Equal(5, Guard.Against.Zero(5));

    [Fact]
    public void Zero_zero_throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.Against.Zero(0));
}
