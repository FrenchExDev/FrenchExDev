namespace FrenchExDev.Net.Diem.Workflow.StateMachine;

public sealed class WorkflowTransitionedEvent
{
    public required Guid EntityId { get; init; }
    public required string WorkflowName { get; init; }
    public required string Action { get; init; }
    public required string FromStage { get; init; }
    public required string ToStage { get; init; }
    public required DateTimeOffset TransitionedAt { get; init; }
    public string? TransitionedBy { get; init; }
}
