using System.Text;

namespace FrenchExDev.Net.FiniteStateMachine.Graph;

public sealed class MermaidOptions
{
    public string Direction { get; set; } = "LR";
    public bool HighlightFinalStates { get; set; } = true;
}

public static class MermaidExporter
{
    public static string Export<TState, TEvent>(
        StateGraph<TState, TEvent> graph,
        MermaidOptions? options = null)
    {
        options ??= new MermaidOptions();
        var sb = new StringBuilder();

        sb.AppendLine("stateDiagram-v2");
        sb.AppendLine($"    [*] --> {graph.InitialState}");

        foreach (var (from, evt, to) in graph.Transitions)
        {
            sb.AppendLine($"    {from} --> {to} : {evt}");
        }

        if (options.HighlightFinalStates)
        {
            foreach (var state in graph.DeadEndStates)
            {
                sb.AppendLine($"    {state} --> [*]");
            }
        }

        return sb.ToString().TrimEnd();
    }

    public static string Export<TState, TEvent>(
        IStateMachineDefinition<TState, TEvent> definition,
        MermaidOptions? options = null)
    {
        var graph = new StateGraph<TState, TEvent>(definition.InitialState);
        foreach (var t in definition.Transitions)
            graph.AddTransition(t.Source, t.Event, t.Target);
        return Export(graph, options);
    }
}
