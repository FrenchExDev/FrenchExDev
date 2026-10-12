function Get-DockerComposeServiceName {
    [CmdletBinding()]
    param(
        [string] $Context
    )

    $contextConfigName = if ([string]::IsNullOrEmpty($Context)) { Get-DockerComposeContext -Action GetActive } else { $Context }
    $contextConfig = Get-DockerComposeContext -Name $contextConfigName

    Get-DockerComposeConfig -Context $contextConfig | yq '.services | keys[]'
}
