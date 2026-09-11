using FrenchExDev.Net.Dsl.Concepts;

namespace FrenchExDev.Net.Dsl;

/// <summary>
/// M3 primitive: declares a typed configuration slot on a concept.
/// </summary>
[MetaConcept(typeof(MetaPropertyConcept))]
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class MetaPropertyAttribute : Attribute
{
    public string Name { get; }
    public string Type { get; }
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }

    public MetaPropertyAttribute(string name, string type)
    {
        Name = name;
        Type = type;
    }
}
