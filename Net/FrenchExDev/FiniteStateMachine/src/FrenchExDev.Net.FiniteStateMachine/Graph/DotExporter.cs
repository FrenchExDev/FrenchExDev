using System.Collections.Generic;
using System.Text;

namespace FrenchExDev.Net.FiniteStateMachine.Graph;

public sealed class DotOptions
{
    public string RankDir { get; set; } = "LR";
    public string NodeShape { get; set; } = "circle";
    public string FinalNodeShape { get; set; } = "doublecircle";
}

public static class DotExporter
{
    public static string Export<TState, TEvent>(
        StateGraph<TState, TEvent> graph,
        DotOptions? options = null)
    {
        options ??= new DotOptions();
        var sb = new StringBuilder();
        var deadEnds = graph.DeadEndStates;

        sb.AppendLine($"digraph StateMachine {{");
        sb.AppendLine($"    rankdir={options.RankDir};");

        // Final states with double circle
        if (deadEnds.Count > 0)
        {
            var finals = string.Join(" ", deadEnds);
            sb.AppendLine($"    node [shape={options.FinalNodeShape}]; {finals};");
        }

        sb.AppendLine($"    node [shape={options.NodeShape}];");

        foreach (var (from, evt, to) in graph.Transitions)
        {
            sb.AppendLine($"    {from} -> {to} [label=\"{evt}\"];");
        }

        sb.AppendLine("}");
        return sb.ToString().TrimEnd();
    }

    public static string Export<TState, TEvent>(
        IStateMachineDefinition<TState, TEvent> definition,
        DotOptions? options = null)
    {
        var graph = new StateGraph<TState, TEvent>(definition.InitialState);
        foreach (var t in definition.Transitions)
            graph.AddTransition(t.Source, t.Event, t.Target);
        return Export(graph, options);
    }
}
