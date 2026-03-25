using System.CommandLine;
using FrenchExDev.Net.Packer.Alpine;
using FrenchExDev.Net.Packer.Bundle;

var versionOption = new Option<string>("--version") { Description = "Alpine Linux version", DefaultValueFactory = _ => "3.21" };
var cpusOption = new Option<int>("--cpus") { Description = "Number of CPUs", DefaultValueFactory = _ => 4 };
var memoryOption = new Option<int>("--memory") { Description = "Memory in MB", DefaultValueFactory = _ => 256 };
var outputOption = new Option<string>("--output") { Description = "Output directory", DefaultValueFactory = _ => "output-vagrant" };

var rootCommand = new RootCommand("packer-alpine - Generate Alpine Linux Packer HCL2 bundles");

// ── build ───────────────────────────────────────────────────────────
var buildCmd = new Command("build", "Generate and optionally run a Packer build");
buildCmd.Options.Add(versionOption);
buildCmd.Options.Add(cpusOption);
buildCmd.Options.Add(memoryOption);
buildCmd.Options.Add(outputOption);
buildCmd.SetAction(async (pr, ct) =>
{
    var config = new AlpinePackerConfig
    {
        AlpineVersion = pr.GetValue(versionOption)!,
        Cpus = pr.GetValue(cpusOption),
        Memory = pr.GetValue(memoryOption),
        OutputDirectory = pr.GetValue(outputOption)!
    };

    var bundle = new PackerBundle();
    bundle.Apply(new AlpineBaseContributor(config));

    var outputDir = config.OutputDirectory;
    Console.WriteLine($"Writing Alpine Packer bundle to: {outputDir}");

    await new PackerBundleWriter().WriteAsync(bundle, outputDir, ct);

    Console.WriteLine($"Done: {bundle.Files.Count + 5} files written.");
    Console.WriteLine();
    Console.WriteLine("Generated files:");
    Console.WriteLine("  packer.pkr.hcl          — packer { required_plugins { } }");
    Console.WriteLine("  variables.pkr.hcl       — variable blocks");
    Console.WriteLine("  locals.pkr.hcl          — local values");
    Console.WriteLine("  sources.pkr.hcl         — source \"virtualbox-iso\" \"alpine\" { }");
    Console.WriteLine("  build.pkr.hcl           — build { provisioner, post-processor }");
    Console.WriteLine($"  scripts/*.sh            — {bundle.Scripts.Count()} provisioning scripts");
    Console.WriteLine($"  http/*                  — {bundle.HttpFiles.Count()} HTTP-served files");
    Console.WriteLine();
    Console.WriteLine("Next steps:");
    Console.WriteLine($"  cd {outputDir}");
    Console.WriteLine("  packer init .");
    Console.WriteLine("  packer build .");
});
rootCommand.Subcommands.Add(buildCmd);

// ── init ────────────────────────────────────────────────────────────
var initCmd = new Command("init", "Generate Packer bundle without building");
initCmd.Options.Add(versionOption);
initCmd.Options.Add(cpusOption);
initCmd.Options.Add(memoryOption);
initCmd.Options.Add(outputOption);
initCmd.SetAction(async (pr, ct) =>
{
    var config = new AlpinePackerConfig
    {
        AlpineVersion = pr.GetValue(versionOption)!,
        Cpus = pr.GetValue(cpusOption),
        Memory = pr.GetValue(memoryOption),
        OutputDirectory = pr.GetValue(outputOption)!
    };

    var bundle = new PackerBundle();
    bundle.Apply(new AlpineBaseContributor(config));

    await new PackerBundleWriter().WriteAsync(bundle, config.OutputDirectory, ct);
    Console.WriteLine($"Bundle generated at: {config.OutputDirectory}");
});
rootCommand.Subcommands.Add(initCmd);

// ── version ─────────────────────────────────────────────────────────
var versionCmd = new Command("version", "Show version");
versionCmd.SetAction((_) => Console.WriteLine("packer-alpine 0.1.0"));
rootCommand.Subcommands.Add(versionCmd);

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();
