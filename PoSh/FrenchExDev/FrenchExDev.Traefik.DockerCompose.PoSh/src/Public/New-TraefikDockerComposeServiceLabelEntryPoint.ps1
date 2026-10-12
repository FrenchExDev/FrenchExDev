function New-TraefikDockerComposeServiceLabelEntryPoint {
    [CmdletBinding()]
    param(
        [string] $Name,
        [string] $Key,
        [string] $Value
    )

    "--entryPoints.${Name}.${Key}=${Value}"
}
