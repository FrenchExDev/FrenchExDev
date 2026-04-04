using FrenchExDev.Net.Vos.Lib;

namespace FrenchExDev.Net.Vos.Lib.Tests;

public class VosEventEmitterTests
{
    [Fact]
    public void Emit_delivers_to_subscriber()
    {
        var emitter = new VosEventEmitter();
        var received = new List<VosEvent>();
        emitter.Subscribe(received.Add);

        emitter.Emit(new ConfigSaved("test.yaml"));

        received.ShouldHaveSingleItem();
        received[0].ShouldBeOfType<ConfigSaved>();
    }

    [Fact]
    public void Subscribe_typed_filters_by_event_type()
    {
        var emitter = new VosEventEmitter();
        var received = new List<ConfigSaved>();
        emitter.Subscribe<ConfigSaved>(received.Add);

        emitter.Emit(new ConfigLoading("test.yaml"));
        emitter.Emit(new ConfigSaved("test.yaml"));

        received.ShouldHaveSingleItem();
    }

    [Fact]
    public void Multiple_subscribers_all_receive_events()
    {
        var emitter = new VosEventEmitter();
        var a = new List<VosEvent>();
        var b = new List<VosEvent>();
        emitter.Subscribe(a.Add);
        emitter.Subscribe(b.Add);

        emitter.Emit(new ConfigSaved("test.yaml"));

        a.Count.ShouldBe(1);
        b.Count.ShouldBe(1);
    }

    [Fact]
    public void Dispose_removes_subscriber()
    {
        var emitter = new VosEventEmitter();
        var received = new List<VosEvent>();
        var sub = emitter.Subscribe(received.Add);

        emitter.Emit(new ConfigSaved("a"));
        sub.Dispose();
        emitter.Emit(new ConfigSaved("b"));

        received.Count.ShouldBe(1);
    }

    [Fact]
    public void All_events_have_correlation_id()
    {
        var evt = new ConfigSaved("test.yaml");
        evt.CorrelationId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Double_dispose_does_not_throw()
    {
        var emitter = new VosEventEmitter();
        var received = new List<VosEvent>();
        var sub = emitter.Subscribe(received.Add);

        sub.Dispose();
        Should.NotThrow(() => sub.Dispose());
    }

    [Fact]
    public void Dispose_removes_only_target_handler_keeping_others()
    {
        var emitter = new VosEventEmitter();
        var kept = new List<VosEvent>();
        var removed = new List<VosEvent>();

        emitter.Subscribe(kept.Add);
        var sub = emitter.Subscribe(removed.Add);

        emitter.Emit(new ConfigSaved("before"));
        kept.Count.ShouldBe(1);
        removed.Count.ShouldBe(1);

        sub.Dispose();

        emitter.Emit(new ConfigSaved("after"));
        kept.Count.ShouldBe(2);
        removed.Count.ShouldBe(1); // no longer subscribed
    }

    [Fact]
    public void Subscribe_typed_does_not_invoke_handler_for_non_matching_event()
    {
        var emitter = new VosEventEmitter();
        var received = new List<ConfigSaved>();
        emitter.Subscribe<ConfigSaved>(received.Add);

        // Emit a non-matching event type only
        emitter.Emit(new ConfigLoading("x"));

        received.ShouldBeEmpty();
    }
}
