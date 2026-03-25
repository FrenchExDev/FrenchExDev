function Up-DockerCompose {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '',Scope='Function')]
    [CmdletBinding()]
    param(
        [parameter(Mandatory = $false, Position = 0)] [string] $Service,
        [string] $Context,
        [switch] $NoDaemon,
        [switch] $RemoveOrphans
    )

    $contextConfigName = if ([string]::IsNullOrEmpty($context)) { Get-DockerComposeContext GetActive } else { $Context }

    $processArgs = [System.Collections.ArrayList]::new()

    if ($NoDaemon -eq $false) {
        $processArgs.Add("-d") | Out-Null
    }

    if ($RemoveOrphans) {
        $processArgs.Add("--remove-orphans") | Out-Null
    }

    if (![string]::IsNullOrEmpty($service)) {
        $processArgs.Add($service)
    }

    Start-DockerCompose -Command up -CommandArgs $processArgs -Context $contextConfigName
}
