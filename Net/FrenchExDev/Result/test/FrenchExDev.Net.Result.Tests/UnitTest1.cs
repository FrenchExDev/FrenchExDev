using System.ComponentModel.DataAnnotations;
using FrenchExDev.Net.Result;

namespace FrenchExDev.Net.Result.Tests;

// ── Result (no generic) ───────────────────────────────────────────────────────

public class ResultTests
{
    [Fact]
    public void Success_IsSuccess_True()
    {
        var result = Result.Success();
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
    }

    [Fact]
    public void Failure_IsFailure_True()
    {
        var result = Result.Failure();
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Match_CallsOnSuccess_WhenSuccess()
    {
        var result = Result.Success();
        var called = result.Match(onSuccess: () => "success", onFailure: () => "failure");
        Assert.Equal("success", called);
    }

    [Fact]
    public void Match_CallsOnFailure_WhenFailure()
    {
        var result = Result.Failure();
        var called = result.Match(onSuccess: () => "success", onFailure: () => "failure");
        Assert.Equal("failure", called);
    }

    [Fact]
    public async Task MatchAsync_CallsOnSuccess_WhenSuccess()
    {
        var result = Result.Success();
        var called = await result.MatchAsync(
            onSuccess: () => Task.FromResult("success"),
            onFailure: () => Task.FromResult("failure"));
        Assert.Equal("success", called);
    }

    [Fact]
    public async Task MatchAsync_CallsOnFailure_WhenFailure()
    {
        var result = Result.Failure();
        var called = await result.MatchAsync(
            onSuccess: () => Task.FromResult("success"),
            onFailure: () => Task.FromResult("failure"));
        Assert.Equal("failure", called);
    }
}

// ── Result<TResult> ───────────────────────────────────────────────────────────

public class ResultOfTTests
{
    [Fact]
    public void Success_HasValue_AndIsSuccess()
    {
        var result = Result<string>.Success("hello");
        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void Failure_HasValidationResult_AndIsFailure()
    {
        var vr = new ValidationResult("invalid", ["Field"]);
        var result = Result<string>.Failure(vr);
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.NotNull(result.ValidationResult);
        Assert.Equal("invalid", result.ValidationResult!.ErrorMessage);
    }

    [Fact]
    public void Failure_WithNullMemberNames_IsHandled()
    {
        var vr = new ValidationResult("err"); // MemberNames is null
        var result = Result<string>.Failure(vr);
        Assert.True(result.IsFailure);
        Assert.Equal("err", result.ValidationResult!.ErrorMessage);
    }

    [Fact]
    public void Failure_StoresDefensiveCopy_OfValidationResult()
    {
        var members = new List<string> { "Field" };
        var vr = new ValidationResult("err", members);
        var result = Result<string>.Failure(vr);

        // Mutate original — result must remain unchanged
        members.Add("Other");

        Assert.DoesNotContain("Other", result.ValidationResult!.MemberNames);
    }

    [Fact]
    public void ValueOrThrow_ReturnsValue_WhenSuccess()
    {
        var result = Result<int>.Success(42);
        Assert.Equal(42, result.ValueOrThrow());
    }

    [Fact]
    public void ValueOrThrow_Throws_WhenFailure()
    {
        var result = Result<int>.Failure(new ValidationResult("bad"));
        Assert.Throws<InvalidOperationException>(() => result.ValueOrThrow());
    }

    [Fact]
    public void Match_CallsOnSuccess_WithValue()
    {
        var result = Result<string>.Success("hi");
        var out_ = result.Match(v => v.ToUpper(), _ => "fail");
        Assert.Equal("HI", out_);
    }

    [Fact]
    public void Match_CallsOnFailure_WhenFailure()
    {
        var result = Result<string>.Failure(new ValidationResult("err"));
        var out_ = result.Match(v => v, _ => "fail");
        Assert.Equal("fail", out_);
    }

    [Fact]
    public void Match_OnFailure_ReceivesValidationResults()
    {
        var result = Result<string>.Failure(new ValidationResult("err", ["Field"]));
        IReadOnlyList<ValidationResult> errors = result.Match(_ => [], vrs => vrs);
        Assert.Single(errors);
        Assert.Equal("err", errors[0].ErrorMessage);
    }

    [Fact]
    public async Task MatchAsync_CallsOnSuccess_WithValue()
    {
        var result = Result<string>.Success("hi");
        var out_ = await result.MatchAsync(
            v => Task.FromResult(v.ToUpper()),
            _ => Task.FromResult("fail"));
        Assert.Equal("HI", out_);
    }

    [Fact]
    public async Task MatchAsync_CallsOnFailure_WhenFailure()
    {
        var result = Result<string>.Failure(new ValidationResult("err"));
        var out_ = await result.MatchAsync(
            v => Task.FromResult(v),
            _ => Task.FromResult("fail"));
        Assert.Equal("fail", out_);
    }

    [Fact]
    public async Task MatchAsync_OnFailure_ReceivesValidationResults()
    {
        var result = Result<string>.Failure(new ValidationResult("err"));
        var msg = await result.MatchAsync(
            v => Task.FromResult(v),
            vrs => Task.FromResult(vrs[0].ErrorMessage ?? ""));
        Assert.Equal("err", msg);
    }
}

// ── Result<TResult, TError> ───────────────────────────────────────────────────

public class ResultOfTErrorTests
{
    [Fact]
    public void Success_HasValue_AndIsSuccess()
    {
        var result = Result<string, InvalidOperationException>.Success("ok");
        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_HasError_AndIsFailure()
    {
        var ex = new InvalidOperationException("boom");
        var result = Result<string, InvalidOperationException>.Failure(ex);
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Same(ex, result.Error);
    }

    [Fact]
    public void Match_CallsOnSuccess_WithValue()
    {
        var result = Result<string, InvalidOperationException>.Success("hello");
        var out_ = result.Match(v => v.Length, e => -1);
        Assert.Equal(5, out_);
    }

    [Fact]
    public void Match_CallsOnFailure_WithError()
    {
        var ex = new InvalidOperationException("boom");
        var result = Result<string, InvalidOperationException>.Failure(ex);
        var out_ = result.Match(v => v, e => e.Message);
        Assert.Equal("boom", out_);
    }

    [Fact]
    public async Task MatchAsync_CallsOnSuccess_WithValue()
    {
        var result = Result<string, InvalidOperationException>.Success("hello");
        var out_ = await result.MatchAsync(
            v => Task.FromResult(v.Length),
            e => Task.FromResult(-1));
        Assert.Equal(5, out_);
    }

    [Fact]
    public async Task MatchAsync_CallsOnFailure_WithError()
    {
        var ex = new InvalidOperationException("boom");
        var result = Result<string, InvalidOperationException>.Failure(ex);
        var out_ = await result.MatchAsync(
            v => Task.FromResult(v),
            e => Task.FromResult(e.Message));
        Assert.Equal("boom", out_);
    }
}

// ── Extensions ────────────────────────────────────────────────────────────────

public class ResultExtensionsTests
{
    [Fact]
    public void Map_TransformsValue_OnSuccess()
    {
        var result = Result<string>.Success("hello").Map(v => v.Length);
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.ValueOrThrow());
    }

