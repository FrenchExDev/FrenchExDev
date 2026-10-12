using FrenchExDev.Net.FiniteStateMachine.Dynamic;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Dynamic;

public class DynamicBuilderTests
{
    [Fact]
    public void Build_WithValidTransitions_ReturnsSuccess()
    {
        var result = new DynamicStateMachineBuilder()
            .InitialState("Created")
            .FinalState("Delivered")
            .When("Created")
                .On("Submit").TransitionTo("Submitted")
            .When("Submitted")
                .On("Approve").TransitionTo("Approved")
            .When("Approved")
                .On("Ship").TransitionTo("Shipped")
            .When("Shipped")
                .On("Deliver").TransitionTo("Delivered")
            .Build();

        Assert.True(result.IsSuccess);
        Assert.Equal("Created", result.Value!.InitialState);
    }

    [Fact]
    public void Build_WithoutInitialState_ReturnsFailure()
    {
        var result = new DynamicStateMachineBuilder()
            .When("A")
                .On("Go").TransitionTo("B")
            .Build();

        Assert.True(result.IsFailure);
    }
}
