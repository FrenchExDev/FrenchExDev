namespace FrenchExDev.Net.Guard.Tests;

using FrenchExDev.Net.Result;

public class GuardToResult_Null
{
    [Fact]
    public void Null_with_value_returns_Success()
    {
        var result = Guard.ToResult.Null("hello");
        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void Null_with_null_returns_Failure()
    {
        var result = Guard.ToResult.Null<string>(null);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Null_with_custom_message()
    {
        var result = Guard.ToResult.Null<string>(null, "Name is required");
        Assert.Equal("Name is required", result.ValidationResult!.ErrorMessage);
    }
}

public class GuardToResult_NullValue
{
    [Fact]
    public void NullValue_with_value_returns_Success()
    {
        var result = Guard.ToResult.NullValue<int>(42);
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void NullValue_with_null_returns_Failure()
    {
        var result = Guard.ToResult.NullValue<int>(null);
        Assert.True(result.IsFailure);
    }
}

public class GuardToResult_NullOrEmpty
{
    [Fact]
    public void NullOrEmpty_valid_returns_Success()
    {
        var result = Guard.ToResult.NullOrEmpty("hello");
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void NullOrEmpty_null_returns_Failure()
    {
        var result = Guard.ToResult.NullOrEmpty(null);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void NullOrEmpty_empty_returns_Failure()
    {
        var result = Guard.ToResult.NullOrEmpty("");
        Assert.True(result.IsFailure);
    }
}

public class GuardToResult_NullOrWhiteSpace
{
    [Fact]
    public void NullOrWhiteSpace_valid_returns_Success()
    {
        var result = Guard.ToResult.NullOrWhiteSpace("hello");
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void NullOrWhiteSpace_whitespace_returns_Failure()
    {
        var result = Guard.ToResult.NullOrWhiteSpace("   ");
        Assert.True(result.IsFailure);
    }
}

public class GuardToResult_OutOfRange
{
    [Fact]
    public void OutOfRange_in_range_returns_Success()
    {
        var result = Guard.ToResult.OutOfRange(5, 1, 10);
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public void OutOfRange_outside_returns_Failure()
    {
        var result = Guard.ToResult.OutOfRange(15, 1, 10);
        Assert.True(result.IsFailure);
    }
}

public class GuardToResult_Negative
{
    [Fact]
    public void Negative_positive_returns_Success()
    {
        var result = Guard.ToResult.Negative(5);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Negative_negative_returns_Failure()
    {
        var result = Guard.ToResult.Negative(-1);
        Assert.True(result.IsFailure);
    }
}

public class GuardToResult_NegativeOrZero
{
    [Fact]
    public void NegativeOrZero_positive_returns_Success()
    {
        var result = Guard.ToResult.NegativeOrZero(5);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void NegativeOrZero_zero_returns_Failure()
    {
        var result = Guard.ToResult.NegativeOrZero(0);
        Assert.True(result.IsFailure);
    }
}

public class GuardToResult_Default
{
    [Fact]
    public void Default_non_default_returns_Success()
    {
        var result = Guard.ToResult.Default(42);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Default_default_returns_Failure()
    {
        var result = Guard.ToResult.Default(0);
        Assert.True(result.IsFailure);
    }
}

public class GuardToResult_EmptyGuid
{
    [Fact]
    public void EmptyGuid_valid_returns_Success()
    {
        var result = Guard.ToResult.EmptyGuid(Guid.NewGuid());
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void EmptyGuid_empty_returns_Failure()
    {
        var result = Guard.ToResult.EmptyGuid(Guid.Empty);
        Assert.True(result.IsFailure);
    }
}

public class GuardToResult_Pipeline
{
    [Fact]
    public void Guards_compose_in_Result_pipeline()
    {
        var result = Guard.ToResult.NullOrEmpty("42")
            .Bind(s => Guard.ToResult.InvalidInput(s, v => int.TryParse(v, out _), "Not a number"))
            .Map(s => int.Parse(s))
            .Bind(n => Guard.ToResult.OutOfRange(n, 1, 100));

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Guard_pipeline_short_circuits_on_first_failure()
    {
        var mapCalled = false;
        var result = Guard.ToResult.NullOrEmpty("")
            .Map(s => { mapCalled = true; return s.Length; });

        Assert.True(result.IsFailure);
        Assert.False(mapCalled);
    }
}
