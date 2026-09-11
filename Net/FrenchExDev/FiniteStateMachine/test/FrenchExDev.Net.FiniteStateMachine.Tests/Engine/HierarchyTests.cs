using FrenchExDev.Net.FiniteStateMachine.Dynamic;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

public class HierarchyTests
{
    /// <summary>
    /// Creates a hierarchical FSM:
    ///   Active (parent)
    ///     ├─ Idle (initial sub-state)
    ///     └─ Working
    ///   Done (terminal)
    ///
    /// Active has a "Cancel" transition that applies to all sub-states.
    /// </summary>
    private static (IStateMachine<string, string> machine, StateHierarchy<string> hierarchy) CreateHierarchicalMachine()
    {
        var hierarchy = new StateHierarchy<string>();
        hierarchy.AddChild("Active", "Idle", isInitial: true);
        hierarchy.AddChild("Active", "Working");

        var definition = new DynamicStateMachineBuilder()
            .InitialState("Idle")
            .FinalState("Done")
            .When("Idle")
                .On("Start").TransitionTo("Working")
            .When("Working")
                .On("Finish").TransitionTo("Done")
            .When("Active") // parent transition — applies to Idle and Working
                .On("Cancel").TransitionTo("Done")
            .Build();

        var machine = new StateMachineEngine<string, string>(
            definition.Value!,
            hierarchy: hierarchy);

        return (machine, hierarchy);
    }

    [Fact]
    public async Task ParentTransition_AppliesToSubState()
    {
        var (machine, _) = CreateHierarchicalMachine();

        // Machine starts in Idle (sub-state of Active)
        Assert.Equal("Idle", machine.CurrentState);

        // "Cancel" is defined on Active (parent) — should work from Idle
        var result = await machine.FireAsync("Cancel");

        Assert.True(result.IsSuccess);
        Assert.Equal("Done", machine.CurrentState);
    }

    [Fact]
    public async Task ParentTransition_AppliesToOtherSubState()
    {
        var (machine, _) = CreateHierarchicalMachine();

        // Move to Working
        await machine.FireAsync("Start");
        Assert.Equal("Working", machine.CurrentState);

        // "Cancel" is defined on Active (parent) — should work from Working too
        var result = await machine.FireAsync("Cancel");

        Assert.True(result.IsSuccess);
        Assert.Equal("Done", machine.CurrentState);
    }

    [Fact]
    public async Task SubStateTransition_TakesPriorityOverParent()
    {
        var (machine, _) = CreateHierarchicalMachine();

        // "Start" is defined on Idle (not on Active) — sub-state transition
        var result = await machine.FireAsync("Start");

        Assert.True(result.IsSuccess);
        Assert.Equal("Working", machine.CurrentState);
    }

    [Fact]
    public void IsInState_CurrentState_ReturnsTrue()
    {
        var (machine, hierarchy) = CreateHierarchicalMachine();

        Assert.True(hierarchy.IsInState("Idle", "Idle"));
    }

    [Fact]
    public void IsInState_ParentState_ReturnsTrue()
    {
        var (machine, hierarchy) = CreateHierarchicalMachine();

        // Idle is a sub-state of Active
        Assert.True(hierarchy.IsInState("Idle", "Active"));
    }

    [Fact]
    public void IsInState_UnrelatedState_ReturnsFalse()
    {
        var (machine, hierarchy) = CreateHierarchicalMachine();

        Assert.False(hierarchy.IsInState("Idle", "Done"));
    }

    [Fact]
    public void GetLCA_SiblingsReturnParent()
    {
        var (_, hierarchy) = CreateHierarchicalMachine();

        Assert.True(hierarchy.TryGetLCA("Idle", "Working", out var lca));
        Assert.Equal("Active", lca);
    }

    [Fact]
    public void GetChildren_ReturnsSubStates()
    {
        var (_, hierarchy) = CreateHierarchicalMachine();

        var children = hierarchy.GetChildren("Active");

        Assert.Contains("Idle", children);
        Assert.Contains("Working", children);
    }

    [Fact]
    public void GetInitialChild_ReturnsCorrectChild()
    {
        var (_, hierarchy) = CreateHierarchicalMachine();

        Assert.True(hierarchy.TryGetInitialChild("Active", out var child));
        Assert.Equal("Idle", child);
    }

    [Fact]
    public async Task CanFireAsync_ParentTransition_ReturnsTrue()
    {
        var (machine, _) = CreateHierarchicalMachine();

        // Cancel is on parent Active, should be fireable from Idle
        Assert.True(await machine.CanFireAsync("Cancel"));
    }
}
