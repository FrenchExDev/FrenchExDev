using FrenchExDev.Net.BinaryWrapper;

namespace FrenchExDev.Net.Vagrant;

public abstract record VagrantEvent;

public sealed record VagrantMachineOutput(string MachineName, string Message) : VagrantEvent;

public sealed record VagrantMachineError(string MachineName, string Message) : VagrantEvent;

public sealed record VagrantProvisionerOutput(string MachineName, string Message) : VagrantEvent;

public sealed record VagrantActionCompleted(string MachineName, bool Success) : VagrantEvent;

public sealed record VagrantMachineReadableEvent(
    long Timestamp, string Target, string EventType, string[] Data) : VagrantEvent;

public sealed record VagrantOutputLine(string Text, OutputSource Source) : VagrantEvent;
