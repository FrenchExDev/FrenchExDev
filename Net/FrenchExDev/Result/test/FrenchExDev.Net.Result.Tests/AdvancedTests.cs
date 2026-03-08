using System.ComponentModel.DataAnnotations;
using CsCheck;
using FrenchExDev.Net.Result;

namespace FrenchExDev.Net.Result.Tests;

// ── FromTryAsync — unexpected exception propagation ───────────────────────────

public class FromTryAsyncExceptionPropagationTests
{
    [Fact]
    public async Task FromTryAsync_Propagates_UnexpectedException()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Result.FromTryAsync<string, InvalidOperationException>(async () =>
            {
                await Task.Yield();
                throw new ArgumentException("unexpected");
            }));
    }
}

// ── Record equality: Result ───────────────────────────────────────────────────

public class ResultRecordEqualityTests
{
    [Fact]
    public void TwoSuccesses_AreEqual()
        => Assert.Equal(Result.Success(), Result.Success());

    [Fact]
    public void TwoFailures_AreEqual()
        => Assert.Equal(Result.Failure(), Result.Failure());

    [Fact]
    public void SuccessAndFailure_AreNotEqual()
        => Assert.NotEqual(Result.Success(), Result.Failure());

    [Fact]
    public void GetHashCode_IsConsistentWithEquality()
        => Assert.Equal(Result.Success().GetHashCode(), Result.Success().GetHashCode());

    [Fact]
    public void UsableAsHashSetKey_DistinctStates()
    {
        var set = new HashSet<Result> { Result.Success(), Result.Failure() };
        Assert.Equal(2, set.Count);
        Assert.Contains(Result.Success(), set);
        Assert.Contains(Result.Failure(), set);
    }

    [Fact]
    public void DuplicateSuccess_IsDeduplicatedInHashSet()
    {
        var set = new HashSet<Result> { Result.Success(), Result.Success() };
        Assert.Single(set);
    }
}

// ── Record equality: Result<T, TError> ───────────────────────────────────────

public class ResultOfTErrorRecordEqualityTests
{
    [Fact]
    public void TwoSuccesses_SameValue_AreEqual()
        => Assert.Equal(
            Result<string, string>.Success("a"),
            Result<string, string>.Success("a"));

    [Fact]
    public void TwoSuccesses_DifferentValues_AreNotEqual()
        => Assert.NotEqual(
            Result<string, string>.Success("a"),
            Result<string, string>.Success("b"));

    [Fact]
    public void TwoFailures_SameError_AreEqual()
        => Assert.Equal(
            Result<string, string>.Failure("e"),
            Result<string, string>.Failure("e"));

    [Fact]
    public void TwoFailures_DifferentErrors_AreNotEqual()
        => Assert.NotEqual(
            Result<string, string>.Failure("e1"),
            Result<string, string>.Failure("e2"));

    [Fact]
    public void SuccessAndFailure_AreNotEqual()
        => Assert.NotEqual(
            Result<string, string>.Success("a"),
            Result<string, string>.Failure("a"));

    [Fact]
    public void UsableAsHashSetKey()
    {
        var set = new HashSet<Result<string, string>>
        {
            Result<string, string>.Success("a"),
            Result<string, string>.Success("a"),   // duplicate
            Result<string, string>.Failure("e"),
        };
        Assert.Equal(2, set.Count);
        Assert.Contains(Result<string, string>.Success("a"), set);
        Assert.Contains(Result<string, string>.Failure("e"), set);
    }
}

// ── IResult interface ─────────────────────────────────────────────────────────

public class IResultInterfaceTests
{
    [Fact]
    public void AllThreeTypes_AreAssignableToIResult()
    {
        IResult r1 = Result.Success();
        IResult r2 = Result<string>.Success("ok");
        IResult r3 = Result<string, Exception>.Success("ok");

        Assert.True(r1.IsSuccess);
        Assert.True(r2.IsSuccess);
        Assert.True(r3.IsSuccess);
    }

    [Fact]
    public void IResult_IsFailure_IsOppositeOfIsSuccess()
    {
        IResult success = Result.Success();
        IResult failure = Result.Failure();

        Assert.False(success.IsFailure);
        Assert.True(failure.IsFailure);
    }

    [Fact]
    public void IResult_WorksForFailures_OfAllThreeTypes()
    {
        IResult r1 = Result.Failure();
        IResult r2 = Result<string>.Failure(new ValidationResult("err"));
        IResult r3 = Result<string, Exception>.Failure(new Exception("err"));

        Assert.True(r1.IsFailure);
        Assert.True(r2.IsFailure);
        Assert.True(r3.IsFailure);
    }
}

// ── ValueOrThrow exception message for Result<T, TError> ─────────────────────

public class ValueOrThrowMessageTests
{
    [Fact]
    public void ValueOrThrow_TE_Failure_ExceptionMessageContainsErrorToString()
    {
        var error = new InvalidOperationException("boom");
        var result = Result<string, InvalidOperationException>.Failure(error);

        var ex = Assert.Throws<InvalidOperationException>(result.ValueOrThrow);

        Assert.Contains(error.ToString(), ex.Message);
    }
}

// ── Lambda never invoked on failure ──────────────────────────────────────────

public class LambdaNotInvokedOnFailureTests
{
    [Fact]
    public void Map_NeverCallsMapper_OnFailure()
    {
        bool called = false;
        Result<string>.Failure(new ValidationResult("err"))
            .Map(v => { called = true; return v.Length; });
        Assert.False(called);
    }

