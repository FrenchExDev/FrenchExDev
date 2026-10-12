namespace FrenchExDev.Net.Injectable.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = true)]
public sealed class InjectableAttribute : Attribute
{
    /// <summary>Service lifetime in the DI container.</summary>
    public Scope Scope { get; set; } = Scope.Transient;

    /// <summary>
    /// Explicit service interfaces to register as.
    /// When null, auto-detects: registers for each directly implemented interface; self only if none.
    /// All types must be interfaces — the analyzer enforces this.
    /// </summary>
    /// <example>
    /// <code>[Injectable(As = new[] { typeof(IReader), typeof(IWriter) })]</code>
    /// </example>
    public Type[]? As { get; set; }

    /// <summary>
    /// Service key for keyed DI registration (.NET 8+).
    /// When set, generates <c>AddKeyed{Scope}</c> calls inside a <c>#if NET8_0_OR_GREATER</c> block.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// When true, generates <c>TryAdd{Scope}</c> instead of <c>Add{Scope}</c>.
    /// The service is only registered if no existing registration for the same service type exists.
    /// </summary>
    public bool TryAdd { get; set; }
}
