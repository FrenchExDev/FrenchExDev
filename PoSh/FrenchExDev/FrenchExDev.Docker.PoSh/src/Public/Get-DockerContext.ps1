enum DockerContextAction {
    New
    Remove
    SwitchTo
}
function Get-DockerContext {
    [CmdletBinding()]
    param(
        [parameter(mandatory = $false, position = 0)] [DockerContextAction[]] $Action,
        [parameter(mandatory = $false, position = 1)] [string] $Name,
        [parameter(mandatory = $false, position = 2)] [string] $HostName,
        [parameter(Mandatory = $false, Position = 3)] [string] $RemoteUser,
        [switch] $Docker,
        [switch] $Ssh,
        [switch] $SwitchTo,
        [switch] $Force
    )

    foreach ($currentAction in $Action) {
        switch ([DockerContextAction]$currentAction) {
            ([DockerContextAction]::New) {
                New-DockerContext -Name $Name -HostName "$HostName" -RemoteUser "$RemoteUser" -Docker:$Docker.IsPresent -Ssh:$Ssh.IsPresent -SwitchTo:$SwitchTo.IsPresent
            }
            ([DockerContextAction]::Remove) {
                Remove-DockerContext -Name $Name -Force:$Force.IsPresent
            }
            ([DockerContextAction]::SwitchTo) {
                Switch-DockerContext -Name $Name
            }
            default {
                throw "Get-DockerContext > Action '$currentAction' not implemented"
            }
        }
    }
}
