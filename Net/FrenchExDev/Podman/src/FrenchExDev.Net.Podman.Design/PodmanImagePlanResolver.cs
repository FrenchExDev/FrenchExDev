using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using FrenchExDev.Net.Wrapper.Versioning;

namespace FrenchExDev.Net.Podman.Design;

/// <summary>Selects the release archive name introduced in Podman 4.4.</summary>
public sealed class PodmanImagePlanResolver : IDesignImagePlanResolver
{
    public IReadOnlyList<DesignImagePlan> Plans { get; } = Array.AsReadOnly(new[]
    {
        CreatePlan("podman-remote-static.tar.gz"),
        CreatePlan("podman-remote-static-linux_amd64.tar.gz"),
    });

    public DesignImagePlan Resolve(string version) =>
        Plans[GitHubReleasesVersionCollector.CompareVersionStrings(version, "4.4.0") >= 0 ? 1 : 0];

    private static DesignImagePlan CreatePlan(string asset) => new()
    {
        ImageName = "podman-cli",
        BaseImage = "alpine:3.19",
        Platform = "linux/amd64",
        BaseInstallScript = "apk add --no-cache curl tar",
        // Keep the archive outside the search directory so only extracted binaries are eligible.
        InstallScript = version =>
            $"curl -fsSL https://github.com/containers/podman/releases/download/v{version}/{asset} -o /tmp/podman.tar.gz && " +
            "mkdir -p /tmp/podman && " +
            "tar xzf /tmp/podman.tar.gz --no-same-owner -C /tmp/podman && " +
            "find /tmp/podman -name 'podman*' -type f | head -1 | xargs -I{} mv {} /usr/local/bin/podman && " +
            "chmod +x /usr/local/bin/podman && " +
            "podman --version && " +
            "rm -rf /tmp/podman.tar.gz /tmp/podman",
    };
}
