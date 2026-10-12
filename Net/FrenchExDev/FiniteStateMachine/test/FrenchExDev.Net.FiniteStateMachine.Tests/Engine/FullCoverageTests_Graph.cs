using FrenchExDev.Net.FiniteStateMachine.Graph;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

/// <summary>
/// Coverage tests for StateGraph: GetPermittedEvents, AllEvents,
/// GetReachableFrom, UnreachableStates.
/// </summary>
public class FullCoverageTests_Graph
{
    [Fact]
    public void StateGraph_GetPermittedEvents_NonExistentState_ReturnsEmpty()
    {
        var graph = new StateGraph<string, string>("A");
        graph.AddTransition("A", "Go", "B");

        var events = graph.GetPermittedEvents("NonExistent");
        Assert.Empty(events);
    }

    [Fact]
    public void StateGraph_AllEvents_ReturnsAll()
    {
        var graph = new StateGraph<string, string>("A");
        graph.AddTransition("A", "Go", "B");
        graph.AddTransition("B", "Back", "A");

        var events = graph.AllEvents;
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public void StateGraph_GetReachableFrom_TraversesGraph()
    {
        var graph = new StateGraph<string, string>("A");
        graph.AddTransition("A", "Go", "B");
        graph.AddTransition("B", "Go", "C");
        graph.AddTransition("C", "Go", "D");

        var reachable = graph.GetReachableFrom("B");
        Assert.Contains("C", reachable);
        Assert.Contains("D", reachable);
        Assert.DoesNotContain("A", reachable);
    }

    [Fact]
    public void StateGraph_UnreachableStates_IdentifiesOrphans()
    {
        var graph = new StateGraph<string, string>("A");
        graph.AddTransition("A", "Go", "B");
        graph.AddTransition("C", "Go", "D"); // C and D are unreachable from A

        var unreachable = graph.UnreachableStates;
        Assert.Contains("C", unreachable);
    }
}
