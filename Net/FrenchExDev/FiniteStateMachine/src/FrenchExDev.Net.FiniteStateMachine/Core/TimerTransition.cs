using System;
using System.Threading;
using System.Threading.Tasks;

namespace FrenchExDev.Net.FiniteStateMachine;

/// <summary>
/// Manages a timeout-based automatic transition for a state.
/// Started on state entry, cancelled on state exit.
/// When the timer fires, auto-fires the timeout event through the machine.
/// </summary>
public sealed class TimerTransition<TState, TEvent> : IDisposable
{
    private readonly IStateMachine<TState, TEvent> _machine;
    private readonly TEvent _timeoutEvent;
    private readonly TimeSpan _duration;
    private CancellationTokenSource? _cts;

    public TimerTransition(
        IStateMachine<TState, TEvent> machine,
        TEvent timeoutEvent,
        TimeSpan duration)
    {
        _machine = machine;
        _timeoutEvent = timeoutEvent;
        _duration = duration;
    }

    /// <summary>Start the timer. Safe to call multiple times — cancels previous timer.</summary>
    public void Start()
    {
        Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_duration, ct).ConfigureAwait(false);
                if (!ct.IsCancellationRequested)
                {
                    await _machine.FireAsync(_timeoutEvent, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Timer was cancelled — expected
            }
        }, ct);
    }

    /// <summary>Cancel the timer if running.</summary>
    public void Cancel()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose() => Cancel();
}
