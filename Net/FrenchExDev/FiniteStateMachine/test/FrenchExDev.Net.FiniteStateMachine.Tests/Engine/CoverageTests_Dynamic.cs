using FrenchExDev.Net.FiniteStateMachine.Dynamic;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

/// <summary>
/// Coverage tests for DynamicStateMachineDefinition string comparison.
/// </summary>
public class CoverageTests_Dynamic
{
    // ── DynamicStateMachineDefinition StringTupleComparer ──
    [Fact]
    public void DynamicDefinition_CanFire_UsesOrdinalComparison()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("a")
            .When("a").On("go").TransitionTo("b")
            .Build().Value!;

        Assert.True(definition.CanFire("a", "go"));
        Assert.False(definition.CanFire("a", "Go")); // case sensitive
    }

    [Fact]
    public void DynamicDefinition_NullSourceKey_GetHashCodeHandlesNull()
    {
        // Construct a definition with null source to exercise null-coalescing in GetHashCode
        var transitions = new List<TransitionDefinition<string, string>>
        {
            new TransitionDefinition<string, string>(
                null!, "Go", "B",
                Array.Empty<IGuard<string, string>>(),
                Array.Empty<ITransitionAction<string, string>>())
        };

        var definition = new DynamicStateMachineDefinition(
            "A",
            new HashSet<string> { "A", "B" },
            new HashSet<string>(),
            transitions,
            new Dictionary<string, List<IStateAction<string, string>>>(),
            new Dictionary<string, List<IStateAction<string, string>>>());

        // CanFire will hash (null, "Go") via the StringTupleComparer
        Assert.True(definition.CanFire(null!, "Go"));
    }

    [Fact]
    public void DynamicDefinition_NullEventKey_GetHashCodeHandlesNull()
    {
        // Construct a definition with null event to exercise null-coalescing in GetHashCode
        var transitions = new List<TransitionDefinition<string, string>>
        {
            new TransitionDefinition<string, string>(
                "A", null!, "B",
                Array.Empty<IGuard<string, string>>(),
                Array.Empty<ITransitionAction<string, string>>())
        };

        var definition = new DynamicStateMachineDefinition(
            "A",
            new HashSet<string> { "A", "B" },
            new HashSet<string>(),
            transitions,
            new Dictionary<string, List<IStateAction<string, string>>>(),
            new Dictionary<string, List<IStateAction<string, string>>>());

        Assert.True(definition.CanFire("A", null!));
    }
}