    [Fact]
    public void Map_PropagatesFailure_OnFailure()
    {
        var result = Result<string>.Failure(new ValidationResult("err")).Map(v => v.Length);
        Assert.True(result.IsFailure);
        Assert.Equal("err", result.ValidationResult!.ErrorMessage);
    }

    [Fact]
    public void Bind_ChainsResults_OnSuccess()
    {
        var result = Result<string>.Success("hello")
            .Bind(v => Result<int>.Success(v.Length));
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.ValueOrThrow());
    }

    [Fact]
    public void Bind_PropagatesFailure_OnFailure()
    {
        var result = Result<string>.Failure(new ValidationResult("err"))
            .Bind(v => Result<int>.Success(v.Length));
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task MapAsync_TransformsValue_OnSuccess()
    {
        var result = await Result<string>.Success("hello")
            .MapAsync(v => Task.FromResult(v.Length));
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.ValueOrThrow());
    }

    [Fact]
    public async Task MapAsync_PropagatesFailure_OnFailure()
    {
        var result = await Result<string>.Failure(new ValidationResult("err"))
            .MapAsync(v => Task.FromResult(v.Length));
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task BindAsync_ChainsResults_OnSuccess()
    {
        var result = await Result<string>.Success("hello")
            .BindAsync(v => Task.FromResult(Result<int>.Success(v.Length)));
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.ValueOrThrow());
    }

