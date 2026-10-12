function Exec-DockerCompose {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '',Scope='Function')]
    [CmdletBinding()]
    param(
        [parameter(Mandatory = $false, Position = 0)] [string] $Service,
        [parameter(Mandatory = $false, Position = 1)] [string] $Command,
        [int] $ServiceInstance,
        [string] $Context,
        [switch] $Force,
        [switch] $Detach,
        [string] $Env,
        [switch] $NoTty,
        [string] $User,
        [string] $WorkDir
    )
    
    $contextConfigName = if ([string]::IsNullOrEmpty($context)) { Get-DockerComposeContext -Action GetActive } else { $Context }

    $processArgs = [System.Collections.ArrayList]::new()

    if ($Detach) {
        $processArgs.Add("--detach") | Out-Null
    }

    if (![string]::IsNullOrEmpty($env)) {
        $processArgs.Add("--env") | Out-Null
        $processArgs.Add($Env) | Out-Null
    }

    if ($NoTty) {
        $processArgs.Add("--no-tty") | Out-Null
    }

    if (![string]::IsNullOrEmpty($user)) {
        $processArgs.Add("--user") | Out-Null
        $processArgs.Add($user) | Out-Null
    }

    if (![string]::IsNullOrEmpty($WorkDir)) {
        $processArgs.Add("--workdir") | Out-Null
        $processArgs.Add($WorkDir) | Out-Null
    }

    if($ServiceInstance -gt 0) {
        $processArgs.Add("--index") |Out-Null
        $processArgs.Add($ServiceInstance) |Out-Null
    }

    if (![string]::IsNullOrEmpty($Service)) {
        $processArgs.Add($Service) | out-null
    }

    $processArgs.Add($command) | Out-Null

    Start-DockerCompose -Command exec -CommandArgs $processArgs -Context $contextConfigName
}
