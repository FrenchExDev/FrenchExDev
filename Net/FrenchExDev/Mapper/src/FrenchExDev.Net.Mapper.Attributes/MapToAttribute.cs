namespace FrenchExDev.Net.Mapper.Attributes;

/// <summary>
/// Marks a class or struct as a mapping source, indicating that a mapper should be
/// generated to convert instances of the decorated type to <see cref="TargetType"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class MapToAttribute : Attribute
{
    /// <summary>
    /// Gets the target type that instances of the decorated type will be mapped to.
    /// </summary>
    public Type TargetType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MapToAttribute"/> class.
    /// </summary>
    /// <param name="targetType">The target type to map to.</param>
    public MapToAttribute(Type targetType) => TargetType = targetType;
}
