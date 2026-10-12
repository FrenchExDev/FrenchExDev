namespace FrenchExDev.Net.Mediator;

/// <summary>
/// Pipeline behavior that wraps the execution of a request handler.
/// Behaviors execute in order and can short-circuit or modify the result.
/// </summary>
public interface IBehavior<in TRequest, TResult> where TRequest : IRequest<TResult>
{
    /// <summary>
    /// Handles the behavior, optionally delegating to the next step in the pipeline.
    /// </summary>
    Task<TResult> HandleAsync(TRequest request, Func<Task<TResult>> next, CancellationToken ct = default);
}
