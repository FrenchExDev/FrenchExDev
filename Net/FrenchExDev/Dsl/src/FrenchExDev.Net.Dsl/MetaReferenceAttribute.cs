using FrenchExDev.Net.Dsl.Concepts;

namespace FrenchExDev.Net.Dsl;

/// <summary>
/// M3 primitive: declares a directed association between concepts.
/// </summary>
[MetaConcept(typeof(MetaReferenceConcept))]
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class MetaReferenceAttribute : Attribute
{
    public string Name { get; }
    public string TargetConcept { get; }
    public string Multiplicity { get; set; } = "0..*";
    public bool IsContainment { get; set; }
    public string? Opposite { get; set; }

    public MetaReferenceAttribute(string name, string targetConcept)
    {
        Name = name;
        TargetConcept = targetConcept;
    }
}
