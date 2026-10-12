using FrenchExDev.Net.FiniteStateMachine.Typed;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Typed;

public class TypedGuardTests
{
    [Fact]
    public async Task FireAsync_WithPassingGuard_Succeeds()
    {
        var definition = new TypedStateMachineBuilder<DoorState, DoorEvent>()
            .InitialState(DoorState.Closed)
            .When(DoorState.Closed)
                .On(DoorEvent.Open).TransitionTo(DoorState.Open)
                    .WithGuard((from, evt, to, ct) => Task.FromResult(true))
            .Build();

        var machine = definition.Value!.CreateMachine();
        var result = await machine.FireAsync(DoorEvent.Open);

        Assert.True(result.IsSuccess);
        Assert.Equal(DoorState.Open, machine.CurrentState);
    }

    [Fact]
    public async Task FireAsync_WithFailingGuard_ReturnsDenied()
    {
        var definition = new TypedStateMachineBuilder<DoorState, DoorEvent>()
            .InitialState(DoorState.Closed)
            .When(DoorState.Closed)
                .On(DoorEvent.Open).TransitionTo(DoorState.Open)
                    .WithGuard((from, evt, to, ct) => Task.FromResult(false))
            .Build();

        var machine = definition.Value!.CreateMachine();
        var result = await machine.FireAsync(DoorEvent.Open);

        Assert.True(result.IsFailure);
        Assert.Equal(DoorState.Closed, machine.CurrentState);
    }

    [Fact]
    public async Task CanFireAsync_WithFailingGuard_ReturnsFalse()
    {
        var definition = new TypedStateMachineBuilder<DoorState, DoorEvent>()
            .InitialState(DoorState.Closed)
            .When(DoorState.Closed)
                .On(DoorEvent.Open).TransitionTo(DoorState.Open)
                    .WithGuard((from, evt, to, ct) => Task.FromResult(false))
            .Build();

        var machine = definition.Value!.CreateMachine();
        Assert.False(await machine.CanFireAsync(DoorEvent.Open));
    }
}
