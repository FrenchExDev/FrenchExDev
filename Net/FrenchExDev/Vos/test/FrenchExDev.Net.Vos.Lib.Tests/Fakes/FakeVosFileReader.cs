using FrenchExDev.Net.Vos.VosFile;

namespace FrenchExDev.Net.Vos.Lib.Tests.Fakes;

public sealed class FakeVosFileReader : IVosFileReader
{
    private readonly Dictionary<string, VosConfig> _configs = new();

    public void SetConfig(string path, VosConfig config) => _configs[path] = config;

    public Task<Res.Result<VosConfig>> ReadAsync(string configPath, CancellationToken ct = default)
    {
        if (_configs.TryGetValue(configPath, out var config))
            return Task.FromResult(Res.Result<VosConfig>.Success(config));
        return Task.FromResult(Res.Result<VosConfig>.Failure(
            new System.ComponentModel.DataAnnotations.ValidationResult($"Config not found: {configPath}")));
    }

    public Task<Res.Result<VosConfigLayers>> ReadLayersAsync(string configPath, CancellationToken ct = default)
    {
        if (_configs.TryGetValue(configPath, out var config))
            return Task.FromResult(Res.Result<VosConfigLayers>.Success(new VosConfigLayers(config, null, config)));
        return Task.FromResult(Res.Result<VosConfigLayers>.Failure(
            new System.ComponentModel.DataAnnotations.ValidationResult($"Config not found: {configPath}")));
    }
}
