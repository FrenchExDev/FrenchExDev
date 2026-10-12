function Rm-DockerCompose {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '',Scope='Function')]
    [CmdletBinding()]
    param(
        [parameter(Mandatory = $false, Position = 0)] [string] $Service,
        [string] $Context,
        [switch] $Force,
        [switch] $Stop,
        [switch] $Volumes
    )
    
    $contextConfigName = if ([string]::IsNullOrEmpty($context)) { Get-DockerComposeContext -Action GetActive } else { $Context }

    $processArgs = [System.Collections.ArrayList]::new()

    if ($Force) {
        $processArgs.Add("--force") | out-null
    }

    if ($Stop) {
        $processArgs.Add("--stop") | out-null
    }

    if ($Volumes) {
        $processArgs.Add("--volumes") | out-null
    }

    if (![string]::IsNullOrEmpty($Service)) {
        $processArgs.Add($Service) | out-null
    }

    Start-DockerCompose -Command rm -CommandArgs $processArgs -Context $contextConfigName -Verbose:$VerbosePreference
}
