namespace FrenchExDev.Net.Reactive;

/// <summary>
/// A domain-oriented event stream that wraps an observable sequence.
/// </summary>
/// <typeparam name="T">The type of events in the stream.</typeparam>
public interface IEventStream<out T>
{
    /// <summary>
    /// Subscribes to the event stream.
    /// </summary>
    /// <param name="onNext">Action invoked for each event.</param>
    /// <param name="onError">Optional action invoked when an error occurs.</param>
    /// <param name="onCompleted">Optional action invoked when the stream completes.</param>
    /// <returns>A disposable subscription.</returns>
    IDisposable Subscribe(Action<T> onNext, Action<Exception>? onError = null, Action? onCompleted = null);

    /// <summary>
    /// Exposes the underlying <see cref="IObservable{T}"/> for interop with Rx operators.
    /// </summary>
    IObservable<T> AsObservable();
}
