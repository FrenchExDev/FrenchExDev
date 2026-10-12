namespace FrenchExDev.Net.Mapper.Attributes;

/// <summary>
/// Specifies a custom property mapping, mapping the decorated property from a
/// differently-named property on the source type.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MapPropertyAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the property on the source type to map from.
    /// </summary>
    public string SourcePropertyName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MapPropertyAttribute"/> class.
    /// </summary>
    /// <param name="sourcePropertyName">The name of the source property.</param>
    public MapPropertyAttribute(string sourcePropertyName) => SourcePropertyName = sourcePropertyName;
}
