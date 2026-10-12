using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FrenchExDev.Net.Vos.VosFile;

public sealed class VosFileReader : IVosFileReader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public async Task<Res.Result<VosConfig>> ReadAsync(string configPath, CancellationToken ct = default)
    {
        var layers = await ReadLayersAsync(configPath, ct);
        if (layers.IsFailure)
            return Res.Result<VosConfig>.Failure(
                new System.ComponentModel.DataAnnotations.ValidationResult($"Failed to load config: {configPath}"));
        return Res.Result<VosConfig>.Success(layers.Value!.Resolved);
    }

    public async Task<Res.Result<VosConfigLayers>> ReadLayersAsync(string configPath, CancellationToken ct = default)
    {
        if (!File.Exists(configPath))
            return Res.Result<VosConfigLayers>.Failure(
                new System.ComponentModel.DataAnnotations.ValidationResult($"Config not found: {configPath}"));

        var baseYaml = await File.ReadAllTextAsync(configPath, ct);
        var baseConfig = Deserializer.Deserialize<VosConfig>(baseYaml) ?? new VosConfig();

        // Resolve env vars in string values
        baseConfig = EnvVarResolver.Resolve(baseConfig);

        // Check for local override
        var dir = Path.GetDirectoryName(configPath) ?? ".";
        var localPath = Path.Combine(dir, "local", "config-vos-local.yaml");
        VosConfig? localConfig = null;

        if (File.Exists(localPath))
        {
            var localYaml = await File.ReadAllTextAsync(localPath, ct);
            localConfig = Deserializer.Deserialize<VosConfig>(localYaml);
            if (localConfig is not null)
                localConfig = EnvVarResolver.Resolve(localConfig);
        }

        // Deep merge: base + local = resolved
        var resolved = localConfig is not null
            ? DeepMerge(baseConfig, localConfig)
            : baseConfig;

        return Res.Result<VosConfigLayers>.Success(new VosConfigLayers(baseConfig, localConfig, resolved));
    }

    private static VosConfig DeepMerge(VosConfig baseConfig, VosConfig localConfig)
    {
        // Simple merge: local overrides base for non-null properties
        // Machine types: merge dictionaries
        var mergedTypes = new Dictionary<string, VosMachineType>(baseConfig.MachineTypes);
        foreach (var (key, value) in localConfig.MachineTypes)
            mergedTypes[key] = value;

        var mergedMachines = new Dictionary<string, VosMachine>(baseConfig.Machines);
        foreach (var (key, value) in localConfig.Machines)
            mergedMachines[key] = value;

        return new VosConfig
        {
            Backend = localConfig.Backend ?? baseConfig.Backend,
            Format = localConfig.Format ?? baseConfig.Format,
            MachineTypes = mergedTypes,
            Machines = mergedMachines
        };
    }
}
