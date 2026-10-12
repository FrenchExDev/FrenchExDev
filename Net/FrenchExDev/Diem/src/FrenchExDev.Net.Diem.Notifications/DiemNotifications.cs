namespace FrenchExDev.Net.Diem.Notifications;

public interface INotificationService
{
    Task SendAsync(Notification notification, CancellationToken ct = default);
}

public sealed class Notification
{
    public required string Recipient { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public NotificationType Type { get; init; } = NotificationType.Email;
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public enum NotificationType { Email, Push, InApp }
