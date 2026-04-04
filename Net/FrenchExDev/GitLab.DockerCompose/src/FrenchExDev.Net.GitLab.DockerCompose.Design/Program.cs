using FrenchExDev.Net.GitLab.DockerCompose.Design;
using FrenchExDev.Net.Wrapper.Versioning;

var outputDir = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..", "..",
    "resources"));

var pipeline = new DesignPipeline<string>()
    .UseHttpDownload(version =>
        $"https://gitlab.com/gitlab-org/omnibus-gitlab/-/raw/{version}%2Bce.0/files/gitlab-config-template/gitlab.rb.template")
    .UseSave()
    .Build();

return await new DesignPipelineRunner<string>
{
    ItemCollector = new GitLabTagsVersionCollector(
        "gitlab-org%2Fomnibus-gitlab",
        tagToVersion: tag =>
        {
            // Tags: "18.10.1+ce.0", "18.10.0+ee.0", "18.10.0+rc43.ce.0"
            // Keep only stable CE: ends with "+ce.0", no "rc"
            if (!tag.EndsWith("+ce.0") || tag.Contains("rc"))
                return null;
            return tag.Replace("+ce.0", ""); // "18.10.1"
        }),
    Pipeline = pipeline,
    KeySelector = v => v,
    ItemFilter = VersionFilters.LatestPatchPerMinor,
    OutputDir = outputDir,
    OutputFilePattern = "gitlab-{key}.rb",
    AuthTokenEnvVar = "GITLAB_TOKEN",
}.RunAsync(args);
