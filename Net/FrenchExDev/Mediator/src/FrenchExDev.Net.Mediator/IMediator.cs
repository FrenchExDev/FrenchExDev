namespace FrenchExDev.Net.Mediator;

/// <summary>
/// Dispatches requests to their handlers and publishes notifications.
/// </summary>
public interface IMediator
{
    /// <summary>
    /// Sends a request to its single handler and returns the result.
    /// </summary>
    Task<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken ct = default);

    /// <summary>
    /// Publishes a notification to all registered handlers.
    /// </summary>
    Task PublishAsync(INotification notification, CancellationToken ct = default, PublishStrategy strategy = PublishStrategy.Sequential);
}
