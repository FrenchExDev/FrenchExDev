namespace FrenchExDev.Net.Clock.Tests;

public class SystemClockTests
{
    [Fact]
    public void UtcNow_returns_current_time()
    {
        var before = DateTimeOffset.UtcNow;
        var clockNow = SystemClock.Instance.UtcNow;
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(clockNow, before, after);
    }

    [Fact]
    public void Today_returns_current_date()
    {
        var expected = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Equal(expected, SystemClock.Instance.Today);
    }

    [Fact]
    public void TimeProvider_returns_system_provider() =>
        Assert.Same(TimeProvider.System, SystemClock.Instance.TimeProvider);

    [Fact]
    public void Now_converts_to_specified_time_zone()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
        var now = SystemClock.Instance.Now(tz);
        Assert.Equal(tz.BaseUtcOffset, now.Offset);
    }

    [Fact]
    public void CreateTimer_returns_timer()
    {
        using var timer = SystemClock.Instance.CreateTimer(
            _ => { }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        Assert.NotNull(timer);
    }

    [Fact]
    public void CreateCancellationTokenSource_returns_source()
    {
        using var cts = SystemClock.Instance.CreateCancellationTokenSource(TimeSpan.FromHours(1));
        Assert.False(cts.IsCancellationRequested);
    }

    [Fact]
    public async Task Delay_completes()
    {
        await SystemClock.Instance.Delay(TimeSpan.Zero);
    }
}
