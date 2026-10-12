namespace FrenchExDev.Net.Entity.Dsl.Abstractions;

/// <summary>
/// Provides the current user identifier for <c>[Blameable]</c> behavior.
/// Developer implements and registers with <c>[Injectable(Scope = Scope.Scoped)]</c>.
/// </summary>
public interface ICurrentUserProvider
{
    string? GetCurrentUserId();
}
