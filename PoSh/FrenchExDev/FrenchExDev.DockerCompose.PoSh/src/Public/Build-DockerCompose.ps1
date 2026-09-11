function Build-DockerCompose {
    [CmdletBinding()]
    param(
        [parameter(Mandatory = $false, Position = 0)] [string] $Service,
        [string] $Context
    )

    $contextConfigName = if ([string]::IsNullOrEmpty($context)) { Get-DockerComposeContext -Action GetActive } else { $Context }

    $processArgs = [System.Collections.ArrayList]::new()

    if (![string]::IsNullOrEmpty($service)) {
        $processArgs.Add($service) | Out-Null
    }

    Start-DockerCompose -Command build -CommandArgs $processArgs -Context $contextConfigName
}
