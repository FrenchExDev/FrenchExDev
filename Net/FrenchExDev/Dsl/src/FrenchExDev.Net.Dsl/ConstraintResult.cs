namespace FrenchExDev.Net.Dsl;

/// <summary>
/// Return type for constraint validation methods.
/// </summary>
public readonly struct ConstraintResult : IEquatable<ConstraintResult>
{
    public bool IsSatisfied { get; }
    public string? Message { get; }

    private ConstraintResult(bool satisfied, string? message)
    {
        IsSatisfied = satisfied;
        Message = message;
    }

    public static ConstraintResult Satisfied() => new ConstraintResult(true, null);
    public static ConstraintResult Failed(string message) => new ConstraintResult(false, message);

    public static ConstraintResult Aggregate(IEnumerable<ConstraintResult> results)
    {
        var failures = new List<string>();
        foreach (var r in results)
        {
            if (!r.IsSatisfied && r.Message is not null)
                failures.Add(r.Message);
        }
        return failures.Count == 0
            ? Satisfied()
            : Failed(string.Join("; ", failures));
    }

    public bool Equals(ConstraintResult other) => IsSatisfied == other.IsSatisfied && Message == other.Message;
    public override bool Equals(object? obj) => obj is ConstraintResult other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (IsSatisfied.GetHashCode() * 397) ^ (Message?.GetHashCode() ?? 0);
        }
    }

    public static bool operator ==(ConstraintResult left, ConstraintResult right) => left.Equals(right);
    public static bool operator !=(ConstraintResult left, ConstraintResult right) => !left.Equals(right);
}
