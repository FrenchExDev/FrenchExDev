#if NET10_0_OR_GREATER
using System.Diagnostics;
using System.Text.Json;
using FrenchExDev.Net.Packer.Bundle.Hcl2;

namespace FrenchExDev.Net.Packer.Bundle;

/// <summary>
/// Reads <c>.pkr.hcl</c> files into C# models by shelling out to <c>hcl2json</c>
/// (official HashiCorp Go parser) and deserializing the JSON output.
/// </summary>
public interface IHclReader
{
    /// <summary>Reads a single .pkr.hcl file and returns the parsed JSON as a <see cref="JsonDocument"/>.</summary>
    Task<JsonDocument> ReadAsync(string hclFilePath, CancellationToken ct = default);
}

/// <summary>
/// Default <see cref="IHclReader"/> implementation using the <c>hcl2json</c> binary.
/// </summary>
public sealed class HclReader : IHclReader
{
    private readonly string _hcl2JsonPath;

    /// <param name="hcl2JsonPath">Path to the hcl2json binary. Defaults to "hcl2json" (on PATH).</param>
    public HclReader(string hcl2JsonPath = "hcl2json")
    {
        _hcl2JsonPath = hcl2JsonPath;
    }

    public async Task<JsonDocument> ReadAsync(string hclFilePath, CancellationToken ct = default)
    {
        if (!File.Exists(hclFilePath))
            throw new FileNotFoundException($"HCL file not found: {hclFilePath}", hclFilePath);

        var psi = new ProcessStartInfo
        {
            FileName = _hcl2JsonPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start hcl2json at '{_hcl2JsonPath}'");

        // Pipe the HCL file content to stdin
        var hclContent = await File.ReadAllTextAsync(hclFilePath, ct);
        await process.StandardInput.WriteAsync(hclContent);
        process.StandardInput.Close();

        var jsonOutput = await process.StandardOutput.ReadToEndAsync(ct);
        var errorOutput = await process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"hcl2json failed (exit {process.ExitCode}) for '{hclFilePath}': {errorOutput}");

        return JsonDocument.Parse(jsonOutput);
    }
}

/// <summary>
/// Reads an entire Packer project directory into a <see cref="PackerBundle"/>.
/// </summary>
public interface IPackerBundleReader
{
    Task<PackerBundle> ReadAsync(string projectDirectory, CancellationToken ct = default);
}

/// <summary>
/// Default <see cref="IPackerBundleReader"/> — reads .pkr.hcl files via <see cref="HclReader"/>
/// and companion files directly from disk.
/// </summary>
public sealed class PackerBundleReader : IPackerBundleReader
{
    private readonly IHclReader _hclReader;

    public PackerBundleReader(IHclReader hclReader)
    {
        _hclReader = hclReader;
    }

    public async Task<PackerBundle> ReadAsync(string projectDirectory, CancellationToken ct = default)
    {
        var bundle = new PackerBundle();

        // Read all .pkr.hcl files
        var hclFiles = Directory.GetFiles(projectDirectory, "*.pkr.hcl");
        foreach (var hclFile in hclFiles)
        {
            using var doc = await _hclReader.ReadAsync(hclFile, ct);
            PopulateFromJson(bundle, doc.RootElement);
        }

        // Read companion files (scripts, http, vagrant, etc.)
        ReadCompanionFiles(bundle, projectDirectory, "scripts");
        ReadCompanionFiles(bundle, projectDirectory, "http");
        ReadCompanionFiles(bundle, projectDirectory, "vagrant");

        // Read .env files
        var envTemplatePath = Path.Combine(projectDirectory, ".env.template");
        if (File.Exists(envTemplatePath))
        {
            var content = await File.ReadAllTextAsync(envTemplatePath, ct);
            // Parse .env.template into EnvTemplate model
            foreach (var line in content.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (trimmed.StartsWith("#"))
                {
                    // Next non-comment line gets this as description
                    continue;
                }
                var eq = trimmed.IndexOf('=');
                if (eq > 0)
                {
                    bundle.EnvTemplate.Variables.Add(new EnvVariable
                    {
                        Key = trimmed.Substring(0, eq).Trim(),
                        DefaultValue = trimmed.Substring(eq + 1).Trim()
                    });
                }
            }
        }

        var envPath = Path.Combine(projectDirectory, ".env");
        if (File.Exists(envPath))
            bundle.EnvValues = EnvValues.Parse(await File.ReadAllTextAsync(envPath, ct));

        return bundle;
    }

    private static void PopulateFromJson(PackerBundle bundle, JsonElement root)
    {
        ParsePackerBlock(bundle, root);
        ParseVariables(bundle, root);
        ParseLocals(bundle, root);
        ParseSources(bundle, root);
        ParseBuild(bundle, root);
    }

