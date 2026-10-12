using FrenchExDev.Net.FiniteStateMachine.Dynamic;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Dynamic;

public class DynamicFireTests
{
    private static IStateMachine<string, string> CreateOrderMachine()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("Created")
            .FinalState("Delivered")
            .FinalState("Cancelled")
            .When("Created")
                .On("Submit").TransitionTo("Submitted")
                .On("Cancel").TransitionTo("Cancelled")
            .When("Submitted")
                .On("Approve").TransitionTo("Approved")
                .On("Reject").TransitionTo("Created")
            .When("Approved")
                .On("Ship").TransitionTo("Shipped")
            .When("Shipped")
                .On("Deliver").TransitionTo("Delivered")
            .Build();

        return definition.Value!.CreateMachine();
    }

    [Fact]
    public async Task FireAsync_ValidTransition_Succeeds()
    {
        var machine = CreateOrderMachine();

        var result = await machine.FireAsync("Submit");

        Assert.True(result.IsSuccess);
        Assert.Equal("Created", result.Value!.From);
        Assert.Equal("Submitted", result.Value.To);
        Assert.Equal("Submitted", machine.CurrentState);
    }

    [Fact]
    public async Task FireAsync_InvalidTransition_ReturnsDenied()
    {
        var machine = CreateOrderMachine();

        var result = await machine.FireAsync("Approve");

        Assert.True(result.IsFailure);
        Assert.Equal("Created", machine.CurrentState);
    }

    [Fact]
    public async Task FireAsync_FullHappyPath_Works()
    {
        var machine = CreateOrderMachine();

        await machine.FireAsync("Submit");
        await machine.FireAsync("Approve");
        await machine.FireAsync("Ship");
        await machine.FireAsync("Deliver");

        Assert.Equal("Delivered", machine.CurrentState);
    }

    [Fact]
    public async Task FireAsync_RejectAndRetry_Works()
    {
        var machine = CreateOrderMachine();

        await machine.FireAsync("Submit");
        await machine.FireAsync("Reject");
        Assert.Equal("Created", machine.CurrentState);

        await machine.FireAsync("Submit");
        Assert.Equal("Submitted", machine.CurrentState);
    }

    [Fact]
    public async Task FireAsync_WithGuard_DeniesWhenFalse()
    {
        var allowed = false;
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A")
                .On("Go").TransitionTo("B")
                    .WithGuard((from, evt, to, ct) => Task.FromResult(allowed))
            .Build();

        var machine = definition.Value!.CreateMachine();

        var result = await machine.FireAsync("Go");
        Assert.True(result.IsFailure);

        allowed = true;
        result = await machine.FireAsync("Go");
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CanFireAsync_ReturnsCorrectValues()
    {
        var machine = CreateOrderMachine();

        Assert.True(await machine.CanFireAsync("Submit"));
        Assert.True(await machine.CanFireAsync("Cancel"));
        Assert.False(await machine.CanFireAsync("Approve"));
    }

    [Fact]
    public async Task GetPermittedEventsAsync_ReturnsCorrectEvents()
    {
        var machine = CreateOrderMachine();

        var events = await machine.GetPermittedEventsAsync();

        Assert.Contains("Submit", events);
        Assert.Contains("Cancel", events);
        Assert.DoesNotContain("Approve", events);
    }
}
