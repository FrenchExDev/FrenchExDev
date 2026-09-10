using System.Text.RegularExpressions;

namespace FrenchExDev.Net.Dotnet.Design;

public static class DotnetSdk
{
    // SDK feature bands start at 100. dotnet/sdk also contains runtime-shaped
    // tags (e.g. v11.0.0), which are not installable SDK versions.
    public static string? VersionFromTag(string tag)
    {
        var match = Regex.Match(tag, @"^v?(\d+\.\d+\.[1-9]\d{2,})$");
        return match.Success ? match.Groups[1].Value : null;
    }

    public static readonly string[] EnvironmentVariables =
    [
        "DOTNET_ROOT=/usr/share/dotnet",
        "DOTNET_CLI_UI_LANGUAGE=en-US",
        "DOTNET_CLI_TELEMETRY_OPTOUT=1",
        "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1",
        "DOTNET_GENERATE_ASPNET_CERTIFICATE=false",
        "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true",
        "DOTNET_NOLOGO=1",
        "LANG=C.UTF-8",
    ];

    public const string BaseInstallScript =
        "apk add --no-cache bash ca-certificates curl icu-libs krb5-libs " +
        "libgcc libintl libssl3 libstdc++ tzdata zlib";

    public static string InstallScript(string version)
    {
        if (VersionFromTag(version) != version)
            throw new ArgumentException("A stable SDK version is required.", nameof(version));

        return "curl -fsSL --retry 3 https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh && " +
            $"bash /tmp/dotnet-install.sh --version {version} --architecture x64 --os linux-musl " +
            "--install-dir /usr/share/dotnet --no-path && " +
            "ln -s /usr/share/dotnet/dotnet /usr/local/bin/dotnet && " +
            "rm /tmp/dotnet-install.sh";
    }
}
