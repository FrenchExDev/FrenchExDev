using FrenchExDev.Net.BinaryWrapper;

namespace FrenchExDev.Net.Packer;

public abstract record PackerEvent;

public sealed record PackerBuildStarted(string BuilderType, string BuildName) : PackerEvent;

public sealed record PackerBuildOutput(string BuildName, string Message) : PackerEvent;

public sealed record PackerBuildError(string BuildName, string Message) : PackerEvent;

public sealed record PackerBuildFinished(string BuildName, bool Success, string? ArtifactId) : PackerEvent;

public sealed record PackerProvisionerOutput(string BuildName, string ProvisionerType, string Message) : PackerEvent;

public sealed record PackerMachineReadableEvent(
    long Timestamp, string Target, string EventType, string[] Data) : PackerEvent;

public sealed record PackerOutputLine(string Text, OutputSource Source) : PackerEvent;
