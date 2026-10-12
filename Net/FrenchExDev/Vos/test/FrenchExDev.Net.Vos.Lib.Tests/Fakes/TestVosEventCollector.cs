namespace FrenchExDev.Net.Vos.Lib.Tests.Fakes;

public sealed class TestVosEventCollector
{
    private readonly List<VosEvent> _events = [];
    public IReadOnlyList<VosEvent> Events => _events;

    public IDisposable Subscribe(IVosEventEmitter emitter) => emitter.Subscribe(_events.Add);

    public bool Has<T>() where T : VosEvent => _events.OfType<T>().Any();
    public T Single<T>() where T : VosEvent => _events.OfType<T>().Single();
    public IEnumerable<T> All<T>() where T : VosEvent => _events.OfType<T>();
}
