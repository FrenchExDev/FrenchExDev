using System.Diagnostics.CodeAnalysis;

namespace FrenchExDev.Net.Vos.Cli.Exceptions;

public abstract class VosCliException(string message) : Exception(message);

public sealed class ConfigNotFoundException(string path)
    : VosCliException($"Config not found: '{path}'. Run 'vos init' first.");

public sealed class MachineTypeNotFoundException(string name)
    : VosCliException($"Machine type '{name}' not found.");

public sealed class MachineNotFoundException(string name)
    : VosCliException($"Machine '{name}' not found.");

public sealed class InstanceNotFoundException(string machine, string instance)
    : VosCliException($"Instance '{instance}' not found in machine '{machine}'.");

public sealed class RequiredArgumentMissingException(string argName)
    : VosCliException($"Argument '{argName}' is required.");

public sealed class RequiredOptionMissingException(string optionName)
    : VosCliException($"Option '{optionName}' is required.");

public sealed class ConfigValidationException(IReadOnlyList<string> errors)
    : VosCliException($"Config has {errors.Count} error(s):\n{string.Join("\n", errors.Select(e => $"  - {e}"))}")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

public static class VosCliThrow
{
    [DoesNotReturn] public static void ConfigNotFound(string path) => throw new ConfigNotFoundException(path);
    [DoesNotReturn] public static void MachineTypeNotFound(string name) => throw new MachineTypeNotFoundException(name);
    [DoesNotReturn] public static void MachineNotFound(string name) => throw new MachineNotFoundException(name);
    [DoesNotReturn] public static void InstanceNotFound(string machine, string instance) => throw new InstanceNotFoundException(machine, instance);
    [DoesNotReturn] public static void RequiredArgumentMissing(string argName) => throw new RequiredArgumentMissingException(argName);
    [DoesNotReturn] public static void RequiredOptionMissing(string optionName) => throw new RequiredOptionMissingException(optionName);
    [DoesNotReturn] public static void ConfigInvalid(IReadOnlyList<string> errors) => throw new ConfigValidationException(errors);
}
