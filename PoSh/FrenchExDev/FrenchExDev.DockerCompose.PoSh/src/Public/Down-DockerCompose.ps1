function Down-DockerCompose {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '',Scope='Function')]
    [CmdletBinding()]
    param(
        [parameter(Mandatory = $false, Position = 0)] [string] $Service,
        [string] $Context,
        [switch] $NoDaemon
    )

    $contextConfigName = if ([string]::IsNullOrEmpty($context)) { Get-DockerComposeContext -Action GetActive } else { $Context }

    $processArgs = [System.Collections.ArrayList]::new()

    if (![string]::IsNullOrEmpty($service)) {
        $processArgs.Add($service)
    }

    Start-DockerCompose -Command down -CommandArgs $processArgs -Context $contextConfigName
}

