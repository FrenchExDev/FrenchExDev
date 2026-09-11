using FrenchExDev.Net.Wrapper.Versioning;

var outputDir = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..",
    "FrenchExDev.Net.DockerCompose.Bundle", "schemas"));

var pipeline = new DesignPipeline<string>()
    .UseHttpDownload(v =>
        $"https://raw.githubusercontent.com/compose-spec/compose-go/v{v}/schema/compose-spec.json")
    .UseSave()
    .Build();

return await new DesignPipelineRunner<string>
{
    ItemCollector = new GitHubReleasesVersionCollector("compose-spec", "compose-go"),
    Pipeline = pipeline,
    KeySelector = v => v,
    ItemFilter = VersionFilters.LatestPatchPerMinor,
    OutputDir = outputDir,
    OutputFilePattern = "compose-spec-v{key}.json",
}.RunAsync(args);
