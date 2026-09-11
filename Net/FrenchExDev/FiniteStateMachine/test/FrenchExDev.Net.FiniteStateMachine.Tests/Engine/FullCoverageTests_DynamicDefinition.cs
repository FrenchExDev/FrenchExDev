using FrenchExDev.Net.FiniteStateMachine.Dynamic;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

/// <summary>
/// Coverage tests for DynamicStateMachineDefinition.
/// </summary>
public class FullCoverageTests_DynamicDefinition
{
    [Fact]
    public void DynamicDefinition_GetEntryActions_NoActions_ReturnsEmpty()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A").On("Go").TransitionTo("B")
            .Build().Value!;

        Assert.Empty(definition.GetEntryActions("A"));
    }

    [Fact]
    public void DynamicDefinition_GetExitActions_NoActions_ReturnsEmpty()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A").On("Go").TransitionTo("B")
            .Build().Value!;

        Assert.Empty(definition.GetExitActions("A"));
    }

    [Fact]
    public void DynamicDefinition_GetEntryActions_WithActions_ReturnsThem()
    {
        var action = new DelegateStateAction<string, string>((s, e, ct) => Task.CompletedTask);
        var entryActions = new Dictionary<string, List<IStateAction<string, string>>>
        {
            { "B", new List<IStateAction<string, string>> { action } }
        };

        var transitions = new List<TransitionDefinition<string, string>>
        {
            new TransitionDefinition<string, string>(
                "A", "Go", "B",
                Array.Empty<IGuard<string, string>>(),
                Array.Empty<ITransitionAction<string, string>>())
        };

        var definition = new DynamicStateMachineDefinition(
            "A",
            new HashSet<string> { "A", "B" },
            new HashSet<string>(),
            transitions,
            entryActions,
            new Dictionary<string, List<IStateAction<string, string>>>());

        var actions = definition.GetEntryActions("B");
        Assert.Single(actions);
    }

    [Fact]
    public void DynamicDefinition_GetExitActions_WithActions_ReturnsThem()
    {
        var action = new DelegateStateAction<string, string>((s, e, ct) => Task.CompletedTask);
        var exitActions = new Dictionary<string, List<IStateAction<string, string>>>
        {
            { "A", new List<IStateAction<string, string>> { action } }
        };

        var transitions = new List<TransitionDefinition<string, string>>
        {
            new TransitionDefinition<string, string>(
                "A", "Go", "B",
                Array.Empty<IGuard<string, string>>(),
                Array.Empty<ITransitionAction<string, string>>())
        };

        var definition = new DynamicStateMachineDefinition(
            "A",
            new HashSet<string> { "A", "B" },
            new HashSet<string>(),
            transitions,
            new Dictionary<string, List<IStateAction<string, string>>>(),
            exitActions);

        var actions = definition.GetExitActions("A");
        Assert.Single(actions);
    }

    [Fact]
    public void DynamicDefinition_GetPermittedEvents_NoTransitions_ReturnsEmpty()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A").On("Go").TransitionTo("B")
            .Build().Value!;

        Assert.Empty(definition.GetPermittedEvents("B")); // B has no outgoing transitions
    }

    [Fact]
    public void DynamicDefinition_GetPermittedEvents_WithTransitions_ReturnsEvents()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A")
                .On("Go").TransitionTo("B")
                .On("Stay").InternalTransition()
            .Build().Value!;

        var events = definition.GetPermittedEvents("A");
        Assert.Equal(2, events.Count);
    }

    // ═══════════════════════════════════════════════════════
    // Helper types
    // ═══════════════════════════════════════════════════════

    private sealed class DelegateStateAction<TState, TEvent> : IStateAction<TState, TEvent>
    {
        private readonly Func<TState, TEvent, CancellationToken, Task> _func;
        public DelegateStateAction(Func<TState, TEvent, CancellationToken, Task> func) => _func = func;
        public Task ExecuteAsync(TState state, TEvent @event, CancellationToken ct) => _func(state, @event, ct);
    }
}
