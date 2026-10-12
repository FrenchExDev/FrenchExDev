namespace FrenchExDev.Net.Dsl;

/// <summary>
/// Context passed to constraint methods. Filled from Roslyn symbols (compile-time)
/// or reflection (design-time).
/// </summary>
public sealed class ConceptValidationContext
{
    public string ConceptName { get; set; } = "";
    public string TypeName { get; set; } = "";
    public IReadOnlyList<ConceptPropertyInfo> Properties { get; set; } = Array.Empty<ConceptPropertyInfo>();
    public IReadOnlyList<ConceptMethodInfo> Methods { get; set; } = Array.Empty<ConceptMethodInfo>();
    public IReadOnlyList<ConceptReferenceInfo> References { get; set; } = Array.Empty<ConceptReferenceInfo>();
    public IReadOnlyList<string> SuperTypes { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ConceptAttributeInfo> ClassAttributes { get; set; } = Array.Empty<ConceptAttributeInfo>();
}

public sealed class ConceptPropertyInfo
{
    public string Name { get; set; } = "";
    public string TypeName { get; set; } = "";
    public IReadOnlyList<string> AttributeNames { get; set; } = Array.Empty<string>();
}

public sealed class ConceptMethodInfo
{
    public string Name { get; set; } = "";
    public string ReturnTypeName { get; set; } = "";
    public IReadOnlyList<string> AttributeNames { get; set; } = Array.Empty<string>();
}

public sealed class ConceptReferenceInfo
{
    public string Name { get; set; } = "";
    public string TargetConcept { get; set; } = "";
    public bool IsContainment { get; set; }
}

public sealed class ConceptAttributeInfo
{
    public string Name { get; set; } = "";
    public IReadOnlyDictionary<string, string?> Properties { get; set; } = new Dictionary<string, string?>();
}
