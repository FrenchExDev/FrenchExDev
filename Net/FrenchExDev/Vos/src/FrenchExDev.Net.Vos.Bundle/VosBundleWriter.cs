using System.Reflection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FrenchExDev.Net.Vos.Bundle;

/// <summary>
/// Materializes a VosBundle to disk:
/// - config-vos.yaml
/// - local/config-vos-local.yaml (if LocalOverrides set)
/// - local/.gitignore
/// - Vagrantfile (embedded resource)
/// - provisioning scripts
/// - shared files
/// </summary>
public sealed class VosBundleWriter
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public async Task WriteAsync(VosBundle bundle, string outputDir, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDir);

        // config-vos.yaml
        var configPath = Path.Combine(outputDir, "config-vos.yaml");
        var yaml = Serializer.Serialize(bundle.Config);
        await File.WriteAllTextAsync(configPath, yaml, ct);

        // local override
        if (bundle.LocalOverrides is not null)
        {
            var localDir = Path.Combine(outputDir, "local");
            Directory.CreateDirectory(localDir);
            await File.WriteAllTextAsync(
                Path.Combine(localDir, ".gitignore"), "*\n", ct);
            await File.WriteAllTextAsync(
                Path.Combine(localDir, "config-vos-local.yaml"),
                Serializer.Serialize(bundle.LocalOverrides), ct);
        }

        // Vagrantfile (embedded resource)
        await WriteEmbeddedResourceAsync("FrenchExDev.Net.Vos.Bundle.Resources.Vagrantfile",
            Path.Combine(outputDir, "Vagrantfile"), ct);

        // All bundle files (provisioning scripts, shared files, etc.)
        foreach (var (_, file) in bundle.Files)
        {
            var filePath = Path.Combine(outputDir, file.Directory, file.FileName);
            var fileDir = Path.GetDirectoryName(filePath)!;
            Directory.CreateDirectory(fileDir);
            await File.WriteAllTextAsync(filePath, file.Content, ct);
        }
    }

    private static async Task WriteEmbeddedResourceAsync(string resourceName, string outputPath, CancellationToken ct)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return; // resource not found — skip silently

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(ct);
        await File.WriteAllTextAsync(outputPath, content, ct);
    }
}
