using FrenchExDev.Net.Vos.Config;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FrenchExDev.Net.Vos.Infra.FileSystem;

public interface IVosConfigSerializer
{
    Task<VosConfig> DeserializeAsync(string path, CancellationToken ct = default);
    Task SerializeAsync(VosConfig config, string path, CancellationToken ct = default);
}

public sealed class VosConfigSerializer : IVosConfigSerializer
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public async Task<VosConfig> DeserializeAsync(string path, CancellationToken ct = default)
    {
        var yaml = await File.ReadAllTextAsync(path, ct);
        return Deserializer.Deserialize<VosConfig>(yaml) ?? new VosConfig();
    }

    public async Task SerializeAsync(VosConfig config, string path, CancellationToken ct = default)
    {
        var yaml = Serializer.Serialize(config);
        await File.WriteAllTextAsync(path, yaml, ct);
    }
}
