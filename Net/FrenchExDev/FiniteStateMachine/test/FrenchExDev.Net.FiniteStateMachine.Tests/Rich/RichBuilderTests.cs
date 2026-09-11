using FrenchExDev.Net.FiniteStateMachine.Rich;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Rich;

public class RichBuilderTests
{
    [Fact]
    public void Build_WithValidTransitions_ReturnsSuccess()
    {
        var result = new RichStateMachineBuilder<IOrderState, IOrderEvent>()
            .InitialState(new CreatedState())
            .FinalState<DeliveredState>()
            .When<CreatedState>()
                .On<SubmitEvent>()
                    .TransitionTo((evt, _) => new SubmittedState(DateTime.UtcNow))
            .Build();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Build_WithoutInitialState_ReturnsFailure()
    {
        var result = new RichStateMachineBuilder<IOrderState, IOrderEvent>()
            .When<CreatedState>()
                .On<SubmitEvent>()
                    .TransitionTo((evt, _) => new SubmittedState(DateTime.UtcNow))
            .Build();

        Assert.True(result.IsFailure);
    }
}
