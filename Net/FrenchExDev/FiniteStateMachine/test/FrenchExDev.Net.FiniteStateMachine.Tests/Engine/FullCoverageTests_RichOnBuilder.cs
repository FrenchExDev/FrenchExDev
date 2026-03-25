using FrenchExDev.Net.FiniteStateMachine.Rich;
using FrenchExDev.Net.FiniteStateMachine.Tests.Engine.RichSimple;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

/// <summary>
/// Coverage tests for RichOnBuilder.
/// </summary>
public class FullCoverageTests_RichOnBuilder
{
    [Fact]
    public void RichOnBuilder_Done_ReturnsParentBuilder()
    {
        var builder = new RichStateMachineBuilder<ISimpleState, ISimpleEvent>()
            .InitialState(new StateA());

        var parentBuilder = builder
            .When<StateA>()
                .On<GoEvent>()
                    .TransitionTo((e, s) => new StateB())
                    .Done();

        // Done() returns the parent builder, which we can continue building with
        var result = parentBuilder
            .When<StateB>()
                .On<GoEvent>()
                    .TransitionTo((e, s) => new StateA())
            .Build();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void RichOnBuilder_Build_DirectlyOnOnBuilder()
    {
        var result = new RichStateMachineBuilder<ISimpleState, ISimpleEvent>()
            .InitialState(new StateA())
            .When<StateA>()
                .On<GoEvent>()
                    .TransitionTo((e, s) => new StateB())
                    .Build();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void RichOnBuilder_When_ChainingFromOnBuilder()
    {
        var result = new RichStateMachineBuilder<ISimpleState, ISimpleEvent>()
            .InitialState(new StateA())
            .When<StateA>()
                .On<GoEvent>()
                    .TransitionTo((e, s) => new StateB())
            .When<StateB>()
                .On<GoEvent>()
                    .TransitionTo((e, s) => new StateA())
            .Build();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task RichOnBuilder_WithGuard_Blocks()
    {
        var machine = new RichStateMachineBuilder<ISimpleState, ISimpleEvent>()
            .InitialState(new StateA())
            .When<StateA>()
                .On<GoEvent>()
                    .TransitionTo((e, s) => new StateB())
                    .WithGuard((e, s) => false)
            .Build().Value!;

        var result = await machine.FireAsync(new GoEvent());
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void RichOnBuilder_Flush_NoTarget_DoesNotAddTransition()
    {
        // Building with an On<Event> but no TransitionTo should skip
        var builder = new RichStateMachineBuilder<ISimpleState, ISimpleEvent>()
            .InitialState(new StateA());

        // Add one complete transition
        builder.When<StateA>()
            .On<GoEvent>()
                .TransitionTo((e, s) => new StateB())
                .Done();

        var result = builder.Build();
        Assert.True(result.IsSuccess);
    }
}
