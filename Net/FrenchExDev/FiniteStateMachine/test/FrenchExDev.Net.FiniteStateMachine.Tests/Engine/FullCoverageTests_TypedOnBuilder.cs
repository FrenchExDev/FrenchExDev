using FrenchExDev.Net.FiniteStateMachine.Tests.Typed;
using FrenchExDev.Net.FiniteStateMachine.Typed;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

/// <summary>
/// Coverage tests for TypedOnBuilder.
/// </summary>
public class FullCoverageTests_TypedOnBuilder
{
    [Fact]
    public async Task TypedOnBuilder_InternalTransition_SetsInternalFlag()
    {
        var result = new TypedStateMachineBuilder<DoorState, DoorEvent>()
            .InitialState(DoorState.Closed)
            .When(DoorState.Closed)
                .On(DoorEvent.Lock).InternalTransition()
            .Build();

        Assert.True(result.IsSuccess);
        var machine = result.Value!.CreateMachine();
        var fireResult = await machine.FireAsync(DoorEvent.Lock);
        Assert.True(fireResult.IsSuccess);
        Assert.Equal(DoorState.Closed, machine.CurrentState);
    }

    [Fact]
    public void TypedOnBuilder_On_Chaining_CreatesMultipleTransitions()
    {
        var result = new TypedStateMachineBuilder<DoorState, DoorEvent>()
            .InitialState(DoorState.Closed)
            .When(DoorState.Closed)
                .On(DoorEvent.Open).TransitionTo(DoorState.Open)
                .On(DoorEvent.Lock).TransitionTo(DoorState.Locked)
            .Build();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.CanFire(DoorState.Closed, DoorEvent.Open));
        Assert.True(result.Value!.CanFire(DoorState.Closed, DoorEvent.Lock));
    }

    [Fact]
    public async Task TypedOnBuilder_WithGuard_IGuardInterface_Works()
    {
        var guard = new AlwaysTrueGuard();
        var result = new TypedStateMachineBuilder<DoorState, DoorEvent>()
            .InitialState(DoorState.Closed)
            .When(DoorState.Closed)
                .On(DoorEvent.Open).TransitionTo(DoorState.Open)
                    .WithGuard(guard)
            .Build();

        Assert.True(result.IsSuccess);
        var machine = result.Value!.CreateMachine();
        var fireResult = await machine.FireAsync(DoorEvent.Open);
        Assert.True(fireResult.IsSuccess);
        Assert.True(guard.WasCalled);
    }

    [Fact]
    public async Task TypedOnBuilder_WithAction_ITransitionActionInterface_Works()
    {
        var action = new RecordingAction();
        var result = new TypedStateMachineBuilder<DoorState, DoorEvent>()
            .InitialState(DoorState.Closed)
            .When(DoorState.Closed)
                .On(DoorEvent.Open).TransitionTo(DoorState.Open)
                    .WithAction(action)
            .Build();

        Assert.True(result.IsSuccess);
        var machine = result.Value!.CreateMachine();
        await machine.FireAsync(DoorEvent.Open);
        Assert.True(action.WasCalled);
    }

    [Fact]
    public void TypedOnBuilder_Build_DirectlyOnOnBuilder_Works()
    {
        // Build() called directly from OnBuilder (not via When)
        var result = new TypedStateMachineBuilder<DoorState, DoorEvent>()
            .InitialState(DoorState.Closed)
            .When(DoorState.Closed)
                .On(DoorEvent.Open).TransitionTo(DoorState.Open)
                    .Build();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TypedOnBuilder_When_ChainingFromOnBuilder()
    {
        var result = new TypedStateMachineBuilder<DoorState, DoorEvent>()
            .InitialState(DoorState.Closed)
            .When(DoorState.Closed)
                .On(DoorEvent.Open).TransitionTo(DoorState.Open)
            .When(DoorState.Open)
                .On(DoorEvent.Close).TransitionTo(DoorState.Closed)
            .Build();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Transitions.Count);
    }

    [Fact]
    public void TypedOnBuilder_Flush_NoTarget_DoesNotAddTransition()
    {
        var result = new TypedStateMachineBuilder<DoorState, DoorEvent>()
            .InitialState(DoorState.Closed)
            .When(DoorState.Closed)
                .On(DoorEvent.Open).TransitionTo(DoorState.Open)
                .On(DoorEvent.Lock) // no TransitionTo — should be skipped
            .Build();

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Transitions);
    }

    // ═══════════════════════════════════════════════════════
    // Helper types
    // ═══════════════════════════════════════════════════════

    private sealed class AlwaysTrueGuard : IGuard<DoorState, DoorEvent>
    {
        public bool WasCalled { get; private set; }
        public Task<bool> EvaluateAsync(DoorState from, DoorEvent @event, DoorState to, CancellationToken ct)
        {
            WasCalled = true;
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingAction : ITransitionAction<DoorState, DoorEvent>
    {
        public bool WasCalled { get; private set; }
        public Task ExecuteAsync(DoorState from, DoorEvent @event, DoorState to, CancellationToken ct)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }
}
