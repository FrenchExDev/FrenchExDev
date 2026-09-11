using FrenchExDev.Net.Packer.Alpine;
using FrenchExDev.Net.Packer.Alpine.DockerHost;
using FrenchExDev.Net.Packer.Bundle;

namespace FrenchExDev.Net.Packer.Alpine.DockerHost.Tests;

public class DockerContributorTests
{
    private static PackerBundle CreateAlpineDockerBundle()
    {
        var bundle = new PackerBundle();
        bundle.Apply(
            new AlpineBaseContributor(),
            new DockerContributor());
        return bundle;
    }

    [Fact]
    public void Adds06DockerScript()
    {
        var bundle = CreateAlpineDockerBundle();

        bundle.Scripts.ShouldContain(s => s.Name == "06docker");
        var docker = bundle.Scripts.First(s => s.Name == "06docker");
        docker.Content.ShouldContain("apk add docker");
        docker.Content.ShouldContain("docker-cli-compose");
        docker.Content.ShouldContain("openrc");
        docker.Content.ShouldContain("addgroup vagrant docker");
    }

    [Fact]
    public void DockerScript_SortedBetween05And08()
    {
        var bundle = CreateAlpineDockerBundle();

        var names = bundle.Scripts.Select(s => s.Name).ToList();
        var idx06 = names.IndexOf("06docker");
        var idx05 = names.IndexOf("05cron");
        var idx08 = names.IndexOf("08virtualbox-guest-additions");

        idx06.ShouldBeGreaterThan(idx05);
        idx06.ShouldBeLessThan(idx08);
    }

    [Fact]
    public void TotalScripts_Is12()
    {
        var bundle = CreateAlpineDockerBundle();
        bundle.Scripts.Count().ShouldBe(12); // 11 Alpine + 1 Docker
    }

    [Fact]
    public void AddsDockerBridgeEnvVariable()
    {
        var bundle = CreateAlpineDockerBundle();

        bundle.EnvTemplate.Variables.ShouldContain(v => v.Key == "DOCKER_BRIDGE");
    }

    [Fact]
    public void ShellProvisionerIncludesDockerScript()
    {
        var bundle = CreateAlpineDockerBundle();

        var build = bundle.Build.Build();
        var shell = build.Provisioners.First(p => p.Type == "shell");
        var scripts = shell.Arguments["scripts"] as List<string>;

        scripts.ShouldNotBeNull();
        scripts.ShouldContain("scripts/06docker.sh");
    }

    [Fact]
    public async Task FullPipeline_WritesToDisk()
    {
        var bundle = CreateAlpineDockerBundle();
        var outputDir = Path.Combine(Path.GetTempPath(), $"docker-test-{Guid.NewGuid():N}");

        try
        {
            await new PackerBundleWriter().WriteAsync(bundle, outputDir);

            File.Exists(Path.Combine(outputDir, "scripts", "06docker.sh")).ShouldBeTrue();
            File.Exists(Path.Combine(outputDir, "scripts", "00base.sh")).ShouldBeTrue();
            File.Exists(Path.Combine(outputDir, "build.pkr.hcl")).ShouldBeTrue();

            var dockerScript = File.ReadAllText(Path.Combine(outputDir, "scripts", "06docker.sh"));
            dockerScript.ShouldContain("docker");
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }
}
