using System.ComponentModel.DataAnnotations;
using CsCheck;
using FrenchExDev.Net.Result;

namespace FrenchExDev.Net.Result.Tests;

// ── Shared generators ─────────────────────────────────────────────────────────

file static class Gens
{
    internal static readonly Gen<string> Value =
        Gen.String.Select(s => s ?? "v");

    internal static readonly Gen<Result<string>> Success =
        Value.Select(Result<string>.Success);

    internal static readonly Gen<Result<string>> Failure =
        Gen.String.Select(msg => Result<string>.Failure(new ValidationResult(msg ?? "err")));

    internal static readonly Gen<Result<string>> AnyResult =
        Gen.OneOf(Success, Failure);

    internal static readonly Gen<Result<string, string>> SuccessTE =
        Value.Select(Result<string, string>.Success);

    internal static readonly Gen<Result<string, string>> FailureTE =
        Gen.String.Select(e => Result<string, string>.Failure(e ?? "err"));

    internal static readonly Gen<Result<string, string>> AnyResultTE =
        Gen.OneOf(SuccessTE, FailureTE);
}

// ── Functor laws: Result<string> ──────────────────────────────────────────────

public class ResultOfT_FunctorLaws
{
    [Fact]
    public void MapIdentity_PreservesObservableBehavior()
    {
        Gens.AnyResult.Sample(result =>
        {
            var mapped = result.Map(x => x);
            Assert.Equal(result.IsSuccess, mapped.IsSuccess);
            if (result.IsSuccess)
                Assert.Equal(result.Value, mapped.Value);
            else
                Assert.Equal(result.ValidationResults.Count, mapped.ValidationResults.Count);
        });
    }

    [Fact]
    public void MapComposition_EqualsFusedMap()
    {
        Gens.AnyResult.Sample(result =>
        {
            Func<string, string> f = s => s + "_f";
            Func<string, string> g = s => "[" + s + "]";
            var composed = result.Map(f).Map(g);
            var fused    = result.Map(x => g(f(x)));
            Assert.Equal(composed.IsSuccess, fused.IsSuccess);
            if (composed.IsSuccess)
                Assert.Equal(composed.Value, fused.Value);
        });
    }
}

// ── Monad laws: Result<string> ────────────────────────────────────────────────

public class ResultOfT_MonadLaws
{
    [Fact]
    public void BindLeftIdentity_SuccessBindF_EqualsFApplied()
    {
        Gens.Value.Sample(v =>
        {
            Func<string, Result<string>> f = s => Result<string>.Success(s + "_f");
            var leftSide  = Result<string>.Success(v).Bind(f);
            var rightSide = f(v);
            Assert.Equal(leftSide.IsSuccess, rightSide.IsSuccess);
            Assert.Equal(leftSide.Value, rightSide.Value);
        });
    }

    [Fact]
    public void BindRightIdentity_BindReturnIsNoOp()
    {
        Gens.AnyResult.Sample(result =>
        {
            var bound = result.Bind(Result<string>.Success);
            Assert.Equal(result.IsSuccess, bound.IsSuccess);
            if (result.IsSuccess)
                Assert.Equal(result.Value, bound.Value);
        });
    }

    [Fact]
    public void BindAssociativity_HoldsForAnyResult()
    {
        Gens.AnyResult.Sample(result =>
        {
            Func<string, Result<string>> f = s => Result<string>.Success(s + "_f");
            Func<string, Result<string>> g = s => Result<string>.Success(s + "_g");
            var leftSide  = result.Bind(f).Bind(g);
            var rightSide = result.Bind(x => f(x).Bind(g));
            Assert.Equal(leftSide.IsSuccess, rightSide.IsSuccess);
            if (leftSide.IsSuccess)
                Assert.Equal(leftSide.Value, rightSide.Value);
        });
    }
}

// ── Tap: Result<string> ───────────────────────────────────────────────────────

public class ResultOfT_TapProperties
{
    [Fact]
    public void Tap_OnSuccess_RunsSideEffectAndReturnsUnchanged()
    {
        Gens.Success.Sample(result =>
        {
            string? seen = null;
            var after = result.Tap(v => seen = v);
            Assert.Equal(result.Value, seen);
            Assert.True(after.IsSuccess);
            Assert.Equal(result.Value, after.Value);
        });
    }

    [Fact]
    public void Tap_OnFailure_DoesNotRunSideEffect()
    {
        Gens.Failure.Sample(result =>
        {
            bool ran = false;
            var after = result.Tap(_ => ran = true);
            Assert.False(ran);
            Assert.True(after.IsFailure);
        });
    }

