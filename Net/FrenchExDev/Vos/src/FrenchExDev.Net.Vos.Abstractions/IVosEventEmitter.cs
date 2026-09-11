namespace FrenchExDev.Net.Vos.Abstractions;

/// <summary>
/// Pub/sub event emitter for Vos operations.
/// Multiple subscribers (CLI output, logging, test assertions).
/// </summary>
public interface IVosEventEmitter
{
    void Emit(VosEvent evt);
    IDisposable Subscribe(Action<VosEvent> handler);
    IDisposable Subscribe<T>(Action<T> handler) where T : VosEvent;
}
