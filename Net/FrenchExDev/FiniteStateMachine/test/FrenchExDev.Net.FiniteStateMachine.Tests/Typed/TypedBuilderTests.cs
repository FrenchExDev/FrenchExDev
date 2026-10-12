using FrenchExDev.Net.FiniteStateMachine.Typed;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Typed;

public enum DoorState { Closed, Open, Locked }
public enum DoorEvent { Open, Close, Lock, Unlock }

public class TypedBuilderTests
{
    [Fact]
    public void Build_WithValidTransitions_ReturnsSuccess()
    {
        var result = new TypedStateMachineBuilder<DoorState, DoorEvent>()
            .InitialState(DoorState.Closed)
            .When(DoorState.Closed)
                .On(DoorEvent.Open).TransitionTo(DoorState.Open)
                .On(DoorEvent.Lock).TransitionTo(DoorState.Locked)
            .When(DoorState.Open)
                .On(DoorEvent.Close).TransitionTo(DoorState.Closed)
            .When(DoorState.Locked)
                .On(DoorEvent.Unlock).TransitionTo(DoorState.Closed)
            .Build();

        Assert.True(result.IsSuccess);
        Assert.Equal(DoorState.Closed, result.Value!.InitialState);
        Assert.Contains(DoorState.Closed, result.Value.States);
        Assert.Contains(DoorState.Open, result.Value.States);
        Assert.Contains(DoorState.Locked, result.Value.States);
    }

    [Fact]
    public void Build_WithoutInitialState_ReturnsFailure()
    {
        var result = new TypedStateMachineBuilder<DoorState, DoorEvent>()
            .When(DoorState.Closed)
                .On(DoorEvent.Open).TransitionTo(DoorState.Open)
            .Build();

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Build_WithFinalStates_TracksThemCorrectly()
    {
        var result = new TypedStateMachineBuilder<DoorState, DoorEvent>()
            .InitialState(DoorState.Closed)
            .FinalState(DoorState.Locked)
            .When(DoorState.Closed)
                .On(DoorEvent.Lock).TransitionTo(DoorState.Locked)
            .Build();

        Assert.True(result.IsSuccess);
        Assert.Contains(DoorState.Locked, result.Value!.FinalStates);
    }
}
