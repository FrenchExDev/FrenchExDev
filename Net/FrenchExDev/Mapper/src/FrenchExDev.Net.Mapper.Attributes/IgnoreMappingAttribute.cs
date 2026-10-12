namespace FrenchExDev.Net.Mapper.Attributes;

/// <summary>
/// Indicates that the decorated property should be excluded from automatic mapping.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class IgnoreMappingAttribute : Attribute;
