using FrenchExDev.Net.FiniteStateMachine.Dynamic;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

public class ParallelRegionTests
{
    private static CompositeStateMachine<string, string> CreateOrderComposite()
    {
        var paymentDef = new DynamicStateMachineBuilder()
            .InitialState("Pending")
            .FinalState("Paid")
            .When("Pending")
                .On("Pay").TransitionTo("Paid")
            .Build().Value!;

        var shippingDef = new DynamicStateMachineBuilder()
            .InitialState("NotShipped")
            .FinalState("Delivered")
            .When("NotShipped")
                .On("Ship").TransitionTo("Shipped")
            .When("Shipped")
                .On("Deliver").TransitionTo("Delivered")
            .Build().Value!;

        var terminals = new HashSet<string> { "Paid", "Delivered" };

        return new CompositeStateMachine<string, string>(
            new[]
            {
                ("payment", paymentDef.CreateMachine()),
                ("shipping", shippingDef.CreateMachine())
            },
            state => terminals.Contains(state));
    }

    [Fact]
    public void Regions_AreAccessible()
    {
        var composite = CreateOrderComposite();

        Assert.Equal(2, composite.Regions.Count);
        Assert.NotNull(composite.GetRegion("payment"));
        Assert.NotNull(composite.GetRegion("shipping"));
        Assert.Null(composite.GetRegion("nonexistent"));
    }

    [Fact]
    public void AllRegionsTerminal_InitiallyFalse()
    {
        var composite = CreateOrderComposite();
        Assert.False(composite.AllRegionsTerminal);
    }

    [Fact]
    public async Task FireAllAsync_BroadcastsToAllRegions()
    {
        var composite = CreateOrderComposite();

        // Pay affects payment region, not shipping
        await composite.FireAllAsync("Pay");

        Assert.Equal("Paid", composite.GetRegion("payment")!.Machine.CurrentState);
        Assert.Equal("NotShipped", composite.GetRegion("shipping")!.Machine.CurrentState);
    }

    [Fact]
    public async Task AllRegionsTerminal_TrueWhenAllDone()
    {
        var composite = CreateOrderComposite();

        await composite.GetRegion("payment")!.Machine.FireAsync("Pay");
        await composite.GetRegion("shipping")!.Machine.FireAsync("Ship");
        await composite.GetRegion("shipping")!.Machine.FireAsync("Deliver");

        Assert.True(composite.AllRegionsTerminal);
    }

    [Fact]
    public async Task RegionsAreIndependent()
    {
        var composite = CreateOrderComposite();

        // Ship doesn't exist in payment, so it just fails silently for that region
        var results = await composite.FireAllAsync("Ship");

        Assert.Equal(2, results.Count);
        // Payment has no "Ship" transition — failure
        Assert.True(results[0].IsFailure);
        // Shipping has "Ship" — success
        Assert.True(results[1].IsSuccess);
        Assert.Equal("Shipped", composite.GetRegion("shipping")!.Machine.CurrentState);
    }
}
