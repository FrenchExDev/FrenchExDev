using FrenchExDev.Net.Dsl.Concepts;

namespace FrenchExDev.Net.Dsl;

/// <summary>
/// M3 primitive: declares metamodel-level inheritance between concepts.
/// </summary>
[MetaConcept(typeof(MetaInheritsConcept))]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class MetaInheritsAttribute : Attribute
{
    public Type ParentConceptType { get; }

    public MetaInheritsAttribute(Type parentConceptType) => ParentConceptType = parentConceptType;
}
