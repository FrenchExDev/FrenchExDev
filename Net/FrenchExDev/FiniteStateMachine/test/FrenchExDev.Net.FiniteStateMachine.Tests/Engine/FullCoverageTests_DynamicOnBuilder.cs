using FrenchExDev.Net.FiniteStateMachine.Dynamic;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

/// <summary>
/// Coverage tests for DynamicOnBuilder and DynamicStateMachineBuilder validation.
/// </summary>
public class FullCoverageTests_DynamicOnBuilder
{
    [Fact]
    public async Task DynamicOnBuilder_InternalTransition_StaysInSameState()
    {
        var executed = false;
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A")
                .On("Ping").InternalTransition()
                    .WithAction((f, e, t, ct) => { executed = true; return Task.CompletedTask; })
            .Build().Value!;

        var machine = definition.CreateMachine();
        var result = await machine.FireAsync("Ping");
        Assert.True(result.IsSuccess);
        Assert.Equal("A", machine.CurrentState);
        Assert.True(executed);
    }

    [Fact]
    public void DynamicOnBuilder_On_Chaining_Works()
    {
        var result = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A")
                .On("Go").TransitionTo("B")
                .On("Stay").InternalTransition()
            .Build();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void DynamicOnBuilder_Build_DirectlyOnOnBuilder_Works()
    {
        var result = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A")
                .On("Go").TransitionTo("B")
                    .Build();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void DynamicOnBuilder_When_ChainingFromOnBuilder()
    {
        var result = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A")
                .On("Go").TransitionTo("B")
            .When("B")
                .On("Back").TransitionTo("A")
            .Build();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Transitions.Count);
    }

    [Fact]
    public async Task DynamicOnBuilder_WithGuard_DelegateGuard()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A")
                .On("Go").TransitionTo("B")
                    .WithGuard((f, e, t, ct) => Task.FromResult(false))
            .Build().Value!;

        var machine = definition.CreateMachine();
        var result = await machine.FireAsync("Go");
        Assert.True(result.IsFailure); // guard denied
    }

    [Fact]
    public void DynamicOnBuilder_Flush_NoTarget_DoesNotAddTransition()
    {
        // When On() is called but TransitionTo is never called,
        // Build should still succeed but only have transitions that were completed
        var result = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A")
                .On("Go").TransitionTo("B")
                .On("Incomplete") // no TransitionTo — should be skipped
            .Build();

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Transitions);
    }

    [Fact]
    public void DynamicBuilder_Build_WithoutInitialState_ReturnsFailure()
    {
        var result = new DynamicStateMachineBuilder()
            .When("A").On("Go").TransitionTo("B")
            .Build();

        Assert.True(result.IsFailure);
    }
}
