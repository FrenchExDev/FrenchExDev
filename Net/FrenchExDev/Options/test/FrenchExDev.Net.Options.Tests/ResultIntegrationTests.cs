namespace FrenchExDev.Net.Options.Tests;

using System.ComponentModel.DataAnnotations;
using FrenchExDev.Net.Result;

public class OptionResult_ToResult
{
    [Fact]
    public void Some_to_Result_returns_Success()
    {
        var result = Option.Some(42).ToResult("not found");
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void None_to_Result_returns_Failure()
    {
        var result = Option.None<int>().ToResult("not found");
        Assert.True(result.IsFailure);
        Assert.Equal("not found", result.ValidationResult!.ErrorMessage);
    }

    [Fact]
    public void None_to_Result_with_factory_invokes_factory()
    {
        var result = Option.None<int>()
            .ToResult(() => new ValidationResult("custom error"));
        Assert.True(result.IsFailure);
        Assert.Equal("custom error", result.ValidationResult!.ErrorMessage);
    }
}

public class OptionResult_ToResultTypedError
{
    [Fact]
    public void Some_to_typed_Result_returns_Success()
    {
        var result = Option.Some(42).ToResult<int, string>("not found");
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void None_to_typed_Result_returns_Failure()
    {
        var result = Option.None<int>().ToResult<int, string>("not found");
        Assert.True(result.IsFailure);
        Assert.Equal("not found", result.Error);
    }
}

public class OptionResult_ToOption
{
    [Fact]
    public void Success_Result_to_Option_returns_Some()
    {
        var option = Result<int>.Success(42).ToOption();
        option.ShouldBeSome(42);
    }

    [Fact]
    public void Failure_Result_to_Option_returns_None()
    {
        var option = Result<int>.Failure(new ValidationResult("error")).ToOption();
        option.ShouldBeNone();
    }

    [Fact]
    public void Success_typed_Result_to_Option_returns_Some()
    {
        var option = Result<int, string>.Success(42).ToOption();
        option.ShouldBeSome(42);
    }

    [Fact]
    public void Failure_typed_Result_to_Option_returns_None()
    {
        var option = Result<int, string>.Failure("error").ToOption();
        option.ShouldBeNone();
    }
}

public class OptionResult_OrResult
{
    [Fact]
    public void Some_OrResult_returns_Success()
    {
        var result = Option.Some(42)
            .OrResult(() => Result<int>.Failure(new ValidationResult("fallback")));
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void None_OrResult_invokes_factory()
    {
        var result = Option.None<int>()
            .OrResult(() => Result<int>.Success(99));
        Assert.True(result.IsSuccess);
        Assert.Equal(99, result.Value);
    }
}

public class OptionResult_BindResult
{
    [Fact]
    public void Some_BindResult_with_Success_returns_Some()
    {
        var option = Option.Some(42)
            .BindResult(x => Result<string>.Success(x.ToString()));
        option.ShouldBeSome("42");
    }

    [Fact]
    public void Some_BindResult_with_Failure_returns_None()
    {
        var option = Option.Some(42)
            .BindResult(_ => Result<string>.Failure(new ValidationResult("error")));
        option.ShouldBeNone();
    }

    [Fact]
    public void None_BindResult_returns_None()
    {
        var called = false;
        var option = Option.None<int>()
            .BindResult(x => { called = true; return Result<string>.Success(x.ToString()); });
        option.ShouldBeNone();
        Assert.False(called);
    }
}
