namespace FrenchExDev.Net.Mediator;

/// <summary>
/// Handles a request of type <typeparamref name="TRequest"/> and produces a <typeparamref name="TResult"/>.
/// </summary>
public interface IRequestHandler<in TRequest, TResult> where TRequest : IRequest<TResult>
{
    /// <summary>
    /// Handles the request.
    /// </summary>
    Task<TResult> HandleAsync(TRequest request, CancellationToken ct = default);
}
