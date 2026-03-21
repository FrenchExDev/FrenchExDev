namespace FrenchExDev.Net.Requirements;

public readonly struct AcceptanceCriterionResult : IEquatable<AcceptanceCriterionResult>
{
    public bool IsSatisfied { get; }
    public string FailureReason { get; }

    private AcceptanceCriterionResult(bool satisfied, string failureReason)
    {
        IsSatisfied = satisfied;
        FailureReason = failureReason;
    }

    public static AcceptanceCriterionResult Satisfied() => new AcceptanceCriterionResult(true, string.Empty);
    public static AcceptanceCriterionResult Failed(string reason) => new AcceptanceCriterionResult(false, reason);

    public static implicit operator bool(AcceptanceCriterionResult r) => r.IsSatisfied;

    public bool Equals(AcceptanceCriterionResult other) => IsSatisfied == other.IsSatisfied && FailureReason == other.FailureReason;
    public override bool Equals(object? obj) => obj is AcceptanceCriterionResult other && Equals(other);
    public override int GetHashCode() => IsSatisfied.GetHashCode() ^ (FailureReason?.GetHashCode() ?? 0);
    public static bool operator ==(AcceptanceCriterionResult left, AcceptanceCriterionResult right) => left.Equals(right);
    public static bool operator !=(AcceptanceCriterionResult left, AcceptanceCriterionResult right) => !left.Equals(right);
}
