namespace FrenchExDev.Net.Injectable.Attributes;

/// <summary>
/// Assembly-level attribute that sets the default <see cref="Scope"/> for all
/// <see cref="InjectableAttribute"/>-decorated classes in the assembly.
/// Individual classes can still override with an explicit <c>Scope</c> property.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class InjectableDefaultsAttribute : Attribute
{
    /// <summary>Default service lifetime applied when a class does not specify one.</summary>
    public Scope Scope { get; set; } = Scope.Transient;
}
