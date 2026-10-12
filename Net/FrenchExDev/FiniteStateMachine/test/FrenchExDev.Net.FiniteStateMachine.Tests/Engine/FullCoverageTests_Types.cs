namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

/// <summary>
/// Coverage tests for TransitionDefinition and Transition value types.
/// </summary>
public class FullCoverageTests_Types
{
    // ═══════════════════════════════════════════════════════
    // TransitionDefinition — IsInternal flag
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void TransitionDefinition_IsInternal_DefaultsFalse()
    {
        var td = new TransitionDefinition<string, string>(
            "A", "Go", "B",
            Array.Empty<IGuard<string, string>>(),
            Array.Empty<ITransitionAction<string, string>>());

        Assert.False(td.IsInternal);
    }

    [Fact]
    public void TransitionDefinition_IsInternal_WhenSetTrue()
    {
        var td = new TransitionDefinition<string, string>(
            "A", "Go", "A",
            Array.Empty<IGuard<string, string>>(),
            Array.Empty<ITransitionAction<string, string>>(),
            isInternal: true);

        Assert.True(td.IsInternal);
    }

    // ═══════════════════════════════════════════════════════
    // Transition — properties
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void Transition_Properties_Work()
    {
        var t = new Transition<string>("A", "B", false);
        Assert.Equal("A", t.From);
        Assert.Equal("B", t.To);
        Assert.False(t.IsReentrant);
    }

    [Fact]
    public void Transition_Reentrant_IsReentrantTrue()
    {
        var t = new Transition<string>("A", "A", true);
        Assert.True(t.IsReentrant);
    }
}
