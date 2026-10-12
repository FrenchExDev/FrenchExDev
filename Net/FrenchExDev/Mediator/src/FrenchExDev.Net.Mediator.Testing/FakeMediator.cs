namespace FrenchExDev.Net.Mediator.Testing;

/// <summary>
/// Test double for <see cref="IMediator"/> that records calls and returns canned responses.
/// </summary>
public sealed class FakeMediator : IMediator
{
    private readonly Dictionary<Type, Delegate> _setups = new();

    /// <summary>
    /// All requests passed to <see cref="SendAsync{TResult}"/>, in order.
    /// </summary>
    public List<object> SentRequests { get; } = new();

    /// <summary>
    /// All notifications passed to <see cref="PublishAsync"/>, in order.
    /// </summary>
    public List<INotification> PublishedNotifications { get; } = new();

    /// <summary>
    /// Registers a canned response factory for a given request type.
    /// </summary>
    public void Setup<TRequest, TResult>(Func<TRequest, TResult> handler) where TRequest : IRequest<TResult>
    {
        _setups[typeof(TRequest)] = handler;
    }

    /// <inheritdoc />
    public Task<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken ct = default)
    {
        SentRequests.Add(request);

        var requestType = request.GetType();
        if (!_setups.TryGetValue(requestType, out var handler))
        {
            throw new InvalidOperationException(
                $"No setup registered for request type '{requestType.Name}'. Call Setup<{requestType.Name}, {typeof(TResult).Name}>() first.");
        }

        var result = (TResult)handler.DynamicInvoke(request)!;
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task PublishAsync(INotification notification, CancellationToken ct = default, PublishStrategy strategy = PublishStrategy.Sequential)
    {
        PublishedNotifications.Add(notification);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns <c>true</c> if a request of type <typeparamref name="TRequest"/> was sent.
    /// </summary>
    public bool WasSent<TRequest>() => SentRequests.Exists(r => r is TRequest);

    /// <summary>
    /// Returns <c>true</c> if a notification of type <typeparamref name="TNotification"/> was published.
    /// </summary>
    public bool WasPublished<TNotification>() where TNotification : INotification =>
        PublishedNotifications.Exists(n => n is TNotification);

    /// <summary>
    /// Clears all recorded requests, notifications, and setups.
    /// </summary>
    public void Reset()
    {
        SentRequests.Clear();
        PublishedNotifications.Clear();
        _setups.Clear();
    }
}
