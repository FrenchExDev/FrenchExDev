namespace FrenchExDev.Net.Saga;

/// <summary>Defines a single step in a saga with execute and compensate actions.</summary>
/// <typeparam name="TContext">The saga context type carrying state across steps.</typeparam>
public interface ISagaStep<in TContext> where TContext : SagaContext
{
    /// <summary>Executes the forward action for this step.</summary>
    /// <param name="context">The saga context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result.Result> ExecuteAsync(TContext context, CancellationToken ct = default);

    /// <summary>Compensates (undoes) the forward action for this step.</summary>
    /// <param name="context">The saga context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating success or failure of compensation.</returns>
    Task<Result.Result> CompensateAsync(TContext context, CancellationToken ct = default);
}
