using FrenchExDev.Net.Dsl.Concepts;

namespace FrenchExDev.Net.Dsl;

/// <summary>
/// M3 primitive: declares a validation rule on a concept.
/// References a real C# static method — not a string expression.
/// </summary>
[MetaConcept(typeof(MetaConstraintConcept))]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class MetaConstraintAttribute : Attribute
{
    public string Name { get; }
    public string ConstraintMethodName { get; }
    public string? Message { get; set; }

    public MetaConstraintAttribute(string name, string constraintMethodName)
    {
        Name = name;
        ConstraintMethodName = constraintMethodName;
    }
}
