namespace FrenchExDev.Net.Diem.Cli.Lib;

/// <summary>
/// CLI command registry. All cmf commands are registered here.
/// </summary>
public static class CmfCommands
{
    public static readonly string[] AvailableCommands =
    [
        "new", "add", "generate", "validate", "migrate", "report", "design", "install", "list", "uninstall"
    ];
}
