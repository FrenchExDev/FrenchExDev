namespace FrenchExDev.Net.Clock.Testing;

using Microsoft.Extensions.Time.Testing;

/// <summary>
/// Test implementation of <see cref="IClock"/> backed by <see cref="FakeTimeProvider"/>.
/// Allows deterministic time control in tests.
/// </summary>
public sealed class FakeClock : IClock
{
    private readonly FakeTimeProvider _provider;

    /// <summary>Creates a FakeClock starting at the specified time.</summary>
    public FakeClock(DateTimeOffset startTime)
    {
        _provider = new FakeTimeProvider(startTime);
    }

    /// <summary>Creates a FakeClock starting at 2024-01-01T00:00:00Z.</summary>
    public FakeClock() : this(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)) { }

    public DateTimeOffset UtcNow => _provider.GetUtcNow();

    public DateTimeOffset Now(TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTime(UtcNow, timeZone);

    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);

    public ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        _provider.CreateTimer(callback, state, dueTime, period);

    public CancellationTokenSource CreateCancellationTokenSource(TimeSpan delay) =>
        new(delay, _provider);

    public Task Delay(TimeSpan delay, CancellationToken ct = default) =>
        Task.Delay(delay, _provider, ct);

    public TimeProvider TimeProvider => _provider;

    /// <summary>Advances time by the specified duration, firing any pending timers.</summary>
    public void Advance(TimeSpan duration) => _provider.Advance(duration);

    /// <summary>Sets the clock to a specific point in time.</summary>
    public void SetUtcNow(DateTimeOffset value) => _provider.SetUtcNow(value);

    /// <summary>The underlying FakeTimeProvider for advanced test scenarios.</summary>
    public FakeTimeProvider FakeTimeProvider => _provider;
}
