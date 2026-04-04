using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace FrenchExDev.Net.Reactive;

/// <summary>
/// A mutable event stream backed by a <see cref="Subject{T}"/>.
/// Supports publishing events and subscribing to them.
/// </summary>
/// <typeparam name="T">The type of events in the stream.</typeparam>
public sealed class EventStream<T> : IEventStream<T>, IDisposable
{
    private readonly Subject<T> _subject = new();

    /// <summary>
    /// Publishes an event to all subscribers.
    /// </summary>
    /// <param name="value">The event value.</param>
    public void Publish(T value) => _subject.OnNext(value);

    /// <summary>
    /// Signals that the stream has completed. No more events will be published.
    /// </summary>
    public void Complete() => _subject.OnCompleted();

    /// <summary>
    /// Signals an error on the stream.
    /// </summary>
    /// <param name="error">The exception to propagate.</param>
    public void Error(Exception error) => _subject.OnError(error);

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
