namespace FrenchExDev.Net.BinaryWrapper.Design;

/// <summary>A runtime failure must abort collection instead of producing an incomplete command tree.</summary>
public sealed class ContainerRuntimeException(string message) : InvalidOperationException(message);
