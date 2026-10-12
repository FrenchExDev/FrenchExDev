namespace FrenchExDev.Net.Saga;

/// <summary>
/// Orchestrates a sequence of saga steps, executing them in order and
/// compensating in reverse on failure.
/// </summary>
/// <typeparam name="TContext">The saga context type carrying state across steps.</typeparam>
public sealed class SagaOrchestrator<TContext> where TContext : SagaContext
{
    private readonly IReadOnlyList<ISagaStep<TContext>> _steps;

    /// <summary>Creates a new orchestrator with the given steps.</summary>
    /// <param name="steps">The ordered list of saga steps to execute.</param>
    public SagaOrchestrator(IReadOnlyList<ISagaStep<TContext>> steps)
    {
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
    }

    /// <summary>
    /// Executes all saga steps in order. On failure, compensates previously
    /// completed steps in reverse order.
    /// </summary>
    /// <param name="context">The saga context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating overall success or failure.</returns>
    public async Task<Result.Result> ExecuteAsync(TContext context, CancellationToken ct = default)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        context.State = SagaState.Running;
        context.StartedAt = DateTimeOffset.UtcNow;

        if (_steps.Count == 0)
        {
            context.State = SagaState.Completed;
            context.CompletedAt = DateTimeOffset.UtcNow;
            return Result.Result.Success();
        }

        for (var i = 0; i < _steps.Count; i++)
        {
            context.CurrentStepIndex = i;
            var result = await _steps[i].ExecuteAsync(context, ct).ConfigureAwait(false);

            if (result.IsFailure)
            {
                context.LastError = $"Step {i} failed";
                context.State = SagaState.Compensating;

                var compensationResult = await CompensateAsync(context, i - 1, ct).ConfigureAwait(false);

                if (compensationResult.IsFailure)
                {
                    context.State = SagaState.Failed;
                    context.CompletedAt = DateTimeOffset.UtcNow;
                    return Result.Result.Failure();
                }

                context.State = SagaState.Compensated;
                context.CompletedAt = DateTimeOffset.UtcNow;
                return Result.Result.Failure();
            }
        }

        context.State = SagaState.Completed;
        context.CompletedAt = DateTimeOffset.UtcNow;
        return Result.Result.Success();
    }

    private async Task<Result.Result> CompensateAsync(TContext context, int fromIndex, CancellationToken ct)
    {
        for (var i = fromIndex; i >= 0; i--)
        {
            context.CurrentStepIndex = i;
            var result = await _steps[i].CompensateAsync(context, ct).ConfigureAwait(false);

            if (result.IsFailure)
            {
                context.LastError = $"Compensation of step {i} failed";
                return Result.Result.Failure();
            }
        }

        return Result.Result.Success();
    }
}
