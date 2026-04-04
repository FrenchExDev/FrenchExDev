using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FrenchExDev.Net.Vos.VosFile;

public sealed class VosFileWriter : IVosFileWriter
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public async Task WriteAsync(VosConfig config, string path, CancellationToken ct = default)
    {
        var yaml = Serializer.Serialize(config);
        await File.WriteAllTextAsync(path, yaml, ct);
    }

    public async Task WriteLocalAsync(VosConfig localOverrides, string configPath, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(configPath) ?? ".";
        var localDir = Path.Combine(dir, "local");

        if (!Directory.Exists(localDir))
        {
            Directory.CreateDirectory(localDir);
            // Add .gitignore
            var gitignorePath = Path.Combine(localDir, ".gitignore");
            if (!File.Exists(gitignorePath))
                await File.WriteAllTextAsync(gitignorePath, "*\n", ct);
        }

        var localPath = Path.Combine(localDir, "config-vos-local.yaml");
        var yaml = Serializer.Serialize(localOverrides);
        await File.WriteAllTextAsync(localPath, yaml, ct);
    }
}
