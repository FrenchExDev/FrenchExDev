namespace FrenchExDev.Net.Clock;

/// <summary>
/// Abstraction over the system clock for testability.
/// Wraps <see cref="TimeProvider"/> with a simpler, domain-oriented API.
/// </summary>
public interface IClock
{
    /// <summary>Current UTC date and time.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Current local date and time in the specified time zone.</summary>
    DateTimeOffset Now(TimeZoneInfo timeZone);

    /// <summary>Current UTC date (time component is midnight).</summary>
    DateOnly Today { get; }

    /// <summary>Creates a timer that fires after the specified delay.</summary>
    ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period);

    /// <summary>Creates a CancellationTokenSource that cancels after the specified delay.</summary>
    CancellationTokenSource CreateCancellationTokenSource(TimeSpan delay);

    /// <summary>Returns a task that completes after the specified delay.</summary>
    Task Delay(TimeSpan delay, CancellationToken ct = default);

    /// <summary>The underlying TimeProvider for advanced scenarios.</summary>
    TimeProvider TimeProvider { get; }
}
