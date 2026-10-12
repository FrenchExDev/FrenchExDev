namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

/// <summary>
/// Coverage tests for StateHierarchy: TryGetLCA, GetChildren, TryGetParent,
/// HasHierarchy, TryGetInitialChild.
/// </summary>
public class FullCoverageTests_Hierarchy
{
    [Fact]
    public void StateHierarchy_TryGetLCA_NoCommonAncestor_ReturnsFalse()
    {
        var hierarchy = new StateHierarchy<string>();
        hierarchy.AddChild("ParentA", "ChildA");
        hierarchy.AddChild("ParentB", "ChildB");

        var found = hierarchy.TryGetLCA("ChildA", "ChildB", out var lca);
        Assert.False(found);
    }

    [Fact]
    public void StateHierarchy_TryGetLCA_OneStateIsAncestorOfOther()
    {
        var hierarchy = new StateHierarchy<string>();
        hierarchy.AddChild("Root", "Middle");
        hierarchy.AddChild("Middle", "Leaf");

        var found = hierarchy.TryGetLCA("Root", "Leaf", out var lca);
        Assert.True(found);
        Assert.Equal("Root", lca);
    }

    [Fact]
    public void StateHierarchy_TryGetLCA_BIsAncestorOfA()
    {
        var hierarchy = new StateHierarchy<string>();
        hierarchy.AddChild("Root", "Middle");
        hierarchy.AddChild("Middle", "Leaf");

        // b ("Middle") is ancestor-of-a ("Leaf")
        var found = hierarchy.TryGetLCA("Leaf", "Middle", out var lca);
        Assert.True(found);
        Assert.Equal("Middle", lca);
    }

    [Fact]
    public void StateHierarchy_GetChildren_NoChildren_ReturnsEmpty()
    {
        var hierarchy = new StateHierarchy<string>();
        var children = hierarchy.GetChildren("NonExistent");
        Assert.Empty(children);
    }

    [Fact]
    public void StateHierarchy_TryGetParent_NoParent_ReturnsFalse()
    {
        var hierarchy = new StateHierarchy<string>();
        var found = hierarchy.TryGetParent("Orphan", out _);
        Assert.False(found);
    }

    [Fact]
    public void StateHierarchy_HasHierarchy_EmptyHierarchy_ReturnsFalse()
    {
        var hierarchy = new StateHierarchy<string>();
        Assert.False(hierarchy.HasHierarchy);
    }

    [Fact]
    public void StateHierarchy_TryGetInitialChild_NoInitial_ReturnsFalse()
    {
        var hierarchy = new StateHierarchy<string>();
        hierarchy.AddChild("Parent", "Child");
        var found = hierarchy.TryGetInitialChild("Parent", out _);
        Assert.False(found);
    }
}
