namespace FrenchExDev.Net.Entity.Dsl.Attributes
{
    /// <summary>
    /// Specifies the delete behavior for a navigation property relationship.
    /// Maps to EF Core's DeleteBehavior enum.
    /// </summary>
    public enum DeleteBehavior
    {
        /// <summary>Cascade delete — dependent entities are deleted when the principal is deleted.</summary>
        Cascade,

        /// <summary>Restrict — prevent deletion of the principal if dependents exist.</summary>
        Restrict,

        /// <summary>No action — the database does not enforce referential integrity.</summary>
        NoAction,

        /// <summary>Set null — foreign key is set to null when the principal is deleted.</summary>
        SetNull,

        /// <summary>Client cascade — cascade is handled client-side, not by the database.</summary>
        ClientCascade,
    }
}