    [Fact]
    public void TapError_OnFailure_RunsSideEffectAndReturnsUnchanged()
    {
        Gens.Failure.Sample(result =>
        {
            int errorCount = -1;
            var after = result.TapError(errs => errorCount = errs.Count);
            Assert.Equal(result.ValidationResults.Count, errorCount);
            Assert.True(after.IsFailure);
        });
    }

    [Fact]
    public void TapError_OnSuccess_DoesNotRunSideEffect()
    {
        Gens.Success.Sample(result =>
        {
            bool ran = false;
            var after = result.TapError(_ => ran = true);
            Assert.False(ran);
            Assert.True(after.IsSuccess);
        });
    }
}

// ── Ensure: Result<string> ────────────────────────────────────────────────────

public class ResultOfT_EnsureProperties
{
    [Fact]
    public void Ensure_AlwaysTrue_LeavesSuccessUnchanged()
    {
        Gens.Success.Sample(result =>
        {
            var after = result.Ensure(_ => true, () => new ValidationResult("x"));
            Assert.True(after.IsSuccess);
            Assert.Equal(result.Value, after.Value);
        });
    }

    [Fact]
    public void Ensure_AlwaysFalse_ConvertsSuccessToFailure()
    {
        Gens.Success.Sample(result =>
        {
            var after = result.Ensure(_ => false, () => new ValidationResult("fail"));
            Assert.True(after.IsFailure);
            Assert.Single(after.ValidationResults);
        });
    }

    [Fact]
    public void Ensure_OnFailure_AlwaysPropagatesFailure()
    {
        Gens.Failure.Sample(result =>
        {
            var after = result.Ensure(_ => true, () => new ValidationResult("x"));
            Assert.True(after.IsFailure);
            Assert.Equal(result.ValidationResults.Count, after.ValidationResults.Count);
        });
    }
}

// ── ValueOrDefault / ValueOrElse: Result<string> ─────────────────────────────

public class ResultOfT_ValueOrProperties
{
    [Fact]
    public void ValueOrDefault_OnSuccess_ReturnsValue()
    {
        Gen.Select(Gens.Success, Gens.Value).Sample((result, fallback) =>
            Assert.Equal(result.Value, result.ValueOrDefault(fallback)));
    }

    [Fact]
    public void ValueOrDefault_OnFailure_ReturnsFallback()
    {
        Gen.Select(Gens.Failure, Gens.Value).Sample((result, fallback) =>
            Assert.Equal(fallback, result.ValueOrDefault(fallback)));
    }

    [Fact]
    public void ValueOrElse_OnSuccess_ReturnsValue()
    {
        Gens.Success.Sample(result =>
            Assert.Equal(result.Value, result.ValueOrElse(_ => "fallback")));
    }

    [Fact]
    public void ValueOrElse_OnFailure_InvokesFallbackWithErrors()
    {
        Gens.Failure.Sample(result =>
        {
            int passedCount = -1;
            var value = result.ValueOrElse(errs => { passedCount = errs.Count; return "fb"; });
            Assert.Equal("fb", value);
            Assert.Equal(result.ValidationResults.Count, passedCount);
        });
    }
}

// ── Recover: Result<string> ───────────────────────────────────────────────────

public class ResultOfT_RecoverProperties
{
    [Fact]
    public void Recover_OnSuccess_IsNoOp()
    {
        Gens.Success.Sample(result =>
        {
            var recovered = result.Recover(_ => "fallback");
            Assert.True(recovered.IsSuccess);
            Assert.Equal(result.Value, recovered.Value);
        });
    }

    [Fact]
    public void Recover_OnFailure_YieldsSuccess()
    {
        Gens.Failure.Sample(result =>
        {
            var recovered = result.Recover(_ => "fallback");
            Assert.True(recovered.IsSuccess);
            Assert.Equal("fallback", recovered.Value);
        });
    }
}

// ── Functor + Monad laws: Result<string, string> ──────────────────────────────

public class ResultOfTError_FunctorAndMonadLaws
{
    [Fact]
    public void MapIdentity_TE_PreservesObservableBehavior()
    {
        Gens.AnyResultTE.Sample(result =>
        {
            var mapped = result.Map(x => x);
            Assert.Equal(result.IsSuccess, mapped.IsSuccess);
            if (result.IsSuccess)
                Assert.Equal(result.Value, mapped.Value);
            else
                Assert.Equal(result.Error, mapped.Error);
        });
    }

