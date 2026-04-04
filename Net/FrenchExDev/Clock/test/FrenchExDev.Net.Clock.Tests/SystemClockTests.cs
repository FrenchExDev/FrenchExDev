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
}
