using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace FrenchExDev.Net.Reactive.Testing;

/// <summary>
/// A test double for <see cref="IEventStream{T}"/> that records all published events.
/// </summary>
/// <typeparam name="T">The type of events in the stream.</typeparam>
public sealed class TestEventStream<T> : IEventStream<T>, IDisposable
{
    private readonly Subject<T> _subject = new();
    private readonly List<T> _events = [];

    /// <summary>
    /// Gets all events that have been published to this stream.
    /// </summary>
    public IReadOnlyList<T> Events => _events;

    /// <summary>
    /// Publishes an event and records it.
    /// </summary>
    /// <param name="value">The event value.</param>
    public void Publish(T value)
    {
        _events.Add(value);
        _subject.OnNext(value);
    }

    /// <summary>
    /// Clears the recorded events list.
    /// </summary>
    public void Clear() => _events.Clear();

    /// <inheritdoc />
    public IDisposable Subscribe(Action<T> onNext, Action<Exception>? onError = null, Action? onCompleted = null)
    {
        return _subject.Subscribe(
            onNext,
            onError ?? (_ => { }),
            onCompleted ?? (() => { }));
    }

    /// <inheritdoc />
    public IObservable<T> AsObservable() => _subject.AsObservable();

    /// <summary>
    /// Disposes the underlying subject.
    /// </summary>
    public void Dispose() => _subject.Dispose();
}
