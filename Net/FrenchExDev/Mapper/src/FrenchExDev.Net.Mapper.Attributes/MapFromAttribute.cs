namespace FrenchExDev.Net.Mapper.Attributes;

/// <summary>
/// Marks a class or struct as a mapping target, indicating that a mapper should be
/// generated to convert instances of <see cref="SourceType"/> to the decorated type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class MapFromAttribute : Attribute
{
    /// <summary>
    /// Gets the source type that will be mapped to instances of the decorated type.
    /// </summary>
    public Type SourceType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MapFromAttribute"/> class.
    /// </summary>
    /// <param name="sourceType">The source type to map from.</param>
    public MapFromAttribute(Type sourceType) => SourceType = sourceType;
}
