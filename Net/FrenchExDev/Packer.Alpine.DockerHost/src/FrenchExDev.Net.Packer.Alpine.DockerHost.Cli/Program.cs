using FrenchExDev.Net.Packer.Alpine;
using FrenchExDev.Net.Packer.Alpine.DockerHost;
using FrenchExDev.Net.Packer.Bundle;

var config = new AlpinePackerConfig();
config.Description = "Alpine Docker Host";

foreach (var arg in args)
{
    if (arg.StartsWith("--version="))
        config.AlpineVersion = arg.Substring("--version=".Length);
    else if (arg.StartsWith("--output="))
        config.OutputDirectory = arg.Substring("--output=".Length);
}

var bundle = new PackerBundle();
bundle.Apply(
    new AlpineBaseContributor(config),
    new DockerContributor());

var outputDir = config.OutputDirectory;
Console.WriteLine($"Writing Alpine Docker Host Packer bundle to: {outputDir}");

var writer = new PackerBundleWriter();
await writer.WriteAsync(bundle, outputDir);

Console.WriteLine($"Done: {bundle.Files.Count + 5} files written.");
Console.WriteLine("Run: packer init . && packer build .");

return 0;