    [Fact]
    public async Task BindAsync_PropagatesFailure_OnFailure()
    {
        var result = await Result<string>.Failure(new ValidationResult("err"))
            .BindAsync(v => Task.FromResult(Result<int>.Success(v.Length)));
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Recover_ReturnsSuccess_FromFailure()
    {
        var result = Result<string>.Failure(new ValidationResult("err"))
            .Recover(_ => "fallback");
        Assert.True(result.IsSuccess);
        Assert.Equal("fallback", result.ValueOrThrow());
    }

    [Fact]
    public void Recover_LeavesSuccess_Unchanged()
    {
        var result = Result<string>.Success("original").Recover(_ => "fallback");
        Assert.Equal("original", result.ValueOrThrow());
    }

    [Fact]
    public void Map_TransformsValue_OnSuccess_Typed()
    {
        var result = Result<string, InvalidOperationException>.Success("hello")
            .Map(v => v.Length);
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public void Map_PropagatesError_OnFailure_Typed()
    {
        var ex = new InvalidOperationException("boom");
        var result = Result<string, InvalidOperationException>.Failure(ex)
            .Map(v => v.Length);
        Assert.True(result.IsFailure);
        Assert.Same(ex, result.Error);
    }

    [Fact]
    public void Bind_ChainsResults_OnSuccess_Typed()
    {
        var result = Result<string, InvalidOperationException>.Success("hello")
            .Bind(v => Result<int, InvalidOperationException>.Success(v.Length));
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public void Bind_PropagatesError_OnFailure_Typed()
    {
        var ex = new InvalidOperationException("boom");
        var result = Result<string, InvalidOperationException>.Failure(ex)
            .Bind(v => Result<int, InvalidOperationException>.Success(v.Length));
        Assert.True(result.IsFailure);
        Assert.Same(ex, result.Error);
    }

    [Fact]
    public async Task MapAsync_TransformsValue_OnSuccess_Typed()
    {
        var result = await Result<string, InvalidOperationException>.Success("hello")
            .MapAsync(v => Task.FromResult(v.Length));
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public async Task MapAsync_PropagatesError_OnFailure_Typed()
    {
        var ex = new InvalidOperationException("boom");
        var result = await Result<string, InvalidOperationException>.Failure(ex)
            .MapAsync(v => Task.FromResult(v.Length));
        Assert.True(result.IsFailure);
        Assert.Same(ex, result.Error);
    }

    [Fact]
    public async Task BindAsync_ChainsResults_OnSuccess_Typed()
    {
        var result = await Result<string, InvalidOperationException>.Success("hello")
            .BindAsync(v => Task.FromResult(Result<int, InvalidOperationException>.Success(v.Length)));
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public async Task BindAsync_PropagatesError_OnFailure_Typed()
    {
        var ex = new InvalidOperationException("boom");
        var result = await Result<string, InvalidOperationException>.Failure(ex)
            .BindAsync(v => Task.FromResult(Result<int, InvalidOperationException>.Success(v.Length)));
        Assert.True(result.IsFailure);
        Assert.Same(ex, result.Error);
    }

    [Fact]
    public void Recover_ReturnsSuccess_FromFailure_Typed()
    {
        var result = Result<string, InvalidOperationException>
            .Failure(new InvalidOperationException("err"))
            .Recover(e => "fallback: " + e.Message);
        Assert.True(result.IsSuccess);
        Assert.Equal("fallback: err", result.Value);
    }

    [Fact]
    public void Recover_LeavesSuccess_Unchanged_Typed()
    {
        var result = Result<string, InvalidOperationException>.Success("original")
            .Recover(e => "fallback");
        Assert.Equal("original", result.Value);
    }

    [Fact]
    public void FromTry_ReturnsSuccess_WhenNoException()
    {
        var result = Result.FromTry<string, InvalidOperationException>(() => "ok");
        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Value);
    }

    [Fact]
    public void FromTry_ReturnsFailure_WhenExpectedExceptionThrown()
    {
        var result = Result.FromTry<string, InvalidOperationException>(
            () => throw new InvalidOperationException("boom"));
        Assert.True(result.IsFailure);
        Assert.Equal("boom", result.Error!.Message);
    }

    [Fact]
    public void FromTry_Propagates_UnexpectedException()
    {
        Assert.Throws<ArgumentException>(() =>
            Result.FromTry<string, InvalidOperationException>(
                () => throw new ArgumentException("unexpected")));
    }

    [Fact]
    public async Task FromTryAsync_ReturnsSuccess_WhenNoException()
    {
        var result = await Result.FromTryAsync<string, InvalidOperationException>(
            () => Task.FromResult("ok"));
        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Value);
    }

    [Fact]
    public async Task FromTryAsync_ReturnsFailure_WhenExpectedExceptionThrown()
    {
        var result = await Result.FromTryAsync<string, InvalidOperationException>(
            async () =>
            {
                await Task.Yield();
                throw new InvalidOperationException("async boom");
            });
        Assert.True(result.IsFailure);
        Assert.Equal("async boom", result.Error!.Message);
    }
}

// ── Combine ───────────────────────────────────────────────────────────────────

public class CombineTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Result<int> ValidAge(int age) =>
        age is >= 0 and <= 150
            ? Result<int>.Success(age)
            : Result<int>.Failure(new ValidationResult("Age invalide", [nameof(age)]));

    private static Result<string> ValidName(string name) =>
        !string.IsNullOrWhiteSpace(name)
            ? Result<string>.Success(name.Trim())
            : Result<string>.Failure(new ValidationResult("Nom requis", [nameof(name)]));

    private static Result<string> ValidEmail(string email) =>
        email.Contains('@')
            ? Result<string>.Success(email)
            : Result<string>.Failure(new ValidationResult("Email invalide", [nameof(email)]));

    // ── Combine 2 ────────────────────────────────────────────────────────────

    [Fact]
    public void Combine2_AllSuccess_ReturnsSuccessTuple()
    {
        var result = Result.Combine(ValidAge(25), ValidName("Alice"));
        Assert.True(result.IsSuccess);
        Assert.Equal((25, "Alice"), result.ValueOrThrow());
    }

    [Fact]
    public void Combine2_FirstFails_ReturnsFailure()
    {
        var result = Result.Combine(ValidAge(-1), ValidName("Alice"));
        Assert.True(result.IsFailure);
        Assert.Single(result.ValidationResults);
        Assert.Equal("Age invalide", result.ValidationResults[0].ErrorMessage);
    }

    [Fact]
    public void Combine2_BothFail_AggregatesBothErrors()
    {
        var result = Result.Combine(ValidAge(-1), ValidName(""));
        Assert.True(result.IsFailure);
        Assert.Equal(2, result.ValidationResults.Count);
        Assert.Contains(result.ValidationResults, vr => vr.ErrorMessage == "Age invalide");
        Assert.Contains(result.ValidationResults, vr => vr.ErrorMessage == "Nom requis");
    }

    [Fact]
    public void Combine2_ValidationResult_IsFirstError()
    {
        var result = Result.Combine(ValidAge(-1), ValidName(""));
        Assert.NotNull(result.ValidationResult);
        Assert.Equal("Age invalide", result.ValidationResult!.ErrorMessage);
    }

    // ── Combine 3 ────────────────────────────────────────────────────────────

    [Fact]
    public void Combine3_AllSuccess_ReturnsSuccessTuple()
    {
        var result = Result.Combine(ValidAge(30), ValidName("Bob"), ValidEmail("bob@example.com"));
        Assert.True(result.IsSuccess);
        Assert.Equal((30, "Bob", "bob@example.com"), result.ValueOrThrow());
    }

    [Fact]
    public void Combine3_AllFail_AggregatesAllThreeErrors()
    {
        var result = Result.Combine(ValidAge(-1), ValidName(""), ValidEmail("not-an-email"));
        Assert.True(result.IsFailure);
        Assert.Equal(3, result.ValidationResults.Count);
    }

    // ── Combine 4-7 ─────────────────────────────────────────────────────────

    [Fact]
    public void Combine4_AllSuccess_ReturnsSuccessTuple()
    {
        var result = Result.Combine(
            ValidAge(20), ValidName("Carol"), ValidEmail("carol@x.com"),
            Result<bool>.Success(true));
        Assert.True(result.IsSuccess);
        var (age, name, email, flag) = result.ValueOrThrow();
        Assert.Equal(20, age);
        Assert.Equal("Carol", name);
        Assert.True(flag);
    }

    [Fact]
    public void Combine5_AllSuccess_ReturnsSuccessTuple()
    {
        var result = Result.Combine(
            Result<int>.Success(1), Result<int>.Success(2), Result<int>.Success(3),
            Result<int>.Success(4), Result<int>.Success(5));
        Assert.True(result.IsSuccess);
        Assert.Equal((1, 2, 3, 4, 5), result.ValueOrThrow());
    }

    [Fact]
    public void Combine6_AllSuccess_ReturnsSuccessTuple()
    {
        var result = Result.Combine(
            Result<int>.Success(1), Result<int>.Success(2), Result<int>.Success(3),
            Result<int>.Success(4), Result<int>.Success(5), Result<int>.Success(6));
        Assert.True(result.IsSuccess);
        Assert.Equal((1, 2, 3, 4, 5, 6), result.ValueOrThrow());
    }

    [Fact]
    public void Combine7_AllSuccess_ReturnsSuccessTuple()
    {
        var result = Result.Combine(
            Result<int>.Success(1), Result<int>.Success(2), Result<int>.Success(3),
            Result<int>.Success(4), Result<int>.Success(5), Result<int>.Success(6),
            Result<int>.Success(7));
        Assert.True(result.IsSuccess);
        Assert.Equal((1, 2, 3, 4, 5, 6, 7), result.ValueOrThrow());
    }

    [Fact]
    public void Combine4_OneFails_ReturnsFailure()
    {
        var result = Result.Combine(
            ValidAge(-1), ValidName("Dave"), ValidEmail("dave@x.com"),
            Result<bool>.Success(true));
        Assert.True(result.IsFailure);
        Assert.Single(result.ValidationResults);
    }

    [Fact]
    public void Combine5_MultipleFail_AggregatesErrors()
    {
        var result = Result.Combine(
            ValidAge(-1), ValidName(""), Result<string>.Success("ok"),
            Result<string>.Success("ok"), Result<string>.Success("ok"));
        Assert.Equal(2, result.ValidationResults.Count);
    }

    // ── Propagation through extensions ───────────────────────────────────────

    [Fact]
    public void Combine_Failure_PropagatesThroughMap()
    {
        var result = Result.Combine(ValidAge(-1), ValidName(""))
            .Map(t => t.Item1 + t.Item2.Length);
        Assert.True(result.IsFailure);
        Assert.Equal(2, result.ValidationResults.Count);
    }

    [Fact]
    public void Combine_Failure_PropagatesThroughBind()
    {
        var result = Result.Combine(ValidAge(-1), ValidName(""))
            .Bind(t => Result<int>.Success(t.Item1));
        Assert.True(result.IsFailure);
        Assert.Equal(2, result.ValidationResults.Count);
    }

    // ── Internal Failure guard ────────────────────────────────────────────────

    [Fact]
    public void InternalFailure_ThrowsArgumentException_WhenErrorsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            Result<string>.Failure([]));
    }

