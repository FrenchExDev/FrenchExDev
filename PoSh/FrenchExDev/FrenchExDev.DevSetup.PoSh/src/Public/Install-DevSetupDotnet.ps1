function Install-DevSetupDotnet {
    [CmdletBinding()]
    param(
        [string[]] $DotnetVersions = @("9")
    )

    $DotnetSoftwaresNonVersioned = @(
        "Microsoft.DotNet.AspNetCore.#VERSION#",
        "Microsoft.DotNet.DesktopRuntime.#VERSION#",
        "Microsoft.DotNet.HostingBundle.#VERSION#",
        "Microsoft.DotNet.Runtime.#VERSION#",
        "Microsoft.DotNet.SDK.#VERSION#"
    )

    foreach ($dotnetSoftwareNonVersioned in $DotnetSoftwaresNonVersioned) {
        foreach ($dotnetVersion in $dotnetVersions) {
            $software = $dotnetSoftwareNonVersioned.Replace("#VERSION#", $dotnetVersion);
            Write-Verbose "Install-DevSetupDotnet> winget install --id $software --source winget"
            winget install --id $software --source winget
        }
    }
}
