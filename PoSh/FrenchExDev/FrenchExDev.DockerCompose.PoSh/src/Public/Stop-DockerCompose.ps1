function Stop-DockerCompose {
    [CmdletBinding()]
    param(
        [parameter(Mandatory = $false, Position = 0)] [string] $Service,
        [string] $Context,
        [switch] $Force
    )

    $contextConfigName = if ([string]::IsNullOrEmpty($context)) { Get-DockerComposeContext -Action GetActive } else { $Context }

    $processArgs = [System.Collections.ArrayList]::new()

    if ($Force) {
        $processArgs.Add("-f") | out-null
    }

    if (![string]::IsNullOrEmpty($service)) {
        $processArgs.Add($service) | out-null
    }

    Start-DockerCompose -Command stop -CommandArgs $processArgs -Context $contextConfigName
}
