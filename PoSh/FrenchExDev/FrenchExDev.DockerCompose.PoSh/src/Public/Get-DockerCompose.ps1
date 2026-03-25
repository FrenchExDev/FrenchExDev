enum DockerComposeAction {
    Build
    Down
    Exec
    Config
    Stop
    Rm
    Up
    Ps
    Pull
    Log
}

function Get-DockerCompose {
    [CmdletBinding()]
    [Alias("gdc")]
    param(
        [parameter(Mandatory = $true, Position = 0)] [DockerComposeAction[]] $Action,
        [parameter(Mandatory = $false, Position = 1)] [string] $Service,
        [string] $DockerContext,
        [string] $Context,
        [string] $Exec,
        [switch] $Force,
        [switch] $Follow,
        [switch] $Pull,
        [switch] $RemoveOrphans
    )

    $dockerComposeContextConfigName = if ([string]::IsNullOrEmpty($Context)) { Get-DockerComposeContext -Action GetActive } else { $Context }
    
    Write-Debug "Get-DockerCompose > Docker Compose Context : $dockerComposeContextConfigName"

    if(![string]::IsNullOrEmpty($DockerContext)) {
        Get-DockerContext SwitchTo -Name $DockerContext
        $dockerComposeContextConfigName = $DockerContext
    }

    foreach($currentAction in $Action) {
        switch([DockerComposeAction]$currentAction) {
            ([DockerComposeAction]::Build) {
                Build-DockerCompose -Context $dockerComposeContextConfigName -Service $service -Verbose:$VerbosePreference -Debug:$DebugPreference
            }
            ([DockerComposeAction]::Down) {
                Down-DockerCompose -Context $dockerComposeContextConfigName -Service $service -Verbose:$VerbosePreference -Debug:$DebugPreference
            }
            ([DockerComposeAction]::Exec) {
                Exec-DockerCompose -Context $dockerComposeContextConfigName -Service $service -Command $Exec -Verbose:$VerbosePreference -Debug:$DebugPreference
            }
            ([DockerComposeAction]::Config) {
                Get-DockerComposeConfig -Context $dockerComposeContextConfigName -Verbose:$VerbosePreference -Debug:$DebugPreference
            }
            ([DockerComposeAction]::Stop) {
                Stop-DockerCompose -Context $dockerComposeContextConfigName -Service $service -Force:$Force.IsPresent -Verbose:$VerbosePreference -Debug:$DebugPreference
            }
            ([DockerComposeAction]::Rm) {
                Rm-DockerCompose -Context $dockerComposeContextConfigName -Service $service -Force:$Force.IsPresent -Stop -Volumes -Verbose:$VerbosePreference -Debug:$DebugPreference
            }
            ([DockerComposeAction]::Up) {
                Up-DockerCompose -Context $dockerComposeContextConfigName -Service $service -RemoveOrphans:$RemoveOrphans.IsPresent -Verbose:$VerbosePreference -Debug:$DebugPreference
            }
            ([DockerComposeAction]::Ps) {
                Exec-DockerCompose -Context $dockerComposeContextConfigName -Service $Service -Command ps -Verbose:$VerbosePreference -Debug:$DebugPreference
            }
            ([DockerComposeAction]::Pull) {
                Pull-DockerCompose -Context $dockerComposeContextConfigName -Service $service -Verbose:$VerbosePreference -Debug:$DebugPreference
            }
            ([DockerComposeAction]::Log) {
                Log-DockerCompose -Context $dockerComposeContextConfigName -Service $service -Follow:$Follow.IsPresent -Timestamp -Verbose:$VerbosePreference -Debug:$DebugPreference
            }
            default {
                throw "Get-DockerCompose > Action '$currentAction' is not yet implemented"
            }
        }
    }
}
