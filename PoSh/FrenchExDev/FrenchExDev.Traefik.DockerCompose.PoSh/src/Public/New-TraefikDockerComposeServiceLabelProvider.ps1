function New-TraefikDockerComposeServiceLabelProvider {
    [CmdletBinding()]
    param(
        [string] $Name,
        [string] $Key,
        [string] $Value
    )

    "--providers.${Name}.${Key}=${Value}"
}
