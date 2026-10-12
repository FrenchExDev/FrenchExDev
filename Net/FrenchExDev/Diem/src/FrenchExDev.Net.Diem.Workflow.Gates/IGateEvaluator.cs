namespace FrenchExDev.Net.Diem.Workflow.Gates;

public interface IGateEvaluator
{
    Task<GateResult> EvaluateAsync(string action, Guid entityId, CancellationToken ct = default);
}

public sealed class GateResult
{
    public bool IsAllowed { get; }
    public string? Reason { get; }
    private GateResult(bool allowed, string? reason) { IsAllowed = allowed; Reason = reason; }
    public static GateResult Allowed() => new(true, null);
    public static GateResult Denied(string reason) => new(false, reason);
}
