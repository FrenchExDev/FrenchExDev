using System.Reactive.Linq;

namespace FrenchExDev.Net.Reactive;

/// <summary>
/// Domain-oriented operators for <see cref="IEventStream{T}"/>, delegating to Rx.
/// </summary>
public static class EventStreamExtensions
{
    /// <summary>
    /// Filters events matching the predicate.
    /// </summary>
    public static IEventStream<T> Filter<T>(this IEventStream<T> stream, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(predicate);
        return new ObservableEventStream<T>(stream.AsObservable().Where(predicate));
    }

    /// <summary>
    /// Projects each event into a new form.
    /// </summary>
    public static IEventStream<TResult> Map<T, TResult>(this IEventStream<T> stream, Func<T, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(mapper);
        return new ObservableEventStream<TResult>(stream.AsObservable().Select(mapper));
    }

    /// <summary>
    /// Merges two event streams into one.
    /// </summary>
    public static IEventStream<T> Merge<T>(this IEventStream<T> stream, IEventStream<T> other)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(other);
        return new ObservableEventStream<T>(stream.AsObservable().Merge(other.AsObservable()));
    }

    /// <summary>
    /// Buffers events into lists of the specified count.
    /// </summary>
    public static IEventStream<IList<T>> Buffer<T>(this IEventStream<T> stream, int count)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new ObservableEventStream<IList<T>>(stream.AsObservable().Buffer(count));
    }

    /// <summary>
    /// Buffers events into lists over the specified time span.
    /// </summary>
    public static IEventStream<IList<T>> Buffer<T>(this IEventStream<T> stream, TimeSpan timeSpan)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new ObservableEventStream<IList<T>>(stream.AsObservable().Buffer(timeSpan));
    }

    /// <summary>
    /// Throttles the stream, emitting only the last event within each time window.
    /// </summary>
    public static IEventStream<T> Throttle<T>(this IEventStream<T> stream, TimeSpan dueTime)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new ObservableEventStream<T>(stream.AsObservable().Throttle(dueTime));
    }

    /// <summary>
    /// Suppresses consecutive duplicate events.
    /// </summary>
    public static IEventStream<T> DistinctUntilChanged<T>(this IEventStream<T> stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new ObservableEventStream<T>(stream.AsObservable().DistinctUntilChanged());
    }

    /// <summary>
    /// Takes only the first <paramref name="count"/> events.
    /// </summary>
    public static IEventStream<T> Take<T>(this IEventStream<T> stream, int count)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new ObservableEventStream<T>(stream.AsObservable().Take(count));
    }

    /// <summary>
    /// Skips the first <paramref name="count"/> events.
    /// </summary>
    public static IEventStream<T> Skip<T>(this IEventStream<T> stream, int count)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new ObservableEventStream<T>(stream.AsObservable().Skip(count));
    }

    /// <summary>
    /// Filters events to only those of the specified target type.
    /// </summary>
    public static IEventStream<TTarget> OfType<TTarget>(this IEventStream<object> stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new ObservableEventStream<TTarget>(stream.AsObservable().OfType<TTarget>());
    }
}

/// <summary>
/// Internal adapter wrapping an <see cref="IObservable{T}"/> as an <see cref="IEventStream{T}"/>.
/// </summary>
internal sealed class ObservableEventStream<T> : IEventStream<T>
{
    private readonly IObservable<T> _observable;

    internal ObservableEventStream(IObservable<T> observable)
    {
        _observable = observable;
    }

    public IDisposable Subscribe(Action<T> onNext, Action<Exception>? onError = null, Action? onCompleted = null)
    {
        return _observable.Subscribe(
            onNext,
            onError ?? (_ => { }),
            onCompleted ?? (() => { }));
    }

    public IObservable<T> AsObservable() => _observable;
}
