function Get-DockerComposeConfig {
    [CmdletBinding()]
    param(
        [string] $Context
    )

    $contextConfigName = if ([string]::IsNullOrEmpty($context)) { Get-DockerComposeContext GetActive -Verbose:$VerbosePreference -Debug:$DebugPreference } else { $Context }
    Start-DockerCompose -Context $contextConfigName -Command config -Verbose:$VerbosePreference -Debug:$DebugPreference
}