    // ── ValidationResults on regular Failure ─────────────────────────────────

    [Fact]
    public void SingleFailure_ValidationResults_HasOneEntry()
    {
        var result = Result<string>.Failure(new ValidationResult("err", ["Field"]));
        Assert.Single(result.ValidationResults);
        Assert.Equal("err", result.ValidationResults[0].ErrorMessage);
    }

    [Fact]
    public void Success_ValidationResults_IsEmpty()
    {
        var result = Result<string>.Success("ok");
        Assert.Empty(result.ValidationResults);
        Assert.Null(result.ValidationResult);
    }

    // ── Combine6 : each condition branch ─────────────────────────────────────
    // Each test exercises one specific &&-branch (ri is the first to fail)

    private static Result<string> Ok   => Result<string>.Success("ok");
    private static Result<string> Fail => Result<string>.Failure(new ValidationResult("err"));

    [Fact] public void Combine6_R1Fails() => Assert.True(Result.Combine(Fail, Ok, Ok, Ok, Ok, Ok).IsFailure);
    [Fact] public void Combine6_R2Fails() => Assert.True(Result.Combine(Ok, Fail, Ok, Ok, Ok, Ok).IsFailure);
    [Fact] public void Combine6_R3Fails() => Assert.True(Result.Combine(Ok, Ok, Fail, Ok, Ok, Ok).IsFailure);
    [Fact] public void Combine6_R4Fails() => Assert.True(Result.Combine(Ok, Ok, Ok, Fail, Ok, Ok).IsFailure);
    [Fact] public void Combine6_R5Fails() => Assert.True(Result.Combine(Ok, Ok, Ok, Ok, Fail, Ok).IsFailure);
    [Fact] public void Combine6_R6Fails() => Assert.True(Result.Combine(Ok, Ok, Ok, Ok, Ok, Fail).IsFailure);

    // ── Combine7 : each condition branch ─────────────────────────────────────

