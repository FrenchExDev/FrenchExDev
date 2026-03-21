namespace FrenchExDev.Net.Diem.Workflow.Locales;

public enum LocaleStatus { Pending, InProgress, Complete }

public class LocaleProgress
{
    public required string Locale { get; init; }
    public LocaleStatus Status { get; set; } = LocaleStatus.Pending;
}

public interface ILocaleTracker
{
    Task<bool> MarkLocaleCompleteAsync(Guid entityId, string stage, string locale, CancellationToken ct = default);
    Task<IReadOnlyList<LocaleProgress>> GetProgressAsync(Guid entityId, string stage, CancellationToken ct = default);
}
