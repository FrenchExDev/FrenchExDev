namespace FrenchExDev.Net.Traefik.Bundle.Attributes;

/// <summary>
/// Marks a generated Traefik model class as a flat discriminated union.
/// Exactly one of its branch properties must be set when the config is built.
/// The companion analyzer (TFK001) reads this attribute to flag object
/// initializers that violate the constraint at compile time.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class TraefikDiscriminatedUnionAttribute : Attribute
{
}
