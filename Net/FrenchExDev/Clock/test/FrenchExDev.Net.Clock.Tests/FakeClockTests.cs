namespace FrenchExDev.Net.Clock.Tests;

using FrenchExDev.Net.Clock.Testing;

public class FakeClockTests
{
    private readonly FakeClock _clock = new(new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void UtcNow_returns_initial_time() =>
        Assert.Equal(new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero), _clock.UtcNow);

    [Fact]
    public void Advance_moves_time_forward()
    {
        _clock.Advance(TimeSpan.FromHours(2));
        Assert.Equal(new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero), _clock.UtcNow);
    }

    [Fact]
    public void SetUtcNow_sets_specific_time()
    {
        var newTime = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _clock.SetUtcNow(newTime);
        Assert.Equal(newTime, _clock.UtcNow);
    }

    [Fact]
    public void Today_returns_date_part() =>
        Assert.Equal(new DateOnly(2025, 6, 15), _clock.Today);

    [Fact]
    public void Advance_across_midnight_changes_Today()
    {
        _clock.Advance(TimeSpan.FromHours(15)); // 10:00 + 15h = 01:00 next day
        Assert.Equal(new DateOnly(2025, 6, 16), _clock.Today);
    }

    [Fact]
    public async Task Delay_completes_after_advance()
    {
        var delayTask = _clock.Delay(TimeSpan.FromMinutes(5));
        Assert.False(delayTask.IsCompleted);

        _clock.Advance(TimeSpan.FromMinutes(5));
        await delayTask;

        Assert.True(delayTask.IsCompleted);
    }

    [Fact]
    public void Timer_fires_after_advance()
    {
        var fired = false;
        _clock.CreateTimer(_ => fired = true, null, TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);

        Assert.False(fired);
        _clock.Advance(TimeSpan.FromSeconds(10));
        Assert.True(fired);
    }

    [Fact]
    public void Default_constructor_starts_at_2024_01_01()
    {
        var clock = new FakeClock();
        Assert.Equal(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), clock.UtcNow);
    }
}
