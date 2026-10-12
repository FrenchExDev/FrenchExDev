using FrenchExDev.Net.FiniteStateMachine.Dynamic;
using FrenchExDev.Net.FiniteStateMachine.Graph;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

/// <summary>
/// Coverage tests for DotExporter, MermaidExporter, and StateGraph.
/// </summary>
public class CoverageTests_Graph
{
    // ── DotExporter with dead ends ──
    [Fact]
    public void DotExporter_WithDeadEnds_UsesDoubleCircle()
    {
        var graph = new StateGraph<string, string>("A");
        graph.AddTransition("A", "Go", "B"); // B is dead-end

        var dot = DotExporter.Export(graph);

        Assert.Contains("doublecircle", dot);
        Assert.Contains("B", dot);
    }

    [Fact]
    public void DotExporter_FromDefinition_Works()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("X")
            .When("X").On("Y").TransitionTo("Z")
            .Build().Value!;

        var dot = DotExporter.Export(definition);
        Assert.Contains("X -> Z", dot);
    }

    // ── MermaidExporter with final states ──
    [Fact]
    public void MermaidExporter_WithFinalStates_ShowsArrowToEnd()
    {
        var graph = new StateGraph<string, string>("Start");
        graph.AddTransition("Start", "End", "Finish");

        var mermaid = MermaidExporter.Export(graph, new MermaidOptions { HighlightFinalStates = true });

        Assert.Contains("Finish --> [*]", mermaid);
    }

    // ── StateGraph GetPermittedEvents ──
    [Fact]
    public void StateGraph_GetPermittedEvents_ReturnsCorrect()
    {
        var graph = new StateGraph<string, string>("A");
        graph.AddTransition("A", "X", "B");
        graph.AddTransition("A", "Y", "C");

        var events = graph.GetPermittedEvents("A");
        Assert.Equal(2, events.Count);
        Assert.Contains("X", events);
        Assert.Contains("Y", events);
    }
}