    private static void ParsePackerBlock(PackerBundle bundle, JsonElement root)
    {
        if (!root.TryGetProperty("packer", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return;

        foreach (var packer in arr.EnumerateArray())
        {
            if (packer.TryGetProperty("required_version", out var rv))
                bundle.Config.WithRequiredVersion(rv.GetString()!);

            if (!packer.TryGetProperty("required_plugins", out var rp) || rp.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var group in rp.EnumerateArray())
                foreach (var plugin in group.EnumerateObject())
                {
                    var ver = plugin.Value.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
                    var src = plugin.Value.TryGetProperty("source", out var s) ? s.GetString() ?? "" : "";
                    bundle.Config.WithRequiredPlugin(plugin.Name, ver, src);
                }
        }
    }

    private static void ParseVariables(PackerBundle bundle, JsonElement root)
    {
        if (!root.TryGetProperty("variable", out var vars) || vars.ValueKind != JsonValueKind.Object)
            return;

        foreach (var entry in vars.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.Array) continue;
            foreach (var _ in entry.Value.EnumerateArray())
                bundle.Variables.Add(new PackerVariable { Name = entry.Name });
        }
    }

    private static void ParseLocals(PackerBundle bundle, JsonElement root)
    {
        if (!root.TryGetProperty("locals", out var localsArr) || localsArr.ValueKind != JsonValueKind.Array)
            return;

        foreach (var localsBlock in localsArr.EnumerateArray())
            foreach (var entry in localsBlock.EnumerateObject())
                bundle.Locals.Add(new PackerLocal { Name = entry.Name, Expression = entry.Value.ToString() });
    }

    private static void ParseSources(PackerBundle bundle, JsonElement root)
    {
        if (!root.TryGetProperty("source", out var sources) || sources.ValueKind != JsonValueKind.Object)
            return;

        foreach (var sourceType in sources.EnumerateObject())
            foreach (var sourceName in sourceType.Value.EnumerateObject())
            {
                if (sourceName.Value.ValueKind != JsonValueKind.Array) continue;
                foreach (var def in sourceName.Value.EnumerateArray())
                {
                    var ps = new PackerSource { Type = sourceType.Name, Name = sourceName.Name };
                    foreach (var prop in def.EnumerateObject())
                        ps.Arguments[prop.Name] = JsonElementToObject(prop.Value);
                    bundle.Sources.Add(ps);
                }
            }
    }

    private static void ParseBuild(PackerBundle bundle, JsonElement root)
    {
        if (!root.TryGetProperty("build", out var builds) || builds.ValueKind != JsonValueKind.Array)
            return;

        foreach (var build in builds.EnumerateArray())
        {
            if (build.TryGetProperty("name", out var name))
                bundle.Build.WithName(name.GetString()!);

            if (build.TryGetProperty("sources", out var srcs) && srcs.ValueKind == JsonValueKind.Array)
                foreach (var src in srcs.EnumerateArray())
                    bundle.Build.WithSource(src.GetString()!);

            // Parse provisioners: { "provisioner": { "shell": [...], "file": [...] } }
            if (build.TryGetProperty("provisioner", out var provs) && provs.ValueKind == JsonValueKind.Object)
                foreach (var provType in provs.EnumerateObject())
                    if (provType.Value.ValueKind == JsonValueKind.Array)
                        foreach (var provDef in provType.Value.EnumerateArray())
                        {
                            var prov = new PackerProvisioner { Type = provType.Name };
                            foreach (var prop in provDef.EnumerateObject())
                                prov.Arguments[prop.Name] = JsonElementToObject(prop.Value);
                            bundle.Build.WithProvisioner(prov);
                        }

            // Parse post-processors: { "post-processor": { "vagrant": [...] } }
            if (build.TryGetProperty("post-processor", out var pps) && pps.ValueKind == JsonValueKind.Object)
                foreach (var ppType in pps.EnumerateObject())
                    if (ppType.Value.ValueKind == JsonValueKind.Array)
                        foreach (var ppDef in ppType.Value.EnumerateArray())
                        {
                            var pp = new PackerPostProcessor { Type = ppType.Name };
                            foreach (var prop in ppDef.EnumerateObject())
                                pp.Arguments[prop.Name] = JsonElementToObject(prop.Value);
                            bundle.Build.WithPostProcessor(pp);
                        }
        }
    }

    private static object? JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var i) ? i : (object)element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private static void ReadCompanionFiles(PackerBundle bundle, string projectDir, string subDir)
    {
        var dir = Path.Combine(projectDir, subDir);
        if (!Directory.Exists(dir)) return;

        foreach (var filePath in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(projectDir, filePath).Replace('\\', '/');
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var ext = Path.GetExtension(filePath);
            var content = File.ReadAllText(filePath);

            bundle.Files[relativePath] = new BundleFile
            {
                Name = fileName,
                Extension = ext,
                Directory = subDir,
                Content = content
            };
        }
    }
}
#endif
