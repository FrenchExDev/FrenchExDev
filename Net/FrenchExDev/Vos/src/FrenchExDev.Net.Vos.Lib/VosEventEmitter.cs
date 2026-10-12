using System.Collections.Concurrent;
using FrenchExDev.Net.Injectable.Attributes;

namespace FrenchExDev.Net.Vos.Lib;

/// <summary>
/// Thread-safe event emitter with multi-subscriber support and typed filtering.
/// </summary>
[Injectable(Scope = Scope.Singleton, As = new[] { typeof(IVosEventEmitter) })]
public sealed class VosEventEmitter : IVosEventEmitter
{
    private readonly ConcurrentBag<Action<VosEvent>> _handlers = [];

    public void Emit(VosEvent evt)
    {
        foreach (var handler in _handlers)
            handler(evt);
    }

    public IDisposable Subscribe(Action<VosEvent> handler)
    {
        _handlers.Add(handler);
        return new Subscription(handler, _handlers);
    }

    public IDisposable Subscribe<T>(Action<T> handler) where T : VosEvent
        => Subscribe(evt => { if (evt is T typed) handler(typed); });

    private sealed class Subscription(Action<VosEvent> handler, ConcurrentBag<Action<VosEvent>> handlers) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // ConcurrentBag doesn't support removal — rebuild without this handler
            var remaining = handlers.Where(h => h != handler).ToList();
            while (handlers.TryTake(out _)) { }
            foreach (var h in remaining) handlers.Add(h);
        }
    }
}
