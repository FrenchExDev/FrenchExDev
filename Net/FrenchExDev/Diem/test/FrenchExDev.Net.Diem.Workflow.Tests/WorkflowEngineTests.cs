using FrenchExDev.Net.Diem.Workflow.StateMachine;
using Xunit;

namespace FrenchExDev.Net.Diem.Workflow.Tests;

public class WorkflowEngineTests
{
    [Fact]
    public void RegisterTransition_StoresTransition()
    {
        var engine = new WorkflowEngine("TestWorkflow");
        engine.RegisterTransition("Draft", "Submit", "Review");

        var target = engine.TryGetTarget("Draft", "Submit");
        Assert.Equal("Review", target);
    }

    [Fact]
    public void TryGetTarget_ReturnsCorrectTarget()
    {
        var engine = new WorkflowEngine("TestWorkflow");
        engine.RegisterTransition("Draft", "Submit", "Review");
        engine.RegisterTransition("Review", "Approve", "Published");
        engine.RegisterTransition("Review", "Reject", "Draft");

        Assert.Equal("Review", engine.TryGetTarget("Draft", "Submit"));
        Assert.Equal("Published", engine.TryGetTarget("Review", "Approve"));
        Assert.Equal("Draft", engine.TryGetTarget("Review", "Reject"));
    }

    [Fact]
    public void TryGetTarget_ReturnsNull_ForInvalidTransition()
    {
        var engine = new WorkflowEngine("TestWorkflow");
        engine.RegisterTransition("Draft", "Submit", "Review");

        Assert.Null(engine.TryGetTarget("Draft", "Approve"));
        Assert.Null(engine.TryGetTarget("Published", "Submit"));
    }

    [Fact]
    public void GetAvailableActions_ReturnsCorrectActions()
    {
        var engine = new WorkflowEngine("TestWorkflow");
        engine.RegisterTransition("Review", "Approve", "Published");
        engine.RegisterTransition("Review", "Reject", "Draft");
        engine.RegisterTransition("Draft", "Submit", "Review");

        var actions = engine.GetAvailableActions("Review");
        Assert.Equal(2, actions.Count);
        Assert.Contains("Approve", actions);
        Assert.Contains("Reject", actions);
    }

    [Fact]
    public void GetAvailableActions_ReturnsEmpty_ForTerminalStage()
    {
        var engine = new WorkflowEngine("TestWorkflow");
        engine.RegisterTransition("Draft", "Submit", "Review");

        var actions = engine.GetAvailableActions("Published");
        Assert.Empty(actions);
    }

    [Fact]
    public void WorkflowName_ReturnsConfiguredName()
    {
        var engine = new WorkflowEngine("ArticleWorkflow");
        Assert.Equal("ArticleWorkflow", engine.WorkflowName);
    }
}