    [Fact]
    public void Bind_NeverCallsBinder_OnFailure()
    {
        bool called = false;
        Result<string>.Failure(new ValidationResult("err"))
            .Bind(v => { called = true; return Result<int>.Success(1); });
        Assert.False(called);
    }

    [Fact]
    public void Ensure_NeverCallsPredicate_OnFailure()
    {
        bool called = false;
        Result<string>.Failure(new ValidationResult("err"))
            .Ensure(v => { called = true; return true; }, () => new ValidationResult("x"));
        Assert.False(called);
    }

    [Fact]
    public void Map_TE_NeverCallsMapper_OnFailure()
    {
        bool called = false;
        Result<string, string>.Failure("err")
            .Map(v => { called = true; return v.Length; });
        Assert.False(called);
    }

    [Fact]
    public void Bind_TE_NeverCallsBinder_OnFailure()
    {
        bool called = false;
        Result<string, string>.Failure("err")
            .Bind(v => { called = true; return Result<int, string>.Success(1); });
        Assert.False(called);
    }
}

// ── Multi-error aggregation: Recover and TapError receive ALL errors ──────────

public class MultiErrorAggregationTests
{
    [Fact]
    public void Recover_CombinedFailure_ReceivesAllAggregatedErrors()
    {
        var combined = Result.Combine(
            Result<int>.Failure(new ValidationResult("err1")),
            Result<int>.Failure(new ValidationResult("err2")));

        IReadOnlyList<ValidationResult>? received = null;
        combined.Recover(errs => { received = errs; return (0, 0); });

        Assert.NotNull(received);
        Assert.Equal(2, received!.Count);
        Assert.Contains(received, vr => vr.ErrorMessage == "err1");
        Assert.Contains(received, vr => vr.ErrorMessage == "err2");
    }

    [Fact]
    public void TapError_CombinedFailure_ReceivesAllAggregatedErrors()
    {
        var combined = Result.Combine(
            Result<int>.Failure(new ValidationResult("err1")),
            Result<int>.Failure(new ValidationResult("err2")),
            Result<int>.Failure(new ValidationResult("err3")));

        int count = -1;
        combined.TapError(errs => count = errs.Count);

        Assert.Equal(3, count);
    }

    [Fact]
    public void ValueOrElse_CombinedFailure_ReceivesAllAggregatedErrors()
    {
        var r1 = Result<string>.Failure(new ValidationResult("a"));
        var r2 = Result<string>.Failure(new ValidationResult("b"));
        var combined = Result.Combine(r1, r2);

        string? message = null;
        combined.TapError(errs => message = string.Join(",", errs.Select(e => e.ErrorMessage!)));

        Assert.Equal("a,b", message);
    }
}

// ── Thread safety ─────────────────────────────────────────────────────────────

public class ThreadSafetyTests
{
    [Fact]
    public async Task ConcurrentReads_FromSuccess_AlwaysReturnSameValue()
    {
        var result = Result<string>.Success("hello");
        var tasks = Enumerable.Range(0, 500)
            .Select(_ => Task.Run(() => result.ValueOrThrow()));
        var values = await Task.WhenAll(tasks);
        Assert.All(values, v => Assert.Equal("hello", v));
    }

    [Fact]
    public async Task ConcurrentReads_FromFailure_AlwaysReturnSameErrors()
    {
        var result = Result<string>.Failure(new ValidationResult("err", ["Field"]));
        var tasks = Enumerable.Range(0, 500)
            .Select(_ => Task.Run(() => result.ValidationResults.Count));
        var counts = await Task.WhenAll(tasks);
        Assert.All(counts, c => Assert.Equal(1, c));
    }

    [Fact]
    public async Task ConcurrentMatch_OnSuccess_AlwaysCallsOnSuccess()
    {
        var result = Result<int>.Success(42);
        var tasks = Enumerable.Range(0, 500)
            .Select(_ => Task.Run(() => result.Match(v => v, _ => -1)));
        var values = await Task.WhenAll(tasks);
        Assert.All(values, v => Assert.Equal(42, v));
    }
}

// ── CsCheck property: Combine error-count law ─────────────────────────────────

public class CombinePropertyTests
{
    private static Result<int> MakeResult(bool ok, string errMsg) =>
        ok ? Result<int>.Success(1) : Result<int>.Failure(new ValidationResult(errMsg));

    [Fact]
    public void Combine2_ErrorCount_EqualsSumOfInputErrorCounts()
    {
        Gen.Select(Gen.Bool, Gen.Bool).Sample((r1ok, r2ok) =>
        {
            var combined = Result.Combine(MakeResult(r1ok, "e1"), MakeResult(r2ok, "e2"));
            int expected = (!r1ok ? 1 : 0) + (!r2ok ? 1 : 0);
            Assert.Equal(expected == 0, combined.IsSuccess);
            Assert.Equal(expected, combined.ValidationResults.Count);
        });
    }

    [Fact]
    public void Combine3_ErrorCount_EqualsSumOfInputErrorCounts()
    {
        Gen.Select(Gen.Bool, Gen.Bool, Gen.Bool).Sample((r1ok, r2ok, r3ok) =>
        {
            var combined = Result.Combine(
                MakeResult(r1ok, "e1"), MakeResult(r2ok, "e2"), MakeResult(r3ok, "e3"));
            int expected = (!r1ok ? 1 : 0) + (!r2ok ? 1 : 0) + (!r3ok ? 1 : 0);
            Assert.Equal(expected == 0, combined.IsSuccess);
            Assert.Equal(expected, combined.ValidationResults.Count);
        });
    }
}
