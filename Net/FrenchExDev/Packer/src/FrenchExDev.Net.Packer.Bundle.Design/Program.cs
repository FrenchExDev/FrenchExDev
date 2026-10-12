using System.Text.Json;
using FrenchExDev.Net.Packer.Bundle.Design;
using FrenchExDev.Net.Wrapper.Versioning;

var outputDir = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..",
    "FrenchExDev.Net.Packer.Bundle", "scrape"));

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
};

var pipeline = new DesignPipeline<PluginRegistryEntry>()
    .UseHttpDownload(entry =>
        $"https://raw.githubusercontent.com/{entry.Org}/{entry.Repo}/main/{entry.Path}")
    .UseContentTransform((entry, goSource) =>
    {
        var (fields, nestedBlocks) = Hcl2SpecParser.Parse(goSource);
        return JsonSerializer.Serialize(new ScrapedPluginType
        {
            PluginKind = entry.PluginKind,
            TypeName = entry.TypeName,
            Repo = $"{entry.Org}/{entry.Repo}",
            SourcePath = entry.Path,
            Fields = fields,
            NestedBlocks = nestedBlocks,
        }, jsonOptions);
    })
    .UseSave()
    .Build();

return await new DesignPipelineRunner<PluginRegistryEntry>
{
    ItemCollector = new StaticItemCollector<PluginRegistryEntry>(PluginRegistry.Entries),
    Pipeline = pipeline,
    KeySelector = e => e.TypeName,
    OutputDir = outputDir,
    OutputFilePattern = "{key}.json",
}.RunAsync(args);
