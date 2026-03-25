using FrenchExDev.Net.FiniteStateMachine.Graph;
using FrenchExDev.Net.FiniteStateMachine.Typed;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Graph;

public enum TrafficState { Red, Yellow, Green }
public enum TrafficEvent { Next }

public class GraphTests
{
    private static StateGraph<TrafficState, TrafficEvent> CreateTrafficGraph()
    {
        var graph = new StateGraph<TrafficState, TrafficEvent>(TrafficState.Red);
        graph.AddTransition(TrafficState.Red, TrafficEvent.Next, TrafficState.Green);
        graph.AddTransition(TrafficState.Green, TrafficEvent.Next, TrafficState.Yellow);
        graph.AddTransition(TrafficState.Yellow, TrafficEvent.Next, TrafficState.Red);
        return graph;
    }

    [Fact]
    public void AllStates_ReturnsAllThreeStates()
    {
        var graph = CreateTrafficGraph();
        Assert.Equal(3, graph.AllStates.Count);
        Assert.Contains(TrafficState.Red, graph.AllStates);
        Assert.Contains(TrafficState.Green, graph.AllStates);
        Assert.Contains(TrafficState.Yellow, graph.AllStates);
    }

    [Fact]
    public void ReachableStates_FromInitial_ReturnsAll()
    {
        var graph = CreateTrafficGraph();
        Assert.Equal(3, graph.ReachableStates.Count);
    }

    [Fact]
    public void UnreachableStates_InCyclicGraph_ReturnsEmpty()
    {
        var graph = CreateTrafficGraph();
        Assert.Empty(graph.UnreachableStates);
    }

    [Fact]
    public void DeadEndStates_InCyclicGraph_ReturnsEmpty()
    {
        var graph = CreateTrafficGraph();
        Assert.Empty(graph.DeadEndStates);
    }

    [Fact]
    public void DeadEndStates_WithTerminalState_ReturnsIt()
    {
        var graph = new StateGraph<string, string>("A");
        graph.AddTransition("A", "Go", "B");
        graph.AddTransition("B", "End", "C");
        // C has no outgoing transitions → dead-end
        Assert.Contains("C", graph.DeadEndStates);
    }

    [Fact]
    public void UnreachableStates_WithOrphan_ReturnsIt()
    {
        var graph = new StateGraph<string, string>("A");
        graph.AddTransition("A", "Go", "B");
        graph.AddTransition("C", "Back", "A"); // C exists but unreachable from A
        Assert.Contains("C", graph.UnreachableStates);
    }

    [Fact]
    public void GetReachableFrom_ReturnsTransitiveClosure()
    {
        var graph = new StateGraph<string, string>("A");
        graph.AddTransition("A", "1", "B");
        graph.AddTransition("B", "2", "C");
        graph.AddTransition("C", "3", "D");

        var reachable = graph.GetReachableFrom("A");
        Assert.Contains("B", reachable);
        Assert.Contains("C", reachable);
        Assert.Contains("D", reachable);
    }

    [Fact]
    public void MermaidExporter_ProducesValidOutput()
    {
        var graph = CreateTrafficGraph();
        var mermaid = MermaidExporter.Export(graph);

        Assert.Contains("stateDiagram-v2", mermaid);
        Assert.Contains("[*] --> Red", mermaid);
        Assert.Contains("Red --> Green : Next", mermaid);
        Assert.Contains("Green --> Yellow : Next", mermaid);
        Assert.Contains("Yellow --> Red : Next", mermaid);
    }

    [Fact]
    public void DotExporter_ProducesValidOutput()
    {
        var graph = CreateTrafficGraph();
        var dot = DotExporter.Export(graph);

        Assert.Contains("digraph", dot);
        Assert.Contains("rankdir=LR", dot);
        Assert.Contains("Red -> Green", dot);
    }

    [Fact]
    public void MermaidExporter_FromDefinition_Works()
    {
        var definition = new TypedStateMachineBuilder<TrafficState, TrafficEvent>()
            .InitialState(TrafficState.Red)
            .When(TrafficState.Red)
                .On(TrafficEvent.Next).TransitionTo(TrafficState.Green)
            .When(TrafficState.Green)
                .On(TrafficEvent.Next).TransitionTo(TrafficState.Yellow)
            .When(TrafficState.Yellow)
                .On(TrafficEvent.Next).TransitionTo(TrafficState.Red)
            .Build()
            .Value!;

        var mermaid = MermaidExporter.Export(definition);

        Assert.Contains("Red --> Green : Next", mermaid);
    }
}
