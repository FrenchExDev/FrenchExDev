using FrenchExDev.Net.FiniteStateMachine.Dynamic;
using FrenchExDev.Net.FiniteStateMachine.Testing;
using FrenchExDev.Net.FiniteStateMachine.Tests.Typed;
using FrenchExDev.Net.FiniteStateMachine.Typed;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

public class TestingHelperTests
{
    private static IStateMachine<string, string> CreateOrderMachine()
    {
        return new DynamicStateMachineBuilder()
            .InitialState("Created")
            .FinalState("Delivered")
            .FinalState("Cancelled")
            .When("Created")
                .On("Submit").TransitionTo("Submitted")
                .On("Cancel").TransitionTo("Cancelled")
            .When("Submitted")
                .On("Approve").TransitionTo("Approved")
            .When("Approved")
                .On("Ship").TransitionTo("Shipped")
            .When("Shipped")
                .On("Deliver").TransitionTo("Delivered")
            .Build().Value!.CreateMachine();
    }

    [Fact]
    public async Task TransitionsToAsync_Succeeds()
    {
        var machine = CreateOrderMachine();
        await StateMachineAssert.TransitionsToAsync(machine, "Submit", "Submitted");
    }

    [Fact]
    public async Task TransitionsToAsync_ThrowsOnDenied()
    {
        var machine = CreateOrderMachine();
        await Assert.ThrowsAsync<StateMachineAssertException>(() =>
            StateMachineAssert.TransitionsToAsync(machine, "Approve", "Approved"));
    }

    [Fact]
    public async Task PathReachesAsync_Succeeds()
    {
        var machine = CreateOrderMachine();
        await StateMachineAssert.PathReachesAsync(machine,
            new[] { "Submit", "Approve", "Ship", "Deliver" },
            "Delivered");
    }

    [Fact]
    public async Task PathReachesAsync_ThrowsOnWrongFinalState()
    {
        var machine = CreateOrderMachine();
        await Assert.ThrowsAsync<StateMachineAssertException>(() =>
            StateMachineAssert.PathReachesAsync(machine, new[] { "Submit" }, "Approved"));
    }

    [Fact]
    public async Task IsDeniedAsync_Succeeds()
    {
        var machine = CreateOrderMachine();
        await StateMachineAssert.IsDeniedAsync(machine, "Approve");
    }

    [Fact]
    public async Task IsDeniedAsync_ThrowsWhenNotDenied()
    {
        var machine = CreateOrderMachine();
        await Assert.ThrowsAsync<StateMachineAssertException>(() =>
            StateMachineAssert.IsDeniedAsync(machine, "Submit"));
    }
}

public class PathGeneratorTests
{
    [Fact]
    public void AllPaths_FindsAllTerminalPaths()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .FinalState("C")
            .FinalState("D")
            .When("A")
                .On("ToB").TransitionTo("B")
                .On("ToD").TransitionTo("D")
            .When("B")
                .On("ToC").TransitionTo("C")
            .Build().Value!;

        var paths = StateMachinePathGenerator.AllPaths(definition);

        // Two paths: A→B→C and A→D
        Assert.Equal(2, paths.Count);
    }

    [Fact]
    public void AllPaths_HandlesCycles()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .FinalState("C")
            .When("A")
                .On("Next").TransitionTo("B")
            .When("B")
                .On("Back").TransitionTo("A")
                .On("End").TransitionTo("C")
            .Build().Value!;

        var paths = StateMachinePathGenerator.AllPaths(definition);

        // Should find A→B→C without infinite loop
        Assert.True(paths.Count >= 1);
        Assert.Contains(paths, p => p.FinalState == "C");
    }

    [Fact]
    public void ShortestPath_FindsDirectPath()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A")
                .On("1").TransitionTo("B")
                .On("Direct").TransitionTo("D")
            .When("B")
                .On("2").TransitionTo("C")
            .When("C")
                .On("3").TransitionTo("D")
            .Build().Value!;

        var path = StateMachinePathGenerator.ShortestPath(definition, "A", "D");

        Assert.NotNull(path);
        var step = Assert.Single(path!.Steps); // A --Direct--> D
        Assert.Equal("D", path.FinalState);
    }

    [Fact]
    public void ShortestPath_ReturnsNull_IfNoPathExists()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A")
                .On("Go").TransitionTo("B")
            .Build().Value!;

        var path = StateMachinePathGenerator.ShortestPath(definition, "B", "A");

        Assert.Null(path); // No path from B to A
    }

    [Fact]
    public void PathToString_IsReadable()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A")
                .On("Go").TransitionTo("B")
            .Build().Value!;

        var paths = StateMachinePathGenerator.AllPaths(definition);

        var singlePath = Assert.Single(paths);
        var str = singlePath.ToString();
        Assert.Contains("A", str);
        Assert.Contains("Go", str);
        Assert.Contains("B", str);
    }
}
