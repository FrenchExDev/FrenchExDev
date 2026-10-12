function Query-DockerComposeConfig {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '',Scope='Function')]
    [CmdletBinding()]
    param(
        [parameter(Mandatory = $true)] [string] $Query,
        [string] $Context
    )

    $contextConfigName = if ([string]::IsNullOrEmpty(($Context))) { Get-DockerComposeContext -Action GetActive } else { $Context }

    Get-DockerComposeConfig -Context $contextConfigName | yq $query
}
