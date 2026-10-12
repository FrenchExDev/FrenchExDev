namespace FrenchExDev.Net.Dsl;

/// <summary>
/// Describes a discovered concept at runtime.
/// </summary>
public sealed class ConceptDescriptor
{
    public string Name { get; set; } = "";
    public Type AttributeType { get; set; } = typeof(object);
    public Type ConceptType { get; set; } = typeof(object);
    public IReadOnlyList<string> Inherits { get; set; } = Array.Empty<string>();
    public IReadOnlyList<PropertyDescriptor> Properties { get; set; } = Array.Empty<PropertyDescriptor>();
    public IReadOnlyList<ReferenceDescriptor> References { get; set; } = Array.Empty<ReferenceDescriptor>();
    public IReadOnlyList<ConstraintDescriptor> Constraints { get; set; } = Array.Empty<ConstraintDescriptor>();
}

public sealed class PropertyDescriptor
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Required { get; set; }
}

public sealed class ReferenceDescriptor
{
    public string Name { get; set; } = "";
    public string TargetConcept { get; set; } = "";
    public string Multiplicity { get; set; } = "0..*";
    public bool IsContainment { get; set; }
}

public sealed class ConstraintDescriptor
{
    public string Name { get; set; } = "";
    public string MethodName { get; set; } = "";
    public string? Message { get; set; }
}
