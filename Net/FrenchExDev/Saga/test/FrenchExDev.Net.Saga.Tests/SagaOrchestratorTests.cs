using FrenchExDev.Net.Saga;
using FrenchExDev.Net.Saga.Testing;

namespace FrenchExDev.Net.Saga.Tests;

public sealed class SagaOrchestratorTests
{
    private sealed class TestContext : SagaContext { }

    private sealed class SucceedingStep : ISagaStep<TestContext>
    {
        public List<string> Log { get; } = new();

        public Task<Result.Result> ExecuteAsync(TestContext context, CancellationToken ct = default)
        {
            Log.Add("execute");
            return Task.FromResult(Result.Result.Success());
        }

        public Task<Result.Result> CompensateAsync(TestContext context, CancellationToken ct = default)
        {
            Log.Add("compensate");
            return Task.FromResult(Result.Result.Success());
        }
    }

    private sealed class FailingStep : ISagaStep<TestContext>
    {
        public List<string> Log { get; } = new();

        public Task<Result.Result> ExecuteAsync(TestContext context, CancellationToken ct = default)
        {
            Log.Add("execute");
            return Task.FromResult(Result.Result.Failure());
        }

        public Task<Result.Result> CompensateAsync(TestContext context, CancellationToken ct = default)
        {
            Log.Add("compensate");
            return Task.FromResult(Result.Result.Success());
        }
    }

    private sealed class FailingCompensationStep : ISagaStep<TestContext>
    {
        public List<string> Log { get; } = new();

        public Task<Result.Result> ExecuteAsync(TestContext context, CancellationToken ct = default)
        {
            Log.Add("execute");
            return Task.FromResult(Result.Result.Failure());
        }

        public Task<Result.Result> CompensateAsync(TestContext context, CancellationToken ct = default)
        {
            Log.Add("compensate");
            return Task.FromResult(Result.Result.Failure());
        }
    }

    [Fact]
    public async Task AllStepsSucceed_StateIsCompleted()
    {
        // Arrange
        var step1 = new SucceedingStep();
        var step2 = new SucceedingStep();
        var step3 = new SucceedingStep();
        var orchestrator = new SagaOrchestrator<TestContext>(new ISagaStep<TestContext>[] { step1, step2, step3 });
        var context = new TestContext();

        // Act
        var result = await orchestrator.ExecuteAsync(context);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SagaState.Completed, context.State);
        Assert.NotNull(context.CompletedAt);
        Assert.Null(context.LastError);
        Assert.Single(step1.Log, "execute");
        Assert.Single(step2.Log, "execute");
        Assert.Single(step3.Log, "execute");
    }

    [Fact]
    public async Task Step2Of3Fails_CompensatesStep1InReverse_StateIsCompensated()
    {
        // Arrange
        var step1 = new SucceedingStep();
        var step2 = new FailingStep();
        var step3 = new SucceedingStep();
        var orchestrator = new SagaOrchestrator<TestContext>(new ISagaStep<TestContext>[] { step1, step2, step3 });
        var context = new TestContext();

        // Act
        var result = await orchestrator.ExecuteAsync(context);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(SagaState.Compensated, context.State);
        Assert.NotNull(context.CompletedAt);
        Assert.NotNull(context.LastError);
        // Step 1 was executed then compensated
        Assert.Equal(new[] { "execute", "compensate" }, step1.Log);
        // Step 2 was executed (failed) but not compensated
        Assert.Single(step2.Log, "execute");
        // Step 3 was never reached
        Assert.Empty(step3.Log);
    }

    [Fact]
    public async Task ZeroSteps_StateIsCompletedImmediately()
    {
        // Arrange
        var orchestrator = new SagaOrchestrator<TestContext>(Array.Empty<ISagaStep<TestContext>>());
        var context = new TestContext();

        // Act
        var result = await orchestrator.ExecuteAsync(context);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SagaState.Completed, context.State);
        Assert.NotNull(context.CompletedAt);
    }

    [Fact]
    public async Task SingleStepFailure_StateIsCompensated()
    {
        // Arrange
        var step = new FailingStep();
        var orchestrator = new SagaOrchestrator<TestContext>(new ISagaStep<TestContext>[] { step });
        var context = new TestContext();

        // Act
        var result = await orchestrator.ExecuteAsync(context);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(SagaState.Compensated, context.State);
        Assert.NotNull(context.CompletedAt);
        // Only execute was called (step 0 failed, compensate runs for i-1 = -1, so no compensation)
        Assert.Single(step.Log, "execute");
    }

    [Fact]
    public async Task CompensationFailure_StateIsFailed()
    {
        // Arrange
        var step1 = new SucceedingStep();
        var failingCompStep = new FailingCompensationStep();
        // step1 succeeds, then failingCompStep fails on execute,
        // compensation of step1 would normally happen but we need compensation to fail.
        // Instead: step1 succeeds, step2 succeeds, step3 fails,
        // then compensate step2 (which fails compensation).
        var step2Compensate = new CompensationFailStep();
        var step3Fail = new FailingStep();
        var orchestrator = new SagaOrchestrator<TestContext>(new ISagaStep<TestContext>[] { step1, step2Compensate, step3Fail });
        var context = new TestContext();

        // Act
        var result = await orchestrator.ExecuteAsync(context);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(SagaState.Failed, context.State);
        Assert.NotNull(context.CompletedAt);
    }

    /// <summary>Step that succeeds on execute but fails on compensate.</summary>
    private sealed class CompensationFailStep : ISagaStep<TestContext>
    {
        public List<string> Log { get; } = new();

        public Task<Result.Result> ExecuteAsync(TestContext context, CancellationToken ct = default)
        {
            Log.Add("execute");
            return Task.FromResult(Result.Result.Success());
        }

        public Task<Result.Result> CompensateAsync(TestContext context, CancellationToken ct = default)
        {
            Log.Add("compensate");
            return Task.FromResult(Result.Result.Failure());
        }
    }
}
