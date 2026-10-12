using FrenchExDev.Net.Wrapper.Versioning;

var outputDir = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..",
    "FrenchExDev.Net.Traefik.Bundle", "schemas"));

var schemas = new (string Name, string Url)[]
{
    ("static", "https://raw.githubusercontent.com/SchemaStore/schemastore/master/src/schemas/json/traefik-v3.json"),
    ("file-provider", "https://raw.githubusercontent.com/SchemaStore/schemastore/master/src/schemas/json/traefik-v3-file-provider.json"),
};

var pipeline = new DesignPipeline<(string Name, string Url)>()
    .UseHttpDownload(item => item.Url)
    .UseSave()
    .Build();

return await new DesignPipelineRunner<(string Name, string Url)>
{
    ItemCollector = new StaticItemCollector<(string Name, string Url)>(schemas),
    Pipeline = pipeline,
    KeySelector = item => item.Name,
    OutputDir = outputDir,
    OutputFilePattern = "traefik-v3-{key}.json",
}.RunAsync(args);
