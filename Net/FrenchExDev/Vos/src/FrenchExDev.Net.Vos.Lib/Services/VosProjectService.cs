using FrenchExDev.Net.Injectable.Attributes;
using FrenchExDev.Net.Vos.Bundle;
using Microsoft.Extensions.Logging;

namespace FrenchExDev.Net.Vos.Lib.Services;

[Injectable(Scope = Scope.Transient, As = new[] { typeof(IVosProjectService) })]
public sealed class VosProjectService(
    IVosFileReader fileReader,
    IVosFileWriter fileWriter,
    IVosEventEmitter emitter,
    ILogger<VosProjectService> logger) : IVosProjectService
{
    public async Task<Res.Result<VosConfig>> InitAsync(string outputDir, CancellationToken ct = default)
    {
        emitter.Emit(new ProjectInitializing(outputDir));

        var config = new VosConfig
        {
            MachineTypes = new()
            {
                ["default"] = new VosMachineType
                {
                    Box = "ubuntu/jammy64",
                    Provider = new VosProviderConfig { Memory = 2048, Cpus = 2 }
                }
            },
            Machines = new()
            {
                ["default"] = new VosMachine
                {
                    MachineTypeName = "default",
                    Instances = [new VosInstance { Name = "default-01" }]
                }
            }
        };

        var configPath = Path.Combine(outputDir, "config-vos.yaml");
        await fileWriter.WriteAsync(config, configPath, ct);
        emitter.Emit(new FileCreated(configPath));

        logger.LogInformation("Initialized Vos project at {OutputDir}", outputDir);
        emitter.Emit(new ProjectInitialized(outputDir));
        return Res.Result<VosConfig>.Success(config);
    }

    public async Task<Res.Result<IReadOnlyList<(string MachineName, ResolvedInstance Instance)>>> ShowConfigAsync(string configPath, CancellationToken ct = default)
    {
        emitter.Emit(new ConfigShowStarted(configPath));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure)
            return Res.Result<IReadOnlyList<(string, ResolvedInstance)>>.Failure(loadResult.ValidationResult!);

        var all = VosConfigMerger.ResolveAll(loadResult.Value!);
        var result = all.Select(x => (x.MachineName, x.Instance)).ToList();
        emitter.Emit(new ConfigShowCompleted(result.Count));
        return Res.Result<IReadOnlyList<(string MachineName, ResolvedInstance Instance)>>.Success(result);
    }

    public async Task<Res.Result<IReadOnlyList<(string MachineName, ResolvedInstance Instance)>>> ValidateAsync(string configPath, CancellationToken ct = default)
    {
        emitter.Emit(new ValidationStarted(configPath));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure)
            return Res.Result<IReadOnlyList<(string, ResolvedInstance)>>.Failure(loadResult.ValidationResult!);

        var errors = VosConfigValidator.Validate(loadResult.Value!);
        foreach (var err in errors)
            emitter.Emit(new ValidationError(err));

        if (errors.Count > 0)
        {
            emitter.Emit(new ValidationCompleted(errors.Count, 0));
            return Res.Result<IReadOnlyList<(string, ResolvedInstance)>>.Failure(
                new System.ComponentModel.DataAnnotations.ValidationResult($"Configuration has {errors.Count} error(s)"));
        }

        var all = VosConfigMerger.ResolveAll(loadResult.Value!);
        var result = all.Select(x => (x.MachineName, x.Instance)).ToList();
        emitter.Emit(new ValidationCompleted(0, result.Count));
        return Res.Result<IReadOnlyList<(string MachineName, ResolvedInstance Instance)>>.Success(result);
    }

    public async Task<Res.Result<IReadOnlyList<(string MachineName, ResolvedInstance Instance)>>> ResolveAsync(string configPath, string? instanceName = null, bool all = true, CancellationToken ct = default)
    {
        emitter.Emit(new ResolveStarted(configPath, instanceName));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure)
            return Res.Result<IReadOnlyList<(string, ResolvedInstance)>>.Failure(loadResult.ValidationResult!);

        var resolved = VosConfigMerger.ResolveAll(loadResult.Value!);
        var targets = (all || instanceName is null)
            ? resolved
            : resolved.Where(x => x.Instance.Name.Equals(instanceName, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var (mn, inst) in targets)
            emitter.Emit(new InstanceResolved(mn, inst.Name));

        var result = targets.Select(x => (x.MachineName, x.Instance)).ToList();
        emitter.Emit(new ResolveCompleted(result.Count));
        return Res.Result<IReadOnlyList<(string MachineName, ResolvedInstance Instance)>>.Success(result);
    }

    public string GetVersion() => "vos 0.3.0";

    public Task<Res.Result> CreateProvisioningScriptAsync(string configPath, string key, string? version = null, string? extension = null, string? templatePath = null, CancellationToken ct = default)
    {
        var ext = extension ?? "sh";
        var dir = Path.GetDirectoryName(configPath) ?? ".";
        var provPath = version is not null
            ? Path.Combine(dir, "provisioning", version, $"{key}.{ext}")
            : Path.Combine(dir, "provisioning", $"{key}.{ext}");

        emitter.Emit(new ProvisioningScriptCreating(key, provPath));

        var provDir = Path.GetDirectoryName(provPath)!;
        Directory.CreateDirectory(provDir);

        var content = templatePath is not null && File.Exists(templatePath)
            ? File.ReadAllText(templatePath)
            : $"#!/bin/sh\nset -eux\n\n# Provisioning: {key}\n";

        File.WriteAllText(provPath, content);
        emitter.Emit(new ProvisioningScriptCreated(key, provPath));
        return Task.FromResult(Res.Result.Success());
    }

    public async Task<Res.Result<IReadOnlyList<string>>> ValidateProvisioningScriptsAsync(string configPath, CancellationToken ct = default)
    {
        emitter.Emit(new ProvisioningValidating());
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure)
            return Res.Result<IReadOnlyList<string>>.Failure(loadResult.ValidationResult!);

        var dir = Path.GetDirectoryName(configPath) ?? ".";
        var missing = new List<string>();

        foreach (var (_, mt) in loadResult.Value!.MachineTypes)
        {
            if (!mt.IsEnabled) continue;
            var provPath = mt.ProvisioningPath ?? "provisioning";
            foreach (var step in mt.Provisioning)
            {
                if (!step.Enabled) continue;
                var ext = step.Extension ?? "sh";
                var path = step.Version is not null
                    ? Path.Combine(dir, provPath, step.Version, $"{step.Key}.{ext}")
                    : Path.Combine(dir, provPath, $"{step.Key}.{ext}");

                if (!File.Exists(path))
                {
                    missing.Add(path);
                    emitter.Emit(new ProvisioningScriptMissing(step.Key, path));
                }
            }
        }

        emitter.Emit(new ProvisioningValidated(missing.Count));
        return Res.Result<IReadOnlyList<string>>.Success(missing);
    }
}
