namespace FrenchExDev.Net.Clock;

/// <summary>
/// Production implementation of <see cref="IClock"/> backed by <see cref="TimeProvider.System"/>.
/// </summary>
public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();

    public DateTimeOffset UtcNow => TimeProvider.GetUtcNow();

    public DateTimeOffset Now(TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTime(UtcNow, timeZone);

    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);

    public ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        TimeProvider.CreateTimer(callback, state, dueTime, period);

    public CancellationTokenSource CreateCancellationTokenSource(TimeSpan delay) =>
        new(delay, TimeProvider);

    public Task Delay(TimeSpan delay, CancellationToken ct = default) =>
        Task.Delay(delay, TimeProvider, ct);

    public TimeProvider TimeProvider => TimeProvider.System;
}
