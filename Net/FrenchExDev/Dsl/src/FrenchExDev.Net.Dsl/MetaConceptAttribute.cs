using FrenchExDev.Net.Dsl.Concepts;

namespace FrenchExDev.Net.Dsl;

/// <summary>
/// M3 primitive: declares that an attribute class represents a modeling concept.
/// Self-describing: [MetaConcept(typeof(MetaConceptConcept))] on itself.
/// </summary>
[MetaConcept(typeof(MetaConceptConcept))]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MetaConceptAttribute : Attribute
{
    public Type ConceptType { get; }
    public string? Description { get; set; }

    public MetaConceptAttribute(Type conceptType) => ConceptType = conceptType;
}
