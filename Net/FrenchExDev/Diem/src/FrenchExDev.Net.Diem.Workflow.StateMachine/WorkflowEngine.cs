namespace FrenchExDev.Net.Diem.Workflow.StateMachine;

public class WorkflowEngine
{
    private readonly Dictionary<(string From, string Action), string> _transitions = new();
    private readonly string _workflowName;

    public WorkflowEngine(string workflowName) { _workflowName = workflowName; }

    public string WorkflowName => _workflowName;

    public void RegisterTransition(string from, string action, string to)
        => _transitions[(from, action)] = to;

    public string? TryGetTarget(string currentStage, string action)
        => _transitions.TryGetValue((currentStage, action), out var target) ? target : null;

    public IReadOnlyList<string> GetAvailableActions(string currentStage)
        => _transitions.Keys.Where(k => k.From == currentStage).Select(k => k.Action).ToList();
}
