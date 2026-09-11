using FrenchExDev.Net.FiniteStateMachine.Rich;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Rich;

public class RichFireTests
{
    private static RichStateMachineDefinition<IOrderState, IOrderEvent> CreateOrderMachine()
    {
        return new RichStateMachineBuilder<IOrderState, IOrderEvent>()
            .InitialState(new CreatedState())
            .FinalState<DeliveredState>()
            .FinalState<CancelledState>()
            .When<CreatedState>()
                .On<SubmitEvent>()
                    .TransitionTo((evt, _) => new SubmittedState(DateTime.UtcNow))
                    .WithGuard((evt, _) => evt.Items.Count > 0)
            .When<SubmittedState>()
                .On<ApproveEvent>()
                    .TransitionTo((evt, _) => new ApprovedState(evt.ApprovedBy))
            .When<ApprovedState>()
                .On<ShipEvent>()
                    .TransitionTo((evt, _) => new ShippedState(evt.TrackingNumber))
            .When<ShippedState>()
                .On<DeliverEvent>()
                    .TransitionTo((_, _) => new DeliveredState(DateTime.UtcNow))
            .Build()
            .Value!;
    }

    [Fact]
    public async Task FireAsync_ValidTransition_ComputesTargetState()
    {
        var machine = CreateOrderMachine();

        var result = await machine.FireAsync(new SubmitEvent(new List<string> { "item1" }));

        Assert.True(result.IsSuccess);
        Assert.IsType<SubmittedState>(machine.CurrentState);
    }

    [Fact]
    public async Task FireAsync_EventPayloadCarriedToState()
    {
        var machine = CreateOrderMachine();

        await machine.FireAsync(new SubmitEvent(new List<string> { "item1" }));
        await machine.FireAsync(new ApproveEvent("Manager"));

        var approved = Assert.IsType<ApprovedState>(machine.CurrentState);
        Assert.Equal("Manager", approved.ApprovedBy);
    }

    [Fact]
    public async Task FireAsync_TrackingNumberCarriedToShipped()
    {
        var machine = CreateOrderMachine();

        await machine.FireAsync(new SubmitEvent(new List<string> { "item1" }));
        await machine.FireAsync(new ApproveEvent("Boss"));
        await machine.FireAsync(new ShipEvent("TRACK-42"));

        var shipped = Assert.IsType<ShippedState>(machine.CurrentState);
        Assert.Equal("TRACK-42", shipped.TrackingNumber);
    }

    [Fact]
    public async Task FireAsync_WithFailingGuard_Denied()
    {
        var machine = CreateOrderMachine();

        var result = await machine.FireAsync(new SubmitEvent(new List<string>())); // empty items

        Assert.True(result.IsFailure);
        Assert.IsType<CreatedState>(machine.CurrentState);
    }

    [Fact]
    public async Task FireAsync_InvalidTransition_Denied()
    {
        var machine = CreateOrderMachine();

        var result = await machine.FireAsync(new DeliverEvent()); // can't deliver from Created

        Assert.True(result.IsFailure);
        Assert.IsType<CreatedState>(machine.CurrentState);
    }

    [Fact]
    public async Task FireAsync_FullHappyPath()
    {
        var machine = CreateOrderMachine();

        await machine.FireAsync(new SubmitEvent(new List<string> { "a" }));
        await machine.FireAsync(new ApproveEvent("Admin"));
        await machine.FireAsync(new ShipEvent("TR-1"));
        await machine.FireAsync(new DeliverEvent());

        Assert.IsType<DeliveredState>(machine.CurrentState);
    }

    [Fact]
    public async Task CanFireAsync_ReturnsCorrectValues()
    {
        var machine = CreateOrderMachine();

        Assert.True(await machine.CanFireAsync(new SubmitEvent(new List<string> { "x" })));
        Assert.False(await machine.CanFireAsync(new SubmitEvent(new List<string>()))); // guard fails
        Assert.False(await machine.CanFireAsync(new DeliverEvent()));
    }
}
