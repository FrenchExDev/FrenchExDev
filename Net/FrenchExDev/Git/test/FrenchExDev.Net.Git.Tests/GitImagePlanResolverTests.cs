using FrenchExDev.Net.Git.Design;
using Shouldly;

namespace FrenchExDev.Net.Git.Tests;

public sealed class GitImagePlanResolverTests
{
    [Theory]
    [InlineData("2.54.0", false)]
    [InlineData("2.55.0", true)]
    [InlineData("2.55.1", true)]
    public void SelectsDependenciesAtRustDefaultBoundary(string version, bool rust)
    {
        var resolver = new GitImagePlanResolver();
        var plan = resolver.Resolve(version);
        plan.ShouldBeSameAs(resolver.Plans[rust ? 1 : 0]);
        plan.BaseImage.ShouldBe("debian:bookworm");
        plan.BaseInstallScript.Contains(" cargo rustc").ShouldBe(rust);
        plan.InstallScript(version).ShouldContain($"/v{version}.tar.gz");
        plan.InstallScript(version).ShouldContain("make prefix=/usr/local -j$(nproc) all &&");
        plan.InstallScript(version).ShouldContain("make prefix=/usr/local install &&");
        plan.InstallScript(version).ShouldNotContain("NO_RUST");
    }

    [Fact]
    public void RustOnlyChangesTheDependencyRecipe()
    {
        var resolver = new GitImagePlanResolver();
        var legacy = resolver.Resolve("2.54.0");
        var modern = resolver.Resolve("2.55.0");
        modern.BaseInstallScript.ShouldBe(legacy.BaseInstallScript + " cargo rustc");
        modern.InstallScript("2.55.0").ShouldBe(legacy.InstallScript("2.55.0"));
    }
}
