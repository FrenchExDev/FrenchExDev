using FrenchExDev.Net.Podman.Design;
using Shouldly;

namespace FrenchExDev.Net.Podman.Tests;

public sealed class PodmanImagePlanResolverTests
{
    [Theory]
    [InlineData("4.3.1", "podman-remote-static.tar.gz", 0)]
    [InlineData("4.4.0", "podman-remote-static-linux_amd64.tar.gz", 1)]
    public void SelectsReleaseArchiveAtBoundary(string version, string asset, int index)
    {
        var resolver = new PodmanImagePlanResolver();
        var plan = resolver.Resolve(version);
        plan.ShouldBeSameAs(resolver.Plans[index]);
        plan.InstallScript(version).ShouldContain($"/v{version}/{asset}");
        plan.BaseImage.ShouldBe(resolver.Plans[0].BaseImage);
        plan.BaseInstallScript.ShouldBe(resolver.Plans[0].BaseInstallScript);
    }
}
