using FrenchExDev.Net.Packer.Alpine;
using FrenchExDev.Net.Packer.Bundle;

var config = new AlpinePackerConfig();

// Parse simple CLI args
foreach (var arg in args)
{
    if (arg.StartsWith("--version="))
        config.AlpineVersion = arg.Substring("--version=".Length);
    else if (arg.StartsWith("--cpus="))
        config.Cpus = int.Parse(arg.Substring("--cpus=".Length));
    else if (arg.StartsWith("--memory="))
        config.Memory = int.Parse(arg.Substring("--memory=".Length));
    else if (arg.StartsWith("--output="))
        config.OutputDirectory = arg.Substring("--output=".Length);
}

// Build the bundle
var bundle = new PackerBundle();
bundle.Apply(new AlpineBaseContributor(config));

// Write to disk
var outputDir = config.OutputDirectory;
Console.WriteLine($"Writing Alpine Packer bundle to: {outputDir}");

var writer = new PackerBundleWriter();
await writer.WriteAsync(bundle, outputDir);

var fileCount = bundle.Files.Count + 5; // 5 .pkr.hcl files
Console.WriteLine($"Done: {fileCount} files written.");
Console.WriteLine("Run: packer init . && packer build .");

return 0;