    [Fact] public void Combine7_R1Fails() => Assert.True(Result.Combine(Fail, Ok, Ok, Ok, Ok, Ok, Ok).IsFailure);
    [Fact] public void Combine7_R2Fails() => Assert.True(Result.Combine(Ok, Fail, Ok, Ok, Ok, Ok, Ok).IsFailure);
    [Fact] public void Combine7_R3Fails() => Assert.True(Result.Combine(Ok, Ok, Fail, Ok, Ok, Ok, Ok).IsFailure);
    [Fact] public void Combine7_R4Fails() => Assert.True(Result.Combine(Ok, Ok, Ok, Fail, Ok, Ok, Ok).IsFailure);
    [Fact] public void Combine7_R5Fails() => Assert.True(Result.Combine(Ok, Ok, Ok, Ok, Fail, Ok, Ok).IsFailure);
    [Fact] public void Combine7_R6Fails() => Assert.True(Result.Combine(Ok, Ok, Ok, Ok, Ok, Fail, Ok).IsFailure);
    [Fact] public void Combine7_R7Fails() => Assert.True(Result.Combine(Ok, Ok, Ok, Ok, Ok, Ok, Fail).IsFailure);
}

// ── Result<TResult, TError> — ValueOrThrow ────────────────────────────────────

public class ResultOfTErrorValueOrThrowTests
{
    [Fact]
    public void ValueOrThrow_ReturnsValue_WhenSuccess()
    {
        var result = Result<string, InvalidOperationException>.Success("ok");
        Assert.Equal("ok", result.ValueOrThrow());
    }

    [Fact]
    public void ValueOrThrow_Throws_WhenFailure()
    {
        var ex = new InvalidOperationException("boom");
        var result = Result<string, InvalidOperationException>.Failure(ex);
        Assert.Throws<InvalidOperationException>(result.ValueOrThrow);
    }
}

// ── Then (alias for Bind) ─────────────────────────────────────────────────────

public class ThenTests
{
    [Fact]
    public void Then_ChainsResults_OnSuccess()
    {
        var result = Result<string>.Success("hello")
            .Then(v => Result<int>.Success(v.Length));
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.ValueOrThrow());
    }

    [Fact]
    public void Then_PropagatesFailure_OnFailure()
    {
        var result = Result<string>.Failure(new ValidationResult("err"))
            .Then(v => Result<int>.Success(v.Length));
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ThenAsync_ChainsResults_OnSuccess()
    {
        var result = await Result<string>.Success("hello")
            .ThenAsync(v => Task.FromResult(Result<int>.Success(v.Length)));
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.ValueOrThrow());
    }

