namespace FrenchExDev.Net.Mediator;

/// <summary>
/// Handles notifications of type <typeparamref name="TNotification"/>.
/// </summary>
public interface INotificationHandler<in TNotification> where TNotification : INotification
{
    /// <summary>
    /// Handles the notification.
    /// </summary>
    Task HandleAsync(TNotification notification, CancellationToken ct = default);
}
