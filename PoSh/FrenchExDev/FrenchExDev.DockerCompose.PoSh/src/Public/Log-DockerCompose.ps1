function Log-DockerCompose {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '',Scope='Function')]
    [CmdletBinding()]
    param(
        [parameter(Mandatory = $false, Position = 0)] [string] $Service,
        [string] $Context,
        [switch] $Follow,
        [switch] $Timestamp
    )

    $processArgs = [System.Collections.ArrayList]::new()

    if ($Follow) {
        $processArgs.Add("--follow") | out-null
    }

    if ($timestamp) {
        $processArgs.Add("--timestamps") | Out-Null
    }

    if (![string]::IsNullOrEmpty($service)) {
        $processArgs.Add($service) | out-null
    }

    Start-DockerCompose -Command logs -CommandArgs $processArgs -Context $Context
}
