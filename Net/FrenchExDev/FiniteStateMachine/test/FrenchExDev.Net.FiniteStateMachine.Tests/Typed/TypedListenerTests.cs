using FrenchExDev.Net.FiniteStateMachine.Typed;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Typed;

public class TypedListenerTests
{
    private sealed class TrackingListener : StateMachineListenerBase<DoorState, DoorEvent>
    {
        public List<string> Events { get; } = new();

        public override Task OnTransitioningAsync(DoorState from, DoorEvent @event, DoorState to, CancellationToken ct)
        {
            Events.Add($"Transitioning:{from}->{to}");
            return Task.CompletedTask;
        }

        public override Task OnTransitionedAsync(DoorState from, DoorEvent @event, DoorState to, CancellationToken ct)
        {
            Events.Add($"Transitioned:{from}->{to}");
            return Task.CompletedTask;
        }

        public override Task OnStateExitingAsync(DoorState state, DoorEvent @event, CancellationToken ct)
        {
            Events.Add($"Exiting:{state}");
            return Task.CompletedTask;
        }

        public override Task OnStateEnteredAsync(DoorState state, DoorEvent triggeringEvent, CancellationToken ct)
        {
            Events.Add($"Entered:{state}");
            return Task.CompletedTask;
        }

        public override Task OnTransitionDeniedAsync(DoorState from, DoorEvent @event, string reason, CancellationToken ct)
        {
            Events.Add($"Denied:{from}");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Listener_ReceivesLifecycleEventsInOrder()
    {
        var definition = new TypedStateMachineBuilder<DoorState, DoorEvent>()
            .InitialState(DoorState.Closed)
            .When(DoorState.Closed)
                .On(DoorEvent.Open).TransitionTo(DoorState.Open)
            .Build();

        var machine = definition.Value!.CreateMachine();
        var listener = new TrackingListener();
        machine.AddListener(listener);

        await machine.FireAsync(DoorEvent.Open);

        Assert.Equal(new[]
        {
            "Transitioning:Closed->Open",
            "Exiting:Closed",
            "Entered:Open",
            "Transitioned:Closed->Open"
        }, listener.Events);
    }

    [Fact]
    public async Task Listener_ReceivesDeniedOnInvalidTransition()
    {
        var definition = new TypedStateMachineBuilder<DoorState, DoorEvent>()
            .InitialState(DoorState.Closed)
            .When(DoorState.Closed)
                .On(DoorEvent.Open).TransitionTo(DoorState.Open)
            .Build();

        var machine = definition.Value!.CreateMachine();
        var listener = new TrackingListener();
        machine.AddListener(listener);

        await machine.FireAsync(DoorEvent.Close);

        Assert.Single(listener.Events);
        Assert.Equal("Denied:Closed", listener.Events[0]);
    }

    [Fact]
    public async Task HistoryListener_RecordsTransitions()
    {
        var definition = new TypedStateMachineBuilder<DoorState, DoorEvent>()
            .InitialState(DoorState.Closed)
            .When(DoorState.Closed)
                .On(DoorEvent.Open).TransitionTo(DoorState.Open)
            .When(DoorState.Open)
                .On(DoorEvent.Close).TransitionTo(DoorState.Closed)
            .Build();

        var machine = definition.Value!.CreateMachine();
        var history = new HistoryListener<DoorState, DoorEvent>();
        machine.AddListener(history);

        await machine.FireAsync(DoorEvent.Open);
        await machine.FireAsync(DoorEvent.Close);

        Assert.Equal(2, history.Records.Count);
        Assert.Equal(DoorState.Closed, history.Records[0].From);
        Assert.Equal(DoorState.Open, history.Records[0].To);
        Assert.Equal(DoorState.Open, history.Records[1].From);
        Assert.Equal(DoorState.Closed, history.Records[1].To);
    }
}