    [Fact]
    public void MapComposition_TE_EqualsFusedMap()
    {
        Gens.AnyResultTE.Sample(result =>
        {
            Func<string, string> f = s => s + "_f";
            Func<string, string> g = s => "[" + s + "]";
            var composed = result.Map(f).Map(g);
            var fused    = result.Map(x => g(f(x)));
            Assert.Equal(composed.IsSuccess, fused.IsSuccess);
            if (composed.IsSuccess)
                Assert.Equal(composed.Value, fused.Value);
            else
                Assert.Equal(composed.Error, fused.Error);
        });
    }

    [Fact]
    public void BindLeftIdentity_TE_SuccessBindF_EqualsFApplied()
    {
        Gens.Value.Sample(v =>
        {
            Func<string, Result<string, string>> f = s => Result<string, string>.Success(s + "_f");
            var leftSide  = Result<string, string>.Success(v).Bind(f);
            var rightSide = f(v);
            Assert.Equal(leftSide.IsSuccess, rightSide.IsSuccess);
            Assert.Equal(leftSide.Value, rightSide.Value);
        });
    }

    [Fact]
    public void BindRightIdentity_TE_BindReturnIsNoOp()
    {
        Gens.AnyResultTE.Sample(result =>
        {
            var bound = result.Bind(Result<string, string>.Success);
            Assert.Equal(result.IsSuccess, bound.IsSuccess);
            if (result.IsSuccess)
                Assert.Equal(result.Value, bound.Value);
            else
                Assert.Equal(result.Error, bound.Error);
        });
    }

    [Fact]
    public void BindAssociativity_TE_HoldsForAnyResult()
    {
        Gens.AnyResultTE.Sample(result =>
        {
            Func<string, Result<string, string>> f = s => Result<string, string>.Success(s + "_f");
            Func<string, Result<string, string>> g = s => Result<string, string>.Success(s + "_g");
            var leftSide  = result.Bind(f).Bind(g);
            var rightSide = result.Bind(x => f(x).Bind(g));
            Assert.Equal(leftSide.IsSuccess, rightSide.IsSuccess);
            if (leftSide.IsSuccess)
                Assert.Equal(leftSide.Value, rightSide.Value);
            else
                Assert.Equal(leftSide.Error, rightSide.Error);
        });
    }
}

// ── Tap + ValueOr + Recover: Result<string, string> ──────────────────────────

public class ResultOfTError_TapValueOrRecoverProperties
{
    [Fact]
    public void Tap_TE_OnSuccess_RunsSideEffect()
    {
        Gens.SuccessTE.Sample(result =>
        {
            string? seen = null;
            result.Tap(v => seen = v);
            Assert.Equal(result.Value, seen);
        });
    }

    [Fact]
    public void Tap_TE_OnFailure_DoesNotRunSideEffect()
    {
        Gens.FailureTE.Sample(result =>
        {
            bool ran = false;
            result.Tap(_ => ran = true);
            Assert.False(ran);
        });
    }

    [Fact]
    public void TapError_TE_OnFailure_RunsSideEffect()
    {
        Gens.FailureTE.Sample(result =>
        {
            string? seen = null;
            result.TapError(e => seen = e);
            Assert.Equal(result.Error, seen);
        });
    }

    [Fact]
    public void ValueOrDefault_TE_OnSuccess_ReturnsValue()
    {
        Gen.Select(Gens.SuccessTE, Gens.Value).Sample((result, fallback) =>
            Assert.Equal(result.Value, result.ValueOrDefault(fallback)));
    }

    [Fact]
    public void ValueOrDefault_TE_OnFailure_ReturnsFallback()
    {
        Gen.Select(Gens.FailureTE, Gens.Value).Sample((result, fallback) =>
            Assert.Equal(fallback, result.ValueOrDefault(fallback)));
    }

    [Fact]
    public void Recover_TE_OnSuccess_IsNoOp()
    {
        Gens.SuccessTE.Sample(result =>
        {
            var recovered = result.Recover(_ => "fb");
            Assert.True(recovered.IsSuccess);
            Assert.Equal(result.Value, recovered.Value);
        });
    }

    [Fact]
    public void Recover_TE_OnFailure_YieldsSuccess()
    {
        Gens.FailureTE.Sample(result =>
        {
            var recovered = result.Recover(_ => "fb");
            Assert.True(recovered.IsSuccess);
            Assert.Equal("fb", recovered.Value);
        });
    }
}
