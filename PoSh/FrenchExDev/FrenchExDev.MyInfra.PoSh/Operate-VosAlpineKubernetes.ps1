[CmdletBinding()]
param(
    [ValidateSet("Up", "Halt")] [string] $Operation
)

$VerboseAndDebug = @{
    Verbose = $VerbosePreference
    Debug = $DebugPreference
}

Push-Location src

Get-VosAlpineKubernetes $Operation -All @VerboseAndDebug

Pop-Location
