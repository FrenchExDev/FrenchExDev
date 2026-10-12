using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FrenchExDev.Net.FiniteStateMachine.Dynamic;

/// <summary>
/// JSON serialization for DynamicStateMachineDefinition.
/// Serializes the graph structure (states, transitions, initial/final).
/// Guards and actions are excluded (they're code — re-attach after deserialization).
/// </summary>
public static class DynamicStateMachineSerializer
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
#if NET10_0_OR_GREATER
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
#endif
    };

    public static string ToJson(this DynamicStateMachineDefinition definition)
    {
        var dto = new FsmDto
        {
            InitialState = definition.InitialState,
            States = new List<string>(definition.States),
            FinalStates = new List<string>(definition.FinalStates),
            Transitions = new List<TransitionDto>()
        };

        foreach (var t in definition.Transitions)
        {
            dto.Transitions.Add(new TransitionDto
            {
                From = t.Source,
                Event = t.Event,
                To = t.Target
            });
        }

        return JsonSerializer.Serialize(dto, Options);
    }

    public static DynamicStateMachineDefinition FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<FsmDto>(json, Options)
            ?? throw new InvalidOperationException("Failed to deserialize FSM definition");

        var builder = new DynamicStateMachineBuilder()
            .InitialState(dto.InitialState);

        foreach (var state in dto.FinalStates)
        {
            builder.FinalState(state);
        }

        // We need to add transitions via the builder's internal method
        foreach (var t in dto.Transitions)
        {
            builder.AddTransition(new TransitionDefinition<string, string>(
                t.From, t.Event, t.To,
                Array.Empty<IGuard<string, string>>(),
                Array.Empty<ITransitionAction<string, string>>()));
        }

        var result = builder.Build();
        if (result.IsFailure)
            throw new InvalidOperationException("Deserialized FSM definition is invalid");

        return result.Value!;
    }

    private sealed class FsmDto
    {
        public string InitialState { get; set; } = "";
        public List<string> States { get; set; } = new List<string>();
        public List<string> FinalStates { get; set; } = new List<string>();
        public List<TransitionDto> Transitions { get; set; } = new List<TransitionDto>();
    }

    private sealed class TransitionDto
    {
        public string From { get; set; } = "";
        public string Event { get; set; } = "";
        public string To { get; set; } = "";
    }
}
