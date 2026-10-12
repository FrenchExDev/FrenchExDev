using FrenchExDev.Net.FiniteStateMachine.Typed;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Typed;

public class TypedFireTests
{
    private static IStateMachine<DoorState, DoorEvent> CreateDoorMachine()
    {
        var definition = new TypedStateMachineBuilder<DoorState, DoorEvent>()
            .InitialState(DoorState.Closed)
            .When(DoorState.Closed)
                .On(DoorEvent.Open).TransitionTo(DoorState.Open)
                .On(DoorEvent.Lock).TransitionTo(DoorState.Locked)
            .When(DoorState.Open)
                .On(DoorEvent.Close).TransitionTo(DoorState.Closed)
            .When(DoorState.Locked)
                .On(DoorEvent.Unlock).TransitionTo(DoorState.Closed)
            .Build();

        return definition.Value!.CreateMachine();
    }

    [Fact]
    public async Task FireAsync_ValidTransition_ReturnsSuccess()
    {
        var machine = CreateDoorMachine();

        var result = await machine.FireAsync(DoorEvent.Open);

        Assert.True(result.IsSuccess);
        Assert.Equal(DoorState.Closed, result.Value!.From);
        Assert.Equal(DoorState.Open, result.Value.To);
        Assert.Equal(DoorState.Open, machine.CurrentState);
    }

    [Fact]
    public async Task FireAsync_InvalidTransition_ReturnsFailure()
    {
        var machine = CreateDoorMachine();

        var result = await machine.FireAsync(DoorEvent.Close);

        Assert.True(result.IsFailure);
        Assert.Equal(DoorState.Closed, machine.CurrentState);
    }

    [Fact]
    public async Task FireAsync_MultipleTransitions_TraversesCorrectly()
    {
        var machine = CreateDoorMachine();

        await machine.FireAsync(DoorEvent.Open);
        Assert.Equal(DoorState.Open, machine.CurrentState);

        await machine.FireAsync(DoorEvent.Close);
        Assert.Equal(DoorState.Closed, machine.CurrentState);

        await machine.FireAsync(DoorEvent.Lock);
        Assert.Equal(DoorState.Locked, machine.CurrentState);

        await machine.FireAsync(DoorEvent.Unlock);
        Assert.Equal(DoorState.Closed, machine.CurrentState);
    }

    [Fact]
    public async Task CanFireAsync_ValidTransition_ReturnsTrue()
    {
        var machine = CreateDoorMachine();

        Assert.True(await machine.CanFireAsync(DoorEvent.Open));
        Assert.False(await machine.CanFireAsync(DoorEvent.Close));
    }

    [Fact]
    public async Task GetPermittedEventsAsync_ReturnsCorrectEvents()
    {
        var machine = CreateDoorMachine();

        var permitted = await machine.GetPermittedEventsAsync();

        Assert.Contains(DoorEvent.Open, permitted);
        Assert.Contains(DoorEvent.Lock, permitted);
        Assert.DoesNotContain(DoorEvent.Close, permitted);
    }
}
