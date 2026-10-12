using FrenchExDev.Net.Wrapper.Versioning;

var outputDir = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..",
    "FrenchExDev.Net.GitLab.Ci.Yaml", "schemas"));

var pipeline = new DesignPipeline<string>()
    .UseHttpDownload(v =>
        $"https://gitlab.com/gitlab-org/gitlab/-/raw/v{v}-ee/app/assets/javascripts/editor/schema/ci.json")
    .UseSave()
    .Build();

return await new DesignPipelineRunner<string>
{
    ItemCollector = new GitLabReleasesVersionCollector(
        "gitlab-org%2Fgitlab",
        tagToVersion: tag =>
        {
            // Tags are "v18.10.0-ee" → strip v prefix and -ee suffix
            var v = tag.StartsWith('v') ? tag[1..] : tag;
            var eeIdx = v.IndexOf("-ee", StringComparison.Ordinal);
            return eeIdx >= 0 ? v[..eeIdx] : v;
        }),
    Pipeline = pipeline,
    KeySelector = v => v,
    ItemFilter = VersionFilters.LatestPatchPerMinor,
    OutputDir = outputDir,
    OutputFilePattern = "gitlab-ci-v{key}.json",
    DefaultMinVersion = "18.0.0",
}.RunAsync(args);