    [Fact]
    public async Task ThenAsync_PropagatesFailure_OnFailure()
    {
        var result = await Result<string>.Failure(new ValidationResult("err"))
            .ThenAsync(v => Task.FromResult(Result<int>.Success(v.Length)));
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Then_ChainsResults_OnSuccess_Typed()
    {
        var result = Result<string, InvalidOperationException>.Success("hello")
            .Then(v => Result<int, InvalidOperationException>.Success(v.Length));
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public void Then_PropagatesError_OnFailure_Typed()
    {
        var ex = new InvalidOperationException("boom");
        var result = Result<string, InvalidOperationException>.Failure(ex)
            .Then(v => Result<int, InvalidOperationException>.Success(v.Length));
        Assert.True(result.IsFailure);
        Assert.Same(ex, result.Error);
    }

    [Fact]
    public async Task ThenAsync_ChainsResults_OnSuccess_Typed()
    {
        var result = await Result<string, InvalidOperationException>.Success("hello")
            .ThenAsync(v => Task.FromResult(Result<int, InvalidOperationException>.Success(v.Length)));
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public async Task ThenAsync_PropagatesError_OnFailure_Typed()
    {
        var ex = new InvalidOperationException("boom");
        var result = await Result<string, InvalidOperationException>.Failure(ex)
            .ThenAsync(v => Task.FromResult(Result<int, InvalidOperationException>.Success(v.Length)));
        Assert.True(result.IsFailure);
        Assert.Same(ex, result.Error);
    }
}

// ── Tap / TapError ────────────────────────────────────────────────────────────

public class TapTests
{
    [Fact]
    public void Tap_ExecutesAction_OnSuccess()
    {
        var captured = "";
        var result = Result<string>.Success("hello").Tap(v => captured = v);
        Assert.Equal("hello", captured);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Tap_DoesNotExecute_OnFailure()
    {
        var called = false;
        var result = Result<string>.Failure(new ValidationResult("err")).Tap(_ => { called = true; });
        Assert.False(called);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TapAsync_ExecutesAction_OnSuccess()
    {
        var captured = "";
        var result = await Result<string>.Success("hello")
            .TapAsync(v => { captured = v; return Task.CompletedTask; });
        Assert.Equal("hello", captured);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TapAsync_DoesNotExecute_OnFailure()
    {
        var called = false;
        var result = await Result<string>.Failure(new ValidationResult("err"))
            .TapAsync(_ => { called = true; return Task.CompletedTask; });
        Assert.False(called);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void TapError_ExecutesAction_OnFailure()
    {
        var captured = "";
        var result = Result<string>.Failure(new ValidationResult("err"))
            .TapError(vrs => captured = vrs[0].ErrorMessage ?? "");
        Assert.Equal("err", captured);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void TapError_DoesNotExecute_OnSuccess()
    {
        var called = false;
        var result = Result<string>.Success("ok").TapError(_ => { called = true; });
        Assert.False(called);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TapErrorAsync_ExecutesAction_OnFailure()
    {
        var captured = "";
        var result = await Result<string>.Failure(new ValidationResult("err"))
            .TapErrorAsync(vrs => { captured = vrs[0].ErrorMessage ?? ""; return Task.CompletedTask; });
        Assert.Equal("err", captured);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TapErrorAsync_DoesNotExecute_OnSuccess()
    {
        var called = false;
        var result = await Result<string>.Success("ok")
            .TapErrorAsync(_ => { called = true; return Task.CompletedTask; });
        Assert.False(called);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Tap_ExecutesAction_OnSuccess_Typed()
    {
        var captured = "";
        var result = Result<string, InvalidOperationException>.Success("hello")
            .Tap(v => captured = v);
        Assert.Equal("hello", captured);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Tap_DoesNotExecute_OnFailure_Typed()
    {
        var called = false;
        var result = Result<string, InvalidOperationException>
            .Failure(new InvalidOperationException("err")).Tap(_ => { called = true; });
        Assert.False(called);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TapAsync_ExecutesAction_OnSuccess_Typed()
    {
        var captured = "";
        var result = await Result<string, InvalidOperationException>.Success("hello")
            .TapAsync(v => { captured = v; return Task.CompletedTask; });
        Assert.Equal("hello", captured);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TapAsync_DoesNotExecute_OnFailure_Typed()
    {
        var called = false;
        var result = await Result<string, InvalidOperationException>
            .Failure(new InvalidOperationException("err"))
            .TapAsync(_ => { called = true; return Task.CompletedTask; });
        Assert.False(called);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void TapError_ExecutesAction_OnFailure_Typed()
    {
        var captured = "";
        var result = Result<string, InvalidOperationException>
            .Failure(new InvalidOperationException("boom"))
            .TapError(e => captured = e.Message);
        Assert.Equal("boom", captured);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void TapError_DoesNotExecute_OnSuccess_Typed()
    {
        var called = false;
        var result = Result<string, InvalidOperationException>.Success("ok")
            .TapError(_ => { called = true; });
        Assert.False(called);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TapErrorAsync_ExecutesAction_OnFailure_Typed()
    {
        var captured = "";
        var result = await Result<string, InvalidOperationException>
            .Failure(new InvalidOperationException("boom"))
            .TapErrorAsync(e => { captured = e.Message; return Task.CompletedTask; });
        Assert.Equal("boom", captured);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TapErrorAsync_DoesNotExecute_OnSuccess_Typed()
    {
        var called = false;
        var result = await Result<string, InvalidOperationException>.Success("ok")
            .TapErrorAsync(_ => { called = true; return Task.CompletedTask; });
        Assert.False(called);
        Assert.True(result.IsSuccess);
    }
}

// ── Ensure ────────────────────────────────────────────────────────────────────

public class EnsureTests
{
    [Fact]
    public void Ensure_ReturnsSuccess_WhenPredicatePasses()
    {
        var result = Result<int>.Success(10).Ensure(v => v > 0, () => new ValidationResult("negative"));
        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.ValueOrThrow());
    }

    [Fact]
    public void Ensure_ReturnsFailure_WhenPredicateFails()
    {
        var result = Result<int>.Success(-1).Ensure(v => v > 0, () => new ValidationResult("negative"));
        Assert.True(result.IsFailure);
        Assert.Equal("negative", result.ValidationResult!.ErrorMessage);
    }

    [Fact]
    public void Ensure_PropagatesFailure_WhenAlreadyFailed()
    {
        var result = Result<int>.Failure(new ValidationResult("original"))
            .Ensure(v => v > 0, () => new ValidationResult("negative"));
        Assert.True(result.IsFailure);
        Assert.Equal("original", result.ValidationResult!.ErrorMessage);
    }

    [Fact]
    public void Ensure_WithFactory_ReturnsFailure_WhenPredicateFails()
    {
        var result = Result<int>.Success(-5)
            .Ensure(v => v > 0, v => new ValidationResult($"{v} is negative"));
        Assert.True(result.IsFailure);
        Assert.Equal("-5 is negative", result.ValidationResult!.ErrorMessage);
    }

    [Fact]
    public void Ensure_WithFactory_ReturnsSuccess_WhenPredicatePasses()
    {
        var result = Result<int>.Success(5)
            .Ensure(v => v > 0, v => new ValidationResult($"{v} is negative"));
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.ValueOrThrow());
    }

    [Fact]
    public void Ensure_WithFactory_PropagatesFailure_WhenAlreadyFailed()
    {
        var result = Result<int>.Failure(new ValidationResult("original"))
            .Ensure(v => v > 0, v => new ValidationResult($"{v} is negative"));
        Assert.True(result.IsFailure);
        Assert.Equal("original", result.ValidationResult!.ErrorMessage);
    }
}

// ── ValueOrDefault / ValueOrElse ─────────────────────────────────────────────

public class ValueOrTests
{
    [Fact]
    public void ValueOrDefault_ReturnsValue_OnSuccess()
    {
        var result = Result<string>.Success("hello").ValueOrDefault("fallback");
        Assert.Equal("hello", result);
    }

    [Fact]
    public void ValueOrDefault_ReturnsFallback_OnFailure()
    {
        var result = Result<string>.Failure(new ValidationResult("err")).ValueOrDefault("fallback");
        Assert.Equal("fallback", result);
    }

    [Fact]
    public void ValueOrElse_ReturnsValue_OnSuccess()
    {
        var result = Result<string>.Success("hello").ValueOrElse(_ => "fallback");
        Assert.Equal("hello", result);
    }

    [Fact]
    public void ValueOrElse_ComputesFallback_OnFailure()
    {
        var result = Result<string>.Failure(new ValidationResult("err", ["F"]))
            .ValueOrElse(vrs => "error: " + (vrs[0].ErrorMessage ?? ""));
        Assert.Equal("error: err", result);
    }

    [Fact]
    public void ValueOrDefault_ReturnsValue_OnSuccess_Typed()
    {
        var result = Result<string, InvalidOperationException>.Success("hello")
            .ValueOrDefault("fallback");
        Assert.Equal("hello", result);
    }

    [Fact]
    public void ValueOrDefault_ReturnsFallback_OnFailure_Typed()
    {
        var result = Result<string, InvalidOperationException>
            .Failure(new InvalidOperationException("err"))
            .ValueOrDefault("fallback");
        Assert.Equal("fallback", result);
    }

    [Fact]
    public void ValueOrElse_ReturnsValue_OnSuccess_Typed()
    {
        var result = Result<string, InvalidOperationException>.Success("hello")
            .ValueOrElse(e => "error: " + e.Message);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void ValueOrElse_ComputesFallback_OnFailure_Typed()
    {
        var result = Result<string, InvalidOperationException>
            .Failure(new InvalidOperationException("boom"))
            .ValueOrElse(e => "error: " + e.Message);
        Assert.Equal("error: boom", result);
    }
}

// ── RecoverAsync ──────────────────────────────────────────────────────────────

public class RecoverAsyncTests
{
    [Fact]
    public async Task RecoverAsync_ReturnsSuccess_FromFailure()
    {
        var result = await Result<string>.Failure(new ValidationResult("err"))
            .RecoverAsync(_ => Task.FromResult("fallback"));
        Assert.True(result.IsSuccess);
        Assert.Equal("fallback", result.ValueOrThrow());
    }

    [Fact]
    public async Task RecoverAsync_LeavesSuccess_Unchanged()
    {
        var result = await Result<string>.Success("original")
            .RecoverAsync(_ => Task.FromResult("fallback"));
        Assert.Equal("original", result.ValueOrThrow());
    }

    [Fact]
    public async Task RecoverAsync_ReturnsSuccess_FromFailure_Typed()
    {
        var result = await Result<string, InvalidOperationException>
            .Failure(new InvalidOperationException("boom"))
            .RecoverAsync(e => Task.FromResult("fallback: " + e.Message));
        Assert.True(result.IsSuccess);
        Assert.Equal("fallback: boom", result.Value);
    }

    [Fact]
    public async Task RecoverAsync_LeavesSuccess_Unchanged_Typed()
    {
        var result = await Result<string, InvalidOperationException>.Success("original")
            .RecoverAsync(e => Task.FromResult("fallback"));
        Assert.Equal("original", result.Value);
    }
}

// ── Task<Result<T>> and Task<Result<T, TError>> pipeline ─────────────────────

public class TaskPipelineTests
{
    private static Task<Result<string>> SuccessTask(string v) =>
        Task.FromResult(Result<string>.Success(v));

    private static Task<Result<string>> FailureTask() =>
        Task.FromResult(Result<string>.Failure(new ValidationResult("err")));

    private static Task<Result<string, InvalidOperationException>> SuccessTypedTask(string v) =>
        Task.FromResult(Result<string, InvalidOperationException>.Success(v));

    private static Task<Result<string, InvalidOperationException>> FailureTypedTask() =>
        Task.FromResult(Result<string, InvalidOperationException>.Failure(
            new InvalidOperationException("boom")));

    // ── Task<Result<T>> ───────────────────────────────────────────────────────

    [Fact]
    public async Task TaskPipeline_MapAsync_Sync_OnSuccess()
    {
        var result = await SuccessTask("hello").MapAsync(v => v.Length);
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.ValueOrThrow());
    }

    [Fact]
    public async Task TaskPipeline_MapAsync_Sync_OnFailure()
    {
        var result = await FailureTask().MapAsync(v => v.Length);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TaskPipeline_MapAsync_Async_OnSuccess()
    {
        var result = await SuccessTask("hello").MapAsync(v => Task.FromResult(v.Length));
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.ValueOrThrow());
    }

    [Fact]
    public async Task TaskPipeline_MapAsync_Async_OnFailure()
    {
        var result = await FailureTask().MapAsync(v => Task.FromResult(v.Length));
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TaskPipeline_BindAsync_Sync_OnSuccess()
    {
        var result = await SuccessTask("hello")
            .BindAsync(v => Result<int>.Success(v.Length));
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.ValueOrThrow());
    }

    [Fact]
    public async Task TaskPipeline_BindAsync_Sync_OnFailure()
    {
        var result = await FailureTask()
            .BindAsync(v => Result<int>.Success(v.Length));
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TaskPipeline_BindAsync_Async_OnSuccess()
    {
        var result = await SuccessTask("hello")
            .BindAsync(v => Task.FromResult(Result<int>.Success(v.Length)));
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.ValueOrThrow());
    }

    [Fact]
    public async Task TaskPipeline_BindAsync_Async_OnFailure()
    {
        var result = await FailureTask()
            .BindAsync(v => Task.FromResult(Result<int>.Success(v.Length)));
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TaskPipeline_ThenAsync_OnSuccess()
    {
        var result = await SuccessTask("hello")
            .ThenAsync(v => Result<int>.Success(v.Length));
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.ValueOrThrow());
    }

    [Fact]
    public async Task TaskPipeline_ThenAsync_OnFailure()
    {
        var result = await FailureTask()
            .ThenAsync(v => Result<int>.Success(v.Length));
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TaskPipeline_RecoverAsync_OnFailure()
    {
        var result = await FailureTask().RecoverAsync(_ => "fallback");
        Assert.True(result.IsSuccess);
        Assert.Equal("fallback", result.ValueOrThrow());
    }

    [Fact]
    public async Task TaskPipeline_RecoverAsync_OnSuccess()
    {
        var result = await SuccessTask("hello").RecoverAsync(_ => "fallback");
        Assert.Equal("hello", result.ValueOrThrow());
    }

    [Fact]
    public async Task TaskPipeline_TapAsync_OnSuccess()
    {
        var captured = "";
        var result = await SuccessTask("hello").TapAsync(v => { captured = v; });
        Assert.Equal("hello", captured);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TaskPipeline_TapAsync_OnFailure()
    {
        var called = false;
        var result = await FailureTask().TapAsync(_ => { called = true; });
        Assert.False(called);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TaskPipeline_TapErrorAsync_OnFailure()
    {
        var captured = "";
        var result = await FailureTask()
            .TapErrorAsync(vrs => { captured = vrs[0].ErrorMessage ?? ""; });
        Assert.Equal("err", captured);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TaskPipeline_TapErrorAsync_OnSuccess()
    {
        var called = false;
        var result = await SuccessTask("hello").TapErrorAsync(_ => { called = true; });
        Assert.False(called);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TaskPipeline_MatchAsync_OnSuccess()
    {
        var out_ = await SuccessTask("hello")
            .MatchAsync(v => v.ToUpper(), _ => "FAIL");
        Assert.Equal("HELLO", out_);
    }

    [Fact]
    public async Task TaskPipeline_MatchAsync_OnFailure()
    {
        var out_ = await FailureTask()
            .MatchAsync(v => v, _ => "FAIL");
        Assert.Equal("FAIL", out_);
    }

    // ── Task<Result<T, TError>> ───────────────────────────────────────────────

    [Fact]
    public async Task TaskPipeline_MapAsync_Sync_OnSuccess_Typed()
    {
        var result = await SuccessTypedTask("hello").MapAsync(v => v.Length);
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public async Task TaskPipeline_MapAsync_Sync_OnFailure_Typed()
    {
        var result = await FailureTypedTask().MapAsync(v => v.Length);
        Assert.True(result.IsFailure);
        Assert.Equal("boom", result.Error!.Message);
    }

    [Fact]
    public async Task TaskPipeline_MapAsync_Async_OnSuccess_Typed()
    {
        var result = await SuccessTypedTask("hello").MapAsync(v => Task.FromResult(v.Length));
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public async Task TaskPipeline_MapAsync_Async_OnFailure_Typed()
    {
        var result = await FailureTypedTask().MapAsync(v => Task.FromResult(v.Length));
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TaskPipeline_BindAsync_Sync_OnSuccess_Typed()
    {
        var result = await SuccessTypedTask("hello")
            .BindAsync(v => Result<int, InvalidOperationException>.Success(v.Length));
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public async Task TaskPipeline_BindAsync_Sync_OnFailure_Typed()
    {
        var ex = new InvalidOperationException("boom");
        var result = await Task.FromResult(Result<string, InvalidOperationException>.Failure(ex))
            .BindAsync(v => Result<int, InvalidOperationException>.Success(v.Length));
        Assert.True(result.IsFailure);
        Assert.Same(ex, result.Error);
    }

    [Fact]
    public async Task TaskPipeline_BindAsync_Async_OnSuccess_Typed()
    {
        var result = await SuccessTypedTask("hello")
            .BindAsync(v => Task.FromResult(Result<int, InvalidOperationException>.Success(v.Length)));
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public async Task TaskPipeline_BindAsync_Async_OnFailure_Typed()
    {
        var result = await FailureTypedTask()
            .BindAsync(v => Task.FromResult(Result<int, InvalidOperationException>.Success(v.Length)));
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TaskPipeline_ThenAsync_OnSuccess_Typed()
    {
        var result = await SuccessTypedTask("hello")
            .ThenAsync(v => Result<int, InvalidOperationException>.Success(v.Length));
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public async Task TaskPipeline_ThenAsync_OnFailure_Typed()
    {
        var result = await FailureTypedTask()
            .ThenAsync(v => Result<int, InvalidOperationException>.Success(v.Length));
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TaskPipeline_RecoverAsync_OnFailure_Typed()
    {
        var result = await FailureTypedTask().RecoverAsync(e => "fallback: " + e.Message);
        Assert.True(result.IsSuccess);
        Assert.Equal("fallback: boom", result.Value);
    }

    [Fact]
    public async Task TaskPipeline_RecoverAsync_OnSuccess_Typed()
    {
        var result = await SuccessTypedTask("hello").RecoverAsync(e => "fallback");
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public async Task TaskPipeline_TapAsync_OnSuccess_Typed()
    {
        var captured = "";
        var result = await SuccessTypedTask("hello").TapAsync(v => { captured = v; });
        Assert.Equal("hello", captured);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TaskPipeline_TapAsync_OnFailure_Typed()
    {
        var called = false;
        var result = await FailureTypedTask().TapAsync(_ => { called = true; });
        Assert.False(called);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TaskPipeline_TapErrorAsync_OnFailure_Typed()
    {
        var captured = "";
        var result = await FailureTypedTask().TapErrorAsync(e => { captured = e.Message; });
        Assert.Equal("boom", captured);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TaskPipeline_TapErrorAsync_OnSuccess_Typed()
    {
        var called = false;
        var result = await SuccessTypedTask("hello").TapErrorAsync(_ => { called = true; });
        Assert.False(called);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TaskPipeline_MatchAsync_OnSuccess_Typed()
    {
        var out_ = await SuccessTypedTask("hello")
            .MatchAsync(v => v.ToUpper(), e => e.Message);
        Assert.Equal("HELLO", out_);
    }

    [Fact]
    public async Task TaskPipeline_MatchAsync_OnFailure_Typed()
    {
        var out_ = await FailureTypedTask()
            .MatchAsync(v => v, e => e.Message);
        Assert.Equal("boom", out_);
    }
}
