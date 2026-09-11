using FrenchExDev.Net.Packer.Bundle.Hcl2;

namespace FrenchExDev.Net.Packer.Bundle;

/// <summary>
/// Renders a <see cref="PackerBundle"/> to disk as <c>.pkr.hcl</c> files + companion files.
/// </summary>
public interface IPackerBundleWriter
{
    Task WriteAsync(PackerBundle bundle, string outputDirectory, CancellationToken ct = default);
}

/// <summary>
/// Default implementation of <see cref="IPackerBundleWriter"/>.
/// Emits multi-file HCL2 templates: packer.pkr.hcl, variables.pkr.hcl, locals.pkr.hcl,
/// sources.pkr.hcl, build.pkr.hcl, plus all companion BundleFiles.
/// </summary>
public sealed class PackerBundleWriter : IPackerBundleWriter
{
    public async Task WriteAsync(PackerBundle bundle, string outputDirectory, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDirectory);

        // 1. Emit HCL2 files
        await WritePackerConfig(bundle, outputDirectory, ct);
        await WriteVariables(bundle, outputDirectory, ct);
        await WriteLocals(bundle, outputDirectory, ct);
        await WriteSources(bundle, outputDirectory, ct);
        await WriteBuild(bundle, outputDirectory, ct);

        // 2. Materialize typed companion models into BundleFiles
        MaterializeCompanionModels(bundle);

        // 3. Write all companion BundleFiles to disk
        foreach (var file in bundle.Files.Values)
        {
            var filePath = Path.Combine(outputDirectory, file.RelativePath);
            var fileDir = Path.GetDirectoryName(filePath);
            if (fileDir is not null)
                Directory.CreateDirectory(fileDir);
            await WriteTextAsync(filePath, file.Content, ct);
        }
    }

    private static async Task WritePackerConfig(PackerBundle bundle, string dir, CancellationToken ct)
    {
        var config = bundle.Config.Build();
        if (config.RequiredVersion is null && config.RequiredPlugins.Count == 0)
            return;

        var path = Path.Combine(dir, "packer.pkr.hcl");
        using var sw = new StreamWriter(path);
        using var writer = new HclWriter(sw);
        config.WriteTo(writer);
    }

    private static async Task WriteVariables(PackerBundle bundle, string dir, CancellationToken ct)
    {
        if (bundle.Variables.Count == 0) return;

        var path = Path.Combine(dir, "variables.pkr.hcl");
        using var sw = new StreamWriter(path);
        using var writer = new HclWriter(sw);

        for (var i = 0; i < bundle.Variables.Count; i++)
        {
            if (i > 0) writer.BlankLine();
            bundle.Variables[i].WriteTo(writer);
        }
    }

    private static async Task WriteLocals(PackerBundle bundle, string dir, CancellationToken ct)
    {
        if (bundle.Locals.Count == 0) return;

        var path = Path.Combine(dir, "locals.pkr.hcl");
        using var sw = new StreamWriter(path);
        using var writer = new HclWriter(sw);

        using var block = writer.Block("locals");
        foreach (var local in bundle.Locals)
            writer.Expression(local.Name, local.Expression);
    }

    private static async Task WriteSources(PackerBundle bundle, string dir, CancellationToken ct)
    {
        if (bundle.Sources.Count == 0) return;

        var path = Path.Combine(dir, "sources.pkr.hcl");
        using var sw = new StreamWriter(path);
        using var writer = new HclWriter(sw);

        for (var i = 0; i < bundle.Sources.Count; i++)
        {
            if (i > 0) writer.BlankLine();
            bundle.Sources[i].WriteTo(writer);
        }
    }

    private static async Task WriteBuild(PackerBundle bundle, string dir, CancellationToken ct)
    {
        var build = bundle.Build.Build();
        if (build.Sources.Count == 0 && build.Provisioners.Count == 0 && build.PostProcessors.Count == 0)
            return;

        var path = Path.Combine(dir, "build.pkr.hcl");
        using var sw = new StreamWriter(path);
        using var writer = new HclWriter(sw);
        build.WriteTo(writer);
    }

    private static void MaterializeCompanionModels(PackerBundle bundle)
    {
        // EnvTemplate → .env.template
        if (bundle.EnvTemplate.Variables.Count > 0)
        {
            var content = bundle.EnvTemplate.Render();
            if (!bundle.Files.ContainsKey(".env.template"))
                bundle.AddFile("", ".env.template", "", content);
        }

        // EnvValues → .env
        if (bundle.EnvValues.Values.Count > 0)
        {
            var content = bundle.EnvValues.Render();
            if (!bundle.Files.ContainsKey(".env"))
                bundle.AddFile("", ".env", "", content);
        }
    }

    private static async Task WriteTextAsync(string path, string content, CancellationToken ct)
    {
#if NETSTANDARD2_0
        File.WriteAllText(path, content);
        await Task.CompletedTask;
#else
        await File.WriteAllTextAsync(path, content, ct);
#endif
    }
}
