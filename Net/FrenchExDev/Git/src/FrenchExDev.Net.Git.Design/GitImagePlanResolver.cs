using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using FrenchExDev.Net.Wrapper.Versioning;

namespace FrenchExDev.Net.Git.Design;

/// <summary>Git 2.55 enables Rust by default; earlier releases only need the C toolchain.</summary>
public sealed class GitImagePlanResolver : IDesignImagePlanResolver
{
    public IReadOnlyList<DesignImagePlan> Plans { get; } = Array.AsReadOnly(new[]
    {
        CreatePlan(withRust: false),
        CreatePlan(withRust: true),
    });

    public DesignImagePlan Resolve(string version) =>
        Plans[GitHubReleasesVersionCollector.CompareVersionStrings(version, "2.55.0") >= 0 ? 1 : 0];

    private static DesignImagePlan CreatePlan(bool withRust) => new()
    {
        ImageName = "git-cli",
        BaseImage = "debian:bookworm",
        Platform = "linux/amd64",
        Shell = "bash",
        BaseInstallScript = "apt-get update -qq && apt-get install -y -qq make gcc libz-dev libcurl4-openssl-dev libssl-dev libexpat1-dev gettext curl"
            + (withRust ? " cargo rustc" : ""),
        InstallScript = version =>
            $"curl -fsSL https://github.com/git/git/archive/refs/tags/v{version}.tar.gz " +
            "-o /tmp/git.tar.gz && " +
            "tar xzf /tmp/git.tar.gz -C /tmp && " +
            $"cd /tmp/git-{version} && " +
            "make prefix=/usr/local -j$(nproc) all && " +
            "make prefix=/usr/local install && " +
            "rm -rf /tmp/git* && apt-get clean > /dev/null 2>&1",
    };
}
