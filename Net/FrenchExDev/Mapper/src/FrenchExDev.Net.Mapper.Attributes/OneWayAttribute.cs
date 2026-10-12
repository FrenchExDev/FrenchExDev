namespace FrenchExDev.Net.Mapper.Attributes;

/// <summary>
/// Indicates that the mapping is one-way only, suppressing the generation of a reverse mapper.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class OneWayAttribute : Attribute;
